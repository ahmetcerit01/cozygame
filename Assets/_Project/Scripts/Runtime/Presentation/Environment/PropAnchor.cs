using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.Presentation.Environment
{
    /// <summary>
    /// One reusable decorative-prop slot: a positioned anchor plus a placeholder visual. Not level-specific —
    /// the same anchor exists for every experiment/level using this theme.
    /// <see cref="Visual"/> is the replaceable slot: assign an authored sprite to it later (typically via
    /// <see cref="SetArt"/>) without touching layout or gameplay code.
    /// </summary>
    public sealed class PropAnchor : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        public Image Visual { get; private set; }

        public static PropAnchor Create(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            var anchor = go.AddComponent<PropAnchor>();
            anchor.Rect = rect;
            return anchor;
        }

        /// <summary>Builds the placeholder look: a single tinted silhouette. Call once after <see cref="Create"/>.</summary>
        public Image SetPlaceholder(Sprite sprite, Color tint, Vector2? size = null, float rotation = 0f)
        {
            Visual = UIFactory.CreateSpriteImage("Visual", Rect, sprite, size ?? Rect.sizeDelta, tint);
            Visual.raycastTarget = false;
            Visual.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            return Visual;
        }

        /// <summary>Swaps in an authored sprite once available, keeping this anchor's position/size.</summary>
        public void SetArt(Sprite sprite, Vector2? size = null)
        {
            if (Visual == null) return;
            Visual.sprite = sprite;
            Visual.type = Image.Type.Simple;
            if (size.HasValue) Visual.rectTransform.sizeDelta = size.Value;
        }
    }
}
