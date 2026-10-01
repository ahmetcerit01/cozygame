using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CozyLab.Puzzle.Localization;
using NUnit.Framework;
using UnityEngine;

namespace CozyLab.Puzzle.Tests
{
    /// <summary>String tables, language selection/persistence, fallback and Turkish text rules.</summary>
    public class LocalizationTests
    {
        private static readonly Regex Placeholder = new Regex(@"\{(\d+)(:[^}]*)?\}");

        [SetUp]
        public void SetUp() => Loc.Initialize(new MemoryLanguageStore("en"), SystemLanguage.English);

        [TearDown]
        public void TearDown() => Loc.Initialize(new MemoryLanguageStore("en"), SystemLanguage.English);

        private static string Raw(string code) => Resources.Load<TextAsset>(Loc.TablePath + code)?.text;

        // ---------------------------------------------------------------- tables

        [Test]
        public void Tables_ExistForEverySupportedLanguage_AndParseCleanly()
        {
            foreach (var language in Loc.Languages)
            {
                string raw = Raw(language.Code);
                Assert.IsNotNull(raw, $"Resources/Localization/{language.Code}.txt");
                var errors = new List<string>();
                var table = LocTableParser.Parse(raw, errors);
                CollectionAssert.IsEmpty(errors, $"{language.Code}: " + string.Join("; ", errors));
                Assert.Greater(table.Count, 50);
                foreach (var pair in table)
                    Assert.IsFalse(string.IsNullOrWhiteSpace(pair.Value), $"{language.Code}: empty value for '{pair.Key}'");
            }
        }

        [Test]
        public void EnglishAndTurkish_HaveIdenticalKeySets()
        {
            var en = new HashSet<string>(Loc.KeysOf("en"));
            var tr = new HashSet<string>(Loc.KeysOf("tr"));
            var missingInTr = en.Except(tr).OrderBy(k => k).ToList();
            var extraInTr = tr.Except(en).OrderBy(k => k).ToList();
            CollectionAssert.IsEmpty(missingInTr, "missing in tr: " + string.Join(", ", missingInTr));
            CollectionAssert.IsEmpty(extraInTr, "only in tr: " + string.Join(", ", extraInTr));
        }

        [Test]
        public void Translations_UseTheSamePlaceholders()
        {
            var en = LocTableParser.Parse(Raw("en"));
            var tr = LocTableParser.Parse(Raw("tr"));
            foreach (var pair in en)
            {
                if (!tr.TryGetValue(pair.Key, out var translated)) continue;
                Assert.AreEqual(Indices(pair.Value), Indices(translated), $"placeholders differ for '{pair.Key}'");
            }
        }

        private static string Indices(string value) =>
            string.Join(",", Placeholder.Matches(value).Cast<Match>().Select(m => m.Groups[1].Value).Distinct().OrderBy(v => v));

        [Test]
        public void RequestedKeys_ArePresent()
        {
            string[] keys =
            {
                "home_continue", "home_experiments", "home_lab", "home_daily", "home_shop", "home_current_sample",
                "gameplay_levels", "gameplay_undo", "gameplay_restart", "gameplay_placed",
                "experiments_title", "experiments_progress", "experiments_play", "experiments_next",
                "lab_title", "lab_upgrade", "lab_level",
                "level_complete_title", "level_complete_research", "level_complete_coins", "level_complete_next",
                "boosters_title", "booster_hint", "booster_auto_cure",
                "settings_title", "settings_music", "settings_sfx", "settings_haptics", "settings_language",
            };
            var en = new HashSet<string>(Loc.KeysOf("en"));
            var tr = new HashSet<string>(Loc.KeysOf("tr"));
            foreach (var key in keys)
            {
                Assert.IsTrue(en.Contains(key), $"en: {key}");
                Assert.IsTrue(tr.Contains(key), $"tr: {key}");
            }
        }

