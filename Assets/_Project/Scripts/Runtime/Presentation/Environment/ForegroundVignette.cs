using CozyLab.Puzzle.Data;
using UnityEngine;

namespace CozyLab.Puzzle.Presentation.Environment
{
    /// <summary>
    /// Environment layer 7: foreground decorative detail, above gameplay but below the HUD. Currently just
    /// a very soft corner vignette so the desk/props read as a cohesive photographed scene; kept subtle
    /// enough that it never affects board or piece readability.
    /// </summary>
    public static class ForegroundVignette
    {
        public static void Build(RectTransform parent, PuzzleTheme theme)
        {
            var root = UIFactory.CreateStretched("ForegroundVignette", parent);
            var tint = new Color(theme.textPrimaryColor.r, theme.textPrimaryColor.g, theme.textPrimaryColor.b, 0.05f);

            AddCorner(root, tint, new Vector2(0f, 0f));
            AddCorner(root, tint, new Vector2(1f, 0f));
            AddCorner(root, tint, new Vector2(0f, 1f));
            AddCorner(root, tint, new Vector2(1f, 1f));
        }

        private static void AddCorner(RectTransform parent, Color tint, Vector2 anchor)
        {
            var image = UIFactory.CreateSpriteImage("Corner", parent, ProceduralSprites.SoftGlow, new Vector2(900f, 900f), tint);
            image.raycastTarget = false;
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
