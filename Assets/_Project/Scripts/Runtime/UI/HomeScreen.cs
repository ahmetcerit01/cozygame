using System;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>What Home should show. Built by the flow from puzzle + meta progression.</summary>
    public struct HomeState
    {
        public string ExperimentLabel;   // "EXPERIMENT 01"
        public string ExperimentTitle;   // "Virus Samples"
        public int Cured;
        public int Total;
        public int NextLevelNumber;      // 1-based, 0 when complete
        public string NextLevelTitle;
        public int MicroscopeLevel;      // shown on the Lab desk object
        public int NewlyCuredFrom;       // index of the first newly cured sample to acknowledge, -1 for none
    }

    /// <summary>
    /// Close-up of the player's workbench. The current experiment dish is the hero; the primary action
    /// (BEGIN / CONTINUE / VIEW RESULTS) plays directly. Experiments and Lab are small desk objects.
    /// </summary>
    public sealed class HomeScreen : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080f, 1920f);

        private PuzzleTheme _theme;
        private GameObject _root;
        private CanvasGroup _group;
        private RectTransform _content;
        private ExperimentDishView _dish;
        private Text _experimentLabel;
        private Text _supportTitle;
        private Text _supportLine;
        private Button _primary;
        private RectTransform _primaryRt;
        private RectTransform _labObjectHolder;
        private RectTransform _toast;
        private CanvasGroup _toastGroup;
        private Coroutine _toastRoutine;
        private Coroutine _enterRoutine;
        private int _shownMicroscopeLevel = -1;

        public enum PrimaryAction
        {
            Begin,
            Continue,
            ViewResults,
        }

        public PrimaryAction CurrentAction { get; private set; }
        public string PrimaryLabel { get; private set; }
        public string SupportTitle => _supportTitle != null ? _supportTitle.text : string.Empty;
        public bool IsVisible => _root != null && _root.activeSelf;
        public ExperimentDishView Dish => _dish;
        public bool IsToastVisible => _toast != null && _toast.gameObject.activeSelf;

        public event Action PrimaryClicked;
        public event Action ExperimentsClicked;
        public event Action LabClicked;

        public void Initialize(PuzzleTheme theme, int experimentLevelCount, string experimentTitle)
        {
            _theme = theme;
            Build(experimentLevelCount, experimentTitle);
        }

        public void Show(HomeState state, float enterFromScale = 0.98f)
        {
            ScreenCanvas.ApplyCamera(_theme);
            _root.SetActive(true);
            Apply(state);

            Tween.Stop(this, ref _enterRoutine);
            _enterRoutine = ScreenTransition.Enter(this, _group, _content, enterFromScale);
            _dish.PlayEntrance();

            // Primary action slides in softly but is clickable immediately.
            Tween.Run(this, 0.35f, Ease.Linear, t =>
            {
                _primaryRt.anchoredPosition = new Vector2(0f, PrimaryY + Mathf.Lerp(-30f, 0f, Ease.OutCubic(t)));
            }, null, 0.08f);

            if (state.NewlyCuredFrom >= 0) ShowToast("NEW SAMPLE CURED");
            else HideToast();
        }

        public void Hide()
        {
            HideToast();
            _root.SetActive(false);
        }

        private void Apply(HomeState state)
        {
            _experimentLabel.text = state.ExperimentLabel;
            _dish.SetProgress(state.Cured, state.NewlyCuredFrom);

            if (state.Cured >= state.Total && state.Total > 0)
            {
                CurrentAction = PrimaryAction.ViewResults;
                PrimaryLabel = "VIEW RESULTS";
                _supportTitle.text = "Experiment complete";
                _supportLine.text = $"{state.ExperimentTitle}  ·  {state.Total} / {state.Total} cured";
            }
            else if (state.Cured == 0)
            {
                CurrentAction = PrimaryAction.Begin;
                PrimaryLabel = "BEGIN EXPERIMENT";
                _supportTitle.text = state.ExperimentTitle;
                _supportLine.text = "Your first experiment";
            }
            else
            {
                CurrentAction = PrimaryAction.Continue;
                PrimaryLabel = "CONTINUE";
                _supportTitle.text = $"Level {state.NextLevelNumber}  ·  {state.NextLevelTitle}";
                _supportLine.text = state.ExperimentTitle;
            }
            UIButtons.SetLabel(_primary, PrimaryLabel);

            if (state.MicroscopeLevel != _shownMicroscopeLevel)
            {
                UIFactory.DestroyChildren(_labObjectHolder);
                LabArt.Microscope(_labObjectHolder, _theme, state.MicroscopeLevel, 250f);
                _shownMicroscopeLevel = state.MicroscopeLevel;
            }
        }

        // ---------------------------------------------------------------- build

        private const float PrimaryY = -560f;

        private void Build(int levelCount, string experimentTitle)
        {
            _root = new GameObject("Home");
            _root.transform.SetParent(transform, false);

            var canvasRoot = ScreenCanvas.Create("HomeCanvas", _root.transform, referenceResolution, sortingOrder: 0);
            _group = canvasRoot.gameObject.AddComponent<CanvasGroup>();
            BuildDesk(canvasRoot);

            var safe = ScreenCanvas.AddSafeArea(canvasRoot);
            _content = safe;

            // Wordmark.
            // Portrait column: wordmark / hero column (label, dish, next level, primary) / desk-object row.
            const float headerHeight = 120f;
            const float deskRowHeight = 400f;

            var header = Band(safe, "Header", top: true, height: headerHeight);
            var mark = UIFactory.CreateText("Wordmark", header, "COZYLAB", 36, _theme.accentColor, TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UIFactory.Stretch(mark.rectTransform);

            var deskRow = Band(safe, "DeskRow", top: false, height: deskRowHeight);
            BuildDeskRow(deskRow);

            // The hero column is designed for a tall phone and scales to fill whatever height is left,
            // so there are no dead bands on tall screens and nothing clips on 16:9.
            var heroArea = UIFactory.CreateStretched("HeroArea", safe);
            heroArea.offsetMin = new Vector2(0f, deskRowHeight);
            heroArea.offsetMax = new Vector2(0f, -headerHeight);
            var hero = UIFactory.CreateRect("Hero", heroArea);
            hero.gameObject.AddComponent<FitToParentScaler>().Configure(new Vector2(1080f, 1560f), 1.2f);

            _experimentLabel = UIFactory.CreateText("ExperimentLabel", hero, "EXPERIMENT 01", 34, _theme.textSecondaryColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(_experimentLabel.rectTransform, new Vector2(700f, 60f), new Vector2(0f, 730f));

            var dishRt = UIFactory.CreateCentered("ExperimentDish", hero, Vector2.zero, new Vector2(0f, 250f));
            _dish = dishRt.gameObject.AddComponent<ExperimentDishView>();
            _dish.Build(_theme, 880f, levelCount, experimentTitle, showLabels: true, ambient: true);

            // Brief acknowledgement pill between the dish and the next-level text.
            _toast = UIFactory.CreateCentered("NewSampleToast", hero, new Vector2(440f, 76f), new Vector2(0f, -238f));
            _toastGroup = _toast.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.blocksRaycasts = false;
            UIFactory.CreateRoundedImage("Body", _toast, new Vector2(440f, 76f), 38f, _theme.cardColor);
            var toastText = UIFactory.CreateText("Text", _toast, string.Empty, 30, _theme.accentColor, TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UIFactory.Stretch(toastText.rectTransform);
            _toast.gameObject.SetActive(false);

            _supportTitle = UIFactory.CreateText("SupportTitle", hero, string.Empty, 46, _theme.textPrimaryColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(_supportTitle.rectTransform, new Vector2(960f, 64f), new Vector2(0f, -330f));
            _supportLine = UIFactory.CreateText("SupportLine", hero, string.Empty, 34, _theme.textSecondaryColor);
            Place(_supportLine.rectTransform, new Vector2(960f, 52f), new Vector2(0f, -392f));

            _primary = UIButtons.CreatePill("PrimaryButton", hero, "CONTINUE", new Vector2(720f, 176f), new Vector2(0f, PrimaryY),
                _theme.accentColor, Color.white, 58);
            _primaryRt = (RectTransform)_primary.transform;
            _primary.onClick.AddListener(() => PrimaryClicked?.Invoke());

            _root.SetActive(false);
        }

        /// <summary>Experiments (sample tray) and Lab (microscope) as desk objects above the bottom safe edge.</summary>
        private void BuildDeskRow(RectTransform row)
        {
            var trayRt = DeskObject(row, "ExperimentsObject", "EXPERIMENTS", new Vector2(-250f, 215f), out var trayVisual);
            LabArt.SampleTray(trayVisual, _theme, 250f, 4);
            TapTarget.Attach(trayRt, trayVisual).Clicked += () => ExperimentsClicked?.Invoke();

            var labRt = DeskObject(row, "LabObject", "LAB", new Vector2(250f, 215f), out var labVisual);
            _labObjectHolder = labVisual;
            TapTarget.Attach(labRt, labVisual).Clicked += () => LabClicked?.Invoke();
        }

        /// <summary>A soft desk coaster with an object on it and a label underneath (340 × 340 touch area).</summary>
        private RectTransform DeskObject(RectTransform parent, string name, string label, Vector2 position, out RectTransform visual)
        {
            var rt = UIFactory.CreateRect(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(340f, 340f);
            rt.anchoredPosition = position;

            visual = UIFactory.CreateCentered("Visual", rt, new Vector2(340f, 280f), new Vector2(0f, 25f));
            UIFactory.CreateSpriteImage("Coaster", visual, ProceduralSprites.SoftGlow, new Vector2(360f, 190f),
                new Color(1f, 1f, 1f, 0.9f), new Vector2(0f, -70f));

            var text = UIFactory.CreateText("Label", rt, label, 32, _theme.textPrimaryColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(text.rectTransform, new Vector2(340f, 48f), new Vector2(0f, -145f));
            return rt;
        }
        private void BuildDesk(RectTransform canvasRoot)
        {
            // The whole screen is the bench mat seen from above: soft mint with a faint measuring grid.
            var bg = UIFactory.CreateStretched("Desk", canvasRoot);
            UIFactory.AddImage(bg, _theme.deskMatColor);
            for (int i = -6; i <= 6; i++)
            {
                var v = UIFactory.CreateRect("GridV", bg);
                v.anchorMin = new Vector2(0.5f, 0f);
                v.anchorMax = new Vector2(0.5f, 1f);
                v.sizeDelta = new Vector2(3f, 0f);
                v.anchoredPosition = new Vector2(i * 180f, 0f);
                UIFactory.AddImage(v, _theme.deskGridColor);
            }
            for (int i = -9; i <= 9; i++)
            {
                var h = UIFactory.CreateRect("GridH", bg);
                h.anchorMin = new Vector2(0f, 0.5f);
                h.anchorMax = new Vector2(1f, 0.5f);
                h.sizeDelta = new Vector2(0f, 3f);
                h.anchoredPosition = new Vector2(0f, i * 180f);
                UIFactory.AddImage(h, _theme.deskGridColor);
            }

            // A couple of props bleeding off the edges make it read as a real workbench.
            var notebook = UIFactory.CreateRect("Notebook", bg);
            notebook.anchorMin = notebook.anchorMax = new Vector2(1f, 1f);
            notebook.sizeDelta = new Vector2(360f, 440f);
            notebook.anchoredPosition = new Vector2(-40f, -150f);
            notebook.localRotation = Quaternion.Euler(0f, 0f, -14f);
            UIFactory.CreateShadow("Shadow", notebook, new Vector2(360f, 440f), 30f, new Color(0.1f, 0.2f, 0.24f, 0.16f),
                new Vector2(0f, -12f));
            UIFactory.CreateRoundedImage("Cover", notebook, new Vector2(360f, 440f), 26f, _theme.cardColor);
            for (int i = 0; i < 5; i++)
                UIFactory.CreateRoundedImage("Line", notebook, new Vector2(230f, 8f), 4f,
                    new Color(_theme.boardRimColor.r, _theme.boardRimColor.g, _theme.boardRimColor.b, 0.9f),
                    new Vector2(-20f, 90f - i * 50f));
            var pencil = UIFactory.CreateRoundedImage("Pencil", notebook, new Vector2(26f, 300f), 13f, _theme.GetPieceColor(3),
                new Vector2(-170f, -10f));
            pencil.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 8f);

            var tubes = UIFactory.CreateRect("Tubes", bg);
            tubes.anchorMin = tubes.anchorMax = new Vector2(0f, 1f);
            tubes.sizeDelta = new Vector2(300f, 300f);
            tubes.anchoredPosition = new Vector2(60f, -330f);
            tubes.localRotation = Quaternion.Euler(0f, 0f, 28f);
            for (int i = 0; i < 3; i++)
            {
                var tube = UIFactory.CreateCentered("Tube", tubes, new Vector2(58f, 280f), new Vector2((i - 1) * 78f, 0f));
                UIFactory.CreateShadow("Shadow", tube, new Vector2(58f, 280f), 14f, new Color(0.1f, 0.2f, 0.24f, 0.14f),
                    new Vector2(0f, -8f));
                UIFactory.CreateRoundedImage("Glass", tube, new Vector2(58f, 280f), 29f, new Color(1f, 1f, 1f, 0.92f));
                UIFactory.CreateRoundedImage("Liquid", tube, new Vector2(42f, 120f), 21f, _theme.GetPieceColor(i + 4),
                    new Vector2(0f, -70f));
                UIFactory.CreateRoundedImage("Cap", tube, new Vector2(64f, 34f), 12f, _theme.GetPieceColor(i + 4),
                    new Vector2(0f, 128f));
            }
        }

        private void ShowToast(string message)
        {
            Tween.Stop(this, ref _toastRoutine);
            _toast.GetComponentInChildren<Text>().text = message;
            _toast.gameObject.SetActive(true);
            _toastGroup.alpha = 0f;
            _toastRoutine = Tween.Run(this, 2.4f, Ease.Linear, t =>
            {
                float a = t < 0.15f ? t / 0.15f : t > 0.8f ? 1f - (t - 0.8f) / 0.2f : 1f;
                _toastGroup.alpha = a;
                float s = t < 0.15f ? Mathf.LerpUnclamped(0.85f, 1f, Ease.OutBack(t / 0.15f)) : 1f;
                _toast.localScale = new Vector3(s, s, 1f);
            }, HideToast, 0.35f);
        }

        private void HideToast()
        {
            Tween.Stop(this, ref _toastRoutine);
            if (_toast != null) _toast.gameObject.SetActive(false);
        }

        private static RectTransform Band(RectTransform parent, string name, bool top, float height)
        {
            var rt = UIFactory.CreateRect(name, parent);
            rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        private static void Place(RectTransform rt, Vector2 size, Vector2 position)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }
    }
}
