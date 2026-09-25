using System;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Meta;
using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>Everything the Lab needs to draw itself. Built by the flow; the Lab never reads saves directly.</summary>
    public struct LabState
    {
        public int Research;
        public int MicroscopeLevel;
        public int MicroscopeMaxLevel;
        public int UpgradeCost;           // -1 at max level
        public int LabLevel;
        public bool IncubatorUnlocked;
        public bool AnalyzerUnlocked;
        public int Cured;
        public bool ExperimentComplete;
        public bool RevealIncubator;      // play the unlock reveal this time
        public bool RevealAnalyzer;
    }

    /// <summary>
    /// The optional Lab: a wider view of the player's workspace with upgradeable equipment.
    /// Purely visual/meta progression — nothing here affects puzzles.
    /// </summary>
    public sealed class LabScreen : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080f, 1920f);

        private PuzzleTheme _theme;
        private GameObject _root;
        private CanvasGroup _group;
        private RectTransform _content;
        private ResearchCounter _research;
        private Text _labLevel;
        private RectTransform _scene;
        private RectTransform _microscopeSlot;
        private RectTransform _incubatorSlot;
        private RectTransform _analyzerSlot;
        private Text _incubatorStatus;
        private Text _analyzerStatus;
        private Text _microscopeStatus;
        private RectTransform _decor;
        private UIParticles _particles;
        private Coroutine _enter;
        private float _nextIdle;
        private LabState _state;

        // Bottom sheet.
        private RectTransform _sheet;
        private RectTransform _sheetPanel;
        private CanvasGroup _sheetGroup;
        private Text _sheetTitle;
        private Text _sheetLevel;
        private Text _sheetBody;
        private RectTransform _costRow;
        private Text _costText;
        private Button _upgradeButton;
        private CanvasGroup _upgradeGroup;
        private Coroutine _sheetRoutine;

        public event Action HomeClicked;
        public event Action<string> EquipmentTapped;
        public event Action<string> UpgradeClicked;

        public bool IsVisible => _root != null && _root.activeSelf;
        public bool IsSheetOpen => _sheet != null && _sheet.gameObject.activeSelf;
        public string SheetEquipmentId { get; private set; }
        public string SheetLevelText => _sheetLevel != null ? _sheetLevel.text : string.Empty;
        public bool UpgradeButtonInteractable => _upgradeButton != null && _upgradeButton.gameObject.activeSelf && _upgradeButton.interactable;
        public int ShownResearch => _research != null ? _research.ShownValue : 0;
        public string IncubatorStatus => _incubatorStatus != null ? _incubatorStatus.text : string.Empty;
        public string AnalyzerStatus => _analyzerStatus != null ? _analyzerStatus.text : string.Empty;

        public void Initialize(PuzzleTheme theme)
        {
            _theme = theme;
            Build();
        }

        public void Show(LabState state, float enterFromScale = 1.1f)
        {
            ScreenCanvas.ApplyCamera(_theme);
            _root.SetActive(true);
            CloseSheet(immediate: true);
            Apply(state, animateResearch: false);
            Tween.Stop(this, ref _enter);
            _enter = ScreenTransition.Enter(this, _group, _content, enterFromScale, 0.38f);

            if (state.RevealIncubator) PlayReveal(_incubatorSlot, 0.45f);
            if (state.RevealAnalyzer) PlayReveal(_analyzerSlot, 0.6f);
        }

        public void Hide()
        {
            CloseSheet(immediate: true);
            _root.SetActive(false);
        }

        /// <summary>Redraws equipment and balance (after an upgrade, reward, dev action...).</summary>
        public void Apply(LabState state, bool animateResearch)
        {
            bool microscopeChanged = state.MicroscopeLevel != _state.MicroscopeLevel || _microscopeSlot.childCount == 0;
            bool incubatorChanged = state.IncubatorUnlocked != _state.IncubatorUnlocked || _incubatorSlot.childCount == 0;
            bool analyzerChanged = state.AnalyzerUnlocked != _state.AnalyzerUnlocked || _analyzerSlot.childCount == 0;
            _state = state;

            _research.SetValue(state.Research, animateResearch);
            _labLevel.text = $"LAB LEVEL {state.LabLevel}";

            if (microscopeChanged)
            {
                UIFactory.DestroyChildren(_microscopeSlot);
                LabArt.Microscope(_microscopeSlot, _theme, state.MicroscopeLevel, 640f);
            }
            if (incubatorChanged)
            {
                UIFactory.DestroyChildren(_incubatorSlot);
                LabArt.Incubator(_incubatorSlot, _theme, state.IncubatorUnlocked, 290f);
            }
            if (analyzerChanged)
            {
                UIFactory.DestroyChildren(_analyzerSlot);
                LabArt.Analyzer(_analyzerSlot, _theme, state.AnalyzerUnlocked, 290f);
            }

            _microscopeStatus.text = state.MicroscopeLevel >= state.MicroscopeMaxLevel ? "Max level" : $"Level {state.MicroscopeLevel}";
            _incubatorStatus.text = state.IncubatorUnlocked ? "Ready for future research" : "Complete Experiment 01";
            _analyzerStatus.text = state.AnalyzerUnlocked ? "Ready for future research" : $"Reach Lab Level {LabEquipment.AnalyzerLabLevel}";

            BuildDecorations(state);
            if (IsSheetOpen) FillSheet(SheetEquipmentId);
        }

        /// <summary>Short bounce + glow + a few particles on the microscope after a successful upgrade.</summary>
        public void PlayUpgrade()
        {
            var slot = _microscopeSlot;
            Tween.Run(this, 0.55f, Ease.Linear, t =>
            {
                float w = Ease.Wobble(t) * 0.1f;
                slot.localScale = new Vector3(1f + w, 1f - w * 0.8f, 1f);
            }, () => slot.localScale = Vector3.one);

            var holder = (RectTransform)slot.parent;
            var glow = UIFactory.CreateSpriteImage("UpgradeGlow", _scene, ProceduralSprites.SoftGlow, new Vector2(820f, 820f),
                new Color(_theme.accentColor.r, _theme.accentColor.g, _theme.accentColor.b, 0f), holder.anchoredPosition);
            glow.transform.SetSiblingIndex(holder.GetSiblingIndex()); // just behind the microscope
            Tween.Run(this, 0.8f, Ease.Linear, t =>
            {
                float a = t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 0.75f;
                glow.color = new Color(_theme.accentColor.r, _theme.accentColor.g, _theme.accentColor.b, 0.55f * a);
            }, () => Destroy(glow.gameObject));

            for (int i = 0; i < 3; i++)
                _particles.BubbleBurst(slot.position, _theme.GetPieceColor(i), 4, 360f, 26f, 90f);
        }

        // ---------------------------------------------------------------- sheet

        public void OpenSheet(string equipmentId)
        {
            SheetEquipmentId = equipmentId;
            FillSheet(equipmentId);
            Tween.Stop(this, ref _sheetRoutine);
            _sheet.gameObject.SetActive(true);
            _sheetGroup.alpha = 0f;
            _sheetRoutine = Tween.Run(this, 0.28f, Ease.Linear, t =>
            {
                _sheetGroup.alpha = Ease.OutCubic(Mathf.Clamp01(t * 1.5f));
                _sheetPanel.anchoredPosition = new Vector2(0f, Mathf.Lerp(-120f, 0f, Ease.OutBackSoft(t)));
            });
        }

        public void CloseSheet(bool immediate = false)
        {
            Tween.Stop(this, ref _sheetRoutine);
            if (_sheet == null) return;
            if (immediate || !_sheet.gameObject.activeSelf)
            {
                _sheet.gameObject.SetActive(false);
                SheetEquipmentId = null;
                return;
            }
            _sheetRoutine = Tween.Run(this, 0.16f, Ease.Linear, t =>
            {
                _sheetGroup.alpha = 1f - t;
                _sheetPanel.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, -80f, t));
            }, () =>
            {
                _sheet.gameObject.SetActive(false);
                SheetEquipmentId = null;
            });
        }

        private void FillSheet(string id)
        {
            _upgradeButton.gameObject.SetActive(false);
            _costRow.gameObject.SetActive(false);

            if (id == LabEquipment.Microscope.Id)
            {
                _sheetTitle.text = "MICROSCOPE";
                _sheetLevel.text = $"Level {_state.MicroscopeLevel}";
                if (_state.UpgradeCost < 0)
                {
                    _sheetBody.text = "Fully upgraded. A precision instrument for your lab.";
                    return;
                }

                bool affordable = _state.Research >= _state.UpgradeCost;
                _sheetBody.text = affordable
                    ? "Upgrade your research equipment."
                    : $"Cure more samples to earn Research. {_state.UpgradeCost - _state.Research} to go.";
                _costRow.gameObject.SetActive(true);
                _costText.text = $"{_state.UpgradeCost} Research";
                _upgradeButton.gameObject.SetActive(true);
                _upgradeButton.interactable = affordable;
                _upgradeGroup.alpha = affordable ? 1f : 0.45f;
                return;
            }

            if (id == LabEquipment.IncubatorId)
            {
                _sheetTitle.text = "INCUBATOR";
                _sheetLevel.text = _state.IncubatorUnlocked ? "Unlocked" : "Dormant";
                _sheetBody.text = _state.IncubatorUnlocked ? "READY FOR FUTURE RESEARCH" : "Complete Experiment 01 to wake it up.";
                return;
            }

            _sheetTitle.text = "ANALYZER";
            _sheetLevel.text = _state.AnalyzerUnlocked ? "Unlocked" : "Locked";
            _sheetBody.text = _state.AnalyzerUnlocked
                ? "READY FOR FUTURE RESEARCH"
                : $"Reach Lab Level {LabEquipment.AnalyzerLabLevel} by upgrading the Microscope.";
        }

        // ---------------------------------------------------------------- build

        private void Build()
        {
            _root = new GameObject("Lab");
            _root.transform.SetParent(transform, false);
            var canvasRoot = ScreenCanvas.Create("LabCanvas", _root.transform, referenceResolution, sortingOrder: 0);
            _group = canvasRoot.gameObject.AddComponent<CanvasGroup>();
            ScreenCanvas.AddBackground(canvasRoot, _theme);
            var safe = ScreenCanvas.AddSafeArea(canvasRoot);
            _content = safe;

            BuildHeader(safe);

            var area = UIFactory.CreateStretched("SceneArea", safe);
            area.offsetMin = new Vector2(0f, 96f);
            area.offsetMax = new Vector2(0f, -236f);
            _scene = UIFactory.CreateRect("Scene", area);
            // Designed for a tall phone: the room uses the full height instead of a squat centred box.
            _scene.gameObject.AddComponent<FitToParentScaler>().Configure(new Vector2(1080f, 1760f), 1.15f);
            BuildRoom(_scene);

            var hint = UIFactory.CreateText("Hint", safe, "Tap equipment to inspect it", 30, _theme.textSecondaryColor);
            var hintRt = hint.rectTransform;
            hintRt.anchorMin = new Vector2(0f, 0f);
            hintRt.anchorMax = new Vector2(1f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.sizeDelta = new Vector2(0f, 80f);
            hintRt.anchoredPosition = new Vector2(0f, 10f);

            _particles = UIFactory.CreateStretched("Particles", canvasRoot).gameObject.AddComponent<UIParticles>();
            _particles.Initialize(30);
            BuildSheet(canvasRoot);
            _root.SetActive(false);
        }

        private void BuildHeader(RectTransform safe)
        {
            var header = UIFactory.CreateRect("Header", safe);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 236f);

            var back = UIButtons.CreatePill("HomeButton", header, "HOME", new Vector2(200f, 96f), Vector2.zero,
                _theme.buttonColor, _theme.buttonTextColor, 32);
            var backRt = (RectTransform)back.transform;
            backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
            backRt.anchoredPosition = new Vector2(140f, -68f);
            back.onClick.AddListener(() => HomeClicked?.Invoke());

            var counterRt = UIFactory.CreateRect("Research", header);
            counterRt.anchorMin = counterRt.anchorMax = new Vector2(1f, 1f);
            counterRt.anchoredPosition = new Vector2(-40f - 125f, -68f);
            _research = counterRt.gameObject.AddComponent<ResearchCounter>();
            _research.Build(_theme.cardColor, _theme.accentColor, _theme.textPrimaryColor, 96f, 40);

            var title = UIFactory.CreateText("Title", header, "LAB", 72, _theme.textPrimaryColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(title.rectTransform, new Vector2(400f, 100f), new Vector2(0f, 0f), top: true, y: -70f);
            _labLevel = UIFactory.CreateText("LabLevel", header, string.Empty, 30, _theme.accentColor, TextAnchor.MiddleCenter,
                FontStyle.Bold);
            Place(_labLevel.rectTransform, new Vector2(500f, 50f), Vector2.zero, top: true, y: -150f);
        }

        private void BuildRoom(RectTransform scene)
        {
            // ---- Back wall: window, spotlight on the microscope, one shelf of authored progress props.
            UIFactory.CreateRoundedImage("Wall", scene, new Vector2(1060f, 1270f), 70f, _theme.labWallColor, new Vector2(0f, 240f));
            UIFactory.CreateSpriteImage("Spotlight", scene, ProceduralSprites.SoftGlow, new Vector2(980f, 980f),
                new Color(1f, 1f, 1f, 0.55f), new Vector2(0f, 60f));

            var frame = Color.Lerp(_theme.labBenchEdgeColor, _theme.metalColor, 0.35f);
            UIFactory.CreateRoundedImage("WindowFrame", scene, new Vector2(330f, 390f), 40f, frame, new Vector2(-300f, 610f));
            UIFactory.CreateRoundedImage("WindowGlass", scene, new Vector2(290f, 350f), 30f,
                Color.Lerp(_theme.backgroundColor, Color.white, 0.65f), new Vector2(-300f, 610f));
            UIFactory.CreateSpriteImage("WindowGlow", scene, ProceduralSprites.SoftGlow, new Vector2(260f, 200f),
                new Color(1f, 1f, 1f, 0.8f), new Vector2(-340f, 680f));
            UIFactory.CreateRoundedImage("WindowBarV", scene, new Vector2(14f, 350f), 7f, frame, new Vector2(-300f, 610f));
            UIFactory.CreateRoundedImage("WindowBarH", scene, new Vector2(290f, 14f), 7f, frame, new Vector2(-300f, 610f));
            UIFactory.CreateRoundedImage("WindowSill", scene, new Vector2(380f, 28f), 14f, _theme.labBenchEdgeColor, new Vector2(-300f, 402f));

            UIFactory.CreateRoundedImage("ShelfShadow", scene, new Vector2(520f, 30f), 15f, new Color(0.1f, 0.2f, 0.24f, 0.12f),
                new Vector2(270f, 368f));
            UIFactory.CreateRoundedImage("Shelf", scene, new Vector2(520f, 30f), 15f, _theme.labBenchEdgeColor, new Vector2(270f, 380f));

            _decor = UIFactory.CreateStretched("Decor", scene);

            // ---- Bench: worktop + cabinet front whose doors carry each object's name and status.
            UIFactory.CreateRoundedImage("BenchFront", scene, new Vector2(1060f, 500f), 50f, _theme.labBenchColor, new Vector2(0f, -620f));
            UIFactory.CreateRoundedImage("BenchTop", scene, new Vector2(1060f, 72f), 36f, _theme.labBenchEdgeColor, new Vector2(0f, -362f));

            _incubatorSlot = EquipmentSlot(scene, "IncubatorObject", new Vector2(-385f, -187f), new Vector2(310f, 300f),
                LabEquipment.IncubatorId, "INCUBATOR", -352f, out _incubatorStatus);
            _microscopeSlot = EquipmentSlot(scene, "MicroscopeObject", new Vector2(0f, -8f), new Vector2(460f, 640f),
                LabEquipment.Microscope.Id, "MICROSCOPE", 0f, out _microscopeStatus);
            _analyzerSlot = EquipmentSlot(scene, "AnalyzerObject", new Vector2(385f, -195f), new Vector2(310f, 290f),
                LabEquipment.AnalyzerId, "ANALYZER", 352f, out _analyzerStatus);
        }

        private RectTransform EquipmentSlot(RectTransform scene, string name, Vector2 position, Vector2 hitSize, string id,
            string label, float doorX, out Text status)
        {
            // Cabinet door below the object, labelled.
            var door = Color.Lerp(_theme.labBenchColor, _theme.labBenchEdgeColor, 0.45f);
            UIFactory.CreateRoundedImage($"{label}Door", scene, new Vector2(330f, 400f), 34f, door, new Vector2(doorX, -630f));
            UIFactory.CreateRoundedImage($"{label}Handle", scene, new Vector2(90f, 16f), 8f, _theme.metalColor, new Vector2(doorX, -470f));

            var hit = UIFactory.CreateCentered(name, scene, hitSize, position);
            var slot = UIFactory.CreateCentered("Art", hit, hitSize);
            TapTarget.Attach(hit, slot).Clicked += () =>
            {
                EquipmentTapped?.Invoke(id);
                OpenSheet(id);
            };

            var nameText = UIFactory.CreateText("Name", scene, label, 32, _theme.textPrimaryColor, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(nameText.rectTransform, new Vector2(320f, 48f), new Vector2(doorX, -560f));
            status = UIFactory.CreateText("Status", scene, string.Empty, 27, _theme.textSecondaryColor);
            Place(status.rectTransform, new Vector2(300f, 110f), new Vector2(doorX, -640f));
            status.verticalOverflow = VerticalWrapMode.Truncate;
            return slot;
        }

        /// <summary>Authored decorations that appear as the player progresses (no inventory, no placement).</summary>
        private void BuildDecorations(LabState state)
        {
            UIFactory.DestroyChildren(_decor);
            if (state.LabLevel >= 2) Decor(LabArt.PendantLamp(_decor, _theme, 230f), new Vector2(0f, 740f));
            if (state.Cured >= 3) Decor(LabArt.TubeRack(_decor, _theme, 130f), new Vector2(110f, 460f));
            if (state.LabLevel >= 3) Decor(LabArt.Jars(_decor, _theme, 170f), new Vector2(290f, 446f));
            if (state.Cured >= 6) Decor(LabArt.Plant(_decor, _theme, 150f), new Vector2(450f, 470f));
            if (state.ExperimentComplete) Decor(LabArt.Certificate(_decor, _theme, 210f), new Vector2(-300f, 250f));
        }
        private static void Decor(RectTransform rt, Vector2 position) => rt.anchoredPosition = position;

        private void BuildSheet(RectTransform canvasRoot)
        {
            _sheet = UIFactory.CreateStretched("EquipmentSheet", canvasRoot);
            var dim = UIFactory.AddImage(_sheet, new Color(_theme.overlayDimColor.r, _theme.overlayDimColor.g, _theme.overlayDimColor.b, 0.18f),
                raycastTarget: true);
            _sheet.gameObject.AddComponent<Button>().onClick.AddListener(() => CloseSheet());
            dim.GetComponent<Button>().transition = Selectable.Transition.None;
            _sheetGroup = _sheet.gameObject.AddComponent<CanvasGroup>();

            var safe = ScreenCanvas.AddSafeArea(_sheet);
            _sheetPanel = UIFactory.CreateRect("Panel", safe);
            _sheetPanel.anchorMin = new Vector2(0f, 0f);
            _sheetPanel.anchorMax = new Vector2(1f, 0f);
            _sheetPanel.pivot = new Vector2(0.5f, 0f);
            _sheetPanel.sizeDelta = new Vector2(-40f, 560f);
            UIFactory.CreateShadow("Shadow", _sheetPanel, new Vector2(1040f, 560f), 40f, new Color(0f, 0f, 0f, 0.18f), new Vector2(0f, 10f));
            var body = UIFactory.CreateRoundedImage("Body", _sheetPanel, new Vector2(1040f, 560f), 60f, _theme.cardColor);
            body.raycastTarget = true; // taps on the panel don't close it
            UIFactory.CreateRoundedImage("Grip", _sheetPanel, new Vector2(110f, 12f), 6f,
                new Color(_theme.textSecondaryColor.r, _theme.textSecondaryColor.g, _theme.textSecondaryColor.b, 0.3f),
                new Vector2(0f, 250f));

            _sheetTitle = UIFactory.CreateText("Title", _sheetPanel, string.Empty, 50, _theme.textPrimaryColor, TextAnchor.MiddleCenter,
                FontStyle.Bold);
            Place(_sheetTitle.rectTransform, new Vector2(900f, 70f), new Vector2(0f, 185f));
            _sheetLevel = UIFactory.CreateText("Level", _sheetPanel, string.Empty, 34, _theme.accentColor, TextAnchor.MiddleCenter,
                FontStyle.Bold);
            Place(_sheetLevel.rectTransform, new Vector2(900f, 50f), new Vector2(0f, 128f));
            _sheetBody = UIFactory.CreateText("Body", _sheetPanel, string.Empty, 32, _theme.textSecondaryColor);
            Place(_sheetBody.rectTransform, new Vector2(880f, 90f), new Vector2(0f, 55f));

            _costRow = UIFactory.CreateCentered("Cost", _sheetPanel, new Vector2(420f, 60f), new Vector2(0f, -25f));
            ResearchIcon.Create(_costRow, 52f, _theme.accentColor, new Vector2(-120f, 0f));
            _costText = UIFactory.CreateText("Amount", _costRow, string.Empty, 36, _theme.textPrimaryColor, TextAnchor.MiddleLeft,
                FontStyle.Bold);
            Place(_costText.rectTransform, new Vector2(300f, 60f), new Vector2(70f, 0f));

            _upgradeButton = UIButtons.CreatePill("UpgradeButton", _sheetPanel, "UPGRADE", new Vector2(520f, 140f),
                new Vector2(0f, -150f), _theme.accentColor, Color.white, 48);
            _upgradeGroup = _upgradeButton.gameObject.AddComponent<CanvasGroup>();
            _upgradeButton.onClick.AddListener(() =>
            {
                if (SheetEquipmentId != null) UpgradeClicked?.Invoke(SheetEquipmentId);
            });

            _sheet.gameObject.SetActive(false);
        }

        private void PlayReveal(RectTransform slot, float delay)
        {
            slot.localScale = new Vector3(0.9f, 0.9f, 1f);
            Tween.Run(this, 0.6f, Ease.Linear, t =>
            {
                float s = Mathf.LerpUnclamped(0.9f, 1f, Ease.OutBack(t));
                slot.localScale = new Vector3(s, s, 1f);
            }, () =>
            {
                slot.localScale = Vector3.one;
                _particles.BubbleBurst(slot.position, _theme.accentColor, 8, 300f, 24f, 80f);
            }, delay);
        }

        private void Update()
        {
            if (_root == null || !_root.activeSelf || Time.unscaledTime < _nextIdle) return;
            _nextIdle = Time.unscaledTime + UnityEngine.Random.Range(3f, 5f);

            // Gentle idle: one piece of working equipment gives a tiny "breath".
            RectTransform pick = _microscopeSlot;
            int r = UnityEngine.Random.Range(0, 3);
            if (r == 1 && _state.IncubatorUnlocked) pick = _incubatorSlot;
            else if (r == 2 && _state.AnalyzerUnlocked) pick = _analyzerSlot;
            var target = pick;
            Tween.Run(this, 0.7f, Ease.Linear, t =>
            {
                float b = Mathf.Sin(t * Mathf.PI) * 0.015f;
                target.localScale = new Vector3(1f - b * 0.5f, 1f + b, 1f);
            }, () => target.localScale = Vector3.one);
        }

        private static void Place(RectTransform rt, Vector2 size, Vector2 position, bool top = false, float y = 0f)
        {
            rt.anchorMin = rt.anchorMax = top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = top ? new Vector2(position.x, y) : position;
        }
    }
}
