using System;
using System.Collections.Generic;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Localization;
using CozyLab.Puzzle.Presentation;
using CozyLab.Puzzle.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>
    /// The experiment's sample rack: every level is a sample tube on a wooden rack (locked / available / cured),
    /// with a detail card for the selected sample and a PLAY button. Reads progression; never writes it.
    /// </summary>
    public sealed class LevelSelectScreen : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080f, 1920f);
        private const float HeaderHeight = 300f;
        private const int Columns = 5;
        private const float TubeWidth = 124f;
        private const float ColumnSpacing = 196f;
        private const float RowSpacing = 500f;

        private const float DetailCardHeight = 250f;

        private LevelCatalog _catalog;
        private LevelProgression _progression;
        private PuzzleTheme _theme;
        private GameObject _root;
        private CanvasGroup _group;
        private RectTransform _content;
        private readonly List<LevelDishView> _dishes = new List<LevelDishView>();
        private Text _progressText;
        private MetaProgressBar _progressBar;
        private Text _selectedTitle;
        private Text _selectedInfo;
        private Button _playButton;
        private CanvasGroup _playGroup;
        private RectTransform _milestone;
        private CanvasGroup _milestoneGroup;
        private RectTransform _milestoneCard;
        private UIParticles _particles;
        private Coroutine _milestoneRoutine;
        private Coroutine _enter;
        private RectTransform _milestoneBonus;
        private Text _milestoneBonusText;
        private int _selectedIndex;

        /// <summary>Player wants to play this (unlocked) level.</summary>
        public event Action<int> PlayRequested;
        /// <summary>Development-only long-press on the title.</summary>
        public event Action DevResetRequested;
        /// <summary>Top-left "ALL" button: the list of all experiments.</summary>
        public event Action BackRequested;
        /// <summary>Experiment-complete card navigation.</summary>
        public event Action MilestoneHomeRequested;
        public event Action MilestoneLabRequested;

        public int SelectedIndex => _selectedIndex;
        public bool IsVisible => _root != null && _root.activeSelf;
        public bool IsMilestoneVisible => _milestone != null && _milestone.gameObject.activeSelf;
        public IReadOnlyList<LevelDishView> Dishes => _dishes;
        public string ProgressText => _progressText != null ? _progressText.text : string.Empty;

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

        /// <param name="milestoneResearch">Research granted for completing the experiment (0 hides the reward line).</param>
        public void Show(int selectIndex, bool showMilestone = false, int milestoneResearch = 0)
        {
            ScreenCanvas.ApplyCamera(_theme);
            bool wasVisible = _root.activeSelf;
            _root.SetActive(true);
            Refresh();
            Select(Mathf.Clamp(selectIndex, 0, _catalog.Count - 1));
            if (showMilestone) ShowMilestone(milestoneResearch);
            else HideMilestone();
            if (!wasVisible)
            {
                Tween.Stop(this, ref _enter);
                _enter = ScreenTransition.Enter(this, _group, _content, 0.98f);
            }
        }

        public void Hide()
        {
            HideMilestone();
            _root.SetActive(false);
        }

        /// <summary>Selects a sample; tapping an already selected, playable sample plays it.</summary>
        public void Select(int index)
        {
            _selectedIndex = index;
            for (int i = 0; i < _dishes.Count; i++) _dishes[i].SetSelected(i == index);

            var level = _catalog.GetLevel(index);
            bool playable = _progression.CanPlay(index);
            bool completed = _progression.IsCompleted(index);

            _selectedTitle.Localize("experiments_level_title", index + 1,
                level != null ? Loc.LevelTitle(level.LevelId, level.Title) : new LocString(null, "?"));
            if (!playable)
                _selectedInfo.Localize("experiments_level_locked", index);
            else if (level != null)
                _selectedInfo.Localize(completed ? "experiments_level_info_cured" : "experiments_level_info", level.Pieces.Count,
                    level.Width, level.Height);

            _playButton.interactable = playable;
            _playGroup.alpha = playable ? 1f : 0.45f;
            UIButtons.SetLabelKey(_playButton, !playable ? "experiments_locked" : completed ? "experiments_replay" : "experiments_play");
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
            _progressText.Localize("experiments_progress", _progression.CompletedCount, _progression.LevelCount);
            _progressBar.SetValue(_progression.LevelCount > 0 ? (float)_progression.CompletedCount / _progression.LevelCount : 0f);
            if (_selectedIndex >= 0 && _selectedIndex < _dishes.Count) Select(_selectedIndex);
        }

        // ---------------------------------------------------------------- build

        private void Build()
        {
            _root = new GameObject("LevelSelect");
            _root.transform.SetParent(transform, false);

            var canvasRoot = ScreenCanvas.Create("LevelSelectCanvas", _root.transform, referenceResolution, sortingOrder: 0);
            _group = canvasRoot.gameObject.AddComponent<CanvasGroup>();
            MetaStyle.WarmBackground(canvasRoot);
            var safe = ScreenCanvas.AddSafeArea(canvasRoot);
            _content = safe;

            BuildHeader(safe);
            BuildRack(safe);
            BuildFooter(safe);

            BuildMilestone(canvasRoot);
            _particles = UIFactory.CreateStretched("Particles", canvasRoot).gameObject.AddComponent<UIParticles>();
            _particles.Initialize(40);
            MetaStyle.RaiseSorting(_particles.gameObject, 21);

            _root.SetActive(false);
        }

        private void BuildHeader(RectTransform safe)
        {
            var header = MetaStyle.TopBand(safe, HeaderHeight);

            // All experiments (the journal).
            var all = MetaStyle.IconButton("AllExperimentsButton", header, 112f, Vector2.zero, MetaIcons.Grid, "experiments_all");
            var allRt = (RectTransform)all.transform;
            allRt.anchorMin = allRt.anchorMax = new Vector2(0f, 1f);
            allRt.anchoredPosition = new Vector2(100f, -84f);
            all.onClick.AddListener(() => BackRequested?.Invoke());

            var chapterTag = MetaStyle.Tag("Chapter", header, string.Empty,
                new Vector2(300f, 54f), Vector2.zero, MetaStyle.TealLight, MetaStyle.TealDark, 25);
            chapterTag.Localize(Loc.ChapterTitle(_catalog.ChapterId, _catalog.ChapterTitle), TextCase.Upper);
            var chapterRt = (RectTransform)chapterTag.transform.parent;
            chapterRt.anchorMin = chapterRt.anchorMax = new Vector2(0.5f, 1f);
            chapterRt.anchoredPosition = new Vector2(0f, -52f);

            var title = UIFactory.CreateText("Title", header, string.Empty, 70, MetaStyle.Ink, TextAnchor.MiddleCenter,
                FontStyle.Bold);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(700f, 90f);
            titleRt.anchoredPosition = new Vector2(0f, -130f);
            CureUI.FitLabel(title, 70);
            title.Localize(Loc.ChapterSubtitle(_catalog.ChapterId, _catalog.ChapterSubtitle));

            if (Debug.isDebugBuild)
            {
                // Development builds / Editor only: hold the title for 3 s to reset progress.
                title.raycastTarget = true;
                var longPress = title.gameObject.AddComponent<LongPressHandler>();
                longPress.Duration = 3f;
                longPress.LongPressed += () => DevResetRequested?.Invoke();
            }

            _progressBar = MetaProgressBar.Create(header, new Vector2(520f, 28f), Vector2.zero);
            var barRt = (RectTransform)_progressBar.transform;
            barRt.anchorMin = barRt.anchorMax = new Vector2(0.5f, 1f);
            barRt.anchoredPosition = new Vector2(-70f, -222f);
            _progressText = UIFactory.CreateText("Progress", header, string.Empty, 30, MetaStyle.TealDark, TextAnchor.MiddleLeft,
                FontStyle.Bold);
            var progressRt = _progressText.rectTransform;
            progressRt.anchorMin = progressRt.anchorMax = new Vector2(0.5f, 1f);
            progressRt.sizeDelta = new Vector2(200f, 44f);
            progressRt.anchoredPosition = new Vector2(300f, -222f);
        }

        private void BuildRack(RectTransform safe)
        {
            float footer = MetaStyle.NavBarHeight + DetailCardHeight + 40f;
            var area = UIFactory.CreateStretched("RackArea", safe);
            area.offsetMin = new Vector2(0f, footer);
            area.offsetMax = new Vector2(0f, -HeaderHeight);

            int count = _catalog.Count;
            int rows = Mathf.Max(1, (count + Columns - 1) / Columns);
            var content = UIFactory.CreateRect("Rack", area);
            content.gameObject.AddComponent<FitToParentScaler>().Configure(new Vector2(1080f, rows * RowSpacing + 20f), 1.15f);

            float top = (rows - 1) * RowSpacing * 0.5f;
            float tubeHeight = TubeWidth * 2.4f;
            float tubeBottom = -(tubeHeight * 0.5f + TubeWidth * 0.2f); // relative to a tube root's centre (see LevelDishView)

            for (int row = 0; row < rows; row++)
            {
                float y = top - row * RowSpacing;
                int inRow = Mathf.Min(Columns, count - row * Columns);
                float rackWidth = (inRow - 1) * ColumnSpacing + 190f;

                // Back board, then tubes, then the front rail and shelf plank in front of them.
                UIFactory.CreateRoundedImage($"Backboard_{row}", content, new Vector2(rackWidth + 60f, tubeHeight + 70f), 36f,
                    new Color(MetaStyle.WoodLight.r, MetaStyle.WoodLight.g, MetaStyle.WoodLight.b, 0.28f),
                    new Vector2(0f, y + tubeBottom + tubeHeight * 0.5f + 10f));

                var tubes = new List<LevelDishView>();
                for (int col = 0; col < inRow; col++)
                {
                    int i = row * Columns + col;
                    float x = (col - (inRow - 1) * 0.5f) * ColumnSpacing;
                    var rt = UIFactory.CreateCentered($"Dish_{i + 1}", content, Vector2.zero, new Vector2(x, y));
                    var dish = rt.gameObject.AddComponent<LevelDishView>();
                    dish.Build(i, TubeWidth, _theme, _theme.GetPieceColor(i));
                    dish.Clicked += OnDishClicked;
                    _dishes.Add(dish);
                }

                float railY = y + tubeBottom + tubeHeight * 0.2f;
                UIFactory.CreateShadow($"RailShadow_{row}", content, new Vector2(rackWidth, 40f), 14f, MetaStyle.WarmShadow,
                    new Vector2(0f, railY - 8f));
                UIFactory.CreateRoundedImage($"Rail_{row}", content, new Vector2(rackWidth, 40f), 14f, MetaStyle.Wood, new Vector2(0f, railY));
                UIFactory.CreateRoundedImage($"RailShine_{row}", content, new Vector2(rackWidth - 30f, 8f), 4f,
                    new Color(1f, 1f, 1f, 0.3f), new Vector2(0f, railY + 10f));

                float plankY = y + tubeBottom - 14f;
                UIFactory.CreateShadow($"PlankShadow_{row}", content, new Vector2(rackWidth + 80f, 30f), 22f, MetaStyle.WarmShadow,
                    new Vector2(0f, plankY - 22f));
                UIFactory.CreateRoundedImage($"PlankLip_{row}", content, new Vector2(rackWidth + 80f, 30f), 12f, MetaStyle.WoodDark,
                    new Vector2(0f, plankY - 12f));
                UIFactory.CreateRoundedImage($"Plank_{row}", content, new Vector2(rackWidth + 80f, 30f), 12f, MetaStyle.WoodLight,
                    new Vector2(0f, plankY));
            }
        }

        private void BuildFooter(RectTransform safe)
        {
            var footer = UIFactory.CreateRect("Footer", safe);
            footer.anchorMin = new Vector2(0.5f, 0f);
            footer.anchorMax = new Vector2(0.5f, 0f);
            footer.sizeDelta = new Vector2(1000f, DetailCardHeight);
            footer.anchoredPosition = new Vector2(0f, MetaStyle.NavBarHeight + 24f + DetailCardHeight * 0.5f);

            var card = MetaStyle.Card("DetailCard", footer, new Vector2(1000f, DetailCardHeight), Vector2.zero, 52f);
            _selectedTitle = UIFactory.CreateText("SelectedTitle", card, string.Empty, 40, MetaStyle.Ink, TextAnchor.MiddleLeft,
                FontStyle.Bold);
            MetaStyle.Place(_selectedTitle.rectTransform, new Vector2(580f, 60f), new Vector2(-170f, 36f));
            CureUI.FitLabel(_selectedTitle, 40);
            _selectedInfo = UIFactory.CreateText("SelectedInfo", card, string.Empty, 29, MetaStyle.InkSoft, TextAnchor.MiddleLeft);
            MetaStyle.Place(_selectedInfo.rectTransform, new Vector2(580f, 50f), new Vector2(-170f, -26f));
            CureUI.FitLabel(_selectedInfo, 29);

            _playButton = MetaStyle.Button("PlayButton", card, "experiments_play", new Vector2(300f, 136f), new Vector2(314f, 4f),
                MetaStyle.ButtonKind.Primary, 48);
            _playGroup = _playButton.gameObject.AddComponent<CanvasGroup>();
            _playButton.onClick.AddListener(() =>
            {
                if (_progression.CanPlay(_selectedIndex)) PlayRequested?.Invoke(_selectedIndex);
            });
        }

        private void BuildMilestone(RectTransform canvasRoot)
        {
            _milestone = UIFactory.CreateStretched("Milestone", canvasRoot);
            UIFactory.AddImage(_milestone, new Color(0.22f, 0.15f, 0.09f, 0.45f), raycastTarget: true);
            _milestoneGroup = _milestone.gameObject.AddComponent<CanvasGroup>();
            MetaStyle.RaiseSorting(_milestone.gameObject, 20); // covers the tab bar
            var safe = ScreenCanvas.AddSafeArea(_milestone);

            var size = new Vector2(880f, 1000f);
            _milestoneCard = MetaStyle.Card("Card", safe, size, Vector2.zero, 72f);
            UIFactory.CreateSpriteImage("Glow", _milestoneCard, ProceduralSprites.SoftGlow, new Vector2(700f, 420f),
                new Color(_theme.comboPerfectColor.r, _theme.comboPerfectColor.g, _theme.comboPerfectColor.b, 0.3f),
                new Vector2(0f, 190f));

            MetaStyle.Tag("Chapter", _milestoneCard, string.Empty, new Vector2(320f, 58f), new Vector2(0f, 410f),
                MetaStyle.TealLight, MetaStyle.TealDark, 27).Localize(Loc.ChapterTitle(_catalog.ChapterId, _catalog.ChapterTitle), TextCase.Upper);

            var title = UIFactory.CreateText("Title", _milestoneCard, string.Empty, 72, MetaStyle.Coral,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            MetaStyle.Place(title.rectTransform, new Vector2(840f, 180f), new Vector2(0f, 305f));
            title.Localize(Loc.ChapterCompleteTitle(_catalog.ChapterId, _catalog.CompletionTitle));

            var cured = UIFactory.CreateText("Cured", _milestoneCard, string.Empty, 36,
                MetaStyle.TealDark, TextAnchor.MiddleCenter, FontStyle.Bold);
            MetaStyle.Place(cured.rectTransform, new Vector2(820f, 60f), new Vector2(0f, 205f));
            CureUI.FitLabel(cured, 36);
            cured.LocalizeUpper("experiments_milestone_cured", Loc.ChapterSubtitle(_catalog.ChapterId, _catalog.ChapterSubtitle), _catalog.Count);

            var message = UIFactory.CreateText("Message", _milestoneCard, string.Empty, 36, MetaStyle.Ink);
            message.Localize(Loc.ChapterCompleteMessage(_catalog.ChapterId, _catalog.CompletionMessage));
            MetaStyle.Place(message.rectTransform, new Vector2(760f, 150f), new Vector2(0f, 100f));

            // One-time experiment reward (hidden when 0).
            _milestoneBonus = UIFactory.CreateCentered("ResearchBonus", _milestoneCard, new Vector2(420f, 84f), new Vector2(0f, -10f));
            UIFactory.CreateRoundedImage("Body", _milestoneBonus, new Vector2(420f, 84f), 42f, MetaStyle.CreamDeep);
            ResearchIcon.Create(_milestoneBonus, 60f, MetaStyle.Teal, new Vector2(-150f, 0f));
            _milestoneBonusText = UIFactory.CreateText("Amount", _milestoneBonus, string.Empty, 36, MetaStyle.Ink,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            MetaStyle.Place(_milestoneBonusText.rectTransform, new Vector2(300f, 84f), new Vector2(40f, 0f));

            // Navigation: Home (primary), Lab, or stay here to review levels. Never forced into the Lab.
            var home = MetaStyle.Button("MilestoneHomeButton", _milestoneCard, "experiments_milestone_home", new Vector2(520f, 136f),
                new Vector2(0f, -170f), MetaStyle.ButtonKind.Primary, 50);
            home.onClick.AddListener(() => MilestoneHomeRequested?.Invoke());
            var lab = MetaStyle.Button("MilestoneLabButton", _milestoneCard, "experiments_milestone_lab", new Vector2(250f, 104f),
                new Vector2(-140f, -335f), MetaStyle.ButtonKind.Accent, 36);
            lab.onClick.AddListener(() => MilestoneLabRequested?.Invoke());
            var levels = MetaStyle.Button("MilestoneLevelsButton", _milestoneCard, "experiments_milestone_samples", new Vector2(250f, 104f),
                new Vector2(140f, -335f), MetaStyle.ButtonKind.Secondary, 34);
            levels.onClick.AddListener(HideMilestone);

            _milestone.gameObject.SetActive(false);
        }

        private void ShowMilestone(int research)
        {
            Tween.Stop(this, ref _milestoneRoutine);
            _milestone.gameObject.SetActive(true);
            _milestoneBonus.gameObject.SetActive(research > 0);
            _milestoneBonusText.Localize("level_complete_research", research);
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
    }
}
