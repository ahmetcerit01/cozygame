using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>Shared setup for full-screen uGUI screens (canvas, background, event system).</summary>
    public static class ScreenCanvas
    {
        public static readonly Vector2 DefaultReferenceResolution = new Vector2(1080f, 1920f);

        /// <summary>Overlay canvas scaled from a portrait reference with 'Expand' so the whole area is always visible.</summary>
        public static RectTransform Create(string name, Transform parent, Vector2 referenceResolution, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = 5; // UI
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
            return (RectTransform)go.transform;
        }

        /// <summary>Theme background with a few big soft blobs for a cozy, out-of-focus lab feel.</summary>
        public static void AddBackground(RectTransform canvasRoot, PuzzleTheme theme)
        {
            var bg = UIFactory.CreateStretched("Background", canvasRoot);
            UIFactory.AddImage(bg, theme.backgroundColor);

            var accent = theme.backgroundAccentColor;
            AddGlow(bg, new Vector2(0.05f, 0.92f), 900f, accent);
            AddGlow(bg, new Vector2(0.98f, 0.55f), 760f, accent);
            AddGlow(bg, new Vector2(0.15f, 0.12f), 820f, accent);
        }

        public static RectTransform AddSafeArea(RectTransform canvasRoot)
        {
            var safe = UIFactory.CreateStretched("SafeArea", canvasRoot);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            return safe;
        }

        public static void EnsureEventSystem()
        {
            var eventSystem = EventSystem.current != null ? EventSystem.current : Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                // Created inactive so the module is fully configured before it enables.
                var go = new GameObject("EventSystem");
                go.SetActive(false);
                eventSystem = go.AddComponent<EventSystem>();
                var module = go.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
                go.SetActive(true);
            }

            // ~1.5 mm of movement before a press becomes a drag, so taps (rotate) are forgiving on touch screens.
            float dpi = Screen.dpi > 0f ? Screen.dpi : 160f;
            eventSystem.pixelDragThreshold = Mathf.Max(8, Mathf.RoundToInt(dpi * 0.06f));
        }

        public static void ApplyCamera(PuzzleTheme theme)
        {
            var camera = Camera.main;
            if (camera == null) return;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = theme.backgroundColor;
        }

        private static void AddGlow(RectTransform parent, Vector2 anchor, float size, Color color)
        {
            var image = UIFactory.CreateSpriteImage("Glow", parent, ProceduralSprites.SoftGlow, new Vector2(size, size), color);
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
