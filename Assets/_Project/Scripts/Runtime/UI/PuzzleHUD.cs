using System;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Localization;
using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>What the HUD shows for the current level. Filled by the screen, not by gameplay.</summary>
    public struct PuzzleHudInfo
    {
        public int LevelNumber;       // shown as "LEVEL 4"
        public LocString Subtitle;    // level name or tutorial hint
        public bool IsLastLevel;      // success button: FINISH instead of NEXT LEVEL
    }

    /// <summary>Back button, title, progress, Undo / Restart buttons and the success overlay.</summary>
    public sealed class PuzzleHUD : MonoBehaviour
    {
        private Text _progress;
        private Button _undoButton;
        private CanvasGroup _undoGroup;
        private RectTransform _undoBadge;
        private Text _undoBadgeText;
        private Button _restartButton;
        private Button _backButton;
        private bool _undoAvailable;
        private int _undoCount;
        private bool _inputLocked;
        private RectTransform _overlay;
        private CanvasGroup _overlayGroup;
        private RectTransform _card;
        private Coroutine _overlayRoutine;
        private RectTransform _reward;
        private Text _rewardText;
        private int _pendingResearch;

        public event Action UndoClicked;
        public event Action RestartClicked;
        public event Action BackClicked;
        public event Action ContinueClicked;
        public event Action LevelsClicked;

        public void Build(RectTransform topBar, RectTransform bottomBar, RectTransform overlayParent, PuzzleTheme theme,
            PuzzleHudInfo info)
        {
            // --- Top bar: back, level label, subtitle, progress.
            _backButton = UIButtons.CreatePill("BackButton", topBar, string.Empty, new Vector2(200f, 96f), Vector2.zero,
                theme.buttonColor, theme.buttonTextColor, 32);
            var backRt = (RectTransform)_backButton.transform;
            backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
            backRt.anchoredPosition = new Vector2(40f + 100f, -68f);
            _backButton.onClick.AddListener(() => BackClicked?.Invoke());
            UIButtons.SetLabelKey(_backButton, "gameplay_levels");

            var title = UIFactory.CreateText("Title", topBar, string.Empty, 60, theme.textPrimaryColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            SetBand(title.rectTransform, 1f, 0.58f, 250f);
            title.Localize("gameplay_level", info.LevelNumber);

            var subtitle = UIFactory.CreateText("Subtitle", topBar, string.Empty, 36, theme.textSecondaryColor);
            subtitle.Localize(info.Subtitle);
            SetBand(subtitle.rectTransform, 0.58f, 0.3f, 60f);

            _progress = UIFactory.CreateText("Progress", topBar, string.Empty, 30, theme.accentColor, TextAnchor.MiddleCenter,
                FontStyle.Bold);
            SetBand(_progress.rectTransform, 0.3f, 0.06f, 60f);

            // --- Bottom bar: Undo + Restart.
            _undoButton = UIButtons.CreatePill("UndoButton", bottomBar, string.Empty, new Vector2(400f, 132f),
                new Vector2(-220f, 0f), theme.buttonColor, theme.buttonTextColor);
            _undoButton.onClick.AddListener(() => UndoClicked?.Invoke());
            UIButtons.SetLabelKey(_undoButton, "gameplay_undo");
            _undoGroup = _undoButton.gameObject.AddComponent<CanvasGroup>();
            BuildUndoBadge(theme);

            _restartButton = UIButtons.CreatePill("RestartButton", bottomBar, string.Empty, new Vector2(400f, 132f),
                new Vector2(220f, 0f), theme.buttonColor, theme.buttonTextColor);
            _restartButton.onClick.AddListener(() => RestartClicked?.Invoke());
            UIButtons.SetLabelKey(_restartButton, "gameplay_restart");

            BuildOverlay(overlayParent, theme, info);
        }

        public void SetProgress(int placed, int total)
        {
            _progress.Localize("gameplay_placed", placed, total);
        }

        /// <summary><paramref name="undoCount"/> only drives the small badge; it's not a limited resource
        /// (Undo remains gated purely by <paramref name="interactable"/>, i.e. whether there's history to undo).</summary>
        public void SetUndoState(bool interactable, int undoCount)
        {
            _undoAvailable = interactable;
            _undoCount = Mathf.Max(0, undoCount);
            RefreshButtons();
        }

        /// <summary>Briefly blocks the HUD buttons (e.g. during the completion sequence).</summary>
        public void SetInputLocked(bool locked)
        {
            _inputLocked = locked;
            RefreshButtons();
        }

        public bool IsSuccessVisible => _overlay != null && _overlay.gameObject.activeSelf;
        public bool IsRewardVisible => _reward != null && _reward.gameObject.activeSelf;
        public string RewardText => _rewardText != null ? _rewardText.text : string.Empty;

        /// <summary>
        /// Research earned by this completion, shown as a small badge once the success card appears.
        /// Supplied by the flow; the HUD has no idea where it comes from.
        /// </summary>
        public void SetPendingResearch(int amount)
        {
            _pendingResearch = Mathf.Max(0, amount);
        }

        public void ShowSuccess(float delay)
        {
            Tween.Stop(this, ref _overlayRoutine);
            _overlay.gameObject.SetActive(true);
            _overlayGroup.alpha = 0f;
            _overlayGroup.blocksRaycasts = true;
            _card.localScale = Vector3.one * 0.6f;

            _overlayRoutine = Tween.Run(this, 0.5f, Ease.Linear, t =>
            {
                _overlayGroup.alpha = Ease.OutCubic(Mathf.Clamp01(t * 1.6f));
                float s = Mathf.LerpUnclamped(0.6f, 1f, Ease.OutBack(t));
                _card.localScale = new Vector3(s, s, 1f);
            }, null, delay);

            _reward.gameObject.SetActive(false);
            if (_pendingResearch > 0) ShowReward(_pendingResearch, delay + 0.4f);
        }

        private void ShowReward(int amount, float delay)
        {
            _rewardText.SetLiteral(Loc.Format("level_complete_research", 0));
            _reward.localScale = Vector3.zero;
            Tween.Run(this, 0.6f, Ease.Linear, t =>
            {
                _reward.gameObject.SetActive(true);
                float s = Mathf.LerpUnclamped(0f, 1f, Ease.OutBack(Mathf.Clamp01(t * 1.8f)));
                _reward.localScale = new Vector3(s, s, 1f);
                _rewardText.SetLiteral(Loc.Format("level_complete_research", Mathf.RoundToInt(amount * Ease.OutCubic(t))));
            }, () => _rewardText.Localize("level_complete_research", amount), delay);
        }

        public void HideSuccess()
        {
            Tween.Stop(this, ref _overlayRoutine);
            if (_overlay == null) return;
            _overlayGroup.blocksRaycasts = false;
            _overlay.gameObject.SetActive(false);
        }

        private void RefreshButtons()
        {
            bool undo = _undoAvailable && !_inputLocked;
            _undoButton.interactable = undo;
            _undoGroup.alpha = undo ? 1f : 0.45f;
            _restartButton.interactable = !_inputLocked;
            _backButton.interactable = !_inputLocked;

            bool showBadge = _undoCount > 0;
            _undoBadge.gameObject.SetActive(showBadge);
            if (showBadge) _undoBadgeText.text = _undoCount.ToString();
        }

        private void BuildUndoBadge(PuzzleTheme theme)
        {
            _undoBadge = UIFactory.CreateCentered("UndoBadge", _undoButton.transform, new Vector2(56f, 56f),
                new Vector2(172f, 50f));
            UIFactory.CreateShadow("Shadow", _undoBadge, new Vector2(56f, 56f), 10f,
                new Color(0.1f, 0.18f, 0.22f, 0.16f), new Vector2(0f, -4f));
            UIFactory.CreateRoundedImage("Body", _undoBadge, new Vector2(56f, 56f), 28f, theme.accentColor);
            _undoBadgeText = UIFactory.CreateText("Count", _undoBadge, "0", 30, Color.white, TextAnchor.MiddleCenter,
                FontStyle.Bold);
            UIFactory.Stretch(_undoBadgeText.rectTransform);
            _undoBadge.gameObject.SetActive(false);
        }

        private void BuildOverlay(RectTransform parent, PuzzleTheme theme, PuzzleHudInfo info)
        {
            _overlay = UIFactory.CreateStretched("SuccessOverlay", parent);
            UIFactory.AddImage(_overlay, theme.overlayDimColor, raycastTarget: true);
            _overlayGroup = _overlay.gameObject.AddComponent<CanvasGroup>();

            // Card lives in a safe-area child so it never hides under the notch.
            var safe = UIFactory.CreateStretched("Safe", _overlay);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            // Sits low on screen so the solved dish stays visible above it.
            var cardSize = new Vector2(860f, 660f);
            _card = UIFactory.CreateCentered("Card", safe, cardSize, new Vector2(0f, -430f));
            UIFactory.CreateShadow("Shadow", _card, cardSize, 60f, new Color(0f, 0f, 0f, 0.25f), new Vector2(0f, -24f));
            UIFactory.CreateRoundedImage("Body", _card, cardSize, 84f, theme.cardColor);

            UIFactory.CreateSpriteImage("Glow", _card, ProceduralSprites.SoftGlow, new Vector2(640f, 340f),
                new Color(theme.accentColor.r, theme.accentColor.g, theme.accentColor.b, 0.35f), new Vector2(0f, 175f));

            var title = UIFactory.CreateText("Title", _card, string.Empty, 100, theme.accentColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            PlaceCentered(title.rectTransform, new Vector2(800f, 150f), new Vector2(0f, 180f));
            CureUI.FitLabel(title, 100);
            title.Localize(new LocString("level_complete_title", theme.successTitle));
            var titleShadow = title.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.12f);
            titleShadow.effectDistance = new Vector2(0f, -5f);

            var subtitle = UIFactory.CreateText("Subtitle", _card, string.Empty, 36, theme.textSecondaryColor);
            subtitle.Localize(new LocString("level_complete_subtitle", theme.successSubtitle));
            PlaceCentered(subtitle.rectTransform, new Vector2(800f, 100f), new Vector2(0f, 65f));

            var next = UIButtons.CreatePill("ContinueButton", _card, string.Empty, new Vector2(540f, 136f),
                new Vector2(0f, -95f), theme.accentColor, Color.white);
            next.onClick.AddListener(() => ContinueClicked?.Invoke());
            UIButtons.SetLabelKey(next, info.IsLastLevel ? "level_complete_finish" : "level_complete_next");

            var levels = UIButtons.CreatePill("LevelsButton", _card, string.Empty, new Vector2(320f, 96f),
                new Vector2(0f, -240f), theme.buttonColor, theme.buttonTextColor, 34, shadow: false);
            levels.onClick.AddListener(() => LevelsClicked?.Invoke());
            UIButtons.SetLabelKey(levels, "gameplay_levels");

            // Research badge riding on the card's top edge (doesn't cover CURE FOUND!).
            _reward = UIFactory.CreateCentered("ResearchReward", _card, new Vector2(380f, 84f), new Vector2(0f, cardSize.y * 0.5f));
            UIFactory.CreateShadow("Shadow", _reward, new Vector2(380f, 84f), 16f, new Color(0.1f, 0.18f, 0.22f, 0.16f),
                new Vector2(0f, -6f));
            UIFactory.CreateRoundedImage("Body", _reward, new Vector2(380f, 84f), 42f, Color.white);
            ResearchIcon.Create(_reward, 58f, theme.accentColor, new Vector2(-138f, 0f));
            _rewardText = UIFactory.CreateText("Amount", _reward, string.Empty, 34, theme.textPrimaryColor, TextAnchor.MiddleLeft,
                FontStyle.Bold);
            PlaceCentered(_rewardText.rectTransform, new Vector2(280f, 84f), new Vector2(45f, 0f));
            _reward.gameObject.SetActive(false);

            _overlay.gameObject.SetActive(false);
        }

        /// <summary>Stretches horizontally and occupies a vertical band given as normalized parent heights.</summary>
        private static void SetBand(RectTransform rt, float top, float bottom, float sideInset)
        {
            rt.anchorMin = new Vector2(0f, bottom);
            rt.anchorMax = new Vector2(1f, top);
            rt.offsetMin = new Vector2(sideInset, 0f);
            rt.offsetMax = new Vector2(-sideInset, 0f);
        }

        private static void PlaceCentered(RectTransform rt, Vector2 size, Vector2 position)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }
    }
}
