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

        // ───────── AI 단서 ─────────

        static FieldHint Hint(FieldMissionData m, string[] seen, params (string, string)[] picks) =>
            FieldRules.Hint(m.ToSpec(), Plan(m, picks), new HashSet<string>(seen));

        [Test]
        public void HintPointsAtOneMissingObservedNeed()
        {
            var m2 = M("f2");
            var h = Hint(m2, new[] { "H", "W" }, ("waiting", "aircon"));
            Assert.AreEqual(FieldHintKind.MissingRequired, h.Kind);
            Assert.AreEqual("W", h.ProblemId, "빠진 것은 마실 물(복도 쉼터)");
            Assert.IsNull(h.SpotId, "놓을 자리를 알려 주지 않는다");
            Assert.AreEqual(FieldHintKind.NeedObservation, Hint(m2, new[] { "H" }).Kind);
        }

        [Test]
        public void HintReportsOverlapOnlyWhenEffectsReallyOverlap()
        {
            var m2 = M("f2");
            var over = Hint(m2, new[] { "H", "W" }, ("waiting", "aircon"), ("hall", "aircon"), ("corridor", "water"));
            Assert.AreEqual(FieldHintKind.Overlap, over.Kind);
            Assert.AreEqual("H", over.ProblemId);
            var clean = Hint(m2, new[] { "H", "W" }, ("waiting", "aircon"), ("corridor", "water"));
            Assert.AreEqual(FieldHintKind.AllCovered, clean.Kind, "중복 없는 계획에 중복이라고 하지 않는다");
            // 현관 대형 냉방기 하나는 H·O를 함께 해결할 뿐 겹침이 아니다.
            Assert.AreEqual(FieldHintKind.AllCovered, Hint(m2, new[] { "H", "W", "O" }, ("hall", "bigcool"), ("corridor", "water")).Kind);
        }

        [Test]
        public void HintNeverMentionsUnobservedProblems()
        {
            var m2 = M("f2");
            // O를 보지 않았으면 O를 빠졌다고 말하지 않고, O만 해결하는 그늘막은 '관찰한 곳에 닿지 않음'으로만 말한다.
            var h = Hint(m2, new[] { "H", "W" }, ("waiting", "aircon"), ("corridor", "water"));
            Assert.AreEqual(FieldHintKind.AllCovered, h.Kind);
            var idle = Hint(m2, new[] { "H", "W" }, ("waiting", "aircon"), ("corridor", "water"), ("yard", "shade"));
            Assert.AreEqual(FieldHintKind.NoEffect, idle.Kind);
            Assert.AreEqual("yard", idle.SpotId);
            Assert.IsNull(idle.ProblemId);
            var bonus = Hint(m2, new[] { "H", "W", "O" }, ("waiting", "aircon"), ("corridor", "water"));
            Assert.AreEqual(FieldHintKind.MissingBonus, bonus.Kind, "본 추가 목표는 말할 수 있다");
            Assert.AreEqual("O", bonus.ProblemId);
            foreach (var m in database.missions)
                foreach (var plan in FieldRules.AllPlans(m.ToSpec()))
                {
                    var seen = new HashSet<string>(m.problems.Where(p => p.mustObserve).Select(p => p.id));
                    var hint = FieldRules.Hint(m.ToSpec(), plan, seen);
                    if (hint.ProblemId != null) Assert.IsTrue(seen.Contains(hint.ProblemId), m.id);
                    if (hint.SpotId != null) Assert.IsNotNull(plan.ItemAt(hint.SpotId), m.id + ": 단서의 자리는 이미 놓은 자리만");
                    if (hint.Kind == FieldHintKind.MissingRequired) Assert.IsFalse(FieldRules.Solved(m.ToSpec(), plan).Contains(hint.ProblemId), m.id);
                }
        }

        [Test]
        public void HintDoesNotChangeThePlan()
        {
            var m3 = M("f3");
            var plan = Plan(m3, ("panel", "generator"));
            var before = string.Join(",", plan.Assignments.Select(a => a.Key + a.Value));
            var h = FieldRules.Hint(m3.ToSpec(), plan, new HashSet<string> { "P", "R" });
            Assert.AreEqual(FieldHintKind.MissingRequired, h.Kind);
            Assert.AreEqual("R", h.ProblemId);
            Assert.AreEqual(before, string.Join(",", plan.Assignments.Select(a => a.Key + a.Value)));
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

        static bool InsideBox(FieldObstacle o, Vector2 at, float margin) =>
            Mathf.Abs(at.x - o.center.x) < o.size.x / 2 + margin && Mathf.Abs(at.y - o.center.y) < o.size.y / 2 + margin;

        [Test]
        public void ShelterVisitorWaitsUnlessGateRoadIsDrained()
        {
            var m = M("f3");
            var route = m.crowdRoutes.Single(r => r.visitor == 0);
            var r = m.Problem("R");
            var deep = m.obstacles.Single(o => o.kind == FieldObstacleKind.Puddle && o.clearedBy == "R");
            Assert.AreEqual("R", route.needsProblem);
            Assert.IsFalse(InsideZone(r, route.waitAt, 0.4f), "대기 지점은 침수 구역(얕은 물 포함) 밖");
            Assert.IsFalse(m.obstacles.Any(o => InsideBox(o, route.waitAt, 0.4f)), "대기 지점은 장애물 밖");
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
                    world.StepCrowd(1f);
                    Assert.AreEqual(route.waitAt, world.CrowdPosition(0), pair.Key + ": 시설 대응 전(깊은 물)에는 모두 대기");
                    world.ApplyStage(e, 1f, 0.3f);
                    world.StepCrowd(1f);
                    Assert.AreEqual(route.waitAt, world.CrowdPosition(0), pair.Key + ": 물이 아직 깊으면(대응 초반) 대기");
                    // 대응 후반: 배수가 됐으면 일정한 걸음으로 걷기 시작하고, 아니면 계속 대기. 어느 순간에도 막힌 깊은 물 안에 들어가지 않는다.
                    world.ApplyStage(e, 1f, 0.8f);
                    for (int i = 0; i < 40; i++)
                    {
                        world.StepCrowd(0.05f);
                        var at = world.CrowdPosition(0);
                        if (world.PuddleBlocking("R")) Assert.IsFalse(InsideBox(deep, at + new Vector2(0, -0.45f), 0f), pair.Key + " 막힌 깊은 물 안 " + i);
                        if (!drained) Assert.IsFalse(InsideZone(r, at, 0f), pair.Key + " 정문 미해결이면 진입로에 들어가지 않음 " + i);
                    }
                    if (!drained) Assert.AreEqual(route.waitAt, world.CrowdPosition(0), pair.Key + ": 정문 미해결이면 대기");
                    else Assert.AreNotEqual(route.waitAt, world.CrowdPosition(0), pair.Key + ": 배수가 되면 걷기 시작");
                    world.ApplyInstant(e);
                    var final = world.CrowdPosition(0);
                    Assert.Less(Vector2.Distance(drained ? m.crowd[0] : route.waitAt, final), 0.01f, pair.Key + ": 최종 자리(건너뛰기·이어하기·엔딩과 같은 ApplyInstant)");
                    world.ApplyInstant(null);
                    Assert.AreEqual(0, world.VisibleCrowd, pair.Key + ": 재계획(상황 전)이면 다시 숨김");
                    return 0;
                });
            }
        }

        [Test]
        public void VisitorWalkFinishesBeforeScenarioEnds()
        {
            // 걸음은 '깊은 물이 빠진 순간(대응 절반)'부터 연출이 끝날 때까지 안에 끝나야 최종 상태로 튀지 않는다.
            var m = M("f3");
            var route = m.crowdRoutes.Single(r => r.visitor == 0);
            var points = new List<Vector2> { route.waitAt };
            points.AddRange(route.via);
            points.Add(m.crowd[route.visitor]);
            float length = 0;
            for (int i = 1; i < points.Count; i++) length += Vector2.Distance(points[i - 1], points[i]);
            float available = FieldWorld.ScenarioRespondSeconds * 0.5f + FieldWorld.ScenarioSettleSeconds;
            Assert.Less(length / FieldWorld.VisitorSpeed, available, "걸음 시간");
        }

        // ───────── 관찰 진행도(전체)와 필수 관찰(승인 조건) ─────────

        [Test]
        public void ObservationProgressCountsAllTargetsFromDataButApprovalNeedsOnlyRequired()
        {
            var expected = new Dictionary<string, (int total, int required)> { ["f1"] = (4, 3), ["f2"] = (3, 2), ["f3"] = (3, 2) };
            foreach (var m in database.missions)
            {
                var spec = m.ToSpec();
                Assert.AreEqual(m.problems.Count, FieldRules.ObservableCount(spec), m.id + " 분모는 데이터의 대상 수");
                Assert.AreEqual(expected[m.id].total, FieldRules.ObservableCount(spec), m.id + " 전체 대상");
                Assert.AreEqual(expected[m.id].required, FieldRules.RequiredObservationCount(spec), m.id + " 필수 대상");
                var observed = new HashSet<string>();
                Assert.AreEqual(0, FieldRules.ObservedCount(spec, observed));
                var extra = m.problems.First(p => !p.mustObserve);
                // 추가 대상부터 관찰: 전체 진행도는 오르지만 필수 관찰 완료로 보지 않는다.
                observed.Add(extra.id);
                observed.Add(extra.id); // 같은 대상 재관찰은 늘지 않음
                Assert.AreEqual(1, FieldRules.ObservedCount(spec, observed), m.id);
                Assert.AreEqual(expected[m.id].required, FieldRules.MissingObservations(spec, observed), m.id + " 추가만 관찰했으면 필수는 그대로 남음");
                Assert.AreEqual(ConfirmBlock.ObservationMissing, FieldRules.CanConfirm(spec, Plan(m, (m.spots[0].id, m.spots[0].options[0].itemId)), observed), m.id);
                // 다른 현장의 ID는 세지 않는다.
                observed.Add("not-a-problem");
                Assert.AreEqual(1, FieldRules.ObservedCount(spec, observed), m.id);
                // 필수만 모두 관찰(추가는 빼고): 승인 조건은 기존처럼 통과.
                var requiredOnly = new HashSet<string>(m.problems.Where(p => p.mustObserve).Select(p => p.id));
                Assert.AreEqual(0, FieldRules.MissingObservations(spec, requiredOnly));
                Assert.AreEqual(expected[m.id].total - 1, FieldRules.ObservedCount(spec, requiredOnly), m.id + " 필수만 보면 전체는 아직 덜 됨");
                var spot = m.spots.First(sp => sp.options.Any(o => o.solves.Count > 0));
                Assert.AreNotEqual(ConfirmBlock.ObservationMissing, FieldRules.CanConfirm(spec, Plan(m, (spot.id, spot.options[0].itemId)), requiredOnly), m.id + " 추가 대상은 승인 조건이 아님");
                // 전부 관찰
                var all = new HashSet<string>(m.problems.Select(p => p.id));
                Assert.AreEqual(expected[m.id].total, FieldRules.ObservedCount(spec, all), m.id);
            }
        }

        // ───────── 깊은 물: 그림과 통행 범위가 같다 ─────────

        [Test]
        public void DeepWaterLooksDeepExactlyWhenItBlocks()
        {
            var m = M("f3");
            var spec = m.ToSpec();
            WithWorld(m, world =>
            {
                foreach (var plan in FieldRules.AllPlans(spec))
                {
                    var e = FieldRules.Evaluate(spec, plan);
                    for (float h = 0; h <= 1.001f; h += 0.25f)
                    for (float rr = 0; rr <= 1.001f; rr += 0.25f)
                    {
                        world.ApplyStage(e, h, rr);
                        foreach (var id in new[] { "R", "B" })
                        {
                            bool blocking = world.PuddleBlocking(id);
                            Assert.AreEqual(blocking, world.DeepShown(id) > 0.01f, $"{id} h{h} r{rr}: 진하게 보이는 것과 막히는 것이 같다");
                        }
                    }
                }
                return 0;
            });
        }

        [Test]
        public void FloodStagesShallowThenDeepThenReopened()
        {
            var m = M("f3");
            var pumped = Eval(m, ("panel", "generator"), ("door", "pump"), ("detour", "sandbags"));
            var none = Eval(m, ("panel", "generator"));
            WithWorld(m, world =>
            {
                world.ApplyStage(none, 0f, 0f);
                Assert.IsFalse(world.PuddleBlocking("R"), "상황 전: 얕은 물이라 지나갈 수 있다");
                world.ApplyStage(none, 1f, 0f);
                Assert.IsTrue(world.PuddleBlocking("R"), "강우 증가 후: 깊은 물이 진입로를 막는다");
                Assert.IsTrue(world.PuddleBlocking("B"));
                world.ApplyInstant(none);
                Assert.IsTrue(world.PuddleBlocking("R"), "대비 없으면 상황 뒤에도 막힌다");

                world.ApplyStage(pumped, 1f, 0f);
                Assert.IsTrue(world.PuddleBlocking("R"), "펌프도 물이 차기 전에는 물을 없애지 못한다(강우 최고조에는 깊다)");
                Assert.IsFalse(world.PuddleBlocking("B"), "모래주머니는 처음부터 물이 넘어오지 않게 막는다");
                world.ApplyInstant(pumped);
                Assert.IsFalse(world.PuddleBlocking("R"), "배수 후 다시 열린 통로");
                Assert.IsFalse(world.PuddleBlocking("B"));
                Assert.AreEqual(0f, world.DeepShown("R"));
                world.ApplyInstant(null);
                Assert.IsFalse(world.PuddleBlocking("R"), "재계획: 상황 전 얕은 물로 복원");
                Assert.IsFalse(world.PuddleBlocking("B"));
                return 0;
            });
        }

        [Test]
        public void PlayerStandingInFutureDeepWaterIsMovedJustOutside()
        {
            var m = M("f3");
            var deep = m.obstacles.Single(o => o.kind == FieldObstacleKind.Puddle && o.clearedBy == "R");
            var none = Eval(m, ("panel", "generator"));
            var starts = new Dictionary<string, Vector2>
            {
                ["물 한가운데"] = deep.center,
                ["물 경계(아래)"] = deep.center + new Vector2(0.3f, -deep.size.y / 2 - 0.1f),
                ["물 경계(오른쪽)"] = deep.center + new Vector2(deep.size.x / 2 + 0.2f, 0.5f)
            };
            foreach (var pair in starts)
            {
                WithWorld(m, world =>
                {
                    world.PlacePlayer(pair.Value, Vector2.right);
                    world.PlayScenario(none, null, null, null);
                    world.SkipScenario();
                    Assert.IsTrue(world.PuddleBlocking("R"));
                    Assert.IsFalse(world.PlayerInDeepWater, pair.Key + ": 막힌 깊은 물 안에 남지 않는다");
                    var at = world.PlayerPosition;
                    Assert.Less(Vector2.Distance(at, pair.Value), 3.2f, pair.Key + ": 가까운 물 밖으로만 옮긴다(출발 지점으로 순간이동하지 않음)");
                    Assert.IsTrue(FieldValidator.Walkable(m, at, deepWater: true), pair.Key + ": 옮긴 자리는 벽·물과 겹치지 않는다 " + at);
                    return 0;
                });
            }
        }

        [Test]
        public void SafePlayerIsNotTeleported()
        {
            var m = M("f3");
            var none = Eval(m, ("panel", "generator"));
            foreach (var at in new[] { m.playerStart, new Vector2(-5f, -6.4f), new Vector2(4.5f, -1f) })
                WithWorld(m, world =>
                {
                    world.PlacePlayer(at, Vector2.right);
                    world.PlayScenario(none, null, null, null);
                    world.SkipScenario();
                    Assert.Less(Vector2.Distance(world.PlayerPosition, at), 0.001f, "이미 안전한 자리면 그대로 " + at);
                    world.ApplyInstant(none);
                    Assert.Less(Vector2.Distance(world.PlayerPosition, at), 0.001f, "이어하기·엔딩처럼 바로 적용해도 그대로 " + at);
                    return 0;
                });
        }

        [Test]
        public void InstantStateAlsoKeepsPlayerOutOfDeepWater()
        {
            var m = M("f3");
            var deep = m.obstacles.Single(o => o.kind == FieldObstacleKind.Puddle && o.clearedBy == "B");
            WithWorld(m, world =>
            {
                world.PlacePlayer(deep.center, Vector2.right);
                world.ApplyInstant(Eval(m, ("panel", "generator")));
                Assert.IsTrue(world.PuddleBlocking("B"));
                Assert.IsFalse(world.PlayerInDeepWater);
                return 0;
            });
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
