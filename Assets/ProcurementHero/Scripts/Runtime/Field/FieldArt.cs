using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ProcurementHero.Field
{
    /// <summary>
    /// 현장과 수첩의 그림. 외부 이미지 없이 기본 도형 Sprite를 겹쳐 그린다.
    /// 물품·문제 그림은 키(FieldItemVisual, FieldProblemKind)별로 이 파일에만 모아 두어 나중에 그림만 바꿔 끼울 수 있게 한다.
    /// </summary>
    public static class FieldArt
    {
        // 바닥층(깊이 정렬 없음)
        public const int OrderGround = 0, OrderPath = 1, OrderPathDetail = 2, OrderZone = 3, OrderFlat = 6;
        // 깊이층: 발 위치(y)가 아래일수록 앞에 그린다. SortingGroup 하나에 이 값을 준다.
        public const int DepthBase = 100;
        // 위층
        public const int OrderDark = 400, OrderGlow = 410, OrderMarker = 420, OrderWeather = 430;
        // 하위 호환(그룹 안 상대 순서)
        public const int OrderObstacle = 0, OrderItem = 0, OrderResident = 0, OrderPlayer = 0;

        public static readonly Color Lamp = new Color(1f, 0.9f, 0.55f, 1f);
        public static readonly Color Ink = Theme.Hex(0x2B2F45);
        public static readonly Color ShadowColor = new Color(0.05f, 0.07f, 0.12f, 0.28f);

        /// <summary>발 위치 y로 깊이 순서를 정한다(지도 세로 -8~8 → 260~100).</summary>
        public static int DepthOrder(float footY) => DepthBase + Mathf.RoundToInt((8f - footY) * 10f);

        /// <summary>깊이 정렬되는 오브젝트 루트. 자식 렌더러는 그룹 안에서 상대 순서를 가진다.</summary>
        public static Transform DepthRoot(Transform parent, string name, Vector2 position, float footY)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var group = go.AddComponent<SortingGroup>();
            group.sortingOrder = DepthOrder(footY);
            return go.transform;
        }

        public static void SetDepth(Transform root, float footY)
        {
            if (root && root.TryGetComponent(out SortingGroup g)) g.sortingOrder = DepthOrder(footY);
        }

        /// <summary>월드 크기(size, 단위)로 스프라이트를 놓는다.</summary>
        public static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, Color color, Vector2 position, Vector2 size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (sprite.border != Vector4.zero)
            {
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = size;
            }
            else
            {
                var b = sprite.bounds.size;
                go.transform.localScale = new Vector3(size.x / b.x, size.y / b.y, 1f);
            }
            return sr;
        }

        public static SpriteRenderer Box(Transform p, string n, Color c, Vector2 pos, Vector2 size, int order) =>
            Sprite(p, n, ShapeSprites.Square, c, pos, size, order);

        public static SpriteRenderer Round(Transform p, string n, Color c, Vector2 pos, Vector2 size, int order) =>
            Sprite(p, n, ShapeSprites.Rounded, c, pos, size, order);

        public static SpriteRenderer Dot(Transform p, string n, Color c, Vector2 pos, float d, int order) =>
            Sprite(p, n, ShapeSprites.Circle, c, pos, new Vector2(d, d), order);

        public static SpriteRenderer Ellipse(Transform p, string n, Color c, Vector2 pos, Vector2 size, int order) =>
            Sprite(p, n, ShapeSprites.Circle, c, pos, size, order);

        public static SpriteRenderer Glow(Transform p, string n, Color c, Vector2 pos, Vector2 size, int order) =>
            Sprite(p, n, ShapeSprites.Glow, c, pos, size, order);

        /// <summary>바닥 그림자(타원).</summary>
        public static SpriteRenderer Shadow(Transform p, Vector2 pos, float width) =>
            Ellipse(p, "Shadow", ShadowColor, pos, new Vector2(width, width * 0.32f), -5);

        /// <summary>회전 축(어깨·엉덩이)을 가진 팔다리. 반환한 피벗을 돌리면 끝이 따라 돈다.</summary>
        public static Transform Limb(Transform parent, string name, Color color, Vector2 pivot, Vector2 size, int order)
        {
            var p = new GameObject(name).transform;
            p.SetParent(parent, false);
            p.localPosition = pivot;
            Round(p, "Shape", color, new Vector2(0, -size.y / 2 + size.x / 2), size, order);
            return p;
        }

        // ───────── 현장 물품 모습 ─────────

        /// <summary>설치된 물품의 월드 모습(깊이 정렬 그룹 안에 넣는다). 반환한 루트를 지점 위치에 둔다.</summary>
        public static GameObject ItemModel(Transform parent, FieldItemVisual visual)
        {
            var root = new GameObject("Item_" + visual).transform;
            root.SetParent(parent, false);
            // 그늘막의 바닥 그늘은 FieldWorld가 땅 높이에 따로 그린다(사람을 덮지 않게).
            if (visual != FieldItemVisual.Shade) Shadow(root, new Vector2(0, 0.02f), visual == FieldItemVisual.BigPump ? 1.5f : 1.1f);
            switch (visual)
            {
                case FieldItemVisual.StreetLamp:
                    Round(root, "Base", Theme.Hex(0x2C3350), new Vector2(0, 0.1f), new Vector2(0.5f, 0.22f), 1);
                    Box(root, "Pole", Theme.Hex(0x3D4560), new Vector2(0, 1.05f), new Vector2(0.16f, 1.9f), 2);
                    Box(root, "Arm", Theme.Hex(0x3D4560), new Vector2(0.22f, 1.95f), new Vector2(0.5f, 0.1f), 2);
                    Round(root, "Head", Theme.Hex(0x2C3350), new Vector2(0.42f, 1.9f), new Vector2(0.62f, 0.26f), 3);
                    Glow(root, "Halo", Theme.WithAlpha(Lamp, 0.7f), new Vector2(0.42f, 1.75f), new Vector2(1.2f, 1.2f), 4);
                    Dot(root, "Bulb", Lamp, new Vector2(0.42f, 1.78f), 0.3f, 5);
                    break;
                case FieldItemVisual.WalkLight:
                    Round(root, "Base", Theme.Hex(0x2C3350), new Vector2(0, 0.08f), new Vector2(0.4f, 0.16f), 1);
                    Box(root, "Post", Theme.Hex(0x3D4560), new Vector2(0, 0.4f), new Vector2(0.2f, 0.7f), 2);
                    Round(root, "Cap", Theme.Hex(0x2C3350), new Vector2(0, 0.84f), new Vector2(0.4f, 0.14f), 3);
                    Glow(root, "Halo", Theme.WithAlpha(Lamp, 0.7f), new Vector2(0, 0.7f), new Vector2(0.9f, 0.9f), 4);
                    Dot(root, "Bulb", Lamp, new Vector2(0, 0.72f), 0.28f, 5);
                    break;
                case FieldItemVisual.AirConditioner:
                    Round(root, "Body", Theme.Paper, new Vector2(0, 0.62f), new Vector2(1.4f, 1.1f), 1);
                    Round(root, "Panel", Theme.Hex(0xE3E8EE), new Vector2(0, 0.8f), new Vector2(1.2f, 0.4f), 2);
                    for (int i = 0; i < 3; i++)
                        Box(root, "Vent", Theme.NavySoft, new Vector2(0, 0.38f + i * 0.1f), new Vector2(1.1f, 0.04f), 3);
                    Dot(root, "Led", Theme.Teal, new Vector2(0.52f, 0.98f), 0.1f, 3);
                    break;
                case FieldItemVisual.WaterDispenser:
                    Round(root, "Body", Theme.Paper, new Vector2(0, 0.5f), new Vector2(0.7f, 1f), 1);
                    Round(root, "Bottle", Theme.WithAlpha(Theme.Cool, 0.95f), new Vector2(0, 1.2f), new Vector2(0.56f, 0.6f), 2);
                    Box(root, "Tap", Theme.Teal, new Vector2(0, 0.6f), new Vector2(0.22f, 0.12f), 3);
                    Round(root, "Cup", Theme.Hex(0xF4F1EA), new Vector2(0, 0.32f), new Vector2(0.18f, 0.2f), 3);
                    break;
                case FieldItemVisual.Shade:
                    Box(root, "PoleL", Theme.Hex(0x7A5A3A), new Vector2(-1f, 0.7f), new Vector2(0.12f, 1.4f), 1);
                    Box(root, "PoleR", Theme.Hex(0x7A5A3A), new Vector2(1f, 0.7f), new Vector2(0.12f, 1.4f), 1);
                    Round(root, "Canvas", Theme.Teal, new Vector2(0, 1.42f), new Vector2(2.6f, 0.5f), 2);
                    for (int i = 0; i < 4; i++)
                        Box(root, "Stripe", Theme.Paper, new Vector2(-0.9f + i * 0.6f, 1.42f), new Vector2(0.22f, 0.44f), 3);
                    break;
                case FieldItemVisual.Generator:
                    Round(root, "Body", Theme.Warning, new Vector2(0, 0.5f), new Vector2(1.3f, 1f), 1);
                    Round(root, "Top", Theme.Hex(0xC9721F), new Vector2(0, 0.95f), new Vector2(1.3f, 0.22f), 2);
                    Box(root, "Panel", Theme.Navy, new Vector2(-0.25f, 0.5f), new Vector2(0.45f, 0.32f), 2);
                    Dot(root, "Led", Theme.Success, new Vector2(0.38f, 0.66f), 0.14f, 3);
                    Box(root, "Cable", Theme.Navy, new Vector2(0.75f, 0.2f), new Vector2(0.4f, 0.06f), 0);
                    break;
                case FieldItemVisual.PortableGenerator:
                    Round(root, "Body", Theme.Hex(0xE8B04B), new Vector2(0, 0.4f), new Vector2(0.9f, 0.7f), 1);
                    Box(root, "Handle", Theme.Navy, new Vector2(0, 0.85f), new Vector2(0.6f, 0.08f), 2);
                    Box(root, "HandleL", Theme.Navy, new Vector2(-0.28f, 0.76f), new Vector2(0.06f, 0.2f), 2);
                    Box(root, "HandleR", Theme.Navy, new Vector2(0.28f, 0.76f), new Vector2(0.06f, 0.2f), 2);
                    Dot(root, "WheelL", Theme.Navy, new Vector2(-0.3f, 0.08f), 0.2f, 2);
                    Dot(root, "WheelR", Theme.Navy, new Vector2(0.3f, 0.08f), 0.2f, 2);
                    Dot(root, "Led", Theme.Success, new Vector2(0.2f, 0.48f), 0.12f, 3);
                    break;
                case FieldItemVisual.Pump:
                    Round(root, "Body", Theme.Teal, new Vector2(0, 0.45f), new Vector2(1.1f, 0.9f), 1);
                    Dot(root, "Gauge", Theme.Paper, new Vector2(-0.18f, 0.55f), 0.32f, 2);
                    Box(root, "Needle", Theme.Danger, new Vector2(-0.12f, 0.58f), new Vector2(0.12f, 0.03f), 3);
                    Round(root, "Hose", Theme.TealDark, new Vector2(0.85f, 0.2f), new Vector2(0.8f, 0.18f), 0);
                    break;
                case FieldItemVisual.FloorCooler:
                    Round(root, "Body", Theme.Paper, new Vector2(0, 0.85f), new Vector2(0.9f, 1.7f), 1);
                    Round(root, "Panel", Theme.Hex(0xE3E8EE), new Vector2(0, 1.35f), new Vector2(0.7f, 0.42f), 2);
                    for (int i = 0; i < 4; i++)
                        Box(root, "Vent", Theme.NavySoft, new Vector2(0, 0.45f + i * 0.14f), new Vector2(0.66f, 0.05f), 3);
                    Dot(root, "Led", Theme.Teal, new Vector2(0.24f, 1.45f), 0.1f, 3);
                    for (int i = 0; i < 3; i++)
                        Round(root, "Wind", Theme.WithAlpha(Theme.Cool, 0.8f), new Vector2(0.75f + i * 0.08f, 0.55f + i * 0.28f), new Vector2(0.42f, 0.08f), 2);
                    break;
                case FieldItemVisual.BigPump:
                    Round(root, "Body", Theme.TealDark, new Vector2(0, 0.55f), new Vector2(1.5f, 1.1f), 1);
                    Round(root, "Top", Theme.Teal, new Vector2(0, 1.0f), new Vector2(1.3f, 0.26f), 2);
                    Dot(root, "Gauge", Theme.Paper, new Vector2(-0.3f, 0.6f), 0.36f, 2);
                    Box(root, "Needle", Theme.Danger, new Vector2(-0.24f, 0.63f), new Vector2(0.14f, 0.03f), 3);
                    Round(root, "HoseA", Theme.Hex(0x1F5D5A), new Vector2(1.1f, 0.25f), new Vector2(1.0f, 0.22f), 0);
                    Round(root, "HoseB", Theme.Hex(0x1F5D5A), new Vector2(-1.1f, 0.2f), new Vector2(0.9f, 0.22f), 0);
                    break;
                default:
                    for (int i = 0; i < 3; i++)
                        Round(root, "Bag", Theme.Hex(0xC9A86A), new Vector2(-0.48f + i * 0.48f, 0.22f), new Vector2(0.56f, 0.36f), 1);
                    for (int i = 0; i < 2; i++)
                        Round(root, "BagTop", Theme.Hex(0xB89558), new Vector2(-0.24f + i * 0.48f, 0.5f), new Vector2(0.56f, 0.36f), 2);
                    break;
            }
            return root.gameObject;
        }

        // ───────── 수첩 아이콘 (UI) ─────────

        static Image Ui(Transform p, string n, Sprite s, Color c, Vector2 pos, Vector2 size)
        {
            var img = UIKit.Shape(p, n, s, c, new Vector2(0.5f, 0.5f), pos, size);
            img.raycastTarget = false;
            return img;
        }

        /// <summary>물품 그림. size 기준 정사각형 안에 그린다.</summary>
        public static RectTransform ItemIcon(Transform parent, FieldItemVisual visual, float size)
        {
            var root = UIKit.Rect(parent, "Icon_" + visual);
            root.sizeDelta = new Vector2(size, size);
            float k = size / 100f;
            switch (visual)
            {
                case FieldItemVisual.StreetLamp:
                    Ui(root, "Glow", ShapeSprites.Glow, Theme.WithAlpha(Lamp, 0.8f), new Vector2(12 * k, 26 * k), new Vector2(70 * k, 70 * k));
                    Ui(root, "Pole", ShapeSprites.Square, Theme.Hex(0x3D4560), new Vector2(-6 * k, -8 * k), new Vector2(8 * k, 76 * k));
                    Ui(root, "Head", ShapeSprites.Rounded, Theme.Hex(0x2C3350), new Vector2(10 * k, 32 * k), new Vector2(40 * k, 14 * k));
                    Ui(root, "Bulb", ShapeSprites.Circle, Lamp, new Vector2(16 * k, 24 * k), new Vector2(16 * k, 16 * k));
                    break;
                case FieldItemVisual.WalkLight:
                    Ui(root, "Glow", ShapeSprites.Glow, Theme.WithAlpha(Lamp, 0.7f), new Vector2(0, 0), new Vector2(48 * k, 48 * k));
                    Ui(root, "Post", ShapeSprites.Square, Theme.Hex(0x3D4560), new Vector2(0, -22 * k), new Vector2(10 * k, 44 * k));
                    Ui(root, "Bulb", ShapeSprites.Circle, Lamp, new Vector2(0, 2 * k), new Vector2(18 * k, 18 * k));
                    break;
                case FieldItemVisual.AirConditioner:
                    Ui(root, "Body", ShapeSprites.Rounded, Theme.Paper, new Vector2(0, 10 * k), new Vector2(80 * k, 44 * k));
                    Ui(root, "Grille", ShapeSprites.Square, Theme.NavySoft, new Vector2(0, 2 * k), new Vector2(60 * k, 5 * k));
                    for (int i = 0; i < 3; i++)
                        Ui(root, "Wind", ShapeSprites.Rounded, Theme.Cool, new Vector2(-24 * k + i * 24 * k, -24 * k), new Vector2(16 * k, 6 * k));
                    break;
                case FieldItemVisual.WaterDispenser:
                    Ui(root, "Body", ShapeSprites.Rounded, Theme.Paper, new Vector2(0, -14 * k), new Vector2(42 * k, 54 * k));
                    Ui(root, "Bottle", ShapeSprites.Circle, Theme.Cool, new Vector2(0, 26 * k), new Vector2(36 * k, 36 * k));
                    Ui(root, "Drop", ShapeSprites.Circle, Theme.Teal, new Vector2(0, -14 * k), new Vector2(12 * k, 12 * k));
                    break;
                case FieldItemVisual.Shade:
                    Ui(root, "Canvas", ShapeSprites.Rounded, Theme.Teal, new Vector2(0, 18 * k), new Vector2(84 * k, 22 * k));
                    Ui(root, "PoleL", ShapeSprites.Square, Theme.Hex(0x7A5A3A), new Vector2(-34 * k, -16 * k), new Vector2(6 * k, 50 * k));
                    Ui(root, "PoleR", ShapeSprites.Square, Theme.Hex(0x7A5A3A), new Vector2(34 * k, -16 * k), new Vector2(6 * k, 50 * k));
                    Ui(root, "Sun", ShapeSprites.Circle, Theme.Hex(0xF2C14E), new Vector2(30 * k, 40 * k), new Vector2(18 * k, 18 * k));
                    break;
                case FieldItemVisual.Generator:
                    Ui(root, "Body", ShapeSprites.Rounded, Theme.Warning, new Vector2(0, 0), new Vector2(76 * k, 58 * k));
                    Ui(root, "BoltA", ShapeSprites.Triangle, Theme.Paper, new Vector2(-4 * k, 6 * k), new Vector2(20 * k, 24 * k));
                    Ui(root, "BoltB", ShapeSprites.Triangle, Theme.Paper, new Vector2(4 * k, -8 * k), new Vector2(20 * k, 24 * k)).rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
                    Ui(root, "Plug", ShapeSprites.Square, Theme.Navy, new Vector2(40 * k, -20 * k), new Vector2(14 * k, 6 * k));
                    break;
                case FieldItemVisual.PortableGenerator:
                    Ui(root, "Body", ShapeSprites.Rounded, Theme.Hex(0xE8B04B), new Vector2(0, -4 * k), new Vector2(56 * k, 44 * k));
                    Ui(root, "Handle", ShapeSprites.Square, Theme.Navy, new Vector2(0, 26 * k), new Vector2(40 * k, 6 * k));
                    Ui(root, "WheelL", ShapeSprites.Circle, Theme.Navy, new Vector2(-18 * k, -28 * k), new Vector2(14 * k, 14 * k));
                    Ui(root, "WheelR", ShapeSprites.Circle, Theme.Navy, new Vector2(18 * k, -28 * k), new Vector2(14 * k, 14 * k));
                    Ui(root, "Bolt", ShapeSprites.Triangle, Theme.Paper, new Vector2(0, -2 * k), new Vector2(16 * k, 20 * k));
                    break;
                case FieldItemVisual.Pump:
                    Ui(root, "Body", ShapeSprites.Rounded, Theme.Teal, new Vector2(-8 * k, 0), new Vector2(56 * k, 50 * k));
                    Ui(root, "Pipe", ShapeSprites.Square, Theme.TealDark, new Vector2(28 * k, -8 * k), new Vector2(30 * k, 10 * k));
                    Ui(root, "Drop", ShapeSprites.Circle, Theme.Cool, new Vector2(40 * k, -26 * k), new Vector2(12 * k, 12 * k));
                    break;
                case FieldItemVisual.FloorCooler:
                    Ui(root, "Body", ShapeSprites.Rounded, Theme.Paper, new Vector2(-10 * k, 0), new Vector2(44 * k, 84 * k));
                    Ui(root, "Panel", ShapeSprites.Rounded, Theme.Hex(0xE3E8EE), new Vector2(-10 * k, 24 * k), new Vector2(34 * k, 20 * k));
                    for (int i = 0; i < 3; i++)
                        Ui(root, "Wind", ShapeSprites.Rounded, Theme.Cool, new Vector2(30 * k, -20 * k + i * 18 * k), new Vector2(24 * k, 6 * k));
                    break;
                case FieldItemVisual.BigPump:
                    Ui(root, "Body", ShapeSprites.Rounded, Theme.TealDark, new Vector2(0, 0), new Vector2(72 * k, 56 * k));
                    Ui(root, "Gauge", ShapeSprites.Circle, Theme.Paper, new Vector2(-14 * k, 4 * k), new Vector2(20 * k, 20 * k));
                    Ui(root, "PipeA", ShapeSprites.Square, Theme.Teal, new Vector2(40 * k, -14 * k), new Vector2(22 * k, 10 * k));
                    Ui(root, "PipeB", ShapeSprites.Square, Theme.Teal, new Vector2(-40 * k, -14 * k), new Vector2(22 * k, 10 * k));
                    Ui(root, "Drop", ShapeSprites.Circle, Theme.Cool, new Vector2(46 * k, -32 * k), new Vector2(14 * k, 14 * k));
                    break;
                default:
                    for (int i = 0; i < 3; i++)
                        Ui(root, "Bag", ShapeSprites.Rounded, Theme.Hex(0xC9A86A), new Vector2(-24 * k + i * 24 * k, -10 * k), new Vector2(26 * k, 20 * k));
                    Ui(root, "BagTop", ShapeSprites.Rounded, Theme.Hex(0xB89558), new Vector2(0, 10 * k), new Vector2(26 * k, 20 * k));
                    break;
            }
            return root;
        }

        /// <summary>문제 그림(어둠·더위·물 부족·전원·침수).</summary>
        public static RectTransform ProblemIcon(Transform parent, FieldProblemKind kind, float size)
        {
            var root = UIKit.Rect(parent, "Problem_" + kind);
            root.sizeDelta = new Vector2(size, size);
            float k = size / 100f;
            switch (kind)
            {
                case FieldProblemKind.Dark:
                    Ui(root, "Night", ShapeSprites.Circle, Theme.Navy, Vector2.zero, new Vector2(84 * k, 84 * k));
                    Ui(root, "Moon", ShapeSprites.Circle, Theme.Hex(0xF5E6A8), new Vector2(-8 * k, 8 * k), new Vector2(40 * k, 40 * k));
                    Ui(root, "MoonCut", ShapeSprites.Circle, Theme.Navy, new Vector2(4 * k, 16 * k), new Vector2(36 * k, 36 * k));
                    break;
                case FieldProblemKind.Heat:
                    Ui(root, "Sun", ShapeSprites.Glow, Theme.Heat, Vector2.zero, new Vector2(96 * k, 96 * k));
                    Ui(root, "Core", ShapeSprites.Circle, Theme.Hex(0xFFC24B), Vector2.zero, new Vector2(46 * k, 46 * k));
                    break;
                case FieldProblemKind.Thirst:
                    Ui(root, "Cup", ShapeSprites.Rounded, Theme.Paper, new Vector2(0, -6 * k), new Vector2(46 * k, 60 * k));
                    Ui(root, "Empty", ShapeSprites.Square, Theme.WithAlpha(Theme.Cool, 0.5f), new Vector2(0, -28 * k), new Vector2(34 * k, 8 * k));
                    break;
                case FieldProblemKind.Power:
                    Ui(root, "Plug", ShapeSprites.Rounded, Theme.NavySoft, Vector2.zero, new Vector2(60 * k, 70 * k));
                    Ui(root, "BoltA", ShapeSprites.Triangle, Theme.Warning, new Vector2(-4 * k, 8 * k), new Vector2(22 * k, 28 * k));
                    Ui(root, "BoltB", ShapeSprites.Triangle, Theme.Warning, new Vector2(4 * k, -8 * k), new Vector2(22 * k, 28 * k)).rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
                    break;
                default:
                    Ui(root, "Water", ShapeSprites.Rounded, Theme.Water, new Vector2(0, -16 * k), new Vector2(90 * k, 40 * k));
                    for (int i = 0; i < 3; i++)
                        Ui(root, "Wave", ShapeSprites.Rounded, Theme.Paper, new Vector2(-26 * k + i * 26 * k, -12 * k), new Vector2(16 * k, 5 * k));
                    Ui(root, "Drop", ShapeSprites.Circle, Theme.Cool, new Vector2(0, 26 * k), new Vector2(22 * k, 22 * k));
                    break;
            }
            return root;
        }

        /// <summary>돋보기 아이콘.</summary>
        public static RectTransform Magnifier(Transform parent, float size)
        {
            var root = UIKit.Rect(parent, "Magnifier");
            root.sizeDelta = new Vector2(size, size);
            float k = size / 100f;
            var handle = Ui(root, "Handle", ShapeSprites.Rounded, Theme.Navy, new Vector2(22 * k, -22 * k), new Vector2(16 * k, 46 * k));
            handle.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            Ui(root, "Ring", ShapeSprites.Circle, Theme.Navy, new Vector2(-8 * k, 8 * k), new Vector2(66 * k, 66 * k));
            Ui(root, "Lens", ShapeSprites.Circle, Theme.TealPale, new Vector2(-8 * k, 8 * k), new Vector2(48 * k, 48 * k));
            return root;
        }

        /// <summary>체크 표시(✓ 모양을 두 막대로 그림).</summary>
        public static RectTransform Tick(Transform parent, float size, Color color)
        {
            var root = UIKit.Rect(parent, "Tick");
            root.sizeDelta = new Vector2(size, size);
            float k = size / 100f;
            Ui(root, "Short", ShapeSprites.Rounded, color, new Vector2(-18 * k, -8 * k), new Vector2(16 * k, 44 * k)).rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            Ui(root, "Long", ShapeSprites.Rounded, color, new Vector2(10 * k, 2 * k), new Vector2(16 * k, 78 * k)).rectTransform.localRotation = Quaternion.Euler(0, 0, -38);
            return root;
        }

        /// <summary>'아직 조사하지 않음' 무늬(사선)와 물음표. 색이 아닌 무늬와 기호로 상태를 보인다.</summary>
        public static void UnknownPattern(RectTransform target, Color stripe)
        {
            var hatch = UIKit.Rect(target, "Hatch").Fill();
            var img = hatch.gameObject.AddComponent<Image>();
            img.sprite = ShapeSprites.Stripes;
            img.type = Image.Type.Tiled;
            img.color = stripe;
            img.raycastTarget = false;
        }
    }
}
