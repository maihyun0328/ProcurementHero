using UnityEngine;
using UnityEngine.UI;

namespace ProcurementHero.Field
{
    /// <summary>
    /// 현장과 수첩의 임시 그림. 기본 도형 Sprite를 조합한다.
    /// 물품·문제 그림은 키(FieldItemVisual, FieldProblemKind)별로 한 곳에 모아 두어 나중에 그림만 바꿔 끼울 수 있게 한다.
    /// </summary>
    public static class FieldArt
    {
        // 정렬 순서
        public const int OrderGround = 0, OrderPath = 1, OrderZone = 2, OrderObstacle = 5, OrderItem = 6,
            OrderResident = 7, OrderDark = 10, OrderGlow = 11, OrderPlayer = 12, OrderMarker = 13, OrderWeather = 14;

        public static readonly Color Lamp = new Color(1f, 0.9f, 0.55f, 1f);

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

        public static SpriteRenderer Glow(Transform p, string n, Color c, Vector2 pos, Vector2 size, int order) =>
            Sprite(p, n, ShapeSprites.Glow, c, pos, size, order);

        // ───────── 현장 물품 모습 ─────────

        /// <summary>설치된 물품의 월드 모습. 반환한 루트를 지점 위치에 둔다.</summary>
        public static GameObject ItemModel(Transform parent, FieldItemVisual visual)
        {
            var root = new GameObject("Item_" + visual).transform;
            root.SetParent(parent, false);
            int o = OrderItem;
            switch (visual)
            {
                case FieldItemVisual.StreetLamp:
                    Box(root, "Pole", Theme.Hex(0x3D4560), new Vector2(0, 0.9f), new Vector2(0.18f, 1.8f), o);
                    Round(root, "Head", Theme.Hex(0x2C3350), new Vector2(0, 1.85f), new Vector2(0.8f, 0.3f), o);
                    Dot(root, "Bulb", Lamp, new Vector2(0, 1.7f), 0.36f, OrderGlow + 1);
                    break;
                case FieldItemVisual.WalkLight:
                    Box(root, "Post", Theme.Hex(0x3D4560), new Vector2(0, 0.35f), new Vector2(0.22f, 0.7f), o);
                    Dot(root, "Bulb", Lamp, new Vector2(0, 0.75f), 0.34f, OrderGlow + 1);
                    break;
                case FieldItemVisual.AirConditioner:
                    Round(root, "Body", Theme.Paper, new Vector2(0, 0.5f), new Vector2(1.3f, 0.7f), o);
                    Box(root, "Grille", Theme.NavySoft, new Vector2(0, 0.35f), new Vector2(1f, 0.08f), o + 1);
                    Box(root, "Grille2", Theme.NavySoft, new Vector2(0, 0.5f), new Vector2(1f, 0.08f), o + 1);
                    Dot(root, "Led", Theme.Teal, new Vector2(0.5f, 0.7f), 0.12f, o + 1);
                    break;
                case FieldItemVisual.WaterDispenser:
                    Round(root, "Body", Theme.Paper, new Vector2(0, 0.45f), new Vector2(0.7f, 0.9f), o);
                    Dot(root, "Bottle", Theme.Cool, new Vector2(0, 1.05f), 0.55f, o + 1);
                    Box(root, "Tap", Theme.Teal, new Vector2(0, 0.45f), new Vector2(0.22f, 0.12f), o + 1);
                    break;
                case FieldItemVisual.Shade:
                    Box(root, "PoleL", Theme.Hex(0x7A5A3A), new Vector2(-0.9f, 0.6f), new Vector2(0.12f, 1.2f), o);
                    Box(root, "PoleR", Theme.Hex(0x7A5A3A), new Vector2(0.9f, 0.6f), new Vector2(0.12f, 1.2f), o);
                    Round(root, "Canvas", Theme.Teal, new Vector2(0, 1.25f), new Vector2(2.4f, 0.5f), o + 1);
                    break;
                case FieldItemVisual.Generator:
                    Round(root, "Body", Theme.Warning, new Vector2(0, 0.45f), new Vector2(1.2f, 0.9f), o);
                    Box(root, "Panel", Theme.Navy, new Vector2(-0.2f, 0.5f), new Vector2(0.4f, 0.3f), o + 1);
                    Dot(root, "Led", Theme.Success, new Vector2(0.35f, 0.65f), 0.14f, o + 1);
                    break;
                case FieldItemVisual.Pump:
                    Round(root, "Body", Theme.Teal, new Vector2(0, 0.4f), new Vector2(1f, 0.8f), o);
                    Box(root, "Pipe", Theme.TealDark, new Vector2(0.75f, 0.25f), new Vector2(0.6f, 0.18f), o + 1);
                    Dot(root, "Gauge", Theme.Paper, new Vector2(-0.15f, 0.5f), 0.3f, o + 1);
                    break;
                default:
                    for (int i = 0; i < 3; i++)
                        Round(root, "Bag", Theme.Hex(0xC9A86A), new Vector2(-0.45f + i * 0.45f, 0.25f), new Vector2(0.5f, 0.35f), o);
                    Round(root, "BagTop", Theme.Hex(0xB89558), new Vector2(0, 0.55f), new Vector2(0.5f, 0.35f), o + 1);
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
                    Ui(root, "Glow", ShapeSprites.Glow, Theme.WithAlpha(Lamp, 0.8f), new Vector2(0, 26 * k), new Vector2(70 * k, 70 * k));
                    Ui(root, "Pole", ShapeSprites.Square, Theme.Hex(0x3D4560), new Vector2(0, -8 * k), new Vector2(8 * k, 76 * k));
                    Ui(root, "Head", ShapeSprites.Rounded, Theme.Hex(0x2C3350), new Vector2(0, 32 * k), new Vector2(40 * k, 14 * k));
                    Ui(root, "Bulb", ShapeSprites.Circle, Lamp, new Vector2(0, 24 * k), new Vector2(16 * k, 16 * k));
                    break;
                case FieldItemVisual.WalkLight:
                    Ui(root, "Glow", ShapeSprites.Glow, Theme.WithAlpha(Lamp, 0.7f), new Vector2(0, 0), new Vector2(48 * k, 48 * k));
                    Ui(root, "Post", ShapeSprites.Square, Theme.Hex(0x3D4560), new Vector2(0, -22 * k), new Vector2(10 * k, 44 * k));
                    Ui(root, "Bulb", ShapeSprites.Circle, Lamp, new Vector2(0, 2 * k), new Vector2(18 * k, 18 * k));
                    break;
                case FieldItemVisual.AirConditioner:
                    Ui(root, "Body", ShapeSprites.Rounded, Theme.Paper, new Vector2(0, 10 * k), new Vector2(80 * k, 40 * k));
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
                    break;
                case FieldItemVisual.Generator:
                    Ui(root, "Body", ShapeSprites.Rounded, Theme.Warning, new Vector2(0, 0), new Vector2(70 * k, 54 * k));
                    Ui(root, "BoltA", ShapeSprites.Triangle, Theme.Paper, new Vector2(-4 * k, 6 * k), new Vector2(20 * k, 24 * k));
                    Ui(root, "BoltB", ShapeSprites.Triangle, Theme.Paper, new Vector2(4 * k, -8 * k), new Vector2(20 * k, 24 * k)).rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
                    break;
                case FieldItemVisual.Pump:
                    Ui(root, "Body", ShapeSprites.Rounded, Theme.Teal, new Vector2(-8 * k, 0), new Vector2(56 * k, 50 * k));
                    Ui(root, "Pipe", ShapeSprites.Square, Theme.TealDark, new Vector2(28 * k, -8 * k), new Vector2(30 * k, 10 * k));
                    Ui(root, "Drop", ShapeSprites.Circle, Theme.Cool, new Vector2(40 * k, -26 * k), new Vector2(12 * k, 12 * k));
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
    }
}
