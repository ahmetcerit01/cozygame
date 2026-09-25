using System.Collections.Generic;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    /// <summary>
    /// A large tactile petri dish representing one experiment. Its ring of sample "colonies" is the progress
    /// indicator: one colony per level, filled with that level's colour once cured.
    /// </summary>
    public sealed class ExperimentDishView : MonoBehaviour
    {
        private PuzzleTheme _theme;
        private float _size;
        private RectTransform _visual;
        private Image _completeGlow;
        private readonly List<RectTransform> _colonies = new List<RectTransform>();
        private readonly List<GameObject> _filled = new List<GameObject>();
        private readonly List<GameObject> _empty = new List<GameObject>();
        private UIParticles _bubbles;
        private Text _title;
        private Text _status;
        private int _cured;
        private float _nextIdle;
        private Coroutine _entrance;

        public int Cured => _cured;
        public int ColonyCount => _colonies.Count;
        public string StatusText => _status != null ? _status.text : string.Empty;

        public void Build(PuzzleTheme theme, float size, int colonyCount, string title, bool showLabels, bool ambient)
        {
            _theme = theme;
            _size = size;
            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(size, size);

            // Soft contact shadow on the desk.
            UIFactory.CreateSpriteImage("Shadow", root, ProceduralSprites.SoftGlow, new Vector2(size * 1.18f, size * 1.12f),
                new Color(0.1f, 0.2f, 0.24f, 0.22f), new Vector2(size * 0.02f, -size * 0.05f));

            _visual = UIFactory.CreateCentered("Visual", root, new Vector2(size, size));
            _completeGlow = UIFactory.CreateSpriteImage("CompleteGlow", _visual, ProceduralSprites.SoftGlow,
                new Vector2(size * 1.35f, size * 1.35f), new Color(theme.accentColor.r, theme.accentColor.g, theme.accentColor.b, 0f));

            // Glass dish: rim, agar, inner lip, shine.
            UIFactory.CreateSpriteImage("Rim", _visual, ProceduralSprites.Circle, new Vector2(size, size), theme.boardRimColor);
            UIFactory.CreateSpriteImage("RimShine", _visual, ProceduralSprites.Ring, new Vector2(size * 0.97f, size * 0.97f),
                new Color(1f, 1f, 1f, 0.55f));
            UIFactory.CreateSpriteImage("Agar", _visual, ProceduralSprites.Circle, new Vector2(size * 0.9f, size * 0.9f),
                Color.Lerp(theme.wellColor, Color.white, 0.55f));
            UIFactory.CreateSpriteImage("Lip", _visual, ProceduralSprites.Ring, new Vector2(size * 0.9f, size * 0.9f),
                new Color(theme.wellInnerShadowColor.r, theme.wellInnerShadowColor.g, theme.wellInnerShadowColor.b, 0.9f));
            UIFactory.CreateSpriteImage("Shine", _visual, ProceduralSprites.SoftGlow, new Vector2(size * 0.42f, size * 0.2f),
                new Color(1f, 1f, 1f, 0.75f), new Vector2(-size * 0.2f, size * 0.33f));

            // Colonies around the dish, starting at the top, clockwise.
            // Colonies ride near the rim so the centre stays clear for the title and progress text.
            float radius = size * 0.355f;
            float colony = size * (colonyCount > 10 ? 0.09f : 0.115f);
            for (int i = 0; i < colonyCount; i++)
            {
                float angle = Mathf.PI * 0.5f - i * Mathf.PI * 2f / colonyCount;
                var pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                var slot = UIFactory.CreateCentered($"Colony_{i + 1}", _visual, new Vector2(colony, colony), pos);

                var empty = UIFactory.CreateSpriteImage("Empty", slot, ProceduralSprites.Ring, new Vector2(colony * 0.8f, colony * 0.8f),
                    new Color(theme.wellInnerShadowColor.r, theme.wellInnerShadowColor.g, theme.wellInnerShadowColor.b, 0.95f));

                var filled = UIFactory.CreateCentered("Filled", slot, new Vector2(colony, colony));
                var color = theme.GetPieceColor(i);
                UIFactory.CreateSpriteImage("Shadow", filled, ProceduralSprites.SoftGlow, new Vector2(colony * 1.35f, colony * 1.35f),
                    new Color(0.1f, 0.15f, 0.2f, 0.25f), new Vector2(0f, -colony * 0.1f));
                UIFactory.CreateSpriteImage("Body", filled, ProceduralSprites.Circle, new Vector2(colony, colony), color);
                UIFactory.CreateSpriteImage("Core", filled, ProceduralSprites.Circle, new Vector2(colony * 0.62f, colony * 0.62f),
                    Color.Lerp(color, Color.white, 0.25f));
                UIFactory.CreateSpriteImage("Highlight", filled, ProceduralSprites.SoftGlow, new Vector2(colony * 0.5f, colony * 0.34f),
                    new Color(1f, 1f, 1f, 0.6f), new Vector2(-colony * 0.14f, colony * 0.2f));
                UIFactory.CreateSpriteImage("Dot", filled, ProceduralSprites.Circle, new Vector2(colony * 0.14f, colony * 0.14f),
                    Color.Lerp(color, new Color(0.2f, 0.22f, 0.32f), 0.4f), new Vector2(colony * 0.12f, -colony * 0.1f));

                _colonies.Add(slot);
                _filled.Add(filled.gameObject);
                _empty.Add(empty.gameObject);
            }

            if (showLabels)
            {
                _title = UIFactory.CreateText("Title", _visual, title.ToUpperInvariant(), Mathf.RoundToInt(size * 0.056f),
                    theme.textPrimaryColor, TextAnchor.MiddleCenter, FontStyle.Bold);
                Place(_title.rectTransform, new Vector2(size * 0.5f, size * 0.1f), new Vector2(0f, size * 0.04f));
                _status = UIFactory.CreateText("Status", _visual, string.Empty, Mathf.RoundToInt(size * 0.046f), theme.accentColor,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                Place(_status.rectTransform, new Vector2(size * 0.57f, size * 0.09f), new Vector2(0f, -size * 0.05f));
            }

            if (ambient)
            {
                _bubbles = UIFactory.CreateStretched("Bubbles", _visual).gameObject.AddComponent<UIParticles>();
                _bubbles.Initialize(8);
            }
            _nextIdle = Time.unscaledTime + 2.5f;
        }

        /// <summary>Shows <paramref name="cured"/> filled colonies. Colonies from <paramref name="animateFrom"/> pop in.</summary>
        public void SetProgress(int cured, int animateFrom = -1)
        {
            _cured = Mathf.Clamp(cured, 0, _colonies.Count);
            bool complete = _colonies.Count > 0 && _cured == _colonies.Count;

            for (int i = 0; i < _colonies.Count; i++)
            {
                bool on = i < _cured;
                _filled[i].SetActive(on);
                _empty[i].SetActive(!on);
                _colonies[i].localScale = Vector3.one;
                if (on && animateFrom >= 0 && i >= animateFrom) PopColony(i, 0.25f + (i - animateFrom) * 0.12f);
            }

            if (_status != null)
                _status.text = complete ? "EXPERIMENT COMPLETE" : $"{_cured} / {_colonies.Count} CURED";

            var c = _theme.accentColor;
            _completeGlow.color = new Color(c.r, c.g, c.b, complete ? 0.35f : 0f);
        }

        /// <summary>Short settle-in; the screen is fully interactive during it.</summary>
        public void PlayEntrance()
        {
            Tween.Stop(this, ref _entrance);
            _entrance = Tween.Run(this, 0.4f, Ease.Linear, t =>
            {
                float s = Mathf.LerpUnclamped(0.94f, 1f, Ease.OutBackSoft(t));
                _visual.localScale = new Vector3(s, s, 1f);
            }, () => _visual.localScale = Vector3.one);
        }

        private void PopColony(int index, float delay)
        {
            var colony = _colonies[index];
            colony.localScale = Vector3.zero;
            Tween.Run(this, 0.4f, Ease.Linear, t =>
            {
                float s = Mathf.LerpUnclamped(0f, 1f, Ease.OutBack(t));
                colony.localScale = new Vector3(s, s, 1f);
            }, () => colony.localScale = Vector3.one, delay);
            if (_bubbles != null)
                Tween.Run(this, 0.01f, Ease.Linear, null,
                    () => _bubbles.BubbleBurst(colony.position, _theme.GetPieceColor(index), 5, _size * 0.35f, _size * 0.035f, 4f),
                    delay + 0.1f);
        }

        private void Update()
        {
            if (_bubbles == null || Time.unscaledTime < _nextIdle) return;
            _nextIdle = Time.unscaledTime + Random.Range(3.5f, 6f);

            // Very subtle life: one cured colony wobbles and releases a single tiny bubble.
            if (_cured == 0) return;
            int i = Random.Range(0, _cured);
            var colony = _colonies[i];
            Tween.Run(this, 0.5f, Ease.Linear, t =>
            {
                float w = Ease.Wobble(t) * 0.07f;
                colony.localScale = new Vector3(1f + w, 1f - w, 1f);
            }, () => colony.localScale = Vector3.one);
            var tint = Color.Lerp(_theme.GetPieceColor(i), Color.white, 0.5f);
            tint.a = 0.7f;
            _bubbles.Emit(colony.position, new Vector2(Random.Range(-8f, 8f), 20f), _size * 0.022f, _size * 0.03f, 1.6f,
                tint, ProceduralSprites.Ring, buoyancy: 18f);
        }

        private static void Place(RectTransform rt, Vector2 size, Vector2 position)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }
    }
}
