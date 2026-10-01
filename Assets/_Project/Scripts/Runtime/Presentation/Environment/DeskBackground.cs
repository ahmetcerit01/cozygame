using CozyLab.Puzzle.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.Presentation.Environment
{
    /// <summary>
    /// Environment layer 1: the desk surface behind everything else. Currently a procedural warm-wood
    /// gradient (base tone + a soft top-down light fade), deliberately kept low-contrast so it stays quiet
    /// behind the dish and tray. <see cref="Surface"/> is the replaceable slot: a future art pass can assign
    /// an authored wood sprite/texture to it directly, with no other code changes.
    /// </summary>
    public sealed class DeskBackground : MonoBehaviour
    {
        /// <summary>The base desk image. Swap <c>Surface.sprite</c> for an authored texture when it's ready.</summary>
        public Image Surface { get; private set; }

        public void Build(RectTransform parent, PuzzleTheme theme)
        {
            var root = UIFactory.CreateStretched("DeskBackground", parent);
            root.SetAsFirstSibling();

            Surface = UIFactory.AddImage(root, theme.deskWoodColor);

            // Soft light-from-above fade: lighter at the top, settling into the base wood tone lower down.
            // Kept to a modest alpha so the centre (behind the dish/tray) stays visually quiet.
            var lightFade = UIFactory.CreateStretched("LightFade", root);
            var fadeImage = UIFactory.AddImage(lightFade, new Color(
                theme.deskWoodLightColor.r, theme.deskWoodLightColor.g, theme.deskWoodLightColor.b, 0.5f));
            fadeImage.sprite = ProceduralSprites.VerticalFade;
            fadeImage.type = Image.Type.Simple;
            fadeImage.raycastTarget = false;
        }
    }
}
