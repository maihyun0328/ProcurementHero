using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProcurementHero.Core.Field;
using UnityEngine;

namespace ProcurementHero.Field
{
    /// <summary>
    /// 미션 데이터로 탑다운 현장을 만든다. 충돌 영역은 그림과 같은 사각형으로 만든다.
    /// 문제 구역의 모습은 '해결 정도(0~1)'로만 바뀌며, 해결 여부는 FieldRules의 판정 결과를 그대로 받는다.
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

        class Site
        {
            public FieldProblemEntry data;
            public float level, target;
            public SpriteRenderer overlay, glow, tint;
            public readonly List<SpriteRenderer> waves = new List<SpriteRenderer>();
            public readonly List<SpriteRenderer> windows = new List<SpriteRenderer>();
            public SpriteRenderer beacon, cup;
            public Transform resident;
            public SpriteRenderer residentMark;
            public GameObject tick;
            public float phase;
        }

        readonly Dictionary<string, Site> sites = new Dictionary<string, Site>();
        readonly Dictionary<string, Transform> spotMarkers = new Dictionary<string, Transform>();
        readonly Dictionary<string, GameObject> installed = new Dictionary<string, GameObject>();
        readonly List<Transform> rain = new List<Transform>();
        Transform truck;
        Transform playerFace;
        Vector2 lastMove = Vector2.down;

        public static FieldWorld Build(Transform parent, FieldMissionData mission, bool withPlayer)
        {
            var go = new GameObject("FieldWorld_" + mission.id);
            go.transform.SetParent(parent, false);
            var world = go.AddComponent<FieldWorld>();
            world.Mission = mission;
            world.BuildGround();
            world.BuildObstacles();
            world.BuildSites();
            world.BuildSpots();
            if (withPlayer) world.BuildPlayer();
            world.BuildTruck();
            if (mission.theme == FieldTheme.Shelter) world.BuildRain();
            return world;
        }

        // ───────── 구성 ─────────

        void Palette(out Color ground, out Color path, out Color building, out Color roof)
        {
            switch (Mission.theme)
            {
                case FieldTheme.NightAlley:
                    ground = Theme.Hex(0x41584F); path = Theme.Hex(0x8C8778); building = Theme.Hex(0x8A6F82); roof = Theme.Hex(0x5E4A63);
                    break;
                case FieldTheme.Clinic:
                    ground = Theme.Grass; path = Theme.Road; building = Theme.ClinicWall; roof = Theme.TealDark;
                    break;
                default:
                    ground = Theme.Hex(0x86A08C); path = Theme.Hex(0xB3AE9D); building = Theme.ShelterWall; roof = Theme.RoofBlue;
                    break;
            }
        }

        void BuildGround()
        {
            Palette(out var ground, out var path, out _, out _);
            FieldArt.Box(transform, "Ground", ground, Vector2.zero, new Vector2(HalfWidth * 2 + 2, HalfHeight * 2 + 4), FieldArt.OrderGround);
            foreach (var p in Mission.paths)
                FieldArt.Round(transform, "Path", path, p.center, p.size, FieldArt.OrderPath);

            if (Mission.theme == FieldTheme.NightAlley)
            {
                // 계단 디딤판
                for (int i = 0; i < 6; i++)
                    FieldArt.Box(transform, "Step", Theme.Hex(0xA49E8C), new Vector2(-0.6f, -1.2f + i * 0.6f), new Vector2(3.2f, 0.12f), FieldArt.OrderPath + 1);
            }
            if (Mission.theme == FieldTheme.Clinic)
                FieldArt.Glow(transform, "Sun", Theme.WithAlpha(Theme.Hex(0xFFD36B), 0.55f), new Vector2(13.5f, 6.5f), new Vector2(5, 5), FieldArt.OrderZone);

            // 바깥 경계 충돌
            AddWall(new Vector2(0, HalfHeight + 0.5f), new Vector2(HalfWidth * 2 + 2, 1));
            AddWall(new Vector2(0, -HalfHeight - 0.5f), new Vector2(HalfWidth * 2 + 2, 1));
            AddWall(new Vector2(HalfWidth + 0.5f, 0), new Vector2(1, HalfHeight * 2 + 2));
            AddWall(new Vector2(-HalfWidth - 0.5f, 0), new Vector2(1, HalfHeight * 2 + 2));
        }

