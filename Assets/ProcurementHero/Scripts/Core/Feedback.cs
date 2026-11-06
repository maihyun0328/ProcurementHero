using System.Collections.Generic;

namespace ProcurementHero.Core
{
    /// <summary>결과 화면의 '잘된 선택 / 아쉬운 선택' 문구를 계산 결과에서 만든다.</summary>
    public static class Feedback
    {
        public sealed class Report
        {
            public string Headline;
            public readonly List<string> Good = new List<string>();
            public readonly List<string> Regret = new List<string>();
        }

        public static Report Build(PurchaseEvaluation e, string itemNameA, string itemNameB)
        {
            var r = new Report();
            var m = e.Mission;
            int remaining = m.Budget - e.TotalCost;

            if (e.Success)
                r.Headline = "요청을 모두 해결했어요!";
            else if (!e.WithinBudget)
                r.Headline = "예산을 넘겨 요청을 해결하지 못했어요.";
            else if (remaining > 0)
                r.Headline = "예산은 아꼈지만 요청을 모두 해결하지는 못했어요.";
            else
                r.Headline = "요청을 일부만 해결했어요.";

            AddItem(r, m, e.ProductA, itemNameA, e.MeetsPerformanceA, e.OnTimeA);
            AddItem(r, m, e.ProductB, itemNameB, e.MeetsPerformanceB, e.OnTimeB);

            if (e.WithinBudget && remaining > 0)
                r.Good.Add($"예산 {m.Budget} 중 {remaining}을(를) 남겼어요.");
            else if (e.WithinBudget)
                r.Good.Add("예산을 정확히 맞춰 썼어요.");
            else
                r.Regret.Add($"총비용 {e.TotalCost}이(가) 예산 {m.Budget}을(를) 넘었어요.");

            CheckMaintenance(r, e.ProductA, itemNameA);
            CheckMaintenance(r, e.ProductB, itemNameB);
            if (e.MaintenanceCostSum <= 16)
                r.Good.Add($"유지비 합계가 {e.MaintenanceCostSum}로 낮아 평가 기간 전체 비용을 줄였어요.");

            double ecoAverage = (e.ProductA.EcoScore + e.ProductB.EcoScore) / 2.0;
            if (ecoAverage >= 80)
                r.Good.Add("환경점수가 높은 제품을 골라 마을 환경에도 도움이 됐어요.");
            else if (ecoAverage < 50)
                r.Regret.Add("환경점수가 낮은 조합이었어요. 에너지와 자원 사용도 함께 살펴봐요.");

            if ((e.ProductA.Type == ProductType.Innovative && e.MeetsPerformanceA && e.OnTimeA) ||
                (e.ProductB.Type == ProductType.Innovative && e.MeetsPerformanceB && e.OnTimeB))
                r.Good.Add("새로운 제품을 현장에서 써 보며 성능을 확인했어요.");

            return r;
        }

        static void AddItem(Report r, MissionSpec m, ProductSpec p, string itemName, bool meetsPerformance, bool onTime)
        {
            if (meetsPerformance && onTime)
                r.Good.Add($"{itemName} · {p.Name}: 목표 성능 {m.TargetPerformance} 이상이고 제때 도착했어요.");
            if (!meetsPerformance)
                r.Regret.Add($"{itemName} · {p.Name}: 성능 {p.Performance}(으)로 목표 {m.TargetPerformance}에 못 미쳐 일부만 해결됐어요.");
            if (!onTime)
                r.Regret.Add($"{itemName} · {p.Name}: 배송 {p.DeliveryDays}일로 납기 {m.DeadlineDays}일을 넘겨 필요한 때 쓰지 못했어요.");
        }

        static void CheckMaintenance(Report r, ProductSpec p, string itemName)
        {
            if (p.MaintenanceCost >= 15)
                r.Regret.Add($"{itemName} · {p.Name}: 구매비는 낮지만 유지비 {p.MaintenanceCost}이(가) 커서 전체 비용이 늘었어요.");
        }
    }
}
