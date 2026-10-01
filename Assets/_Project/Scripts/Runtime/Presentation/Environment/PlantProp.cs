using CozyLab.Puzzle.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.Presentation.Environment
{
    /// <summary>
    /// A small desk plant, deliberately built as pot + separate leaf transforms rather than one baked
    /// image, so individual leaves can sway independently (see <see cref="AmbientSceneAnimator"/>) or be
    /// swapped for authored sprites one at a time later.
    /// </summary>
    public sealed class PlantProp : MonoBehaviour
    {
        public RectTransform Pot { get; private set; }
        public Image PotImage { get; private set; }
        public RectTransform[] Leaves { get; private set; }

        public void Build(PuzzleTheme theme, int leafCount = 5)
        {
            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(220f, 260f);

            Pot = UIFactory.CreateRoundedImage("Pot", root, new Vector2(120f, 90f), 20f, theme.potColor,
                new Vector2(0f, -70f)).rectTransform;
            PotImage = Pot.GetComponent<Image>();
            // A slightly darker rim so the pot reads as a distinct vessel, not a flat blob.
            var potRim = UIFactory.CreateRoundedImage("PotRim", root, new Vector2(126f, 22f), 11f,
                Color.Lerp(theme.potColor, Color.black, 0.15f), new Vector2(0f, -28f));
            potRim.raycastTarget = false;

            Leaves = new RectTransform[leafCount];
            for (int i = 0; i < leafCount; i++)
            {
                // Fan the leaves out from the pot's rim, alternating the two leaf tones for variety.
                float spread = leafCount > 1 ? (i / (float)(leafCount - 1) - 0.5f) : 0f;
                float angle = spread * 110f;
                float height = 150f - Mathf.Abs(spread) * 40f;
                var tint = i % 2 == 0 ? theme.leafColor : theme.leafColorAlt;

                var leaf = UIFactory.CreateRoundedImage($"Leaf_{i}", root, new Vector2(46f, height), 23f, tint,
                    new Vector2(0f, -20f));
                leaf.raycastTarget = false;
                // Pivot at the base of the leaf so rotation/sway reads as it bending from the stem.
                leaf.rectTransform.pivot = new Vector2(0.5f, 0f);
                leaf.rectTransform.anchoredPosition = new Vector2(0f, -20f);
                leaf.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                Leaves[i] = leaf.rectTransform;
            }
        }
    }
}
