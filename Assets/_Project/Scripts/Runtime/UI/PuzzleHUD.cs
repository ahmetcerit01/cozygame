using System;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>What the HUD shows for the current level. Filled by the screen, not by gameplay.</summary>
    public struct PuzzleHudInfo
    {
        public string LevelLabel;     // e.g. "LEVEL 4"
        public string Subtitle;       // level name or tutorial hint
        public string ContinueLabel;  // success primary button, e.g. "NEXT LEVEL" / "FINISH"
    }

    /// <summary>Back button, title, progress, Undo / Restart buttons and the success overlay.</summary>
    public sealed class PuzzleHUD : MonoBehaviour
    {
        private Text _progress;
        private Button _undoButton;
        private CanvasGroup _undoGroup;
        private Button _restartButton;
        private Button _backButton;
        private bool _undoAvailable;
        private bool _inputLocked;
        private RectTransform _overlay;
        private CanvasGroup _overlayGroup;
        private RectTransform _card;
        private Coroutine _overlayRoutine;

        public event Action UndoClicked;
        public event Action RestartClicked;
        public event Action BackClicked;
        public event Action ContinueClicked;
        public event Action LevelsClicked;

        public void Build(RectTransform topBar, RectTransform bottomBar, RectTransform overlayParent, PuzzleTheme theme,
            PuzzleHudInfo info)
        {
            // --- Top bar: back, level label, subtitle, progress.
            _backButton = UIButtons.CreatePill("BackButton", topBar, "LEVELS", new Vector2(200f, 96f), Vector2.zero,
                theme.buttonColor, theme.buttonTextColor, 32);
            var backRt = (RectTransform)_backButton.transform;
            backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
            backRt.anchoredPosition = new Vector2(40f + 100f, -68f);
            _backButton.onClick.AddListener(() => BackClicked?.Invoke());

            var title = UIFactory.CreateText("Title", topBar, info.LevelLabel, 60, theme.textPrimaryColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            SetBand(title.rectTransform, 1f, 0.58f, 250f);

            var subtitle = UIFactory.CreateText("Subtitle", topBar, info.Subtitle, 36, theme.textSecondaryColor);
            SetBand(subtitle.rectTransform, 0.58f, 0.3f, 60f);

            _progress = UIFactory.CreateText("Progress", topBar, string.Empty, 30, theme.accentColor, TextAnchor.MiddleCenter,
                FontStyle.Bold);
            SetBand(_progress.rectTransform, 0.3f, 0.06f, 60f);

            // --- Bottom bar: Undo + Restart.
            _undoButton = UIButtons.CreatePill("UndoButton", bottomBar, theme.undoLabel, new Vector2(400f, 132f),
                new Vector2(-220f, 0f), theme.buttonColor, theme.buttonTextColor);
            _undoButton.onClick.AddListener(() => UndoClicked?.Invoke());
            _undoGroup = _undoButton.gameObject.AddComponent<CanvasGroup>();

            _restartButton = UIButtons.CreatePill("RestartButton", bottomBar, theme.restartLabel, new Vector2(400f, 132f),
                new Vector2(220f, 0f), theme.buttonColor, theme.buttonTextColor);
            _restartButton.onClick.AddListener(() => RestartClicked?.Invoke());

            BuildOverlay(overlayParent, theme, info);
        }

        public void SetProgress(int placed, int total)
        {
            _progress.text = $"{placed} / {total} placed";
        }

        public void SetUndoInteractable(bool interactable)
        {
            _undoAvailable = interactable;
            RefreshButtons();
        }

        /// <summary>Briefly blocks the HUD buttons (e.g. during the completion sequence).</summary>
        public void SetInputLocked(bool locked)
        {
            _inputLocked = locked;
            RefreshButtons();
        }

        public bool IsSuccessVisible => _overlay != null && _overlay.gameObject.activeSelf;

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

            var title = UIFactory.CreateText("Title", _card, theme.successTitle, 100, theme.accentColor,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            PlaceCentered(title.rectTransform, new Vector2(800f, 150f), new Vector2(0f, 180f));
            var titleShadow = title.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.12f);
            titleShadow.effectDistance = new Vector2(0f, -5f);

            var subtitle = UIFactory.CreateText("Subtitle", _card, theme.successSubtitle, 36, theme.textSecondaryColor);
            PlaceCentered(subtitle.rectTransform, new Vector2(800f, 100f), new Vector2(0f, 65f));

            var next = UIButtons.CreatePill("ContinueButton", _card, info.ContinueLabel, new Vector2(540f, 136f),
                new Vector2(0f, -95f), theme.accentColor, Color.white);
            next.onClick.AddListener(() => ContinueClicked?.Invoke());

            var levels = UIButtons.CreatePill("LevelsButton", _card, "LEVELS", new Vector2(320f, 96f),
                new Vector2(0f, -240f), theme.buttonColor, theme.buttonTextColor, 34, shadow: false);
            levels.onClick.AddListener(() => LevelsClicked?.Invoke());

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
