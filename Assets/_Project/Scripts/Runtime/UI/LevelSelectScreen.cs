using System;
using System.Collections.Generic;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Presentation;
using CozyLab.Puzzle.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>
    /// Simple level picker: a grid of dishes (locked / available / completed), an info line for the selected
    /// level and a PLAY button. Reads progression; never writes it.
    /// </summary>
    public sealed class LevelSelectScreen : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080f, 1920f);
        [SerializeField] private float headerHeight = 330f;
        [SerializeField] private float footerHeight = 360f;
        [SerializeField] private Vector2 gridDesignSize = new Vector2(1080f, 1180f);
        [SerializeField] private int columns = 3;
        [SerializeField] private float dishSize = 210f;
        [SerializeField] private float columnSpacing = 300f;
        [SerializeField] private float rowSpacing = 270f;

        private LevelCatalog _catalog;
        private LevelProgression _progression;
        private PuzzleTheme _theme;
        private GameObject _root;
        private readonly List<LevelDishView> _dishes = new List<LevelDishView>();
        private Text _progressText;
        private Text _selectedTitle;
        private Text _selectedInfo;
        private Button _playButton;
        private CanvasGroup _playGroup;
        private RectTransform _milestone;
        private CanvasGroup _milestoneGroup;
        private RectTransform _milestoneCard;
        private UIParticles _particles;
        private Coroutine _milestoneRoutine;
        private int _selectedIndex;

        /// <summary>Player wants to play this (unlocked) level.</summary>
        public event Action<int> PlayRequested;
        /// <summary>Development-only long-press on the title.</summary>
        public event Action DevResetRequested;

        public int SelectedIndex => _selectedIndex;
        public bool IsVisible => _root != null && _root.activeSelf;
        public bool IsMilestoneVisible => _milestone != null && _milestone.gameObject.activeSelf;
        public IReadOnlyList<LevelDishView> Dishes => _dishes;

        public void Initialize(LevelCatalog catalog, LevelProgression progression, PuzzleTheme theme)
        {
            _catalog = catalog;
            _progression = progression;
            _theme = theme;
            Build();
            _progression.Changed += Refresh;
        }

        private void OnDestroy()
        {
            if (_progression != null) _progression.Changed -= Refresh;
        }

        public void Show(int selectIndex, bool showMilestone = false)
        {
            ScreenCanvas.ApplyCamera(_theme);
            _root.SetActive(true);
            Refresh();
            Select(Mathf.Clamp(selectIndex, 0, _catalog.Count - 1));
            if (showMilestone) ShowMilestone();
            else HideMilestone();
        }

        public void Hide()
        {
            HideMilestone();
            _root.SetActive(false);
        }

        /// <summary>Selects a dish; tapping an already selected, playable dish plays it.</summary>
        public void Select(int index)
        {
            _selectedIndex = index;
            for (int i = 0; i < _dishes.Count; i++) _dishes[i].SetSelected(i == index);

            var level = _catalog.GetLevel(index);
            bool playable = _progression.CanPlay(index);
            bool completed = _progression.IsCompleted(index);

            _selectedTitle.text = $"LEVEL {index + 1}  ·  {(level != null ? level.Title : "?")}";
            if (!playable)
                _selectedInfo.text = $"Locked — cure Level {index} to unlock";
            else if (level != null)
                _selectedInfo.text = $"{level.Pieces.Count} samples  ·  {level.Width}×{level.Height} dish" +
                                     (completed ? "  ·  cured" : string.Empty);

            _playButton.interactable = playable;
            _playGroup.alpha = playable ? 1f : 0.45f;
            UIButtons.SetLabel(_playButton, !playable ? "LOCKED" : completed ? "REPLAY" : "PLAY");
        }

        public void Refresh()
        {
            if (_root == null) return;
            int current = _progression.CurrentLevelIndex;
            for (int i = 0; i < _dishes.Count; i++)
            {
                var state = _progression.IsCompleted(i) ? LevelDishState.Completed
                    : _progression.IsUnlocked(i) ? LevelDishState.Available
                    : LevelDishState.Locked;
                _dishes[i].SetState(state, i == current);
            }
            _progressText.text = $"{_progression.CompletedCount} / {_progression.LevelCount} cured";
            if (_selectedIndex >= 0 && _selectedIndex < _dishes.Count) Select(_selectedIndex);
        }

        // ---------------------------------------------------------------- build

        private void Build()
        {
            _root = new GameObject("LevelSelect");
            _root.transform.SetParent(transform, false);

            var canvasRoot = ScreenCanvas.Create("LevelSelectCanvas", _root.transform, referenceResolution, sortingOrder: 0);
            ScreenCanvas.AddBackground(canvasRoot, _theme);
            var safe = ScreenCanvas.AddSafeArea(canvasRoot);

            BuildHeader(safe);
            BuildGrid(safe);
            BuildFooter(safe);

            BuildMilestone(canvasRoot);
            _particles = UIFactory.CreateStretched("Particles", canvasRoot).gameObject.AddComponent<UIParticles>();
            _particles.Initialize(40);

            _root.SetActive(false);
        }

        private void BuildHeader(RectTransform safe)
        {
            var header = UIFactory.CreateRect("Header", safe);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, headerHeight);

            var chapter = UIFactory.CreateText("Chapter", header, _catalog.ChapterTitle.ToUpperInvariant(), 34,
                _theme.accentColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Band(chapter.rectTransform, 1f, 0.76f);

            var title = UIFactory.CreateText("Title", header, _catalog.ChapterSubtitle, 76, _theme.textPrimaryColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Band(title.rectTransform, 0.76f, 0.4f);

            if (Debug.isDebugBuild)
            {
                // Development builds / Editor only: hold the title for 3 s to reset progress.
                title.raycastTarget = true;
                var longPress = title.gameObject.AddComponent<LongPressHandler>();
                longPress.Duration = 3f;
                longPress.LongPressed += () => DevResetRequested?.Invoke();
            }

            _progressText = UIFactory.CreateText("Progress", header, string.Empty, 36, _theme.textSecondaryColor);
            Band(_progressText.rectTransform, 0.4f, 0.16f);
        }

        private void BuildGrid(RectTransform safe)
        {
            var area = UIFactory.CreateStretched("GridArea", safe);
            area.offsetMin = new Vector2(0f, footerHeight);
            area.offsetMax = new Vector2(0f, -headerHeight);

            var content = UIFactory.CreateRect("Grid", area);
            content.gameObject.AddComponent<FitToParentScaler>().Configure(gridDesignSize, 1f);

            int count = _catalog.Count;
            // Full rows first; a leftover last level (e.g. the chapter finale) is centred and slightly bigger.
            int fullRows = count / columns;
            int remainder = count % columns;
            int rows = fullRows + (remainder > 0 ? 1 : 0);
            float top = (rows - 1) * rowSpacing * 0.5f;

            for (int i = 0; i < count; i++)
            {
                int row = i / columns;
                int col = i % columns;
                int inRow = row < fullRows ? columns : remainder;
                float x = (col - (inRow - 1) * 0.5f) * columnSpacing;
                float y = top - row * rowSpacing;
                bool finale = remainder == 1 && i == count - 1;
                float size = finale ? dishSize * 1.2f : dishSize;

                var rt = UIFactory.CreateCentered($"Dish_{i + 1}", content, new Vector2(size, size), new Vector2(x, y));
                var dish = rt.gameObject.AddComponent<LevelDishView>();
                dish.Build(i, size, _theme, _theme.GetPieceColor(i));
                dish.Clicked += OnDishClicked;
                _dishes.Add(dish);
            }
        }

        private void BuildFooter(RectTransform safe)
        {
            var footer = UIFactory.CreateRect("Footer", safe);
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.sizeDelta = new Vector2(0f, footerHeight);

            _selectedTitle = UIFactory.CreateText("SelectedTitle", footer, string.Empty, 44, _theme.textPrimaryColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Band(_selectedTitle.rectTransform, 1f, 0.8f);
            _selectedInfo = UIFactory.CreateText("SelectedInfo", footer, string.Empty, 32, _theme.textSecondaryColor);
            Band(_selectedInfo.rectTransform, 0.8f, 0.63f);

            _playButton = UIButtons.CreatePill("PlayButton", footer, "PLAY", new Vector2(560f, 150f), new Vector2(0f, 0f),
                _theme.accentColor, Color.white, 52);
            var rt = (RectTransform)_playButton.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 120f);
            _playGroup = _playButton.gameObject.AddComponent<CanvasGroup>();
            _playButton.onClick.AddListener(() =>
            {
                if (_progression.CanPlay(_selectedIndex)) PlayRequested?.Invoke(_selectedIndex);
            });
        }

        private void BuildMilestone(RectTransform canvasRoot)
        {
            _milestone = UIFactory.CreateStretched("Milestone", canvasRoot);
            UIFactory.AddImage(_milestone, _theme.overlayDimColor, raycastTarget: true);
            _milestoneGroup = _milestone.gameObject.AddComponent<CanvasGroup>();
            var safe = ScreenCanvas.AddSafeArea(_milestone);

            var size = new Vector2(880f, 760f);
            _milestoneCard = UIFactory.CreateCentered("Card", safe, size);
            UIFactory.CreateShadow("Shadow", _milestoneCard, size, 60f, new Color(0f, 0f, 0f, 0.25f), new Vector2(0f, -24f));
            UIFactory.CreateRoundedImage("Body", _milestoneCard, size, 84f, _theme.cardColor);
            UIFactory.CreateSpriteImage("Glow", _milestoneCard, ProceduralSprites.SoftGlow, new Vector2(700f, 420f),
                new Color(_theme.comboPerfectColor.r, _theme.comboPerfectColor.g, _theme.comboPerfectColor.b, 0.3f),
                new Vector2(0f, 190f));

            var chapter = UIFactory.CreateText("Chapter", _milestoneCard, _catalog.ChapterTitle.ToUpperInvariant(), 34,
                _theme.textSecondaryColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(chapter.rectTransform, new Vector2(800f, 60f), new Vector2(0f, 280f));

            var title = UIFactory.CreateText("Title", _milestoneCard, _catalog.CompletionTitle, 78, _theme.comboPerfectColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(title.rectTransform, new Vector2(840f, 200f), new Vector2(0f, 170f));

            var message = UIFactory.CreateText("Message", _milestoneCard, _catalog.CompletionMessage, 38,
                _theme.textPrimaryColor);
            Place(message.rectTransform, new Vector2(760f, 160f), new Vector2(0f, 10f));

            var cont = UIButtons.CreatePill("ContinueButton", _milestoneCard, "CONTINUE", new Vector2(520f, 140f),
                new Vector2(0f, -230f), _theme.accentColor, Color.white);
            cont.onClick.AddListener(HideMilestone);

            _milestone.gameObject.SetActive(false);
        }

        private void ShowMilestone()
        {
            Tween.Stop(this, ref _milestoneRoutine);
            _milestone.gameObject.SetActive(true);
            _milestoneGroup.alpha = 0f;
            _milestoneCard.localScale = Vector3.one * 0.7f;
            _milestoneRoutine = Tween.Run(this, 0.55f, Ease.Linear, t =>
            {
                _milestoneGroup.alpha = Ease.OutCubic(Mathf.Clamp01(t * 1.6f));
                float s = Mathf.LerpUnclamped(0.7f, 1f, Ease.OutBack(t));
                _milestoneCard.localScale = new Vector3(s, s, 1f);
            }, null, 0.15f);

            // A small celebratory fizz of bubbles in every sample colour.
            for (int i = 0; i < 6; i++)
            {
                _particles.BubbleBurst(_milestoneCard.position, _theme.GetPieceColor(i), 5, 520f, 36f, 220f);
            }
        }

        private void HideMilestone()
        {
            Tween.Stop(this, ref _milestoneRoutine);
            if (_milestone != null) _milestone.gameObject.SetActive(false);
        }

        private void OnDishClicked(LevelDishView dish)
        {
            if (!_progression.CanPlay(dish.Index))
            {
                dish.PlayNudge();
                Select(dish.Index);
                return;
            }
            if (dish.Index == _selectedIndex)
            {
                PlayRequested?.Invoke(dish.Index);
                return;
            }
            Select(dish.Index);
        }

        private static void Band(RectTransform rt, float top, float bottom)
        {
            rt.anchorMin = new Vector2(0f, bottom);
            rt.anchorMax = new Vector2(1f, top);
            rt.offsetMin = new Vector2(40f, 0f);
            rt.offsetMax = new Vector2(-40f, 0f);
        }

        private static void Place(RectTransform rt, Vector2 size, Vector2 position)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }
    }
}
