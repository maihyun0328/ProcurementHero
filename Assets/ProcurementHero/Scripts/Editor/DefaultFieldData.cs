using System.Collections.Generic;
using System.Linq;
using ProcurementHero.Field;
using UnityEngine;

namespace ProcurementHero.EditorTools
{
    /// <summary>
    /// Setup Field Notebook이 미션 에셋을 처음 만들 때 쓰는 초기값.
    /// 이미 있는 에셋은 덮어쓰지 않으며, 'Reset Field Mission Data' 메뉴를 실행할 때만 이 값으로 되돌린다.
    /// 미션 1 수치는 기획서 2.0의 값이고, 미션 2·3의 비용·효과·배치는 개발 초안이다(draftNumbers = true).
    /// 맵 좌표는 월드 단위(가로 -16~16, 세로 -8~8)다.
    /// </summary>
    public static class DefaultFieldData
    {
        public sealed class Seed
        {
            public string AssetName;
            public FieldMissionData Data;
        }

        static FieldProblemEntry Problem(string id, string name, FieldProblemKind kind, bool required, bool mustObserve,
            Vector2 point, Vector2 zoneCenter, Vector2 zoneSize, Vector2 resident, string line, string solvedLine) =>
            new FieldProblemEntry
            {
                id = id, displayName = name, kind = kind, required = required, mustObserve = mustObserve,
                observePoint = point, zoneCenter = zoneCenter, zoneSize = zoneSize, residentPosition = resident,
                line = line, solvedLine = solvedLine
            };

        static FieldItemEntry Item(string id, string name, int cost, FieldItemVisual visual, string hint) =>
            new FieldItemEntry { id = id, displayName = name, cost = cost, visual = visual, hint = hint };

        static FieldSpotOption Opt(string item, params string[] solves) =>
            new FieldSpotOption { itemId = item, solves = solves.ToList() };

        static FieldSpotEntry Spot(string id, string name, Vector2 pos, params FieldSpotOption[] options) =>
            new FieldSpotEntry { id = id, displayName = name, position = pos, options = options.ToList() };

        static FieldObstacle Block(FieldObstacleKind kind, float x, float y, float w, float h, string clearedBy = null) =>
            new FieldObstacle { kind = kind, center = new Vector2(x, y), size = new Vector2(w, h), clearedBy = clearedBy };

        static void Memo(FieldMissionData m, params (string id, string memo)[] memos)
        {
            foreach (var (id, memo) in memos) m.Problem(id).memo = memo;
        }

        static FieldPath Path(float x, float y, float w, float h) =>
            new FieldPath { center = new Vector2(x, y), size = new Vector2(w, h) };

