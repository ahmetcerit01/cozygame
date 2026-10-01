using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>
    /// Flat, single-colour procedural icons for meta buttons and tabs, built from the same rounded primitives
    /// as the rest of the UI (no texture assets). Each draws into a square of <c>size</c> centred on the parent.
    /// </summary>
    public static class MetaIcons
    {
        public static void Home(RectTransform p, float s, Color c)
        {
            var roof = R(p, "Roof", s * 0.62f, s * 0.62f, s * 0.1f, c, 0f, s * 0.06f);
            roof.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            R(p, "Body", s * 0.66f, s * 0.46f, s * 0.08f, c, 0f, -s * 0.2f);
            R(p, "Door", s * 0.18f, s * 0.26f, s * 0.07f, Hole(c), 0f, -s * 0.3f);
        }

        /// <summary>Three sample tubes in a rack.</summary>
        public static void Rack(RectTransform p, float s, Color c)
        {
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * s * 0.28f;
                R(p, "Tube", s * 0.2f, s * 0.78f, s * 0.1f, c, x, s * 0.02f);
                R(p, "Liquid", s * 0.12f, s * (0.2f + i * 0.08f), s * 0.06f, Hole(c), x, -s * (0.22f - i * 0.04f));
            }
            R(p, "Bar", s * 0.96f, s * 0.12f, s * 0.05f, c, 0f, -s * 0.06f);
        }

        public static void Microscope(RectTransform p, float s, Color c)
        {
            R(p, "Base", s * 0.72f, s * 0.14f, s * 0.07f, c, 0f, -s * 0.42f);
            R(p, "Arm", s * 0.16f, s * 0.62f, s * 0.08f, c, s * 0.2f, -s * 0.08f);
            var tube = R(p, "Tube", s * 0.2f, s * 0.5f, s * 0.08f, c, -s * 0.08f, s * 0.14f);
            tube.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            R(p, "Stage", s * 0.5f, s * 0.08f, s * 0.04f, c, -s * 0.04f, -s * 0.18f);
            R(p, "Eyepiece", s * 0.26f, s * 0.1f, s * 0.05f, c, -s * 0.16f, s * 0.42f)
                .rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
        }

        public static void Gift(RectTransform p, float s, Color c)
        {
            R(p, "Box", s * 0.74f, s * 0.52f, s * 0.08f, c, 0f, -s * 0.2f);
            R(p, "Lid", s * 0.86f, s * 0.2f, s * 0.07f, c, 0f, s * 0.12f);
            R(p, "RibbonV", s * 0.13f, s * 0.72f, s * 0.04f, Hole(c), 0f, -s * 0.1f);
            R(p, "RibbonH", s * 0.86f, s * 0.06f, s * 0.03f, Hole(c), 0f, s * 0.02f);
            var left = R(p, "BowL", s * 0.3f, s * 0.2f, s * 0.1f, c, -s * 0.15f, s * 0.31f);
            left.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 25f);
            var right = R(p, "BowR", s * 0.3f, s * 0.2f, s * 0.1f, c, s * 0.15f, s * 0.31f);
            right.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -25f);
        }

        /// <summary>Shopping basket: wide slatted body under a tall arched handle.</summary>
        public static void Bag(RectTransform p, float s, Color c)
        {
            UIFactory.CreateSpriteImage("Handle", p, ProceduralSprites.Ring, new Vector2(s * 0.72f, s * 0.8f), c, new Vector2(0f, s * 0.08f));
            R(p, "Rim", s * 1.0f, s * 0.14f, s * 0.07f, c, 0f, -s * 0.06f);
            R(p, "Body", s * 0.84f, s * 0.38f, s * 0.1f, c, 0f, -s * 0.28f);
            for (int i = -1; i <= 1; i++)
                R(p, "Slat", s * 0.08f, s * 0.24f, s * 0.04f, Hole(c), i * s * 0.22f, -s * 0.28f);
        }

        public static void Lock(RectTransform p, float s, Color c)
        {
            UIFactory.CreateSpriteImage("Shackle", p, ProceduralSprites.Ring, new Vector2(s * 0.52f, s * 0.6f), c, new Vector2(0f, s * 0.14f));
            R(p, "Body", s * 0.72f, s * 0.5f, s * 0.1f, c, 0f, -s * 0.18f);
            UIFactory.CreateSpriteImage("Hole", p, ProceduralSprites.Circle, new Vector2(s * 0.14f, s * 0.14f), Hole(c),
                new Vector2(0f, -s * 0.16f));
        }

        public static void Check(RectTransform p, float s, Color c)
        {
            float t = s * 0.16f;
            R(p, "Short", s * 0.36f, t, t * 0.5f, c, -s * 0.16f, -s * 0.04f).rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            R(p, "Long", s * 0.66f, t, t * 0.5f, c, s * 0.1f, s * 0.06f).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        /// <summary>Four tiles: "all experiments".</summary>
        public static void Grid(RectTransform p, float s, Color c)
        {
            for (int i = 0; i < 4; i++)
                R(p, "Tile", s * 0.38f, s * 0.38f, s * 0.1f, c, (i % 2 == 0 ? -1f : 1f) * s * 0.22f, (i < 2 ? 1f : -1f) * s * 0.22f);
        }

        public static void Arrow(RectTransform p, float s, Color c)
        {
            float t = s * 0.2f;
            R(p, "Stem", t, s * 0.56f, t * 0.5f, c, 0f, -s * 0.12f);
            R(p, "L", s * 0.5f, t, t * 0.5f, c, -s * 0.13f, s * 0.2f).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            R(p, "R", s * 0.5f, t, t * 0.5f, c, s * 0.13f, s * 0.2f).rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
        }

        /// <summary>Left-pointing arrow (back buttons).</summary>
        public static void ArrowLeft(RectTransform p, float s, Color c)
        {
            float t = s * 0.2f;
            R(p, "Stem", s * 0.7f, t, t * 0.5f, c, s * 0.08f, 0f);
            R(p, "Up", s * 0.5f, t, t * 0.5f, c, -s * 0.16f, s * 0.15f).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            R(p, "Down", s * 0.5f, t, t * 0.5f, c, -s * 0.16f, -s * 0.15f).rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
        }

        /// <summary>Rounded "×" (close buttons).</summary>
        public static void Close(RectTransform p, float s, Color c)
        {
            float t = s * 0.2f;
            R(p, "A", s * 0.9f, t, t * 0.5f, c, 0f, 0f).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            R(p, "B", s * 0.9f, t, t * 0.5f, c, 0f, 0f).rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
        }

        /// <summary>Petri-dish logo mark: a dish with two little cells.</summary>
        public static void Cell(RectTransform p, float s, Color c)
        {
            UIFactory.CreateSpriteImage("Dish", p, ProceduralSprites.Circle, new Vector2(s, s), c);
            UIFactory.CreateSpriteImage("Agar", p, ProceduralSprites.Circle, new Vector2(s * 0.78f, s * 0.78f), MetaStyle.Glass);
            UIFactory.CreateSpriteImage("CellA", p, ProceduralSprites.Circle, new Vector2(s * 0.34f, s * 0.34f), MetaStyle.Coral,
                new Vector2(-s * 0.1f, s * 0.08f));
            UIFactory.CreateSpriteImage("CellB", p, ProceduralSprites.Circle, new Vector2(s * 0.22f, s * 0.22f), MetaStyle.Olive,
                new Vector2(s * 0.17f, -s * 0.14f));
            UIFactory.CreateSpriteImage("Shine", p, ProceduralSprites.SoftGlow, new Vector2(s * 0.3f, s * 0.18f),
                new Color(1f, 1f, 1f, 0.8f), new Vector2(-s * 0.2f, s * 0.24f));
        }

        // ---------------------------------------------------------------- full-colour icons (tiles, pills)

        public static void Coin(RectTransform p, float s)
        {
            UIFactory.CreateSpriteImage("Edge", p, ProceduralSprites.Circle, new Vector2(s * 0.84f, s * 0.84f), MetaStyle.GoldDark,
                new Vector2(0f, -s * 0.04f));
            UIFactory.CreateSpriteImage("Face", p, ProceduralSprites.Circle, new Vector2(s * 0.84f, s * 0.84f), MetaStyle.Gold);
            UIFactory.CreateSpriteImage("Ring", p, ProceduralSprites.Ring, new Vector2(s * 0.6f, s * 0.6f),
                new Color(MetaStyle.GoldDark.r, MetaStyle.GoldDark.g, MetaStyle.GoldDark.b, 0.8f));
            R(p, "Mark", s * 0.1f, s * 0.3f, s * 0.05f, new Color(1f, 0.93f, 0.7f), 0f, 0f);
            UIFactory.CreateSpriteImage("Shine", p, ProceduralSprites.SoftGlow, new Vector2(s * 0.34f, s * 0.2f),
                new Color(1f, 1f, 1f, 0.8f), new Vector2(-s * 0.14f, s * 0.2f));
        }

        public static void Flask(RectTransform p, float s) => ResearchIcon.Create(p, s, MetaStyle.Teal);

        /// <summary>A little petri dish with colourful jelly samples (Experiments tile).</summary>
        public static void Petri(RectTransform p, float s)
        {
            UIFactory.CreateSpriteImage("Shadow", p, ProceduralSprites.SoftGlow, new Vector2(s * 1.1f, s * 0.5f),
                new Color(0.3f, 0.18f, 0.08f, 0.35f), new Vector2(0f, -s * 0.2f));
            var dish = UIFactory.CreateCentered("Dish", p, new Vector2(s, s * 0.62f), new Vector2(0f, -s * 0.02f));
            UIFactory.CreateSpriteImage("Rim", dish, ProceduralSprites.Circle, new Vector2(s, s * 0.62f), MetaStyle.GlassEdge);
            UIFactory.CreateSpriteImage("Agar", dish, ProceduralSprites.Circle, new Vector2(s * 0.88f, s * 0.52f), MetaStyle.Glass);
            Blob(dish, s * 0.3f, MetaStyle.Hex(0xFF8A5B), -s * 0.2f, s * 0.06f);
            Blob(dish, s * 0.26f, MetaStyle.Hex(0xB37BF0), s * 0.2f, s * 0.08f);
            Blob(dish, s * 0.34f, MetaStyle.Hex(0x3FC4D8), -s * 0.02f, -s * 0.08f);
            Blob(dish, s * 0.22f, MetaStyle.Hex(0xF07BB8), s * 0.24f, -s * 0.1f);
            UIFactory.CreateSpriteImage("Shine", dish, ProceduralSprites.SoftGlow, new Vector2(s * 0.4f, s * 0.14f),
                new Color(1f, 1f, 1f, 0.75f), new Vector2(-s * 0.18f, s * 0.2f));
        }

        public static void Calendar(RectTransform p, float s)
        {
            R(p, "Shadow", s * 0.84f, s * 0.8f, s * 0.14f, new Color(0.3f, 0.18f, 0.08f, 0.25f), 0f, -s * 0.07f);
            R(p, "Page", s * 0.84f, s * 0.8f, s * 0.14f, MetaStyle.Ivory, 0f, -s * 0.03f);
            R(p, "Top", s * 0.84f, s * 0.26f, s * 0.12f, MetaStyle.Coral, 0f, s * 0.24f);
            R(p, "RingL", s * 0.08f, s * 0.2f, s * 0.04f, MetaStyle.Charcoal, -s * 0.2f, s * 0.38f);
            R(p, "RingR", s * 0.08f, s * 0.2f, s * 0.04f, MetaStyle.Charcoal, s * 0.2f, s * 0.38f);
            for (int i = 0; i < 6; i++)
                R(p, "Day", s * 0.14f, s * 0.12f, s * 0.03f, i == 4 ? MetaStyle.Teal : MetaStyle.CreamEdge,
                    (i % 3 - 1) * s * 0.22f, -s * (0.04f + (i / 3) * 0.2f));
        }

        public static void Basket(RectTransform p, float s)
        {
            UIFactory.CreateSpriteImage("Handle", p, ProceduralSprites.Ring, new Vector2(s * 0.62f, s * 0.7f), MetaStyle.CoralDark,
                new Vector2(0f, s * 0.12f));
            R(p, "Rim", s * 0.96f, s * 0.16f, s * 0.08f, MetaStyle.CoralDark, 0f, -s * 0.04f);
            R(p, "Body", s * 0.82f, s * 0.42f, s * 0.1f, MetaStyle.Hex(0xF29A4A), 0f, -s * 0.28f);
            for (int i = -1; i <= 1; i++)
                R(p, "Slat", s * 0.07f, s * 0.26f, s * 0.035f, MetaStyle.Hex(0xC76A22), i * s * 0.2f, -s * 0.28f);
        }

        /// <summary>Lab microscope in lab colours (Lab tile).</summary>
        public static void LabMicroscope(RectTransform p, float s, Data.PuzzleTheme theme) => LabArt.Microscope(p, theme, 2, s);

        private static void Blob(Transform parent, float d, Color c, float x, float y)
        {
            UIFactory.CreateSpriteImage("Blob", parent, ProceduralSprites.Circle, new Vector2(d, d * 0.8f), c, new Vector2(x, y));
            UIFactory.CreateSpriteImage("Hi", parent, ProceduralSprites.SoftGlow, new Vector2(d * 0.45f, d * 0.25f),
                new Color(1f, 1f, 1f, 0.7f), new Vector2(x - d * 0.12f, y + d * 0.16f));
        }

        private static Color Hole(Color c) =>
            c.grayscale > 0.8f ? new Color(MetaStyle.Teal.r, MetaStyle.Teal.g, MetaStyle.Teal.b, 0.9f) : MetaStyle.Cream;

        private static Image R(Transform parent, string name, float w, float h, float radius, Color color, float x, float y) =>
            UIFactory.CreateRoundedImage(name, parent, new Vector2(w, h), radius, color, new Vector2(x, y));
    }
}
