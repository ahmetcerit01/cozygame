using System;
using CozyLab.Puzzle.Localization;
using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>
    /// Shared tactile press for kit buttons and cards. Press: scale to ~0.96, the face sinks onto its base,
    /// the drop shadow tucks in and the surface darkens slightly. Release: smooth spring back with a tiny
    /// overshoot. Skipped while the button is not interactable.
    /// </summary>
    public sealed class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private RectTransform _face;
        private RectTransform _shadow;
        private Image _shadowImage;
        private Vector2 _shadowRest;
        private float _shadowAlpha;
        private Graphic[] _tinted = Array.Empty<Graphic>();
        private float _depth;
        private Selectable _selectable;
        private Coroutine _routine;
        private float _amount; // 0 = rest, 1 = fully pressed
        private bool _pressed;

        public bool IsPressed => _pressed;

        /// <param name="face">Moves down by <paramref name="depth"/> while pressed (null: no movement).</param>
        /// <param name="shadow">Drop shadow that tucks in while pressed (optional).</param>
        /// <param name="tinted">Surfaces that darken slightly while pressed.</param>
        public void Configure(RectTransform face, float depth, Image shadow, params Graphic[] tinted)
        {
            _face = face;
            _depth = depth;
            _shadowImage = shadow;
            _shadow = shadow != null ? shadow.rectTransform : null;
            _shadowRest = _shadow != null ? _shadow.anchoredPosition : Vector2.zero;
            _shadowAlpha = shadow != null ? shadow.color.a : 0f;
            _tinted = tinted ?? Array.Empty<Graphic>();
            _selectable = GetComponent<Selectable>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.IsInteractable()) return;
            _pressed = true;
            AnimateTo(1f, CureTokens.PressDuration, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();
        public void OnPointerExit(PointerEventData eventData) => Release();

        private void OnDisable()
        {
            _pressed = false;
            _routine = null;
            Apply(0f);
        }

        private void Release()
        {
            if (!_pressed) return;
            _pressed = false;
            AnimateTo(0f, CureTokens.ReleaseDuration, Ease.OutBackSoft);
        }

        private void AnimateTo(float target, float duration, Func<float, float> ease)
        {
            Tween.Stop(this, ref _routine);
            if (!isActiveAndEnabled)
            {
                Apply(target);
                return;
            }
            float from = _amount;
            _routine = Tween.Run(this, duration, ease, t => Apply(Mathf.LerpUnclamped(from, target, t)), () => Apply(target));
        }

        private void Apply(float amount)
        {
            _amount = amount;
            float s = Mathf.LerpUnclamped(1f, CureTokens.PressScale, amount);
            transform.localScale = new Vector3(s, s, 1f);
            if (_face != null) _face.anchoredPosition = new Vector2(0f, -_depth * amount);
            if (_shadow != null)
            {
                _shadow.anchoredPosition = _shadowRest + new Vector2(0f, _depth * 0.6f * amount);
                var c = _shadowImage.color;
                c.a = _shadowAlpha * (1f - 0.45f * Mathf.Clamp01(amount));
                _shadowImage.color = c;
            }
            float tint = Mathf.Lerp(1f, CureTokens.PressTint, Mathf.Clamp01(amount));
            foreach (var graphic in _tinted)
            {
                if (graphic != null) graphic.canvasRenderer.SetColor(new Color(tint, tint, tint, 1f));
            }
        }
    }

    /// <summary>Coral notification dot ("!" or a count) with a slow, small heartbeat.</summary>
    public sealed class NotificationBadge : MonoBehaviour
    {
        private Text _text;
        private RectTransform _visual;

        public bool IsVisible => gameObject.activeSelf;

        internal void Build(float size)
        {
            var root = (RectTransform)transform;
            _visual = UIFactory.CreateCentered("Visual", root, new Vector2(size, size));
            UIFactory.CreateSpriteImage("Shadow", _visual, ProceduralSprites.SoftGlow, new Vector2(size * 1.3f, size * 0.7f),
                CureTokens.ShadowSoft, new Vector2(0f, -size * 0.36f));
            UIFactory.CreateSpriteImage("Rim", _visual, ProceduralSprites.Circle, new Vector2(size, size), CureTokens.Ivory);
            UIFactory.CreateSpriteImage("Base", _visual, ProceduralSprites.Circle, new Vector2(size * 0.84f, size * 0.84f),
                CureTokens.CoralDeep, new Vector2(0f, -size * 0.03f));
            UIFactory.CreateSpriteImage("Face", _visual, ProceduralSprites.Circle, new Vector2(size * 0.78f, size * 0.78f),
                CureTokens.Coral, new Vector2(0f, size * 0.01f));
            UIFactory.CreateSpriteImage("Gloss", _visual, ProceduralSprites.SoftGlow, new Vector2(size * 0.44f, size * 0.22f),
                new Color(1f, 1f, 1f, 0.7f), new Vector2(0f, size * 0.2f));
            _text = UIFactory.CreateText("Mark", _visual, "!", Mathf.RoundToInt(size * 0.56f), Color.white, TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UIFactory.Stretch(_text.rectTransform);
        }

        /// <summary>0 shows "!", otherwise the number (capped at 99+).</summary>
        public void SetCount(int count) => _text.text = count <= 0 ? "!" : count > 99 ? "99+" : count.ToString();

        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        private void Update()
        {
            float t = Time.unscaledTime % 2.2f;
            float s = t < 0.5f ? 1f + 0.12f * Mathf.Sin(t / 0.5f * Mathf.PI) : 1f;
            _visual.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Inset warm track with a glossy teal fill and an optional value label ("9 / 10").</summary>
    public sealed class CureProgressBar : MonoBehaviour
    {
        private RectTransform _fill;
        private Text _label;
        private float _width;
        private float _height;

        public float Value { get; private set; }
        public string LabelText => _label != null ? _label.text : string.Empty;

        internal void Build(Vector2 size, bool withLabel)
        {
            var root = (RectTransform)transform;
            float trackWidth = withLabel ? size.x - 130f : size.x;
            var track = UIFactory.CreateCentered("Track", root, new Vector2(trackWidth, size.y),
                new Vector2(-(size.x - trackWidth) * 0.5f, 0f));
            UIFactory.CreateRoundedImage("Rim", track, new Vector2(trackWidth + 6f, size.y + 6f), (size.y + 6f) * 0.5f,
                new Color(1f, 0.95f, 0.85f, 0.35f));
            UIFactory.CreateRoundedImage("Groove", track, new Vector2(trackWidth, size.y), size.y * 0.5f, CureTokens.Track);
            UIFactory.CreateRoundedImage("GrooveShade", track, new Vector2(trackWidth - 8f, size.y * 0.35f), size.y * 0.17f,
                new Color(0f, 0f, 0f, 0.18f), new Vector2(0f, size.y * 0.25f));

            _width = trackWidth - 6f;
            _height = size.y - 6f;
            _fill = UIFactory.CreateRoundedImage("Fill", track, new Vector2(_height, _height), _height * 0.5f, CureTokens.Teal).rectTransform;
            _fill.anchorMin = _fill.anchorMax = _fill.pivot = new Vector2(0f, 0.5f);
            _fill.anchoredPosition = new Vector2(3f, 0f);
            var gloss = UIFactory.CreateRoundedImage("Gloss", _fill, Vector2.zero, _height * 0.2f, CureTokens.TealGloss).rectTransform;
            gloss.anchorMin = new Vector2(0f, 0.55f);
            gloss.anchorMax = new Vector2(1f, 0.55f);
            gloss.sizeDelta = new Vector2(-_height * 0.5f, _height * 0.4f);
            gloss.anchoredPosition = Vector2.zero;

            if (withLabel)
            {
                _label = UIFactory.CreateText("Label", root, string.Empty, Mathf.RoundToInt(Mathf.Max(26f, size.y * 1.1f)),
                    CureTokens.TitleLight, TextAnchor.MiddleRight, FontStyle.Bold);
                MetaStyle.Place(_label.rectTransform, new Vector2(120f, size.y + 20f), new Vector2(size.x * 0.5f - 60f, 0f));
                CureUI.TitleEffects(_label, 2f);
            }
            SetValue(0f);
        }

        public void SetValue(float value)
        {
            Value = Mathf.Clamp01(value);
            _fill.gameObject.SetActive(Value > 0f);
            _fill.sizeDelta = new Vector2(Mathf.Max(_height, _width * Value), _height);
        }

        /// <summary>Sets fill and the "done / total" label together.</summary>
        public void SetProgress(int done, int total)
        {
            SetValue(total > 0 ? (float)done / total : 0f);
            if (_label != null) _label.SetLiteral($"{Loc.Number(done)} / {Loc.Number(total)}");
        }
    }

    /// <summary>
    /// Modal panel: warm dimmer, a raised parchment card with a title and a round close button, and a
    /// <see cref="Content"/> area for the caller. Pops in with a short spring; tapping the dimmer closes it.
    /// </summary>
    public sealed class ModalPanel : MonoBehaviour
    {
        private CanvasGroup _group;
        private RectTransform _card;
        private Text _title;
        private Coroutine _routine;

        public RectTransform Content { get; private set; }
        public Button CloseButton { get; private set; }
        public bool IsOpen => gameObject.activeSelf;
        public string TitleText => _title != null ? _title.text : string.Empty;

        public event Action Closed;

        internal void Build(Vector2 size)
        {
            var root = (RectTransform)transform;
            _group = gameObject.AddComponent<CanvasGroup>();
            UIFactory.AddImage(root, CureTokens.Dim, raycastTarget: true);
            var dimButton = gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.navigation = new Navigation { mode = Navigation.Mode.None };
            dimButton.onClick.AddListener(Close);

            var safe = ScreenCanvas.AddSafeArea(root);
            _card = UIFactory.CreateCentered("Card", safe, size);
            var face = CureUI.ContentCard("Panel", _card, size, Vector2.zero, CureTokens.RadiusPanel);
            face.GetComponent<Image>().raycastTarget = true; // taps on the card don't close it

            _title = UIFactory.CreateText("Title", face, string.Empty, CureTokens.HeaderSize, CureTokens.Text, TextAnchor.MiddleCenter,
                FontStyle.Bold);
            MetaStyle.Place(_title.rectTransform, new Vector2(size.x - 200f, 80f), new Vector2(0f, size.y * 0.5f - 72f));
            CureUI.Emboss(_title);
            UIFactory.CreateRoundedImage("TitleRule", face, new Vector2(size.x * 0.5f, 4f), 2f,
                new Color(CureTokens.Caramel.r, CureTokens.Caramel.g, CureTokens.Caramel.b, 0.35f), new Vector2(0f, size.y * 0.5f - 122f));

            CloseButton = CureUI.IconButton("CloseButton", face, 84f, MetaIcons.Close);
            var closeRt = (RectTransform)CloseButton.transform;
            closeRt.anchoredPosition = new Vector2(size.x * 0.5f - 62f, size.y * 0.5f - 62f);
            CloseButton.onClick.AddListener(Close);

            Content = UIFactory.CreateRect("Content", face);
            Content.anchorMin = Vector2.zero;
            Content.anchorMax = Vector2.one;
            Content.offsetMin = new Vector2(40f, 40f);
            Content.offsetMax = new Vector2(-40f, -140f);
            gameObject.SetActive(false);
        }

        public void SetTitle(string key) => _title.Localize(key);

        public void Open()
        {
            Tween.Stop(this, ref _routine);
            gameObject.SetActive(true);
            _group.alpha = 0f;
            _card.localScale = Vector3.one * 0.86f;
            _routine = Tween.Run(this, 0.32f, Ease.Linear, t =>
            {
                _group.alpha = Ease.OutCubic(Mathf.Clamp01(t * 1.8f));
                float s = Mathf.LerpUnclamped(0.86f, 1f, Ease.OutBackSoft(t));
                _card.localScale = new Vector3(s, s, 1f);
            });
        }

        public void Close()
        {
            Tween.Stop(this, ref _routine);
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            Closed?.Invoke();
        }
    }

    /// <summary>Small cream card showing a reward: icon, "+25" and a label ("Research").</summary>
    public sealed class RewardCard : MonoBehaviour
    {
        private Text _amount;
        private Text _label;

        public string AmountText => _amount != null ? _amount.text : string.Empty;

        internal void Build(Vector2 size, Sprite icon, string labelKey)
        {
            var face = CureUI.ContentCard("Card", transform, size, Vector2.zero, CureTokens.RadiusSmall + 8f);
            float iconSize = size.y * 0.5f;
            UIFactory.CreateSpriteImage("IconShadow", face, ProceduralSprites.SoftGlow, new Vector2(iconSize * 0.9f, iconSize * 0.25f),
                CureTokens.ShadowSoft, new Vector2(-size.x * 0.22f, size.y * 0.02f));
            CureUI.SpriteIcon(face, "Icon", icon, iconSize, new Vector2(-size.x * 0.22f, size.y * 0.16f));
            _amount = UIFactory.CreateText("Amount", face, string.Empty, Mathf.RoundToInt(size.y * 0.3f), CureTokens.Text,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            MetaStyle.Place(_amount.rectTransform, new Vector2(size.x * 0.55f, size.y * 0.42f), new Vector2(size.x * 0.16f, size.y * 0.14f));
            CureUI.Emboss(_amount);
            _label = UIFactory.CreateText("Label", face, string.Empty, Mathf.RoundToInt(size.y * 0.16f), CureTokens.TextSoft,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            MetaStyle.Place(_label.rectTransform, new Vector2(size.x - 30f, size.y * 0.24f), new Vector2(0f, -size.y * 0.3f));
            _label.Localize(labelKey);
        }

        public void SetAmount(int amount) => _amount.SetLiteral("+" + Loc.Number(amount));
    }

    /// <summary>Screen header: optional round back button on the left and a light, outlined title over the artwork.</summary>
    public sealed class ScreenHeader : MonoBehaviour
    {
        public Text Title { get; internal set; }
        public Button Back { get; internal set; }
    }

    /// <summary>A soft diagonal highlight that glides across a (masked) button every few seconds.</summary>
    public sealed class ShineSweep : MonoBehaviour
    {
        private RectTransform _shine;
        private float _travel;
        private float _period;
        private float _t;

        public void Configure(RectTransform shine, float halfTravel, float period)
        {
            _shine = shine;
            _travel = halfTravel;
            _period = period;
            _t = period * 0.6f;
        }

        private void Update()
        {
            if (_shine == null) return;
            _t += Time.unscaledDeltaTime;
            if (_t > _period) _t -= _period;
            const float sweep = 0.75f; // seconds the highlight is visible
            float p = Mathf.Clamp01(_t / sweep);
            _shine.anchoredPosition = new Vector2(Mathf.Lerp(-_travel, _travel, Ease.InOutCubic(p)), 0f);
            _shine.gameObject.SetActive(_t < sweep);
        }
    }

    /// <summary>
    /// Compact currency pill: warm glass body, authored icon overhanging the left edge, the amount, and an
    /// optional small "+" action. Counts up softly when the value changes.
    /// </summary>
    public sealed class CurrencyPill : MonoBehaviour
    {
        private Text _amount;
        private RectTransform _pop;
        private Coroutine _routine;

        public int ShownValue { get; private set; }
        public string ShownText => _amount != null ? _amount.text : string.Empty;
        /// <summary>The "+" action, or null when the pill has none.</summary>
        public Button Plus { get; private set; }

        internal void Build(string name, float width, Sprite icon, Sprite plusIcon)
        {
            const float h = 78f;
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(width, h);
            _pop = UIFactory.CreateCentered("Pop", rt, new Vector2(width, h));
            CureUI.GlassBody(_pop, new Vector2(width, h), round: false);

            UIFactory.CreateSpriteImage("IconShadow", _pop, ProceduralSprites.SoftGlow, new Vector2(84f, 30f),
                new Color(0.12f, 0.06f, 0.02f, 0.4f), new Vector2(-width * 0.5f + 30f, -38f));
            CureUI.SpriteIcon(_pop, "Icon", icon, 100f, new Vector2(-width * 0.5f + 28f, 2f));

            _amount = UIFactory.CreateText("Amount", _pop, "0", 40, CureTokens.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            var art = _amount.rectTransform;
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.offsetMin = new Vector2(92f, 0f);
            art.offsetMax = new Vector2(plusIcon != null ? -64f : -18f, 0f);
            CureUI.Emboss(_amount);

            if (plusIcon != null)
            {
                var plusRt = UIFactory.CreateCentered(name + "Plus", _pop, new Vector2(78f, 78f), new Vector2(width * 0.5f - 22f, 0f));
                var shadow = UIFactory.CreateSpriteImage("Shadow", plusRt, ProceduralSprites.SoftGlow, new Vector2(84f, 40f),
                    new Color(0.1f, 0.05f, 0.02f, 0.4f), new Vector2(0f, -30f));
                var face = CureUI.SpriteIcon(plusRt, "Icon", plusIcon, 78f, Vector2.zero);
                face.raycastTarget = true;
                Plus = plusRt.gameObject.AddComponent<Button>();
                Plus.targetGraphic = face;
                Plus.transition = Selectable.Transition.None;
                Plus.navigation = new Navigation { mode = Navigation.Mode.None };
                plusRt.gameObject.AddComponent<PressFeedback>().Configure(null, 0f, shadow, face);
            }
        }

        public void SetValue(int value, bool animate)
        {
            Tween.Stop(this, ref _routine);
            if (!animate || !isActiveAndEnabled || value == ShownValue)
            {
                ShownValue = value;
                _amount.text = Loc.Number(value);
                _pop.localScale = Vector3.one;
                return;
            }
            int from = ShownValue;
            _routine = Tween.Run(this, 0.55f, Ease.Linear, t =>
            {
                ShownValue = Mathf.RoundToInt(Mathf.Lerp(from, value, Ease.OutCubic(t)));
                _amount.text = Loc.Number(ShownValue);
                float s = 1f + 0.1f * Mathf.Sin(Mathf.Clamp01(t * 1.6f) * Mathf.PI);
                _pop.localScale = new Vector3(s, s, 1f);
            }, () =>
            {
                ShownValue = value;
                _amount.text = Loc.Number(value);
                _pop.localScale = Vector3.one;
            });
        }

        private void OnEnable() => Loc.LanguageChanged += RefreshNumber;
        private void OnDisable() => Loc.LanguageChanged -= RefreshNumber;
        private void RefreshNumber() => _amount.text = Loc.Number(ShownValue); // 1,240 ↔ 1.240
    }
}
