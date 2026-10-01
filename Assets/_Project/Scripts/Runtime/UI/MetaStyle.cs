using System;
using CozyLab.Puzzle.Localization;
using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>
    /// Front-end style system for the meta screens (Home, Experiments, Lab): a warm cream / soft teal / olive /
    /// wood palette and the reusable building blocks every meta screen is made of — tactile buttons with a
    /// visible base, icon buttons, cards, chips, progress bars and top bars. Gameplay keeps its own look.
    /// </summary>
    public static class MetaStyle
    {
        // ---------------------------------------------------------------- palette

        public static readonly Color Cream = Hex(0xFBF5E9);
        public static readonly Color CreamDeep = Hex(0xF2E6CF);
        public static readonly Color CreamEdge = Hex(0xDCC7A4);
        public static readonly Color Paper = Hex(0xF5ECDD);
        public static readonly Color Ink = Hex(0x4A3D33);
        public static readonly Color InkSoft = Hex(0x8F7C69);
        public static readonly Color Teal = Hex(0x44A89A);
        public static readonly Color TealDark = Hex(0x2F7F74);
        public static readonly Color TealLight = Hex(0xCFEAE3);
        public static readonly Color Olive = Hex(0x97A35A);
        public static readonly Color OliveDark = Hex(0x6E7A3B);
        public static readonly Color Wood = Hex(0xC9955F);
        public static readonly Color WoodDark = Hex(0x9A6A40);
        public static readonly Color WoodLight = Hex(0xE2BC8D);
        public static readonly Color Coral = Hex(0xEE7E5E);
        public static readonly Color CoralDark = Hex(0xC45E41);
        public static readonly Color Glass = Hex(0xE3F2EE);
        public static readonly Color GlassEdge = Hex(0xB9D3CD);
        public static readonly Color Locked = Hex(0xD9CFBF);
        public static readonly Color WarmShadow = new Color(0.30f, 0.20f, 0.10f, 0.22f);
        public static readonly Color Charcoal = Hex(0x2F3437);
        public static readonly Color Petrol = Hex(0x2C5561);
        public static readonly Color Ivory = Hex(0xFCF6EA);
        public static readonly Color IvoryEdge = Hex(0xD8C29E);
        public static readonly Color Gold = Hex(0xF2BE4E);
        public static readonly Color GoldDark = Hex(0xC98E22);

        /// <summary>Height reserved at the bottom of every meta screen for the shared tab bar.</summary>
        public const float NavBarHeight = 196f;

        public enum ButtonKind
        {
            Primary,    // teal, the one main action on a screen
            Secondary,  // cream, supporting actions
            Accent,     // olive, positive/upgrade actions
        }

        // ---------------------------------------------------------------- buttons

        /// <summary>
        /// Tactile button: a darker base under a raised face that presses down on touch. The label is the only
        /// Text under it, so <see cref="UIButtons.SetLabel"/> keeps working.
        /// </summary>
        public static Button Button(string name, Transform parent, string labelKey, Vector2 size, Vector2 position,
            ButtonKind kind = ButtonKind.Primary, int fontSize = 0)
        {
            GetButtonColors(kind, out var face, out var baseColor, out var text, out var textShadow);
            float depth = Mathf.Round(Mathf.Clamp(size.y * 0.09f, 7f, 14f));
            float radius = Mathf.Min(size.y * 0.5f, 46f);

            var rt = UIFactory.CreateCentered(name, parent, size, position);
            var dropShadow = UIFactory.CreateShadow("Shadow", rt, size, 20f, WarmShadow, new Vector2(0f, -depth - 6f));
            UIFactory.CreateRoundedImage("Base", rt, size, radius, baseColor, new Vector2(0f, -depth));

            var faceRt = UIFactory.CreateCentered("Face", rt, size);
            var body = UIFactory.CreateRoundedImage("Body", faceRt, size, radius, face);
            body.rectTransform.anchoredPosition = Vector2.zero;
            body.raycastTarget = true;
            UIFactory.CreateRoundedImage("Sheen", faceRt, new Vector2(size.x - radius * 1.4f, size.y * 0.16f), size.y * 0.08f,
                new Color(1f, 1f, 1f, kind == ButtonKind.Secondary ? 0.5f : 0.16f), new Vector2(0f, size.y * 0.3f));

            var t = UIFactory.CreateText("Label", faceRt, string.Empty, fontSize > 0 ? fontSize : Mathf.RoundToInt(size.y * 0.34f), text,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(16f, 0f);
            t.rectTransform.offsetMax = new Vector2(-16f, 0f);
            CureUI.FitLabel(t, t.fontSize);
            if (!string.IsNullOrEmpty(labelKey)) t.Localize(labelKey);
            var shadow = t.gameObject.AddComponent<Shadow>();
            shadow.effectColor = textShadow;
            shadow.effectDistance = new Vector2(0f, -3f);

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = body;
            ConfigureColors(button);
            button.transition = Selectable.Transition.None; // PressFeedback tints instead
            rt.gameObject.AddComponent<PressFeedback>().Configure(faceRt, depth * 0.7f, dropShadow, body);
            return button;
        }

        /// <summary>Round tactile icon button with an optional caption under it and an optional badge dot.</summary>
        public static Button IconButton(string name, Transform parent, float size, Vector2 position,
            Action<RectTransform, float, Color> icon, string captionKey = null, ButtonKind kind = ButtonKind.Secondary)
        {
            GetButtonColors(kind, out var face, out var baseColor, out var iconColor, out _);
            float depth = Mathf.Round(size * 0.08f);
            var rt = UIFactory.CreateCentered(name, parent, new Vector2(size, size), position);
            var dropShadow = UIFactory.CreateSpriteImage("Shadow", rt, ProceduralSprites.SoftGlow, new Vector2(size * 1.3f, size * 1.1f),
                WarmShadow, new Vector2(0f, -depth - size * 0.12f));
            UIFactory.CreateSpriteImage("Base", rt, ProceduralSprites.Circle, new Vector2(size, size), baseColor, new Vector2(0f, -depth));
            var faceRt = UIFactory.CreateCentered("Face", rt, new Vector2(size, size));
            var body = UIFactory.CreateSpriteImage("Body", faceRt, ProceduralSprites.Circle, new Vector2(size, size), face);
            body.raycastTarget = true;
            UIFactory.CreateSpriteImage("Sheen", faceRt, ProceduralSprites.SoftGlow, new Vector2(size * 0.6f, size * 0.3f),
                new Color(1f, 1f, 1f, 0.55f), new Vector2(-size * 0.1f, size * 0.24f));
            var iconRt = UIFactory.CreateCentered("Icon", faceRt, new Vector2(size * 0.56f, size * 0.56f));
            icon?.Invoke(iconRt, size * 0.56f, iconColor == Color.white ? Color.white : TealDark);

            if (!string.IsNullOrEmpty(captionKey))
            {
                var label = UIFactory.CreateText("Caption", rt, string.Empty, Mathf.RoundToInt(Mathf.Clamp(size * 0.2f, 22f, 30f)), Ink,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                Place(label.rectTransform, new Vector2(size * 1.6f, 40f), new Vector2(0f, -size * 0.5f - depth - 26f));
                var outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(Cream.r, Cream.g, Cream.b, 0.8f);
                outline.effectDistance = new Vector2(2f, -2f);
                label.Localize(captionKey);
            }

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = body;
            ConfigureColors(button);
            button.transition = Selectable.Transition.None;
            rt.gameObject.AddComponent<PressFeedback>().Configure(faceRt, depth * 0.7f, dropShadow, body);
            return button;
        }

        /// <summary>Greys out a button (still raycastable so taps can explain why) without hiding it.</summary>
        public static void SetAvailable(Button button, bool available)
        {
            button.interactable = available;
            var group = button.GetComponent<CanvasGroup>();
            if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
            group.alpha = available ? 1f : 0.5f;
        }

        /// <summary>Small round notification dot (coral with "!").</summary>
        public static RectTransform Badge(Transform parent, float size, Vector2 position)
        {
            var rt = UIFactory.CreateCentered("Badge", parent, new Vector2(size, size), position);
            UIFactory.CreateSpriteImage("Ring", rt, ProceduralSprites.Circle, new Vector2(size, size), Cream);
            UIFactory.CreateSpriteImage("Dot", rt, ProceduralSprites.Circle, new Vector2(size * 0.8f, size * 0.8f), Coral);
            var t = UIFactory.CreateText("Mark", rt, "!", Mathf.RoundToInt(size * 0.6f), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(t.rectTransform);
            return rt;
        }

        // ---------------------------------------------------------------- surfaces

        /// <summary>Thick cream card: soft shadow, a darker edge showing below the face (reads as a physical tile).</summary>
        public static RectTransform Card(string name, Transform parent, Vector2 size, Vector2 position, float radius = 52f,
            Color? face = null, Color? edge = null)
        {
            var rt = UIFactory.CreateCentered(name, parent, size, position);
            UIFactory.CreateShadow("Shadow", rt, size, 34f, WarmShadow, new Vector2(0f, -18f));
            UIFactory.CreateRoundedImage("Edge", rt, size, radius, edge ?? CreamEdge, new Vector2(0f, -10f));
            UIFactory.CreateRoundedImage("Face", rt, size, radius, face ?? Cream);
            return rt;
        }

        /// <summary>Pill label ("EXPERIMENT 01", "LAB LEVEL 2", "NEXT").</summary>
        public static Text Tag(string name, Transform parent, string text, Vector2 size, Vector2 position, Color background,
            Color textColor, int fontSize = 26)
        {
            var rt = UIFactory.CreateCentered(name, parent, size, position);
            UIFactory.CreateRoundedImage("Body", rt, size, size.y * 0.5f, background);
            var t = UIFactory.CreateText("Text", rt, text, fontSize, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(t.rectTransform);
            return t;
        }

        /// <summary>Research balance chip styled for the meta screens.</summary>
        public static ResearchCounter ResearchChip(Transform parent, Vector2 anchor, Vector2 position, float height = 92f)
        {
            var rt = UIFactory.CreateRect("Research", parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = position;
            var counter = rt.gameObject.AddComponent<ResearchCounter>();
            counter.Build(Cream, Teal, Ink, height, 40);
            return counter;
        }

        /// <summary>Top band of a meta screen (anchored to the safe area top).</summary>
        public static RectTransform TopBand(Transform safe, float height)
        {
            var header = UIFactory.CreateRect("Header", safe);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, height);
            header.anchoredPosition = Vector2.zero;
            return header;
        }

        /// <summary>Warm paper backdrop shared by Experiments and Lab (cream wall, soft light from the top).</summary>
        public static void WarmBackground(RectTransform canvasRoot, Color? baseColor = null)
        {
            var bg = UIFactory.CreateStretched("Background", canvasRoot);
            UIFactory.AddImage(bg, baseColor ?? Paper);
            var light = UIFactory.CreateStretched("TopLight", bg);
            light.anchorMin = new Vector2(0f, 0.45f);
            UIFactory.AddImage(light, new Color(1f, 0.99f, 0.95f, 0.75f)).sprite = ProceduralSprites.VerticalFade;
            Glow(bg, new Vector2(0.1f, 0.95f), 1000f, new Color(1f, 0.93f, 0.78f, 0.55f));
            Glow(bg, new Vector2(0.95f, 0.35f), 900f, new Color(0.83f, 0.92f, 0.87f, 0.45f));
            Glow(bg, new Vector2(0.1f, 0.05f), 900f, new Color(0.93f, 0.85f, 0.72f, 0.5f));
        }

        /// <summary>Gives a subtree its own sorting (e.g. a sheet or card that must cover the tab bar).</summary>
        public static void RaiseSorting(GameObject go, int sortingOrder)
        {
            var canvas = go.GetComponent<Canvas>();
            if (canvas == null) canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
        }

        public static void Place(RectTransform rt, Vector2 size, Vector2 position)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        private static void Glow(RectTransform parent, Vector2 anchor, float size, Color color)
        {
            var image = UIFactory.CreateSpriteImage("Glow", parent, ProceduralSprites.SoftGlow, new Vector2(size, size), color);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = anchor;
            image.rectTransform.anchoredPosition = Vector2.zero;
        }

        private static void GetButtonColors(ButtonKind kind, out Color face, out Color baseColor, out Color text, out Color textShadow)
        {
            switch (kind)
            {
                case ButtonKind.Secondary:
                    face = Cream; baseColor = CreamEdge; text = Ink; textShadow = new Color(1f, 1f, 1f, 0f);
                    break;
                case ButtonKind.Accent:
                    face = Olive; baseColor = OliveDark; text = Color.white; textShadow = new Color(OliveDark.r, OliveDark.g, OliveDark.b, 0.6f);
                    break;
                default:
                    face = Teal; baseColor = TealDark; text = Color.white; textShadow = new Color(TealDark.r, TealDark.g, TealDark.b, 0.7f);
                    break;
            }
        }

        private static void ConfigureColors(Button button)
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.94f, 0.94f, 0.94f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
        }
    }

    /// <summary>Rounded progress bar (inset track + teal fill).</summary>
    public sealed class MetaProgressBar : MonoBehaviour
    {
        private RectTransform _fill;
        private float _width;
        private float _height;

        public float Value { get; private set; }

        public static MetaProgressBar Create(Transform parent, Vector2 size, Vector2 position, Color? fill = null)
        {
            var rt = UIFactory.CreateCentered("ProgressBar", parent, size, position);
            UIFactory.CreateRoundedImage("Track", rt, size, size.y * 0.5f, MetaStyle.CreamDeep);
            UIFactory.CreateRoundedImage("TrackShade", rt, new Vector2(size.x - 6f, size.y * 0.4f), size.y * 0.2f,
                new Color(0.45f, 0.33f, 0.2f, 0.08f), new Vector2(0f, size.y * 0.22f));
            var bar = rt.gameObject.AddComponent<MetaProgressBar>();
            bar._width = size.x;
            bar._height = size.y;
            var fillImage = UIFactory.CreateRoundedImage("Fill", rt, new Vector2(size.y, size.y - 6f), (size.y - 6f) * 0.5f,
                fill ?? MetaStyle.Teal);
            bar._fill = fillImage.rectTransform;
            bar._fill.anchorMin = bar._fill.anchorMax = bar._fill.pivot = new Vector2(0f, 0.5f);
            bar._fill.anchoredPosition = new Vector2(3f, 0f);
            var shine = UIFactory.CreateRoundedImage("Shine", bar._fill, Vector2.zero, (size.y - 6f) * 0.15f,
                new Color(1f, 1f, 1f, 0.35f)).rectTransform;
            shine.anchorMin = new Vector2(0f, 0.64f);
            shine.anchorMax = new Vector2(1f, 0.64f);
            shine.sizeDelta = new Vector2(-(size.y - 6f) * 0.6f, (size.y - 6f) * 0.3f);
            shine.anchoredPosition = Vector2.zero;
            bar.SetValue(0f);
            return bar;
        }

        public void SetValue(float value)
        {
            Value = Mathf.Clamp01(value);
            _fill.gameObject.SetActive(Value > 0f);
            float inner = _width - 6f;
            _fill.sizeDelta = new Vector2(Mathf.Max(_height - 6f, inner * Value), _height - 6f);
        }
    }
}
