namespace ProcurementHero.Core
{
    /// <summary>게임 속 제품 분류. 실제 제도상의 분류가 아니다.</summary>
    public enum ProductType
    {
        Basic,
        Efficient,
        Innovative
    }

    /// <summary>점수 계산에 필요한 제품 수치만 담은 불변 값. 비용은 가상 게임 단위다.</summary>
    public sealed class ProductSpec
    {
        public readonly string Id;
        public readonly string Name;
        public readonly ProductType Type;
        public readonly int PurchaseCost;
        /// <summary>미션 평가 기간 전체의 유지비. 반복 차감하지 않는다.</summary>
        public readonly int MaintenanceCost;
        public readonly int Performance;
        public readonly int DeliveryDays;
        public readonly int EcoScore;

        public ProductSpec(string id, string name, ProductType type, int purchaseCost, int maintenanceCost,
            int performance, int deliveryDays, int ecoScore)
        {
            Id = id;
            Name = name;
            Type = type;
            PurchaseCost = purchaseCost;
            MaintenanceCost = maintenanceCost;
            Performance = performance;
            DeliveryDays = deliveryDays;
            EcoScore = ecoScore;
        }

        public int TotalCost => PurchaseCost + MaintenanceCost;
    }

    public sealed class MissionSpec
    {
        public readonly string Id;
        public readonly int Budget;
        public readonly int DeadlineDays;
        public readonly int TargetPerformance;

        public MissionSpec(string id, int budget, int deadlineDays, int targetPerformance)
        {
            Id = id;
            Budget = budget;
            DeadlineDays = deadlineDays;
            TargetPerformance = targetPerformance;
        }
    }
}
