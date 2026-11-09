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
    /// 각 현장은 '아직 상황 전'에서 시작한다: line은 지금 보이는 초기 징후, eventLine은 예측한 상황이 온 뒤 대비하지 못했을 때,
    /// solvedLine·반응 대사는 준비한 시설이 대응한 뒤의 말이다. 예측(forecast)은 가상 AI 수첩 '내일'의 정해진 문장이다.
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

        static FieldItemEntry Item(string id, string name, int cost, FieldItemVisual visual, string hint, string response) =>
            new FieldItemEntry { id = id, displayName = name, cost = cost, visual = visual, hint = hint, responseLine = response };

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

        /// <summary>상황 전 초기 징후 세기와, 상황이 일어난 뒤 해결하지 못했을 때 대사.</summary>
        static void Early(FieldMissionData m, params (string id, float level, string eventLine)[] signs)
        {
            foreach (var (id, level, eventLine) in signs)
            {
                m.Problem(id).earlyLevel = level;
                m.Problem(id).eventLine = eventLine;
            }
        }

        static FieldReaction React(string spot, string item, string problem, string line, Vector2? moveTo = null) =>
            new FieldReaction
            {
                spotId = spot, itemId = item, problemId = problem, line = line,
                moveResident = moveTo.HasValue, residentPosition = moveTo ?? Vector2.zero
            };

        static FieldPath Path(float x, float y, float w, float h) =>
            new FieldPath { center = new Vector2(x, y), size = new Vector2(w, h) };

        public static IEnumerable<Seed> Missions()
        {
            var m1 = ScriptableObject.CreateInstance<FieldMissionData>();
            m1.id = "f1";
            m1.title = "해가 지기 전, 스마트 귀갓길";
            m1.placeName = "새봄시 골목길";
            m1.intro = "해가 지기 전에 어두워질 곳을 확인하고 조명을 놓을 자리를 정해요.";
            m1.arrivalLine = "어두워질 곳을 미리 살펴볼까?";
            m1.theme = FieldTheme.NightAlley;
            m1.forecast = "18:00 이후 보행 증가 · 계단 주변 조도 부족 예상";
            m1.nowTime = "16:30";
            m1.eventTime = "18:00";
            m1.eventCaption = "저녁이 되었습니다. 귀갓길 주민이 늘어요";
            m1.responseCaption = "조도 센서가 어둠을 감지했어요";
            m1.crowd = new List<Vector2> { new Vector2(-4.5f, -5.4f), new Vector2(1.8f, -4.6f) };
            m1.budget = 75;
            m1.draftNumbers = false;
            m1.playerStart = new Vector2(-14.5f, -6f);
            m1.problems = new List<FieldProblemEntry>
            {
                Problem("A", "마을 입구", FieldProblemKind.Dark, true, true, new Vector2(-11.5f, -4.5f), new Vector2(-11f, -4.6f), new Vector2(7f, 4.4f),
                    new Vector2(-10.2f, -3.6f), "해가 지면 입구 쪽은 금방 깜깜해져요", "입구가 환해졌어요!"),
                Problem("B", "계단", FieldProblemKind.Dark, true, true, new Vector2(-0.6f, -0.2f), new Vector2(-0.6f, 0.6f), new Vector2(4.6f, 5f),
                    new Vector2(-0.6f, -2.2f), "저녁엔 계단 끝이 잘 안 보여서 걱정이에요", "이제 어두워져도 계단이 잘 보이네요."),
                Problem("C", "안쪽 골목", FieldProblemKind.Dark, true, true, new Vector2(9.5f, 4.6f), new Vector2(9.6f, 4.8f), new Vector2(4.4f, 5.4f),
                    new Vector2(9.5f, 6.2f), "골목 안은 해만 지면 불빛이 없어요", "이제 골목을 편하게 지나갈 수 있어요"),
                Problem("D", "공원", FieldProblemKind.Dark, false, false, new Vector2(9.5f, -4.2f), new Vector2(10.4f, -4.8f), new Vector2(8f, 4.6f),
                    new Vector2(11.5f, -5.6f), "저녁 산책길이 어두워질까 봐 걱정돼요", "밤에도 공원을 걸을 수 있겠어요")
            };
            Memo(m1, ("A", "저녁 보행 많음 · 조명 없음"), ("B", "계단 끝 그늘 · 발 헛디딜 위험"), ("C", "골목 안 불빛 없음"), ("D", "공원 산책길 조명 없음"));
            Early(m1, ("A", 0.12f, "입구가 어두워서 무서워요"), ("B", 0.15f, "계단이 잘 안 보여요"), ("C", 0.15f, "골목이 너무 깜깜해요"), ("D", 0.1f, "공원이 어두워 산책을 못 해요"));
            m1.items = new List<FieldItemEntry>
            {
                Item("streetlamp", "가로등", 35, FieldItemVisual.StreetLamp, "넓게 밝힘", "가로등이 넓게 켜져요"),
                Item("walklight", "보행등", 20, FieldItemVisual.WalkLight, "가까이 밝힘", "보행등이 다가오는 사람을 감지해 켜져요")
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
            m2.title = "폭염보다 먼저 준비하는 보건소";
            m2.placeName = "새봄시 보건소";
            m2.intro = "아직 한산할 때 더위와 물 부족이 올 곳을 확인하고 장치를 놓을 자리를 정해요.";
            m2.arrivalLine = "아직은 한산하네. 오후 더위 전에 둘러보자.";
            m2.theme = FieldTheme.Clinic;
            m2.forecast = "14:00 폭염 · 보건소 대기 인원 증가 예상";
            m2.nowTime = "11:00";
            m2.eventTime = "14:00";
            m2.eventCaption = "폭염 경보, 보건소 대기 인원이 늘어요";
            m2.responseCaption = "온도 센서가 더위를 감지했어요";
            m2.crowd = new List<Vector2> { new Vector2(1.2f, -4.4f), new Vector2(-0.9f, -5.3f) };
            m2.budget = 85;
            m2.draftNumbers = true;
            m2.playerStart = new Vector2(0f, -7f);
            m2.problems = new List<FieldProblemEntry>
            {
                Problem("H", "대기실", FieldProblemKind.Heat, true, true, new Vector2(-5f, 3.2f), new Vector2(-4.2f, 3.6f), new Vector2(11.4f, 6.4f),
                    new Vector2(-6.4f, 4.8f), "벌써 부채질이 나오네요. 오후엔 더 덥겠죠?", "이제 대기실이 시원해졌어요"),
                Problem("W", "복도 쉼터", FieldProblemKind.Thirst, true, true, new Vector2(6f, 4.2f), new Vector2(6f, 4.4f), new Vector2(7.4f, 5f),
                    new Vector2(6.4f, 5.2f), "물통이 벌써 비었어요. 사람이 늘면 어쩌죠?", "이제 목마를 때 물을 마실 수 있겠어요."),
                Problem("O", "바깥 줄", FieldProblemKind.Heat, false, false, new Vector2(-4.6f, -4.6f), new Vector2(-4f, -3.4f), new Vector2(6.4f, 4.2f),
                    new Vector2(-5.4f, -3.4f), "오후엔 여기 줄이 길어진대요. 햇볕이 걱정이에요", "이제 밖에서 기다리기 한결 나아요")
            };
            Memo(m2, ("H", "실내만 식힐 때는 작은 장치로도 충분."), ("W", "복도 쉼터 물통이 빔"), ("O", "현관의 넓은 장치는 바깥 대기 공간까지 대응."));
            Early(m2, ("H", 0.3f, "대기실이 너무 더워요"), ("W", 1f, "물통이 비어서 마실 물이 없어요"), ("O", 0.25f, "햇볕 아래서 기다리기 힘들어요"));
            m2.items = new List<FieldItemEntry>
            {
                Item("aircon", "냉방장치", 40, FieldItemVisual.AirConditioner, "실내 한 곳 냉방", "냉방장치가 켜져요"),
                Item("bigcool", "대형 냉방기", 60, FieldItemVisual.FloorCooler, "넓은 냉방 · 현관이면 바깥까지", "대형 냉방기가 바람을 보내요"),
                Item("water", "급수장치", 25, FieldItemVisual.WaterDispenser, "마실 물", "주민이 급수장치를 이용해요"),
                Item("shade", "그늘막", 20, FieldItemVisual.Shade, "실외 햇빛 가림", "주민이 그늘막 아래로 피해요")
            };
            // 바깥 줄(O)은 두 가지로 풀 수 있다: 줄 옆 그늘막(20) 또는 현관에 둔 대형 냉방기(60, 대기실까지 함께).
            // 대형 냉방기를 대기실 안쪽에 두면 대기실만 시원해진다(같은 물품도 자리에 따라 효과가 다르다).
            // 별 3개 계획은 '대기실 냉방 + 쉼터 급수 + 그늘막'과 '현관 대형 냉방기 + 쉼터 급수'로 둘 다 85.
            m2.spots = new List<FieldSpotEntry>
            {
                Spot("waiting", "대기실 안쪽", new Vector2(-8f, 6f), Opt("aircon", "H"), Opt("bigcool", "H")),
                Spot("hall", "현관 안쪽", new Vector2(0f, 1.4f), Opt("aircon", "H"), Opt("bigcool", "H", "O")),
                Spot("corridor", "복도 쉼터", new Vector2(8.6f, 4.0f), Opt("water", "W")),
                Spot("yard", "대기 줄 옆", new Vector2(-3f, -3.6f), Opt("shade", "O"))
            };
            m2.reactions = new List<FieldReaction>
            {
                React("yard", "shade", "O", "이제 햇볕을 피해서 기다릴 수 있겠어요.", new Vector2(-2.7f, -3.05f)),
                React("hall", "bigcool", "O", "여기서 기다리니 한결 덜 덥네요.", new Vector2(-2.4f, -1.2f)),
                React("corridor", "water", "W", "이제 목마를 때 물을 마실 수 있겠어요.", new Vector2(7.7f, 4.5f))
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
            m3.title = "폭우에도 멈추지 않는 대피소";
            m3.placeName = "새봄시 대피소";
            m3.intro = "약한 비가 오는 지금, 침수와 정전이 올 곳을 확인하고 장치를 놓을 자리를 정해요.";
            m3.arrivalLine = "비가 더 거세지기 전에 서둘러야겠어.";
            m3.theme = FieldTheme.Shelter;
            m3.forecast = "30분 뒤 강우 증가 · 진입로 침수·전력 중단 위험";
            m3.nowTime = "15:00";
            m3.eventTime = "15:30";
            m3.eventCaption = "비가 거세지고 전력 공급이 끊겼어요";
            m3.responseCaption = "정전·수위 센서가 상황을 감지했어요";
            m3.crowd = new List<Vector2> { new Vector2(-0.6f, 1.5f) };
            // 대피하러 오는 주민: 큰길을 따라 와서 정문 진입로 입구(물 밖)에서 기다리고, 진입로 배수(R)가 실제로 해결되어
            // 깊은 물이 빠진 뒤에만 진입로 가운데로 올라가 대피소 앞에 선다. 진입로 끝 대형 펌프(-2.4,-5.4) 아래쪽 큰길로 지나간다.
            m3.crowdRoutes = new List<FieldCrowdRoute>
            {
                new FieldCrowdRoute
                {
                    visitor = 0, needsProblem = "R", start = new Vector2(-12f, -6.7f), waitAt = new Vector2(-0.6f, -6.0f),
                    via = new List<Vector2> { new Vector2(0f, -5.7f), new Vector2(0f, 0.9f) }
                }
            };
            m3.budget = 85;
            m3.draftNumbers = true;
            m3.playerStart = new Vector2(-14f, -6.5f);
            m3.problems = new List<FieldProblemEntry>
            {
                Problem("P", "대피소 전력", FieldProblemKind.Power, true, true, new Vector2(0f, 2.2f), new Vector2(0f, 5f), new Vector2(11f, 5f),
                    new Vector2(-2f, 2.2f), "비가 세지면 전기가 끊길까 봐 걱정이에요", "불이 켜져서 안심돼요"),
                Problem("R", "정문 진입로", FieldProblemKind.Flood, true, true, new Vector2(0f, -4.3f), new Vector2(0f, -2.4f), new Vector2(5.2f, 6f),
                    new Vector2(1.7f, -4.6f), "벌써 물이 고였어요. 비가 더 오면 못 들어오겠어요", "이제 정문 길로 안전하게 들어갈 수 있어요."),
                Problem("B", "보조 통로", FieldProblemKind.Flood, false, false, new Vector2(10f, 1.5f), new Vector2(10f, -0.4f), new Vector2(4f, 7f),
                    new Vector2(11.4f, 1.6f), "옆길로 빗물이 조금씩 흘러들어요", "이제 옆길로도 다닐 수 있어요")
            };
            Memo(m3, ("P", "배전반 하나뿐 · 비상전원 없음"), ("R", "얕게 고인 물 · 강우 증가 시 진입로 침수 위험"), ("B", "옆길로 빗물이 흘러듦"));
            Early(m3, ("P", 0f, "불이 꺼져서 불안해요"), ("R", 0.35f, "정문 길에 물이 차서 못 들어가요"), ("B", 0.12f, "옆길도 물에 잠겼어요"));
            m3.items = new List<FieldItemEntry>
            {
                Item("generator", "비상전원", 40, FieldItemVisual.Generator, "배전반에 연결", "비상전원으로 자동 전환돼요"),
                Item("portable", "이동형 발전기", 25, FieldItemVisual.PortableGenerator, "정문 가까이 연결", "이동형 발전기가 전기를 보내요"),
                Item("pump", "배수펌프", 30, FieldItemVisual.Pump, "고인 물 빼기", "배수펌프가 물을 빼요"),
                Item("bigpump", "대형 배수펌프", 45, FieldItemVisual.BigPump, "깊게 고인 물 빼기", "대형 배수펌프가 깊은 물을 빼요"),
                Item("sandbags", "모래주머니", 15, FieldItemVisual.Sandbags, "물이 들어오기 전 물막이", "모래주머니가 물이 넘어오지 않게 막아요")
            };
            // 정문 앞은 싼 이동형 발전기와 배수펌프가 겨루는 자리다. 발전기를 정문에 두면
            // 물은 더 깊게 고인 진입로 끝에서 대형 배수펌프로 빼야 한다. 두 별 3개 계획 모두 85.
            m3.spots = new List<FieldSpotEntry>
            {
                Spot("panel", "배전반 옆", new Vector2(-7f, 4.5f), Opt("generator", "P")),
                Spot("door", "정문 앞", new Vector2(2.8f, 1.8f), Opt("portable", "P"), Opt("pump", "R")),
                Spot("gate", "진입로 끝", new Vector2(-2.4f, -5.4f), Opt("bigpump", "R")),
                Spot("detour", "보조 통로 입구", new Vector2(7.6f, -3.4f), Opt("sandbags", "B"))
            };
            m3.obstacles = new List<FieldObstacle>
            {
                Block(FieldObstacleKind.Building, 0f, 5f, 10f, 4f),
                Block(FieldObstacleKind.Building, -11.5f, -2.5f, 5f, 4f),
                Block(FieldObstacleKind.Building, 12.5f, 6f, 5f, 3f),
                // 깊은 물(Puddle): 강우가 늘어 물이 깊어지면 이 사각형만 진하게 보이고 통행을 막는다. 둘레의 넓은 문제 구역은 얕은 물(통행 가능).
                // 진입로·보조 통로 폭 전체를 덮어, 주민이 기다리는 깊은 물을 플레이어도 지나가지 못한다(풀밭으로 돌아가는 것은 가능).
                Block(FieldObstacleKind.Puddle, 0f, -2.2f, 4.2f, 3.2f, "R"),
                Block(FieldObstacleKind.Puddle, 10f, -1f, 3.2f, 3.2f, "B"),
                Block(FieldObstacleKind.Tree, 6f, 2.6f, 1.2f, 1.2f),
                Block(FieldObstacleKind.Fence, 13.6f, -1f, 0.5f, 5f)
            };
            m3.paths = new List<FieldPath> { Path(0f, -3.2f, 3.2f, 10f), Path(0f, -6.6f, 32f, 2.2f), Path(10f, -1.6f, 3f, 9f), Path(0f, 2f, 14f, 1.6f) };
            yield return new Seed { AssetName = "FieldMission3_Shelter", Data = m3 };
        }
    }
}
