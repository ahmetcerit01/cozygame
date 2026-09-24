using System;
using System.Collections.Generic;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Feedback;
using CozyLab.Puzzle.Presentation;
using CozyLab.Puzzle.UI;
using UnityEngine;

namespace CozyLab.Puzzle.Gameplay
{
    /// <summary>What to open in the puzzle screen. Supplied by the navigation layer.</summary>
    public struct PuzzleScreenRequest
    {
        public LevelDefinition Level;
        public int LevelNumber;      // 1-based, for display
        public bool IsLastLevel;     // changes the success button to "FINISH"
    }

    /// <summary>
    /// Builds the canvas, board, tray and HUD for one level and hands control to <see cref="PuzzleController"/>.
    /// Knows nothing about progression: it only reports what the player did via events.
    /// </summary>
    public sealed class PuzzleScreen : MonoBehaviour
    {
        [SerializeField] private PuzzleTheme theme;
        [Tooltip("Optional. Sounds for pickup / rotate / place / invalid / completion. Empty clips are fine.")]
        [SerializeField] private PuzzleAudioSet audioSet;
        [SerializeField] private bool hapticsEnabled = true;

        [Header("Canvas")]
        [Tooltip("Conventional portrait reference. 'Expand' keeps this whole area visible on any aspect ratio.")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1080f, 1920f);

        [Header("Layout (canvas units)")]
        [SerializeField] private float topBarHeight = 260f;
        [SerializeField] private float bottomBarHeight = 220f;
        [Tooltip("Fixed design box for board + tray; scaled down uniformly if the screen is shorter.")]
        [SerializeField] private Vector2 playAreaDesignSize = new Vector2(1080f, 1600f);
        [SerializeField] private float boardSize = 800f;
        [SerializeField] private float boardGridPadding = 60f;
        [SerializeField] private float boardCenterY = 390f;
        [SerializeField] private Vector2 traySize = new Vector2(1000f, 720f);
        [SerializeField] private float trayCenterY = -400f;
        [SerializeField, Range(0.2f, 1f)] private float maxTrayPieceScale = 0.6f;

        private GameObject _root;

        /// <summary>The puzzle was solved (raised immediately, before the celebration finishes).</summary>
        public event Action<LevelDefinition> Solved;
        /// <summary>Player pressed the success panel's continue button.</summary>
        public event Action ContinueRequested;
        /// <summary>Player asked to go back to level select (top bar or success panel).</summary>
        public event Action LevelSelectRequested;

        public PuzzleController Controller { get; private set; }
        public PuzzleHUD Hud { get; private set; }
        public LevelDefinition CurrentLevel { get; private set; }
        public bool IsOpen => _root != null;
        public PuzzleTheme Theme => theme;

        public void Configure(PuzzleTheme puzzleTheme, PuzzleAudioSet audio)
        {
            if (puzzleTheme != null) theme = puzzleTheme;
            if (audio != null) audioSet = audio;
        }

        /// <summary>Opens a level, replacing whatever was open. Returns false if the level data is invalid.</summary>
        public bool Open(PuzzleScreenRequest request)
        {
            Close();
            if (request.Level == null || theme == null)
            {
                Debug.LogError("[PuzzleScreen] Missing level or theme.", this);
                return false;
            }

            var problems = request.Level.Validate(checkSolvable: false);
            if (problems.Count > 0)
            {
                foreach (var problem in problems)
                    Debug.LogError($"[PuzzleScreen] Level '{request.Level.name}': {problem}", request.Level);
                return false;
            }

            CurrentLevel = request.Level;
            ScreenCanvas.ApplyCamera(theme);
            ScreenCanvas.EnsureEventSystem();
            Build(request);
            return true;
        }

        public void Close()
        {
            if (_root != null)
            {
                _root.SetActive(false);
                Destroy(_root);
            }
            _root = null;
            Controller = null;
            Hud = null;
            CurrentLevel = null;
        }