        public static IEnumerable<Seed> Missions()
        {
            var m1 = ScriptableObject.CreateInstance<FieldMissionData>();
            m1.id = "f1";
            m1.title = "밤길을 밝혀라";
            m1.placeName = "새봄마을 저녁 골목";
            m1.intro = "어두운 곳을 살펴보고 조명을 놓을 자리를 정해요.";
            m1.arrivalLine = "어디가 불편한지 살펴볼까?";
            m1.theme = FieldTheme.NightAlley;
            m1.budget = 75;
            m1.draftNumbers = false;
            m1.playerStart = new Vector2(-14.5f, -6f);
            m1.problems = new List<FieldProblemEntry>
            {
                Problem("A", "마을 입구", FieldProblemKind.Dark, true, true, new Vector2(-11.5f, -4.5f), new Vector2(-11f, -4.6f), new Vector2(7f, 4.4f),
                    new Vector2(-10.2f, -3.6f), "입구가 어두워서 무서워요", "입구가 환해졌어요!"),
                Problem("B", "계단", FieldProblemKind.Dark, true, true, new Vector2(-0.6f, -0.2f), new Vector2(-0.6f, 0.6f), new Vector2(4.6f, 5f),
                    new Vector2(-0.6f, -2.2f), "계단이 잘 안 보여요", "이제 계단이 잘 보여요"),
                Problem("C", "안쪽 골목", FieldProblemKind.Dark, true, true, new Vector2(9.5f, 4.6f), new Vector2(9.6f, 4.8f), new Vector2(4.4f, 5.4f),
                    new Vector2(9.5f, 6.2f), "골목이 너무 깜깜해요", "골목을 편하게 지나가요"),
                Problem("D", "공원", FieldProblemKind.Dark, false, false, new Vector2(9.5f, -4.2f), new Vector2(10.4f, -4.8f), new Vector2(8f, 4.6f),
                    new Vector2(11.5f, -5.6f), "공원이 어두워 산책을 못 해요", "밤에도 공원에서 쉬어요")
            };
            Memo(m1, ("A", "입구가 깜깜함"), ("B", "계단 앞에서 발을 멈춤"), ("C", "골목 안이 어두움"), ("D", "공원 산책길이 어두움"));
            m1.items = new List<FieldItemEntry>
            {
                Item("streetlamp", "가로등", 35, FieldItemVisual.StreetLamp, "넓게 밝힘"),
                Item("walklight", "보행등", 20, FieldItemVisual.WalkLight, "가까이 밝힘")
            };
            m1.spots = new List<FieldSpotEntry>
            {
                Spot("entrance", "입구 갈림길", new Vector2(-7f, -3f), Opt("streetlamp", "A", "B"), Opt("walklight", "A")),
                Spot("stairtop", "계단 위 갈림길", new Vector2(3.2f, 2.8f), Opt("streetlamp", "B", "C"), Opt("walklight", "B")),
                Spot("park", "공원 가장자리", new Vector2(5.6f, -3.2f), Opt("streetlamp", "D"), Opt("walklight", "D"))
            };
            m1.obstacles = new List<FieldObstacle>
            {
                Block(FieldObstacleKind.Building, -12f, 3f, 6f, 5f),
                Block(FieldObstacleKind.Building, -3f, 6f, 6f, 3.5f),
                Block(FieldObstacleKind.Building, 4f, 6.5f, 5f, 2.5f),
                Block(FieldObstacleKind.Building, 14f, 5f, 4f, 5f),
                Block(FieldObstacleKind.Wall, -4f, 0f, 2.5f, 3.5f),
                Block(FieldObstacleKind.Wall, 3f, -0.5f, 3f, 2.5f),
                Block(FieldObstacleKind.Tree, 13.6f, -2.6f, 1.2f, 1.2f),
                Block(FieldObstacleKind.Tree, 7.4f, -6.6f, 1.2f, 1.2f),
                Block(FieldObstacleKind.Fence, 14.5f, -5.5f, 0.5f, 3.5f)
            };
            m1.paths = new List<FieldPath>
            {
                Path(0f, -5f, 32f, 2.6f), Path(-7f, -1.5f, 2.4f, 7f), Path(-0.6f, 0.4f, 3.6f, 4.2f),
                Path(3.5f, 2.8f, 21f, 2.2f), Path(9.6f, 5f, 2.6f, 5f)
            };
            yield return new Seed { AssetName = "FieldMission1_NightAlley", Data = m1 };

            var m2 = ScriptableObject.CreateInstance<FieldMissionData>();
            m2.id = "f2";
            m2.title = "시원한 보건소";
            m2.placeName = "새봄보건소";
            m2.intro = "더운 곳과 물이 모자란 곳을 찾아 장치를 놓을 자리를 정해요.";
            m2.arrivalLine = "이번에는 보건소를 확인해 보자.";
            m2.theme = FieldTheme.Clinic;
            m2.budget = 85;
            m2.draftNumbers = true;
            m2.playerStart = new Vector2(0f, -7f);
            m2.problems = new List<FieldProblemEntry>
            {
                Problem("H", "대기실", FieldProblemKind.Heat, true, true, new Vector2(-5f, 3.2f), new Vector2(-5.6f, 3.6f), new Vector2(8.2f, 6.4f),
                    new Vector2(-6.4f, 4.8f), "대기실이 너무 더워요", "대기실이 시원해요"),
                Problem("W", "복도 쉼터", FieldProblemKind.Thirst, true, true, new Vector2(6f, 4.2f), new Vector2(6f, 4.4f), new Vector2(7.4f, 5f),
                    new Vector2(7.2f, 5.4f), "마실 물이 부족해요", "시원한 물을 마셔요"),
                Problem("O", "바깥 줄", FieldProblemKind.Heat, false, false, new Vector2(-4f, -4f), new Vector2(-4.2f, -4.2f), new Vector2(8f, 4.4f),
                    new Vector2(-5.6f, -4f), "밖에서 기다리기 힘들어요", "그늘에서 편히 기다려요")
            };
            Memo(m2, ("H", "대기실이 더워 부채질"), ("W", "복도 쉼터 물통이 빔"), ("O", "현관 밖 줄 · 뙤약볕"));
            m2.items = new List<FieldItemEntry>
            {
                Item("aircon", "냉방장치", 40, FieldItemVisual.AirConditioner, "실내 냉방 · 실외 불가"),
                Item("water", "급수장치", 25, FieldItemVisual.WaterDispenser, "마실 물"),
                Item("shade", "그늘막", 20, FieldItemVisual.Shade, "실외 햇빛 가림")
            };
            // 대기실 안쪽: 대기실만 시원해진다. 현관 안쪽: 열린 문으로 바깥 줄까지 바람이 가지만 급수 자리와 겹친다.
            // 복도 쉼터: 물만. 앞마당: 실외라 그늘막만. 벽 너머 먼 구역은 해결하지 않는다.
            m2.spots = new List<FieldSpotEntry>
            {
                Spot("waiting", "대기실 안쪽", new Vector2(-8f, 6f), Opt("aircon", "H")),
                Spot("hall", "현관 안쪽", new Vector2(0f, 1.4f), Opt("aircon", "H", "O"), Opt("water", "W")),
                Spot("corridor", "복도 쉼터", new Vector2(8.4f, 1.6f), Opt("water", "W")),
                Spot("yard", "현관 앞마당", new Vector2(2.6f, -3f), Opt("shade", "O"))
            };
            m2.obstacles = new List<FieldObstacle>
            {
                Block(FieldObstacleKind.Wall, 0f, 7.3f, 20.6f, 0.6f),
                Block(FieldObstacleKind.Wall, -10f, 3.5f, 0.6f, 7.6f),
                Block(FieldObstacleKind.Wall, 10f, 3.5f, 0.6f, 7.6f),
                Block(FieldObstacleKind.Wall, -5.75f, 0f, 8.5f, 0.6f),
                Block(FieldObstacleKind.Wall, 5.75f, 0f, 8.5f, 0.6f),
                Block(FieldObstacleKind.Wall, 2f, 4.9f, 0.6f, 4.2f),
                Block(FieldObstacleKind.Tree, 9f, -5f, 1.4f, 1.4f),
                Block(FieldObstacleKind.Tree, -13f, -2f, 1.4f, 1.4f),
                Block(FieldObstacleKind.Fence, -14f, 4f, 0.5f, 6f)
            };
            m2.paths = new List<FieldPath> { Path(0f, -4f, 3f, 8f), Path(0f, -6.5f, 32f, 2.2f), Path(0f, 3.5f, 19.4f, 7f) };
            yield return new Seed { AssetName = "FieldMission2_Clinic", Data = m2 };

            var m3 = ScriptableObject.CreateInstance<FieldMissionData>();
            m3.id = "f3";
            m3.title = "비 오기 전 대피소";
            m3.placeName = "새봄대피소";
            m3.intro = "꺼진 불과 물이 고인 길을 살펴보고 장치를 놓을 자리를 정해요.";
            m3.arrivalLine = "비가 오기 전에 서둘러 살펴보자.";
            m3.theme = FieldTheme.Shelter;
            m3.budget = 85;
            m3.draftNumbers = true;
            m3.playerStart = new Vector2(-14f, -6.5f);
            m3.problems = new List<FieldProblemEntry>
            {
                Problem("P", "대피소 전력", FieldProblemKind.Power, true, true, new Vector2(0f, 2.2f), new Vector2(0f, 5f), new Vector2(11f, 5f),
                    new Vector2(-2f, 2.2f), "전원이 필요해요", "불이 켜져서 안심돼요"),
                Problem("R", "정문 진입로", FieldProblemKind.Flood, true, true, new Vector2(0f, -2f), new Vector2(0f, -2.6f), new Vector2(4.4f, 6f),
                    new Vector2(1.6f, -0.6f), "물이 빠지지 않아요", "정문 길로 안전하게 들어가요"),
                Problem("B", "보조 통로", FieldProblemKind.Flood, false, false, new Vector2(10f, 0.6f), new Vector2(10f, -0.4f), new Vector2(4f, 7f),
                    new Vector2(11.4f, 1.6f), "옆길도 물에 잠겼어요", "옆길로도 다닐 수 있어요")
            };
            Memo(m3, ("P", "창문 불이 꺼짐 · 비상등 깜빡"), ("R", "정문 길에 물이 고임"), ("B", "옆길도 물에 잠김"));
            m3.items = new List<FieldItemEntry>
            {
                Item("generator", "비상전원", 40, FieldItemVisual.Generator, "배전반에 연결 · 건물 전체"),
                Item("portable", "이동형 발전기", 25, FieldItemVisual.PortableGenerator, "문 가까이에서만 전원"),
                Item("pump", "배수펌프", 30, FieldItemVisual.Pump, "고인 물 빼기"),
                Item("sandbags", "모래주머니", 15, FieldItemVisual.Sandbags, "옆길 물막이")
            };
            // 정문 앞은 싼 이동형 발전기와 배수펌프가 겨루는 자리다. 발전기를 놓으면 배수는 진입로 끝에서 해야 한다.
            m3.spots = new List<FieldSpotEntry>
            {
                Spot("panel", "배전반 옆", new Vector2(-7f, 4.5f), Opt("generator", "P")),
                Spot("door", "정문 앞", new Vector2(2.8f, 1.8f), Opt("portable", "P"), Opt("pump", "R")),
                Spot("gate", "진입로 끝", new Vector2(-2.4f, -5.4f), Opt("pump", "R")),
                Spot("detour", "보조 통로 입구", new Vector2(7.6f, -3.4f), Opt("pump", "B"), Opt("sandbags", "B"))
            };
            m3.obstacles = new List<FieldObstacle>
            {
                Block(FieldObstacleKind.Building, 0f, 5f, 10f, 4f),
                Block(FieldObstacleKind.Building, -11.5f, -2.5f, 5f, 4f),
                Block(FieldObstacleKind.Building, 12.5f, 6f, 5f, 3f),
                Block(FieldObstacleKind.Puddle, -3.4f, -2.6f, 2f, 1.4f, "R"),
                Block(FieldObstacleKind.Puddle, 10.8f, -2.8f, 1.4f, 1.2f, "B"),
                Block(FieldObstacleKind.Tree, 6f, 2.6f, 1.2f, 1.2f),
                Block(FieldObstacleKind.Fence, 13.6f, -1f, 0.5f, 5f)
            };
            m3.paths = new List<FieldPath> { Path(0f, -3.2f, 3.2f, 10f), Path(0f, -6.6f, 32f, 2.2f), Path(10f, -1.6f, 3f, 9f), Path(0f, 2f, 14f, 1.6f) };
            yield return new Seed { AssetName = "FieldMission3_Shelter", Data = m3 };
        }
    }
}
