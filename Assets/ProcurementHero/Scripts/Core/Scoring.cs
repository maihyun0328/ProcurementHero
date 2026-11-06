using System;

namespace ProcurementHero.Core
{
    public enum PurchaseBlockReason
    {
        None,
        MissingBoth,
        MissingA,
        MissingB,
        OverBudget
    }

    /// <summary>한 물품의 현장 결과. 연출과 결과 문구가 이 값으로 갈린다.</summary>
    public enum ItemOutcome
    {
        Full,
        ShortPerformance,
        Late,
        ShortAndLate
    }

    /// <summary>구매 조합 하나의 계산 결과. 구매 전 예상과 확정 결과가 같은 객체 형식을 쓴다.</summary>
    public sealed class PurchaseEvaluation
    {
        public MissionSpec Mission;
        public ProductSpec ProductA;
        public ProductSpec ProductB;

        public int PurchaseCostSum;
        public int MaintenanceCostSum;
        public int TotalCost;
        public double DeliveryFactorA;
        public double DeliveryFactorB;
        public double PerformanceScore;
        public double EcoScore;
        public double BudgetScore;
        public double TotalScore;

        public bool WithinBudget;
        public bool MeetsPerformanceA;
        public bool MeetsPerformanceB;
        public bool OnTimeA;
        public bool OnTimeB;

        public bool Success => WithinBudget && MeetsPerformanceA && MeetsPerformanceB && OnTimeA && OnTimeB;

        public ItemOutcome OutcomeA => Scoring.Outcome(MeetsPerformanceA, OnTimeA);
        public ItemOutcome OutcomeB => Scoring.Outcome(MeetsPerformanceB, OnTimeB);
    }

    public static class Scoring
    {
        public const double PerformanceWeight = 60.0;
        public const double EcoWeight = 15.0;
        public const double BudgetWeight = 25.0;
        public const double LateDeliveryFactor = 0.4;

        public static double DeliveryFactor(MissionSpec mission, ProductSpec product)
        {
            return product.DeliveryDays <= mission.DeadlineDays ? 1.0 : LateDeliveryFactor;
        }

        public static int TotalCost(ProductSpec a, ProductSpec b)
        {
            int total = 0;
            if (a != null) total += a.TotalCost;
            if (b != null) total += b.TotalCost;
            return total;
        }

        /// <summary>구매 확정 버튼을 막아야 하는 이유. 성능 부족과 지연은 막지 않는다.</summary>
        public static PurchaseBlockReason GetBlockReason(MissionSpec mission, ProductSpec a, ProductSpec b)
        {
            if (a == null && b == null) return PurchaseBlockReason.MissingBoth;
            if (a == null) return PurchaseBlockReason.MissingA;
            if (b == null) return PurchaseBlockReason.MissingB;
            if (TotalCost(a, b) > mission.Budget) return PurchaseBlockReason.OverBudget;
            return PurchaseBlockReason.None;
        }

        /// <summary>예상 점수와 결과 화면이 함께 쓰는 유일한 계산 함수. 반올림하지 않는다.</summary>
        public static PurchaseEvaluation Evaluate(MissionSpec mission, ProductSpec a, ProductSpec b)
        {
            if (mission == null) throw new ArgumentNullException(nameof(mission));
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            var e = new PurchaseEvaluation
            {
                Mission = mission,
                ProductA = a,
                ProductB = b,
                PurchaseCostSum = a.PurchaseCost + b.PurchaseCost,
                MaintenanceCostSum = a.MaintenanceCost + b.MaintenanceCost,
                DeliveryFactorA = DeliveryFactor(mission, a),
                DeliveryFactorB = DeliveryFactor(mission, b)
            };
            e.TotalCost = e.PurchaseCostSum + e.MaintenanceCostSum;

            double perfA = a.Performance / 100.0 * e.DeliveryFactorA;
            double perfB = b.Performance / 100.0 * e.DeliveryFactorB;
            e.PerformanceScore = (perfA + perfB) / 2.0 * PerformanceWeight;
            e.EcoScore = (a.EcoScore + b.EcoScore) / 2.0 / 100.0 * EcoWeight;
            e.BudgetScore = Clamp01((mission.Budget - e.TotalCost) / (double)mission.Budget) * BudgetWeight;
            e.TotalScore = e.PerformanceScore + e.EcoScore + e.BudgetScore;

            e.WithinBudget = e.TotalCost <= mission.Budget;
            e.MeetsPerformanceA = a.Performance >= mission.TargetPerformance;
            e.MeetsPerformanceB = b.Performance >= mission.TargetPerformance;
            e.OnTimeA = a.DeliveryDays <= mission.DeadlineDays;
            e.OnTimeB = b.DeliveryDays <= mission.DeadlineDays;
            return e;
        }

        public static ItemOutcome Outcome(bool meetsPerformance, bool onTime)
        {
            if (meetsPerformance && onTime) return ItemOutcome.Full;
            if (!meetsPerformance && !onTime) return ItemOutcome.ShortAndLate;
            return meetsPerformance ? ItemOutcome.Late : ItemOutcome.ShortPerformance;
        }

        /// <summary>표시용 반올림(소수점 첫째 자리). 계산에는 쓰지 않는다.</summary>
        public static string Format1(double value)
        {
            return Math.Round(value, 1, MidpointRounding.AwayFromZero).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        static double Clamp01(double v)
        {
            if (v < 0) return 0;
            if (v > 1) return 1;
            return v;
        }
    }
}
