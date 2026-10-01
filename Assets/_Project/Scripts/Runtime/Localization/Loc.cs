using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace CozyLab.Puzzle.Localization
{
    /// <summary>A supported language: its code (table file name), its own name and the culture used for casing/numbers.</summary>
    public readonly struct LanguageInfo
    {
        public readonly string Code;
        public readonly string NativeName;
        public readonly string CultureName;

        public LanguageInfo(string code, string nativeName, string cultureName)
        {
            Code = code;
            NativeName = nativeName;
            CultureName = cultureName;
        }
    }

    /// <summary>Where the chosen language is remembered between launches.</summary>
    public interface ILanguageStore
    {
        /// <summary>The saved language code, or null/empty if the player never chose one.</summary>
        string Load();
        void Save(string code);
    }

    /// <summary>Device-level preference (not part of the progress save).</summary>
    public sealed class PlayerPrefsLanguageStore : ILanguageStore
    {
        public const string DefaultKey = "cozylab.language";
        private readonly string _key;

        public PlayerPrefsLanguageStore(string key = DefaultKey) => _key = key;

        public string Load() => PlayerPrefs.GetString(_key, string.Empty);

        public void Save(string code)
        {
            PlayerPrefs.SetString(_key, code);
            PlayerPrefs.Save();
        }
    }

    /// <summary>In-memory store for tests.</summary>
    public sealed class MemoryLanguageStore : ILanguageStore
    {
        public string Saved { get; private set; }
        public MemoryLanguageStore(string saved = null) => Saved = saved;
        public string Load() => Saved;
        public void Save(string code) => Saved = code;
    }

    /// <summary>
    /// A piece of text to localize later: a table key plus the text to show if no table has the key
    /// (used for content such as level titles, whose source text lives in data assets).
    /// </summary>
    public readonly struct LocString
    {
        public readonly string Key;
        public readonly string Fallback;

        public LocString(string key, string fallback = null)
        {
            Key = key;
            Fallback = fallback;
        }

        public override string ToString() => Loc.Resolve(this);
    }

    /// <summary>
    /// Cure Cells localization: one UTF-8 string table per language in <c>Resources/Localization/&lt;code&gt;.txt</c>
    /// (<c>key = value</c> lines). English is the fallback for missing keys. The first launch follows the device
    /// language (Turkish → tr, anything else → en); a choice made in Settings is saved and wins from then on.
    /// Changing the language raises <see cref="LanguageChanged"/> so bound texts update instantly.
    /// To add a language: add its table file and one <see cref="Languages"/> entry.
    /// </summary>
    public static class Loc
    {
        public const string FallbackLanguage = "en";
        public const string TablePath = "Localization/";

        public static readonly LanguageInfo[] Languages =
        {
            new LanguageInfo("en", "English", "en-US"),
            new LanguageInfo("tr", "Türkçe", "tr-TR"),
        };

        private static ILanguageStore _store;
        private static Func<string, string> _loadTable;
        private static Dictionary<string, string> _current = new Dictionary<string, string>();
        private static Dictionary<string, string> _fallback = new Dictionary<string, string>();
        private static readonly HashSet<string> ReportedMissing = new HashSet<string>();

        /// <summary>Raised after the language changes (bound texts refresh themselves).</summary>
        public static event Action LanguageChanged;

        public static bool IsInitialized { get; private set; }
        public static string Current { get; private set; } = FallbackLanguage;
        public static CultureInfo Culture { get; private set; } = CultureInfo.InvariantCulture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsInitialized = false;
            _store = null;
            _loadTable = null;
            LanguageChanged = null;
            ReportedMissing.Clear();
        }

        /// <param name="store">Where the player's choice is kept.</param>
        /// <param name="systemLanguage">Device language, used only when nothing was saved yet.</param>
        /// <param name="loadTable">Returns a table's raw text for a language code (default: Resources).</param>
        public static void Initialize(ILanguageStore store, SystemLanguage systemLanguage, Func<string, string> loadTable = null)
        {
            _store = store ?? new MemoryLanguageStore();
            _loadTable = loadTable ?? LoadFromResources;
            _fallback = LoadTable(FallbackLanguage);
            ReportedMissing.Clear();
            IsInitialized = true;

            string saved = _store.Load();
            string code = IsSupported(saved) ? saved : DetectLanguage(systemLanguage);
            Apply(code, raiseEvent: true);
        }

        /// <summary>Initializes with the device language and PlayerPrefs if nothing did so yet.</summary>
        public static void EnsureInitialized()
        {
            if (!IsInitialized) Initialize(new PlayerPrefsLanguageStore(), Application.systemLanguage);
        }

        public static string DetectLanguage(SystemLanguage systemLanguage) =>
            systemLanguage == SystemLanguage.Turkish ? "tr" : FallbackLanguage;

        public static bool IsSupported(string code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            foreach (var language in Languages)
            {
                if (language.Code == code) return true;
            }
            return false;
        }

        public static LanguageInfo CurrentInfo
        {
            get
            {
                foreach (var language in Languages)
                {
                    if (language.Code == Current) return language;
                }
                return Languages[0];
            }
        }

        /// <summary>Switches language instantly and remembers the choice. Returns false for unsupported codes.</summary>
        public static bool SetLanguage(string code)
        {
            EnsureInitialized();
            if (!IsSupported(code)) return false;
            _store.Save(code);
            if (code != Current) Apply(code, raiseEvent: true);
            return true;
        }

        // ---------------------------------------------------------------- lookup

        public static bool Has(string key) => key != null && (Table.ContainsKey(key) || _fallback.ContainsKey(key));

        /// <summary>The text for <paramref name="key"/>: current language, else English, else the key itself (logged once).</summary>
        public static string Get(string key)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (_current.TryGetValue(key, out var value)) return value;
            if (_fallback.TryGetValue(key, out value))
            {
                ReportMissing(key, Current);
                return value;
            }
            ReportMissing(key, FallbackLanguage);
            return key;
        }

        /// <summary>Like <see cref="Get"/>, but returns <paramref name="fallback"/> (not the key) when no table has it.</summary>
        public static string GetOr(string key, string fallback)
        {
            EnsureInitialized();
            if (!string.IsNullOrEmpty(key))
            {
                if (_current.TryGetValue(key, out var value)) return value;
                if (_fallback.TryGetValue(key, out value)) return value;
            }
            return fallback ?? key ?? string.Empty;
        }

        public static string Resolve(LocString text) =>
            text.Fallback != null ? GetOr(text.Key, text.Fallback) : Get(text.Key);

        /// <summary><see cref="Get"/> + <c>string.Format</c> in the current culture. <see cref="LocString"/> args are resolved too.</summary>
        public static string Format(string key, params object[] args)
        {
            string pattern = Get(key);
            if (args == null || args.Length == 0) return pattern;
            var resolved = new object[args.Length];
            for (int i = 0; i < args.Length; i++) resolved[i] = args[i] is LocString s ? Resolve(s) : args[i];
            try
            {
                return string.Format(Culture, pattern, resolved);
            }
            catch (FormatException)
            {
                Debug.LogWarning($"[Loc] Bad format for '{key}' in '{Current}': {pattern}");
                return pattern;
            }
        }

        /// <summary>Upper-case in the current language (Turkish: i → İ, ı → I).</summary>
        public static string Upper(string text) => string.IsNullOrEmpty(text) ? text ?? string.Empty : text.ToUpper(Culture);

        /// <summary>Whole number with the current language's grouping (1,240 / 1.240).</summary>
        public static string Number(int value) => value.ToString("N0", Culture);

        // ---------------------------------------------------------------- content keys

        public static LocString LevelTitle(string levelId, string fallback) => new LocString($"level_title_{levelId}", fallback);
        public static LocString LevelHint(string levelId, string fallback) => new LocString($"level_hint_{levelId}", fallback);
        public static LocString ChapterTitle(string chapterId, string fallback) => new LocString($"chapter_title_{chapterId}", fallback);
        public static LocString ChapterSubtitle(string chapterId, string fallback) => new LocString($"chapter_subtitle_{chapterId}", fallback);
        public static LocString ChapterCompleteTitle(string chapterId, string fallback) =>
            new LocString($"chapter_complete_title_{chapterId}", fallback);
        public static LocString ChapterCompleteMessage(string chapterId, string fallback) =>
            new LocString($"chapter_complete_message_{chapterId}", fallback);

        // ---------------------------------------------------------------- tables

        /// <summary>The keys of a language's table (for completeness checks).</summary>
        public static IReadOnlyCollection<string> KeysOf(string code)
        {
            EnsureInitialized();
            return LoadTable(code).Keys;
        }

        private static Dictionary<string, string> Table => _current;

        private static void Apply(string code, bool raiseEvent)
        {
            Current = code;
            _current = code == FallbackLanguage ? _fallback : LoadTable(code);
            Culture = CreateCulture(code);
            if (raiseEvent) LanguageChanged?.Invoke();
        }

        private static Dictionary<string, string> LoadTable(string code)
        {
            string raw = _loadTable?.Invoke(code);
            if (raw == null)
            {
                Debug.LogWarning($"[Loc] No string table for '{code}' at Resources/{TablePath}{code}.");
                return new Dictionary<string, string>();
            }
            var errors = new List<string>();
            var table = LocTableParser.Parse(raw, errors);
            foreach (var error in errors) Debug.LogWarning($"[Loc] {code}: {error}");
            return table;
        }

        private static string LoadFromResources(string code)
        {
            var asset = Resources.Load<TextAsset>(TablePath + code);
            return asset != null ? asset.text : null;
        }

        private static CultureInfo CreateCulture(string code)
        {
            foreach (var language in Languages)
            {
                if (language.Code != code) continue;
                try
                {
                    return CultureInfo.GetCultureInfo(language.CultureName);
                }
                catch (CultureNotFoundException)
                {
                    break;
                }
            }
            return CultureInfo.InvariantCulture;
        }

        private static void ReportMissing(string key, string language)
        {
            if (ReportedMissing.Add(language + ":" + key))
                Debug.LogWarning(language == FallbackLanguage
                    ? $"[Loc] Missing key '{key}' (no table has it)."
                    : $"[Loc] Missing '{language}' translation for '{key}'; using English.");
        }
    }

    /// <summary>
    /// Parses a string table: one <c>key = value</c> per line, <c>#</c> comments, blank lines ignored.
    /// Values may contain <c>=</c>; <c>\n</c> is a line break. UTF-8 (Turkish characters are fine).
    /// </summary>
    public static class LocTableParser
    {
        public static Dictionary<string, string> Parse(string text, List<string> errors = null)
        {
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return table;
            if (text[0] == '﻿') text = text.Substring(1);

            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    errors?.Add($"line {i + 1}: expected 'key = value'");
                    continue;
                }
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim().Replace("\\n", "\n");
                if (table.ContainsKey(key)) errors?.Add($"line {i + 1}: duplicate key '{key}'");
                table[key] = value;
            }
            return table;
        }
    }
}
