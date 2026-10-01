using System.Collections;
using System.IO;
using System.Linq;
using CozyLab.Puzzle.Flow;
using CozyLab.Puzzle.Localization;
using CozyLab.Puzzle.Presentation;
using CozyLab.Puzzle.Progression;
using CozyLab.Puzzle.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using T = CozyLab.Puzzle.Tests.VerticalSliceFlowTests;

namespace CozyLab.Puzzle.Tests
{
    /// <summary>Turkish + English on the real Game scene: live switching, persistence and rendering.</summary>
    public class LocalizationFlowTests
    {
        private const float CompletionWait = 1.8f;
        private string _savePath;
        private MemoryLanguageStore _languageStore;

        [SetUp]
        public void SetUp()
        {
            _savePath = Path.Combine(Path.GetTempPath(), "cozylab_loc_" + System.Guid.NewGuid().ToString("N") + ".json");
            GameFlow.StoreOverride = new JsonFileProgressStore(_savePath);
            _languageStore = new MemoryLanguageStore();
        }

        [TearDown]
        public void TearDown()
        {
            GameFlow.StoreOverride = null;
            foreach (var p in new[] { _savePath, _savePath + ".tmp" })
            {
                if (File.Exists(p)) File.Delete(p);
            }
            Loc.Initialize(new MemoryLanguageStore("en"), SystemLanguage.English);
        }

        private static string TextOf(string objectPath)
        {
            var text = Object.FindObjectsByType<Text>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.gameObject.activeInHierarchy && HierarchyPath(t.transform).EndsWith(objectPath));
            Assert.IsNotNull(text, $"text '{objectPath}' visible");
            return text.text;
        }

        private static string HierarchyPath(Transform t) => t.parent == null ? t.name : HierarchyPath(t.parent) + "/" + t.name;

        [UnityTest]
        public IEnumerator TurkishDevice_FirstLaunch_PlaysInTurkish()
        {
            Loc.Initialize(_languageStore, SystemLanguage.Turkish); // no saved choice yet
            yield return T.LoadGame();
            var flow = GameFlow.Active;
            Assert.AreEqual("tr", Loc.Current);

            // Home.
            Assert.AreEqual("BAŞLA", flow.Home.PrimaryLabel);
            Assert.AreEqual("GÜNCEL NUMUNE", flow.Home.ClipboardHeader);
            Assert.AreEqual("İlk Numune", flow.Home.ClipboardTitle);
            Assert.AreEqual("Seviye 1", flow.Home.ClipboardLevel);
            Assert.AreEqual("Deneyler", TextOf("ExperimentsButton/Face/Label"));
            Assert.AreEqual("Laboratuvar", TextOf("LabButton/Face/Label"));
            Assert.AreEqual("Günlük", TextOf("DailyButton/Face/Label"));
            Assert.AreEqual("Mağaza", TextOf("ShopButton/Face/Label"));

            // Gameplay HUD + completion.
            T.Click(T.FindButton("PrimaryButton"));
            yield return null;
            Assert.AreEqual("SEVİYE 1", TextOf("TopBar/Title"));
            Assert.AreEqual("SEVİYELER", UIButtons.GetLabel(T.FindButton("BackButton")));
            Assert.AreEqual("GERİ AL", UIButtons.GetLabel(T.FindButton("UndoButton")));
            Assert.AreEqual("YENİDEN", UIButtons.GetLabel(T.FindButton("RestartButton")));
            yield return T.SolveCurrentLevel(flow, dragFirstPiece: false);
            yield return new WaitForSecondsRealtime(CompletionWait + 0.5f);
            Assert.AreEqual("+20 ARAŞTIRMA", flow.PuzzleScreen.Hud.RewardText);
            Assert.AreEqual("SONRAKİ SEVİYE", UIButtons.GetLabel(T.FindButton("ContinueButton")));

            // Sample rack.
            T.Click(T.FindButton("LevelsButton"));
            yield return null;
            Assert.AreEqual("1 / 10 tedavi edildi", flow.LevelSelect.ProgressText);
            Assert.AreEqual("DENEYLER", TextOf("NavExperiments/Label"));

            // Lab.
            flow.ShowLab();
            yield return null;
            Assert.AreEqual("Deney 01'i tamamla", flow.Lab.IncubatorStatus);
        }

