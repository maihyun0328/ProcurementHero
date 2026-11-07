using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcurementHero.Core.Field;
using ProcurementHero.EditorTools;
using ProcurementHero.Field;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProcurementHero.Tests
{
    /// <summary>
    /// 예측하고 미리 준비하는 흐름: 확정 계획은 그대로 고정되고, 설치 때는 해결 모습이 없으며,
    /// 시간이 흘러 상황이 일어난 뒤에야 판정 결과와 같은 시설 대응·주민·충돌이 나타나는지 확인한다.
    /// </summary>
    public class FieldFutureFlowTests
    {
        FieldDatabase database;

        [OneTimeSetUp]
        public void Load()
        {
            database = AssetDatabase.LoadAssetAtPath<FieldDatabase>(FieldSetup.DatabasePath);
            Assert.IsNotNull(database);
        }

        FieldMissionData M(string id) => database.missions.First(m => m.id == id);

        static FieldPlan Plan(FieldMissionData m, params (string spot, string item)[] picks)
        {
            var spec = m.ToSpec();
            var plan = new FieldPlan();
            foreach (var (spot, item) in picks) Assert.AreNotEqual(AssignOutcome.NotAllowed, plan.Toggle(spec, spot, item));
            return plan;
        }

        static FieldEvaluation Eval(FieldMissionData m, params (string spot, string item)[] picks) => FieldRules.Evaluate(m.ToSpec(), Plan(m, picks));

        static T WithWorld<T>(FieldMissionData m, System.Func<FieldWorld, T> body)
        {
            var world = FieldWorld.Build(null, m, true);
            try { return body(world); }
            finally { Object.DestroyImmediate(world.gameObject); }
        }

        // ───────── 기준 사례 ─────────

        [Test]
        public void Mission1BenchmarkCasesAndBudgetEdge()
        {
            var m = M("f1");
            var spec = m.ToSpec();
            var all = new HashSet<string> { "A", "B", "C" };
            void Case(int cost, int stars, bool confirmable, params (string, string)[] picks)
            {
                var plan = Plan(m, picks);
                var e = FieldRules.Evaluate(spec, plan);
                Assert.AreEqual(cost, e.Cost, string.Join(",", picks));
                Assert.AreEqual(stars, e.Stars, string.Join(",", picks));
                Assert.AreEqual(confirmable, FieldRules.CanConfirm(spec, plan, all) == ConfirmBlock.None, string.Join(",", picks));
            }
            Case(70, 2, true, ("entrance", "streetlamp"), ("stairtop", "streetlamp"));
            Case(75, 3, true, ("entrance", "walklight"), ("stairtop", "streetlamp"), ("park", "walklight"));
            Case(55, 1, true, ("entrance", "streetlamp"), ("stairtop", "walklight"));
            Case(105, 3, false, ("entrance", "streetlamp"), ("stairtop", "streetlamp"), ("park", "streetlamp"));
            Case(20, 0, true, ("park", "walklight"));
            Assert.AreEqual(ConfirmBlock.OverBudget, FieldRules.CanConfirm(spec, Plan(m, ("entrance", "streetlamp"), ("stairtop", "streetlamp"), ("park", "streetlamp")), all));
            Assert.AreEqual(ConfirmBlock.ObservationMissing, FieldRules.CanConfirm(spec, Plan(m, ("park", "walklight")), new HashSet<string> { "A", "B" }));
        }

        [Test]
        public void Missions2And3RepresentativePlans()
        {
            var m2 = M("f2");
            Assert.AreEqual((65, 2), Pair(Eval(m2, ("waiting", "aircon"), ("corridor", "water"))));
            Assert.AreEqual((85, 3), Pair(Eval(m2, ("waiting", "aircon"), ("corridor", "water"), ("yard", "shade"))));
            Assert.AreEqual((85, 3), Pair(Eval(m2, ("hall", "bigcool"), ("corridor", "water"))));
            var m3 = M("f3");
            Assert.AreEqual((70, 2), Pair(Eval(m3, ("panel", "generator"), ("door", "pump"))));
            Assert.AreEqual((85, 3), Pair(Eval(m3, ("panel", "generator"), ("door", "pump"), ("detour", "sandbags"))));
            Assert.AreEqual((85, 3), Pair(Eval(m3, ("door", "portable"), ("gate", "bigpump"), ("detour", "sandbags"))));
        }

        static (int, int) Pair(FieldEvaluation e) => (e.Cost, e.Stars);

        // ───────── 예측·관찰 정보 공개 ─────────

        [Test]
        public void EveryMissionHasForecastStoryTimeAndEventLines()
        {
            foreach (var m in database.missions)
            {
                Assert.IsNotEmpty(m.forecast, m.id);
                Assert.IsNotEmpty(m.eventCaption, m.id);
                Assert.IsNotEmpty(m.responseCaption, m.id);
                Assert.IsTrue(System.TimeSpan.TryParse(m.nowTime, out var now) && System.TimeSpan.TryParse(m.eventTime, out var ev) && ev > now, m.id + ": 상황 시각이 지금보다 뒤");
                foreach (var p in m.problems)
                {
                    Assert.IsNotEmpty(p.eventLine, $"{m.id}/{p.id}");
                    Assert.AreNotEqual(p.line, p.eventLine, $"{m.id}/{p.id}: 상황 전 대사와 상황 후 대사는 달라야 한다");
                }
            }
        }

        [Test]
        public void ResponseCaptionNamesOnlyInstalledSolvingItems()
        {
            var m3 = M("f3");
            var two = m3.ResponseSummary(Eval(m3, ("panel", "generator"), ("door", "pump")).Assignments);
            StringAssert.Contains("비상전원", two);
            StringAssert.Contains("배수펌프", two);
            StringAssert.DoesNotContain("모래주머니", two, "설치하지 않은 물품은 안내하지 않는다");
            StringAssert.Contains("모래주머니", m3.ResponseSummary(Eval(m3, ("detour", "sandbags")).Assignments));
            StringAssert.Contains("없어", m3.ResponseSummary(new Dictionary<string, string>()));
            foreach (var m in database.missions)
                foreach (var item in m.items) Assert.IsNotEmpty(item.responseLine, $"{m.id}/{item.id}");
        }

        [Test]
        public void RecommendationHiddenUntilRequiredObservedAndIgnoresUnobserved()
        {
            var m2 = M("f2");
            var spec = m2.ToSpec();
            Assert.IsNull(FieldRules.Recommend(spec, new HashSet<string>()), "관찰 전에는 추천 없음");
            Assert.IsNull(FieldRules.Recommend(spec, new HashSet<string> { "H" }), "필수 관찰이 남으면 추천 없음");

            // 바깥 줄(O)을 보지 않았으면 O는 비교에 넣지 않는다: 필수만 가장 싸게.
            var rec = FieldRules.Recommend(spec, new HashSet<string> { "H", "W" });
            var e = FieldRules.Evaluate(spec, rec);
            Assert.AreEqual(65, e.Cost);
            Assert.IsTrue(e.AllRequiredSolved);
            Assert.IsFalse(e.IsSolved("O"));

            // 보았으면 예산 안에서 O까지.
            var full = FieldRules.Evaluate(spec, FieldRules.Recommend(spec, new HashSet<string> { "H", "W", "O" }));
            Assert.AreEqual(3, full.Stars);
            Assert.LessOrEqual(full.Cost, m2.budget);

            var m3 = M("f3");
            var r3 = FieldRules.Evaluate(m3.ToSpec(), FieldRules.Recommend(m3.ToSpec(), new HashSet<string> { "P", "R" }));
            Assert.IsTrue(r3.AllRequiredSolved);
            Assert.AreEqual(70, r3.Cost);
            Assert.IsFalse(r3.IsSolved("B"));
        }

        [Test]
        public void RecommendationIsBestWithinBudgetByObservedPriority()
        {
            foreach (var m in database.missions)
            {
                var spec = m.ToSpec();
                var observed = new HashSet<string>(m.problems.Select(p => p.id));
                var rec = FieldRules.Evaluate(spec, FieldRules.Recommend(spec, observed));
                Assert.LessOrEqual(rec.Cost, m.budget);
                foreach (var plan in FieldRules.AllPlans(spec).Where(p => p.Count > 0))
                {
                    var e = FieldRules.Evaluate(spec, plan);
                    if (!e.WithinBudget) continue;
                    Assert.LessOrEqual((e.RequiredSolved, e.BonusSolved).CompareTo((rec.RequiredSolved, rec.BonusSolved)), 0, m.id);
                    if (e.RequiredSolved == rec.RequiredSolved && e.BonusSolved == rec.BonusSolved) Assert.GreaterOrEqual(e.Cost, rec.Cost, m.id);
                }
            }
        }

        // ───────── 계획 → 공급 → 상황 → 결과 단계 ─────────

        [Test]
        public void BeforeEventNothingLooksSolvedEvenAfterInstall()
        {
            foreach (var m in database.missions)
            {
                var spec = m.ToSpec();
                var best = FieldRules.AllPlans(spec).Select(p => FieldRules.Evaluate(spec, p)).Where(e => e.WithinBudget).OrderByDescending(e => e.Stars).First();
                WithWorld(m, world =>
                {
                    Assert.AreEqual(0f, world.Hazard, m.id + ": 승인 전에는 상황이 진행되지 않는다");
                    Assert.AreEqual(0, world.VisibleCrowd);
                    world.PlayInstall(best, null, null);
                    Assert.IsTrue(m.problems.All(p => world.SiteShown(p.id) == 0f), m.id + ": 배송 중 해결 모습 없음");
                    world.SkipInstall();
                    Assert.AreEqual(best.Assignments.Count, world.InstalledCount, m.id + ": 설치물은 놓인다");
                    Assert.IsTrue(m.problems.All(p => world.SiteShown(p.id) == 0f), m.id + ": 설치만 끝난 상태에서도 해결 모습 없음");
                    Assert.IsTrue(m.problems.All(p => world.ResidentAnchor(p.id) == p.residentPosition), m.id + ": 주민은 아직 옮겨 가지 않는다");
                    Assert.IsTrue(m.problems.All(p => world.LineFor(p) == p.line), m.id + ": 상황 전 대사");
                    Assert.AreEqual(0f, world.Hazard);
                    return 0;
                });
            }
        }

        [Test]
        public void AfterEventEveryPlanShowsExactlyTheRuleResult()
        {
            foreach (var m in database.missions)
            {
                var spec = m.ToSpec();
                WithWorld(m, world =>
                {
                    foreach (var plan in FieldRules.AllPlans(spec))
                    {
                        var e = FieldRules.Evaluate(spec, plan);
                        world.ApplyInstant(e);
                        string tag = m.id + ":" + string.Join(",", e.Assignments.Select(a => a.Key + "=" + a.Value));
                        Assert.AreEqual(1f, world.Hazard, tag);
                        foreach (var p in m.problems)
                        {
                            bool solved = e.IsSolved(p.id);
                            Assert.AreEqual(solved ? 1f : 0f, world.SiteShown(p.id), tag + " 시설 대응/" + p.id);
                            Assert.AreEqual(solved ? world.SolvedLine(p) : p.eventLine, world.LineFor(p), tag + " 대사/" + p.id);
                            if (!solved) Assert.AreEqual(p.residentPosition, world.ResidentAnchor(p.id), tag + " 미해결 주민은 그대로/" + p.id);
                            if (m.obstacles.Any(o => o.clearedBy == p.id))
                                Assert.AreEqual(!solved, world.PuddleBlocking(p.id), tag + " 물웅덩이 충돌/" + p.id);
                        }
                        Assert.AreEqual(m.crowd.Count, world.VisibleCrowd, tag);
                    }
                    return 0;
                });
            }
        }

        [Test]
        public void ReplanReturnsToBeforeEventState()
        {
            var m = M("f3");
            WithWorld(m, world =>
            {
                world.ApplyInstant(Eval(m, ("panel", "generator"), ("door", "pump"), ("detour", "sandbags")));
                world.ApplyInstant(null);
                Assert.AreEqual(0f, world.Hazard);
                Assert.AreEqual(0f, world.Respond);
                Assert.AreEqual(0, world.InstalledCount);
                Assert.AreEqual(0, world.VisibleCrowd);
                Assert.AreEqual(0, world.TempObjectCount);
                foreach (var p in m.problems)
                {
                    Assert.AreEqual(p.residentPosition, world.ResidentAnchor(p.id));
                    Assert.AreEqual(0f, world.SiteShown(p.id));
                    Assert.AreEqual(p.line, world.LineFor(p));
                    Assert.IsFalse(world.PuddleBlocking(p.id), "상황 전 얕은 물은 통행 가능");
                }
                return 0;
            });
        }

        [Test]
        public void SandbagsHoldWaterBackInsteadOfDryingIt()
        {
            var m = M("f3");
            WithWorld(m, world =>
            {
                world.ApplyInstant(Eval(m, ("panel", "generator"), ("door", "pump"), ("detour", "sandbags")));
                Assert.AreEqual(FieldItemVisual.Sandbags, world.SolvedBy("B"));
                Assert.IsFalse(world.PuddleBlocking("B"), "물막이로 보조 통로는 막히지 않는다");
                return 0;
            });
        }

        // ───────── 대피소 이용객: 정문 배수 결과를 따른다 ─────────

        static bool InsideZone(FieldProblemEntry p, Vector2 at, float margin) =>
            Mathf.Abs(at.x - p.zoneCenter.x) < p.zoneSize.x / 2 + margin && Mathf.Abs(at.y - p.zoneCenter.y) < p.zoneSize.y / 2 + margin;

        [Test]
        public void ShelterVisitorWaitsUnlessGateRoadIsDrained()
        {
            var m = M("f3");
            var route = m.crowdRoutes.Single(r => r.visitor == 0);
            var r = m.Problem("R");
            Assert.AreEqual("R", route.needsProblem);
            Assert.IsFalse(InsideZone(r, route.waitAt, 0.4f), "대기 지점은 침수 구역 밖");
            Assert.IsFalse(m.obstacles.Any(o => Mathf.Abs(route.waitAt.x - o.center.x) < o.size.x / 2 + 0.4f && Mathf.Abs(route.waitAt.y - o.center.y) < o.size.y / 2 + 0.4f), "대기 지점은 장애물 밖");
            var plans = new Dictionary<string, (string, string)[]>
            {
                ["정문 미해결(전력만)"] = new[] { ("panel", "generator") },
                ["보조 통로만"] = new[] { ("detour", "sandbags") },
                ["전력+보조 통로(정문 미해결)"] = new[] { ("door", "portable"), ("detour", "sandbags") },
                ["정문 펌프"] = new[] { ("panel", "generator"), ("door", "pump") },
                ["대형 펌프"] = new[] { ("door", "portable"), ("gate", "bigpump") }
            };
            foreach (var pair in plans)
            {
                var e = Eval(m, pair.Value);
                bool drained = e.IsSolved("R");
                WithWorld(m, world =>
                {
                    world.ApplyStage(e, 1f, 0f);
                    Assert.AreEqual(route.waitAt, world.CrowdPosition(0), pair.Key + ": 시설 대응 전에는 모두 대기");
                    world.ApplyStage(e, 1f, 0.5f);
                    if (!drained) Assert.AreEqual(route.waitAt, world.CrowdPosition(0), pair.Key + ": 대응 중에도 정문 미해결이면 대기");
                    else Assert.AreNotEqual(route.waitAt, world.CrowdPosition(0), pair.Key + ": 배수가 되면 걷기 시작");
                    world.ApplyInstant(e);
                    var final = world.CrowdPosition(0);
                    Assert.Less(Vector2.Distance(drained ? m.crowd[0] : route.waitAt, final), 0.01f, pair.Key + ": 최종 자리(건너뛰기·이어하기·엔딩과 같은 ApplyInstant)");
                    // 걷는 동안 어느 순간에도 물이 찬 구역 안에 있지 않다(배수된 경우만 진입로를 지남).
                    for (float t = 0; t <= 1.001f; t += 0.05f)
                    {
                        world.ApplyStage(e, 1f, t);
                        var at = world.CrowdPosition(0);
                        if (!drained) Assert.IsFalse(InsideZone(r, at, 0f), pair.Key + " t=" + t);
                    }
                    world.ApplyInstant(null);
                    Assert.AreEqual(0, world.VisibleCrowd, pair.Key + ": 재계획(상황 전)이면 다시 숨김");
                    return 0;
                });
            }
        }

        [Test]
        public void ScenarioCanBeSkippedOnceFromAnyPointAndMatchesFinalState()
        {
            var m = M("f1");
            var plan = Eval(m, ("entrance", "walklight"), ("stairtop", "streetlamp"));
            string Snap(FieldWorld w) => $"{w.Hazard}/{w.Respond}/{w.InstalledCount}/{w.VisibleCrowd}/" +
                string.Join(",", m.problems.Select(p => p.id + w.SiteShown(p.id) + "@" + w.ResidentAnchor(p.id)));
            string skipped = WithWorld(m, world =>
            {
                int done = 0, stages = 0;
                world.PlayScenario(plan, null, s => stages++, () => done++);
                Assert.AreEqual(1, stages, "시작하자마자 '시간 경과' 단계");
                Assert.IsTrue(world.IsScenarioPlaying);
                world.SkipScenario();
                world.SkipScenario();
                Assert.AreEqual(1, done);
                Assert.IsFalse(world.IsScenarioPlaying);
                Assert.AreEqual(0, world.TempObjectCount);
                return Snap(world);
            });
            string instant = WithWorld(m, world => { world.ApplyInstant(plan); return Snap(world); });
            Assert.AreEqual(instant, skipped);
        }
    }
}
