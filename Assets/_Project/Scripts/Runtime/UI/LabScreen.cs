using System;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Meta;
using CozyLab.Puzzle.Localization;
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
    /// The optional Lab: a warm, front-on research room whose equipment is the progression. The Lab Level and
    /// the next goal sit in the header, an upgrade tag floats over the Microscope whenever Research allows it,
    /// and tapping any equipment opens its sheet. Purely visual/meta progression — nothing here affects puzzles.
    /// </summary>
    public sealed class LabScreen : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080f, 1920f);

        private const float HeaderHeight = 250f;
        private const float BadgeY = 340f;

        private PuzzleTheme _theme;
        private GameObject _root;
        private CanvasGroup _group;
        private RectTransform _content;
        private ResearchCounter _research;
        private Text _labLevel;
        private Text _goal;
        private Image[] _levelPips;
        private RectTransform _scene;
        private RectTransform _microscopeSlot;
        private RectTransform _incubatorSlot;
        private RectTransform _analyzerSlot;
        private Text _incubatorStatus;
        private Text _analyzerStatus;
        private Text _microscopeStatus;
        private RectTransform _decor;
        private RectTransform _upgradeBadge;
        private Image _upgradeBadgeBody;
        private Text _upgradeBadgeText;
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
        private Image[] _sheetPips;
        private RectTransform _costRow;
        private Text _costText;
        private Button _upgradeButton;
        private CanvasGroup _upgradeGroup;
        private Coroutine _sheetRoutine;

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
        public bool IsUpgradeTagVisible => _upgradeBadge != null && _upgradeBadge.gameObject.activeSelf;

        public void Initialize(PuzzleTheme theme)
        {
            _theme = theme;
            Build();
        }

        public void Show(LabState state, float enterFromScale = 1.06f)
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
            _labLevel.Localize("lab_level", state.LabLevel);
            for (int i = 0; i < _levelPips.Length; i++)
                _levelPips[i].color = i < state.LabLevel ? MetaStyle.Teal : MetaStyle.CreamEdge;
            if (state.UpgradeCost < 0) _goal.Localize("lab_goal_max");
            else if (!state.AnalyzerUnlocked && state.LabLevel + 1 >= LabEquipment.AnalyzerLabLevel) _goal.Localize("lab_goal_analyzer");
            else _goal.Localize("lab_goal_level", state.LabLevel + 1);

            if (microscopeChanged)
            {
                UIFactory.DestroyChildren(_microscopeSlot);
                LabArt.Microscope(_microscopeSlot, _theme, state.MicroscopeLevel, 600f);
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

            if (state.MicroscopeLevel >= state.MicroscopeMaxLevel) _microscopeStatus.Localize("lab_max_level");
            else _microscopeStatus.Localize("lab_equipment_level", state.MicroscopeLevel);
            _incubatorStatus.Localize(state.IncubatorUnlocked ? "lab_ready" : "lab_incubator_locked");
            if (state.AnalyzerUnlocked) _analyzerStatus.Localize("lab_ready");
            else _analyzerStatus.Localize("lab_analyzer_locked", LabEquipment.AnalyzerLabLevel);

            ApplyUpgradeTag(state);
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
                new Color(MetaStyle.Olive.r, MetaStyle.Olive.g, MetaStyle.Olive.b, 0f), holder.anchoredPosition);
            glow.transform.SetSiblingIndex(holder.GetSiblingIndex()); // just behind the microscope
            Tween.Run(this, 0.8f, Ease.Linear, t =>
            {
                float a = t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 0.75f;
                glow.color = new Color(MetaStyle.Olive.r, MetaStyle.Olive.g, MetaStyle.Olive.b, 0.55f * a);
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
                _sheetTitle.Localize("lab_microscope");
                _sheetLevel.Localize("lab_equipment_level", _state.MicroscopeLevel);
                SetPips(_state.MicroscopeLevel, _state.MicroscopeMaxLevel);
                if (_state.UpgradeCost < 0)
                {
                    _sheetBody.Localize("lab_microscope_max_body");
                    return;
                }

                bool affordable = _state.Research >= _state.UpgradeCost;
                if (affordable) _sheetBody.Localize("lab_microscope_body", _state.LabLevel + 1);
                else _sheetBody.Localize("lab_microscope_need", _state.UpgradeCost - _state.Research);
                _costRow.gameObject.SetActive(true);
                _costText.Localize("lab_cost", _state.UpgradeCost);
                _upgradeButton.gameObject.SetActive(true);
                _upgradeButton.interactable = affordable;
                _upgradeGroup.alpha = affordable ? 1f : 0.45f;
                return;
            }

            if (id == LabEquipment.IncubatorId)
            {
                _sheetTitle.Localize("lab_incubator");
                _sheetLevel.Localize(_state.IncubatorUnlocked ? "lab_unlocked" : "lab_dormant");
                SetPips(0, 0);
                _sheetBody.Localize(_state.IncubatorUnlocked ? "lab_ready_caps" : "lab_incubator_locked_body");
                return;
            }

            _sheetTitle.Localize("lab_analyzer");
            _sheetLevel.Localize(_state.AnalyzerUnlocked ? "lab_unlocked" : "lab_locked");
            SetPips(0, 0);
            if (_state.AnalyzerUnlocked) _sheetBody.Localize("lab_ready_caps");
            else _sheetBody.Localize("lab_analyzer_locked_body", LabEquipment.AnalyzerLabLevel);
        }

        private void SetPips(int level, int max)
        {
            for (int i = 0; i < _sheetPips.Length; i++)
            {
                _sheetPips[i].gameObject.SetActive(i < max);
                _sheetPips[i].color = i < level ? MetaStyle.Olive : MetaStyle.CreamEdge;
            }
        }

        private void ApplyUpgradeTag(LabState state)
        {
            bool upgradable = state.UpgradeCost >= 0;
            _upgradeBadge.gameObject.SetActive(upgradable);
            if (!upgradable) return;
            bool affordable = state.Research >= state.UpgradeCost;
            _upgradeBadgeBody.color = affordable ? MetaStyle.Olive : MetaStyle.Cream;
            _upgradeBadgeText.color = affordable ? Color.white : MetaStyle.InkSoft;
            if (affordable) _upgradeBadgeText.Localize("lab_upgrade_tag", state.UpgradeCost);
            else _upgradeBadgeText.SetLiteral($"{Loc.Number(state.Research)} / {Loc.Number(state.UpgradeCost)}");
        }

        // ---------------------------------------------------------------- build

        private void Build()
        {
            _root = new GameObject("Lab");
            _root.transform.SetParent(transform, false);
            var canvasRoot = ScreenCanvas.Create("LabCanvas", _root.transform, referenceResolution, sortingOrder: 0);
            _group = canvasRoot.gameObject.AddComponent<CanvasGroup>();
            MetaStyle.WarmBackground(canvasRoot, MetaStyle.Hex(0xEFE3CF));
            var safe = ScreenCanvas.AddSafeArea(canvasRoot);
            _content = safe;

            BuildHeader(safe);

            var area = UIFactory.CreateStretched("SceneArea", safe);
            area.offsetMin = new Vector2(0f, MetaStyle.NavBarHeight + 64f);
            area.offsetMax = new Vector2(0f, -HeaderHeight);
            _scene = UIFactory.CreateRect("Scene", area);
            // Designed for a tall phone: the room uses the full height instead of a squat centred box.
            _scene.gameObject.AddComponent<FitToParentScaler>().Configure(new Vector2(1080f, 1600f), 1.15f);
            BuildRoom(_scene);

            var hint = UIFactory.CreateText("Hint", safe, string.Empty, 28, MetaStyle.InkSoft);
            hint.Localize("lab_hint");
            var hintRt = hint.rectTransform;
            hintRt.anchorMin = new Vector2(0f, 0f);
            hintRt.anchorMax = new Vector2(1f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.sizeDelta = new Vector2(0f, 50f);
            hintRt.anchoredPosition = new Vector2(0f, MetaStyle.NavBarHeight + 8f);

            _particles = UIFactory.CreateStretched("Particles", canvasRoot).gameObject.AddComponent<UIParticles>();
            _particles.Initialize(30);
            BuildSheet(canvasRoot);
            _root.SetActive(false);
        }

        private void BuildHeader(RectTransform safe)
        {
            var header = MetaStyle.TopBand(safe, HeaderHeight);

            var title = UIFactory.CreateText("Title", header, string.Empty, 72, MetaStyle.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = titleRt.anchorMax = new Vector2(0f, 1f);
            titleRt.pivot = new Vector2(0f, 0.5f);
            titleRt.sizeDelta = new Vector2(600f, 100f); // room for "LABORATUVAR"
            CureUI.FitLabel(title, 72);
            title.Localize("lab_title");
            titleRt.anchoredPosition = new Vector2(56f, -80f);

            var levelTag = UIFactory.CreateRect("LabLevelTag", header);
            levelTag.anchorMin = levelTag.anchorMax = new Vector2(0f, 1f);
            levelTag.pivot = new Vector2(0f, 0.5f);
            levelTag.sizeDelta = new Vector2(380f, 60f);
            levelTag.anchoredPosition = new Vector2(50f, -160f);
            UIFactory.CreateRoundedImage("Body", levelTag, new Vector2(380f, 60f), 30f, MetaStyle.TealLight).rectTransform.anchoredPosition =
                Vector2.zero;
            _labLevel = UIFactory.CreateText("LabLevel", levelTag, string.Empty, 28, MetaStyle.TealDark, TextAnchor.MiddleLeft, FontStyle.Bold);
            CureUI.FitLabel(_labLevel, 28);
            MetaStyle.Place(_labLevel.rectTransform, new Vector2(230f, 60f), new Vector2(-55f, 0f));
            _levelPips = new Image[LabEquipment.Microscope.MaxLevel];
            for (int i = 0; i < _levelPips.Length; i++)
                _levelPips[i] = UIFactory.CreateSpriteImage("Pip", levelTag, ProceduralSprites.Circle, new Vector2(26f, 26f), MetaStyle.CreamEdge,
                    new Vector2(90f + i * 36f, 0f));

            _goal = UIFactory.CreateText("Goal", header, string.Empty, 28, MetaStyle.InkSoft, TextAnchor.MiddleLeft);
            CureUI.FitLabel(_goal, 28);
            var goalRt = _goal.rectTransform;
            goalRt.anchorMin = goalRt.anchorMax = new Vector2(0f, 1f);
            goalRt.pivot = new Vector2(0f, 0.5f);
            goalRt.sizeDelta = new Vector2(980f, 44f);
            goalRt.anchoredPosition = new Vector2(56f, -222f);

            _research = MetaStyle.ResearchChip(header, new Vector2(1f, 1f), new Vector2(-165f, -85f));
        }

        private void BuildRoom(RectTransform scene)
        {
            // ---- Back wall with a warm window, a shelf of authored progress props and a soft spotlight.
            var wall = MetaStyle.Hex(0xF6EBD8);
            UIFactory.CreateRoundedImage("Wall", scene, new Vector2(1060f, 1150f), 70f, wall, new Vector2(0f, 220f));
            UIFactory.CreateRoundedImage("Wainscot", scene, new Vector2(1060f, 260f), 40f, MetaStyle.Hex(0xE9D8BC), new Vector2(0f, -210f));
            UIFactory.CreateSpriteImage("Spotlight", scene, ProceduralSprites.SoftGlow, new Vector2(980f, 980f),
                new Color(1f, 0.96f, 0.86f, 0.8f), new Vector2(0f, 60f));

            var frame = MetaStyle.Wood;
            UIFactory.CreateRoundedImage("WindowFrame", scene, new Vector2(310f, 340f), 40f, frame, new Vector2(-300f, 610f));
            UIFactory.CreateRoundedImage("WindowGlass", scene, new Vector2(270f, 300f), 30f, MetaStyle.Hex(0xFFF1D2), new Vector2(-300f, 610f));
            UIFactory.CreateSpriteImage("Sun", scene, ProceduralSprites.SoftGlow, new Vector2(300f, 260f),
                new Color(1f, 0.85f, 0.55f, 0.85f), new Vector2(-340f, 660f));
            UIFactory.CreateRoundedImage("Leaf", scene, new Vector2(120f, 60f), 30f, MetaStyle.Olive, new Vector2(-235f, 530f))
                .rectTransform.localRotation = Quaternion.Euler(0f, 0f, 30f);
            UIFactory.CreateRoundedImage("WindowBarV", scene, new Vector2(14f, 300f), 7f, frame, new Vector2(-300f, 610f));
            UIFactory.CreateRoundedImage("WindowBarH", scene, new Vector2(270f, 14f), 7f, frame, new Vector2(-300f, 610f));
            UIFactory.CreateRoundedImage("WindowSill", scene, new Vector2(380f, 30f), 14f, MetaStyle.WoodDark, new Vector2(-300f, 426f));

            UIFactory.CreateShadow("ShelfShadow", scene, new Vector2(460f, 30f), 14f, MetaStyle.WarmShadow, new Vector2(300f, 420f));
            UIFactory.CreateRoundedImage("ShelfLip", scene, new Vector2(460f, 30f), 14f, MetaStyle.WoodDark, new Vector2(300f, 422f));
            UIFactory.CreateRoundedImage("Shelf", scene, new Vector2(460f, 26f), 13f, MetaStyle.WoodLight, new Vector2(300f, 434f));

            _decor = UIFactory.CreateStretched("Decor", scene);

            // ---- Bench: wooden worktop + cream cabinet whose doors carry each object's name and status.
            UIFactory.CreateShadow("BenchShadow", scene, new Vector2(1060f, 470f), 30f, MetaStyle.WarmShadow, new Vector2(0f, -600f));
            UIFactory.CreateRoundedImage("BenchFront", scene, new Vector2(1060f, 470f), 50f, MetaStyle.CreamDeep, new Vector2(0f, -585f));
            UIFactory.CreateRoundedImage("BenchTopLip", scene, new Vector2(1080f, 72f), 36f, MetaStyle.WoodDark, new Vector2(0f, -372f));
            UIFactory.CreateRoundedImage("BenchTop", scene, new Vector2(1080f, 64f), 32f, MetaStyle.Wood, new Vector2(0f, -362f));
            UIFactory.CreateRoundedImage("BenchTopShine", scene, new Vector2(1020f, 10f), 5f, new Color(1f, 1f, 1f, 0.25f), new Vector2(0f, -344f));

            _incubatorSlot = EquipmentSlot(scene, "IncubatorObject", new Vector2(-385f, -187f), new Vector2(310f, 300f),
                LabEquipment.IncubatorId, "lab_incubator", -352f, out _incubatorStatus);
            _microscopeSlot = EquipmentSlot(scene, "MicroscopeObject", new Vector2(0f, -28f), new Vector2(460f, 600f),
                LabEquipment.Microscope.Id, "lab_microscope", 0f, out _microscopeStatus);
            _analyzerSlot = EquipmentSlot(scene, "AnalyzerObject", new Vector2(385f, -195f), new Vector2(310f, 290f),
                LabEquipment.AnalyzerId, "lab_analyzer", 352f, out _analyzerStatus);

            // Floating upgrade tag over the microscope (tap = open its sheet).
            _upgradeBadge = UIFactory.CreateCentered("UpgradeBadge", scene, new Vector2(330f, 84f), new Vector2(0f, BadgeY));
            UIFactory.CreateShadow("Shadow", _upgradeBadge, new Vector2(330f, 84f), 16f, MetaStyle.WarmShadow, new Vector2(0f, -8f));
            var badgeVisual = UIFactory.CreateCentered("Visual", _upgradeBadge, new Vector2(330f, 84f));
            _upgradeBadgeBody = UIFactory.CreateRoundedImage("Body", badgeVisual, new Vector2(330f, 84f), 42f, MetaStyle.Olive);
            var pointer = UIFactory.CreateRoundedImage("Pointer", badgeVisual, new Vector2(34f, 34f), 6f, MetaStyle.Olive, new Vector2(0f, -40f));
            pointer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            pointer.transform.SetAsFirstSibling();
            ResearchIcon.Create(badgeVisual, 56f, MetaStyle.Teal, new Vector2(-122f, 2f));
            _upgradeBadgeText = UIFactory.CreateText("Text", badgeVisual, string.Empty, 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            CureUI.FitLabel(_upgradeBadgeText, 30);
            MetaStyle.Place(_upgradeBadgeText.rectTransform, new Vector2(250f, 84f), new Vector2(30f, 0f));
            TapTarget.Attach(_upgradeBadge, badgeVisual).Clicked += () =>
            {
                EquipmentTapped?.Invoke(LabEquipment.Microscope.Id);
                OpenSheet(LabEquipment.Microscope.Id);
            };
        }

        private RectTransform EquipmentSlot(RectTransform scene, string name, Vector2 position, Vector2 hitSize, string id,
            string labelKey, float doorX, out Text status)
        {
            // Cabinet door below the object with a small name plate.
            UIFactory.CreateRoundedImage($"{name}DoorEdge", scene, new Vector2(330f, 380f), 34f, MetaStyle.CreamEdge, new Vector2(doorX, -616f));
            UIFactory.CreateRoundedImage($"{name}Door", scene, new Vector2(330f, 372f), 34f, MetaStyle.Cream, new Vector2(doorX, -612f));
            UIFactory.CreateRoundedImage($"{name}Plate", scene, new Vector2(290f, 150f), 26f, MetaStyle.Paper, new Vector2(doorX, -575f));
            UIFactory.CreateRoundedImage($"{name}Handle", scene, new Vector2(90f, 16f), 8f, MetaStyle.WoodDark, new Vector2(doorX, -740f));

            var hit = UIFactory.CreateCentered(name, scene, hitSize, position);
            var slot = UIFactory.CreateCentered("Art", hit, hitSize);
            TapTarget.Attach(hit, slot).Clicked += () =>
            {
                EquipmentTapped?.Invoke(id);
                OpenSheet(id);
            };

            var nameText = UIFactory.CreateText("Name", scene, string.Empty, 31, MetaStyle.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            CureUI.FitLabel(nameText, 31);
            nameText.Localize(labelKey);
            MetaStyle.Place(nameText.rectTransform, new Vector2(300f, 46f), new Vector2(doorX, -540f));
            status = UIFactory.CreateText("Status", scene, string.Empty, 26, MetaStyle.InkSoft);
            MetaStyle.Place(status.rectTransform, new Vector2(270f, 80f), new Vector2(doorX, -600f));
            status.verticalOverflow = VerticalWrapMode.Truncate;
            return slot;
        }

        /// <summary>Authored decorations that appear as the player progresses (no inventory, no placement).</summary>
        private void BuildDecorations(LabState state)
        {
            UIFactory.DestroyChildren(_decor);
            if (state.LabLevel >= 2) Decor(LabArt.PendantLamp(_decor, _theme, 230f), new Vector2(60f, 720f));
            if (state.Cured >= 3) Decor(LabArt.TubeRack(_decor, _theme, 130f), new Vector2(150f, 513f));
            if (state.LabLevel >= 3) Decor(LabArt.Jars(_decor, _theme, 170f), new Vector2(310f, 500f));
            if (state.Cured >= 6) Decor(LabArt.Plant(_decor, _theme, 150f), new Vector2(460f, 523f));
            if (state.ExperimentComplete) Decor(LabArt.Certificate(_decor, _theme, 210f), new Vector2(-300f, 210f));
        }
        private static void Decor(RectTransform rt, Vector2 position) => rt.anchoredPosition = position;

        private void BuildSheet(RectTransform canvasRoot)
        {
            _sheet = UIFactory.CreateStretched("EquipmentSheet", canvasRoot);
            UIFactory.AddImage(_sheet, new Color(0.22f, 0.15f, 0.09f, 0.3f), raycastTarget: true);
            var dimButton = _sheet.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(() => CloseSheet());
            _sheetGroup = _sheet.gameObject.AddComponent<CanvasGroup>();
            MetaStyle.RaiseSorting(_sheet.gameObject, 20); // covers the tab bar

            var safe = ScreenCanvas.AddSafeArea(_sheet);
            _sheetPanel = UIFactory.CreateRect("Panel", safe);
            _sheetPanel.anchorMin = new Vector2(0f, 0f);
            _sheetPanel.anchorMax = new Vector2(1f, 0f);
            _sheetPanel.pivot = new Vector2(0.5f, 0f);
            _sheetPanel.sizeDelta = new Vector2(-40f, 620f);
            var card = MetaStyle.Card("Card", _sheetPanel, new Vector2(1040f, 600f), new Vector2(0f, 10f), 60f);
            card.Find("Face").GetComponent<Image>().raycastTarget = true; // taps on the panel don't close it
            UIFactory.CreateRoundedImage("Grip", card, new Vector2(110f, 12f), 6f,
                new Color(MetaStyle.InkSoft.r, MetaStyle.InkSoft.g, MetaStyle.InkSoft.b, 0.3f), new Vector2(0f, 270f));

            _sheetTitle = UIFactory.CreateText("Title", card, string.Empty, 50, MetaStyle.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            MetaStyle.Place(_sheetTitle.rectTransform, new Vector2(900f, 70f), new Vector2(0f, 205f));
            _sheetLevel = UIFactory.CreateText("Level", card, string.Empty, 32, MetaStyle.OliveDark, TextAnchor.MiddleCenter,
                FontStyle.Bold);
            MetaStyle.Place(_sheetLevel.rectTransform, new Vector2(900f, 48f), new Vector2(0f, 148f));
            _sheetPips = new Image[3];
            for (int i = 0; i < _sheetPips.Length; i++)
                _sheetPips[i] = UIFactory.CreateSpriteImage("Pip", card, ProceduralSprites.Circle, new Vector2(24f, 24f), MetaStyle.CreamEdge,
                    new Vector2((i - 1) * 40f, 106f));
            _sheetBody = UIFactory.CreateText("Body", card, string.Empty, 32, MetaStyle.InkSoft);
            MetaStyle.Place(_sheetBody.rectTransform, new Vector2(880f, 90f), new Vector2(0f, 40f));

            _costRow = UIFactory.CreateCentered("Cost", card, new Vector2(360f, 64f), new Vector2(0f, -40f));
            UIFactory.CreateRoundedImage("Body", _costRow, new Vector2(360f, 64f), 32f, MetaStyle.CreamDeep);
            ResearchIcon.Create(_costRow, 50f, MetaStyle.Teal, new Vector2(-130f, 0f));
            _costText = UIFactory.CreateText("Amount", _costRow, string.Empty, 32, MetaStyle.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            MetaStyle.Place(_costText.rectTransform, new Vector2(260f, 64f), new Vector2(40f, 0f));

            _upgradeButton = MetaStyle.Button("UpgradeButton", card, "lab_upgrade", new Vector2(520f, 140f), new Vector2(0f, -170f),
                MetaStyle.ButtonKind.Accent, 48);
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
                _particles.BubbleBurst(slot.position, MetaStyle.Teal, 8, 300f, 24f, 80f);
            }, delay);
        }

        private void Update()
        {
            if (_root == null || !_root.activeSelf) return;

            // The upgrade tag bobs gently when an upgrade is affordable.
            if (_upgradeBadge.gameObject.activeSelf)
            {
                bool affordable = _state.UpgradeCost >= 0 && _state.Research >= _state.UpgradeCost;
                float bob = affordable ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.6f)) * 10f : 0f;
                _upgradeBadge.anchoredPosition = new Vector2(0f, BadgeY + bob);
            }

            if (Time.unscaledTime < _nextIdle) return;
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
    }
}
