using System;
using System.Collections.Generic;
using System.Linq;
using ProcurementHero.Core.Field;
using UnityEngine;

namespace ProcurementHero.Field
{
    public enum FieldTheme
    {
        NightAlley,
        Clinic,
        Shelter
    }

    /// <summary>문제를 그림으로 보여 주는 방식.</summary>
    public enum FieldProblemKind
    {
        Dark,
        Heat,
        Thirst,
        Power,
        Flood
    }

    /// <summary>물품 그림과 설치 모습. 나중에 그림을 바꿀 때 이 키를 기준으로 교체한다.</summary>
    public enum FieldItemVisual
    {
        StreetLamp,
        WalkLight,
        AirConditioner,
        WaterDispenser,
        Shade,
        Generator,
        Pump,
        Sandbags,
        PortableGenerator,
        FloorCooler,
        BigPump
    }

    public enum FieldObstacleKind
    {
        Building,
        Wall,
        Fence,
        Puddle,
        Tree
    }

    [Serializable]
    public class FieldProblemEntry
    {
        public string id;
        public string displayName;
        public FieldProblemKind kind;
        public bool required = true;
        public bool mustObserve = true;
        [Tooltip("관찰하려면 이 지점 가까이 가야 한다.")] public Vector2 observePoint;
        [Tooltip("문제가 보이는 구역(어둠·더위·물). 해결되면 바뀐다.")] public Vector2 zoneCenter;
        public Vector2 zoneSize = new Vector2(4, 4);
        public Vector2 residentPosition;
        [Tooltip("상황 전(지금) 주민 대사: 앞으로의 불편을 알려 주는 초기 징후")] [TextArea(1, 2)] public string line;
        [Tooltip("예상한 상황이 일어난 뒤 해결하지 못했을 때 대사. 비우면 line")] [TextArea(1, 2)] public string eventLine;
        [TextArea(1, 2)] public string solvedLine;
        [Tooltip("상황 전에 보이는 초기 징후 세기(0: 아직 없음, 1: 이미 심함). 상황이 일어나면 1이 된다.")]
        [Range(0, 1)] public float earlyLevel;
        [Tooltip("관찰 후 수첩 '자세히'에 남는 메모(전체 문장)")] public string memo;
        [Tooltip("관찰 기록 카드에 한 줄로 보이는 핵심 단서(짧게, 약 15자). 비우면 memo")] public string clue;
    }

    [Serializable]
    public class FieldItemEntry
    {
        public string id;
        public string displayName;
        public int cost;
        public FieldItemVisual visual;
        [Tooltip("수첩에서 그림 옆에 붙는 짧은 특징(예: 넓게 밝힘).")] public string hint;
        [Tooltip("상황이 일어났을 때 이 물품이 자동으로 대응하는 모습 한 줄(예: 배수펌프가 물을 빼요)")] public string responseLine;
    }

    [Serializable]
    public class FieldSpotOption
    {
        public string itemId;
        [Tooltip("이 지점에 이 물품을 놓았을 때 해결되는 문제 ID")] public List<string> solves = new List<string>();
    }

    [Serializable]
    public class FieldSpotEntry
    {
        public string id;
        public string displayName;
        public Vector2 position;
        public List<FieldSpotOption> options = new List<FieldSpotOption>();
    }

    [Serializable]
    public class FieldObstacle
    {
        public FieldObstacleKind kind;
        public Vector2 center;
        public Vector2 size = Vector2.one;
        [Tooltip("이 문제가 해결되면 사라지는 장애물(예: 배수 후 물웅덩이). 비우면 항상 남는다.")] public string clearedBy;
    }

