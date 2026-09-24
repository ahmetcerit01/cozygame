using UnityEngine;

namespace CozyLab.Puzzle.UI
{
    /// <summary>
    /// Keeps a fixed-size design rect centred in its parent and uniformly scales it down when the parent is
    /// smaller (e.g. short/wide screens). Gameplay layout can then use fixed design coordinates.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class FitToParentScaler : MonoBehaviour
    {
        [SerializeField] private Vector2 designSize = new Vector2(1080f, 1600f);
        [SerializeField] private float maxScale = 1f;

        private Vector2 _lastParentSize;

        public void Configure(Vector2 size, float max)
        {
            designSize = size;
            maxScale = max;
            _lastParentSize = Vector2.zero;
            Apply();
        }

        private void OnEnable() => Apply();

        private void LateUpdate()
        {
            var parent = transform.parent as RectTransform;
            if (parent != null && parent.rect.size != _lastParentSize) Apply();
        }

        private void Apply()
        {
            var rt = (RectTransform)transform;
            var parent = rt.parent as RectTransform;
            if (parent == null) return;

            var parentSize = parent.rect.size;
            _lastParentSize = parentSize;

            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = designSize;

            if (parentSize.x <= 0f || parentSize.y <= 0f) return;
            float scale = Mathf.Min(parentSize.x / designSize.x, parentSize.y / designSize.y, maxScale);
            rt.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
