using System.Collections.Generic;
using ProcurementHero.Core;

namespace ProcurementHero.EditorTools
{
    /// <summary>
    /// Setup Game이 데이터 에셋을 처음 만들 때만 쓰는 기획 수치.
    /// 이미 있는 에셋은 덮어쓰지 않으므로, 이후 수정은 ScriptableObject에서 한다.
    /// 모든 제품과 수치는 가상이며 비용은 게임 단위다.
    /// </summary>
    static class DefaultGameData
    {
        public sealed class MissionSeed
        {
            public string AssetName;
            public string Id, Title, FacilityName, Request, ItemA, ItemB;
            public FacilityKind Facility;
            public int Budget, Deadline, Target;
            public ProductEntry[] A, B;
            public string Success, Partial, Fail;
        }

        static ProductEntry P(string id, string name, ProductType type, int buy, int keep, int perf, int days, int eco, string summary)
        {
            return new ProductEntry
            {
                id = id,
                displayName = name,
                type = type,
                purchaseCost = buy,
                maintenanceCost = keep,
                performance = perf,
                deliveryDays = days,
                ecoScore = eco,
                summary = summary
            };
        }

        public static IEnumerable<MissionSeed> Missions()
        {
            yield return new MissionSeed
            {
                AssetName = "Mission1_School",
                Id = "m1",
                Title = "학교 축제를 밝혀라",
                Facility = FacilityKind.School,
                FacilityName = "새봄초등학교",
                Request = "축제가 이틀 남았어요! 강당이 너무 어둡고 방송이 잘 안 들려요.",
                ItemA = "강당 조명",
                ItemB = "방송장치",
                Budget = 140,
                Deadline = 2,
                Target = 70,
                A = new[]
                {
                    P("m1-a-basic", "튼튼 조명", ProductType.Basic, 40, 15, 70, 1, 40, "값이 싸고 빨리 오지만 전기를 많이 써요"),
                    P("m1-a-efficient", "절전 조명", ProductType.Efficient, 65, 5, 85, 2, 95, "밝고 전기를 아껴 유지비가 적어요"),
                    P("m1-a-innovative", "공간맞춤 조명", ProductType.Innovative, 55, 5, 78, 2, 80, "강당 구역에 맞춰 빛을 나눠 비춰요")
                },
                B = new[]
                {
                    P("m1-b-basic", "기본 방송", ProductType.Basic, 40, 15, 70, 1, 40, "설치가 쉽지만 소리가 조금 탁해요"),
                    P("m1-b-efficient", "또렷 방송", ProductType.Efficient, 65, 5, 85, 2, 90, "뒷자리까지 소리가 또렷해요"),
                    P("m1-b-innovative", "소음대응 방송", ProductType.Innovative, 55, 5, 78, 2, 80, "주변 소음에 맞춰 크기를 조절해요")
                },
                Success = "강당이 환해지고 노래가 또렷하게 들려요! 축제 준비 끝!",
                Partial = "조금 나아졌지만 아직 어둡거나 소리가 작은 곳이 있어요.",
                Fail = "물품은 왔지만 축제 시간에 제대로 쓰기 어려웠어요."
            };

            yield return new MissionSeed
            {
                AssetName = "Mission2_Clinic",
                Id = "m2",
                Title = "폭염 속 보건소",
                Facility = FacilityKind.Clinic,
                FacilityName = "새봄보건소",
                Request = "폭염이 계속돼요. 대기실이 너무 덥고 마실 물이 모자라요.",
                ItemA = "냉방장치",
                ItemB = "급수장치",
                Budget = 140,
                Deadline = 3,
                Target = 80,
                A = new[]
                {
                    P("m2-a-basic", "일반 냉방", ProductType.Basic, 40, 30, 70, 1, 35, "싸고 빠르지만 전기요금이 많이 나와요"),
                    P("m2-a-efficient", "고효율 냉방", ProductType.Efficient, 70, 5, 90, 2, 95, "시원하고 전기를 적게 써요"),
                    P("m2-a-innovative", "수요반응 냉방", ProductType.Innovative, 60, 8, 82, 3, 85, "사람 수에 맞춰 냉방 세기를 바꿔요")
                },
                B = new[]
                {
                    P("m2-b-basic", "일반 급수", ProductType.Basic, 40, 25, 72, 1, 40, "바로 쓸 수 있지만 물이 자주 새요"),
                    P("m2-b-efficient", "절수 급수", ProductType.Efficient, 65, 5, 85, 2, 95, "물을 아끼면서 넉넉히 나와요"),
                    P("m2-b-innovative", "사용량감지 급수", ProductType.Innovative, 55, 8, 82, 3, 85, "쓰는 양을 감지해 물을 채워요")
                },
                Success = "대기실이 시원해지고 시원한 물도 넉넉해요. 이제 편히 쉴 수 있어요!",
                Partial = "조금 시원해졌지만 아직 덥거나 물이 부족한 순간이 있어요.",
                Fail = "장치는 왔지만 무더위를 이겨 내기엔 부족했어요."
            };

            yield return new MissionSeed
            {
                AssetName = "Mission3_Shelter",
                Id = "m3",
                Title = "비 오기 전 대피소",
                Facility = FacilityKind.Shelter,
                FacilityName = "새봄대피소",
                Request = "내일 큰비가 온대요! 정전과 침수에 하루 안에 대비해야 해요.",
                ItemA = "비상전원",
                ItemB = "배수장치",
                Budget = 170,
                Deadline = 1,
                Target = 80,
                A = new[]
                {
                    P("m3-a-basic", "기본 전원", ProductType.Basic, 45, 20, 72, 1, 35, "바로 오지만 오래 버티지 못해요"),
                    P("m3-a-efficient", "장시간 전원", ProductType.Efficient, 70, 8, 90, 2, 90, "오래 버티지만 배송에 이틀 걸려요"),
                    P("m3-a-innovative", "이동형 전원", ProductType.Innovative, 60, 8, 82, 1, 80, "필요한 곳으로 옮겨 쓸 수 있어요")
                },
                B = new[]
                {
                    P("m3-b-basic", "기본 배수", ProductType.Basic, 45, 20, 75, 1, 40, "빨리 오지만 물 빼는 힘이 약해요"),
                    P("m3-b-efficient", "고효율 배수", ProductType.Efficient, 70, 8, 92, 2, 90, "힘이 세지만 배송에 이틀 걸려요"),
                    P("m3-b-innovative", "수위대응 배수", ProductType.Innovative, 60, 8, 82, 1, 80, "물 높이에 맞춰 알아서 작동해요")
                },
                Success = "불이 켜지고 물이 빠졌어요! 모두 안전하게 비를 피했어요.",
                Partial = "조금 안전해졌지만 아직 어둡거나 물이 고인 곳이 있어요.",
                Fail = "장치가 제때 제 역할을 못 해서 불안한 밤을 보냈어요."
            };
        }
    }
}
