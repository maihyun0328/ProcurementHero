using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProcurementHero.Core.Field;
using UnityEngine;

namespace ProcurementHero.Field
{
    /// <summary>
    /// 미션 데이터로 탑다운 현장을 만든다. 충돌 영역은 그림의 바닥(발자국) 사각형과 같다.
    /// 문제 구역의 모습은 세 값으로만 정해진다: 확정 계획이 그 문제를 해결하는지(level, FieldRules 판정 그대로),
    /// 예측한 상황이 얼마나 일어났는지(hazard), 준비한 시설이 얼마나 대응했는지(respond).
    /// 배송·설치 때는 물품만 놓이고(hazard·respond 0), 시간이 흐른 뒤 상황이 일어나야 해결 모습이 나타난다.
    /// 건물·나무·사람은 발 위치로 깊이 정렬해 앞뒤가 자연스럽게 겹친다.
    /// </summary>
    public class FieldWorld : MonoBehaviour
    {
        public const float HalfWidth = 16f, HalfHeight = 8f;
        public const float ObserveRange = 2.2f;
        public const float PlayerRadius = 0.35f;
        const float TruckRoadY = -6.2f;

        public FieldMissionData Mission { get; private set; }
        public Rigidbody2D Player { get; private set; }
        /// <summary>현장 연출 시간. 수첩·일시정지 중에는 0을 돌려준다.</summary>
        public Func<float> Delta = () => Time.deltaTime;

        class Rig
        {
            public Transform root, visual, body, head, armBack, armFront, legL, legR, prop;
            public SpriteRenderer eye, mark, brim;
            public float walkPhase, breath;
        }

        class Site
        {
            public FieldProblemEntry data;
            public float level, target;
            public SpriteRenderer overlay, glow, tint;
            public readonly List<SpriteRenderer> waves = new List<SpriteRenderer>();
            public readonly List<SpriteRenderer> winds = new List<SpriteRenderer>();
            public readonly List<SpriteRenderer> windows = new List<SpriteRenderer>();
            public SpriteRenderer beacon, jugWater, sweat;
            public Rig resident;
            public GameObject tick;
            public Transform jug;
            public float phase, reactTimer, cheerTimer;
            public bool reacted;
            // 주민이 머무는 자리(anchor)와 해결 후 옮겨 가는 이동. 판정 결과에 맞는 반응(reaction)이 정한다.
            public Vector2 anchor, moveFrom, moveTo;
            public float moveT = 1f;
            public FieldReaction reaction;
            public FieldItemVisual? solvedBy;
            public string solvedSpot;
            public SpriteRenderer patch;
            public bool Moving => moveT < 1f;
        }

        class Puddle
        {
            public string clearedBy;
            public SpriteRenderer sprite, shine;
            public Collider2D collider;
        }

        readonly Dictionary<string, Site> sites = new Dictionary<string, Site>();
        readonly Dictionary<string, Transform> spotMarkers = new Dictionary<string, Transform>();
        readonly Dictionary<string, GameObject> installed = new Dictionary<string, GameObject>();
        readonly List<Puddle> puddles = new List<Puddle>();
        readonly List<Transform> rain = new List<Transform>();
        // 설치 연출 중에만 있는 임시 오브젝트(운반 상자·반짝임). 건너뛰기·재시작 때 모두 지운다.
        readonly List<GameObject> temps = new List<GameObject>();
        Transform truck, van;
        Rig player;
        // 미래 상황: hazard(예측한 상황의 진행 0~1)와 respond(준비한 시설의 자동 대응 0~1).
        float hazard, respond;
        SpriteRenderer sky, sun, cloud, playerHalo;
        readonly List<SpriteRenderer> litWindows = new List<SpriteRenderer>();
        class Visitor { public Rig rig; public Vector2 from, to; public FieldCrowdRoute route; public Vector2 last; public bool moving; }
        readonly List<Visitor> crowd = new List<Visitor>();
        float observePose, observeTimer, notebookPose;

        public static FieldWorld Build(Transform parent, FieldMissionData mission, bool withPlayer)
        {
            var go = new GameObject("FieldWorld_" + mission.id);
            if (parent) go.transform.SetParent(parent, false);
            var world = go.AddComponent<FieldWorld>();
            world.Mission = mission;
            world.BuildGround();
            world.BuildObstacles();
            world.BuildSites();
            world.BuildSpots();
            if (withPlayer) world.BuildPlayer();
            world.truck = world.BuildVehicle("Truck", Theme.Teal, Theme.TealDark, true);
            world.van = world.BuildVehicle("Van", Theme.Hex(0xF2EEE4), Theme.Teal, false);
            if (mission.theme == FieldTheme.Shelter) world.BuildRain();
            world.BuildSky();
            world.BuildCrowd();
            world.RenderAll();
            return world;
        }

