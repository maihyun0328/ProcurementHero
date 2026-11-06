using System.Linq;
using NUnit.Framework;
using ProcurementHero.Core.Field;
using ProcurementHero.EditorTools;
using Object = UnityEngine.Object;
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

        static FieldEvaluation Eval(FieldMissionData m, params (string spot, string item)[] picks)
        {
            var spec = m.ToSpec();
            var plan = new FieldPlan();
            foreach (var (spot, item) in picks) Assert.AreNotEqual(AssignOutcome.NotAllowed, plan.Toggle(spec, spot, item), $"{spot}={item}");
            return FieldRules.Evaluate(spec, plan);
        }

        [Test]
        public void Mission2SamePlaceItemHasDifferentEffectByPosition()
        {
            var m = M("f2");
            // 냉방장치: 대기실 안쪽은 대기실만, 현관 안쪽은 열린 문으로 바깥 줄까지.
            CollectionAssert.AreEquivalent(new[] { "H" }, Eval(m, ("waiting", "aircon")).Solved);
            CollectionAssert.AreEquivalent(new[] { "H", "O" }, Eval(m, ("hall", "aircon")).Solved);
            // 실외(앞마당)에는 냉방장치를 놓을 수 없고 그늘막만 된다. 복도 쉼터 냉방은 대기실까지 가지 않으므로 허용하지 않는다.
            var spec = m.ToSpec();
            Assert.AreEqual(AssignOutcome.NotAllowed, new FieldPlan().Toggle(spec, "yard", "aircon"));
            Assert.AreEqual(AssignOutcome.NotAllowed, new FieldPlan().Toggle(spec, "corridor", "aircon"));
            CollectionAssert.AreEquivalent(new[] { "O" }, Eval(m, ("yard", "shade")).Solved);
        }

        [Test]
        public void Mission2HasSeveralWaysAndStarDifference()
        {
            var m = M("f2");
            var twoStar = Eval(m, ("waiting", "aircon"), ("corridor", "water"));
            Assert.AreEqual(65, twoStar.Cost);
            Assert.AreEqual(2, twoStar.Stars);
            var threeA = Eval(m, ("hall", "aircon"), ("corridor", "water"));
            Assert.AreEqual(3, threeA.Stars);
            Assert.AreEqual(65, threeA.Cost);
            var threeB = Eval(m, ("waiting", "aircon"), ("hall", "water"), ("yard", "shade"));
            Assert.AreEqual(3, threeB.Stars);
            Assert.AreEqual(85, threeB.Cost);
            Assert.IsTrue(threeB.WithinBudget);
        }

        [Test]
        public void Mission3DoorSpotIsAContestedChoice()
        {
            var m = M("f3");
            // 정문 앞: 싼 이동형 발전기(P)와 배수펌프(R)가 같은 자리를 두고 겨룬다.
            CollectionAssert.AreEquivalent(new[] { "P" }, Eval(m, ("door", "portable")).Solved);
            CollectionAssert.AreEquivalent(new[] { "R" }, Eval(m, ("door", "pump")).Solved);
            var cheap = Eval(m, ("door", "portable"), ("gate", "pump"), ("detour", "sandbags"));
            Assert.AreEqual(70, cheap.Cost);
            Assert.AreEqual(3, cheap.Stars);
            var fixedPower = Eval(m, ("panel", "generator"), ("door", "pump"), ("detour", "sandbags"));
            Assert.AreEqual(85, fixedPower.Cost);
            Assert.AreEqual(3, fixedPower.Stars);
            Assert.AreEqual(2, Eval(m, ("panel", "generator"), ("door", "pump")).Stars);
            Assert.IsFalse(Eval(m, ("panel", "generator"), ("gate", "pump"), ("detour", "pump")).WithinBudget, "펌프 두 대 + 비상전원은 예산 초과");
        }

        [Test]
        public void CheapestItemEverywhereIsNotAWinningShortcut()
        {
            foreach (var id in new[] { "f2", "f3" })
            {
                var m = M(id);
                var spec = m.ToSpec();
                var plan = new FieldPlan();
                foreach (var spot in m.spots)
                {
                    var cheapest = spot.options.Select(o => m.Item(o.itemId)).OrderBy(i => i.cost).First();
                    plan.Toggle(spec, spot.id, cheapest.id);
                }
                var e = FieldRules.Evaluate(spec, plan);
                Assert.IsFalse(e.WithinBudget && e.Stars == 3, m.title + ": 모든 자리에 가장 싼 물품만 고르면 끝나면 안 된다");
            }
        }

        [Test]
        public void Missions2And3HaveMoreThanOneWayToSolveRequired()
        {
            foreach (var id in new[] { "f2", "f3" })
            {
                var m = M(id);
                var spec = m.ToSpec();
                var ways = FieldRules.AllPlans(spec).Select(p => FieldRules.Evaluate(spec, p))
                    .Where(e => e.WithinBudget && e.AllRequiredSolved)
                    .Select(e => string.Join(",", e.Assignments.OrderBy(a => a.Key).Select(a => a.Key + "=" + a.Value)))
                    .Distinct().ToList();
                Assert.GreaterOrEqual(ways.Count, 3, m.title);
                Assert.IsTrue(m.spots.Any(s => s.options.Count >= 2), m.title + ": 고를 물품이 둘 이상인 자리가 있어야 한다");
            }
        }

        [Test]
        public void AssetsMatchDefaultSeedData()
        {
            foreach (var seed in DefaultFieldData.Missions())
            {
                var asset = database.missions.First(m => m.id == seed.Data.id);
                Assert.AreEqual(seed.Data.budget, asset.budget, asset.name);
                CollectionAssert.AreEqual(seed.Data.items.Select(i => i.id + ":" + i.cost), asset.items.Select(i => i.id + ":" + i.cost), asset.name);
                CollectionAssert.AreEqual(
                    seed.Data.spots.SelectMany(s => s.options.Select(o => s.id + ":" + o.itemId + ">" + string.Join("+", o.solves))),
                    asset.spots.SelectMany(s => s.options.Select(o => s.id + ":" + o.itemId + ">" + string.Join("+", o.solves))), asset.name);
                CollectionAssert.AreEqual(seed.Data.obstacles.Select(o => o.clearedBy ?? ""), asset.obstacles.Select(o => o.clearedBy ?? ""), asset.name);
                Object.DestroyImmediate(seed.Data);
            }
        }

        [Test]
        public void DrainedPuddleClearsAndReplanRestores()
        {
            var m = M("f3");
            var world = FieldWorld.Build(null, m, true);
            try
            {
                Assert.IsTrue(world.PuddleBlocking("R"));
                Assert.IsTrue(world.PuddleBlocking("B"));
                world.ApplyInstant(Eval(m, ("door", "pump")));
                Assert.IsFalse(world.PuddleBlocking("R"), "정문 길 배수 후 관련 물웅덩이는 통행 가능");
                Assert.IsTrue(world.PuddleBlocking("B"), "관련 없는 물웅덩이는 남는다");
                Assert.AreEqual(1, world.InstalledCount);
                world.ApplyInstant(null);
                Assert.IsTrue(world.PuddleBlocking("R"), "설치 전 상태로 되돌리면 다시 막힌다");
                Assert.AreEqual(0, world.InstalledCount);
            }
            finally
            {
                Object.DestroyImmediate(world.gameObject);
            }
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
