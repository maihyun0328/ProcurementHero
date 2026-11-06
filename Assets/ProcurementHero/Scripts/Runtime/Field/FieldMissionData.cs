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
        [TextArea(1, 2)] public string line;
        [TextArea(1, 2)] public string solvedLine;
        [Tooltip("관찰 후 수첩에 남는 한 줄 메모")] public string memo;
    }

    [Serializable]
    public class FieldItemEntry
    {
        public string id;
        public string displayName;
        public int cost;
        public FieldItemVisual visual;
        [Tooltip("수첩에서 그림 옆에 붙는 짧은 특징(예: 넓게 밝힘).")] public string hint;
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
