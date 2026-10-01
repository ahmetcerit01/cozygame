using System.Collections.Generic;
using CozyLab.Puzzle.Data;
using UnityEngine;

namespace CozyLab.Puzzle.Presentation.Environment
{
    /// <summary>
    /// Environment layer 3: reusable decorative lab props, anchored around the edges of the composition so
    /// they never cover the board, pieces, drag paths or buttons — they sit behind the dish and tray in
    /// z-order, so on narrower screens only their edges peek out from behind those, which reads as the prop
    /// living "on the desk around the dish" rather than on top of it.
    /// Not level- or experiment-specific: the same set exists for every level using this theme.
    /// </summary>
    public sealed class LabPropsLayer : MonoBehaviour
    {
        public PlantProp Plant { get; private set; }
        public IReadOnlyList<PropAnchor> Anchors => _anchors;
        private readonly List<PropAnchor> _anchors = new List<PropAnchor>();

        public void Build(RectTransform parent, PuzzleTheme theme)
        {
            var root = UIFactory.CreateStretched("LabProps", parent);

            BuildTweezers(root, theme);
            BuildTestTubeRack(root, theme);
            BuildGlassware(root, theme);
            BuildNotebook(root, theme);
            BuildPaperNote(root, theme);
            BuildPen(root, theme);

            var plantRect = UIFactory.CreateRect("Plant", root);
            plantRect.anchorMin = plantRect.anchorMax = new Vector2(1f, 0f);
            plantRect.pivot = new Vector2(0.5f, 0.5f);
            plantRect.anchoredPosition = new Vector2(-150f, 220f);
            Plant = plantRect.gameObject.AddComponent<PlantProp>();
            Plant.Build(theme);
        }

        // Top-left: small, unobtrusive.
        private void BuildTweezers(RectTransform root, PuzzleTheme theme)
        {
            var anchor = PropAnchor.Create("Tweezers", root, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(90f, -160f), new Vector2(28f, 200f));
            anchor.SetPlaceholder(ProceduralSprites.RoundedRect, theme.metalColor, rotation: -18f);
            _anchors.Add(anchor);
        }

        // Top-right: a rack body with a few tinted "sample" tubes.
        private void BuildTestTubeRack(RectTransform root, PuzzleTheme theme)
        {
            var anchor = PropAnchor.Create("TestTubeRack", root, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-120f, -190f), new Vector2(220f, 140f));
            anchor.SetPlaceholder(ProceduralSprites.RoundedRect, theme.labBenchEdgeColor, new Vector2(220f, 40f));
            _anchors.Add(anchor);

            Color[] tints = { theme.GetPieceColor(0), theme.GetPieceColor(1), theme.GetPieceColor(2) };
            for (int i = 0; i < 3; i++)
            {
                float x = -70f + i * 70f;
                var tube = UIFactory.CreateRoundedImage($"Tube_{i}", anchor.Rect, new Vector2(26f, 110f), 13f,
                    Color.Lerp(theme.boardColor, Color.white, 0.4f), new Vector2(x, 30f));
                tube.raycastTarget = false;
                var liquid = UIFactory.CreateRoundedImage($"Liquid_{i}", anchor.Rect, new Vector2(22f, 50f), 11f,
                    tints[i], new Vector2(x, 8f));
                liquid.raycastTarget = false;
            }
        }

        // Small optional flask near the rack.
        private void BuildGlassware(RectTransform root, PuzzleTheme theme)
        {
            var anchor = PropAnchor.Create("Glassware", root, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-260f, -300f), new Vector2(70f, 80f));
            var body = anchor.SetPlaceholder(ProceduralSprites.RoundedRect,
                Color.Lerp(theme.boardColor, Color.white, 0.3f), new Vector2(70f, 60f));
            body.rectTransform.pivot = new Vector2(0.5f, 0f);
            var fill = UIFactory.CreateRoundedImage("Fill", anchor.Rect, new Vector2(60f, 26f), 10f,
                theme.GetPieceColor(4), new Vector2(0f, -22f));
            fill.raycastTarget = false;
            _anchors.Add(anchor);
        }

        // Bottom-left: a closed notebook, slightly rotated.
        private void BuildNotebook(RectTransform root, PuzzleTheme theme)
        {
            var anchor = PropAnchor.Create("Notebook", root, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(60f, 190f), new Vector2(220f, 160f));
            anchor.SetPlaceholder(ProceduralSprites.RoundedRect, theme.labBenchEdgeColor, rotation: -6f);
            var line1 = UIFactory.CreateRoundedImage("Line1", anchor.Rect, new Vector2(140f, 6f), 3f,
                Color.Lerp(theme.labBenchEdgeColor, Color.white, 0.5f), new Vector2(0f, 20f));
            line1.raycastTarget = false;
            line1.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -6f);
            var line2 = UIFactory.CreateRoundedImage("Line2", anchor.Rect, new Vector2(140f, 6f), 3f,
                Color.Lerp(theme.labBenchEdgeColor, Color.white, 0.5f), new Vector2(-8f, -10f));
            line2.raycastTarget = false;
            line2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -6f);
            _anchors.Add(anchor);
        }

        // Tiny note tucked near the notebook.
        private void BuildPaperNote(RectTransform root, PuzzleTheme theme)
        {
            var anchor = PropAnchor.Create("PaperNote", root, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(70f, 300f), new Vector2(90f, 90f));
            anchor.SetPlaceholder(ProceduralSprites.RoundedRect, Color.Lerp(theme.labBenchEdgeColor, Color.white, 0.65f),
                rotation: 10f);
            var line = UIFactory.CreateRoundedImage("Line", anchor.Rect, new Vector2(56f, 5f), 2.5f,
                theme.labBenchEdgeColor, Vector2.zero);
            line.raycastTarget = false;
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 10f);
            _anchors.Add(anchor);
        }

        // Bottom-right: a pen resting beside the plant.
        private void BuildPen(RectTransform root, PuzzleTheme theme)
        {
            var anchor = PropAnchor.Create("Pen", root, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-40f, 500f), new Vector2(18f, 160f));
            var body = anchor.SetPlaceholder(ProceduralSprites.RoundedRect, theme.metalDarkColor, rotation: 24f);
            var cap = UIFactory.CreateRoundedImage("Cap", anchor.Rect, new Vector2(18f, 34f), 9f, theme.accentColor,
                new Vector2(0f, 63f));
            cap.raycastTarget = false;
            cap.rectTransform.localRotation = body.rectTransform.localRotation;
            _anchors.Add(anchor);
        }
    }
}
