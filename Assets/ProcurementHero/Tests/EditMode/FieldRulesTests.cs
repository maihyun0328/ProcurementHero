using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcurementHero.Core.Field;

namespace ProcurementHero.Tests
{
    /// <summary>현장 수첩 버전의 배정·비용·별 판정 규칙(기획서 2.0 미션 1 기준). 기존 카드 비교 규칙 테스트와 분리한다.</summary>
    public class FieldRulesTests
    {
        static FieldMissionSpec Mission1(int budget = 75) => new FieldMissionSpec("f1", budget,
            new[]
            {
                new FieldProblemSpec("A", true, true), new FieldProblemSpec("B", true, true),
                new FieldProblemSpec("C", true, true), new FieldProblemSpec("D", false, false)
            },
            new[] { new FieldItemSpec("streetlamp", 35), new FieldItemSpec("walklight", 20) },
            new[]
            {
                new FieldSpotSpec("entrance", new Dictionary<string, string[]> { ["streetlamp"] = new[] { "A", "B" }, ["walklight"] = new[] { "A" } }),
                new FieldSpotSpec("stairtop", new Dictionary<string, string[]> { ["streetlamp"] = new[] { "B", "C" }, ["walklight"] = new[] { "B" } }),
                new FieldSpotSpec("park", new Dictionary<string, string[]> { ["streetlamp"] = new[] { "D" }, ["walklight"] = new[] { "D" } })
            });

        static readonly string[] AllObserved = { "A", "B", "C" };

        static FieldPlan Plan(FieldMissionSpec m, params (string spot, string item)[] picks)
        {
            var plan = new FieldPlan();
            foreach (var (spot, item) in picks) plan.Toggle(m, spot, item);
            return plan;
        }

        [Test]
        public void TwentySevenCombinationsForMission1()
        {
            var m = Mission1();
            var all = FieldRules.AllPlans(m).ToList();
            Assert.AreEqual(27, all.Count);
            Assert.AreEqual(27, all.Select(p => string.Join(",", p.Assignments.OrderBy(a => a.Key).Select(a => a.Key + "=" + a.Value))).Distinct().Count());
            foreach (var plan in all)
            {
                var e = FieldRules.Evaluate(m, plan);
                Assert.AreEqual(plan.Assignments.Sum(a => a.Value == "streetlamp" ? 35 : 20), e.Cost);
                Assert.That(e.Stars, Is.InRange(0, 3));
            }
        }

        [Test]
        public void ReferenceCase_TwoStreetLamps_70_TwoStars()
        {
            var m = Mission1();
            var e = FieldRules.Evaluate(m, Plan(m, ("entrance", "streetlamp"), ("stairtop", "streetlamp")));
            Assert.AreEqual(70, e.Cost);
            CollectionAssert.AreEquivalent(new[] { "A", "B", "C" }, e.Solved);
            Assert.AreEqual(2, e.Stars);
            Assert.IsTrue(e.Success);
        }

        [Test]
        public void ReferenceCase_Walk_Street_Walk_75_ThreeStars()
        {
            var m = Mission1();
            var plan = Plan(m, ("entrance", "walklight"), ("stairtop", "streetlamp"), ("park", "walklight"));
            var e = FieldRules.Evaluate(m, plan);
            Assert.AreEqual(75, e.Cost);
            CollectionAssert.AreEquivalent(new[] { "A", "B", "C", "D" }, e.Solved);
            Assert.AreEqual(3, e.Stars);
            Assert.AreEqual(ConfirmBlock.None, FieldRules.CanConfirm(m, plan, AllObserved));
        }

        [Test]
        public void ReferenceCase_StreetEntrance_WalkStair_55_OneStar()
        {
            var m = Mission1();
            var e = FieldRules.Evaluate(m, Plan(m, ("entrance", "streetlamp"), ("stairtop", "walklight")));
            Assert.AreEqual(55, e.Cost);
            Assert.IsFalse(e.IsSolved("C"));
            Assert.AreEqual(1, e.Stars);
            Assert.IsFalse(e.Success);
        }

        [Test]
        public void ReferenceCase_ThreeStreetLamps_105_Blocked()
        {
            var m = Mission1();
            var plan = Plan(m, ("entrance", "streetlamp"), ("stairtop", "streetlamp"), ("park", "streetlamp"));
            Assert.AreEqual(105, FieldRules.Cost(m, plan));
            Assert.AreEqual(ConfirmBlock.OverBudget, FieldRules.CanConfirm(m, plan, AllObserved));
        }

        [Test]
        public void ReferenceCase_ParkWalkLightOnly_20_ZeroStars()
        {
            var m = Mission1();
            var plan = Plan(m, ("park", "walklight"));
            var e = FieldRules.Evaluate(m, plan);
            Assert.AreEqual(20, e.Cost);
            Assert.AreEqual(0, e.Stars);
            Assert.AreEqual(ConfirmBlock.None, FieldRules.CanConfirm(m, plan, AllObserved), "필수 해결이 없어도 확정은 가능하다");
        }

        [Test]
        public void BudgetBoundary75AllowedAnd76Blocked()
        {
            // 같은 미션 구조에서 비용 경계만 바꿔 계산한다: 75는 허용, 76은 차단.
            var at75 = Mission1(75);
            var plan75 = Plan(at75, ("entrance", "walklight"), ("stairtop", "streetlamp"), ("park", "walklight"));
            Assert.AreEqual(ConfirmBlock.None, FieldRules.CanConfirm(at75, plan75, AllObserved));

            var pricier = new FieldMissionSpec("f1", 75, at75.Problems,
                new[] { new FieldItemSpec("streetlamp", 36), new FieldItemSpec("walklight", 20) }, at75.Spots);
            var plan76 = Plan(pricier, ("entrance", "walklight"), ("stairtop", "streetlamp"), ("park", "walklight"));
            Assert.AreEqual(76, FieldRules.Cost(pricier, plan76));
            Assert.AreEqual(ConfirmBlock.OverBudget, FieldRules.CanConfirm(pricier, plan76, AllObserved));
        }

