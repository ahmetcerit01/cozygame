using System;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Localization;
using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>What Home should show. Built by the flow from puzzle + meta progression.</summary>
    public struct HomeState
    {
        public LocString ExperimentTitle; // "Virus Samples"
        public int Cured;
        public int Total;
        public int NextLevelNumber;      // 1-based, 0 when complete
        public LocString NextLevelTitle;
        public int MicroscopeLevel;
        public int NewlyCuredFrom;       // index of the first newly cured sample to acknowledge, -1 for none
        public int Research;
        public bool UpgradeAvailable;    // Research affords a Lab upgrade (badge on the Lab tile)
    }

    /// <summary>
    /// The hub, built on the authored Home clean plate (desk, clipboard, petri dish of jelly samples).
    /// Only dynamic UI is layered on top: currency pills, the clipboard's Current Sample text, the CONTINUE
    /// action and a 2×2 block of navigation tiles in the free desk area.
    /// </summary>
    public sealed class HomeScreen : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080f, 1920f);

        // Authored full-screen Home art and UI icons, supplied by GameFlow. The art is cover-fitted, never stretched.
        private Sprite _background;
        private HomeIcons _icons;

        // Blank clipboard paper inside the plate, in normalized image coordinates (0,0 = bottom-left).
        private static readonly Rect ClipboardPaper = new Rect(0.340f, 0.659f, 0.393f, 0.111f);
        // Upper half of the petri dish, where the "new sample cured" note appears.
        private static readonly Vector2 DishTop = new Vector2(0.5f, 0.60f);
        // Clipboard text is laid out at this size (the paper at 1080×1920) and scaled with the art.
        private static readonly Vector2 ClipboardDesign = new Vector2(424f, 213f);

        // Bottom block, laid out bottom-up in the safe area: two tile rows, then CONTINUE.
        private const float TileWidth = 476f;
        private const float TileRowHigh = 200f;
        private const float TileRowLow = 150f;
        private const float TileGap = 26f;
        private const float BottomMargin = 30f;
        private const float PrimaryGap = 40f;
        private const float PrimaryHeight = 150f;
        private const float StatusBarHeight = 130f;

        private PuzzleTheme _theme;
        private GameObject _root;
        private CanvasGroup _group;
        private RectTransform _content;
        private RectTransform _art;
        private Text _clipHeader;
        private Text _clipTitle;
        private Text _clipLevel;
        private Image[] _progressDots = Array.Empty<Image>();
        private RectTransform _dotsRow;
        private CurrencyPill _coins;
        private CurrencyPill _research;
        private Button _primary;
        private RectTransform _primaryRt;
        private float _primaryY;
        private NotificationBadge _labBadge;
        private RectTransform _toast;
        private CanvasGroup _toastGroup;
        private Coroutine _toastRoutine;
        private Coroutine _enterRoutine;
        private int _cured;
        private HomeArtFramer _artFramer;

        public enum PrimaryAction
        {
            Begin,
            Continue,
            ViewResults,
        }

        public PrimaryAction CurrentAction { get; private set; }
        /// <summary>The primary button's label as currently displayed (localized).</summary>
        public string PrimaryLabel => UIButtons.GetLabel(_primary);
        public string ClipboardHeader => _clipHeader != null ? _clipHeader.text : string.Empty;
        public string ClipboardTitle => _clipTitle != null ? _clipTitle.text : string.Empty;
        public string ClipboardLevel => _clipLevel != null ? _clipLevel.text : string.Empty;
        public int Cured => _cured;
        public bool IsVisible => _root != null && _root.activeSelf;
        public bool IsToastVisible => _toast != null && _toast.gameObject.activeSelf;
        public bool IsLabBadgeVisible => _labBadge != null && _labBadge.IsVisible;
        public bool HasAuthoredBackground => _background != null;
        public int ShownResearch => _research != null ? _research.ShownValue : 0;

        public event Action PrimaryClicked;
        public event Action ExperimentsClicked;
        public event Action LabClicked;
        public event Action DailyClicked;
        public event Action ShopClicked;
        public event Action SettingsClicked;

        /// <param name="background">Authored Home art (GameFlow's Home Background reference).</param>
        /// <param name="icons">Authored Home UI icons (GameFlow's Home Icons).</param>
        public void Initialize(PuzzleTheme theme, Sprite background, HomeIcons icons)
        {
            _theme = theme;
            _background = background;
            _icons = icons;
            Build();
        }

        /// <summary>How much the art is lifted (1 = not at all), for layout checks.</summary>
        public float ArtLift => _artFramer != null ? _artFramer.Scale : 1f;

        public void Show(HomeState state, float enterFromScale = 0.985f)
        {
            ScreenCanvas.ApplyCamera(_theme);
            _root.SetActive(true);
            Apply(state);

            Tween.Stop(this, ref _enterRoutine);
            _enterRoutine = ScreenTransition.Enter(this, _group, _content, enterFromScale);

            // Primary action rises softly but is clickable immediately.
            Tween.Run(this, 0.35f, Ease.Linear, t =>
            {
                _primaryRt.anchoredPosition = new Vector2(0f, _primaryY + Mathf.Lerp(-24f, 0f, Ease.OutCubic(t)));
            }, null, 0.06f);

            if (state.NewlyCuredFrom >= 0) ShowToast("home_new_sample_cured");
            else HideToast();
        }

        public void Hide()
        {
            HideToast();
            _root.SetActive(false);
        }

        private void Apply(HomeState state)
        {
            _cured = Mathf.Clamp(state.Cured, 0, state.Total);
            _research.SetValue(state.Research, animate: false);
            _coins.SetValue(0, animate: false); // Coins are not implemented yet: placeholder slot.
            _labBadge.SetVisible(state.UpgradeAvailable);

            // Bound texts: they re-render by themselves if the language changes while Home is visible.
            bool complete = state.Total > 0 && state.Cured >= state.Total;
            if (complete)
            {
                CurrentAction = PrimaryAction.ViewResults;
                _clipHeader.Localize("home_experiment_complete");
                _clipTitle.Localize(state.ExperimentTitle);
                _clipLevel.Localize("home_all_cured", state.Total);
            }
            else
            {
                CurrentAction = state.Cured == 0 ? PrimaryAction.Begin : PrimaryAction.Continue;
                _clipHeader.Localize("home_current_sample");
                _clipTitle.Localize(state.NextLevelTitle);
                _clipLevel.Localize("home_level", state.NextLevelNumber);
            }
            UIButtons.SetLabelKey(_primary, CurrentAction == PrimaryAction.ViewResults ? "home_view_results"
                : CurrentAction == PrimaryAction.Begin ? "home_begin" : "home_continue");
            ApplyDots(state);
        }

        private void ApplyDots(HomeState state)
        {
            if (_progressDots.Length != state.Total)
            {
                UIFactory.DestroyChildren(_dotsRow);
                _progressDots = new Image[state.Total];
                const float spacing = 26f;
                for (int i = 0; i < state.Total; i++)
                    _progressDots[i] = UIFactory.CreateSpriteImage("Dot", _dotsRow, ProceduralSprites.Circle, new Vector2(16f, 16f),
                        Color.white, new Vector2((i - (state.Total - 1) * 0.5f) * spacing, 0f));
            }
            for (int i = 0; i < _progressDots.Length; i++)
            {
                bool cured = i < state.Cured;
                bool current = !cured && i == state.Cured;
                _progressDots[i].color = cured ? _theme.GetPieceColor(i)
                    : current ? MetaStyle.Teal
                    : new Color(MetaStyle.Petrol.r, MetaStyle.Petrol.g, MetaStyle.Petrol.b, 0.18f);
                _progressDots[i].rectTransform.sizeDelta = Vector2.one * (current ? 20f : 16f);
            }
        }

        // ---------------------------------------------------------------- build

        private void Build()
        {
            _root = new GameObject("Home");
            _root.transform.SetParent(transform, false);

            var canvasRoot = ScreenCanvas.Create("HomeCanvas", _root.transform, referenceResolution, sortingOrder: 0);
            _group = canvasRoot.gameObject.AddComponent<CanvasGroup>();
            var frame = BuildBackground(canvasRoot);
            BuildClipboard();
            BuildToast();

            var safe = ScreenCanvas.AddSafeArea(canvasRoot);
            _content = safe;
            BuildStatusBar(safe);
            BuildBottom(safe);

            // Lift the art just enough for the dish to clear CONTINUE, without the clipboard meeting the status bar.
            _artFramer = frame.gameObject.AddComponent<HomeArtFramer>();
            _artFramer.Configure(safe, ArtAspect, _primaryY + PrimaryHeight * 0.5f + 8f, StatusBarHeight);

            _root.SetActive(false);
        }

        private float ArtAspect => _background != null ? _background.rect.width / _background.rect.height : 9f / 16f;

        /// <summary>
        /// The authored plate, cover-fitted (uniform scale, outer edges cropped) inside a frame that the
        /// <see cref="HomeArtFramer"/> scales about the bottom edge, plus soft top/bottom scrims.
        /// </summary>
        private RectTransform BuildBackground(RectTransform canvasRoot)
        {
            var bg = UIFactory.CreateStretched("Background", canvasRoot);
            UIFactory.AddImage(bg, _theme.deskWoodColor);

            var frame = UIFactory.CreateStretched("ArtFrame", bg);
            frame.pivot = new Vector2(0.5f, 0f);
            _art = UIFactory.CreateRect("HomeArt", frame);
            _art.anchorMin = _art.anchorMax = _art.pivot = new Vector2(0.5f, 0.5f);
            var fitter = _art.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = ArtAspect;
            if (_background != null)
            {
                var image = UIFactory.AddImage(_art, Color.white);
                image.sprite = _background;
            }

            // Gentle darkening at the very top and bottom keeps pills and tiles readable without dulling the hero.
            var top = UIFactory.CreateStretched("TopScrim", bg);
            top.anchorMin = new Vector2(0f, 0.88f);
            UIFactory.AddImage(top, new Color(0.16f, 0.09f, 0.04f, 0.32f)).sprite = ProceduralSprites.VerticalFade;
            var bottom = UIFactory.CreateStretched("BottomScrim", bg);
            bottom.anchorMax = new Vector2(1f, 0.34f);
            var bottomImage = UIFactory.AddImage(bottom, new Color(0.16f, 0.09f, 0.04f, 0.3f));
            bottomImage.sprite = ProceduralSprites.VerticalFade;
            bottom.localScale = new Vector3(1f, -1f, 1f); // opaque edge at the bottom
            return frame;
        }

        /// <summary>Current Sample text on the blank clipboard, anchored to the paper inside the art.</summary>
        private void BuildClipboard()
        {
            var paper = UIFactory.CreateRect("ClipboardPaper", _art);
            paper.anchorMin = ClipboardPaper.min;
            paper.anchorMax = ClipboardPaper.max;
            paper.offsetMin = paper.offsetMax = Vector2.zero;

            var clip = UIFactory.CreateRect("ClipboardText", paper);
            clip.gameObject.AddComponent<FitToParentScaler>().Configure(ClipboardDesign, 3f);

            _clipHeader = UIFactory.CreateText("Header", clip, string.Empty, 25, MetaStyle.Petrol, TextAnchor.MiddleCenter, FontStyle.Bold);
            CureUI.FitLabel(_clipHeader, 25);
            MetaStyle.Place(_clipHeader.rectTransform, new Vector2(400f, 34f), new Vector2(0f, 72f));
            UIFactory.CreateRoundedImage("Rule", clip, new Vector2(230f, 3f), 1.5f,
                new Color(MetaStyle.Petrol.r, MetaStyle.Petrol.g, MetaStyle.Petrol.b, 0.25f), new Vector2(0f, 50f));

            _clipTitle = UIFactory.CreateText("Title", clip, string.Empty, 44, MetaStyle.Charcoal, TextAnchor.MiddleCenter, FontStyle.Bold);
            MetaStyle.Place(_clipTitle.rectTransform, new Vector2(410f, 60f), new Vector2(0f, 12f));
            _clipTitle.resizeTextForBestFit = true;
            _clipTitle.resizeTextMinSize = 28;
            _clipTitle.resizeTextMaxSize = 44;
            _clipTitle.verticalOverflow = VerticalWrapMode.Truncate;

            _clipLevel = UIFactory.CreateText("Level", clip, string.Empty, 30, MetaStyle.Petrol, TextAnchor.MiddleCenter);
            CureUI.FitLabel(_clipLevel, 30);
            MetaStyle.Place(_clipLevel.rectTransform, new Vector2(400f, 40f), new Vector2(0f, -38f));

            _dotsRow = UIFactory.CreateCentered("Progress", clip, new Vector2(300f, 24f), new Vector2(0f, -78f));
        }

        private void BuildToast()
        {
            var anchor = UIFactory.CreateRect("ToastAnchor", _art);
            anchor.anchorMin = anchor.anchorMax = DishTop;
            anchor.sizeDelta = Vector2.zero;
            _toast = UIFactory.CreateCentered("NewSampleToast", anchor, new Vector2(420f, 72f));
            _toastGroup = _toast.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.blocksRaycasts = false;
            UIFactory.CreateShadow("Shadow", _toast, new Vector2(420f, 72f), 16f, MetaStyle.WarmShadow, new Vector2(0f, -6f));
            UIFactory.CreateRoundedImage("Body", _toast, new Vector2(420f, 72f), 36f, new Color(MetaStyle.Ivory.r, MetaStyle.Ivory.g, MetaStyle.Ivory.b, 0.95f));
            var text = UIFactory.CreateText("Text", _toast, string.Empty, 28, MetaStyle.TealDark, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(16f, 0f);
            text.rectTransform.offsetMax = new Vector2(-16f, 0f);
            CureUI.FitLabel(text, 28);
            _toast.gameObject.SetActive(false);
        }

        /// <summary>Settings (left), Coins and Research glass pills with authored icons and "+" actions.</summary>
        private void BuildStatusBar(RectTransform safe)
        {
            var bar = MetaStyle.TopBand(safe, StatusBarHeight);

            var settings = CureUI.SettingsButton(bar);
            var settingsRt = (RectTransform)settings.transform;
            settingsRt.anchorMin = settingsRt.anchorMax = new Vector2(0f, 0.5f);
            settingsRt.anchoredPosition = new Vector2(36f + 46f, 0f);
            settings.onClick.AddListener(() => SettingsClicked?.Invoke());

            _coins = CureUI.CurrencyPill("CoinsPill", bar, 350f, new Vector2(0f, 0.5f), new Vector2(170f + 175f, 0f), _icons.coin, _icons.add);
            _coins.Plus.onClick.AddListener(() => ShopClicked?.Invoke());
            _research = CureUI.CurrencyPill("ResearchPill", bar, 350f, new Vector2(1f, 0.5f), new Vector2(-52f - 175f, 0f), _icons.research,
                _icons.add);
            _research.Plus.onClick.AddListener(() => ExperimentsClicked?.Invoke()); // earn Research by curing samples
        }

        /// <summary>CONTINUE above a 2×2 block of beveled menu tiles, laid out bottom-up in the free desk area.</summary>
        private void BuildBottom(RectTransform safe)
        {
            var bottom = UIFactory.CreateRect("Bottom", safe);
            bottom.anchorMin = bottom.anchorMax = new Vector2(0.5f, 0f);
            bottom.pivot = new Vector2(0.5f, 0f);
            bottom.sizeDelta = new Vector2(1080f, 0f);

            float x = (TileWidth + TileGap) * 0.5f;
            float lowY = BottomMargin + TileRowLow * 0.5f;
            float highY = BottomMargin + TileRowLow + TileGap + TileRowHigh * 0.5f;
            var high = new Vector2(TileWidth, TileRowHigh);
            var low = new Vector2(TileWidth, TileRowLow);

            var experiments = CureUI.NavigationCard("ExperimentsButton", bottom, "home_experiments", high, new Vector2(-x, highY),
                _icons.experiments, 158f, 38);
            experiments.onClick.AddListener(() => ExperimentsClicked?.Invoke());
            var lab = CureUI.NavigationCard("LabButton", bottom, "home_lab", high, new Vector2(x, highY), _icons.lab, 158f, 38);
            lab.onClick.AddListener(() => LabClicked?.Invoke());
            _labBadge = CureUI.Badge(lab.transform, 50f, new Vector2(TileWidth * 0.5f - 20f, TileRowHigh * 0.5f - 16f));

            var daily = CureUI.NavigationCard("DailyButton", bottom, "home_daily", low, new Vector2(-x, lowY), _icons.daily, 108f, 34);
            daily.onClick.AddListener(() => DailyClicked?.Invoke());
            var shop = CureUI.NavigationCard("ShopButton", bottom, "home_shop", low, new Vector2(x, lowY), _icons.shop, 108f, 34);
            shop.onClick.AddListener(() => ShopClicked?.Invoke());

            _primaryY = highY + TileRowHigh * 0.5f + PrimaryGap + PrimaryHeight * 0.5f;
            _primary = CureUI.PrimaryButton("PrimaryButton", bottom, "home_continue", new Vector2(640f, PrimaryHeight),
                new Vector2(0f, _primaryY), 60);
            _primaryRt = (RectTransform)_primary.transform;
            _primary.onClick.AddListener(() => PrimaryClicked?.Invoke());
        }

        private void ShowToast(string key)
        {
            Tween.Stop(this, ref _toastRoutine);
            _toast.GetComponentInChildren<Text>().Localize(key);
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
    }
}
