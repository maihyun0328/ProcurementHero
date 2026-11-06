using UnityEngine;

namespace ProcurementHero
{
    /// <summary>외부 그림 없이 쓰는 기본 도형 Sprite. 실행 시 한 번 생성해 재사용한다.</summary>
    public static class ShapeSprites
    {
        static Sprite square, rounded, circle, triangle, glow, stripes;

        /// <summary>'아직 조사하지 않음'을 색이 아닌 무늬로도 보이게 하는 사선 무늬(타일).</summary>
        public static Sprite Stripes
        {
            get
            {
                if (stripes) return stripes;
                const int size = 32;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int d = (x + y) % 16;
                    byte a = (byte)(d < 5 ? 255 : 0);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(pixels);
                tex.Apply(false, true);
                stripes = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                stripes.hideFlags = HideFlags.DontSave;
                return stripes;
            }
        }

        public static Sprite Square => square ? square : square = Make(4, 0, (x, y, s) => 1f);

        /// <summary>9-slice용 둥근 사각형.</summary>
        public static Sprite Rounded => rounded ? rounded : rounded = Make(64, 24, RoundedAlpha);

        public static Sprite Circle => circle ? circle : circle = Make(128, 0, (x, y, s) =>
        {
            float r = s * 0.5f;
            float d = Mathf.Sqrt((x - r + 0.5f) * (x - r + 0.5f) + (y - r + 0.5f) * (y - r + 0.5f));
            return Mathf.Clamp01(r - d);
        });

        /// <summary>위쪽을 가리키는 삼각형(지붕).</summary>
        public static Sprite Triangle => triangle ? triangle : triangle = Make(128, 0, (x, y, s) =>
        {
            float fy = (y + 0.5f) / s;
            float half = (1f - fy) * 0.5f * s;
            float c = s * 0.5f;
            return Mathf.Clamp01(half - Mathf.Abs(x + 0.5f - c) + 0.5f);
        });

        /// <summary>가장자리로 갈수록 투명해지는 빛 번짐.</summary>
        public static Sprite Glow => glow ? glow : glow = Make(128, 0, (x, y, s) =>
        {
            float r = s * 0.5f;
            float d = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r)) / r;
            return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d);
        });

        static float RoundedAlpha(int x, int y, int s)
        {
            const float radius = 22f;
            float px = x + 0.5f, py = y + 0.5f;
            float cx = Mathf.Clamp(px, radius, s - radius);
            float cy = Mathf.Clamp(py, radius, s - radius);
            float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
            return Mathf.Clamp01(radius - d + 0.5f);
        }

        static Sprite Make(int size, int border, System.Func<int, int, int, float> alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha(x, y, size) * 255f));
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
