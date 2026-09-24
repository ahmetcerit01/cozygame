using System;
using CozyLab.Puzzle.Data;
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

    /// <summary>One round "petri dish" button on the level select screen.</summary>
    public sealed class LevelDishView : MonoBehaviour, IPointerClickHandler
    {
        private RectTransform _visual;
        private Image _halo;
        private Image _rim;
        private Image _body;
        private Text _number;
        private GameObject _lockIcon;
        private GameObject _checkBadge;
        private PuzzleTheme _theme;
        private Color _completedColor;
        private float _size;
        private bool _selected;
        private bool _isCurrent;
        private Coroutine _nudgeRoutine;

        public int Index { get; private set; }
        public LevelDishState State { get; private set; }
        public event Action<LevelDishView> Clicked;

        public void Build(int index, float size, PuzzleTheme theme, Color completedColor)
        {
            Index = index;
            _theme = theme;
            _size = size;
            _completedColor = completedColor;

            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(size, size);

            // Generous invisible hit area (covers halo too).
            var hit = UIFactory.AddImage(root, new Color(1f, 1f, 1f, 0f), raycastTarget: true);
            hit.sprite = ProceduralSprites.Circle;

            _visual = UIFactory.CreateCentered("Visual", root, new Vector2(size, size));
            _halo = UIFactory.CreateSpriteImage("Halo", _visual, ProceduralSprites.Ring, new Vector2(size * 1.26f, size * 1.26f),
                theme.accentColor);
            UIFactory.CreateSpriteImage("Shadow", _visual, ProceduralSprites.SoftGlow, new Vector2(size * 1.15f, size * 1.15f),
                new Color(0.1f, 0.18f, 0.22f, 0.22f), new Vector2(0f, -size * 0.06f));
            _rim = UIFactory.CreateSpriteImage("Rim", _visual, ProceduralSprites.Circle, new Vector2(size, size), theme.boardRimColor);
            _body = UIFactory.CreateSpriteImage("Body", _visual, ProceduralSprites.Circle, new Vector2(size * 0.86f, size * 0.86f),
                theme.cardColor);
            UIFactory.CreateSpriteImage("Shine", _visual, ProceduralSprites.SoftGlow, new Vector2(size * 0.42f, size * 0.26f),
                new Color(1f, 1f, 1f, 0.55f), new Vector2(-size * 0.14f, size * 0.22f));

            _number = UIFactory.CreateText("Number", _visual, (index + 1).ToString(), Mathf.RoundToInt(size * 0.34f),
                theme.textPrimaryColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(_number.rectTransform);

            _lockIcon = BuildLock(_visual, size, theme);
            _checkBadge = BuildCheck(_visual, size, theme);
        }

        public void SetState(LevelDishState state, bool isCurrent)
        {
            State = state;
            _isCurrent = isCurrent;
            switch (state)
            {
                case LevelDishState.Locked:
                    _rim.color = Color.Lerp(_theme.wellInnerShadowColor, _theme.backgroundColor, 0.3f);
                    _body.color = _theme.wellColor;
                    _number.enabled = false;
                    break;
                case LevelDishState.Available:
                    _rim.color = _theme.accentColor;
                    _body.color = _theme.cardColor;
                    _number.enabled = true;
                    _number.color = _theme.textPrimaryColor;
                    break;
                case LevelDishState.Completed:
                    _rim.color = Color.Lerp(_completedColor, Color.white, 0.45f);
                    _body.color = _completedColor;
                    _number.enabled = true;
                    _number.color = Color.white;
                    break;
            }
            _lockIcon.SetActive(state == LevelDishState.Locked);
            _checkBadge.SetActive(state == LevelDishState.Completed);
            ApplySelection();
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            ApplySelection();
        }

        /// <summary>"Not yet" wiggle for locked dishes.</summary>
        public void PlayNudge()
        {
            Tween.Stop(this, ref _nudgeRoutine);
            _nudgeRoutine = Tween.Run(this, 0.3f, Ease.Linear,
                t => _visual.anchoredPosition = new Vector2(Mathf.Sin(t * Mathf.PI * 6f) * _size * 0.05f * (1f - t), 0f),
                () => _visual.anchoredPosition = Vector2.zero);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Clicked?.Invoke(this);
        }

        private void Update()
        {
            // The level to play next breathes gently so it's easy to find.
            float pulse = _isCurrent && State == LevelDishState.Available
                ? 1f + 0.035f * Mathf.Sin(Time.unscaledTime * 3.2f)
                : 1f;
            float scale = (_selected ? 1.07f : 1f) * pulse;
            _visual.localScale = Vector3.Lerp(_visual.localScale, new Vector3(scale, scale, 1f),
                1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        }

        private void ApplySelection()
        {
            var c = _theme.accentColor;
            _halo.color = new Color(c.r, c.g, c.b, _selected ? 0.9f : 0f);
        }

        private static GameObject BuildLock(RectTransform parent, float size, PuzzleTheme theme)
        {
            var root = UIFactory.CreateCentered("Lock", parent, Vector2.zero);
            var color = new Color(theme.textSecondaryColor.r, theme.textSecondaryColor.g, theme.textSecondaryColor.b, 0.75f);
            // Shackle (ring) first so the body covers its lower half.
            UIFactory.CreateSpriteImage("Shackle", root, ProceduralSprites.Ring, new Vector2(size * 0.24f, size * 0.26f), color,
                new Vector2(0f, size * 0.07f));
            UIFactory.CreateRoundedImage("Body", root, new Vector2(size * 0.32f, size * 0.24f), size * 0.05f, color,
                new Vector2(0f, -size * 0.07f));
            return root.gameObject;
        }

        private static GameObject BuildCheck(RectTransform parent, float size, PuzzleTheme theme)
        {
            var root = UIFactory.CreateCentered("Check", parent, Vector2.zero, new Vector2(size * 0.32f, -size * 0.32f));
            float badge = size * 0.3f;
            UIFactory.CreateSpriteImage("BadgeShadow", root, ProceduralSprites.SoftGlow, new Vector2(badge * 1.3f, badge * 1.3f),
                new Color(0f, 0f, 0f, 0.18f), new Vector2(0f, -badge * 0.08f));
            UIFactory.CreateSpriteImage("Badge", root, ProceduralSprites.Circle, new Vector2(badge, badge), Color.white);

            // Tick made of two rounded bars.
            float thickness = badge * 0.13f;
            var shortBar = UIFactory.CreateRoundedImage("TickShort", root, new Vector2(badge * 0.28f, thickness), thickness * 0.5f,
                theme.accentColor, new Vector2(-badge * 0.12f, -badge * 0.04f));
            shortBar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            var longBar = UIFactory.CreateRoundedImage("TickLong", root, new Vector2(badge * 0.5f, thickness), thickness * 0.5f,
                theme.accentColor, new Vector2(badge * 0.08f, badge * 0.04f));
            longBar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return root.gameObject;
        }
    }
}