        [Test]
        public void Parser_HandlesCommentsEscapesEqualsAndTurkishCharacters()
        {
            var errors = new List<string>();
            var table = LocTableParser.Parse("﻿# comment\n\na = b = c\nmulti = one\\ntwo\n tr = çğıİöşü ÇĞIÖŞÜ \nbroken line\n", errors);
            Assert.AreEqual("b = c", table["a"]);
            Assert.AreEqual("one\ntwo", table["multi"]);
            Assert.AreEqual("çğıİöşü ÇĞIÖŞÜ", table["tr"]);
            Assert.AreEqual(1, errors.Count, "'broken line' reported");
        }

        // ---------------------------------------------------------------- language choice

        [Test]
        public void DeviceLanguage_TurkishPicksTurkish_OtherwiseEnglish()
        {
            Assert.AreEqual("tr", Loc.DetectLanguage(SystemLanguage.Turkish));
            Assert.AreEqual("en", Loc.DetectLanguage(SystemLanguage.English));
            Assert.AreEqual("en", Loc.DetectLanguage(SystemLanguage.German));
            Assert.AreEqual("en", Loc.DetectLanguage(SystemLanguage.Unknown));
        }

        [Test]
        public void FirstLaunch_FollowsDevice_SavedChoiceWins()
        {
            Loc.Initialize(new MemoryLanguageStore(), SystemLanguage.Turkish);
            Assert.AreEqual("tr", Loc.Current);
            Assert.AreEqual("DEVAM ET", Loc.Get("home_continue"));

            Loc.Initialize(new MemoryLanguageStore("en"), SystemLanguage.Turkish);
            Assert.AreEqual("en", Loc.Current, "a saved choice beats the device language");
            Assert.AreEqual("CONTINUE", Loc.Get("home_continue"));

            Loc.Initialize(new MemoryLanguageStore("xx"), SystemLanguage.Turkish);
            Assert.AreEqual("tr", Loc.Current, "an unknown saved code is ignored");
        }

        [Test]
        public void SwitchingLanguage_BothWays_RaisesEventAndPersists()
        {
            var store = new MemoryLanguageStore("en");
            Loc.Initialize(store, SystemLanguage.English);
            int changes = 0;
            void OnChanged() => changes++;
            Loc.LanguageChanged += OnChanged;
            try
            {
                Assert.IsTrue(Loc.SetLanguage("tr"));
                Assert.AreEqual("tr", Loc.Current);
                Assert.AreEqual("tr", store.Saved);
                Assert.AreEqual("Mağaza", Loc.Get("home_shop"));

                Assert.IsTrue(Loc.SetLanguage("en"));
                Assert.AreEqual("en", store.Saved);
                Assert.AreEqual("Shop", Loc.Get("home_shop"));
                Assert.AreEqual(2, changes);

                Assert.IsFalse(Loc.SetLanguage("de"), "unsupported language rejected");
                Assert.AreEqual("en", Loc.Current);

                // "Restart": a fresh initialization reading the same store keeps the choice.
                Loc.SetLanguage("tr");
                Loc.Initialize(store, SystemLanguage.English);
                Assert.AreEqual("tr", Loc.Current);
            }
            finally
            {
                Loc.LanguageChanged -= OnChanged;
            }
        }

        [Test]
        public void PlayerPrefsStore_RemembersChoiceAcrossInstances()
        {
            string key = "cozylab.test.language." + System.Guid.NewGuid().ToString("N");
            try
            {
                Loc.Initialize(new PlayerPrefsLanguageStore(key), SystemLanguage.English);
                Loc.SetLanguage("tr");
                Assert.AreEqual("tr", new PlayerPrefsLanguageStore(key).Load());
                Loc.Initialize(new PlayerPrefsLanguageStore(key), SystemLanguage.English);
                Assert.AreEqual("tr", Loc.Current, "restored after restart");
            }
            finally
            {
                PlayerPrefs.DeleteKey(key);
            }
        }

        // ---------------------------------------------------------------- fallback

