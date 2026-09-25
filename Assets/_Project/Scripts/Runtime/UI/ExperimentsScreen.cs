using System;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>
    /// Lightweight list of experiments. For now: Experiment 01 (opens the existing Level Select)
    /// and one quiet "coming soon" placeholder.
    /// </summary>
    public sealed class ExperimentsScreen : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080f, 1920f);

        private PuzzleTheme _theme;
        private GameObject _root;
        private CanvasGroup _group;
        private RectTransform _content;
        private ExperimentDishView _dish;
        private Text _progress;
        private Coroutine _enter;

        public event Action HomeClicked;
        public event Action ExperimentClicked;

        public bool IsVisible => _root != null && _root.activeSelf;
        public string ProgressText => _progress != null ? _progress.text : string.Empty;

        public void Initialize(PuzzleTheme theme, LevelCatalog catalog)
        {
            _theme = theme;
            Build(catalog);
        }

        public void Show(int cured, int total)
        {
            ScreenCanvas.ApplyCamera(_theme);
            _root.SetActive(true);
            _dish.SetProgress(cured);
            _progress.text = cured >= total ? $"{total} / {total} CURED  ·  COMPLETE" : $"{cured} / {total} CURED";
            Tween.Stop(this, ref _enter);
            _enter = ScreenTransition.Enter(this, _group, _content, 0.98f);
        }

        public void Hide() => _root.SetActive(false);

        private void Build(LevelCatalog catalog)
        {
            _root = new GameObject("Experiments");
            _root.transform.SetParent(transform, false);
            var canvasRoot = ScreenCanvas.Create("ExperimentsCanvas", _root.transform, referenceResolution, sortingOrder: 0);
            _group = canvasRoot.gameObject.AddComponent<CanvasGroup>();
            ScreenCanvas.AddBackground(canvasRoot, _theme);
            var safe = ScreenCanvas.AddSafeArea(canvasRoot);
            _content = safe;

            var header = UIFactory.CreateRect("Header", safe);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 200f);

            var back = UIButtons.CreatePill("HomeButton", header, "HOME", new Vector2(200f, 96f), Vector2.zero,
                _theme.buttonColor, _theme.buttonTextColor, 32);
            var backRt = (RectTransform)back.transform;
            backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
            backRt.anchoredPosition = new Vector2(140f, -68f);
            back.onClick.AddListener(() => HomeClicked?.Invoke());

            var title = UIFactory.CreateText("Title", header, "EXPERIMENTS", 56, _theme.textPrimaryColor, TextAnchor.MiddleCenter,
                FontStyle.Bold);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(250f, 60f);
            titleRt.offsetMax = new Vector2(-250f, 0f);

            var area = UIFactory.CreateStretched("Area", safe);
            area.offsetMax = new Vector2(0f, -200f);
            var content = UIFactory.CreateRect("Content", area);
            content.gameObject.AddComponent<FitToParentScaler>().Configure(new Vector2(1080f, 1500f), 1.12f);

            // Experiment 01: a dish on a soft mat, tappable as a whole.
            var item = UIFactory.CreateCentered("Experiment01", content, new Vector2(900f, 900f), new Vector2(0f, 230f));
            var itemVisual = UIFactory.CreateCentered("Visual", item, new Vector2(900f, 900f));
            UIFactory.CreateSpriteImage("Mat", itemVisual, ProceduralSprites.SoftGlow, new Vector2(900f, 760f),
                new Color(1f, 1f, 1f, 0.75f), new Vector2(0f, 90f));
            var dishRt = UIFactory.CreateCentered("Dish", itemVisual, Vector2.zero, new Vector2(0f, 120f));
            _dish = dishRt.gameObject.AddComponent<ExperimentDishView>();
            _dish.Build(_theme, 540f, catalog.Count, catalog.ChapterSubtitle, showLabels: false, ambient: false);

            var chapter = UIFactory.CreateText("Chapter", itemVisual, catalog.ChapterTitle.ToUpperInvariant(), 32, _theme.accentColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(chapter.rectTransform, new Vector2(800f, 50f), new Vector2(0f, -210f));
            var nameText = UIFactory.CreateText("Name", itemVisual, catalog.ChapterSubtitle.ToUpperInvariant(), 60,
                _theme.textPrimaryColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(nameText.rectTransform, new Vector2(860f, 80f), new Vector2(0f, -275f));
            _progress = UIFactory.CreateText("Progress", itemVisual, string.Empty, 34, _theme.textSecondaryColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(_progress.rectTransform, new Vector2(860f, 50f), new Vector2(0f, -340f));
            TapTarget.Attach(item, itemVisual).Clicked += () => ExperimentClicked?.Invoke();

            var open = UIButtons.CreatePill("OpenExperimentButton", content, "OPEN", new Vector2(460f, 136f), new Vector2(0f, -330f),
                _theme.accentColor, Color.white, 48);
            open.onClick.AddListener(() => ExperimentClicked?.Invoke());

            // One restrained placeholder for what comes next (not interactive, no fake content).
            var soon = UIFactory.CreateCentered("ComingSoon", content, new Vector2(700f, 150f), new Vector2(0f, -560f));
            UIFactory.CreateSpriteImage("Ring", soon, ProceduralSprites.Ring, new Vector2(110f, 110f),
                new Color(_theme.textSecondaryColor.r, _theme.textSecondaryColor.g, _theme.textSecondaryColor.b, 0.35f),
                new Vector2(-200f, 0f));
            var soonText = UIFactory.CreateText("Text", soon, "EXPERIMENT 02\nCOMING SOON", 30,
                new Color(_theme.textSecondaryColor.r, _theme.textSecondaryColor.g, _theme.textSecondaryColor.b, 0.7f),
                TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(soonText.rectTransform, new Vector2(420f, 110f), new Vector2(60f, 0f));

            _root.SetActive(false);
        }

        private static void Place(RectTransform rt, Vector2 size, Vector2 position)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }
    }
}
