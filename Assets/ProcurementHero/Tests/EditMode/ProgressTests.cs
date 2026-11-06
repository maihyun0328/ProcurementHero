using NUnit.Framework;
using ProcurementHero.Core;

namespace ProcurementHero.Tests
{
    public class ProgressTests
    {
        static readonly MissionSpec Mission = new MissionSpec("t", 140, 2, 70);
        static readonly ProductSpec Good = new ProductSpec("g", "g", ProductType.Efficient, 50, 5, 90, 1, 90);
        static readonly ProductSpec Weak = new ProductSpec("w", "w", ProductType.Basic, 30, 5, 50, 1, 30);

        static PurchaseEvaluation Win() => Scoring.Evaluate(Mission, Good, Good);
        static PurchaseEvaluation Lose() => Scoring.Evaluate(Mission, Weak, Weak);

        [Test]
        public void RetryReplacesResultWithoutDoubleCounting()
        {
            var p = new GameProgress(3);
            var first = Lose();
            p.SetResult(0, first);
            Assert.AreEqual(first.TotalScore, p.TotalScore, 1e-9);

            var retry = Win();
            p.SetResult(0, retry);
            Assert.AreEqual(retry.TotalScore, p.TotalScore, 1e-9);
            Assert.AreEqual(1, p.SuccessCount);
        }

        [Test]
        public void TotalIsSumOfAdoptedResults()
        {
            var p = new GameProgress(3);
            p.SetResult(0, Win());
            p.SetResult(1, Lose());
            p.SetResult(2, Win());
            Assert.AreEqual(Win().TotalScore * 2 + Lose().TotalScore, p.TotalScore, 1e-9);
            Assert.IsTrue(p.IsComplete);
        }

        [Test]
        public void EndingsByAdoptedSuccessCount()
        {
            var p = new GameProgress(3);
            p.SetResult(0, Win()); p.SetResult(1, Win()); p.SetResult(2, Win());
            Assert.AreEqual(EndingKind.Hero, p.Ending);
            p.SetResult(2, Lose());
            Assert.AreEqual(EndingKind.Growing, p.Ending);
            p.SetResult(1, Lose());
            Assert.AreEqual(EndingKind.TryAgain, p.Ending);
            p.SetResult(0, Lose());
            Assert.AreEqual(EndingKind.TryAgain, p.Ending);
        }

        [Test]
        public void MissionsUnlockInOrderAndResetClears()
        {
            var p = new GameProgress(3);
            Assert.IsTrue(p.IsUnlocked(0));
            Assert.IsFalse(p.IsUnlocked(1));
            p.SetResult(0, Lose());
            Assert.IsTrue(p.IsUnlocked(1), "부분 해결이어도 다음 미션으로 갈 수 있어야 한다");
            Assert.IsFalse(p.IsUnlocked(2));
            p.Reset();
            Assert.IsFalse(p.HasAnyResult);
            Assert.AreEqual(0, p.TotalScore);
            Assert.IsFalse(p.IsUnlocked(1));
        }
    }
}