        static void Kill(GameObject go)
        {
            if (!go) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        // ───────── 지형 ─────────

        void Palette(out Color ground, out Color path, out Color edge, out Color wall, out Color roof)
        {
            switch (Mission.theme)
            {
                case FieldTheme.NightAlley:
                    // 아직 밝은 오후의 골목. 저녁은 위에 덮는 하늘색(sky)으로 표현한다.
                    ground = Theme.Hex(0x6E9472); path = Theme.Hex(0xBDB39C); edge = Theme.Hex(0x8F8672);
                    wall = Theme.Hex(0xC9A98E); roof = Theme.Hex(0x7A4A5C);
                    break;
                case FieldTheme.Clinic:
                    ground = Theme.Hex(0x9FD27E); path = Theme.Hex(0xEADBB8); edge = Theme.Hex(0xC9B58C);
                    wall = Theme.ClinicWall; roof = Theme.TealDark;
                    break;
                default:
                    ground = Theme.Hex(0x7F9A86); path = Theme.Hex(0xB5AE9A); edge = Theme.Hex(0x8C8672);
                    wall = Theme.Hex(0xB8C7D3); roof = Theme.RoofBlue;
                    break;
            }
        }

        void BuildGround()
        {
            Palette(out var ground, out var path, out var edge, out _, out _);
            FieldArt.Box(transform, "Ground", ground, Vector2.zero, new Vector2(HalfWidth * 2 + 2, HalfHeight * 2 + 4), FieldArt.OrderGround);
            var rng = new System.Random(Mission.id.GetHashCode());
            for (int i = 0; i < 70; i++)
            {
                var p = new Vector2((float)rng.NextDouble() * 32f - 16f, (float)rng.NextDouble() * 16f - 8f);
                FieldArt.Round(transform, "Tuft", Color.Lerp(ground, Color.black, 0.12f), p, new Vector2(0.28f, 0.1f), FieldArt.OrderGround);
            }
            foreach (var p in Mission.paths)
                FieldArt.Round(transform, "PathEdge", edge, p.center, p.size + new Vector2(0.24f, 0.24f), FieldArt.OrderPath);
            foreach (var p in Mission.paths)
                FieldArt.Round(transform, "Path", path, p.center, p.size, FieldArt.OrderPath);
            foreach (var p in Mission.paths)
            {
                int count = Mathf.Clamp(Mathf.RoundToInt(p.size.x * p.size.y / 6f), 2, 20);
                for (int i = 0; i < count; i++)
                {
                    var q = p.center + new Vector2(((float)rng.NextDouble() - 0.5f) * p.size.x * 0.85f, ((float)rng.NextDouble() - 0.5f) * p.size.y * 0.8f);
                    FieldArt.Round(transform, "Stone", Color.Lerp(path, edge, 0.5f), q, new Vector2(0.36f, 0.16f), FieldArt.OrderPathDetail);
                }
            }

            if (Mission.theme == FieldTheme.NightAlley)
                for (int i = 0; i < 7; i++)
                    FieldArt.Box(transform, "Step", Theme.Hex(0xB2AA96), new Vector2(-0.6f, -1.4f + i * 0.55f), new Vector2(3.2f, 0.14f), FieldArt.OrderPathDetail);
            if (Mission.theme == FieldTheme.Clinic)
                sun = FieldArt.Glow(transform, "Sun", Theme.WithAlpha(Theme.Hex(0xFFD36B), 0.55f), new Vector2(13.5f, 6.5f), new Vector2(5, 5), FieldArt.OrderZone);

            AddCollider(new Vector2(0, HalfHeight + 0.5f), new Vector2(HalfWidth * 2 + 2, 1));
            AddCollider(new Vector2(0, -HalfHeight - 0.5f), new Vector2(HalfWidth * 2 + 2, 1));
            AddCollider(new Vector2(HalfWidth + 0.5f, 0), new Vector2(1, HalfHeight * 2 + 2));
            AddCollider(new Vector2(-HalfWidth - 0.5f, 0), new Vector2(1, HalfHeight * 2 + 2));
        }

        Collider2D AddCollider(Vector2 center, Vector2 size)
        {
            var go = new GameObject("Collider");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = center;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = size;
            return col;
        }

        void BuildObstacles()
        {
            Palette(out _, out _, out _, out var wall, out var roof);
            foreach (var o in Mission.obstacles)
            {
                float foot = o.center.y - o.size.y / 2;
                var collider = AddCollider(o.center, o.size);
                switch (o.kind)
                {
                    case FieldObstacleKind.Building:
                    {
                        // 발자국(충돌) 위로 벽과 지붕을 높게 그려, 뒤쪽을 지날 때 건물에 가려지게 한다.
                        var root = FieldArt.DepthRoot(transform, "Building", o.center, foot);
                        float h = o.size.y + 1.1f;
                        float cy = 0.55f;
                        FieldArt.Round(root, "Wall", wall, new Vector2(0, cy), new Vector2(o.size.x, h), 0);
                        FieldArt.Box(root, "Base", Color.Lerp(wall, Color.black, 0.18f), new Vector2(0, -o.size.y / 2 + 0.12f), new Vector2(o.size.x - 0.2f, 0.24f), 1);
                        FieldArt.Round(root, "Roof", roof, new Vector2(0, cy + h / 2 - h * 0.2f), new Vector2(o.size.x + 0.4f, h * 0.42f), 3);
                        FieldArt.Box(root, "RoofLine", Color.Lerp(roof, Color.black, 0.25f), new Vector2(0, cy + h / 2 - h * 0.41f), new Vector2(o.size.x + 0.3f, 0.12f), 4);
                        int columns = Mathf.Max(1, Mathf.FloorToInt(o.size.x / 1.6f));
                        for (int i = 0; i < columns; i++)
                        {
                            float x = -o.size.x / 2 + (i + 0.5f) * o.size.x / columns;
                            var window = FieldArt.Round(root, "Window", Theme.Hex(0x8FB7D6), new Vector2(x, -o.size.y / 2 + 0.95f), new Vector2(0.62f, 0.55f), 2);
                            if (Mission.theme == FieldTheme.NightAlley) litWindows.Add(window);
                            FieldArt.Box(root, "Sill", Color.Lerp(wall, Color.black, 0.3f), new Vector2(x, -o.size.y / 2 + 0.62f), new Vector2(0.7f, 0.08f), 2);
                        }
                        break;
                    }
                    case FieldObstacleKind.Wall:
                    {
                        var root = FieldArt.DepthRoot(transform, "Wall", o.center, foot);
                        Color c = Mission.theme == FieldTheme.Clinic ? Theme.Hex(0x6B7799) : Theme.Hex(0x7C766C);
                        FieldArt.Box(root, "Body", c, new Vector2(0, 0.2f), o.size + new Vector2(0, 0.4f), 0);
                        FieldArt.Box(root, "Top", Color.Lerp(c, Color.white, 0.25f), new Vector2(0, o.size.y / 2 + 0.35f), new Vector2(o.size.x, 0.12f), 1);
                        break;
                    }
                    case FieldObstacleKind.Fence:
                    {
                        var root = FieldArt.DepthRoot(transform, "Fence", o.center, foot);
                        FieldArt.Box(root, "Rail", Theme.Hex(0x8A6440), new Vector2(0, 0.3f), o.size + new Vector2(0, 0.3f), 0);
                        int posts = Mathf.Max(2, Mathf.RoundToInt(Mathf.Max(o.size.x, o.size.y) / 0.8f));
                        for (int i = 0; i <= posts; i++)
                        {
                            Vector2 p = o.size.x > o.size.y
                                ? new Vector2(-o.size.x / 2 + i * o.size.x / posts, 0.35f)
                                : new Vector2(0, -o.size.y / 2 + i * o.size.y / posts + 0.3f);
                            FieldArt.Box(root, "Post", Theme.Hex(0x6E4E31), p, new Vector2(0.14f, 0.5f), 1);
                        }
                        break;
                    }
                    case FieldObstacleKind.Puddle:
                    {
                        var sprite = FieldArt.Round(transform, "Puddle", Theme.Water, o.center, o.size, FieldArt.OrderFlat);
                        var shine = FieldArt.Round(transform, "PuddleShine", Theme.WithAlpha(Color.white, 0.5f), o.center + new Vector2(-o.size.x * 0.15f, o.size.y * 0.15f),
                            new Vector2(o.size.x * 0.4f, 0.1f), FieldArt.OrderFlat + 1);
                        puddles.Add(new Puddle { clearedBy = o.clearedBy, sprite = sprite, shine = shine, collider = collider });
                        break;
                    }
                    default:
                    {
                        var root = FieldArt.DepthRoot(transform, "Tree", o.center, foot);
                        float d = Mathf.Max(o.size.x, o.size.y);
                        FieldArt.Shadow(root, new Vector2(0, -o.size.y / 2 + 0.05f), d * 1.1f);
                        FieldArt.Box(root, "Trunk", Theme.Hex(0x7A5A3A), new Vector2(0, -o.size.y * 0.15f), new Vector2(d * 0.22f, o.size.y * 0.7f), 0);
                        FieldArt.Dot(root, "LeafBack", Theme.Hex(0x2F6B40), new Vector2(0, d * 0.45f), d * 1.25f, 1);
                        FieldArt.Dot(root, "LeafA", Theme.Hex(0x3F8A50), new Vector2(-d * 0.25f, d * 0.55f), d * 0.8f, 2);
                        FieldArt.Dot(root, "LeafB", Theme.Hex(0x4C9A5C), new Vector2(d * 0.22f, d * 0.7f), d * 0.7f, 3);
                        FieldArt.Dot(root, "Shine", Theme.WithAlpha(Color.white, 0.18f), new Vector2(-d * 0.15f, d * 0.85f), d * 0.3f, 4);
                        break;
                    }
                }
            }
        }

        void BuildSites()
        {
            foreach (var p in Mission.problems)
            {
                int index = Mission.problems.IndexOf(p);
                var site = new Site { data = p, phase = index * 1.7f % 6f };
                switch (p.kind)
                {
                    case FieldProblemKind.Dark:
                        site.glow = FieldArt.Glow(transform, "Light_" + p.id, Theme.WithAlpha(FieldArt.Lamp, 0f), p.zoneCenter, p.zoneSize * 1.25f, FieldArt.OrderGlow);
                        site.overlay = FieldArt.Round(transform, "Dark_" + p.id, Theme.WithAlpha(Theme.Hex(0x0B1026), 0.66f), p.zoneCenter, p.zoneSize, FieldArt.OrderDark);
                        break;
                    case FieldProblemKind.Heat:
                        site.tint = FieldArt.Round(transform, "Heat_" + p.id, Theme.WithAlpha(Theme.Heat, 0.32f), p.zoneCenter, p.zoneSize, FieldArt.OrderZone);
                        for (int i = 0; i < 4; i++)
                            site.waves.Add(FieldArt.Round(transform, "HeatWave", Theme.WithAlpha(Theme.Heat, 0.8f),
                                p.zoneCenter + new Vector2(-p.zoneSize.x * 0.35f + i * p.zoneSize.x * 0.23f, -p.zoneSize.y * 0.25f), new Vector2(0.9f, 0.14f), FieldArt.OrderDark - 1));
                        for (int i = 0; i < 3; i++)
                            site.winds.Add(FieldArt.Round(transform, "CoolWind", Theme.WithAlpha(Theme.Cool, 0f),
                                p.zoneCenter + new Vector2(-p.zoneSize.x * 0.3f, p.zoneSize.y * (0.2f - i * 0.2f)), new Vector2(1.2f, 0.12f), FieldArt.OrderDark - 1));
                        break;
                    case FieldProblemKind.Thirst:
                    {
                        site.tint = FieldArt.Round(transform, "Thirst_" + p.id, Theme.WithAlpha(Theme.Danger, 0.12f), p.zoneCenter, p.zoneSize, FieldArt.OrderZone);
                        var jug = FieldArt.DepthRoot(transform, "Jug", p.observePoint + new Vector2(1.6f, 0.4f), p.observePoint.y);
                        site.jug = jug;
                        FieldArt.Shadow(jug, new Vector2(0, -0.45f), 0.8f);
                        FieldArt.Round(jug, "Body", Theme.Paper, Vector2.zero, new Vector2(0.6f, 0.9f), 0);
                        site.jugWater = FieldArt.Round(jug, "Water", Theme.Cool, new Vector2(0, -0.25f), new Vector2(0.44f, 0.12f), 1);
                        break;
                    }
                    case FieldProblemKind.Power:
                        site.glow = FieldArt.Glow(transform, "PowerLight_" + p.id, Theme.WithAlpha(FieldArt.Lamp, 0f), p.zoneCenter, p.zoneSize * 1.1f, FieldArt.OrderGlow);
                        for (int i = 0; i < 3; i++)
                            site.windows.Add(FieldArt.Round(transform, "Window", Theme.Window,
                                p.zoneCenter + new Vector2(-3f + i * 3f, -0.2f), new Vector2(1.4f, 1f), FieldArt.OrderGlow - 2));
                        site.beacon = FieldArt.Dot(transform, "Beacon", Theme.Warning, p.zoneCenter + new Vector2(0, 1.9f), 0.5f, FieldArt.OrderGlow - 1);
                        break;
                    default:
                        site.overlay = FieldArt.Round(transform, "Flood_" + p.id, Theme.WithAlpha(Theme.Hex(0x4F99DB), 0.62f), p.zoneCenter, p.zoneSize, FieldArt.OrderZone);
                        for (int i = 0; i < 3; i++)
                            site.waves.Add(FieldArt.Round(transform, "Ripple", Theme.WithAlpha(Color.white, 0.6f),
                                p.zoneCenter + new Vector2(-p.zoneSize.x * 0.25f + i * p.zoneSize.x * 0.25f, -p.zoneSize.y * 0.2f + i * 0.6f), new Vector2(0.8f, 0.12f), FieldArt.OrderZone + 1));
                        break;
                }

                site.anchor = p.residentPosition;
                site.resident = BuildPerson(p.residentPosition, index, false);
                AddProp(site);
                site.tick = new GameObject("Observed_" + p.id);
                site.tick.transform.SetParent(transform, false);
                site.tick.transform.localPosition = p.observePoint + new Vector2(0, 1.1f);
                FieldArt.Dot(site.tick.transform, "Badge", Theme.Success, Vector2.zero, 0.55f, FieldArt.OrderMarker);
                FieldArt.Box(site.tick.transform, "S", Color.white, new Vector2(-0.08f, -0.04f), new Vector2(0.07f, 0.2f), FieldArt.OrderMarker + 1).transform.localRotation = Quaternion.Euler(0, 0, 45);
                FieldArt.Box(site.tick.transform, "L", Color.white, new Vector2(0.05f, 0.02f), new Vector2(0.07f, 0.34f), FieldArt.OrderMarker + 1).transform.localRotation = Quaternion.Euler(0, 0, -38);
                site.tick.SetActive(false);
                sites[p.id] = site;
                Render(site);
            }
        }

        /// <summary>문제마다 주민이 드는 소품(부채, 빈 컵)과 땀방울.</summary>
        void AddProp(Site site)
        {
            var r = site.resident;
            switch (site.data.kind)
            {
                case FieldProblemKind.Heat:
                    r.prop = FieldArt.Sprite(r.armFront, "Fan", ShapeSprites.Triangle, Theme.Hex(0xF4E3B5), new Vector2(0.05f, -0.42f), new Vector2(0.34f, 0.3f), 10).transform;
                    site.sweat = FieldArt.Dot(r.head, "Sweat", Theme.Cool, new Vector2(0.24f, 0.12f), 0.1f, 9);
                    break;
                case FieldProblemKind.Thirst:
                    r.prop = FieldArt.Round(r.armFront, "Cup", Theme.Paper, new Vector2(0.04f, -0.4f), new Vector2(0.16f, 0.2f), 10).transform;
                    break;
            }
        }

        Rig BuildPerson(Vector2 position, int index, bool isPlayer)
        {
            Color[] shirts = { Theme.Hex(0x3BA7A0), Theme.Hex(0xE58E3A), Theme.Hex(0x8E7CC3), Theme.Hex(0xD9655B) };
            Color[] hair = { Theme.Hex(0x3B2A22), Theme.Hex(0x6B4A2E), Theme.Hex(0x2B2B33), Theme.Hex(0x8A5A3A) };
            var rig = new Rig { breath = index * 0.9f };
            rig.root = FieldArt.DepthRoot(transform, isPlayer ? "Player" : "Resident", position, position.y - 0.45f);
            FieldArt.Shadow(rig.root, new Vector2(0, -0.46f), 0.75f);
            rig.visual = new GameObject("Visual").transform;
            rig.visual.SetParent(rig.root, false);

            Color shirt = isPlayer ? Theme.Navy : shirts[index % shirts.Length];
            Color pants = isPlayer ? Theme.Hex(0x2A3556) : Theme.Hex(0x4A4E63);
            rig.legL = FieldArt.Limb(rig.visual, "LegL", pants, new Vector2(-0.11f, -0.16f), new Vector2(0.16f, 0.32f), 1);
            rig.legR = FieldArt.Limb(rig.visual, "LegR", pants, new Vector2(0.11f, -0.16f), new Vector2(0.16f, 0.32f), 1);
            rig.armBack = FieldArt.Limb(rig.visual, "ArmBack", Color.Lerp(shirt, Color.black, 0.2f), new Vector2(-0.24f, 0.18f), new Vector2(0.13f, 0.36f), 2);
            rig.body = new GameObject("Body").transform;
            rig.body.SetParent(rig.visual, false);
            FieldArt.Round(rig.body, "Torso", shirt, new Vector2(0, 0.04f), new Vector2(0.56f, 0.5f), 3);
            if (isPlayer)
            {
                FieldArt.Box(rig.body, "Vest", Theme.Teal, new Vector2(0, 0.02f), new Vector2(0.44f, 0.26f), 4);
                FieldArt.Box(rig.body, "Badge", Theme.Paper, new Vector2(0.1f, 0.1f), new Vector2(0.1f, 0.07f), 5);
            }
            rig.head = new GameObject("Head").transform;
            rig.head.SetParent(rig.visual, false);
            rig.head.localPosition = new Vector2(0, 0.52f);
            FieldArt.Dot(rig.head, "Face", Theme.Skin, Vector2.zero, 0.5f, 6);
            rig.eye = FieldArt.Dot(rig.head, "Eye", FieldArt.Ink, new Vector2(0.11f, 0.02f), 0.08f, 7);
            FieldArt.Dot(rig.head, "Cheek", Theme.WithAlpha(Theme.Roof, 0.35f), new Vector2(0.12f, -0.1f), 0.1f, 7);
            if (isPlayer)
            {
                FieldArt.Round(rig.head, "Cap", Theme.TealDark, new Vector2(-0.02f, 0.17f), new Vector2(0.54f, 0.24f), 8);
                rig.brim = FieldArt.Round(rig.head, "Brim", Theme.TealDark, new Vector2(0.2f, 0.1f), new Vector2(0.3f, 0.09f), 8);
            }
            else
            {
                FieldArt.Round(rig.head, "Hair", hair[index % hair.Length], new Vector2(-0.04f, 0.14f), new Vector2(0.5f, 0.26f), 8);
            }
            rig.armFront = FieldArt.Limb(rig.visual, "ArmFront", Color.Lerp(shirt, Color.white, 0.1f), new Vector2(0.22f, 0.18f), new Vector2(0.13f, 0.36f), 9);
            if (isPlayer)
                rig.prop = FieldArt.Round(rig.armFront, "Notebook", Theme.Hex(0x8A5A34), new Vector2(0.08f, -0.36f), new Vector2(0.24f, 0.3f), 10).transform;
            rig.mark = FieldArt.Dot(rig.root, "Mood", isPlayer ? Theme.WithAlpha(Color.white, 0f) : Theme.NavySoft, new Vector2(0, 1.02f), 0.2f, 11);
            return rig;
        }

        void BuildSpots()
        {
            foreach (var s in Mission.spots)
            {
                var marker = new GameObject("Spot_" + s.id).transform;
                marker.SetParent(transform, false);
                marker.localPosition = s.position;
                FieldArt.Ellipse(marker, "Ring", Theme.WithAlpha(Theme.Paper, 0.8f), Vector2.zero, new Vector2(1f, 0.6f), FieldArt.OrderFlat);
                FieldArt.Ellipse(marker, "Hole", Theme.WithAlpha(Theme.Teal, 0.85f), Vector2.zero, new Vector2(0.66f, 0.4f), FieldArt.OrderFlat + 1);
                FieldArt.Box(marker, "CrossA", Theme.Paper, Vector2.zero, new Vector2(0.3f, 0.06f), FieldArt.OrderFlat + 2);
                FieldArt.Box(marker, "CrossB", Theme.Paper, Vector2.zero, new Vector2(0.06f, 0.2f), FieldArt.OrderFlat + 2);
                spotMarkers[s.id] = marker;
            }
        }

        void BuildPlayer()
        {
            player = BuildPerson(Mission.playerStart, 0, true);
            var go = player.root.gameObject;
            playerHalo = FieldArt.Glow(player.root, "Halo", Theme.WithAlpha(Color.white, 0f), new Vector2(0, 0.1f), new Vector2(2.4f, 2.4f), -6);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = PlayerRadius;
            col.offset = new Vector2(0, -0.1f);
            Player = rb;
        }

        Transform BuildVehicle(string name, Color body, Color trim, bool cargo)
        {
            var root = FieldArt.DepthRoot(transform, name, new Vector2(-HalfWidth - 5, TruckRoadY), -8f);
            FieldArt.Shadow(root, new Vector2(0, -0.6f), 3.2f);
            if (cargo)
            {
                FieldArt.Round(root, "Cargo", body, new Vector2(-0.4f, 0.25f), new Vector2(2.4f, 1.3f), 0);
                FieldArt.Box(root, "Stripe", Theme.Paper, new Vector2(-0.4f, 0.2f), new Vector2(2f, 0.12f), 1);
                FieldArt.Round(root, "Cab", trim, new Vector2(1.3f, 0f), new Vector2(1f, 0.95f), 0);
            }
            else
            {
                FieldArt.Round(root, "Body", body, new Vector2(0, 0.15f), new Vector2(2.8f, 1.1f), 0);
                FieldArt.Box(root, "Band", trim, new Vector2(0, 0.05f), new Vector2(2.8f, 0.2f), 1);
                FieldArt.Round(root, "Window", Theme.Sky, new Vector2(-0.3f, 0.45f), new Vector2(1.4f, 0.35f), 1);
                FieldArt.Round(root, "Door", Color.Lerp(body, Color.black, 0.08f), new Vector2(-0.2f, 0.05f), new Vector2(0.7f, 0.8f), 1);
            }
            FieldArt.Round(root, "Glass", Theme.Sky, new Vector2(1.45f, 0.2f), new Vector2(0.5f, 0.35f), 2);
            FieldArt.Dot(root, "WheelA", FieldArt.Ink, new Vector2(-1f, -0.5f), 0.55f, 3);
            FieldArt.Dot(root, "WheelB", FieldArt.Ink, new Vector2(1.1f, -0.5f), 0.55f, 3);
            FieldArt.Dot(root, "Light", FieldArt.Lamp, new Vector2(1.75f, -0.1f), 0.16f, 3);
            return root;
        }

        void BuildRain()
        {
            cloud = FieldArt.Box(transform, "Cloud", Theme.WithAlpha(Theme.Hex(0x45506A), 0.25f), new Vector2(0, 7.2f), new Vector2(34, 3), FieldArt.OrderWeather - 1);
            var rng = new System.Random(7);
            for (int i = 0; i < 46; i++)
            {
                var d = FieldArt.Round(transform, "Rain", Theme.WithAlpha(Theme.Cool, 0.65f),
                    new Vector2((float)rng.NextDouble() * 32 - 16, (float)rng.NextDouble() * 18 - 8), new Vector2(0.08f, 0.5f), FieldArt.OrderWeather);
                rain.Add(d.transform);
            }
        }

        /// <summary>현장 전체에 덮는 하늘빛(저녁·폭염·폭우). 문제 구역의 어둠·불빛보다 아래, 건물·사람보다 위.</summary>
        void BuildSky()
        {
            sky = FieldArt.Box(transform, "Sky", Color.clear, Vector2.zero, new Vector2(HalfWidth * 2 + 4, HalfHeight * 2 + 6), FieldArt.OrderDark - 5);
        }

        /// <summary>상황이 일어나면 늘어나는 이용객(장식용 소수). 판정·충돌과 무관하다.</summary>
        void BuildCrowd()
        {
            int i = 0;
            foreach (var to in Mission.crowd)
            {
                var from = new Vector2(to.x + (i % 2 == 0 ? -2.5f : 2.5f), -HalfHeight - 1.2f);
                var rig = BuildPerson(from, Mission.problems.Count + i + 1, false);
                rig.mark.enabled = false;
                rig.root.gameObject.SetActive(false);
                var route = Mission.crowdRoutes.FirstOrDefault(r => r.visitor == i);
                if (route != null) from = route.start;
                crowd.Add(new Visitor { rig = rig, from = from, to = to, route = route, last = from });
                i++;
            }
        }

        // ───────── 상태 ─────────

        /// <summary>예측한 상황의 진행(0: 상황 전, 1: 상황이 일어남).</summary>
        public float Hazard => hazard;
        /// <summary>준비한 시설의 자동 대응 진행(0~1). 상황이 일어난 뒤에만 오른다.</summary>
        public float Respond => respond;
        /// <summary>해당 문제가 지금 화면에서 해결된 모습으로 보이는 정도(=확정 계획 해결 여부 × 시설 대응).</summary>
        public float SiteShown(string problemId) => sites.TryGetValue(problemId, out var s) ? Response(s) : -1f;
        /// <summary>장식용 이용객 중 지금 보이는 수(검증용).</summary>
        public int VisibleCrowd => crowd.Count(v => v.rig.root.gameObject.activeSelf);
        /// <summary>장식용 이용객의 지금 위치(검증용).</summary>
        public Vector2 CrowdPosition(int index) => crowd[index].rig.root.localPosition;

        /// <summary>
        /// 확인용: 확정 결과를 상황 진행·시설 대응의 중간 값으로 적용한다(연출 중 한 프레임과 같은 상태).
        /// 판정은 result 그대로이며 게임 진행에는 쓰지 않는다.
        /// </summary>
        public void ApplyStage(FieldEvaluation result, float hazardLevel, float respondLevel)
        {
            StopRoutines();
            ApplyState(result, Mathf.Clamp01(hazardLevel), Mathf.Clamp01(respondLevel));
        }

        /// <summary>
        /// 이용객 위치. 조건부 길이 있으면: 상황 진행만큼 대기 지점까지 오고, 필요한 문제가 확정 계획으로 해결된 경우에만
        /// 시설 대응만큼 목적지로 간다(해결 안 됐으면 대기 지점에 머문다). 없으면 아래에서 곧장 걸어온다.
        /// </summary>
        Vector2 VisitorPosition(Visitor v)
        {
            if (v.route == null) return Vector2.Lerp(v.from, v.to, Mathf.Clamp01(hazard * 1.15f));
            var approach = Mathf.Clamp01(hazard * 1.15f);
            if (approach < 1f || !sites.TryGetValue(v.route.needsProblem ?? "", out var need) || need.level < 1f)
                return Vector2.Lerp(v.route.start, v.route.waitAt, approach);
            var points = new List<Vector2> { v.route.waitAt };
            points.AddRange(v.route.via);
            points.Add(v.to);
            return AlongPath(points, respond);
        }

        static Vector2 AlongPath(List<Vector2> points, float t)
        {
            float total = 0;
            for (int i = 1; i < points.Count; i++) total += Vector2.Distance(points[i - 1], points[i]);
            float goal = Mathf.Clamp01(t) * total;
            for (int i = 1; i < points.Count; i++)
            {
                float d = Vector2.Distance(points[i - 1], points[i]);
                if (goal <= d || i == points.Count - 1) return Vector2.Lerp(points[i - 1], points[i], d > 0 ? Mathf.Clamp01(goal / d) : 1f);
                goal -= d;
            }
            return points[points.Count - 1];
        }

        public IEnumerable<FieldProblemEntry> Problems => Mission.problems;
        public Vector2 PlayerPosition => Player ? (Vector2)Player.transform.localPosition : Mission.playerStart;
        public int InstalledCount => installed.Count;

        /// <summary>해당 문제로 사라지는 물웅덩이가 아직 길을 막고 있는지(검증용).</summary>
        public bool PuddleBlocking(string problemId) => puddles.Any(p => p.clearedBy == problemId && p.collider.enabled);
        public float SiteLevel(string problemId) => sites.TryGetValue(problemId, out var s) ? s.level : -1f;

        /// <summary>주민의 지금 위치(이동 중이면 이동 중인 위치).</summary>
        public Vector2 ResidentPosition(string problemId) => sites.TryGetValue(problemId, out var s) ? (Vector2)s.resident.root.localPosition : Vector2.zero;

        /// <summary>주민이 머무는 자리(이동이 끝난 뒤의 위치). 설치 전에는 데이터의 대기 위치.</summary>
        public Vector2 ResidentAnchor(string problemId) => sites.TryGetValue(problemId, out var s) ? s.anchor : Vector2.zero;

        public bool ResidentMoved(string problemId) => sites.TryGetValue(problemId, out var s) && (s.Moving || s.anchor != s.data.residentPosition);

        /// <summary>
        /// 지금 들려줄 주민 대사. 시설이 대응해 해결된 모습이면 해결 대사, 상황이 일어났는데 해결되지 않았으면 상황 대사,
        /// 상황 전이면 초기 징후 대사. 판정은 바꾸지 않고 보여 주는 단계만 따른다.
        /// </summary>
        public string LineFor(FieldProblemEntry p)
        {
            if (!sites.TryGetValue(p.id, out var s)) return p.line;
            if (Response(s) >= 0.99f) return SolvedLine(p);
            if (hazard >= 0.99f && !string.IsNullOrEmpty(p.eventLine)) return p.eventLine;
            return p.line;
        }

        /// <summary>문제를 해결한 물품 그림(해결 전이면 null). 반응·그림을 고르는 데만 쓰고 판정에는 쓰지 않는다.</summary>
        public FieldItemVisual? SolvedBy(string problemId) => sites.TryGetValue(problemId, out var s) ? s.solvedBy : null;

        /// <summary>관찰할 때 다가가야 하는 지점. 주민이 옮겨 갔다면 새 자리 앞이다.</summary>
        public Vector2 ObservePoint(FieldProblemEntry p) =>
            sites.TryGetValue(p.id, out var s) && s.anchor != p.residentPosition ? s.anchor + new Vector2(0, -0.9f) : p.observePoint;

        /// <summary>해결 후 대사: 실제로 해결한 자리·물품의 반응 대사, 없으면 문제의 기본 해결 대사.</summary>
        public string SolvedLine(FieldProblemEntry p) =>
            sites.TryGetValue(p.id, out var s) && s.reaction != null && !string.IsNullOrEmpty(s.reaction.line) ? s.reaction.line : p.solvedLine;

        /// <summary>
        /// 엔딩용: 이미 정착한 주민을 원래 대기 자리에서 정착 자리까지 다시 걷게 한다(반응 데이터의 이동 그대로).
        /// 옮겨 가지 않는 주민은 해결 순간처럼 한 번 기뻐한다. 해결되지 않은 주민에게는 아무것도 하지 않는다.
        /// </summary>
        public void ReplayResolution(string problemId)
        {
            if (!sites.TryGetValue(problemId, out var s) || s.level < 0.99f) return;
            if (s.anchor != s.data.residentPosition)
            {
                s.moveFrom = s.data.residentPosition;
                s.moveTo = s.anchor;
                s.moveT = 0f;
                s.cheerTimer = 0f;
                s.resident.root.localPosition = s.moveFrom;
            }
            else s.cheerTimer = 0.8f;
        }

        /// <summary>남아 있는 임시 연출 오브젝트 수(검증용).</summary>
        public int TempObjectCount => temps.Count(t => t);

        /// <summary>설치된 물품의 자리 목록(검증용).</summary>
        public IEnumerable<string> InstalledSpots => installed.Keys;

        public void MarkObserved(string problemId, bool observed)
        {
            if (sites.TryGetValue(problemId, out var s)) s.tick.SetActive(observed);
        }

        /// <summary>가장 가까운, 관찰 범위 안의 문제 하나. 범위 밖이거나 담당자가 아직 내리지 않았으면 null.</summary>
        public FieldProblemEntry NearestInRange()
        {
            // player(그림 묶음)는 에디터 플레이 중 스크립트 재컴파일 뒤 비어 있을 수 있다(Rigidbody만 복원됨).
            if (!Player || player == null || !player.root.gameObject.activeSelf) return null;
            var pos = PlayerPosition;
            FieldProblemEntry best = null;
            float bestDistance = ObserveRange;
            foreach (var p in Mission.problems)
            {
                float d = Vector2.Distance(pos, ObservePoint(p));
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }
            return best;
        }

        public Vector3 WorldPoint(Vector2 local) => transform.TransformPoint(local);

        /// <summary>
        /// 판정 결과를 연출 없이 바로 적용한다. 결과가 있으면 '상황이 일어나고 시설이 대응을 마친' 최종 상태
        /// (재확인·이어하기·엔딩·건너뛰기 끝), null이면 설치 전·상황 전 상태(재계획)다.
        /// </summary>
        public void ApplyInstant(FieldEvaluation result)
        {
            StopRoutines();
            ApplyState(result, result != null ? 1f : 0f, result != null ? 1f : 0f);
        }

        /// <summary>물품만 설치한 상황 전 상태(배송·설치가 끝난 직후). 해결 모습은 아직 보이지 않는다.</summary>
        public void ApplyPrepared(FieldEvaluation result)
        {
            StopRoutines();
            ApplyState(result, 0f, 0f);
        }

        void StopRoutines()
        {
            if (installing != null) StopCoroutine(installing);
            installing = null;
            if (scenario != null) StopCoroutine(scenario);
            scenario = null;
            pendingDone = null;
            ClearTemps();
        }

        GameObject Temp(GameObject go)
        {
            temps.Add(go);
            return go;
        }

        void KillTemp(GameObject go)
        {
            temps.Remove(go);
            Kill(go);
        }

        void ClearTemps()
        {
            foreach (var go in temps) Kill(go);
            temps.Clear();
        }

        /// <summary>판정에서 해결된 문제에 대해 어떤 물품이 해결했는지와 주민 반응을 정한다(그림·대사용).</summary>
        void ResolveOutcome(Site site, FieldEvaluation result)
        {
            site.reaction = null;
            site.solvedBy = null;
            site.solvedSpot = null;
            if (result == null || !result.IsSolved(site.data.id)) return;
            site.reaction = Mission.ReactionFor(site.data.id, result.Assignments);
            if (site.reaction != null)
            {
                site.solvedBy = Mission.Item(site.reaction.itemId)?.visual;
                site.solvedSpot = site.reaction.spotId;
                return;
            }
            foreach (var spot in Mission.spots)
            {
                if (!result.Assignments.TryGetValue(spot.id, out var itemId)) continue;
                var option = spot.options.FirstOrDefault(o => o.itemId == itemId);
                if (option == null || !option.solves.Contains(site.data.id)) continue;
                site.solvedBy = Mission.Item(itemId)?.visual;
                site.solvedSpot = spot.id;
                return;
            }
        }

        static Vector2 SettledPosition(Site site) =>
            site.reaction != null && site.reaction.moveResident ? site.reaction.residentPosition : site.data.residentPosition;

        void ApplyState(FieldEvaluation result, float hazardLevel, float respondLevel)
        {
            truck.localPosition = new Vector2(-HalfWidth - 5, TruckRoadY);
            hazard = hazardLevel;
            respond = respondLevel;
            ClearInstalled();
            if (result != null)
                foreach (var pair in result.Assignments) PlaceItem(pair.Key, pair.Value);
            foreach (var site in sites.Values)
            {
                site.level = site.target = result != null && result.IsSolved(site.data.id) ? 1f : 0f;
                site.reacted = Response(site) >= 0.99f;
                site.cheerTimer = 0f;
                ResolveOutcome(site, result);
                // 건너뛰기·정상 재생 모두 같은 자리로 정착한다. 시설이 대응하기 전(설치 직후·재계획)에는 원래 대기 위치.
                site.anchor = respond >= 0.99f ? SettledPosition(site) : site.data.residentPosition;
                site.moveT = 1f;
                site.resident.root.localPosition = site.anchor;
                FieldArt.SetDepth(site.resident.root, site.anchor.y - 0.45f);
            }
            RenderAll();
        }

        void ClearInstalled()
        {
            foreach (var go in installed.Values) Kill(go);
            installed.Clear();
            placedItems.Clear();
            foreach (var m in spotMarkers.Values) m.gameObject.SetActive(true);
        }

        void PlaceItem(string spotId, string itemId)
        {
            var spot = Mission.Spot(spotId);
            var item = Mission.Item(itemId);
            if (spot == null || item == null) return;
            if (installed.TryGetValue(spotId, out var old)) Kill(old);
            var container = new GameObject("Installed_" + spotId).transform;
            container.SetParent(transform, false);
            container.localPosition = spot.position;
            var root = FieldArt.DepthRoot(container, "Model", Vector2.zero, spot.position.y);
            FieldArt.ItemModel(root, item.visual);
            if (item.visual == FieldItemVisual.Shade)
            {
                // 그늘은 땅에 그린다: 사람보다 아래, 길보다 위.
                FieldArt.Ellipse(container, "ShadeGround", Theme.WithAlpha(Theme.Navy, 0.22f), new Vector2(0.15f, 0.45f), new Vector2(2.5f, 1.0f), FieldArt.OrderFlat + 3);
            }
            installed[spotId] = container.gameObject;
            placedItems[spotId] = itemId;
            spotMarkers[spotId].gameObject.SetActive(false);
            // 막 설치한 조명은 꺼진 채로 둔다(상황이 와야 켜진다).
            RenderEnvironment();
        }

        Coroutine installing, scenario;
        FieldEvaluation pendingResult;
        Action pendingDone;

        public bool IsInstalling => installing != null;
        public bool IsScenarioPlaying => scenario != null;

        /// <summary>
        /// 배송 차량 → 지점별 상자 → 설치. 물품만 놓고 문제 구역·주민은 그대로 둔다(미래 상황의 성공을 미리 보여 주지 않는다).
        /// 건너뛰면 같은 '설치만 끝난' 상태를 한 번만 적용한다.
        /// </summary>
        public void PlayInstall(FieldEvaluation result, Action<Sfx> sound, Action done)
        {
            ApplyInstant(null);
            pendingResult = result;
            pendingDone = done;
            installing = StartCoroutine(InstallRoutine(result, sound));
        }

        public void SkipInstall()
        {
            if (installing == null) return;
            var result = pendingResult;
            var done = pendingDone;
            ApplyPrepared(result);
            done?.Invoke();
        }

        IEnumerator InstallRoutine(FieldEvaluation result, Action<Sfx> sound)
        {
            var order = Mission.spots.Where(s => result.Assignments.ContainsKey(s.id)).ToList();
            foreach (var spot in order)
            {
                Vector2 stop = new Vector2(Mathf.Clamp(spot.position.x - 1f, -HalfWidth + 2, HalfWidth - 2), TruckRoadY);
                yield return Move(truck, truck.localPosition, stop, Mathf.Clamp(Vector2.Distance(truck.localPosition, stop) / 14f, 0.35f, 1.1f));
                sound?.Invoke(Sfx.Arrive);

                var boxRoot = FieldArt.DepthRoot(transform, "Box", stop + new Vector2(0, 0.8f), -8f);
                Temp(boxRoot.gameObject);
                FieldArt.Round(boxRoot, "Box", Theme.Hex(0xD9A066), Vector2.zero, new Vector2(0.7f, 0.6f), 0);
                FieldArt.Box(boxRoot, "Tape", Theme.Hex(0xF2D49B), Vector2.zero, new Vector2(0.7f, 0.1f), 1);
                yield return Move(boxRoot, boxRoot.localPosition, spot.position, 0.45f, arc: 1.4f);
                yield return Animate(0.12f, t => boxRoot.localScale = new Vector3(1 + t * 0.3f, 1 - t * 0.4f, 1));
                KillTemp(boxRoot.gameObject);
                PlaceItem(spot.id, result.Assignments[spot.id]);
                var model = installed[spot.id].transform;
                yield return Animate(0.25f, t => model.localScale = Vector3.one * (0.4f + 0.6f * EaseBack(t)));
                Sparkle(spot.position + new Vector2(0, 0.6f));
                sound?.Invoke(Sfx.Install);
                yield return Wait(0.2f);
            }
            yield return Move(truck, truck.localPosition, new Vector2(HalfWidth + 5, TruckRoadY), 0.8f);
            // 연출 끝에도 같은 '설치만 끝난' 상태를 다시 적용해 건너뛰기와 같게 한다.
            var done = pendingDone;
            pendingDone = null;
            installing = null;
            ApplyState(result, 0f, 0f);
            done?.Invoke();
        }

        // ───────── 시간 경과 · 상황 발생 · 시설 대응 ─────────

        public const float ScenarioPassSeconds = 2.0f, ScenarioPeakSeconds = 0.6f, ScenarioRespondSeconds = 0.9f, ScenarioSettleSeconds = 1.4f;

        /// <summary>
        /// 확정 계획을 고정한 뒤 시간이 흐르고(hazard 0→1) 예측한 상황이 일어난다. 잠시 뒤 준비한 시설이 자동으로 대응하고(respond 0→1)
        /// 주민이 시설을 이용한다. 해결 여부는 result(FieldRules 판정)만 따른다. stage: 0 시간 경과 시작, 1 상황 발생, 2 시설 대응.
        /// 건너뛰면 끝까지 본 것과 같은 최종 상태를 한 번만 적용한다.
        /// </summary>
        public void PlayScenario(FieldEvaluation result, Action<Sfx> sound, Action<int> stage, Action done)
        {
            StopRoutines();
            ApplyState(result, 0f, 0f);
            // 물이 찰 수 있는 웅덩이 위에 서 있었다면 충돌이 켜지기 전에 출발 지점으로 옮긴다(벽 안에 끼지 않게).
            if (Player && puddles.Any(p => Mathf.Abs(PlayerPosition.x - p.collider.transform.localPosition.x) < ((BoxCollider2D)p.collider).size.x / 2 + PlayerRadius + 0.2f
                                        && Mathf.Abs(PlayerPosition.y - p.collider.transform.localPosition.y) < ((BoxCollider2D)p.collider).size.y / 2 + PlayerRadius + 0.2f))
                PlacePlayer(Mission.playerStart, Vector2.right);
            pendingResult = result;
            pendingDone = done;
            scenario = StartCoroutine(ScenarioRoutine(result, sound, stage));
        }

        public void SkipScenario()
        {
            if (scenario == null) return;
            var result = pendingResult;
            var done = pendingDone;
            ApplyInstant(result);
            done?.Invoke();
        }

        IEnumerator ScenarioRoutine(FieldEvaluation result, Action<Sfx> sound, Action<int> stage)
        {
            stage?.Invoke(0);
            yield return Animate(ScenarioPassSeconds, t => { hazard = Mathf.SmoothStep(0, 1, t); RenderAll(); });
            stage?.Invoke(1);
            sound?.Invoke(Sfx.Fail);
            yield return Wait(ScenarioPeakSeconds);

            stage?.Invoke(2);
            // 센서가 상황을 감지하면 문제를 해결하는 시설만 신호를 낸다(해결하지 못하는 시설은 반응 대상이 없다).
            foreach (var spot in Mission.spots)
                if (result.Assignments.TryGetValue(spot.id, out var item) && spot.options.Any(o => o.itemId == item && o.solves.Count > 0))
                    Ping(spot.position + new Vector2(0, 0.4f));
            sound?.Invoke(Sfx.Install);
            foreach (var site in sites.Values)
            {
                var r = site.reaction;
                if (site.level < 1f || r == null || !r.moveResident) continue;
                site.moveFrom = site.resident.root.localPosition;
                site.moveTo = r.residentPosition;
                site.anchor = r.residentPosition;
                site.moveT = 0f;
                site.cheerTimer = 0f;
                site.reacted = true;
            }
            yield return Animate(ScenarioRespondSeconds, t => { respond = t; RenderAll(); });
            yield return Wait(ScenarioSettleSeconds);

            var done = pendingDone;
            pendingDone = null;
            scenario = null;
            ApplyState(result, 1f, 1f);
            done?.Invoke();
        }

        /// <summary>시설이 상황을 감지했다는 짧은 신호(퍼지는 고리).</summary>
        void Ping(Vector2 at)
        {
            var ring = FieldArt.Ellipse(transform, "SensorPing", Theme.WithAlpha(Theme.TealPale, 0.9f), at, new Vector2(0.6f, 0.36f), FieldArt.OrderMarker);
            Temp(ring.gameObject);
            StartCoroutine(PingRoutine(ring, at));
        }

        IEnumerator PingRoutine(SpriteRenderer ring, Vector2 at)
        {
            float t = 0;
            while (t < 0.9f && ring)
            {
                t += Delta();
                float e = Mathf.Clamp01(t / 0.9f);
                ring.transform.localScale = Vector3.one * Mathf.Lerp(1f, 4.5f, e);
                ring.color = Theme.WithAlpha(Theme.TealPale, 0.9f * (1 - e));
                yield return null;
            }
            if (ring) KillTemp(ring.gameObject);
        }

        void Sparkle(Vector2 at)
        {
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2;
                var s = FieldArt.Glow(transform, "Spark", Theme.WithAlpha(FieldArt.Lamp, 0.9f), at, new Vector2(0.4f, 0.4f), FieldArt.OrderMarker);
                Temp(s.gameObject);
                StartCoroutine(Fly(s, at, at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.1f, 0.4f));
            }
        }

