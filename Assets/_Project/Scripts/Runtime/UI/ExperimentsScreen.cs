using System;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Localization;
using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>
    /// All experiments, as cards in a research journal. Experiment 01 opens its sample rack (Level Select);
    /// future experiments show as sealed cards. Adding a chapter later means adding a card, not a new screen.
    /// </summary>
    public sealed class ExperimentsScreen : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080f, 1920f);

        private PuzzleTheme _theme;
        private GameObject _root;
        private CanvasGroup _group;
        private RectTransform _content;
        private ExperimentDishView _dish;
        private MetaProgressBar _bar;
        private Text _progress;
        private ResearchCounter _research;
        private Button _open;
        private Coroutine _enter;

        public event Action ExperimentClicked;

        public bool IsVisible => _root != null && _root.activeSelf;
        public string ProgressText => _progress != null ? _progress.text : string.Empty;

        public void Initialize(PuzzleTheme theme, LevelCatalog catalog)
        {
            _theme = theme;
            Build(catalog);
        }

        public void Show(int cured, int total, int research)
        {
            ScreenCanvas.ApplyCamera(_theme);
            _root.SetActive(true);
            _dish.SetProgress(cured);
            _bar.SetValue(total > 0 ? (float)cured / total : 0f);
            if (cured >= total) _progress.Localize("experiments_progress_complete", total);
            else _progress.Localize("experiments_progress_caps", cured, total);
            UIButtons.SetLabelKey(_open, cured == 0 ? "experiments_start" : cured >= total ? "experiments_review" : "experiments_open");
            _research.SetValue(research, animate: false);
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
            MetaStyle.WarmBackground(canvasRoot);
            var safe = ScreenCanvas.AddSafeArea(canvasRoot);
            _content = safe;

            var header = MetaStyle.TopBand(safe, 230f);
            var title = UIFactory.CreateText("Title", header, string.Empty, 64, MetaStyle.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            CureUI.FitLabel(title, 64);
            title.Localize("experiments_title");
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 0.45f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(56f, 0f);
            titleRt.offsetMax = new Vector2(-320f, 0f);
            var sub = UIFactory.CreateText("Subtitle", header, string.Empty, 32, MetaStyle.InkSoft, TextAnchor.MiddleLeft);
            sub.Localize("experiments_subtitle");
            var subRt = sub.rectTransform;
            subRt.anchorMin = new Vector2(0f, 0.15f);
            subRt.anchorMax = new Vector2(1f, 0.45f);
            subRt.offsetMin = new Vector2(58f, 0f);
            subRt.offsetMax = new Vector2(-320f, 0f);
            _research = MetaStyle.ResearchChip(header, new Vector2(1f, 1f), new Vector2(-165f, -85f));

            var area = UIFactory.CreateStretched("Area", safe);
            area.offsetMin = new Vector2(0f, MetaStyle.NavBarHeight + 10f);
            area.offsetMax = new Vector2(0f, -230f);
            var content = UIFactory.CreateRect("Content", area);
            content.gameObject.AddComponent<FitToParentScaler>().Configure(new Vector2(1080f, 1420f), 1.1f);

            // ---- Experiment 01: the active card, tappable as a whole.
            var item = UIFactory.CreateCentered("Experiment01", content, new Vector2(960f, 640f), new Vector2(0f, 360f));
            var card = MetaStyle.Card("Card", item, new Vector2(960f, 640f), Vector2.zero, 60f);

            var dishRt = UIFactory.CreateCentered("Dish", card, Vector2.zero, new Vector2(-250f, 60f));
            UIFactory.CreateSpriteImage("DishMat", card, ProceduralSprites.SoftGlow, new Vector2(520f, 480f),
                new Color(MetaStyle.TealLight.r, MetaStyle.TealLight.g, MetaStyle.TealLight.b, 0.9f), new Vector2(-250f, 60f))
                .transform.SetSiblingIndex(dishRt.GetSiblingIndex());
            _dish = dishRt.gameObject.AddComponent<ExperimentDishView>();
            _dish.Build(_theme, 380f, catalog.Count, Loc.ChapterSubtitle(catalog.ChapterId, catalog.ChapterSubtitle), showLabels: false,
                ambient: false);

            MetaStyle.Tag("Chapter", card, string.Empty, new Vector2(300f, 54f), new Vector2(190f, 220f),
                MetaStyle.TealLight, MetaStyle.TealDark, 25).Localize(Loc.ChapterTitle(catalog.ChapterId, catalog.ChapterTitle), TextCase.Upper);
            var name = UIFactory.CreateText("Name", card, string.Empty, 54, MetaStyle.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            name.Localize(Loc.ChapterSubtitle(catalog.ChapterId, catalog.ChapterSubtitle));
            MetaStyle.Place(name.rectTransform, new Vector2(420f, 130f), new Vector2(250f, 110f));
            var levels = UIFactory.CreateText("Levels", card, string.Empty, 30, MetaStyle.InkSoft, TextAnchor.MiddleLeft);
            levels.Localize("experiments_samples_to_cure", catalog.Count);
            MetaStyle.Place(levels.rectTransform, new Vector2(420f, 44f), new Vector2(250f, 20f));

            _bar = MetaProgressBar.Create(card, new Vector2(400f, 28f), new Vector2(240f, -50f));
            _progress = UIFactory.CreateText("Progress", card, string.Empty, 28, MetaStyle.TealDark, TextAnchor.MiddleLeft, FontStyle.Bold);
            MetaStyle.Place(_progress.rectTransform, new Vector2(420f, 44f), new Vector2(250f, -95f));

            TapTarget.Attach(item, card).Clicked += () => ExperimentClicked?.Invoke();

            _open = MetaStyle.Button("OpenExperimentButton", card, "experiments_open", new Vector2(360f, 130f), new Vector2(230f, -215f),
                MetaStyle.ButtonKind.Primary, 46);
            _open.onClick.AddListener(() => ExperimentClicked?.Invoke());

            // ---- Sealed future experiments (not interactive, no fake content).
            SealedCard(content, "Experiment02", 2, new Vector2(0f, -160f));
            SealedCard(content, "Experiment03", 3, new Vector2(0f, -440f));

            _root.SetActive(false);
        }

        private static void SealedCard(RectTransform parent, string name, int number, Vector2 position)
        {
            var card = MetaStyle.Card(name, parent, new Vector2(960f, 220f), position, 52f, MetaStyle.CreamDeep, MetaStyle.Locked);
            var seal = UIFactory.CreateCentered("Seal", card, new Vector2(130f, 130f), new Vector2(-370f, 0f));
            UIFactory.CreateSpriteImage("Disc", seal, ProceduralSprites.Circle, new Vector2(130f, 130f), MetaStyle.Locked);
            var icon = UIFactory.CreateCentered("Icon", seal, new Vector2(64f, 64f));
            MetaIcons.Lock(icon, 64f, MetaStyle.InkSoft);

            var title = UIFactory.CreateText("Title", card, string.Empty, 40, MetaStyle.InkSoft, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.Localize("experiments_numbered", number);
            MetaStyle.Place(title.rectTransform, new Vector2(600f, 56f), new Vector2(40f, 28f));
            var sub = UIFactory.CreateText("Status", card, string.Empty, 30,
                new Color(MetaStyle.InkSoft.r, MetaStyle.InkSoft.g, MetaStyle.InkSoft.b, 0.8f), TextAnchor.MiddleLeft);
            MetaStyle.Place(sub.rectTransform, new Vector2(600f, 44f), new Vector2(40f, -30f));
            sub.Localize("experiments_arriving_soon");
            var soon = MetaStyle.Tag("Soon", card, string.Empty, new Vector2(140f, 50f), new Vector2(360f, 0f), MetaStyle.Locked, MetaStyle.Ink, 24);
            CureUI.FitLabel(soon, 24);
            soon.Localize("experiments_soon");
        }
    }
}