        private void Build(PuzzleScreenRequest request)
        {
            var level = request.Level;
            _root = new GameObject($"Puzzle_{level.LevelId}");
            _root.transform.SetParent(transform, false);

            var canvasRoot = ScreenCanvas.Create("PuzzleCanvas", _root.transform, referenceResolution, sortingOrder: 0);
            ScreenCanvas.AddBackground(canvasRoot, theme);

            // --- Safe area with top bar / play area / bottom bar
            var safe = ScreenCanvas.AddSafeArea(canvasRoot);

            var topBar = UIFactory.CreateRect("TopBar", safe);
            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = new Vector2(1f, 1f);
            topBar.pivot = new Vector2(0.5f, 1f);
            topBar.sizeDelta = new Vector2(0f, topBarHeight);
            topBar.anchoredPosition = Vector2.zero;

            var bottomBar = UIFactory.CreateRect("BottomBar", safe);
            bottomBar.anchorMin = new Vector2(0f, 0f);
            bottomBar.anchorMax = new Vector2(1f, 0f);
            bottomBar.pivot = new Vector2(0.5f, 0f);
            bottomBar.sizeDelta = new Vector2(0f, bottomBarHeight);
            bottomBar.anchoredPosition = Vector2.zero;

            var playArea = UIFactory.CreateStretched("PlayArea", safe);
            playArea.offsetMin = new Vector2(0f, bottomBarHeight);
            playArea.offsetMax = new Vector2(0f, -topBarHeight);

            var content = UIFactory.CreateRect("Content", playArea);
            content.gameObject.AddComponent<FitToParentScaler>().Configure(playAreaDesignSize, 1f);

            // --- Session + views
            var session = level.CreateSession();

            var boardRt = UIFactory.CreateCentered("Board", content, Vector2.zero, new Vector2(0f, boardCenterY));
            var board = boardRt.gameObject.AddComponent<BoardView>();
            board.Build(session.Grid, theme, boardSize, boardGridPadding);

            var trayRt = UIFactory.CreateCentered("Tray", content, Vector2.zero, new Vector2(0f, trayCenterY));
            var tray = trayRt.gameObject.AddComponent<TrayView>();
            tray.Build(session.Pieces, board.Pitch, traySize, theme, maxTrayPieceScale);

            // Effects sit above the board/tray but below the piece being dragged.
            var fxLayer = UIFactory.CreateStretched("Fx", content);
            var particles = UIFactory.CreateStretched("Particles", fxLayer).gameObject.AddComponent<UIParticles>();
            particles.Initialize(72);
            var combo = UIFactory.CreateStretched("Combos", fxLayer).gameObject.AddComponent<ComboPopup>();
            combo.Initialize(68);

            var dragLayer = UIFactory.CreateStretched("DragLayer", content);

            var feedback = _root.AddComponent<PuzzleFeedback>();
            feedback.Initialize(audioSet);
            feedback.HapticsEnabled = hapticsEnabled;

            // --- HUD (overlay covers the full screen, above everything)
            var overlayRoot = UIFactory.CreateStretched("Overlay", canvasRoot);
            Hud = canvasRoot.gameObject.AddComponent<PuzzleHUD>();
            Hud.Build(topBar, bottomBar, overlayRoot, theme, new PuzzleHudInfo
            {
                LevelLabel = $"LEVEL {request.LevelNumber}",
                Subtitle = string.IsNullOrEmpty(level.Hint) ? level.Title : level.Hint,
                ContinueLabel = request.IsLastLevel ? "FINISH" : "NEXT LEVEL",
            });
            Hud.ContinueClicked += () => ContinueRequested?.Invoke();
            Hud.LevelsClicked += () => LevelSelectRequested?.Invoke();
            Hud.BackClicked += () => LevelSelectRequested?.Invoke();

            var colors = new List<Color>(level.Pieces.Count);
            foreach (var entry in level.Pieces) colors.Add(theme.GetPieceColor(entry.colorIndex));

            Controller = _root.AddComponent<PuzzleController>();
            Controller.Initialize(session, theme, colors, board, tray, dragLayer, Hud, particles, combo, feedback);

            session.Solved += () => Solved?.Invoke(level);
        }
    }
}
