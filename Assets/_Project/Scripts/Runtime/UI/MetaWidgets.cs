using System;
using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>Procedural "Research" icon: a small round-bottom flask with bubbles.</summary>
    public static class ResearchIcon
    {
        public static RectTransform Create(Transform parent, float size, Color color, Vector2 position = default)
        {
            var root = UIFactory.CreateCentered("ResearchIcon", parent, new Vector2(size, size), position);
            var dark = Color.Lerp(color, new Color(0.15f, 0.2f, 0.28f), 0.35f);
            UIFactory.CreateRoundedImage("Neck", root, new Vector2(size * 0.26f, size * 0.4f), size * 0.06f, color,
                new Vector2(0f, size * 0.18f));
            UIFactory.CreateRoundedImage("Rim", root, new Vector2(size * 0.4f, size * 0.11f), size * 0.05f, dark,
                new Vector2(0f, size * 0.38f));
            UIFactory.CreateSpriteImage("Bulb", root, ProceduralSprites.Circle, new Vector2(size * 0.72f, size * 0.72f), color,
                new Vector2(0f, -size * 0.12f));
            UIFactory.CreateSpriteImage("Shine", root, ProceduralSprites.SoftGlow, new Vector2(size * 0.3f, size * 0.22f),
                new Color(1f, 1f, 1f, 0.7f), new Vector2(-size * 0.13f, -size * 0.02f));
            UIFactory.CreateSpriteImage("Bubble", root, ProceduralSprites.Circle, new Vector2(size * 0.11f, size * 0.11f),
                new Color(1f, 1f, 1f, 0.85f), new Vector2(size * 0.1f, -size * 0.18f));
            UIFactory.CreateSpriteImage("Bubble2", root, ProceduralSprites.Circle, new Vector2(size * 0.07f, size * 0.07f),
                new Color(1f, 1f, 1f, 0.75f), new Vector2(-size * 0.04f, -size * 0.3f));
            return root;
        }
    }

    /// <summary>A small pill showing the Research balance, with a restrained count-up and pop.</summary>
    public sealed class ResearchCounter : MonoBehaviour
    {
        private Text _text;
        private RectTransform _pop;
        private Coroutine _routine;
        private int _shown;

        public int ShownValue => _shown;

        public void Build(Color background, Color iconColor, Color textColor, float height = 88f, int fontSize = 40)
        {
            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(250f, height);
            _pop = UIFactory.CreateCentered("Pop", root, root.sizeDelta);
            UIFactory.CreateShadow("Shadow", _pop, root.sizeDelta, 16f, new Color(0.1f, 0.18f, 0.22f, 0.14f), new Vector2(0f, -6f));
            UIFactory.CreateRoundedImage("Body", _pop, root.sizeDelta, height * 0.5f, background);
            ResearchIcon.Create(_pop, height * 0.72f, iconColor, new Vector2(-root.sizeDelta.x * 0.5f + height * 0.55f, 0f));
            _text = UIFactory.CreateText("Amount", _pop, "0", fontSize, textColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            var rt = _text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(height * 1.05f, 0f);
            rt.offsetMax = new Vector2(-18f, 0f);
        }

        public void SetValue(int value, bool animate)
        {
            Tween.Stop(this, ref _routine);
            if (!animate || !isActiveAndEnabled)
            {
                _shown = value;
                _text.text = value.ToString();
                _pop.localScale = Vector3.one;
                return;
            }

            int from = _shown;
            _routine = Tween.Run(this, 0.55f, Ease.Linear, t =>
            {
                _shown = Mathf.RoundToInt(Mathf.Lerp(from, value, Ease.OutCubic(t)));
                _text.text = _shown.ToString();
                float s = 1f + 0.12f * Mathf.Sin(Mathf.Clamp01(t * 1.6f) * Mathf.PI);
                _pop.localScale = new Vector3(s, s, 1f);
            }, () =>
            {
                _shown = value;
                _text.text = value.ToString();
                _pop.localScale = Vector3.one;
            });
        }
    }

    /// <summary>Tappable scene object (desk item, lab equipment) with a soft press squish.</summary>
    public sealed class TapTarget : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        public event Action Clicked;
        public RectTransform Visual;
        private Coroutine _routine;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Clicked?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData) => Animate(0.95f, 0.08f, Ease.OutQuad);
        public void OnPointerUp(PointerEventData eventData) => Animate(1f, 0.22f, Ease.OutBackSoft);

        /// <summary>Programmatic click (tests / keyboard).</summary>
        public void Invoke() => Clicked?.Invoke();

        private void Animate(float target, float duration, Func<float, float> ease)
        {
            var visual = Visual != null ? Visual : (RectTransform)transform;
            Tween.Stop(this, ref _routine);
            float from = visual.localScale.x;
            _routine = Tween.Run(this, duration, ease, t =>
            {
                float s = Mathf.LerpUnclamped(from, target, t);
                visual.localScale = new Vector3(s, s, 1f);
            });
        }

        /// <summary>Adds a transparent hit area of the given size and a TapTarget to <paramref name="rt"/>.</summary>
        public static TapTarget Attach(RectTransform rt, RectTransform visual)
        {
            var hit = UIFactory.AddImage(rt, new Color(1f, 1f, 1f, 0f), raycastTarget: true);
            hit.sprite = null;
            var target = rt.gameObject.AddComponent<TapTarget>();
            target.Visual = visual;
            return target;
        }
    }

    /// <summary>Short, interruptible screen entrances (input is live immediately).</summary>
    public static class ScreenTransition
    {
        /// <summary>Fades the group in while scaling <paramref name="content"/> from <paramref name="fromScale"/> to 1.</summary>
        public static Coroutine Enter(MonoBehaviour host, CanvasGroup group, RectTransform content, float fromScale,
            float duration = 0.32f)
        {
            group.alpha = 0f;
            group.blocksRaycasts = true;
            content.localScale = new Vector3(fromScale, fromScale, 1f);
            return Tween.Run(host, duration, Ease.Linear, t =>
            {
                group.alpha = Ease.OutCubic(Mathf.Clamp01(t * 1.4f));
                float s = Mathf.LerpUnclamped(fromScale, 1f, Ease.OutCubic(t));
                content.localScale = new Vector3(s, s, 1f);
            });
        }
    }
}
