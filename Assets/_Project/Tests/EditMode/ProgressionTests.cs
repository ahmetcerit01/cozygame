using System.Collections.Generic;
using System.IO;
using CozyLab.Puzzle.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CozyLab.Puzzle.Tests
{
    public class ProgressionTests
    {
        private static readonly List<string> Ids = new List<string> { "a", "b", "c", "d" };

        [Test]
        public void FreshProgress_OnlyFirstLevelUnlocked()
        {
            var p = new LevelProgression(Ids, new MemoryProgressStore());
            Assert.IsTrue(p.IsUnlocked(0));
            Assert.IsFalse(p.IsUnlocked(1));
            Assert.AreEqual(0, p.HighestUnlockedIndex);
            Assert.AreEqual(0, p.CurrentLevelIndex);
            Assert.AreEqual(0, p.CompletedCount);
        }

        [Test]
        public void LockedLevels_AreRejected()
        {
            var store = new MemoryProgressStore();
            var p = new LevelProgression(Ids, store);
            Assert.IsFalse(p.CanPlay(2));
            Assert.IsFalse(p.MarkCompleted(2), "cannot complete a locked level");
            Assert.IsFalse(p.MarkCompleted(-1));
            Assert.IsFalse(p.MarkCompleted(99));
            Assert.AreEqual(0, p.CompletedCount);
            Assert.AreEqual(0, store.SaveCount, "rejections must not write the save");
        }

        [Test]
        public void CompletingLevel_UnlocksNext()
        {
            var p = new LevelProgression(Ids, new MemoryProgressStore());
            int changed = 0;
            p.Changed += () => changed++;

            Assert.IsTrue(p.MarkCompleted(0));
            Assert.IsTrue(p.IsCompleted(0));
            Assert.IsTrue(p.IsUnlocked(1));
            Assert.IsFalse(p.IsUnlocked(2));
            Assert.AreEqual(1, p.CurrentLevelIndex);
            Assert.AreEqual(1, changed);
        }

        [Test]
        public void ReplayingCompletedLevel_IsAllowedAndKeepsProgress()
        {
            var store = new MemoryProgressStore();
            var p = new LevelProgression(Ids, store);
            p.MarkCompleted(0);
            p.MarkCompleted(1);
            int saves = store.SaveCount;

            Assert.IsTrue(p.CanPlay(0));
            Assert.IsTrue(p.MarkCompleted(0), "replay completion is accepted");
            Assert.AreEqual(2, p.HighestUnlockedIndex, "replay must not lower the unlock frontier");
            Assert.AreEqual(2, p.CompletedCount);
            Assert.AreEqual(saves, store.SaveCount, "nothing changed, nothing saved");
        }

        [Test]
        public void CompletingLastLevel_CompletesChapter()
        {
            var p = new LevelProgression(Ids, new MemoryProgressStore());
            for (int i = 0; i < Ids.Count; i++) Assert.IsTrue(p.MarkCompleted(i));
            Assert.IsTrue(p.IsChapterComplete);
            Assert.AreEqual(Ids.Count - 1, p.HighestUnlockedIndex);
            Assert.AreEqual(Ids.Count - 1, p.CurrentLevelIndex);
        }

        [Test]
        public void Progress_SurvivesSaveAndLoad()
        {
            var path = TempPath();
            try
            {
                var first = new LevelProgression(Ids, new JsonFileProgressStore(path));
                first.MarkCompleted(0);
                first.MarkCompleted(1);
                Assert.IsTrue(File.Exists(path));

                // A brand-new instance (like reopening the game) reads the same state.
                var reopened = new LevelProgression(Ids, new JsonFileProgressStore(path));
                Assert.IsTrue(reopened.IsCompleted(0));
                Assert.IsTrue(reopened.IsCompleted(1));
                Assert.IsTrue(reopened.IsUnlocked(2));
                Assert.IsFalse(reopened.IsUnlocked(3));

                var json = File.ReadAllText(path);
                StringAssert.Contains("\"version\": 1", json);
                StringAssert.Contains("\"highestUnlockedIndex\": 2", json);
            }
            finally
            {
                Cleanup(path);
            }
        }

        [Test]
        public void Reset_ReturnsToFreshInstall()
        {
            var path = TempPath();
            try
            {
                var p = new LevelProgression(Ids, new JsonFileProgressStore(path));
                p.MarkCompleted(0);
                p.MarkCompleted(1);
                p.Reset();

                Assert.AreEqual(0, p.CompletedCount);
                Assert.IsFalse(p.IsUnlocked(1));
                Assert.IsFalse(File.Exists(path), "reset deletes the save file");

                var reopened = new LevelProgression(Ids, new JsonFileProgressStore(path));
                Assert.IsFalse(reopened.IsUnlocked(1));
            }
            finally
            {
                Cleanup(path);
            }
        }

        [Test]
        public void CorruptSave_StartsFresh()
        {
            var path = TempPath();
            try
            {
                File.WriteAllText(path, "{ this is not json");
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Could not read"));
                var p = new LevelProgression(Ids, new JsonFileProgressStore(path));
                Assert.IsTrue(p.IsUnlocked(0));
                Assert.IsFalse(p.IsUnlocked(1));
            }
            finally
            {
                Cleanup(path);
            }
        }

        [Test]
        public void SaveUsesLevelIds_SoReorderingKeepsCompletions()
        {
            var store = new MemoryProgressStore();
            new LevelProgression(Ids, store).MarkCompleted(0); // completes "a"

            // Catalog reordered: "a" is now the third level.
            var reordered = new LevelProgression(new List<string> { "b", "c", "a", "d" }, store);
            Assert.IsTrue(reordered.IsCompleted(2));
            Assert.IsTrue(reordered.IsUnlocked(3), "a completed level always unlocks the next one");
        }

        [Test]
        public void OutOfRangeSave_IsClamped()
        {
            var store = new MemoryProgressStore();
            store.Save(new ProgressData { highestUnlockedIndex = 57, completedLevelIds = new List<string> { "zzz" } });
            var p = new LevelProgression(Ids, store);
            Assert.AreEqual(Ids.Count - 1, p.HighestUnlockedIndex);
            Assert.AreEqual(0, p.CompletedCount, "unknown ids are ignored");
        }

        private static string TempPath() =>
            Path.Combine(Path.GetTempPath(), "cozylab_test_" + System.Guid.NewGuid().ToString("N") + ".json");

        private static void Cleanup(string path)
        {
            foreach (var p in new[] { path, path + ".tmp", path + ".corrupt" })
            {
                if (File.Exists(p)) File.Delete(p);
            }
        }
    }
}
