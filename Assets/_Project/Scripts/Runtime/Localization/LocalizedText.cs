using UnityEngine;
using UnityEngine.UI;

namespace CozyLab.Puzzle.Localization
{
    public enum TextCase
    {
        AsIs,
        Upper,
    }

    /// <summary>
    /// Keeps a uGUI <see cref="Text"/> showing a localized key (with optional format args) and re-renders it
    /// whenever the language changes. Screens call <see cref="LocExtensions.Localize"/> instead of assigning text.
    /// Args may be <see cref="LocString"/>s (e.g. level titles); they are re-resolved on every refresh.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        private Text _text;
        private string _key;
        private LocString? _content;
        private object[] _args;
        private TextCase _case;
        private bool _subscribed;

        public string Key => _content.HasValue ? _content.Value.Key : _key;

        public void Set(string key, object[] args, TextCase textCase)
        {
            _key = key;
            _content = null;
            _args = args;
            _case = textCase;
            Refresh();
        }

        public void Set(LocString content, TextCase textCase)
        {
            _key = null;
            _content = content;
            _args = null;
            _case = textCase;
            Refresh();
        }

        /// <summary>Stops tracking the language (the text becomes literal).</summary>
        public void Clear()
        {
            _key = null;
            _content = null;
            _args = null;
        }

        public void Refresh()
        {
            if (_text == null) _text = GetComponent<Text>();
            if (_key == null && !_content.HasValue) return;
            string value = _content.HasValue ? Loc.Resolve(_content.Value) : Loc.Format(_key, _args);
            _text.text = _case == TextCase.Upper ? Loc.Upper(value) : value;
        }

        private void OnEnable()
        {
            if (!_subscribed)
            {
                Loc.LanguageChanged += Refresh;
                _subscribed = true;
            }
            Refresh(); // the language may have changed while this was hidden
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            Loc.LanguageChanged -= Refresh;
            _subscribed = false;
        }
    }

    public static class LocExtensions
    {
        /// <summary>Shows <paramref name="key"/> (formatted with <paramref name="args"/>) and keeps it in sync with the language.</summary>
        public static Text Localize(this Text text, string key, params object[] args)
        {
            Binder(text).Set(key, args, TextCase.AsIs);
            return text;
        }

        public static Text LocalizeUpper(this Text text, string key, params object[] args)
        {
            Binder(text).Set(key, args, TextCase.Upper);
            return text;
        }

        /// <summary>Shows localized content (key with a data-asset fallback), e.g. a level title.</summary>
        public static Text Localize(this Text text, LocString content, TextCase textCase = TextCase.AsIs)
        {
            Binder(text).Set(content, textCase);
            return text;
        }

        /// <summary>Shows text that is not language-dependent (numbers, symbols) and drops any binding.</summary>
        public static Text SetLiteral(this Text text, string value)
        {
            var binder = text.GetComponent<LocalizedText>();
            if (binder != null) binder.Clear();
            text.text = value;
            return text;
        }

        private static LocalizedText Binder(Text text)
        {
            var binder = text.GetComponent<LocalizedText>();
            return binder != null ? binder : text.gameObject.AddComponent<LocalizedText>();
        }
    }
}
