using NUnit.Framework;
using ProcurementHero.Core;

namespace ProcurementHero.Tests
{
    public class ScoringTests
    {
        static readonly MissionSpec Mission = new MissionSpec("t", 140, 2, 70);
        static ProductSpec P(int buy, int keep, int perf, int days, int eco) =>
            new ProductSpec("p" + buy + keep + perf + days + eco, "p", ProductType.Basic, buy, keep, perf, days, eco);

        [Test]
        public void TotalCostIsPurchasePlusMaintenance()
        {
            var e = Scoring.Evaluate(Mission, P(40, 15, 70, 1, 40), P(65, 5, 85, 2, 90));
            Assert.AreEqual(105, e.PurchaseCostSum);
            Assert.AreEqual(20, e.MaintenanceCostSum);
            Assert.AreEqual(125, e.TotalCost);
        }

        [Test]
        public void ScoreFormulaMatchesDesign()
        {
            var e = Scoring.Evaluate(Mission, P(40, 15, 70, 1, 40), P(40, 15, 70, 1, 40));
            Assert.AreEqual(42.0, e.PerformanceScore, 1e-9);
            Assert.AreEqual(6.0, e.EcoScore, 1e-9);
            Assert.AreEqual(30.0 / 140.0 * 25.0, e.BudgetScore, 1e-9);
            Assert.AreEqual(42.0 + 6.0 + 30.0 / 140.0 * 25.0, e.TotalScore, 1e-9);
            Assert.AreEqual("53.4", Scoring.Format1(e.TotalScore));
        }

        [Test]
        public void LateDeliveryUsesFactor04()
        {
            var late = P(40, 5, 80, 3, 50);
            Assert.AreEqual(0.4, Scoring.DeliveryFactor(Mission, late), 1e-9);
            var e = Scoring.Evaluate(Mission, late, late);
            Assert.AreEqual(0.8 * 0.4 * 60.0, e.PerformanceScore, 1e-9);
            Assert.IsFalse(e.OnTimeA);
            Assert.IsFalse(e.Success);
            Assert.AreEqual(ItemOutcome.Late, e.OutcomeA);
        }

        [Test]
        public void BudgetScoreIsClampedAtZeroWhenOverBudget()
        {
            var e = Scoring.Evaluate(Mission, P(80, 10, 90, 1, 90), P(80, 10, 90, 1, 90));
            Assert.AreEqual(180, e.TotalCost);
            Assert.AreEqual(0.0, e.BudgetScore, 1e-9);
            Assert.IsFalse(e.WithinBudget);
            Assert.IsFalse(e.Success);
        }

        [Test]
        public void ExactBudgetIsAllowed()
        {
            var a = P(65, 5, 85, 2, 95);
            Assert.AreEqual(PurchaseBlockReason.None, Scoring.GetBlockReason(Mission, a, a));
            Assert.IsTrue(Scoring.Evaluate(Mission, a, a).WithinBudget);
        }

        [Test]
        public void BlockReasons()
        {
            var a = P(40, 15, 70, 1, 40);
            var big = P(100, 10, 90, 1, 90);
            Assert.AreEqual(PurchaseBlockReason.MissingBoth, Scoring.GetBlockReason(Mission, null, null));
            Assert.AreEqual(PurchaseBlockReason.MissingB, Scoring.GetBlockReason(Mission, a, null));
            Assert.AreEqual(PurchaseBlockReason.MissingA, Scoring.GetBlockReason(Mission, null, a));
            Assert.AreEqual(PurchaseBlockReason.OverBudget, Scoring.GetBlockReason(Mission, big, a));
            Assert.AreEqual(PurchaseBlockReason.None, Scoring.GetBlockReason(Mission, a, a));
        }

        [Test]
        public void ShortPerformanceIsWarningNotBlock()
        {
            var weak = P(30, 5, 60, 1, 50);
            Assert.AreEqual(PurchaseBlockReason.None, Scoring.GetBlockReason(Mission, weak, weak));
            var e = Scoring.Evaluate(Mission, weak, weak);
            Assert.IsFalse(e.Success);
            Assert.AreEqual(ItemOutcome.ShortPerformance, e.OutcomeA);
        }

        [Test]
        public void EvaluationIsDeterministic()
        {
            var a = P(55, 5, 78, 2, 80);
            var b = P(65, 5, 85, 2, 90);
            var first = Scoring.Evaluate(Mission, a, b);
            var second = Scoring.Evaluate(Mission, a, b);
            Assert.AreEqual(first.TotalScore, second.TotalScore);
            Assert.AreEqual(first.TotalCost, second.TotalCost);
            Assert.AreEqual(first.Success, second.Success);
        }

        [Test]
        public void FeedbackSaysWhenBudgetSavedButNotSolved()
        {
            var weak = P(30, 5, 60, 1, 50);
            var report = Feedback.Build(Scoring.Evaluate(Mission, weak, weak), "A", "B");
            StringAssert.Contains("예산은 아꼈지만", report.Headline);
            Assert.IsNotEmpty(report.Regret);
        }
    }
}
