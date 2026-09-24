using UnityEngine;

namespace CozyLab.Puzzle.UI
{
    /// <summary>Keeps a full-screen RectTransform inside Screen.safeArea (notch / Dynamic Island / home bar).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void OnEnable() => Apply(force: true);

        private void Update() => Apply(force: false);

        private void Apply(bool force)
        {
            var safe = Screen.safeArea;
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (!force && safe == _lastSafeArea && screenSize == _lastScreenSize) return;
            if (screenSize.x <= 0 || screenSize.y <= 0) return;

            _lastSafeArea = safe;
            _lastScreenSize = screenSize;

            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / screenSize.x, safe.yMin / screenSize.y);
            rt.anchorMax = new Vector2(safe.xMax / screenSize.x, safe.yMax / screenSize.y);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
