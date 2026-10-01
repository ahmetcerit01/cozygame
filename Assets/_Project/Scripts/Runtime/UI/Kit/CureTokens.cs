using UnityEngine;

namespace CozyLab.Puzzle.UI
{
    /// <summary>
    /// Design tokens of the Cure Cells UI kit (colours, radii, type sizes, motion). Every kit component reads
    /// from here, so the look is tuned in one place. Direction: warm cream/ivory surfaces, restrained teal,
    /// subtle warm gold, dark brown type, soft rounded corners, layered warm shadows and a gentle bevel.
    /// </summary>
    public static class CureTokens
    {
        // ---------------------------------------------------------------- colour

        // Surfaces (cream family).
        public static readonly Color Ivory = MetaStyle.Hex(0xFCF6EA);
        public static readonly Color Cream = MetaStyle.Hex(0xF2DEBA);
        public static readonly Color CreamLight = MetaStyle.Hex(0xFFF7E7);
        public static readonly Color CreamRim = MetaStyle.Hex(0xEBD5AC);
        public static readonly Color Caramel = MetaStyle.Hex(0xA8794A);       // bevel base under cream
        public static readonly Color CaramelDark = MetaStyle.Hex(0x7A5230);   // glass rims
        public static readonly Color Parchment = MetaStyle.Hex(0xF7EBD3);

        // Type.
        public static readonly Color Text = MetaStyle.Hex(0x4A3020);          // dark brown
        public static readonly Color TextSoft = MetaStyle.Hex(0x7E644E);
        public static readonly Color TextOnTeal = MetaStyle.Hex(0xFFFCF2);
        public static readonly Color TitleLight = MetaStyle.Hex(0xFFF3DC);   // titles over artwork

        // Teal (primary).
        public static readonly Color Teal = MetaStyle.Hex(0x2A9A8A);
        public static readonly Color TealGloss = MetaStyle.Hex(0x62D2BD);
        public static readonly Color TealBase = MetaStyle.Hex(0x1C6A60);
        public static readonly Color TealRim = MetaStyle.Hex(0xC6F1E6);
        public static readonly Color TealInk = new Color(0.07f, 0.33f, 0.29f, 0.9f);

        // Accents.
        public static readonly Color Gold = MetaStyle.Hex(0xE9B44C);
        public static readonly Color GoldDeep = MetaStyle.Hex(0xB9862E);
        public static readonly Color Coral = MetaStyle.Hex(0xEE7E5E);
        public static readonly Color CoralDeep = MetaStyle.Hex(0xC45E41);

        // Depth.
        public static readonly Color Shadow = new Color(0.14f, 0.07f, 0.02f, 0.5f);
        public static readonly Color ShadowSoft = new Color(0.14f, 0.07f, 0.02f, 0.3f);
        public static readonly Color Dim = new Color(0.16f, 0.1f, 0.05f, 0.55f);
        public static readonly Color Track = new Color(0.22f, 0.13f, 0.06f, 0.55f);

        // ---------------------------------------------------------------- shape

        public const float RadiusCard = 46f;
        public const float RadiusPanel = 56f;
        public const float RadiusSmall = 28f;
        public const float Bevel = 11f;          // visible base under raised surfaces
        public const float RimInset = 6f;        // rim thickness around faces

        // ---------------------------------------------------------------- type

        public const int TitleSize = 64;
        public const int HeaderSize = 56;
        public const int ButtonSize = 54;
        public const int ButtonSizeSmall = 40;
        public const int BodySize = 32;
        public const int CaptionSize = 26;

        // ---------------------------------------------------------------- motion

        public const float PressScale = 0.96f;
        public const float PressDuration = 0.07f;
        public const float ReleaseDuration = 0.3f;
        public const float PressTint = 0.9f;
    }
}