    /// <summary>
    /// 특정 자리에 놓은 특정 물품이 문제를 해결했을 때 주민이 보이는 반응.
    /// 같은 문제라도 무엇으로 해결했는지에 따라 대사와 머무는 자리가 달라진다(예: 그늘막 아래 / 현관 바람 앞).
    /// 해결 여부는 FieldRules 판정이 정하고, 이 데이터는 보여 주는 방식만 정한다.
    /// </summary>
    [Serializable]
    public class FieldReaction
    {
        public string spotId;
        public string itemId;
        public string problemId;
        [Tooltip("해결 후 주민 대사. 비우면 문제의 기본 해결 대사")] public string line;
        [Tooltip("해결 후 주민이 옮겨 가는지")] public bool moveResident;
        [Tooltip("옮겨 간 뒤 주민 발 아래 중심(월드 좌표)")] public Vector2 residentPosition;
    }

    /// <summary>
    /// 장식용 이용객 한 명의 조건부 길. 상황이 오면 start → waitAt까지 걸어와 기다리고,
    /// needsProblem이 확정 계획으로 해결되고 시설이 대응한 뒤에만 via를 거쳐 목적지(crowd 위치)로 간다.
    /// 해결되지 않으면 waitAt(물·벽 밖의 안전한 자리)에서 기다린다. 길찾기 없이 이 점들만 잇는다.
    /// </summary>
    [Serializable]
    public class FieldCrowdRoute
    {
        [Tooltip("crowd 목록의 번호")] public int visitor;
        [Tooltip("이 문제가 해결돼야 지나간다(예: 정문 진입로 R)")] public string needsProblem;
        public Vector2 start;
        public Vector2 waitAt;
        [Tooltip("waitAt에서 목적지까지 거치는 점(벽·물·설치물을 피하도록)")] public List<Vector2> via = new List<Vector2>();
    }

    [Serializable]
    public class FieldPath
    {
        public Vector2 center;
        public Vector2 size = Vector2.one;
    }

    /// <summary>현장 수첩 버전의 미션 하나. 비용·효과·배치를 화면 코드와 분리해 둔다.</summary>
    [CreateAssetMenu(menuName = "Procurement Hero/Field Mission", fileName = "FieldMission")]
    public class FieldMissionData : ScriptableObject
    {
        public string id;
        public string title;
        public string placeName;
        [TextArea(1, 2)] public string intro;
        [Tooltip("현장 도착 때 담당자가 하는 한 문장")] public string arrivalLine;
        public FieldTheme theme;
        [Tooltip("가상 AI 수첩 '내일'의 예측 한 줄(정의된 데이터, 실제 AI 분석 아님)")] public string forecast;
        [Tooltip("현장에 도착한 지금 시각(이야기상의 시간, HH:MM)")] public string nowTime = "15:00";
        [Tooltip("예측한 상황이 일어나는 시각(HH:MM). 계획을 승인하기 전에는 시간이 흐르지 않는다.")] public string eventTime = "18:00";
        [Tooltip("상황 발생 안내(시간 경과 뒤 한 줄)")] public string eventCaption;
        [Tooltip("센서가 상황을 감지했다는 안내. 그 뒤에 실제로 설치해 문제를 해결하는 물품의 대응 문구만 붙는다.")] public string responseCaption;
        public int budget;
        [Tooltip("개발 초안 수치인지(기획서에서 확정되지 않은 값)")] public bool draftNumbers;
        public Vector2 playerStart;
        public List<FieldProblemEntry> problems = new List<FieldProblemEntry>();
        public List<FieldItemEntry> items = new List<FieldItemEntry>();
        public List<FieldSpotEntry> spots = new List<FieldSpotEntry>();
        public List<FieldObstacle> obstacles = new List<FieldObstacle>();
        public List<FieldPath> paths = new List<FieldPath>();
        [Tooltip("물품·자리별 주민 반응. 목록 순서가 우선순위다(같은 문제에 둘 이상 맞으면 앞의 것)")]
        public List<FieldReaction> reactions = new List<FieldReaction>();
        [Tooltip("상황이 일어날 때 늘어나는 이용객(장식용, 판정과 무관)의 머무는 자리")]
        public List<Vector2> crowd = new List<Vector2>();
        [Tooltip("조건부로 움직이는 이용객의 길(없는 이용객은 아래쪽에서 곧장 걸어온다)")]
        public List<FieldCrowdRoute> crowdRoutes = new List<FieldCrowdRoute>();