        [Test]
        public void ToggleReplaceAndClearNeverAccumulateCost()
        {
            var m = Mission1();
            var plan = new FieldPlan();
            Assert.AreEqual(AssignOutcome.Assigned, plan.Toggle(m, "entrance", "streetlamp"));
            Assert.AreEqual(35, FieldRules.Cost(m, plan));
            Assert.AreEqual(AssignOutcome.Replaced, plan.Toggle(m, "entrance", "walklight"));
            Assert.AreEqual(20, FieldRules.Cost(m, plan));
            Assert.AreEqual(AssignOutcome.Replaced, plan.Toggle(m, "entrance", "streetlamp"));
            Assert.AreEqual(AssignOutcome.Cleared, plan.Toggle(m, "entrance", "streetlamp"));
            Assert.AreEqual(0, FieldRules.Cost(m, plan));
            Assert.AreEqual(0, plan.Count);
            for (int i = 0; i < 9; i++) plan.Toggle(m, "park", "walklight");
            Assert.AreEqual(20, FieldRules.Cost(m, plan), "홀수 번 체크하면 배정 하나만 남는다");
        }

        [Test]
        public void SameItemTypeOnSeveralSpotsAddsPerSpot()
        {
            var m = Mission1();
            var plan = Plan(m, ("entrance", "walklight"), ("stairtop", "walklight"), ("park", "walklight"));
            Assert.AreEqual(60, FieldRules.Cost(m, plan));
        }

        [Test]
        public void NotAllowedItemAndUnknownSpotAreRejected()
        {
            var m = Mission1();
            var plan = new FieldPlan();
            Assert.AreEqual(AssignOutcome.NotAllowed, plan.Toggle(m, "entrance", "aircon"));
            Assert.AreEqual(AssignOutcome.UnknownSpot, plan.Toggle(m, "nowhere", "streetlamp"));
            Assert.AreEqual(0, plan.Count);
            Assert.AreEqual(ConfirmBlock.NothingAssigned, FieldRules.CanConfirm(m, plan, AllObserved));
        }

        [Test]
        public void DuplicateSolvesCountOnce()
        {
            var m = Mission1();
            // 입구 가로등(A,B) + 계단 위 보행등(B): B가 두 번 비춰도 한 번만 센다.
            var e = FieldRules.Evaluate(m, Plan(m, ("entrance", "streetlamp"), ("stairtop", "walklight")));
            Assert.AreEqual(2, e.Solved.Count);
            Assert.AreEqual(2, e.RequiredSolved);
        }

        [Test]
        public void MissingObservationBlocksConfirm()
        {
            var m = Mission1();
            var plan = Plan(m, ("entrance", "streetlamp"));
            Assert.AreEqual(ConfirmBlock.ObservationMissing, FieldRules.CanConfirm(m, plan, new[] { "A", "B" }));
            Assert.AreEqual(1, FieldRules.MissingObservations(m, new[] { "A", "B" }));
            Assert.AreEqual(ConfirmBlock.None, FieldRules.CanConfirm(m, plan, new[] { "A", "B", "C" }), "추가 목표 D는 관찰하지 않아도 된다");
        }

        [Test]
        public void ConfirmedSnapshotIsIndependentOfLaterEdits()
        {
            var m = Mission1();
            var plan = Plan(m, ("entrance", "streetlamp"), ("stairtop", "streetlamp"));
            var snapshot = FieldRules.Evaluate(m, plan);
            plan.Toggle(m, "entrance", "streetlamp");
            plan.Toggle(m, "park", "streetlamp");
            Assert.AreEqual(2, snapshot.Assignments.Count);
            Assert.AreEqual("streetlamp", snapshot.Assignments["entrance"]);
            Assert.AreEqual(70, snapshot.Cost);
            Assert.AreEqual(2, snapshot.Stars);
        }

        [Test]
        public void StarRules()
        {
            Assert.AreEqual(0, FieldRules.Stars(0, 3, 1, 1));
            Assert.AreEqual(1, FieldRules.Stars(2, 3, 1, 1));
            Assert.AreEqual(2, FieldRules.Stars(3, 3, 0, 1));
            Assert.AreEqual(3, FieldRules.Stars(3, 3, 1, 1));
        }

        [Test]
        public void ReplanReplacesResultWithoutAccumulating()
        {
            var m = Mission1();
            var progress = new FieldProgress(3);
            progress.Set(0, FieldRules.Evaluate(m, Plan(m, ("entrance", "streetlamp"), ("stairtop", "walklight"))));
            Assert.AreEqual(1, progress.TotalStars);
            Assert.IsFalse(progress.IsUnlocked(1), "별 1개면 다음 현장은 잠겨 있다");
            progress.Set(0, FieldRules.Evaluate(m, Plan(m, ("entrance", "walklight"), ("stairtop", "streetlamp"), ("park", "walklight"))));
            Assert.AreEqual(3, progress.TotalStars);
            Assert.IsTrue(progress.IsUnlocked(1));
            Assert.IsFalse(progress.IsComplete);
        }
    }
}
