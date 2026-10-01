using CozyLab.Puzzle.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.Presentation.Environment
{
    /// <summary>
    /// Environment layer 2: ambient light and shadow, as independent overlays sitting above the desk but
    /// below the props/dish. Kept deliberately subtle (low alpha) so board readability never changes
    /// materially. Each overlay exposes its <see cref="RectTransform"/> for <see cref="AmbientSceneAnimator"/>
    /// to drift, and its <see cref="Image"/> as the replaceable slot for an authored transparent PNG later.
    /// </summary>
    public sealed class LightingOverlay : MonoBehaviour
    {
        public RectTransform Sunlight { get; private set; }
        public Image SunlightImage { get; private set; }

        public RectTransform WindowShadow { get; private set; }
        public Image WindowShadowImage { get; private set; }

        /// <summary>Optional; a future authored leaf-shadow overlay can replace this placeholder blob.</summary>
        public RectTransform LeafShadow { get; private set; }
        public Image LeafShadowImage { get; private set; }

        public void Build(RectTransform parent, PuzzleTheme theme)
        {
            var root = UIFactory.CreateStretched("LightingOverlay", parent);

            // Warm sunlight: a big, very soft glow from the upper-left, like light through a window.
            SunlightImage = UIFactory.CreateSpriteImage("Sunlight", root, ProceduralSprites.SoftGlow,
                new Vector2(1500f, 1500f), new Color(theme.warmLightColor.r, theme.warmLightColor.g, theme.warmLightColor.b, 0.16f),
                new Vector2(-260f, 520f));
            SunlightImage.raycastTarget = false;
            Sunlight = SunlightImage.rectTransform;

            // Soft window-pane shadow: a faint cross of dark bars, restrained enough to read as ambience,
            // not a distraction. A "+" made of two thin soft rectangles.
            var shadowTint = new Color(theme.textPrimaryColor.r, theme.textPrimaryColor.g, theme.textPrimaryColor.b, 0.05f);
            WindowShadow = UIFactory.CreateCentered("WindowShadow", root, Vector2.zero, new Vector2(120f, 300f));
            var bar1 = UIFactory.CreateSpriteImage("Bar1", WindowShadow, ProceduralSprites.SoftGlow, new Vector2(1400f, 90f), shadowTint);
            bar1.raycastTarget = false;
            var bar2 = UIFactory.CreateSpriteImage("Bar2", WindowShadow, ProceduralSprites.SoftGlow, new Vector2(90f, 1600f), shadowTint);
            bar2.raycastTarget = false;
            WindowShadowImage = bar1;

            // Optional soft leaf shadow: a couple of small irregular dark blobs near one edge, hinting at
            // foliage outside the frame without ever crossing the play area.
            LeafShadow = UIFactory.CreateCentered("LeafShadow", root, Vector2.zero, new Vector2(-460f, 760f));
            LeafShadowImage = UIFactory.CreateSpriteImage("Blob1", LeafShadow, ProceduralSprites.SoftGlow, new Vector2(220f, 260f), shadowTint);
            LeafShadowImage.raycastTarget = false;
            var blob2 = UIFactory.CreateSpriteImage("Blob2", LeafShadow, ProceduralSprites.SoftGlow, new Vector2(140f, 170f), shadowTint,
                new Vector2(90f, -70f));
            blob2.raycastTarget = false;
        }
    }
}
