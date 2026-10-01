using System;
using CozyLab.Puzzle.Localization;
using CozyLab.Puzzle.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.UI
{
    public enum MetaTab
    {
        Experiments,
        Home,
        Lab,
    }

    /// <summary>
    /// Persistent bottom tab bar shared by the meta screens (Experiments · Home · Lab). One instance, owned by the
    /// flow, drawn above the screens and hidden during gameplay.
    /// </summary>
    public sealed class MetaNavBar : MonoBehaviour
    {
        private sealed class TabView
        {
            public MetaTab Tab;
            public Button Button;
            public RectTransform Bubble;
            public RectTransform Icon;
            public Text Label;
            public RectTransform Badge;
            public Action<RectTransform, float, Color> DrawIcon;
        }

        private GameObject _root;
        private readonly TabView[] _tabs = new TabView[3];

        public event Action<MetaTab> TabClicked;

        public bool IsVisible => _root != null && _root.activeSelf;
        public MetaTab Active { get; private set; } = MetaTab.Home;

        public void Initialize(Vector2 referenceResolution)
        {
            _root = new GameObject("MetaNav");
            _root.transform.SetParent(transform, false);
            var canvasRoot = ScreenCanvas.Create("NavCanvas", _root.transform, referenceResolution, sortingOrder: 10);
            var safe = ScreenCanvas.AddSafeArea(canvasRoot);

            var bar = UIFactory.CreateRect("Bar", safe);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, MetaStyle.NavBarHeight);

            // The panel runs past the bottom safe edge so the home-indicator strip is covered too.
            var panel = UIFactory.CreateStretched("Panel", bar);
            panel.offsetMin = new Vector2(-10f, -300f);
            panel.offsetMax = new Vector2(10f, 0f);
            var shadow = UIFactory.CreateStretched("Shadow", panel);
            shadow.offsetMax = new Vector2(0f, 26f);
            UIFactory.AddImage(shadow, new Color(0.3f, 0.2f, 0.1f, 0.10f)).sprite = null;
            ProceduralSprites.ApplySoftShadow(shadow.GetComponent<Image>(), 24f);
            var edge = UIFactory.CreateStretched("Edge", panel);
            ProceduralSprites.ApplyRounded(UIFactory.AddImage(edge, MetaStyle.CreamEdge, raycastTarget: true), 56f);
            var face = UIFactory.CreateStretched("Face", panel);
            face.offsetMax = new Vector2(0f, -6f);
            ProceduralSprites.ApplyRounded(UIFactory.AddImage(face, MetaStyle.Cream), 56f);

            _tabs[0] = BuildTab(bar, MetaTab.Experiments, "NavExperiments", "nav_experiments", -340f, MetaIcons.Rack);
            _tabs[1] = BuildTab(bar, MetaTab.Home, "NavHome", "nav_home", 0f, MetaIcons.Home);
            _tabs[2] = BuildTab(bar, MetaTab.Lab, "NavLab", "nav_lab", 340f, MetaIcons.Microscope);
            _root.SetActive(false);
        }

        public void Show(MetaTab active)
        {
            _root.SetActive(true);
            if (Active == active && _tabs[0].Icon.childCount > 0) return;
            Active = active;
            foreach (var tab in _tabs) Apply(tab, tab.Tab == active);
        }

        public void Hide() => _root.SetActive(false);

        public void SetBadge(MetaTab tab, bool visible) => _tabs[(int)tab].Badge.gameObject.SetActive(visible);

        public bool IsBadgeVisible(MetaTab tab) => _tabs[(int)tab].Badge.gameObject.activeSelf;

        private TabView BuildTab(RectTransform bar, MetaTab tab, string name, string labelKey, float x, Action<RectTransform, float, Color> icon)
        {
            var rt = UIFactory.CreateRect(name, bar);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320f, MetaStyle.NavBarHeight);
            rt.anchoredPosition = new Vector2(x, 0f);
            var hit = UIFactory.AddImage(rt, new Color(1f, 1f, 1f, 0f), raycastTarget: true);

            var view = new TabView { Tab = tab, DrawIcon = icon };
            view.Bubble = UIFactory.CreateCentered("Bubble", rt, new Vector2(118f, 118f), new Vector2(0f, 40f));
            UIFactory.CreateSpriteImage("Base", view.Bubble, ProceduralSprites.Circle, new Vector2(118f, 118f), MetaStyle.TealDark,
                new Vector2(0f, -8f));
            UIFactory.CreateSpriteImage("Face", view.Bubble, ProceduralSprites.Circle, new Vector2(118f, 118f), MetaStyle.Teal);
            UIFactory.CreateSpriteImage("Sheen", view.Bubble, ProceduralSprites.SoftGlow, new Vector2(70f, 36f),
                new Color(1f, 1f, 1f, 0.4f), new Vector2(-12f, 30f));

            view.Icon = UIFactory.CreateCentered("Icon", rt, new Vector2(64f, 64f), new Vector2(0f, 22f));
            view.Label = UIFactory.CreateText("Label", rt, string.Empty, 26, MetaStyle.InkSoft, TextAnchor.MiddleCenter, FontStyle.Bold);
            CureUI.FitLabel(view.Label, 26);
            view.Label.Localize(labelKey);
            MetaStyle.Place(view.Label.rectTransform, new Vector2(320f, 40f), new Vector2(0f, -56f));
            view.Badge = MetaStyle.Badge(rt, 44f, new Vector2(44f, 70f));
            view.Badge.gameObject.SetActive(false);

            view.Button = rt.gameObject.AddComponent<Button>();
            view.Button.targetGraphic = hit;
            view.Button.transition = Selectable.Transition.None;
            view.Button.navigation = new Navigation { mode = Navigation.Mode.None };
            view.Button.onClick.AddListener(() => TabClicked?.Invoke(tab));
            rt.gameObject.AddComponent<PressScale>();
            return view;
        }

        private static void Apply(TabView view, bool active)
        {
            view.Bubble.gameObject.SetActive(active);
            view.Icon.anchoredPosition = new Vector2(0f, active ? 40f : 22f);
            view.Icon.sizeDelta = Vector2.one * (active ? 70f : 64f);
            UIFactory.DestroyChildren(view.Icon);
            view.DrawIcon(view.Icon, active ? 70f : 64f, active ? Color.white : MetaStyle.InkSoft);
            view.Label.color = active ? MetaStyle.TealDark : MetaStyle.InkSoft;
            view.Label.rectTransform.anchoredPosition = new Vector2(0f, active ? -52f : -48f);
        }
    }

    /// <summary>
    /// Small modal for placeholder entry points (Shop, Daily Reward) and Settings, built on the kit's
    /// <see cref="ModalPanel"/>. Settings shows a language switch (English / Türkçe) that applies instantly.
    /// </summary>
    public sealed class MetaDialog : MonoBehaviour
    {
        private ModalPanel _panel;
        private RectTransform _iconBack;
        private RectTransform _icon;
        private Text _body;
        private RectTransform _languageRow;
        private Button[] _languageButtons;

        public bool IsVisible => _panel != null && _panel.IsOpen;
        public string Title => _panel != null ? _panel.TitleText : string.Empty;
        public string Body => _body != null ? _body.text : string.Empty;
        public bool IsLanguageRowVisible => _languageRow != null && _languageRow.gameObject.activeSelf;

        /// <summary>Raised when the player picks a language in Settings (after it is applied).</summary>
        public event Action<string> LanguageSelected;

        public void Initialize(Vector2 referenceResolution)
        {
            var root = new GameObject("MetaDialog");
            root.transform.SetParent(transform, false);
            var canvasRoot = ScreenCanvas.Create("DialogCanvas", root.transform, referenceResolution, sortingOrder: 30);
            _panel = CureUI.Modal("Dialog", canvasRoot, new Vector2(860f, 860f), null);
            var content = _panel.Content;

            _iconBack = UIFactory.CreateRect("IconBack", content);
            _iconBack.anchorMin = _iconBack.anchorMax = new Vector2(0.5f, 1f);
            _iconBack.sizeDelta = new Vector2(180f, 180f);
            _iconBack.anchoredPosition = new Vector2(0f, -100f);
            UIFactory.CreateSpriteImage("Disc", _iconBack, ProceduralSprites.Circle, new Vector2(180f, 180f),
                new Color(CureTokens.CreamRim.r, CureTokens.CreamRim.g, CureTokens.CreamRim.b, 0.8f));
            _icon = UIFactory.CreateCentered("Icon", _iconBack, new Vector2(110f, 110f));

            _body = CureUI.BodyText("Body", content, null);
            var bodyRt = _body.rectTransform;
            bodyRt.anchorMin = bodyRt.anchorMax = new Vector2(0.5f, 1f);
            bodyRt.sizeDelta = new Vector2(700f, 150f);
            bodyRt.anchoredPosition = new Vector2(0f, -290f);
            _body.color = CureTokens.TextSoft;

            BuildLanguageRow(content);

            var ok = CureUI.PrimaryButton("DialogOkButton", content, "common_ok", new Vector2(420f, 128f), Vector2.zero, 48);
            var okRt = (RectTransform)ok.transform;
            okRt.anchorMin = okRt.anchorMax = new Vector2(0.5f, 0f);
            okRt.anchoredPosition = new Vector2(0f, 90f);
            ok.onClick.AddListener(Close);
        }

        /// <summary>Opens a notice. <paramref name="icon"/> draws into a 110-unit square (or null).</summary>
        public void Show(string titleKey, string bodyKey, Action<RectTransform, float, Color> icon)
        {
            _panel.SetTitle(titleKey);
            _body.Localize(bodyKey);
            UIFactory.DestroyChildren(_icon);
            _iconBack.gameObject.SetActive(icon != null);
            icon?.Invoke(_icon, 110f, CureTokens.Teal);
            _languageRow.gameObject.SetActive(false);
            _panel.Open();
        }

        /// <summary>Settings: the placeholder notice plus the language switch.</summary>
        public void ShowSettings(Action<RectTransform, float, Color> icon)
        {
            Show("settings_title", "settings_coming_soon", icon);
            _iconBack.gameObject.SetActive(false);
            _languageRow.gameObject.SetActive(true);
            RefreshLanguageButtons();
        }

        public void Close()
        {
            if (_panel != null) _panel.Close();
        }

        private void BuildLanguageRow(RectTransform content)
        {
            _languageRow = UIFactory.CreateRect("LanguageRow", content);
            _languageRow.anchorMin = _languageRow.anchorMax = new Vector2(0.5f, 1f);
            _languageRow.sizeDelta = new Vector2(740f, 220f);
            _languageRow.anchoredPosition = new Vector2(0f, -110f);

            var label = CureUI.BodyText("Label", _languageRow, "settings_language", CureTokens.BodySize + 4);
            label.fontStyle = FontStyle.Bold;
            MetaStyle.Place(label.rectTransform, new Vector2(700f, 50f), new Vector2(0f, 70f));

            _languageButtons = new Button[Loc.Languages.Length];
            float width = 320f;
            for (int i = 0; i < Loc.Languages.Length; i++)
            {
                var language = Loc.Languages[i];
                float x = (i - (Loc.Languages.Length - 1) * 0.5f) * (width + 30f);
                // Each language is always shown in its own name ("English", "Türkçe"), so no key.
                var button = CureUI.SecondaryButton("LanguageButton_" + language.Code, _languageRow, null, new Vector2(width, 112f),
                    new Vector2(x, -30f), 40);
                UIButtons.SetLabel(button, language.NativeName);
                string code = language.Code;
                button.onClick.AddListener(() =>
                {
                    Loc.SetLanguage(code);
                    RefreshLanguageButtons();
                    LanguageSelected?.Invoke(code);
                });
                _languageButtons[i] = button;
            }
            _languageRow.gameObject.SetActive(false);
        }

        private void RefreshLanguageButtons()
        {
            for (int i = 0; i < _languageButtons.Length; i++)
            {
                bool selected = Loc.Languages[i].Code == Loc.Current;
                var group = _languageButtons[i].GetComponent<CanvasGroup>();
                if (group == null) group = _languageButtons[i].gameObject.AddComponent<CanvasGroup>();
                group.alpha = selected ? 1f : 0.55f;
                var label = _languageButtons[i].GetComponentInChildren<Text>();
                label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
            }
        }
    }
}
