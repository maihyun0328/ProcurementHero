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

        public static int MissingObservations(FieldMissionSpec mission, ICollection<string> observed) =>
            mission.Problems.Count(p => p.MustObserve && !observed.Contains(p.Id));

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
        public int MaxStars => results.Length * 3;
        public void Reset() => Array.Clear(results, 0, results.Length);
    }
}
