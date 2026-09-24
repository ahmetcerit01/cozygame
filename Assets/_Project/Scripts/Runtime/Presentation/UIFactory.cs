using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.Presentation
{
    /// <summary>Small helpers for building uGUI hierarchies from code.</summary>
    public static class UIFactory
    {
        private static Font _font;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _font = null;

        public static Font DefaultFont
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5; // 5 = UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform CreateCentered(string name, Transform parent, Vector2 size, Vector2 anchoredPosition = default)
        {
            var rt = CreateRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPosition;
            return rt;
        }

        public static RectTransform CreateStretched(string name, Transform parent)
        {
            var rt = CreateRect(name, parent);
            Stretch(rt);
            return rt;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static Image AddImage(RectTransform rt, Color color, bool raycastTarget = false)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        public static Image CreateRoundedImage(string name, Transform parent, Vector2 size, float radius, Color color,
            Vector2 anchoredPosition = default)
        {
            var rt = CreateCentered(name, parent, size, anchoredPosition);
            var image = AddImage(rt, color);
            ProceduralSprites.ApplyRounded(image, radius);
            return image;
        }

        /// <summary>Soft shadow behind a rect of <paramref name="casterSize"/>.</summary>
        public static Image CreateShadow(string name, Transform parent, Vector2 casterSize, float blur, Color color,
            Vector2 anchoredPosition = default)
        {
            float pad = ProceduralSprites.ShadowPadding(blur) * 2f;
            var rt = CreateCentered(name, parent, casterSize + new Vector2(pad, pad), anchoredPosition);
            var image = AddImage(rt, color);
            ProceduralSprites.ApplySoftShadow(image, blur);
            return image;
        }

        public static Image CreateSpriteImage(string name, Transform parent, Sprite sprite, Vector2 size, Color color,
            Vector2 anchoredPosition = default)
        {
            var rt = CreateCentered(name, parent, size, anchoredPosition);
            var image = AddImage(rt, color);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            return image;
        }

        public static Text CreateText(string name, Transform parent, string content, int fontSize, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = CreateRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static void DestroyChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                Object.Destroy(child);
            }
        }
    }
}
