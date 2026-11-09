using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcurementHero.Core.Field
{
    /// <summary>현장 수첩 버전의 문제 하나. 필수 문제와 추가 목표를 구분한다.</summary>
    public sealed class FieldProblemSpec
    {
        public readonly string Id;
        public readonly bool Required;
        /// <summary>계획을 확정하기 전에 반드시 관찰해야 하는지.</summary>
        public readonly bool MustObserve;

        public FieldProblemSpec(string id, bool required, bool mustObserve)
        {
            Id = id;
            Required = required;
            MustObserve = mustObserve;
        }
    }

    public sealed class FieldItemSpec
    {
        public readonly string Id;
        public readonly int Cost;

        public FieldItemSpec(string id, int cost)
        {
            Id = id;
            Cost = cost;
        }
    }

    /// <summary>설치 지점. 물품마다 이 지점에 놓았을 때 해결되는 문제 ID 집합을 가진다. 목록에 없는 물품은 설치할 수 없다.</summary>
    public sealed class FieldSpotSpec
    {
        public readonly string Id;
        readonly Dictionary<string, string[]> solves;

        public FieldSpotSpec(string id, IDictionary<string, string[]> solvesByItem)
        {
            Id = id;
            solves = new Dictionary<string, string[]>(solvesByItem);
        }

        public bool Allows(string itemId) => itemId != null && solves.ContainsKey(itemId);
        public IReadOnlyCollection<string> AllowedItems => solves.Keys;
        public IReadOnlyList<string> Solves(string itemId) => solves.TryGetValue(itemId, out var s) ? s : Array.Empty<string>();
    }

    public sealed class FieldMissionSpec
    {
        public readonly string Id;
        public readonly int Budget;
        public readonly IReadOnlyList<FieldProblemSpec> Problems;
        public readonly IReadOnlyList<FieldItemSpec> Items;
        public readonly IReadOnlyList<FieldSpotSpec> Spots;

        public FieldMissionSpec(string id, int budget, IEnumerable<FieldProblemSpec> problems, IEnumerable<FieldItemSpec> items,
            IEnumerable<FieldSpotSpec> spots)
        {
            Id = id;
            Budget = budget;
            Problems = problems.ToList();
            Items = items.ToList();
            Spots = spots.ToList();
        }

        public FieldItemSpec Item(string id) => Items.FirstOrDefault(i => i.Id == id);
        public FieldSpotSpec Spot(string id) => Spots.FirstOrDefault(s => s.Id == id);
    }

    public enum AssignOutcome
    {
        Assigned,
        Replaced,
        Cleared,
        NotAllowed,
        UnknownSpot
    }

    /// <summary>
    /// 지점별 물품 배정 목록. 지점당 물품 하나. 비용은 언제나 이 목록에서 다시 계산하므로
    /// 체크·교체·취소를 몇 번 해도 누적되지 않는다.
    /// </summary>
    public sealed class FieldPlan
    {
        readonly Dictionary<string, string> bySpot = new Dictionary<string, string>();

        public IReadOnlyDictionary<string, string> Assignments => bySpot;
        public int Count => bySpot.Count;

        public string ItemAt(string spotId) => bySpot.TryGetValue(spotId, out var item) ? item : null;

        /// <summary>같은 물품을 다시 체크하면 취소, 다른 물품이면 교체한다.</summary>
        public AssignOutcome Toggle(FieldMissionSpec mission, string spotId, string itemId)
        {
            var spot = mission.Spot(spotId);
            if (spot == null) return AssignOutcome.UnknownSpot;
            if (!spot.Allows(itemId) || mission.Item(itemId) == null) return AssignOutcome.NotAllowed;
            if (bySpot.TryGetValue(spotId, out var current))
            {
                if (current == itemId)
                {
                    bySpot.Remove(spotId);
                    return AssignOutcome.Cleared;
                }
                bySpot[spotId] = itemId;
                return AssignOutcome.Replaced;
            }
            bySpot[spotId] = itemId;
            return AssignOutcome.Assigned;
        }

        public void Clear(string spotId) => bySpot.Remove(spotId);

        public FieldPlan Clone()
        {
            var copy = new FieldPlan();
            foreach (var pair in bySpot) copy.bySpot[pair.Key] = pair.Value;
            return copy;
        }
    }

    public enum ConfirmBlock
    {
        None,
        ObservationMissing,
        NothingAssigned,
        OverBudget
    }

    /// <summary>한 계획의 판정 결과. 수첩 미리보기와 결과 화면이 같은 값을 쓴다.</summary>
    public sealed class FieldEvaluation
    {
        public string MissionId;
        public IReadOnlyDictionary<string, string> Assignments;
        public int Cost;
        public int Budget;
        public IReadOnlyCollection<string> Solved;
        public int RequiredTotal;
        public int RequiredSolved;
        public int BonusTotal;
        public int BonusSolved;
        public int Stars;

        public bool WithinBudget => Cost <= Budget;
        public bool AllRequiredSolved => RequiredSolved == RequiredTotal;
        public bool Success => Stars >= 2;
        public bool IsSolved(string problemId) => Solved.Contains(problemId);
    }

    /// <summary>AI 단서의 종류. 하나만 골라 짧게 알려 준다(지점·물품 정답을 한꺼번에 알려 주지 않음).</summary>
    public enum FieldHintKind
    {
        NeedObservation,
        MissingRequired,
        Overlap,
        NoEffect,
        OverBudget,
        MissingBonus,
        AllCovered
    }

    public sealed class FieldHint
    {
        public FieldHintKind Kind;
        /// <summary>관련 문제(관찰한 문제만). 없으면 null.</summary>
        public string ProblemId;
        /// <summary>관련 설치 지점(지금 계획에 이미 놓은 자리만). 없으면 null.</summary>
        public string SpotId;
        public int Amount;
    }

    public static class FieldRules
    {
        public static int Cost(FieldMissionSpec mission, FieldPlan plan)
        {
            int sum = 0;
            foreach (var pair in plan.Assignments)
            {
                var item = mission.Item(pair.Value);
                if (item != null && mission.Spot(pair.Key)?.Allows(pair.Value) == true) sum += item.Cost;
            }
            return sum;
        }

        /// <summary>배정된 물품이 해결하는 문제 ID의 합집합. 여러 물품이 같은 문제를 해결해도 한 번만 센다.</summary>
        public static HashSet<string> Solved(FieldMissionSpec mission, FieldPlan plan)
        {
            var solved = new HashSet<string>();
            foreach (var pair in plan.Assignments)
            {
                var spot = mission.Spot(pair.Key);
                if (spot == null || !spot.Allows(pair.Value)) continue;
                foreach (var id in spot.Solves(pair.Value)) solved.Add(id);
            }
            return solved;
        }

        public static int Stars(int requiredSolved, int requiredTotal, int bonusSolved, int bonusTotal)
        {
            if (requiredSolved == 0) return 0;
            if (requiredSolved < requiredTotal) return 1;
            return bonusSolved >= bonusTotal ? 3 : 2;
        }

        public static FieldEvaluation Evaluate(FieldMissionSpec mission, FieldPlan plan)
        {
            var solved = Solved(mission, plan);
            var required = mission.Problems.Where(p => p.Required).Select(p => p.Id).ToList();
            var bonus = mission.Problems.Where(p => !p.Required).Select(p => p.Id).ToList();
            int requiredSolved = required.Count(solved.Contains);
            int bonusSolved = bonus.Count(solved.Contains);
            return new FieldEvaluation
            {
                MissionId = mission.Id,
                // 확정 뒤 수첩을 고쳐도 결과가 바뀌지 않도록 복사해 둔다.
                Assignments = new Dictionary<string, string>(plan.Assignments.ToDictionary(p => p.Key, p => p.Value)),
                Cost = Cost(mission, plan),
                Budget = mission.Budget,
                Solved = solved,
                RequiredTotal = required.Count,
                RequiredSolved = requiredSolved,
                BonusTotal = bonus.Count,
                BonusSolved = bonusSolved,
                Stars = Stars(requiredSolved, required.Count, bonusSolved, bonus.Count)
            };
        }

        /// <summary>확정을 막는 첫 번째 이유. 일부 문제를 해결하지 못하는 계획은 막지 않는다.</summary>
        public static ConfirmBlock CanConfirm(FieldMissionSpec mission, FieldPlan plan, ICollection<string> observed)
        {
            if (mission.Problems.Any(p => p.MustObserve && !observed.Contains(p.Id))) return ConfirmBlock.ObservationMissing;
            if (!plan.Assignments.Any(a => mission.Spot(a.Key)?.Allows(a.Value) == true)) return ConfirmBlock.NothingAssigned;
            if (Cost(mission, plan) > mission.Budget) return ConfirmBlock.OverBudget;
            return ConfirmBlock.None;
        }

        /// <summary>
        /// 수첩 지도에 '해결 예상'으로 보여 줄 문제. 관찰한 문제만 미리 보여 주고,
        /// 아직 조사하지 않은 구역은 계획이 해결하더라도 숨긴다(판정 자체는 바꾸지 않는다).
        /// </summary>
        public static HashSet<string> PreviewSolved(FieldEvaluation evaluation, ICollection<string> observed)
        {
            return new HashSet<string>(evaluation.Solved.Where(observed.Contains));
        }

        public static int MissingObservations(FieldMissionSpec mission, ICollection<string> observed) =>
            mission.Problems.Count(p => p.MustObserve && !observed.Contains(p.Id));

        /// <summary>
        /// 수첩의 '추천안 비교'(가상 실증 보조). 필수 관찰을 마치기 전에는 null.
        /// 예산 안의 모든 계획을 관찰한 필수 문제 해결 수 → 관찰한 추가 문제 해결 수 → 낮은 비용 → 적은 물품 수로 비교한다.
        /// 관찰하지 않은 문제는 비교에 넣지 않는다(정답을 미리 알려 주지 않는다). 판정은 Evaluate와 같은 Solved를 쓴다.
        /// </summary>
        public static FieldPlan Recommend(FieldMissionSpec mission, ICollection<string> observed)
        {
            if (MissingObservations(mission, observed) > 0) return null;
            var required = mission.Problems.Where(p => p.Required && observed.Contains(p.Id)).Select(p => p.Id).ToList();
            var bonus = mission.Problems.Where(p => !p.Required && observed.Contains(p.Id)).Select(p => p.Id).ToList();
            FieldPlan best = null;
            int bestRequired = -1, bestBonus = -1, bestCost = int.MaxValue, bestCount = int.MaxValue;
            foreach (var plan in AllPlans(mission))
            {
                if (plan.Count == 0) continue;
                int cost = Cost(mission, plan);
                if (cost > mission.Budget) continue;
                var solved = Solved(mission, plan);
                int r = required.Count(solved.Contains), b = bonus.Count(solved.Contains);
                bool better = r != bestRequired ? r > bestRequired
                    : b != bestBonus ? b > bestBonus
                    : cost != bestCost ? cost < bestCost
                    : plan.Count < bestCount;
                if (!better) continue;
                best = plan;
                bestRequired = r;
                bestBonus = b;
                bestCost = cost;
                bestCount = plan.Count;
            }
            return best;
        }

        /// <summary>
        /// AI 단서: 관찰한 문제와 지금 계획을 비교해 한 가지만 알려 준다. 계획은 바꾸지 않는다.
        /// 순서: 관찰한 필수 문제 중 빠진 대비 → 같은 문제에 겹친 효과 → 관찰한 곳에 닿지 않는 물품 → 예산 초과 → 관찰한 추가 목표 → 모두 갖춤.
        /// 관찰하지 않은 문제는 어떤 단서에도 쓰지 않는다. 판정은 Evaluate와 같은 지점·물품 효과만 쓴다.
        /// </summary>
        public static FieldHint Hint(FieldMissionSpec mission, FieldPlan plan, ICollection<string> observed)
        {
            if (MissingObservations(mission, observed) > 0)
                return new FieldHint { Kind = FieldHintKind.NeedObservation, Amount = MissingObservations(mission, observed) };
            var solved = Solved(mission, plan);
            var seen = mission.Problems.Where(p => observed.Contains(p.Id)).ToList();

            var missing = seen.FirstOrDefault(p => p.Required && !solved.Contains(p.Id));
            if (missing != null) return new FieldHint { Kind = FieldHintKind.MissingRequired, ProblemId = missing.Id };

            var valid = plan.Assignments.Where(a => mission.Spot(a.Key)?.Allows(a.Value) == true)
                .Select(a => (spot: a.Key, solves: mission.Spot(a.Key).Solves(a.Value))).ToList();
            foreach (var p in seen)
            {
                var covering = valid.Where(v => v.solves.Contains(p.Id)).ToList();
                if (covering.Count >= 2) return new FieldHint { Kind = FieldHintKind.Overlap, ProblemId = p.Id };
            }

            var idle = valid.FirstOrDefault(v => !v.solves.Any(observed.Contains));
            if (idle.spot != null) return new FieldHint { Kind = FieldHintKind.NoEffect, SpotId = idle.spot };

            int cost = Cost(mission, plan);
            if (cost > mission.Budget) return new FieldHint { Kind = FieldHintKind.OverBudget, Amount = cost - mission.Budget };

            var bonus = seen.FirstOrDefault(p => !p.Required && !solved.Contains(p.Id));
            if (bonus != null) return new FieldHint { Kind = FieldHintKind.MissingBonus, ProblemId = bonus.Id };

            return new FieldHint { Kind = FieldHintKind.AllCovered };
        }

        /// <summary>모든 지점에 (미배정 + 설치 가능한 물품) 중 하나를 고르는 모든 조합을 만든다.</summary>
        public static IEnumerable<FieldPlan> AllPlans(FieldMissionSpec mission)
        {
            IEnumerable<FieldPlan> Expand(int index, FieldPlan current)
            {
                if (index == mission.Spots.Count)
                {
                    yield return current.Clone();
                    yield break;
                }
                var spot = mission.Spots[index];
                foreach (var plan in Expand(index + 1, current)) yield return plan;
                foreach (var item in spot.AllowedItems)
                {
                    current.Toggle(mission, spot.Id, item);
                    foreach (var plan in Expand(index + 1, current)) yield return plan;
                    current.Clear(spot.Id);
                }
            }
            return Expand(0, new FieldPlan());
        }
    }

    /// <summary>세 현장의 현재 채택 결과. 재계획 결과는 이전 결과를 대체한다.</summary>
    public sealed class FieldProgress
    {
        readonly FieldEvaluation[] results;

        public FieldProgress(int missionCount)
        {
            results = new FieldEvaluation[missionCount];
        }

        public int MissionCount => results.Length;
        public FieldEvaluation Get(int index) => results[index];
        public void Set(int index, FieldEvaluation evaluation) => results[index] = evaluation;
        public bool IsUnlocked(int index) => index == 0 || (results[index - 1] != null && results[index - 1].Success);
        public bool IsComplete => results.All(r => r != null && r.Success);
        public bool HasAny => results.Any(r => r != null);
        public int TotalStars => results.Where(r => r != null).Sum(r => r.Stars);

        /// <summary>
        /// 완주 뒤 현장 재도전 결과를 대표 결과로 삼을지 정하고 반영한다. 성공(별 2개 이상)이면서 지금 대표 결과보다 별이 많거나 같을 때만
        /// 바꾼다(같으면 최신 성공 배정). 실패·더 낮은 별은 대표 결과·다른 현장·해금을 건드리지 않는다. 바꿨으면 true.
        /// 대표 결과는 한 번의 시도 그대로 통째로 바꾸며, 다른 시도의 배정과 섞지 않는다.
        /// </summary>
        public bool SubmitRetry(int index, FieldEvaluation attempt)
        {
            if (attempt == null || !attempt.Success) return false;
            var current = results[index];
            if (current != null && attempt.Stars < current.Stars) return false;
            results[index] = attempt;
            return true;
        }
        public int MaxStars => results.Length * 3;
        public void Reset() => Array.Clear(results, 0, results.Length);
    }
}
