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

            var text = UIFactory.CreateText("Label", rt, label, fontSize, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(text.rectTransform);

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

        public static void SetLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<Text>();
            if (text != null) text.text = label;
        }
    }
}