        IEnumerator Fly(SpriteRenderer s, Vector2 from, Vector2 to, float duration)
        {
            float t = 0;
            while (t < duration && s)
            {
                t += Delta();
                float e = Mathf.Clamp01(t / duration);
                s.transform.localPosition = Vector2.Lerp(from, to, e);
                s.color = Theme.WithAlpha(FieldArt.Lamp, 0.9f * (1 - e));
                yield return null;
            }
            if (s) KillTemp(s.gameObject);
        }

        void OnDestroy() => ClearTemps();

        // ───────── 도착 연출 ─────────

        Coroutine arriving;
        Action arrivalDone;
        public bool IsArriving => arriving != null;

        /// <summary>현장 차량 진입 → 정차 → 담당자 하차 → 둘러보기. fast면 짧게. 건너뛰어도 같은 상태로 끝난다.</summary>
        public void PlayArrival(bool fast, Action<Sfx> sound, Action lookAround, Action done)
        {
            arrivalDone = done;
            player.root.gameObject.SetActive(false);
            arriving = StartCoroutine(ArrivalRoutine(fast, sound, lookAround));
        }

        public void SkipArrival()
        {
            if (arriving == null) return;
            StopCoroutine(arriving);
            FinishArrival();
        }

        void FinishArrival()
        {
            arriving = null;
            van.localPosition = new Vector2(-HalfWidth - 5, Mission.playerStart.y);
            player.root.gameObject.SetActive(true);
            player.root.localScale = Vector3.one;
            player.visual.localPosition = Vector2.zero;
            Player.position = WorldPoint(Mission.playerStart);
            player.root.localPosition = Mission.playerStart;
            Face(Vector2.right);
            var done = arrivalDone;
            arrivalDone = null;
            done?.Invoke();
        }

