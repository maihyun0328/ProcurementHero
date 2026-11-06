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
        Sandbags
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
        public FieldTheme theme;
        public int budget;
        [Tooltip("개발 초안 수치인지(기획서에서 확정되지 않은 값)")] public bool draftNumbers;
        public Vector2 playerStart;
        public List<FieldProblemEntry> problems = new List<FieldProblemEntry>();
        public List<FieldItemEntry> items = new List<FieldItemEntry>();
        public List<FieldSpotEntry> spots = new List<FieldSpotEntry>();
        public List<FieldObstacle> obstacles = new List<FieldObstacle>();
        public List<FieldPath> paths = new List<FieldPath>();

        public FieldProblemEntry Problem(string problemId) => problems.FirstOrDefault(p => p.id == problemId);
        public FieldItemEntry Item(string itemId) => items.FirstOrDefault(i => i.id == itemId);
        public FieldSpotEntry Spot(string spotId) => spots.FirstOrDefault(s => s.id == spotId);

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