        [UnityTest]
        public IEnumerator SwitchingLanguage_UpdatesVisibleScreensImmediately_BothWays()
        {
            Loc.Initialize(_languageStore, SystemLanguage.English);
            yield return T.LoadGame();
            var flow = GameFlow.Active;
            Assert.AreEqual("BEGIN", flow.Home.PrimaryLabel);

            // Home is visible: switch to Turkish through Settings.
            T.Click(T.FindButton("SettingsButton"));
            yield return null;
            Assert.IsTrue(flow.Dialog.IsLanguageRowVisible);
            Assert.AreEqual("Settings", flow.Dialog.Title);
            T.Click(T.FindButton("LanguageButton_tr"));
            yield return null;
            Assert.AreEqual("tr", Loc.Current);
            Assert.AreEqual("Ayarlar", flow.Dialog.Title, "the open dialog itself updates");
            Assert.AreEqual("BAŞLA", flow.Home.PrimaryLabel, "Home behind the dialog updates");
            Assert.AreEqual("GÜNCEL NUMUNE", flow.Home.ClipboardHeader);
            Assert.AreEqual("İlk Numune", flow.Home.ClipboardTitle);
            Assert.AreEqual("Mağaza", TextOf("ShopButton/Face/Label"));
            Assert.AreEqual("tr", _languageStore.Saved, "choice saved");
            flow.Dialog.Close();

            // Lab visible: switch back to English.
            flow.ShowLab();
            yield return null;
            Assert.AreEqual("LABORATUVAR", TextOf("Header/Title"));
            Loc.SetLanguage("en");
            yield return null;
            Assert.AreEqual("LAB", TextOf("Header/Title"));
            Assert.AreEqual("LAB", TextOf("NavLab/Label"));
            Assert.AreEqual("Complete Experiment 01", flow.Lab.IncubatorStatus);

            // Sample rack visible: English → Turkish again.
            flow.ShowLevelSelect();
            yield return null;
            Assert.AreEqual("0 / 10 cured", flow.LevelSelect.ProgressText);
            Assert.AreEqual("PLAY", UIButtons.GetLabel(T.FindButton("PlayButton")));
            Loc.SetLanguage("tr");
            yield return null;
            Assert.AreEqual("0 / 10 tedavi edildi", flow.LevelSelect.ProgressText);
            Assert.AreEqual("OYNA", UIButtons.GetLabel(T.FindButton("PlayButton")));
            Assert.AreEqual("SEVİYE 1  ·  İlk Numune", TextOf("DetailCard/SelectedTitle"));
        }

        [UnityTest]
        public IEnumerator ChosenLanguage_PersistsAfterRestart()
        {
            Loc.Initialize(_languageStore, SystemLanguage.English);
            yield return T.LoadGame();
            T.Click(T.FindButton("SettingsButton"));
            yield return null;
            T.Click(T.FindButton("LanguageButton_tr"));
            yield return null;
            Assert.AreEqual("tr", _languageStore.Saved);

            // Restart: fresh language service (device says English) + fresh scene, same saved preference.
            Loc.Initialize(_languageStore, SystemLanguage.English);
            yield return T.LoadGame();
            Assert.AreEqual("tr", Loc.Current);
            Assert.AreEqual("BAŞLA", GameFlow.Active.Home.PrimaryLabel);

            // And back to English, also remembered.
            Loc.SetLanguage("en");
            Loc.Initialize(_languageStore, SystemLanguage.Turkish);
            yield return T.LoadGame();
            Assert.AreEqual("en", Loc.Current, "saved English beats a Turkish device");
            Assert.AreEqual("BEGIN", GameFlow.Active.Home.PrimaryLabel);
        }

        [UnityTest]
        public IEnumerator TurkishCharacters_RenderWithTheUIFont()
        {
            Loc.Initialize(new MemoryLanguageStore("tr"), SystemLanguage.English);
            yield return T.LoadGame();
            yield return null;

            const string turkish = "çğıİöşüÇĞIÖŞÜ";
            var font = UIFactory.DefaultFont;
            font.RequestCharactersInTexture(turkish, 40, FontStyle.Bold);
            font.RequestCharactersInTexture(turkish, 40, FontStyle.Normal);
            foreach (char c in turkish)
            {
                Assert.IsTrue(font.GetCharacterInfo(c, out var bold, 40, FontStyle.Bold), $"bold glyph '{c}'");
                Assert.Greater(bold.advance, 0, $"bold advance '{c}'");
                Assert.IsTrue(font.GetCharacterInfo(c, out var normal, 40, FontStyle.Normal), $"glyph '{c}'");
                Assert.Greater(normal.advance, 0, $"advance '{c}'");
            }

            // A real Turkish label lays out every character (nothing dropped).
            var label = T.FindButton("ShopButton").GetComponentInChildren<Text>();
            Assert.AreEqual("Mağaza", label.text);
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(label.text.Length, label.cachedTextGenerator.characterCountVisible);
        }
    }
}
