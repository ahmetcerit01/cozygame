using CozyLab.Puzzle.Localization;
using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>Rounded "pill" buttons with a soft shadow and press squish, shared by all screens.</summary>
    public static class UIButtons
    {
        public static Button CreatePill(string name, Transform parent, string label, Vector2 size, Vector2 position,
            Color color, Color textColor, int fontSize = 44, bool shadow = true)
        {
            var rt = UIFactory.CreateCentered(name, parent, size, position);
            if (shadow)
                UIFactory.CreateShadow("Shadow", rt, size, 22f, new Color(0.1f, 0.18f, 0.22f, 0.18f), new Vector2(0f, -10f));

            var body = UIFactory.CreateRoundedImage("Body", rt, size, size.y * 0.5f, color);
            body.raycastTarget = true;

            // Restrained top sheen + bottom shade so pills read as tactile/pressable rather than flat UI
            // chrome. Both use a fixed white/black tint (not the pill's own color) so the cue still shows on
            // pale buttons, where a same-color highlight would blend away to nothing.
            var sheen = UIFactory.CreateRoundedImage("Sheen", rt, new Vector2(size.x * 0.82f, size.y * 0.34f),
                size.y * 0.17f, new Color(1f, 1f, 1f, 0.22f), new Vector2(0f, size.y * 0.2f));
            sheen.raycastTarget = false;
            var shade = UIFactory.CreateRoundedImage("Shade", rt, new Vector2(size.x * 0.86f, size.y * 0.32f),
                size.y * 0.16f, new Color(0f, 0f, 0f, 0.06f), new Vector2(0f, -size.y * 0.24f));
            shade.raycastTarget = false;

            var text = UIFactory.CreateText("Label", rt, label, fontSize, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(12f, 0f);
            text.rectTransform.offsetMax = new Vector2(-12f, 0f);
            CureUI.FitLabel(text, fontSize); // longer translations shrink instead of clipping

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = body;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            // Don't keep keyboard/gamepad selection highlight after a tap.
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            rt.gameObject.AddComponent<PressScale>();
            return button;
        }

        /// <summary>Shows a literal (non-localized) label.</summary>
        public static void SetLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<Text>();
            if (text != null) text.SetLiteral(label);
        }

        /// <summary>Shows a localized label that follows language changes.</summary>
        public static void SetLabelKey(Button button, string key, params object[] args)
        {
            var text = button.GetComponentInChildren<Text>();
            if (text != null) text.Localize(key, args);
        }

        public static string GetLabel(Button button)
        {
            var text = button != null ? button.GetComponentInChildren<Text>() : null;
            return text != null ? text.text : string.Empty;
        }
    }
}
