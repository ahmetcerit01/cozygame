using System;
using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>Authored UI icons (Assets/Art/UI/Icons). Serialized on GameFlow; shared through <see cref="CureUI.Icons"/>.</summary>
    [Serializable]
    public struct HomeIcons
    {
        public Sprite coin;
        public Sprite research;
        public Sprite add;
        public Sprite settings;
        public Sprite experiments;
        public Sprite lab;
        public Sprite daily;
        public Sprite shop;
    }

    /// <summary>
    /// Lifts the authored Home art so the hero group (clipboard + dish) sits higher and the lower third is free for
    /// UI. Scales the cover-fitted art about the screen's bottom edge — uniform, never stretched, cropping only the
    /// top and sides — just enough for the dish to clear the bottom UI, but never so much that the clipboard runs
    /// under the top status bar. Adapts to each aspect ratio.
    /// </summary>
    public sealed class HomeArtFramer : MonoBehaviour
    {
        // In the authored plate (fractions of its height, from the bottom).
        private const float DishBottom = 0.288f;
        private const float ClipboardTop = 0.843f;
        private const float MaxLift = 1.15f;

        private RectTransform _safe;
        private float _aspect;
        private float _bottomUi;
        private float _topUi;
        private Vector3 _lastKey;

        public float Scale { get; private set; } = 1f;

        /// <param name="bottomUi">Height above the safe bottom the dish should clear.</param>
        /// <param name="topUi">Height below the safe top reserved for the status bar.</param>
        public void Configure(RectTransform safe, float aspect, float bottomUi, float topUi)
        {
            _safe = safe;
            _aspect = aspect;
            _bottomUi = bottomUi;
            _topUi = topUi;
            _lastKey = Vector3.zero;
        }

        private void LateUpdate()
        {
            var frame = (RectTransform)transform;
            var parent = frame.parent as RectTransform;
            if (parent == null || _safe == null) return;
            var size = parent.rect.size;
            var key = new Vector3(size.x, size.y, _safe.anchorMin.y * 1000f + _safe.anchorMax.y);
            if (key == _lastKey || size.x <= 0f || size.y <= 0f) return;
            _lastKey = key;

            float artHeight = Mathf.Max(size.y, size.x / _aspect);
            float artBottom = (size.y - artHeight) * 0.5f; // cover-fit art is centred
            float safeBottom = _safe.anchorMin.y * size.y;
            float safeTop = (1f - _safe.anchorMax.y) * size.y;

            float dishY = artBottom + DishBottom * artHeight;
            float clipY = artBottom + ClipboardTop * artHeight;
            float wanted = (safeBottom + _bottomUi) / Mathf.Max(1f, dishY);
            float limit = (size.y - safeTop - _topUi) / Mathf.Max(1f, clipY);
            Scale = Mathf.Clamp(wanted, 1f, Mathf.Clamp(limit, 1f, MaxLift));
            frame.localScale = new Vector3(Scale, Scale, 1f);
        }
    }
}
