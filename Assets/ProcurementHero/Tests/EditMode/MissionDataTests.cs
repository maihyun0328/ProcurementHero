using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcurementHero.Core;
using ProcurementHero.EditorTools;
using UnityEditor;

namespace ProcurementHero.Tests
{
    /// <summary>Setup Game이 만든 실제 데이터 에셋으로 3×3 조합 전체를 확인한다.</summary>
    public class MissionDataTests
    {
        GameDatabase database;

        [OneTimeSetUp]
        public void Load()
        {
            database = AssetDatabase.LoadAssetAtPath<GameDatabase>(GameSetup.DatabasePath);
            Assert.IsNotNull(database, "Tools/Procurement Hero/Setup Game을 먼저 실행해야 합니다.");
        }

        MissionData Mission(string id) => database.missions.First(m => m.id == id);

        static IEnumerable<PurchaseEvaluation> All(MissionData m) =>
            from a in m.productsA from b in m.productsB select Scoring.Evaluate(m.ToSpec(), a.ToSpec(), b.ToSpec());

        [Test]
        public void ThreeMissionsWithThreeByThreeProducts()
        {
            Assert.AreEqual(3, database.missions.Count);
            foreach (var m in database.missions)
            {
                Assert.AreEqual(3, m.productsA.Count, m.name);
                Assert.AreEqual(3, m.productsB.Count, m.name);
                Assert.AreEqual(9, All(m).Count());
            }
        }

        [Test]
        public void EveryCombinationCostIsSumOfFourParts()
        {
            foreach (var m in database.missions)
            foreach (var a in m.productsA)
            foreach (var b in m.productsB)
            {
                var e = Scoring.Evaluate(m.ToSpec(), a.ToSpec(), b.ToSpec());
                Assert.AreEqual(a.purchaseCost + a.maintenanceCost + b.purchaseCost + b.maintenanceCost, e.TotalCost);
                Assert.That(e.TotalScore, Is.InRange(0.0, 100.0));
            }
        }

        [Test]
        public void EachMissionHasAtLeastOneSuccessfulCombinationWithinBudget()
        {
            foreach (var m in database.missions)
                Assert.IsTrue(All(m).Any(e => e.Success && e.WithinBudget), m.title);
        }

        [Test]
        public void Mission2BothEfficientIsOverBudget()
        {
            var m = Mission("m2");
            var a = m.productsA.Single(p => p.type == ProductType.Efficient);
            var b = m.productsB.Single(p => p.type == ProductType.Efficient);
            var e = Scoring.Evaluate(m.ToSpec(), a.ToSpec(), b.ToSpec());
            Assert.AreEqual(145, e.TotalCost);
            Assert.IsFalse(e.WithinBudget);
            Assert.AreEqual(PurchaseBlockReason.OverBudget, Scoring.GetBlockReason(m.ToSpec(), a.ToSpec(), b.ToSpec()));
        }

        [Test]
        public void Mission3TwoDayDeliveryIsLate()
        {
            var m = Mission("m3");
            var spec = m.ToSpec();
            var twoDay = m.productsA.Concat(m.productsB).Where(p => p.deliveryDays == 2).ToList();
            Assert.AreEqual(2, twoDay.Count);
            foreach (var p in twoDay)
                Assert.AreEqual(0.4, Scoring.DeliveryFactor(spec, p.ToSpec()), 1e-9, p.displayName);
            foreach (var e in All(m).Where(e => e.ProductA.DeliveryDays == 2 || e.ProductB.DeliveryDays == 2))
                Assert.IsFalse(e.Success);
        }

        [Test]
        public void ProductIdsAreUnique()
        {
            var ids = database.missions.SelectMany(m => m.productsA.Concat(m.productsB)).Select(p => p.id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count());
        }

        [Test]
        public void ValidatorFindsNoDataErrors()
        {
            var report = new GameValidator.Report();
            GameValidator.ValidateData(database, report);
            Assert.IsTrue(report.Ok, report.ToString());
        }
    }
}
