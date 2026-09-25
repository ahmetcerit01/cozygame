using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>
    /// Procedural lab artwork built from the same soft rounded primitives as the puzzle.
    /// All builders return a root sized (w, h) with the object standing on its bottom edge.
    /// </summary>
    public static class LabArt
    {
        // ------------------------------------------------------------------ microscope

        /// <summary>Side-view microscope. Levels 1–3 add visibly more detail (not just a label change).</summary>
        public static RectTransform Microscope(Transform parent, PuzzleTheme theme, int level, float height)
        {
            float h = height;
            float w = h * 0.8f;
            var root = UIFactory.CreateCentered($"Microscope_L{level}", parent, new Vector2(w, h));

            bool refined = level >= 2;
            bool advanced = level >= 3;
            var body = refined ? Color.Lerp(theme.cardColor, theme.metalColor, 0.12f) : theme.metalColor;
            var dark = theme.metalDarkColor;
            var trim = refined ? theme.accentColor : Color.Lerp(theme.metalColor, dark, 0.35f);

            float Y(float fromBottom) => -h * 0.5f + fromBottom * h;

            Shadow(root, w * 0.95f, h * 0.08f, Y(0.02f));

            // Base.
            R(root, "Base", w * 0.78f, h * 0.12f, h * 0.05f, dark, 0f, Y(0.07f));
            R(root, "BaseTop", w * 0.7f, h * 0.035f, h * 0.02f, Color.Lerp(dark, Color.white, 0.2f), 0f, Y(0.125f));

            // Arm (behind the tube).
            R(root, "Arm", w * 0.15f, h * 0.62f, w * 0.07f, body, w * 0.22f, Y(0.45f));
            R(root, "ArmShade", w * 0.05f, h * 0.55f, w * 0.025f, Color.Lerp(body, dark, 0.25f), w * 0.27f, Y(0.45f));
            // Focus knob.
            C(root, "Knob", w * 0.16f, dark, w * 0.25f, Y(0.36f));
            C(root, "KnobCap", w * 0.08f, trim, w * 0.25f, Y(0.36f));

            // Stage + light.
            if (advanced)
                UIFactory.CreateSpriteImage("StageLight", root, ProceduralSprites.SoftGlow, new Vector2(w * 0.5f, h * 0.16f),
                    new Color(theme.warmLightColor.r, theme.warmLightColor.g, theme.warmLightColor.b, 0.85f), new Vector2(-w * 0.06f, Y(0.29f)));
            R(root, "Stage", w * 0.62f, h * 0.045f, h * 0.02f, dark, -w * 0.06f, Y(0.33f));
            if (refined)
            {
                // Sample slide with a tiny colony.
                R(root, "Slide", w * 0.36f, h * 0.022f, h * 0.01f, new Color(1f, 1f, 1f, 0.95f), -w * 0.06f, Y(0.36f));
                C(root, "Sample", w * 0.06f, theme.GetPieceColor(1), -w * 0.06f, Y(0.365f));
            }

            // Objective(s).
            R(root, "Objective", w * 0.09f, h * 0.1f, w * 0.03f, dark, -w * 0.06f, Y(0.43f));
            if (refined)
            {
                var second = R(root, "Objective2", w * 0.08f, h * 0.085f, w * 0.03f, Color.Lerp(dark, trim, 0.3f), -w * 0.15f, Y(0.44f));
                second.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -22f);
            }
            if (advanced)
            {
                var third = R(root, "Objective3", w * 0.08f, h * 0.085f, w * 0.03f, Color.Lerp(dark, trim, 0.3f), w * 0.03f, Y(0.44f));
                third.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 22f);
            }
            R(root, "Nosepiece", w * 0.3f, h * 0.05f, h * 0.025f, Color.Lerp(body, dark, 0.2f), -w * 0.06f, Y(0.495f));

            // Tube + eyepiece.
            R(root, "Tube", w * 0.2f, h * 0.36f, w * 0.08f, body, -w * 0.06f, Y(0.68f));
            R(root, "TubeShine", w * 0.04f, h * 0.3f, w * 0.02f, new Color(1f, 1f, 1f, 0.45f), -w * 0.11f, Y(0.68f));
            R(root, "Band", w * 0.22f, h * 0.035f, h * 0.015f, trim, -w * 0.06f, Y(0.62f));
            R(root, "Eyepiece", w * 0.16f, h * 0.1f, w * 0.05f, dark, -w * 0.06f, Y(0.9f));
            R(root, "EyeRim", w * 0.2f, h * 0.03f, h * 0.015f, advanced ? trim : Color.Lerp(dark, Color.white, 0.25f),
                -w * 0.06f, Y(0.955f));

            if (advanced)
            {
                // Small display on the base with a glowing readout.
                R(root, "Display", w * 0.3f, h * 0.075f, h * 0.02f, new Color(0.16f, 0.24f, 0.3f), -w * 0.18f, Y(0.07f));
                UIFactory.CreateSpriteImage("DisplayGlow", root, ProceduralSprites.SoftGlow, new Vector2(w * 0.36f, h * 0.11f),
                    new Color(theme.accentColor.r, theme.accentColor.g, theme.accentColor.b, 0.55f), new Vector2(-w * 0.18f, Y(0.07f)));
                for (int i = 0; i < 4; i++)
                    R(root, "Bar", w * 0.03f, h * (0.02f + 0.012f * ((i * 7) % 3)), w * 0.01f, theme.accentColor,
                        -w * 0.26f + i * w * 0.05f, Y(0.07f));
                C(root, "Led", w * 0.045f, new Color(0.45f, 0.85f, 0.6f), w * 0.1f, Y(0.07f));
            }
            return root;
        }

        // ------------------------------------------------------------------ incubator

        public static RectTransform Incubator(Transform parent, PuzzleTheme theme, bool active, float size)
        {
            float w = size;
            float h = size * 0.95f;
            var root = UIFactory.CreateCentered("Incubator", parent, new Vector2(w, h));
            float Y(float fromBottom) => -h * 0.5f + fromBottom * h;

            var shell = active ? theme.cardColor : Color.Lerp(theme.wellColor, theme.metalColor, 0.25f);
            var trim = active ? theme.accentColor : theme.metalColor;

            Shadow(root, w, h * 0.08f, Y(0.02f));
            R(root, "Foot", w * 0.12f, h * 0.06f, h * 0.02f, theme.metalDarkColor, -w * 0.3f, Y(0.04f));
            R(root, "Foot2", w * 0.12f, h * 0.06f, h * 0.02f, theme.metalDarkColor, w * 0.3f, Y(0.04f));
            R(root, "Body", w * 0.92f, h * 0.86f, w * 0.12f, shell, 0f, Y(0.5f));
            R(root, "Panel", w * 0.8f, h * 0.12f, h * 0.05f, Color.Lerp(shell, theme.metalDarkColor, 0.12f), 0f, Y(0.83f));
            C(root, "Led", w * 0.06f, active ? new Color(0.45f, 0.85f, 0.6f) : Color.Lerp(theme.metalColor, Color.white, 0.2f),
                w * 0.3f, Y(0.83f));
            R(root, "Dial", w * 0.28f, h * 0.045f, h * 0.02f, trim, -w * 0.15f, Y(0.83f));

            // Round window.
            float win = w * 0.52f;
            C(root, "WindowRim", win * 1.12f, trim, 0f, Y(0.44f));
            C(root, "Window", win, active ? new Color(1f, 0.95f, 0.86f) : new Color(0.55f, 0.61f, 0.67f), 0f, Y(0.44f));
            if (active)
            {
                UIFactory.CreateSpriteImage("Warmth", root, ProceduralSprites.SoftGlow, new Vector2(win * 1.1f, win * 1.1f),
                    new Color(theme.warmLightColor.r, theme.warmLightColor.g, theme.warmLightColor.b, 0.9f), new Vector2(0f, Y(0.44f)));
                C(root, "Culture", win * 0.18f, theme.GetPieceColor(0), -win * 0.14f, Y(0.41f));
                C(root, "Culture2", win * 0.13f, theme.GetPieceColor(2), win * 0.16f, Y(0.47f));
            }
            UIFactory.CreateSpriteImage("Glass", root, ProceduralSprites.SoftGlow, new Vector2(win * 0.45f, win * 0.24f),
                new Color(1f, 1f, 1f, active ? 0.6f : 0.25f), new Vector2(-win * 0.15f, Y(0.52f)));
            return root;
        }

        // ------------------------------------------------------------------ analyzer

        public static RectTransform Analyzer(Transform parent, PuzzleTheme theme, bool active, float size)
        {
            float w = size;
            float h = size * 0.9f;
            var root = UIFactory.CreateCentered("Analyzer", parent, new Vector2(w, h));
            float Y(float fromBottom) => -h * 0.5f + fromBottom * h;

            var shell = active ? theme.cardColor : Color.Lerp(theme.wellColor, theme.metalColor, 0.25f);

            Shadow(root, w, h * 0.08f, Y(0.02f));
            R(root, "Body", w * 0.94f, h * 0.62f, w * 0.1f, shell, 0f, Y(0.33f));
            R(root, "Keys", w * 0.5f, h * 0.08f, h * 0.03f, Color.Lerp(shell, theme.metalDarkColor, 0.15f), -w * 0.12f, Y(0.14f));
            for (int i = 0; i < 4; i++)
                C(root, "Key", w * 0.06f, active ? theme.GetPieceColor(i) : theme.metalColor, -w * 0.3f + i * w * 0.12f, Y(0.14f));

            // Monitor on a short stand.
            R(root, "Stand", w * 0.1f, h * 0.12f, w * 0.03f, theme.metalDarkColor, 0f, Y(0.66f));
            R(root, "Monitor", w * 0.86f, h * 0.44f, w * 0.08f, theme.metalDarkColor, 0f, Y(0.84f) - h * 0.06f);
            var screen = R(root, "Screen", w * 0.74f, h * 0.33f, w * 0.05f,
                active ? new Color(0.14f, 0.3f, 0.33f) : new Color(0.22f, 0.26f, 0.3f), 0f, Y(0.84f) - h * 0.06f);
            if (active)
            {
                UIFactory.CreateSpriteImage("ScreenGlow", root, ProceduralSprites.SoftGlow, new Vector2(w * 1.05f, h * 0.6f),
                    new Color(theme.accentColor.r, theme.accentColor.g, theme.accentColor.b, 0.35f), screen.rectTransform.anchoredPosition);
                // Tiny waveform readout.
                float[] bars = { 0.3f, 0.6f, 0.4f, 0.9f, 0.5f, 0.75f, 0.35f };
                for (int i = 0; i < bars.Length; i++)
                    R(root, "Wave", w * 0.05f, h * 0.22f * bars[i], w * 0.02f, theme.accentColor,
                        -w * 0.27f + i * w * 0.09f, screen.rectTransform.anchoredPosition.y - h * 0.1f + h * 0.11f * bars[i]);
            }
            return root;
        }

        // ------------------------------------------------------------------ decorations

        public static RectTransform TubeRack(Transform parent, PuzzleTheme theme, float size)
        {
            var root = UIFactory.CreateCentered("TubeRack", parent, new Vector2(size, size));
            float Y(float f) => -size * 0.5f + f * size;
            Shadow(root, size, size * 0.08f, Y(0.02f));
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * size * 0.26f;
                R(root, "Tube", size * 0.16f, size * 0.72f, size * 0.08f, new Color(1f, 1f, 1f, 0.9f), x, Y(0.44f));
                R(root, "Liquid", size * 0.12f, size * 0.32f, size * 0.06f, theme.GetPieceColor(i + 3), x, Y(0.25f));
                R(root, "Cap", size * 0.18f, size * 0.06f, size * 0.03f, theme.GetPieceColor(i + 3), x, Y(0.8f));
            }
            R(root, "Rack", size * 0.95f, size * 0.12f, size * 0.04f, theme.metalDarkColor, 0f, Y(0.34f));
            R(root, "RackBase", size, size * 0.08f, size * 0.03f, theme.metalDarkColor, 0f, Y(0.06f));
            return root;
        }

        public static RectTransform Plant(Transform parent, PuzzleTheme theme, float size)
        {
            var root = UIFactory.CreateCentered("Plant", parent, new Vector2(size, size));
            float Y(float f) => -size * 0.5f + f * size;
            var leaf = new Color(0.52f, 0.76f, 0.5f);
            var leafDark = new Color(0.4f, 0.65f, 0.43f);
            float[] angles = { -40f, -12f, 14f, 38f };
            for (int i = 0; i < angles.Length; i++)
            {
                var l = R(root, "Leaf", size * 0.2f, size * 0.52f, size * 0.1f, i % 2 == 0 ? leaf : leafDark,
                    Mathf.Sin(angles[i] * Mathf.Deg2Rad) * size * 0.22f, Y(0.6f));
                l.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angles[i]);
            }
            R(root, "Pot", size * 0.5f, size * 0.36f, size * 0.08f, new Color(0.93f, 0.62f, 0.5f), 0f, Y(0.18f));
            R(root, "PotRim", size * 0.58f, size * 0.09f, size * 0.04f, new Color(0.88f, 0.55f, 0.44f), 0f, Y(0.36f));
            return root;
        }

        public static RectTransform Lamp(Transform parent, PuzzleTheme theme, float size)
        {
            var root = UIFactory.CreateCentered("Lamp", parent, new Vector2(size, size));
            float Y(float f) => -size * 0.5f + f * size;
            UIFactory.CreateSpriteImage("Light", root, ProceduralSprites.SoftGlow, new Vector2(size * 1.1f, size * 0.7f),
                new Color(theme.warmLightColor.r, theme.warmLightColor.g, theme.warmLightColor.b, 0.55f), new Vector2(-size * 0.12f, Y(0.3f)));
            R(root, "Base", size * 0.5f, size * 0.08f, size * 0.04f, theme.metalDarkColor, size * 0.15f, Y(0.04f));
            var arm = R(root, "Arm", size * 0.06f, size * 0.7f, size * 0.03f, theme.metalDarkColor, size * 0.2f, Y(0.4f));
            arm.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 10f);
            var shade = R(root, "Shade", size * 0.42f, size * 0.2f, size * 0.1f, theme.accentColor, -size * 0.05f, Y(0.74f));
            shade.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 25f);
            return root;
        }

        /// <summary>Pendant lamp hanging from the ceiling with a warm pool of light (Lab Level 2+).</summary>
        public static RectTransform PendantLamp(Transform parent, PuzzleTheme theme, float size)
        {
            float h = size * 1.2f;
            var root = UIFactory.CreateCentered("PendantLamp", parent, new Vector2(size, h));
            UIFactory.CreateSpriteImage("Light", root, ProceduralSprites.SoftGlow, new Vector2(size * 2.2f, size * 1.6f),
                new Color(theme.warmLightColor.r, theme.warmLightColor.g, theme.warmLightColor.b, 0.45f), new Vector2(0f, -h * 0.55f));
            R(root, "Cord", size * 0.03f, h * 0.55f, size * 0.015f, theme.metalDarkColor, 0f, h * 0.22f);
            R(root, "Cap", size * 0.16f, size * 0.09f, size * 0.04f, theme.metalDarkColor, 0f, -h * 0.07f);
            R(root, "Shade", size * 0.62f, size * 0.26f, size * 0.12f, theme.accentColor, 0f, -h * 0.2f);
            R(root, "ShadeRim", size * 0.66f, size * 0.06f, size * 0.03f, Color.Lerp(theme.accentColor, Color.black, 0.15f),
                0f, -h * 0.3f);
            C(root, "Bulb", size * 0.18f, new Color(1f, 0.97f, 0.88f), 0f, -h * 0.34f);
            return root;
        }

        public static RectTransform Jars(Transform parent, PuzzleTheme theme, float size)
        {
            var root = UIFactory.CreateCentered("Jars", parent, new Vector2(size, size * 0.6f));
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * size * 0.32f;
                float jh = size * (0.42f + 0.08f * (i % 2));
                R(root, "Jar", size * 0.26f, jh, size * 0.07f, new Color(1f, 1f, 1f, 0.85f), x, -size * 0.3f + jh * 0.5f);
                C(root, "Specimen", size * 0.13f, theme.GetPieceColor(i + 5), x, -size * 0.3f + jh * 0.4f);
                R(root, "Lid", size * 0.28f, size * 0.06f, size * 0.03f, theme.metalDarkColor, x, -size * 0.3f + jh);
            }
            return root;
        }

        /// <summary>Framed "experiment complete" note pinned to the wall.</summary>
        public static RectTransform Certificate(Transform parent, PuzzleTheme theme, float size)
        {
            var root = UIFactory.CreateCentered("Certificate", parent, new Vector2(size, size * 0.78f));
            UIFactory.CreateShadow("Shadow", root, new Vector2(size, size * 0.78f), size * 0.08f, new Color(0f, 0f, 0f, 0.15f),
                new Vector2(0f, -size * 0.04f));
            UIFactory.CreateRoundedImage("Frame", root, new Vector2(size, size * 0.78f), size * 0.06f, theme.metalDarkColor);
            UIFactory.CreateRoundedImage("Paper", root, new Vector2(size * 0.86f, size * 0.64f), size * 0.04f, theme.cardColor);
            for (int i = 0; i < 3; i++)
                UIFactory.CreateRoundedImage("Line", root, new Vector2(size * 0.5f, size * 0.035f), size * 0.015f,
                    new Color(theme.textSecondaryColor.r, theme.textSecondaryColor.g, theme.textSecondaryColor.b, 0.35f),
                    new Vector2(-size * 0.08f, size * (0.12f - i * 0.1f)));
            UIFactory.CreateSpriteImage("Seal", root, ProceduralSprites.Circle, new Vector2(size * 0.2f, size * 0.2f),
                theme.comboPerfectColor, new Vector2(size * 0.26f, -size * 0.12f));
            return root;
        }

        /// <summary>Top-down sample tray (Home "Experiments" object).</summary>
        public static RectTransform SampleTray(Transform parent, PuzzleTheme theme, float size, int filled)
        {
            var root = UIFactory.CreateCentered("SampleTray", parent, new Vector2(size, size * 0.7f));
            UIFactory.CreateShadow("Shadow", root, new Vector2(size, size * 0.7f), size * 0.07f, new Color(0.1f, 0.2f, 0.24f, 0.2f),
                new Vector2(0f, -size * 0.04f));
            UIFactory.CreateRoundedImage("Tray", root, new Vector2(size, size * 0.7f), size * 0.14f, theme.boardRimColor);
            UIFactory.CreateRoundedImage("Inner", root, new Vector2(size * 0.9f, size * 0.6f), size * 0.1f, theme.cardColor);
            for (int i = 0; i < 6; i++)
            {
                float x = (i % 3 - 1) * size * 0.28f;
                float y = (i < 3 ? 1 : -1) * size * 0.14f;
                UIFactory.CreateSpriteImage("Well", root, ProceduralSprites.Circle, new Vector2(size * 0.22f, size * 0.22f),
                    theme.wellColor, new Vector2(x, y));
                if (i < filled)
                    UIFactory.CreateSpriteImage("Sample", root, ProceduralSprites.Circle, new Vector2(size * 0.15f, size * 0.15f),
                        theme.GetPieceColor(i), new Vector2(x, y));
            }
            return root;
        }

        // ------------------------------------------------------------------ helpers

        private static Image R(Transform parent, string name, float w, float h, float radius, Color color, float x, float y)
        {
            return UIFactory.CreateRoundedImage(name, parent, new Vector2(w, h), radius, color, new Vector2(x, y));
        }

        private static Image C(Transform parent, string name, float d, Color color, float x, float y)
        {
            return UIFactory.CreateSpriteImage(name, parent, ProceduralSprites.Circle, new Vector2(d, d), color, new Vector2(x, y));
        }

        private static void Shadow(Transform parent, float w, float h, float y)
        {
            UIFactory.CreateSpriteImage("Shadow", parent, ProceduralSprites.SoftGlow, new Vector2(w * 1.1f, h * 2.2f),
                new Color(0.1f, 0.2f, 0.24f, 0.22f), new Vector2(0f, y));
        }
    }
}
