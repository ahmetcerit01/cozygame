using System;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Localization;
using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    public enum LevelDishState
    {
        Locked,
        Available,
        Completed,
    }

    /// <summary>
    /// One level as a glass sample tube standing in the experiment rack: sealed and frosted when locked,
    /// a pale waiting sample when available (the current one glows and wears a NEXT flag), and full of its
    /// cured colour with bubbles once completed. The name is historical — it used to be a round dish.
    /// </summary>
    public sealed class LevelDishView : MonoBehaviour, IPointerClickHandler
    {
        private RectTransform _visual;
        private Image _halo;
        private Image _glass;
        private Image _liquid;
        private Image _liquidTop;
        private Image _cap;
        private Image _capBase;
        private Text _number;
        private GameObject _lockIcon;
        private GameObject _checkBadge;
        private GameObject _bubbles;
        private RectTransform _nextFlag;
        private PuzzleTheme _theme;
        private Color _completedColor;
        private float _width;
        private float _height;
        private bool _selected;
        private bool _isCurrent;
        private Coroutine _nudgeRoutine;

        public int Index { get; private set; }
        public LevelDishState State { get; private set; }
        public event Action<LevelDishView> Clicked;

        /// <param name="size">Tube width in UI units; the tube is about 2.4× as tall.</param>
        public void Build(int index, float size, PuzzleTheme theme, Color completedColor)
        {
            Index = index;
            _theme = theme;
            _completedColor = completedColor;
            _width = size;
            _height = size * 2.4f;
            float w = _width, h = _height;

            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(w * 1.5f, h + w * 0.9f);

            // Generous invisible hit area.
            UIFactory.AddImage(root, new Color(1f, 1f, 1f, 0f), raycastTarget: true);

            _visual = UIFactory.CreateCentered("Visual", root, new Vector2(w, h));
            _visual.pivot = new Vector2(0.5f, 0f);
            _visual.anchoredPosition = new Vector2(0f, -h * 0.5f - w * 0.2f);

            _halo = UIFactory.CreateSpriteImage("Halo", _visual, ProceduralSprites.SoftGlow, new Vector2(w * 2.3f, h * 1.35f),
                Color.clear, new Vector2(0f, h * 0.5f));

            // Glass: edge, body, liquid, shine.
            UIFactory.CreateRoundedImage("GlassEdge", _visual, new Vector2(w, h), w * 0.5f, MetaStyle.GlassEdge, new Vector2(0f, h * 0.5f));
            _glass = UIFactory.CreateRoundedImage("Glass", _visual, new Vector2(w - 10f, h - 10f), w * 0.5f - 5f, MetaStyle.Glass,
                new Vector2(0f, h * 0.5f));

            float liquidW = w - 26f;
            _liquid = UIFactory.CreateRoundedImage("Liquid", _visual, new Vector2(liquidW, h * 0.6f), liquidW * 0.5f, Color.white);
            _liquid.rectTransform.pivot = new Vector2(0.5f, 0f);
            _liquid.rectTransform.anchoredPosition = new Vector2(0f, 13f);
            _liquidTop = UIFactory.CreateRoundedImage("Surface", _liquid.transform, new Vector2(liquidW - 16f, 12f), 6f,
                new Color(1f, 1f, 1f, 0.45f));
            _liquidTop.rectTransform.anchorMin = _liquidTop.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _liquidTop.rectTransform.anchoredPosition = new Vector2(0f, -12f);

            _bubbles = UIFactory.CreateRect("Bubbles", _liquid.transform).gameObject;
            var bubblesRt = (RectTransform)_bubbles.transform;
            bubblesRt.anchorMin = bubblesRt.anchorMax = new Vector2(0.5f, 0f);
            for (int i = 0; i < 4; i++)
            {
                float d = w * (0.1f + (i % 2) * 0.06f);
                UIFactory.CreateSpriteImage("Bubble", bubblesRt, ProceduralSprites.Ring, new Vector2(d, d), new Color(1f, 1f, 1f, 0.75f),
                    new Vector2((i % 2 == 0 ? -1f : 1f) * w * 0.14f, h * (0.1f + i * 0.11f)));
            }

            UIFactory.CreateRoundedImage("Shine", _visual, new Vector2(w * 0.14f, h * 0.62f), w * 0.07f, new Color(1f, 1f, 1f, 0.6f),
                new Vector2(-w * 0.24f, h * 0.55f));

            // Cork / cap.
            _capBase = UIFactory.CreateRoundedImage("CapBase", _visual, new Vector2(w * 1.12f, w * 0.34f), w * 0.12f, Color.white,
                new Vector2(0f, h - w * 0.02f));
            _cap = UIFactory.CreateRoundedImage("Cap", _visual, new Vector2(w * 1.12f, w * 0.3f), w * 0.12f, Color.white,
                new Vector2(0f, h + w * 0.02f));

            // Label sticker with the level number.
            var sticker = UIFactory.CreateCentered("Sticker", _visual, new Vector2(w * 0.86f, w * 0.62f), new Vector2(0f, h * 0.6f));
            UIFactory.CreateRoundedImage("Paper", sticker, new Vector2(w * 0.86f, w * 0.62f), w * 0.14f, MetaStyle.Cream);
            _number = UIFactory.CreateText("Number", sticker, (index + 1).ToString(), Mathf.RoundToInt(w * 0.4f), MetaStyle.Ink,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(_number.rectTransform);

            var lockRt = UIFactory.CreateCentered("Lock", sticker, new Vector2(w * 0.4f, w * 0.4f));
            MetaIcons.Lock(lockRt, w * 0.4f, MetaStyle.InkSoft);
            _lockIcon = lockRt.gameObject;

            var check = UIFactory.CreateCentered("Check", _visual, new Vector2(w * 0.5f, w * 0.5f), new Vector2(w * 0.44f, h * 0.84f));
            UIFactory.CreateSpriteImage("BadgeShadow", check, ProceduralSprites.SoftGlow, new Vector2(w * 0.7f, w * 0.7f),
                new Color(0f, 0f, 0f, 0.18f), new Vector2(0f, -w * 0.04f));
            UIFactory.CreateSpriteImage("Badge", check, ProceduralSprites.Circle, new Vector2(w * 0.5f, w * 0.5f), Color.white);
            var tick = UIFactory.CreateCentered("Tick", check, new Vector2(w * 0.3f, w * 0.3f));
            MetaIcons.Check(tick, w * 0.3f, MetaStyle.Teal);
            _checkBadge = check.gameObject;

            // "Next" flag above the cap for the level to play.
            _nextFlag = UIFactory.CreateCentered("NextFlag", _visual, new Vector2(w * 1.25f, w * 0.44f), new Vector2(0f, h + w * 0.5f));
            UIFactory.CreateRoundedImage("Body", _nextFlag, new Vector2(w * 1.25f, w * 0.44f), w * 0.22f, MetaStyle.Coral);
            var next = UIFactory.CreateText("Text", _nextFlag, string.Empty, Mathf.RoundToInt(w * 0.24f), Color.white, TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UIFactory.Stretch(next.rectTransform);
            next.rectTransform.offsetMin = new Vector2(6f, 0f);
            next.rectTransform.offsetMax = new Vector2(-6f, 0f);
            CureUI.FitLabel(next, next.fontSize); // "SIRADAKİ" is longer than "NEXT"
            next.Localize("experiments_next");

            // Everything above is laid out from the tube's bottom edge.
            foreach (RectTransform child in _visual)
                child.anchorMin = child.anchorMax = new Vector2(0.5f, 0f);
        }

        public void SetState(LevelDishState state, bool isCurrent)
        {
            State = state;
            _isCurrent = isCurrent && state == LevelDishState.Available;
            var pale = Color.Lerp(_completedColor, Color.white, 0.6f);
            var capDark = Color.Lerp(_completedColor, new Color(0.25f, 0.2f, 0.18f), 0.35f);

            switch (state)
            {
                case LevelDishState.Locked:
                    _glass.color = Color.Lerp(MetaStyle.Glass, MetaStyle.Locked, 0.55f);
                    _liquid.gameObject.SetActive(false);
                    _cap.color = MetaStyle.Locked;
                    _capBase.color = Color.Lerp(MetaStyle.Locked, MetaStyle.InkSoft, 0.35f);
                    break;
                case LevelDishState.Available:
                    _glass.color = MetaStyle.Glass;
                    _liquid.gameObject.SetActive(true);
                    _liquid.color = pale;
                    _liquid.rectTransform.sizeDelta = new Vector2(_liquid.rectTransform.sizeDelta.x, _height * 0.5f);
                    _bubbles.SetActive(false);
                    _cap.color = MetaStyle.Teal;
                    _capBase.color = MetaStyle.TealDark;
                    break;
                case LevelDishState.Completed:
                    _glass.color = MetaStyle.Glass;
                    _liquid.gameObject.SetActive(true);
                    _liquid.color = _completedColor;
                    _liquid.rectTransform.sizeDelta = new Vector2(_liquid.rectTransform.sizeDelta.x, _height * 0.8f);
                    _bubbles.SetActive(true);
                    _cap.color = Color.Lerp(_completedColor, Color.white, 0.15f);
                    _capBase.color = capDark;
                    break;
            }
            _number.enabled = state != LevelDishState.Locked;
            _lockIcon.SetActive(state == LevelDishState.Locked);
            _checkBadge.SetActive(state == LevelDishState.Completed);
            _nextFlag.gameObject.SetActive(_isCurrent);
            ApplySelection();
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            ApplySelection();
        }

        /// <summary>"Not yet" wiggle for locked samples.</summary>
        public void PlayNudge()
        {
            Tween.Stop(this, ref _nudgeRoutine);
            _nudgeRoutine = Tween.Run(this, 0.3f, Ease.Linear,
                t => _visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 6f) * 6f * (1f - t)),
                () => _visual.localRotation = Quaternion.identity);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Clicked?.Invoke(this);
        }

        private void Update()
        {
            // The level to play next breathes gently so it's easy to find; the selected one lifts slightly.
            float pulse = _isCurrent ? 1f + 0.03f * Mathf.Sin(Time.unscaledTime * 3.2f) : 1f;
            float scale = (_selected ? 1.08f : 1f) * pulse;
            _visual.localScale = Vector3.Lerp(_visual.localScale, new Vector3(scale, scale, 1f),
                1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            if (_isCurrent && _nextFlag != null)
                _nextFlag.anchoredPosition = new Vector2(0f, _height + _width * 0.5f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.2f)) * 8f);
        }

        private void ApplySelection()
        {
            var c = _isCurrent || State == LevelDishState.Available ? MetaStyle.Teal : State == LevelDishState.Completed ? _completedColor : MetaStyle.InkSoft;
            _halo.color = new Color(c.r, c.g, c.b, _selected ? 0.55f : _isCurrent ? 0.25f : 0f);
        }
    }
}