        public FieldProblemEntry Problem(string problemId) => problems.FirstOrDefault(p => p.id == problemId);
        public FieldItemEntry Item(string itemId) => items.FirstOrDefault(i => i.id == itemId);
        public FieldSpotEntry Spot(string spotId) => spots.FirstOrDefault(s => s.id == spotId);

        /// <summary>
        /// 확정된 배정에서 문제를 해결한 자리·물품에 맞는 반응. 판정상 해결되지 않았다면 null.
        /// 반응 목록 순서대로 찾고, 맞는 반응이 없으면 null(기본 해결 대사를 쓴다).
        /// </summary>
        public FieldReaction ReactionFor(string problemId, IReadOnlyDictionary<string, string> assignments)
        {
            if (assignments == null) return null;
            foreach (var r in reactions)
            {
                if (r.problemId != problemId) continue;
                if (!assignments.TryGetValue(r.spotId, out var item) || item != r.itemId) continue;
                var option = Spot(r.spotId)?.options.FirstOrDefault(o => o.itemId == r.itemId);
                if (option != null && option.solves.Contains(problemId)) return r;
            }
            return null;
        }

        /// <summary>
        /// 상황 발생 뒤 안내: 확정 배정 중 실제로 문제를 해결하는 물품의 대응 문구만 모은다(설치하지 않은 물품은 말하지 않는다).
        /// </summary>
        public string ResponseSummary(IReadOnlyDictionary<string, string> assignments)
        {
            var lines = new List<string>();
            if (assignments != null)
                foreach (var spot in spots)
                {
                    if (!assignments.TryGetValue(spot.id, out var itemId)) continue;
                    var option = spot.options.FirstOrDefault(o => o.itemId == itemId);
                    var item = Item(itemId);
                    if (option == null || option.solves.Count == 0 || item == null || string.IsNullOrEmpty(item.responseLine)) continue;
                    if (!lines.Contains(item.responseLine)) lines.Add(item.responseLine);
                }
            return lines.Count > 0 ? string.Join(" · ", lines) : "대비한 시설이 없어 불편이 그대로예요";
        }

        public FieldMissionSpec ToSpec()
        {
            return new FieldMissionSpec(id, budget,
                problems.Select(p => new FieldProblemSpec(p.id, p.required, p.mustObserve)),
                items.Select(i => new FieldItemSpec(i.id, i.cost)),
                spots.Select(s => new FieldSpotSpec(s.id, s.options.ToDictionary(o => o.itemId, o => o.solves.ToArray()))));
        }
    }

    /// <summary>현장 수첩 버전의 별 기록. 기존 숫자 점수와 섞이지 않도록 버전이 붙은 키를 쓴다.</summary>
    public static class FieldRecords
    {
        const string Prefix = "PH2_Field_";

        public static string MissionKey(string missionId) => Prefix + "BestStars_" + missionId;
        public const string TotalKey = Prefix + "BestTotalStars";

        public static int BestStars(string missionId) => SaveService.Store.GetInt(MissionKey(missionId), -1);
        public static int BestTotal => SaveService.Store.GetInt(TotalKey, -1);

        /// <summary>미션 별 기록을 갱신한다. 더 많은 별일 때만 바꾸고 true를 돌려준다.</summary>
        public static bool SubmitMission(string missionId, int stars)
        {
            if (stars <= BestStars(missionId)) return false;
            SaveService.Store.SetInt(MissionKey(missionId), stars);
            SaveService.Store.Save();
            return true;
        }

        public static bool SubmitTotal(int stars)
        {
            if (stars <= BestTotal) return false;
            SaveService.Store.SetInt(TotalKey, stars);
            SaveService.Store.Save();
            return true;
        }

        public static void Reset(IEnumerable<string> missionIds)
        {
            foreach (var id in missionIds) SaveService.Store.DeleteKey(MissionKey(id));
            SaveService.Store.DeleteKey(TotalKey);
            SaveService.Store.Save();
        }
    }
}