        IEnumerator ArrivalRoutine(bool fast, Action<Sfx> sound, Action lookAround)
        {
            float k = fast ? 0.6f : 1f;
            float laneY = Mission.playerStart.y + 0.3f;
            Vector2 stop = new Vector2(Mission.playerStart.x + 1.8f, laneY);
            FieldArt.SetDepth(van, laneY - 0.7f);
            yield return Move(van, new Vector2(-HalfWidth - 4, laneY), stop, 1.3f * k);
            sound?.Invoke(Sfx.Arrive);
            yield return Wait(0.2f * k);
            // 하차: 차 옆에서 폴짝 내린다.
            player.root.gameObject.SetActive(true);
            Player.position = WorldPoint(Mission.playerStart);
            player.root.localPosition = Mission.playerStart;
            yield return Animate(0.35f * k, t =>
            {
                player.root.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, EaseBack(t));
                player.visual.localPosition = new Vector2(0, Mathf.Sin(t * Mathf.PI) * 0.35f);
            });
            player.visual.localPosition = Vector2.zero;
            // 둘러보기: 왼쪽 → 오른쪽 → 위 → 오른쪽
            lookAround?.Invoke();
            foreach (var dir in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.right })
            {
                Face(dir);
                yield return Wait(0.4f * k);
            }
            yield return Move(van, stop, new Vector2(-HalfWidth - 5, laneY), 0.8f * k);
            FinishArrival();
        }

        // ───────── 담당자 자세 ─────────

        void Face(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.01f || player == null) return;
            var f = dir.normalized;
            if (Mathf.Abs(f.x) > 0.1f) player.visual.localScale = new Vector3(f.x < 0 ? -1 : 1, 1, 1);
            bool up = f.y > 0.6f;
            player.eye.enabled = !up;
            if (player.brim) player.brim.enabled = !up;
        }

        /// <summary>관찰 동작: 대상을 향해 몸을 숙이고 고개를 기울인다. 주민은 놀라 쳐다본다.</summary>
        public void PlayObserve(FieldProblemEntry problem)
        {
            if (player == null) return;
            Face(problem.observePoint - PlayerPosition);
            observeTimer = 0.6f;
            if (sites.TryGetValue(problem.id, out var site)) site.reactTimer = 0.6f;
        }

        /// <summary>수첩 자세(0: 내림, 1: 눈앞에 들어 올림). 수첩 연출에서 직접 호출한다(현장 시간이 멈춰도 동작).</summary>
        public void SetNotebookPose(float amount)
        {
            notebookPose = Mathf.Clamp01(amount);
            if (player != null) PoseArms(player, 0f);
        }

        public Vector2 PlayerHeadPoint => PlayerPosition + new Vector2(0, 1.6f);

        void PoseArms(Rig rig, float swing)
        {
            float raise = notebookPose;
            rig.armFront.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-swing, 110f, raise) + observePose * 35f);
            rig.armBack.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(swing, 60f, raise));
            if (rig.prop) rig.prop.localScale = Vector3.one * Mathf.Lerp(1f, 1.5f, raise);
            rig.head.localRotation = Quaternion.Euler(0, 0, -raise * 12f - observePose * 14f);
        }

        // ───────── 이동 ─────────

        /// <summary>플레이어 이동. 대각선 속도는 정규화한다. 걷기 그림은 속도에 맞춰 움직이고 판정에는 관여하지 않는다.</summary>
        public void MovePlayer(Vector2 input, float speed)
        {
            if (!Player) return;
            if (input.sqrMagnitude > 1f) input.Normalize();
            Player.linearVelocity = input * speed;
            if (input.sqrMagnitude > 0.01f) Face(input);
        }

        /// <summary>연출용: 담당자를 한 자리에 세우고 방향을 정한다.</summary>
        public void PlacePlayer(Vector2 at, Vector2 face)
        {
            if (!Player) return;
            Player.linearVelocity = Vector2.zero;
            Player.position = WorldPoint(at);
            player.root.localPosition = at;
            Face(face);
        }

        public void ResetPlayer()
        {
            if (!Player) return;
            Player.linearVelocity = Vector2.zero;
            Player.position = WorldPoint(Mission.playerStart);
        }

        // ───────── 그리기 ─────────

        float Response(Site s) => s.level * respond;
        float Intensity(Site s) => Mathf.Lerp(s.data.earlyLevel, 1f, hazard);

        /// <summary>
        /// 물에 잠긴 정도. 배수펌프는 상황이 온 뒤 고인 물을 빼고(대응), 모래주머니는 처음부터 물이 더 들어오지 않게 막는다
        /// (이미 있던 얕은 물을 말리지는 않는다).
        /// </summary>
        float Water(Site s) => s.level > 0 && s.solvedBy == FieldItemVisual.Sandbags
            ? s.data.earlyLevel
            : Intensity(s) * (1f - Response(s));

        /// <summary>센서 보행등이 해결한 어둠: 사람이 다가오면 밝게, 아니면 낮게 켜 둔다(보여 주기만, 판정과 무관).</summary>
        float Sensor(Site s)
        {
            if (s.solvedBy != FieldItemVisual.WalkLight || s.solvedSpot == null) return 1f;
            var at = Mission.Spot(s.solvedSpot).position;
            bool near = Vector2.Distance(s.resident.root.localPosition, at) < 2.6f || (Player && Vector2.Distance(PlayerPosition, at) < 2.6f);
            return near ? 1f : 0.55f;
        }

        void RenderAll()
        {
            foreach (var site in sites.Values) Render(site);
            RenderEnvironment();
        }

        void Render(Site s)
        {
            float shown = Response(s);
            float intensity = Intensity(s);
            switch (s.data.kind)
            {
                case FieldProblemKind.Dark:
                {
                    float lit = shown * Sensor(s);
                    s.overlay.color = Theme.WithAlpha(Theme.Hex(0x0B1026), intensity * Mathf.Lerp(0.66f, 0.05f, lit));
                    s.glow.color = Theme.WithAlpha(FieldArt.Lamp, 0.35f * lit);
                    break;
                }
                case FieldProblemKind.Heat:
                {
                    // 주민이 옮겨 가는 해결(그늘막 아래·현관 바람 앞)은 그 자리만 나아지고, 구역 전체가 시원해지지는 않는다.
                    bool local = s.reaction != null && s.reaction.moveResident;
                    var hot = Theme.WithAlpha(Theme.Heat, 0.32f * intensity);
                    s.tint.color = local ? Theme.WithAlpha(Theme.Heat, 0.32f * intensity * (1f - 0.4f * shown))
                        : Color.Lerp(hot, s.solvedBy == FieldItemVisual.Shade ? Theme.WithAlpha(Theme.Hex(0x7FA88A), 0.18f) : Theme.WithAlpha(Theme.Cool, 0.2f), shown);
                    foreach (var w in s.waves) w.color = Theme.WithAlpha(Theme.Heat, 0.8f * intensity * (1 - shown));
                    if (s.sweat) s.sweat.enabled = intensity * (1 - shown) > 0.5f;
                    if (local && s.solvedBy != FieldItemVisual.Shade)
                    {
                        if (!s.patch)
                            s.patch = FieldArt.Ellipse(transform, "CoolPatch_" + s.data.id, Color.clear, s.reaction.residentPosition + new Vector2(0.5f, -0.2f), new Vector2(3.2f, 1.6f), FieldArt.OrderFlat + 2);
                        s.patch.color = Theme.WithAlpha(Theme.Cool, 0.4f * shown);
                    }
                    else if (s.patch) s.patch.color = Color.clear;
                    break;
                }
                case FieldProblemKind.Thirst:
                    s.tint.color = Color.Lerp(Theme.WithAlpha(Theme.Danger, 0.12f * intensity), Theme.WithAlpha(Theme.Teal, 0.16f), shown);
                    // 빈 물통은 이용객이 늘어 급수장치를 쓰기 시작하면 치운다(물통이 저절로 차지 않는다).
                    if (s.jug) s.jug.gameObject.SetActive(shown < 0.99f);
                    break;
                case FieldProblemKind.Power:
                {
                    // 상황 전에는 평소 전기로 불이 켜져 있다. 정전이 오면 꺼지고, 비상전원이 준비된 곳만 자동 전환으로 다시 켜진다.
                    bool outage = hazard >= 0.5f;
                    bool lit = !outage || shown >= 0.5f;
                    s.glow.color = Theme.WithAlpha(FieldArt.Lamp, 0.3f * shown);
                    for (int i = 0; i < s.windows.Count; i++)
                        s.windows[i].color = lit && (!outage || shown * s.windows.Count > i + 0.5f) ? Theme.WindowLit : Theme.Window;
                    break;
                }
                default:
                {
                    float water = Water(s);
                    s.overlay.color = Theme.WithAlpha(Theme.Hex(0x4F99DB), 0.62f * water);
                    s.overlay.transform.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, water), Mathf.Lerp(0.45f, 1f, water), 1);
                    foreach (var w in s.waves) w.color = Theme.WithAlpha(Color.white, 0.6f * Mathf.Clamp01(water * 1.5f - 0.2f));
                    break;
                }
            }
            // 물웅덩이는 문제의 물높이를 그대로 따른다: 깊으면 길을 막고(충돌), 얕거나 빠지면 지나갈 수 있다.
            foreach (var p in puddles.Where(p => p.clearedBy == s.data.id))
            {
                float water = s.data.kind == FieldProblemKind.Flood ? Water(s) : 1f - shown;
                p.sprite.color = Theme.WithAlpha(Theme.Water, Theme.Water.a * water);
                p.shine.color = Theme.WithAlpha(Color.white, 0.5f * water);
                p.collider.enabled = water > 0.5f;
            }
            s.resident.mark.color = shown >= 0.99f ? Theme.Warning : Theme.NavySoft;
        }

        /// <summary>하늘·해·비·창문·설치한 조명. 상황 진행(hazard)과 시설 대응(respond)만 따른다.</summary>
        void RenderEnvironment()
        {
            switch (Mission.theme)
            {
                case FieldTheme.NightAlley:
                    sky.color = Theme.WithAlpha(Theme.Hex(0x101838), 0.6f * hazard);
                    foreach (var w in litWindows) w.color = Color.Lerp(Theme.Hex(0x8FB7D6), Theme.Hex(0xF6D98A), hazard);
                    if (playerHalo) playerHalo.color = Theme.WithAlpha(Color.white, 0.3f * hazard);
                    break;
                case FieldTheme.Clinic:
                    sky.color = Theme.WithAlpha(Theme.Hex(0xFFB15C), 0.14f * hazard);
                    if (sun)
                    {
                        sun.color = Theme.WithAlpha(Theme.Hex(0xFFD36B), Mathf.Lerp(0.4f, 0.8f, hazard));
                        sun.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.5f, hazard);
                    }
                    break;
                default:
                    sky.color = Theme.WithAlpha(Theme.Hex(0x223050), 0.32f * hazard);
                    if (cloud) cloud.color = Theme.WithAlpha(Theme.Hex(0x45506A), Mathf.Lerp(0.18f, 0.5f, hazard));
                    int shownDrops = Mathf.RoundToInt(rain.Count * Mathf.Lerp(0.3f, 1f, hazard));
                    for (int i = 0; i < rain.Count; i++) rain[i].gameObject.SetActive(i < shownDrops);
                    break;
            }
            // 설치한 조명은 상황(저녁)이 와야 켜진다. 보행등은 사람이 다가오면 더 밝아진다.
            foreach (var pair in installed)
            {
                var item = Mission.Item(Plan(pair.Key));
                if (item == null || (item.visual != FieldItemVisual.StreetLamp && item.visual != FieldItemVisual.WalkLight)) continue;
                float on = respond;
                if (item.visual == FieldItemVisual.WalkLight)
                {
                    var at = Mission.Spot(pair.Key).position;
                    bool near = sites.Values.Any(x => Vector2.Distance(x.resident.root.localPosition, at) < 2.6f) || (Player && Vector2.Distance(PlayerPosition, at) < 2.6f);
                    on *= near ? 1f : 0.45f;
                }
                foreach (var r in pair.Value.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (r.name == "Halo") r.color = Theme.WithAlpha(FieldArt.Lamp, 0.7f * on);
                    else if (r.name == "Bulb") r.color = Color.Lerp(Theme.Hex(0x9AA0B4), FieldArt.Lamp, on);
                }
            }
            for (int i = 0; i < crowd.Count; i++)
            {
                var v = crowd[i];
                bool visible = hazard > 0.05f;
                v.rig.root.gameObject.SetActive(visible);
                if (!visible) continue;
                var pos = VisitorPosition(v);
                v.moving = Vector2.Distance(pos, v.last) > 0.001f;
                if (v.moving) FaceRig(v.rig, pos.x - v.last.x);
                v.last = pos;
                v.rig.root.localPosition = pos;
                FieldArt.SetDepth(v.rig.root, pos.y - 0.45f);
            }
        }

        readonly Dictionary<string, string> placedItems = new Dictionary<string, string>();
        string Plan(string spotId) => placedItems.TryGetValue(spotId, out var item) ? item : null;

        void Update()
        {
            float dt = Delta();

            foreach (var s in sites.Values)
            {
                s.phase += dt;
                AnimateResident(s, dt);
                if (s.data.kind == FieldProblemKind.Heat)
                {
                    if (Response(s) < 0.99f)
                        for (int i = 0; i < s.waves.Count; i++)
                            s.waves[i].transform.localPosition += new Vector3(Mathf.Sin(s.phase * 3f + i) * 0.4f * dt, 0, 0);
                    // 냉방 바람: 구역 전체를 식히는 해결이면 구역 안에서, 현관 바람처럼 그 자리만 나아지는 해결이면 그 자리 둘레에서만 분다.
                    bool local = s.reaction != null && s.reaction.moveResident;
                    var area = local ? new Vector2(3.2f, 1.6f) : s.data.zoneSize;
                    var center = local ? s.reaction.residentPosition + new Vector2(0.5f, 0.2f) : s.data.zoneCenter;
                    for (int i = 0; i < s.winds.Count; i++)
                    {
                        float ph = Mathf.Repeat(s.phase * 0.7f + i * 0.33f, 1f);
                        var basePos = center + new Vector2(-area.x * 0.35f, area.y * (0.2f - i * 0.2f));
                        s.winds[i].transform.localPosition = basePos + new Vector2(ph * area.x * 0.6f, Mathf.Sin(ph * 6f) * 0.1f);
                        float wind = s.solvedBy == FieldItemVisual.Shade ? 0f : 1f;
                        s.winds[i].color = Theme.WithAlpha(Theme.Cool, 0.85f * Response(s) * wind * Mathf.Sin(ph * Mathf.PI));
                    }
                }
                // 비상등: 상황 전에는 꺼져 있고, 정전이 오면 깜빡이며, 비상전원으로 전환되면 초록으로 켜진다.
                if (s.beacon)
                    s.beacon.color = Response(s) >= 0.99f ? Theme.Success
                        : hazard < 0.5f ? Theme.WithAlpha(Theme.Warning, 0.15f)
                        : Theme.WithAlpha(Theme.Warning, Mathf.Repeat(s.phase * 2.5f, 1f) < 0.5f ? 1f : 0.2f);
                if (s.data.kind == FieldProblemKind.Flood && Water(s) > 0.2f)
                    for (int i = 0; i < s.waves.Count; i++)
                        s.waves[i].transform.localScale = new Vector3(1f + Mathf.Sin(s.phase * 2f + i) * 0.2f, 1, 1);
            }

            // 센서 보행등은 사람이 다가올 때마다 밝기가 바뀐다.
            if (respond > 0f && Mission.theme == FieldTheme.NightAlley) RenderAll();
            foreach (var v in crowd)
            {
                if (!v.rig.root.gameObject.activeSelf) continue;
                // 실제로 자리가 바뀐 프레임에만 걷는다(대기 지점에서는 서서 기다림).
                bool walking = v.moving && dt > 0;
                v.moving = false;
                if (walking) Walk(v.rig, Time.time * 9f);
                else Stand(v.rig);
            }

            if (player != null && player.root.gameObject.activeSelf)
            {
                AnimatePlayer(dt);
                FieldArt.SetDepth(player.root, PlayerPosition.y - 0.45f);
            }

            foreach (var d in rain)
            {
                var p = (Vector2)d.localPosition + new Vector2(-1.5f, -12f) * dt;
                if (p.y < -HalfHeight - 1) p = new Vector2(UnityEngine.Random.Range(-HalfWidth, HalfWidth + 3), HalfHeight + 2);
                d.localPosition = p;
            }
        }

        void AnimatePlayer(float dt)
        {
            var rig = player;
            float speed = Player ? Player.linearVelocity.magnitude : 0f;
            bool walking = speed > 0.1f && dt > 0;
            if (observeTimer > 0) observeTimer -= dt;
            observePose = Mathf.MoveTowards(observePose, observeTimer > 0 ? 1f : 0f, dt * 6f);

            if (walking)
            {
                rig.walkPhase += dt * 11f;
                float s = Mathf.Sin(rig.walkPhase);
                rig.legL.localRotation = Quaternion.Euler(0, 0, s * 28f);
                rig.legR.localRotation = Quaternion.Euler(0, 0, -s * 28f);
                rig.body.localPosition = new Vector2(0, Mathf.Abs(Mathf.Cos(rig.walkPhase)) * 0.05f);
                rig.head.localPosition = new Vector2(0, 0.52f + Mathf.Abs(Mathf.Cos(rig.walkPhase)) * 0.05f);
                PoseArms(rig, s * 30f);
            }
            else
            {
                rig.breath += dt;
                rig.legL.localRotation = Quaternion.identity;
                rig.legR.localRotation = Quaternion.identity;
                rig.body.localPosition = Vector2.zero;
                rig.body.localScale = new Vector3(1f, 1f + Mathf.Sin(rig.breath * 2.4f) * 0.025f, 1f);
                rig.head.localPosition = new Vector2(0, 0.52f + Mathf.Sin(rig.breath * 2.4f) * 0.012f);
                PoseArms(rig, 0f);
            }
            // 관찰: 몸을 대상 쪽으로 숙인다.
            rig.visual.localRotation = Quaternion.Euler(0, 0, -observePose * 10f * Mathf.Sign(rig.visual.localScale.x));
        }

        void AnimateResident(Site s, float dt)
        {
            var r = s.resident;
            var home = s.anchor;
            // 화면에서 '해결된 모습'은 시설이 대응한 뒤에만 나온다. 상황 전에는 초기 징후만 보인다.
            bool solved = Response(s) >= 0.99f;
            float intensity = Intensity(s);
            float t = s.phase;
            Vector2 pos = home;
            float bob = 0;

            if (s.Moving)
            {
                // 해결한 물품 쪽으로 짧게 걸어간다. 도착하면 한 번 기뻐한다.
                s.moveT = Mathf.Min(1f, s.moveT + dt / 0.8f);
                pos = Vector2.Lerp(s.moveFrom, s.moveTo, Ease(s.moveT));
                FaceRig(r, s.moveTo.x - s.moveFrom.x);
                Walk(r, t * 10f);
                r.visual.localPosition = Vector2.zero;
                r.root.localPosition = pos;
                FieldArt.SetDepth(r.root, pos.y - 0.45f);
                PlaceTick(s, pos);
                if (!s.Moving) s.cheerTimer = 0.8f;
                return;
            }

            if (solved && !s.reacted)
            {
                s.reacted = true;
                s.cheerTimer = 0.8f;
            }
            if (s.cheerTimer > 0)
            {
                // 해결된 순간: 두 팔을 들고 두 번 뛴다.
                s.cheerTimer -= dt;
                bob = Mathf.Abs(Mathf.Sin((0.8f - s.cheerTimer) * 8f)) * 0.35f;
                r.armFront.localRotation = Quaternion.Euler(0, 0, 150);
                r.armBack.localRotation = Quaternion.Euler(0, 0, -150);
                r.visual.localPosition = new Vector2(0, bob);
                return;
            }

            switch (s.data.kind)
            {
                case FieldProblemKind.Dark:
                case FieldProblemKind.Flood:
                    if (solved)
                    {
                        // 이제 길을 오간다.
                        pos = home + new Vector2(Mathf.Sin(t * 0.9f) * 1.3f, 0);
                        FaceRig(r, Mathf.Cos(t * 0.9f));
                        Walk(r, t * 9f);
                    }
                    else if (s.data.kind == FieldProblemKind.Dark && intensity < 0.5f)
                    {
                        // 아직 밝은 오후: 평소처럼 서 있다.
                        Stand(r);
                        r.armFront.localRotation = Quaternion.Euler(0, 0, 8);
                    }
                    else if (s.data.kind == FieldProblemKind.Dark)
                    {
                        // 어두워지자 멈춰 서서 두리번거리며 망설인다.
                        FaceRig(r, Mathf.Sin(t * 0.8f));
                        Stand(r);
                        r.armFront.localRotation = Quaternion.Euler(0, 0, 20);
                    }
                    else if (Water(s) < 0.5f)
                    {
                        // 얕은 물: 걱정스럽게 하늘을 본다.
                        Stand(r);
                        r.armFront.localRotation = Quaternion.Euler(0, 0, 30);
                    }
                    else
                    {
                        // 물 앞에서 발을 동동 구른다.
                        bob = Mathf.Abs(Mathf.Sin(t * 7f)) * 0.08f;
                        Stand(r);
                        r.armFront.localRotation = Quaternion.Euler(0, 0, 40 + Mathf.Sin(t * 7f) * 10f);
                    }
                    break;
                case FieldProblemKind.Heat:
                    Stand(r);
                    // 더위가 풀리면 부채를 내려놓는다.
                    if (r.prop) r.prop.gameObject.SetActive(!solved);
                    if (solved)
                    {
                        // 앉아서 천천히 쉰다.
                        bob = -0.12f;
                        r.legL.localRotation = r.legR.localRotation = Quaternion.Euler(0, 0, 70);
                        r.armFront.localRotation = Quaternion.Euler(0, 0, 60 + Mathf.Sin(t * 1.5f) * 10f);
                    }
                    else
                    {
                        // 더울수록 부채질이 빨라진다(상황 전에는 가볍게).
                        r.armFront.localRotation = Quaternion.Euler(0, 0, 100 + Mathf.Sin(t * Mathf.Lerp(6f, 16f, intensity)) * 30f);
                        if (s.sweat) s.sweat.transform.localPosition = new Vector2(0.24f, 0.12f - Mathf.Repeat(t, 1f) * 0.25f);
                    }
                    break;
                case FieldProblemKind.Thirst:
                    Stand(r);
                    if (solved && s.reaction != null && Mission.Spot(s.reaction.spotId) is FieldSpotEntry dispenser)
                        FaceRig(r, dispenser.position.x - pos.x);
                    r.armFront.localRotation = solved
                        ? Quaternion.Euler(0, 0, Mathf.Repeat(t, 3f) < 1f ? 140 : 40)
                        : Quaternion.Euler(0, 0, 120 + Mathf.Sin(t * 10f) * 15f);
                    break;
                default:
                    Stand(r);
                    if (solved)
                        r.armFront.localRotation = Quaternion.Euler(0, 0, 150 + Mathf.Sin(t * 6f) * 20f);
                    else if (hazard < 0.5f)
                        r.armFront.localRotation = Quaternion.Euler(0, 0, 8);
                    else
                    {
                        pos += new Vector2(Mathf.Sin(t * 40f) * 0.02f, 0);
                        r.armFront.localRotation = Quaternion.Euler(0, 0, 70);
                        r.armBack.localRotation = Quaternion.Euler(0, 0, -70);
                    }
                    break;
            }

            if (s.reactTimer > 0)
            {
                // 담당자가 살펴볼 때 그쪽을 보고 깜짝 놀란다.
                s.reactTimer -= dt;
                bob += Mathf.Sin((0.6f - s.reactTimer) / 0.6f * Mathf.PI) * 0.25f;
                FaceRig(r, PlayerPosition.x - pos.x);
                r.mark.color = Theme.Warning;
            }
            else
            {
                r.mark.color = solved ? Theme.Warning : Theme.NavySoft;
            }
            r.visual.localPosition = new Vector2(0, bob);
            r.root.localPosition = pos;
            FieldArt.SetDepth(r.root, pos.y - 0.45f);
            PlaceTick(s, pos);
        }

        /// <summary>관찰 표시는 주민이 옮겨 갔으면 주민 머리 옆을 따라간다.</summary>
        void PlaceTick(Site s, Vector2 residentPos)
        {
            bool moved = s.Moving || s.anchor != s.data.residentPosition;
            s.tick.transform.localPosition = moved ? residentPos + new Vector2(0.55f, 1.45f) : s.data.observePoint + new Vector2(0, 1.1f);
        }

        static void Stand(Rig r)
        {
            r.legL.localRotation = r.legR.localRotation = Quaternion.identity;
            r.armBack.localRotation = Quaternion.Euler(0, 0, 8);
        }

        static void Walk(Rig r, float phase)
        {
            float s = Mathf.Sin(phase);
            r.legL.localRotation = Quaternion.Euler(0, 0, s * 25f);
            r.legR.localRotation = Quaternion.Euler(0, 0, -s * 25f);
            r.armFront.localRotation = Quaternion.Euler(0, 0, -s * 25f);
            r.armBack.localRotation = Quaternion.Euler(0, 0, s * 25f);
        }

        static void FaceRig(Rig r, float dirX)
        {
            if (Mathf.Abs(dirX) > 0.05f) r.visual.localScale = new Vector3(dirX < 0 ? -1 : 1, 1, 1);
        }

        // ───────── 시간 도우미 ─────────

        static float Ease(float t) => 1f - (1f - t) * (1f - t);

        static float EaseBack(float t)
        {
            const float c = 1.6f;
            t -= 1;
            return 1 + (c + 1) * t * t * t + c * t * t;
        }

        IEnumerator Wait(float seconds)
        {
            float t = 0;
            while (t < seconds) { t += Delta(); yield return null; }
        }

        IEnumerator Animate(float duration, Action<float> step)
        {
            float t = 0;
            while (t < duration)
            {
                t += Delta();
                step(Mathf.Clamp01(t / duration));
                yield return null;
            }
            step(1f);
        }

        IEnumerator Move(Transform target, Vector2 from, Vector2 to, float duration, float arc = 0f)
        {
            yield return Animate(duration, t =>
            {
                float e = Ease(t);
                target.localPosition = Vector2.Lerp(from, to, e) + new Vector2(0, Mathf.Sin(t * Mathf.PI) * arc);
            });
        }
    }
}