        void AddWall(Vector2 center, Vector2 size)
        {
            var go = new GameObject("Bound");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider2D>().size = size;
        }

        void BuildObstacles()
        {
            Palette(out _, out _, out var building, out var roof);
            foreach (var o in Mission.obstacles)
            {
                SpriteRenderer body;
                switch (o.kind)
                {
                    case FieldObstacleKind.Building:
                        body = FieldArt.Round(transform, "Building", building, o.center, o.size, FieldArt.OrderObstacle);
                        FieldArt.Round(transform, "Roof", roof, o.center + new Vector2(0, o.size.y * 0.32f), new Vector2(o.size.x, o.size.y * 0.36f), FieldArt.OrderObstacle + 1);
                        break;
                    case FieldObstacleKind.Wall:
                        body = FieldArt.Box(transform, "Wall", Mission.theme == FieldTheme.Clinic ? Theme.NavySoft : Theme.Hex(0x6E6A62), o.center, o.size, FieldArt.OrderObstacle);
                        break;
                    case FieldObstacleKind.Fence:
                        body = FieldArt.Box(transform, "Fence", Theme.Hex(0x8A6440), o.center, o.size, FieldArt.OrderObstacle);
                        break;
                    case FieldObstacleKind.Puddle:
                        body = FieldArt.Round(transform, "Puddle", Theme.Water, o.center, o.size, FieldArt.OrderObstacle);
                        break;
                    default:
                        FieldArt.Box(transform, "Trunk", Theme.Hex(0x7A5A3A), o.center + new Vector2(0, -o.size.y * 0.2f), new Vector2(o.size.x * 0.25f, o.size.y * 0.5f), FieldArt.OrderObstacle);
                        body = FieldArt.Dot(transform, "Tree", Theme.Hex(0x3F7F4E), o.center + new Vector2(0, o.size.y * 0.15f), Mathf.Max(o.size.x, o.size.y) * 1.1f, FieldArt.OrderObstacle + 1);
                        break;
                }
                var col = new GameObject("Collider_" + o.kind);
                col.transform.SetParent(transform, false);
                col.transform.localPosition = o.center;
                col.AddComponent<BoxCollider2D>().size = o.size;
            }
        }

