using UnityEngine;

namespace ProcurementHero
{
    /// <summary>크림 배경 · 남색 본문 · 청록 강조 팔레트.</summary>
    public static class Theme
    {
        public static readonly Color Cream = Hex(0xFFF6E3);
        public static readonly Color CreamDeep = Hex(0xF6E7C6);
        public static readonly Color Paper = Hex(0xFFFDF8);
        public static readonly Color Navy = Hex(0x1F2A4D);
        public static readonly Color NavySoft = Hex(0x55607E);
        public static readonly Color Teal = Hex(0x139E9A);
        public static readonly Color TealDark = Hex(0x0C7471);
        public static readonly Color TealPale = Hex(0xD3F1EE);
        public static readonly Color Success = Hex(0x2E9E5B);
        public static readonly Color SuccessPale = Hex(0xDDF3E4);
        public static readonly Color Warning = Hex(0xE0862A);
        public static readonly Color WarningPale = Hex(0xFCEBD6);
        public static readonly Color Danger = Hex(0xD24B46);
        public static readonly Color DangerPale = Hex(0xF8DEDC);
        public static readonly Color Disabled = Hex(0xC9C4B8);
        public static readonly Color Shade = new Color(0.12f, 0.16f, 0.3f, 0.55f);

        // 마을 색
        public static readonly Color Sky = Hex(0xBFE6F2);
        public static readonly Color Grass = Hex(0xA8D98A);
        public static readonly Color GrassDark = Hex(0x7FBF68);
        public static readonly Color Road = Hex(0xE9D7B0);
        public static readonly Color SchoolWall = Hex(0xF6C46A);
        public static readonly Color ClinicWall = Hex(0xF7F2EA);
        public static readonly Color ShelterWall = Hex(0x9FB7C9);
        public static readonly Color Roof = Hex(0xD9655B);
        public static readonly Color RoofBlue = Hex(0x4C6E9E);
        public static readonly Color Window = Hex(0x6A7A99);
        public static readonly Color WindowLit = Hex(0xFFE58A);
        public static readonly Color Water = new Color(0.31f, 0.6f, 0.86f, 0.75f);
        public static readonly Color Heat = Hex(0xF08A4B);
        public static readonly Color Cool = Hex(0x7FC8F0);
        public static readonly Color Skin = Hex(0xF4CFA8);

        public static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