        [Test]
        public void MissingTranslation_FallsBackToEnglish_UnknownKeyShowsKey()
        {
            var tables = new Dictionary<string, string>
            {
                ["en"] = "greeting = Hello\nonly_en = English only\ncount = {0} items",
                ["tr"] = "greeting = Merhaba",
            };
            Loc.Initialize(new MemoryLanguageStore("tr"), SystemLanguage.English, code => tables.TryGetValue(code, out var t) ? t : null);

            Assert.AreEqual("Merhaba", Loc.Get("greeting"));
            Assert.AreEqual("English only", Loc.Get("only_en"), "missing tr key falls back to English");
            Assert.AreEqual("5 items", Loc.Format("count", 5));
            Assert.AreEqual("no_such_key", Loc.Get("no_such_key"), "unknown key is shown as the key");
            Assert.AreEqual("Asset text", Loc.GetOr("no_such_key", "Asset text"), "content falls back to its asset text");
            Assert.AreEqual("Asset title", Loc.Resolve(new LocString("level_title_x", "Asset title")));
        }

        // ---------------------------------------------------------------- Turkish rules

        [Test]
        public void Turkish_UppercaseUsesDottedAndDotlessI()
        {
            Loc.SetLanguage("tr");
            Assert.AreEqual("İSTANBUL ILIK", Loc.Upper("istanbul ılık"));
            Assert.AreEqual("DİKENLİ SUŞ", Loc.Upper(Loc.Get("level_title_lvl_08")));
            Assert.AreEqual("İLK NUMUNE", Loc.Upper(Loc.Get("level_title_lvl_01")));
            Assert.AreEqual("VİRÜS NUMUNELERİ", Loc.Upper(Loc.Get("chapter_subtitle_experiment_01")));

            Loc.SetLanguage("en");
            Assert.AreEqual("ISTANBUL", Loc.Upper("istanbul"), "English keeps the plain I");
            Assert.AreEqual("VIRUS SAMPLES", Loc.Upper(Loc.Get("chapter_subtitle_experiment_01")));
        }

        [Test]
        public void Turkish_AllCapsTableValuesUseCorrectI()
        {
            // Hand-written all-caps Turkish must use İ for dotted i and I only for dotless ı.
            var tr = LocTableParser.Parse(Raw("tr"));
            Assert.AreEqual("SEVİYELER", tr["gameplay_levels"]);
            Assert.AreEqual("GERİ AL", tr["gameplay_undo"]);
            Assert.AreEqual("KİLİTLİ", tr["experiments_locked"]);
            Assert.AreEqual("MİKROSKOP", tr["lab_microscope"]);
            Assert.AreEqual("İNKÜBATÖR", tr["lab_incubator"]);
            Assert.AreEqual("GELİŞTİR", tr["lab_upgrade"]);
            Assert.AreEqual("YENİ NUMUNE TEDAVİ EDİLDİ", tr["home_new_sample_cured"]);
            Assert.AreEqual("GELECEK ARAŞTIRMALARA HAZIR", tr["lab_ready_caps"], "HAZIR has a dotless ı");
            Assert.AreEqual("SONUÇLARI GÖR", tr["home_view_results"], "SONUÇLARI has a dotless ı");

            // And they round-trip through Turkish casing (lower → upper) unchanged.
            var culture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
            foreach (var key in new[] { "gameplay_levels", "experiments_locked", "lab_ready_caps", "home_view_results", "home_begin" })
                Assert.AreEqual(tr[key], tr[key].ToLower(culture).ToUpper(culture), key);
        }

        [Test]
        public void Numbers_UseTheLanguagesGrouping()
        {
            Loc.SetLanguage("en");
            Assert.AreEqual("1,240", Loc.Number(1240));
            Loc.SetLanguage("tr");
            Assert.AreEqual("1.240", Loc.Number(1240));
        }

        [Test]
        public void Format_ResolvesContentArgumentsInTheCurrentLanguage()
        {
            var title = Loc.LevelTitle("lvl_09", "Heart Tissue");
            Loc.SetLanguage("en");
            Assert.AreEqual("LEVEL 9  ·  Heart Tissue", Loc.Format("experiments_level_title", 9, title));
            Loc.SetLanguage("tr");
            Assert.AreEqual("SEVİYE 9  ·  Kalp Dokusu", Loc.Format("experiments_level_title", 9, title));
        }
    }
}
