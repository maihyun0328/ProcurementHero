using System.Linq;
using NUnit.Framework;
using ProcurementHero.Core.Field;
using ProcurementHero.EditorTools;
using ProcurementHero.Field;
using UnityEditor;
using UnityEngine;

namespace ProcurementHero.Tests
{
    /// <summary>Setup Field Notebook이 만든 실제 데이터 에셋 검사. 미션 2·3 수치는 개발 초안이다.</summary>
    public class FieldDataTests
    {
        FieldDatabase database;

        [OneTimeSetUp]
        public void Load()
        {
            database = AssetDatabase.LoadAssetAtPath<FieldDatabase>(FieldSetup.DatabasePath);
            Assert.IsNotNull(database, "Tools/Procurement Hero/Setup Field Notebook을 먼저 실행해야 합니다.");
        }

        FieldMissionData M(string id) => database.missions.First(m => m.id == id);

        [Test]
        public void Mission1AssetMatchesDesignDocument()
        {
            var m = M("f1");
            Assert.AreEqual(75, m.budget);
            Assert.IsFalse(m.draftNumbers);
            Assert.AreEqual(35, m.Item("streetlamp").cost);
            Assert.AreEqual(20, m.Item("walklight").cost);
            CollectionAssert.AreEquivalent(new[] { "A", "B" }, m.Spot("entrance").options.First(o => o.itemId == "streetlamp").solves);
            CollectionAssert.AreEquivalent(new[] { "B", "C" }, m.Spot("stairtop").options.First(o => o.itemId == "streetlamp").solves);
            CollectionAssert.AreEquivalent(new[] { "D" }, m.Spot("park").options.First(o => o.itemId == "walklight").solves);
            CollectionAssert.AreEquivalent(new[] { "A", "B", "C" }, m.problems.Where(p => p.mustObserve).Select(p => p.id));
            Assert.AreEqual(27, FieldRules.AllPlans(m.ToSpec()).Count());
        }

        [Test]
        public void EveryMissionHasSuccessAndThreeStarPlanWithinBudget()
        {
            foreach (var m in database.missions)
            {
                var spec = m.ToSpec();
                var valid = FieldRules.AllPlans(spec).Select(p => FieldRules.Evaluate(spec, p)).Where(e => e.Assignments.Count > 0 && e.WithinBudget).ToList();
                Assert.IsTrue(valid.Any(e => e.Success), m.title + ": 성공 조합");
                Assert.IsTrue(valid.Any(e => e.Stars == 3), m.title + ": 별 3개 조합");
                Assert.IsTrue(valid.Any(e => e.Stars <= 1), m.title + ": 실패도 가능해야 선택이 의미 있다");
            }
        }

        [Test]
        public void Mission2And3AreMarkedAsDraft()
        {
            Assert.IsTrue(M("f2").draftNumbers);
            Assert.IsTrue(M("f3").draftNumbers);
        }

        [Test]
        public void ExpensiveOnlyPlanLeavesARequiredProblemInMission2()
        {
            var m = M("f2");
            var spec = m.ToSpec();
            var plan = new FieldPlan();
            plan.Toggle(spec, "waiting", "aircon");
            plan.Toggle(spec, "corridor", "aircon");
            var e = FieldRules.Evaluate(spec, plan);
            Assert.AreEqual(80, e.Cost);
            Assert.AreEqual(1, e.Stars, "냉방장치만 두 개면 급수 문제가 남는다");
        }

        [Test]
        public void ValidatorPassesIncludingReachability()
        {
            var report = new GameValidator.Report();
            FieldValidator.Validate(database, report);
            Assert.IsTrue(report.Ok, report.ToString());
        }

        [Test]
        public void ObservePointsAreReachableAndBuildingsBlock()
        {
            var m = M("f1");
            foreach (var p in m.problems) Assert.IsTrue(FieldValidator.Reachable(m, p.observePoint, 1.3f), p.displayName);
            Assert.IsFalse(FieldValidator.Walkable(m, new Vector2(-12f, 3f)), "건물 한가운데는 지나갈 수 없다");
        }

        [Test]
        public void FieldRecordsUseVersionedKeysSeparateFromOldScore()
        {
            var original = SaveService.Store;
            var store = new MemoryStore();
            SaveService.Store = store;
            try
            {
                SaveService.SubmitRecord(150, 2);
                Assert.IsTrue(FieldRecords.SubmitMission("f1", 2));
                Assert.IsFalse(FieldRecords.SubmitMission("f1", 2), "같은 별은 새 기록이 아니다");
                Assert.IsFalse(FieldRecords.SubmitMission("f1", 1));
                Assert.IsTrue(FieldRecords.SubmitMission("f1", 3));
                Assert.AreEqual(3, FieldRecords.BestStars("f1"));
                Assert.IsTrue(FieldRecords.SubmitTotal(7));
                Assert.AreNotEqual("PH_BestScore", FieldRecords.TotalKey);
                Assert.IsTrue(FieldRecords.TotalKey.StartsWith("PH2_"));

                FieldRecords.Reset(new[] { "f1", "f2", "f3" });
                Assert.AreEqual(-1, FieldRecords.BestTotal);
                Assert.IsTrue(SaveService.HasBest, "새 기록 초기화가 이전 버전 점수를 지우면 안 된다");
                Assert.AreEqual(150f, SaveService.BestScore);
            }
            finally
            {
                SaveService.Store = original;
            }
        }
    }
}