        void BuildSites()
        {
            foreach (var p in Mission.problems)
            {
                var site = new Site { data = p, phase = UnityEngine.Random.value * 6f };
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
                        break;
                    case FieldProblemKind.Thirst:
                        site.tint = FieldArt.Round(transform, "Thirst_" + p.id, Theme.WithAlpha(Theme.Danger, 0.12f), p.zoneCenter, p.zoneSize, FieldArt.OrderZone);
                        FieldArt.Round(transform, "EmptyJug", Theme.Paper, p.observePoint + new Vector2(1.6f, 0.4f), new Vector2(0.6f, 0.9f), FieldArt.OrderItem);
                        site.cup = FieldArt.Round(transform, "JugWater", Theme.Cool, p.observePoint + new Vector2(1.6f, 0.15f), new Vector2(0.44f, 0.12f), FieldArt.OrderItem + 1);
                        break;
                    case FieldProblemKind.Power:
                        site.glow = FieldArt.Glow(transform, "PowerLight_" + p.id, Theme.WithAlpha(FieldArt.Lamp, 0f), p.zoneCenter, p.zoneSize * 1.1f, FieldArt.OrderGlow);
                        for (int i = 0; i < 3; i++)
                            site.windows.Add(FieldArt.Round(transform, "Window", Theme.Window,
                                p.zoneCenter + new Vector2(-3f + i * 3f, -0.2f), new Vector2(1.4f, 1f), FieldArt.OrderObstacle + 2));
                        site.beacon = FieldArt.Dot(transform, "Beacon", Theme.Warning, p.zoneCenter + new Vector2(0, 1.6f), 0.5f, FieldArt.OrderObstacle + 2);
                        break;
                    default:
                        site.overlay = FieldArt.Round(transform, "Flood_" + p.id, Theme.WithAlpha(Theme.Hex(0x4F99DB), 0.62f), p.zoneCenter, p.zoneSize, FieldArt.OrderZone);
                        for (int i = 0; i < 3; i++)
                            site.waves.Add(FieldArt.Round(transform, "Ripple", Theme.WithAlpha(Color.white, 0.6f),
                                p.zoneCenter + new Vector2(-p.zoneSize.x * 0.25f + i * p.zoneSize.x * 0.25f, -p.zoneSize.y * 0.2f + i * 0.6f), new Vector2(0.8f, 0.12f), FieldArt.OrderZone + 1));
                        break;
                }

                site.resident = BuildResident(p.residentPosition, Mission.problems.IndexOf(p), out site.residentMark);
                site.tick = new GameObject("Observed_" + p.id);
                site.tick.transform.SetParent(transform, false);
                site.tick.transform.localPosition = p.observePoint + new Vector2(0, 1.1f);
                FieldArt.Dot(site.tick.transform, "Badge", Theme.Success, Vector2.zero, 0.55f, FieldArt.OrderMarker);
                var shortStroke = FieldArt.Box(site.tick.transform, "S", Color.white, new Vector2(-0.08f, -0.04f), new Vector2(0.07f, 0.2f), FieldArt.OrderMarker + 1);
                shortStroke.transform.localRotation = Quaternion.Euler(0, 0, 45);
                var longStroke = FieldArt.Box(site.tick.transform, "L", Color.white, new Vector2(0.05f, 0.02f), new Vector2(0.07f, 0.34f), FieldArt.OrderMarker + 1);
                longStroke.transform.localRotation = Quaternion.Euler(0, 0, -38);
                site.tick.SetActive(false);
                sites[p.id] = site;
                Render(site);
            }
        }

        Transform BuildResident(Vector2 position, int index, out SpriteRenderer mark)
        {
            Color[] shirts = { Theme.Teal, Theme.Warning, Theme.Hex(0x8E7CC3), Theme.Hex(0xD9655B) };
            var root = new GameObject("Resident").transform;
            root.SetParent(transform, false);
            root.localPosition = position;
            FieldArt.Round(root, "Body", shirts[index % shirts.Length], new Vector2(0, -0.1f), new Vector2(0.62f, 0.7f), FieldArt.OrderResident);
            FieldArt.Dot(root, "Head", Theme.Skin, new Vector2(0, 0.45f), 0.5f, FieldArt.OrderResident + 1);
            mark = FieldArt.Dot(root, "Mood", Theme.NavySoft, new Vector2(0, 0.95f), 0.22f, FieldArt.OrderResident + 1);
            return root;
        }

        void BuildSpots()
        {
            foreach (var s in Mission.spots)
            {
                var marker = new GameObject("Spot_" + s.id).transform;
                marker.SetParent(transform, false);
                marker.localPosition = s.position;
                FieldArt.Dot(marker, "Ring", Theme.WithAlpha(Theme.Paper, 0.75f), Vector2.zero, 0.9f, FieldArt.OrderPath + 2);
                FieldArt.Dot(marker, "Hole", Theme.WithAlpha(Theme.Teal, 0.85f), Vector2.zero, 0.55f, FieldArt.OrderPath + 3);
                FieldArt.Dot(marker, "Center", Theme.Paper, Vector2.zero, 0.2f, FieldArt.OrderPath + 4);
                spotMarkers[s.id] = marker;
            }
        }

        void BuildPlayer()
        {
            var go = new GameObject("Player");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Mission.playerStart;
            FieldArt.Glow(go.transform, "Halo", Theme.WithAlpha(Color.white, Mission.theme == FieldTheme.NightAlley ? 0.35f : 0f), Vector2.zero, new Vector2(2.4f, 2.4f), FieldArt.OrderGlow);
            FieldArt.Round(go.transform, "Body", Theme.Navy, new Vector2(0, -0.05f), new Vector2(0.72f, 0.8f), FieldArt.OrderPlayer);
            FieldArt.Box(go.transform, "Vest", Theme.Teal, new Vector2(0, -0.08f), new Vector2(0.5f, 0.3f), FieldArt.OrderPlayer + 1);
            FieldArt.Dot(go.transform, "Head", Theme.Skin, new Vector2(0, 0.52f), 0.56f, FieldArt.OrderPlayer + 1);
            FieldArt.Round(go.transform, "Cap", Theme.TealDark, new Vector2(0, 0.74f), new Vector2(0.6f, 0.22f), FieldArt.OrderPlayer + 2);
            playerFace = FieldArt.Dot(go.transform, "Face", Theme.Navy, new Vector2(0, 0.42f), 0.12f, FieldArt.OrderPlayer + 2).transform;
            FieldArt.Round(go.transform, "Notebook", Theme.Hex(0xE9C46A), new Vector2(0.38f, -0.05f), new Vector2(0.3f, 0.36f), FieldArt.OrderPlayer + 2);

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

        void BuildTruck()
        {
            truck = new GameObject("Truck").transform;
            truck.SetParent(transform, false);
            truck.localPosition = new Vector2(-HalfWidth - 4, TruckRoadY);
            FieldArt.Round(truck, "Cargo", Theme.Teal, new Vector2(-0.4f, 0.2f), new Vector2(2.4f, 1.2f), FieldArt.OrderMarker);
            FieldArt.Round(truck, "Cab", Theme.TealDark, new Vector2(1.3f, 0f), new Vector2(1f, 0.9f), FieldArt.OrderMarker);
            FieldArt.Round(truck, "Glass", Theme.Sky, new Vector2(1.45f, 0.15f), new Vector2(0.5f, 0.35f), FieldArt.OrderMarker + 1);
            FieldArt.Dot(truck, "WheelA", Theme.Navy, new Vector2(-1f, -0.5f), 0.55f, FieldArt.OrderMarker + 1);
            FieldArt.Dot(truck, "WheelB", Theme.Navy, new Vector2(1.1f, -0.5f), 0.55f, FieldArt.OrderMarker + 1);
            FieldArt.Box(truck, "Stripe", Theme.Paper, new Vector2(-0.4f, 0.2f), new Vector2(2f, 0.12f), FieldArt.OrderMarker + 1);
        }

        void BuildRain()
        {
            FieldArt.Box(transform, "Cloud", Theme.WithAlpha(Theme.Hex(0x45506A), 0.25f), new Vector2(0, 7.2f), new Vector2(34, 3), FieldArt.OrderWeather - 1);
            for (int i = 0; i < 46; i++)
            {
                var d = FieldArt.Round(transform, "Rain", Theme.WithAlpha(Theme.Cool, 0.65f),
                    new Vector2(UnityEngine.Random.Range(-HalfWidth, HalfWidth), UnityEngine.Random.Range(-HalfHeight, HalfHeight + 2)), new Vector2(0.08f, 0.5f), FieldArt.OrderWeather);
                rain.Add(d.transform);
            }
        }

        // ───────── 상태 표시 ─────────

        public IEnumerable<FieldProblemEntry> Problems => Mission.problems;
        public Vector2 PlayerPosition => Player ? (Vector2)Player.transform.localPosition : Mission.playerStart;

        public void MarkObserved(string problemId, bool observed)
        {
            if (sites.TryGetValue(problemId, out var s)) s.tick.SetActive(observed);
        }

        /// <summary>가장 가까운, 관찰 범위 안의 문제 하나. 범위 밖이면 null.</summary>
        public FieldProblemEntry NearestInRange()
        {
            if (!Player) return null;
            var pos = PlayerPosition;
            FieldProblemEntry best = null;
            float bestDistance = ObserveRange;
            foreach (var p in Mission.problems)
            {
                float d = Vector2.Distance(pos, p.observePoint);
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }
            return best;
        }

        public Vector3 WorldPoint(Vector2 local) => transform.TransformPoint(local);

        /// <summary>판정 결과(해결된 문제와 배정 목록)를 연출 없이 바로 적용한다.</summary>
        public void ApplyInstant(FieldEvaluation result)
        {
            if (installing != null) StopCoroutine(installing);
            installing = null;
            ApplyState(result);
        }

        void ApplyState(FieldEvaluation result)
        {
            truck.localPosition = new Vector2(-HalfWidth - 4, TruckRoadY);
            ClearInstalled();
            if (result != null)
                foreach (var pair in result.Assignments) PlaceItem(pair.Key, pair.Value);
            foreach (var site in sites.Values)
            {
                site.level = site.target = result != null && result.IsSolved(site.data.id) ? 1f : 0f;
                Render(site);
            }
        }

        void ClearInstalled()
        {
            foreach (var go in installed.Values) if (go) Destroy(go);
            installed.Clear();
            foreach (var m in spotMarkers.Values) m.gameObject.SetActive(true);
        }

        void PlaceItem(string spotId, string itemId)
        {
            var spot = Mission.Spot(spotId);
            var item = Mission.Item(itemId);
            if (spot == null || item == null) return;
            var model = FieldArt.ItemModel(transform, item.visual);
            model.transform.localPosition = spot.position;
            installed[spotId] = model;
            spotMarkers[spotId].gameObject.SetActive(false);
        }

        Coroutine installing;
        FieldEvaluation pendingResult;
        Action pendingDone;

        public bool IsInstalling => installing != null;

        /// <summary>배송 차량 → 지점별 상자 → 설치 → 문제 구역 변화. 건너뛰면 같은 최종 상태를 한 번만 적용한다.</summary>
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
            pendingDone = null;
            ApplyInstant(result);
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

                var box = FieldArt.Round(transform, "Box", Theme.Hex(0xD9A066), stop + new Vector2(0, 0.8f), new Vector2(0.7f, 0.6f), FieldArt.OrderMarker + 2).transform;
                yield return Move(box, box.localPosition, spot.position, 0.45f, arc: 1.4f);
                Destroy(box.gameObject);
                PlaceItem(spot.id, result.Assignments[spot.id]);
                var model = installed[spot.id].transform;
                yield return Animate(0.25f, t => model.localScale = Vector3.one * (0.4f + 0.6f * t));
                sound?.Invoke(Sfx.Install);

                var item = Mission.Item(result.Assignments[spot.id]);
                var solves = Mission.Spot(spot.id).options.First(o => o.itemId == item.id).solves;
                foreach (var id in solves)
                    if (sites.TryGetValue(id, out var site)) site.target = 1f;
                yield return Animate(0.5f, t =>
                {
                    foreach (var site in sites.Values)
                        if (site.target > site.level) { site.level = Mathf.Max(site.level, t); Render(site); }
                });
            }
            yield return Move(truck, truck.localPosition, new Vector2(HalfWidth + 4, TruckRoadY), 0.8f);
            truck.localPosition = new Vector2(-HalfWidth - 4, TruckRoadY);
            // 연출 끝에도 판정 결과를 그대로 다시 적용해 건너뛰기와 같은 상태가 되게 한다.
            var done = pendingDone;
            pendingDone = null;
            installing = null;
            ApplyState(result);
            done?.Invoke();
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
                float e = 1f - (1f - t) * (1f - t);
                target.localPosition = Vector2.Lerp(from, to, e) + new Vector2(0, Mathf.Sin(t * Mathf.PI) * arc);
            });
        }

        void Render(Site s)
        {
            float v = s.level;
            switch (s.data.kind)
            {
                case FieldProblemKind.Dark:
                    s.overlay.color = Theme.WithAlpha(Theme.Hex(0x0B1026), Mathf.Lerp(0.66f, 0.05f, v));
                    s.glow.color = Theme.WithAlpha(FieldArt.Lamp, 0.35f * v);
                    break;
                case FieldProblemKind.Heat:
                    s.tint.color = Color.Lerp(Theme.WithAlpha(Theme.Heat, 0.32f), Theme.WithAlpha(Theme.Cool, 0.2f), v);
                    foreach (var w in s.waves) w.color = Theme.WithAlpha(Theme.Heat, 0.8f * (1 - v));
                    break;
                case FieldProblemKind.Thirst:
                    s.tint.color = Color.Lerp(Theme.WithAlpha(Theme.Danger, 0.12f), Theme.WithAlpha(Theme.Teal, 0.16f), v);
                    s.cup.size = new Vector2(0.44f, Mathf.Lerp(0.12f, 0.62f, v));
                    break;
                case FieldProblemKind.Power:
                    s.glow.color = Theme.WithAlpha(FieldArt.Lamp, 0.3f * v);
                    foreach (var w in s.windows) w.color = Color.Lerp(Theme.Window, Theme.WindowLit, v);
                    break;
                default:
                    s.overlay.color = Theme.WithAlpha(Theme.Hex(0x4F99DB), Mathf.Lerp(0.62f, 0.08f, v));
                    foreach (var w in s.waves) w.color = Theme.WithAlpha(Color.white, 0.6f * (1 - v));
                    break;
            }
            s.residentMark.color = v >= 0.99f ? Theme.Warning : Theme.NavySoft;
        }

        void Update()
        {
            float dt = Delta();
            float time = Time.time;
            foreach (var s in sites.Values)
            {
                s.phase += dt;
                var home = s.data.residentPosition;
                if (s.level >= 0.99f)
                    s.resident.localPosition = home + new Vector2(Mathf.Sin(s.phase * 1.4f) * 1.1f, Mathf.Abs(Mathf.Sin(s.phase * 7f)) * 0.08f);
                else
                    s.resident.localPosition = home + new Vector2(Mathf.Sin(s.phase * 9f) * 0.03f, 0);
                if (s.data.kind == FieldProblemKind.Heat && s.level < 0.99f)
                    for (int i = 0; i < s.waves.Count; i++)
                        s.waves[i].transform.localPosition += new Vector3(Mathf.Sin(s.phase * 3f + i) * 0.4f * dt, 0, 0);
                if (s.beacon)
                    s.beacon.color = s.level >= 0.99f ? Theme.Success : Theme.WithAlpha(Theme.Warning, Mathf.Repeat(s.phase * 2.5f, 1f) < 0.5f ? 1f : 0.2f);
            }
            foreach (var d in rain)
            {
                var p = (Vector2)d.localPosition + new Vector2(-1.5f, -12f) * dt;
                if (p.y < -HalfHeight - 1) p = new Vector2(UnityEngine.Random.Range(-HalfWidth, HalfWidth + 3), HalfHeight + 2);
                d.localPosition = p;
            }
        }

        /// <summary>플레이어 이동. 대각선 속도는 정규화한다.</summary>
        public void MovePlayer(Vector2 input, float speed)
        {
            if (!Player) return;
            if (input.sqrMagnitude > 1f) input.Normalize();
            Player.linearVelocity = input * speed;
            if (input.sqrMagnitude > 0.01f)
            {
                lastMove = input.normalized;
                playerFace.localPosition = new Vector2(lastMove.x * 0.14f, 0.42f + lastMove.y * 0.06f);
            }
        }

        public void ResetPlayer()
        {
            if (!Player) return;
            Player.linearVelocity = Vector2.zero;
            Player.position = WorldPoint(Mission.playerStart);
        }
    }
}
