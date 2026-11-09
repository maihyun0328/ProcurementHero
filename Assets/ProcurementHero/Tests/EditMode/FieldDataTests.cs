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
            // 대형 냉방기: 대기실 안쪽은 대기실만, 현관 안쪽은 열린 문으로 바깥 줄까지.
            CollectionAssert.AreEquivalent(new[] { "H" }, Eval(m, ("waiting", "bigcool")).Solved);
            CollectionAssert.AreEquivalent(new[] { "H", "O" }, Eval(m, ("hall", "bigcool")).Solved);
            // 보통 냉방장치는 어디에 두어도 실내 한 곳(대기실)만.
            CollectionAssert.AreEquivalent(new[] { "H" }, Eval(m, ("hall", "aircon")).Solved);
            // 실외(대기 줄 옆)에는 냉방장치를 놓을 수 없고 그늘막만 된다. 복도 쉼터는 급수만.
            var spec = m.ToSpec();
            Assert.AreEqual(AssignOutcome.NotAllowed, new FieldPlan().Toggle(spec, "yard", "aircon"));
            Assert.AreEqual(AssignOutcome.NotAllowed, new FieldPlan().Toggle(spec, "yard", "bigcool"));
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
            var shadeWay = Eval(m, ("waiting", "aircon"), ("corridor", "water"), ("yard", "shade"));
            Assert.AreEqual(85, shadeWay.Cost);
            Assert.AreEqual(3, shadeWay.Stars);
            var coolerWay = Eval(m, ("hall", "bigcool"), ("corridor", "water"));
            Assert.AreEqual(85, coolerWay.Cost);
            Assert.AreEqual(3, coolerWay.Stars);
            Assert.IsFalse(Eval(m, ("hall", "bigcool"), ("corridor", "water"), ("yard", "shade")).WithinBudget, "두 방법을 겹치면 예산 초과");
        }

        [Test]
        public void Mission3DoorSpotIsAContestedChoice()
        {
            var m = M("f3");
            // 정문 앞: 싼 이동형 발전기(P)와 배수펌프(R)가 같은 자리를 두고 겨룬다.
            CollectionAssert.AreEquivalent(new[] { "P" }, Eval(m, ("door", "portable")).Solved);
            CollectionAssert.AreEquivalent(new[] { "R" }, Eval(m, ("door", "pump")).Solved);
            // 발전기를 정문에 두면 물은 진입로 끝에서 대형 배수펌프로 빼야 해서 아낀 비용이 돌아간다.
            var portableWay = Eval(m, ("door", "portable"), ("gate", "bigpump"), ("detour", "sandbags"));
            Assert.AreEqual(85, portableWay.Cost);
            Assert.AreEqual(3, portableWay.Stars);
            var panelWay = Eval(m, ("panel", "generator"), ("door", "pump"), ("detour", "sandbags"));
            Assert.AreEqual(85, panelWay.Cost);
            Assert.AreEqual(3, panelWay.Stars);
            Assert.AreEqual(2, Eval(m, ("panel", "generator"), ("door", "pump")).Stars);
            Assert.AreEqual(70, Eval(m, ("door", "portable"), ("gate", "bigpump")).Cost);
            Assert.IsFalse(Eval(m, ("panel", "generator"), ("gate", "bigpump"), ("detour", "sandbags")).WithinBudget, "비상전원 + 대형 펌프 + 모래주머니는 예산 초과");
        }

        [Test]
        public void ClinicCoolingCluesMatchRealEffects()
        {
            var m = M("f2");
            // 관찰 메모: 실내만 식힐 때는 작은 장치로 충분, 현관의 넓은 장치는 바깥 대기 공간까지.
            StringAssert.Contains("작은 장치로도 충분", m.Problem("H").memo);
            StringAssert.Contains("현관", m.Problem("O").memo);
            StringAssert.Contains("바깥", m.Problem("O").memo);
            StringAssert.Contains("현관", m.Item("bigcool").hint);
            // 설명과 효과가 어긋나지 않는다: 대형 냉방기는 현관에서만 바깥까지, 냉방장치는 어디서나 실내 한 곳.
            foreach (var spot in m.spots)
            {
                var big = spot.options.FirstOrDefault(o => o.itemId == "bigcool");
                if (big != null) Assert.AreEqual(spot.id == "hall", big.solves.Contains("O"), spot.id);
                foreach (var o in spot.options.Where(o => o.itemId == "aircon")) CollectionAssert.AreEqual(new[] { "H" }, o.solves, spot.id);
            }
            // 비용·판정은 바꾸지 않았다.
            Assert.AreEqual(40, m.Item("aircon").cost);
            Assert.AreEqual(60, m.Item("bigcool").cost);
            Assert.AreEqual(85, m.budget);
        }

        [Test]
        public void Missions2And3ThreeStarPlansAreRealAlternatives()
        {
            foreach (var id in new[] { "f2", "f3" })
            {
                var m = M(id);
                var spec = m.ToSpec();
                var all = FieldRules.AllPlans(spec).Select(p => FieldRules.Evaluate(spec, p)).Where(e => e.WithinBudget).ToList();
                var three = all.Where(e => e.Stars == 3).ToList();
                // 더 싼 별 3개 계획이 같은 효과로 비싼 계획을 의미 없게 만들지 않는다: 별 3개 계획은 비용이 모두 같다.
                Assert.AreEqual(1, three.Select(e => e.Cost).Distinct().Count(), m.title + ": 별 3개 계획 비용 " + string.Join(",", three.Select(e => e.Cost)));
                // 서로 다른 자리 조합이어야 고민할 거리가 된다.
                Assert.GreaterOrEqual(three.Select(e => string.Join(",", e.Assignments.Keys.OrderBy(k => k))).Distinct().Count(), 2, m.title);
                // 별 2개 계획은 별 3개보다 싸게 가능하다(추가 목표를 포기하면 예산이 남는다).
                Assert.Less(all.Where(e => e.Stars == 2).Min(e => e.Cost), three[0].Cost, m.title);
            }
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
                CollectionAssert.AreEqual(seed.Data.problems.Select(p => p.memo + "|" + p.clue), asset.problems.Select(p => p.memo + "|" + p.clue), asset.name);
                CollectionAssert.AreEqual(seed.Data.items.Select(i => i.id + ":" + i.cost), asset.items.Select(i => i.id + ":" + i.cost), asset.name);
                CollectionAssert.AreEqual(
                    seed.Data.spots.SelectMany(s => s.options.Select(o => s.id + ":" + o.itemId + ">" + string.Join("+", o.solves))),
                    asset.spots.SelectMany(s => s.options.Select(o => s.id + ":" + o.itemId + ">" + string.Join("+", o.solves))), asset.name);
                CollectionAssert.AreEqual(seed.Data.obstacles.Select(o => o.clearedBy ?? ""), asset.obstacles.Select(o => o.clearedBy ?? ""), asset.name);
                Assert.AreEqual(seed.Data.title, asset.title, asset.name);
                Assert.AreEqual(seed.Data.arrivalLine, asset.arrivalLine, asset.name);
                CollectionAssert.AreEqual(seed.Data.problems.Select(p => p.line + "|" + p.eventLine + "|" + p.solvedLine + "|" + p.earlyLevel + "|" + p.residentPosition + "|" + p.memo),
                    asset.problems.Select(p => p.line + "|" + p.eventLine + "|" + p.solvedLine + "|" + p.earlyLevel + "|" + p.residentPosition + "|" + p.memo), asset.name);
                Assert.AreEqual(seed.Data.forecast + seed.Data.nowTime + seed.Data.eventTime + seed.Data.eventCaption + seed.Data.responseCaption,
                    asset.forecast + asset.nowTime + asset.eventTime + asset.eventCaption + asset.responseCaption, asset.name);
                CollectionAssert.AreEqual(seed.Data.crowd, asset.crowd, asset.name);
                CollectionAssert.AreEqual(seed.Data.crowdRoutes.Select(r => $"{r.visitor}/{r.needsProblem}/{r.start}/{r.waitAt}/{string.Join(";", r.via)}"),
                    asset.crowdRoutes.Select(r => $"{r.visitor}/{r.needsProblem}/{r.start}/{r.waitAt}/{string.Join(";", r.via)}"), asset.name);
                CollectionAssert.AreEqual(seed.Data.items.Select(i => i.hint + "|" + i.responseLine), asset.items.Select(i => i.hint + "|" + i.responseLine), asset.name);
                CollectionAssert.AreEqual(seed.Data.spots.Select(sp => sp.id + "@" + sp.position), asset.spots.Select(sp => sp.id + "@" + sp.position), asset.name);
                CollectionAssert.AreEqual(seed.Data.reactions.Select(r => $"{r.spotId}/{r.itemId}/{r.problemId}/{r.line}/{r.moveResident}/{r.residentPosition}"),
                    asset.reactions.Select(r => $"{r.spotId}/{r.itemId}/{r.problemId}/{r.line}/{r.moveResident}/{r.residentPosition}"), asset.name);
                Object.DestroyImmediate(seed.Data);
            }
        }

        [Test]
        public void ObservedClueFitsOneCardLine()
        {
            // 관찰 카드 한 줄(1366×768에서 약 16px 글자)에 들어가도록 짧게. 긴 설명은 memo(자세히)에 둔다.
            foreach (var m in database.missions)
                foreach (var p in m.problems)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(p.clue), m.id + "/" + p.id);
                    Assert.LessOrEqual(p.clue.Length, 16, m.id + "/" + p.id + " " + p.clue);
                    Assert.IsFalse(string.IsNullOrEmpty(p.memo), m.id + "/" + p.id);
                }
        }

        [Test]
        public void DrainedPuddleClearsAndReplanRestores()
        {
            var m = M("f3");
            var world = FieldWorld.Build(null, m, true);
            try
            {
                Assert.IsFalse(world.PuddleBlocking("R"), "상황 전에는 얕게 고인 물이라 지나갈 수 있다");
                Assert.IsFalse(world.PuddleBlocking("B"));
                world.ApplyInstant(Eval(m, ("door", "pump")));
                Assert.IsFalse(world.PuddleBlocking("R"), "폭우 뒤에도 배수한 정문 길은 통행 가능");
                Assert.IsTrue(world.PuddleBlocking("B"), "대비하지 않은 보조 통로는 폭우 뒤 물이 차 막힌다");
                Assert.AreEqual(1, world.InstalledCount);
                world.ApplyInstant(null);
                Assert.IsFalse(world.PuddleBlocking("R"), "재계획(상황 전)으로 되돌리면 다시 얕은 물");
                Assert.IsFalse(world.PuddleBlocking("B"));
                Assert.AreEqual(0, world.InstalledCount);
            }
            finally
            {
                Object.DestroyImmediate(world.gameObject);
            }
        }

        static T WithWorld<T>(FieldMissionData m, System.Func<FieldWorld, T> body)
        {
            var world = FieldWorld.Build(null, m, true);
            try { return body(world); }
            finally { Object.DestroyImmediate(world.gameObject); }
        }

        [Test]
        public void ShadeMovesQueueResidentUnderTheShade()
        {
            var m = M("f2");
            var o = m.Problem("O");
            var yard = m.Spot("yard");
            WithWorld(m, world =>
            {
                Assert.AreEqual(o.residentPosition, world.ResidentAnchor("O"), "설치 전에는 원래 대기 위치");
                world.ApplyInstant(Eval(m, ("waiting", "aircon"), ("corridor", "water"), ("yard", "shade")));
                var at = world.ResidentAnchor("O");
                Assert.AreEqual(at, world.ResidentPosition("O"), "건너뛰기·즉시 적용도 같은 자리에 정착");
                // 그늘막 기둥(±1) 사이, 천막 바로 뒤(발이 그늘 위)
                Assert.Less(Mathf.Abs(at.x - yard.position.x), 0.9f);
                float foot = at.y - 0.45f;
                Assert.That(foot - yard.position.y, Is.InRange(0.0f, 0.5f), "발이 그늘막 바닥 그늘 안");
                Assert.AreEqual(FieldItemVisual.Shade, world.SolvedBy("O"));
                StringAssert.Contains("햇볕을 피해서", world.SolvedLine(o));
                // 관찰·말풍선 기준도 새 자리
                Assert.Less(Vector2.Distance(world.ObservePoint(o), at), FieldWorld.ObserveRange);
                Assert.Greater(Vector2.Distance(world.ObservePoint(o), o.observePoint), 1f);
                world.ApplyInstant(null);
                Assert.AreEqual(o.residentPosition, world.ResidentAnchor("O"), "재계획(설치 전)이면 원래 자리로");
                Assert.AreEqual(o.observePoint, world.ObservePoint(o));
                return 0;
            });
        }

        [Test]
        public void CoolerSolvedQueueHasNoShadeLineOrMove()
        {
            var m = M("f2");
            var o = m.Problem("O");
            WithWorld(m, world =>
            {
                world.ApplyInstant(Eval(m, ("hall", "bigcool"), ("corridor", "water")));
                Assert.AreEqual(FieldItemVisual.FloorCooler, world.SolvedBy("O"));
                var line = world.SolvedLine(o);
                StringAssert.DoesNotContain("그늘", line);
                StringAssert.DoesNotContain("햇볕", line);
                CollectionAssert.DoesNotContain(world.InstalledSpots, "yard", "그늘막을 놓지 않았다");
                var at = world.ResidentAnchor("O");
                Assert.Greater(Vector2.Distance(at, m.Spot("yard").position), 1.5f, "없는 그늘 아래로 가지 않는다");
                return 0;
            });
        }

        [Test]
        public void UnsolvedBonusResidentStaysAndKeepsComplaint()
        {
            var m = M("f2");
            var o = m.Problem("O");
            WithWorld(m, world =>
            {
                world.ApplyInstant(Eval(m, ("waiting", "aircon"), ("corridor", "water")));
                Assert.AreEqual(o.residentPosition, world.ResidentAnchor("O"));
                Assert.IsNull(world.SolvedBy("O"));
                Assert.AreEqual(0f, world.SiteLevel("O"));
                return 0;
            });
        }

        [Test]
        public void WaterResidentMovesToDispenserWithPlainWaterLine()
        {
            var m = M("f2");
            var w = m.Problem("W");
            WithWorld(m, world =>
            {
                world.ApplyInstant(Eval(m, ("waiting", "aircon"), ("corridor", "water")));
                Assert.Less(Vector2.Distance(world.ResidentAnchor("W"), m.Spot("corridor").position), 1.2f);
                StringAssert.DoesNotContain("시원한 물", world.SolvedLine(w));
                return 0;
            });
        }

        [Test]
        public void SkipInstallLeavesNoTempObjectsAndMatchesInstantState()
        {
            var m = M("f3");
            var plan = Eval(m, ("panel", "generator"), ("door", "pump"), ("detour", "sandbags"));
            string Snapshot(FieldWorld w) =>
                $"{w.InstalledCount}|{string.Join(",", w.InstalledSpots.OrderBy(x => x))}|R:{w.PuddleBlocking("R")}|B:{w.PuddleBlocking("B")}|H{w.Hazard}|S{w.Respond}|" +
                string.Join(",", m.problems.Select(p => p.id + "=" + w.SiteLevel(p.id) + "/" + w.SiteShown(p.id) + "@" + w.ResidentAnchor(p.id)));
            string skipped = WithWorld(m, world =>
            {
                int calls = 0, scenarioCalls = 0;
                world.PlayInstall(plan, null, () => calls++);
                world.SkipInstall();
                world.SkipInstall();
                Assert.AreEqual(1, calls, "완료 콜백은 한 번만");
                Assert.AreEqual(0, world.TempObjectCount);
                Assert.IsTrue(m.problems.All(p => world.SiteShown(p.id) == 0f), "설치만 끝난 상태에서는 해결 모습이 없다");
                world.PlayScenario(plan, null, null, () => scenarioCalls++);
                world.SkipScenario();
                world.SkipScenario();
                Assert.AreEqual(1, scenarioCalls, "상황 완료 콜백도 한 번만");
                Assert.AreEqual(0, world.TempObjectCount);
                return Snapshot(world);
            });
            string instant = WithWorld(m, world =>
            {
                world.ApplyInstant(plan);
                return Snapshot(world);
            });
            Assert.AreEqual(instant, skipped);
        }

        [Test]
        public void ReactionsOnlyDescribeRealSolutions()
        {
            foreach (var m in database.missions)
                foreach (var r in m.reactions)
                {
                    var option = m.Spot(r.spotId).options.First(o => o.itemId == r.itemId);
                    CollectionAssert.Contains(option.solves, r.problemId, m.title);
                    if (r.moveResident) Assert.IsTrue(FieldValidator.ResidentFits(m, r.residentPosition, r.spotId), m.title + " " + r.spotId);
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
