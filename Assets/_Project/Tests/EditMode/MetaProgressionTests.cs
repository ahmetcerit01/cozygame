using System.Collections.Generic;
using System.IO;
using System.Linq;
using CozyLab.Puzzle.Meta;
using CozyLab.Puzzle.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CozyLab.Puzzle.Tests
{
    internal static class MetaTestData
    {
        public static readonly List<string> Ids = Enumerable.Range(1, 10).Select(i => $"lvl_{i:D2}").ToList();
        public static readonly ExperimentInfo Experiment = new ExperimentInfo("experiment_01", Ids);

        public static ProgressRepository MigratingRepo(IProgressStore store) =>
            new ProgressRepository(store, data => SaveMigration.MigrateToCurrent(data, new List<ExperimentInfo> { Experiment }));

        /// <summary>A version-1 save exactly as the previous milestone wrote it.</summary>
        public static string V1Json(int completed)
        {
            var ids = string.Join(",", Ids.Take(completed).Select(id => $"\"{id}\""));
            int highest = Mathf.Min(completed, Ids.Count - 1);
            return $"{{\"version\":1,\"highestUnlockedIndex\":{highest},\"completedLevelIds\":[{ids}]}}";
        }

        public static string TempPath() =>
            Path.Combine(Path.GetTempPath(), "cozylab_meta_" + System.Guid.NewGuid().ToString("N") + ".json");

        public static void Cleanup(string path)
        {
            foreach (var p in new[] { path, path + ".tmp", path + ".corrupt" })
            {
                if (File.Exists(p)) File.Delete(p);
            }
        }

        /// <summary>Simulates completing a level the way GameFlow does: mark completed, then award.</summary>
        public static ResearchAward Complete(LevelProgression progression, MetaProgression meta, int index)
        {
            Assert.IsTrue(progression.MarkCompleted(index));
            return meta.AwardCompletion(Experiment, Ids[index]);
        }
    }

    public class ResearchRewardTests
    {
        private MemoryProgressStore _store;
        private ProgressRepository _repo;
        private LevelProgression _progression;
        private MetaProgression _meta;

        [SetUp]
        public void SetUp()
        {
            _store = new MemoryProgressStore();
            _repo = MetaTestData.MigratingRepo(_store);
            _progression = new LevelProgression(MetaTestData.Ids, _repo);
            _meta = new MetaProgression(_repo);
        }

        [Test]
        public void FreshPlayer_HasZeroResearch()
        {
            Assert.AreEqual(0, _meta.Research);
            Assert.AreEqual(ProgressData.CurrentVersion, _repo.Data.version);
        }

        [Test]
        public void FirstClear_AwardsExactly20()
        {
            var award = MetaTestData.Complete(_progression, _meta, 0);
            Assert.AreEqual(ResearchRules.LevelFirstClear, award.LevelReward);
            Assert.AreEqual(0, award.ExperimentReward);
            Assert.AreEqual(20, _meta.Research);
            Assert.IsTrue(_meta.IsLevelRewardClaimed("lvl_01"));
        }

        [Test]
        public void Replay_AwardsNothing()
        {
            MetaTestData.Complete(_progression, _meta, 0);
            var replay = MetaTestData.Complete(_progression, _meta, 0);
            Assert.AreEqual(0, replay.Total);
            Assert.AreEqual(20, _meta.Research);
        }

        [Test]
        public void DifferentFirstClears_EachAward20()
        {
            MetaTestData.Complete(_progression, _meta, 0);
            MetaTestData.Complete(_progression, _meta, 1);
            MetaTestData.Complete(_progression, _meta, 2);
            Assert.AreEqual(60, _meta.Research);
        }

        [Test]
        public void UncompletedLevel_CannotBeClaimed()
        {
            var award = _meta.AwardCompletion(MetaTestData.Experiment, "lvl_05");
            Assert.AreEqual(0, award.Total);
            Assert.AreEqual(0, _meta.Research);
        }

        [Test]
        public void Level10FirstClear_Gets20_PlusExperimentBonusOnce()
        {
            for (int i = 0; i < 9; i++) MetaTestData.Complete(_progression, _meta, i);
            Assert.AreEqual(180, _meta.Research);

            var finale = MetaTestData.Complete(_progression, _meta, 9);
            Assert.AreEqual(20, finale.LevelReward, "normal level 10 first-clear reward");
            Assert.AreEqual(100, finale.ExperimentReward, "experiment completion reward");
            Assert.AreEqual(300, _meta.Research);
            Assert.IsTrue(_meta.IsExperimentRewardClaimed("experiment_01"));

            var replay = MetaTestData.Complete(_progression, _meta, 9);
            Assert.AreEqual(0, replay.Total, "experiment completion replay awards nothing");
            Assert.AreEqual(300, _meta.Research);
        }

        [Test]
        public void ClaimedRewards_PersistAcrossReload()
        {
            MetaTestData.Complete(_progression, _meta, 0);
            MetaTestData.Complete(_progression, _meta, 1);

            var reloaded = MetaTestData.MigratingRepo(_store);
            var progression = new LevelProgression(MetaTestData.Ids, reloaded);
            var meta = new MetaProgression(reloaded);
            Assert.AreEqual(40, meta.Research);
            Assert.IsTrue(meta.IsLevelRewardClaimed("lvl_01"));
            Assert.IsTrue(meta.IsLevelRewardClaimed("lvl_02"));
            Assert.AreEqual(0, MetaTestData.Complete(progression, meta, 0).Total, "still claimed after reload");
        }

        [Test]
        public void Research_NeverGatesPuzzles()
        {
            // A player who ignores the Lab entirely can still finish every level.
            for (int i = 0; i < MetaTestData.Ids.Count; i++)
            {
                Assert.IsTrue(_progression.CanPlay(i), $"level {i + 1} playable without any Lab interaction");
                MetaTestData.Complete(_progression, _meta, i);
            }
            Assert.IsTrue(_progression.IsChapterComplete);
            Assert.AreEqual(1, _meta.GetLevel(LabEquipment.Microscope), "Lab untouched");
        }
    }

    public class SaveMigrationTests
    {
        private static (ProgressRepository repo, LevelProgression progression, MetaProgression meta) Load(IProgressStore store)
        {
            var repo = MetaTestData.MigratingRepo(store);
            return (repo, new LevelProgression(MetaTestData.Ids, repo), new MetaProgression(repo));
        }

        [Test]
        public void V1Save_With5Completed_MigratesTo100Research()
        {
            var path = MetaTestData.TempPath();
            try
            {
                File.WriteAllText(path, MetaTestData.V1Json(5));
                var (repo, progression, meta) = Load(new JsonFileProgressStore(path));

                Assert.IsTrue(repo.Migrated);
                Assert.AreEqual(1, repo.LoadedVersion);
                Assert.AreEqual(ProgressData.CurrentVersion, repo.Data.version);
                Assert.AreEqual(100, meta.Research);
                for (int i = 0; i < 5; i++)
                {
                    Assert.IsTrue(progression.IsCompleted(i), "completed levels stay completed");
                    Assert.IsTrue(meta.IsLevelRewardClaimed(MetaTestData.Ids[i]), "migrated reward marked claimed");
                }
                Assert.IsFalse(meta.IsLevelRewardClaimed("lvl_06"));
                Assert.IsTrue(progression.IsUnlocked(5), "unlock frontier preserved");
                Assert.IsFalse(progression.IsUnlocked(6));
                Assert.IsFalse(meta.IsExperimentRewardClaimed("experiment_01"));

                StringAssert.Contains($"\"version\": {ProgressData.CurrentVersion}", File.ReadAllText(path), "migrated save written back");
            }
            finally
            {
                MetaTestData.Cleanup(path);
            }
        }

        [Test]
        public void V1Save_With10Completed_MigratesTo300Research()
        {
            var store = new MemoryProgressStore();
            store.SaveRaw(MetaTestData.V1Json(10));
            var (_, progression, meta) = Load(store);

            Assert.AreEqual(300, meta.Research);
            Assert.IsTrue(meta.IsExperimentRewardClaimed("experiment_01"));
            Assert.IsTrue(progression.IsChapterComplete);
            foreach (var id in MetaTestData.Ids) Assert.IsTrue(meta.IsLevelRewardClaimed(id));
        }

        [Test]
        public void SecondLoad_DoesNotGrantAgain()
        {
            var store = new MemoryProgressStore();
            store.SaveRaw(MetaTestData.V1Json(5));
            Load(store);
            var (repo, _, meta) = Load(store);
            Assert.IsFalse(repo.Migrated, "migration happens exactly once");
            Assert.AreEqual(100, meta.Research);

            var (_, _, third) = Load(store);
            Assert.AreEqual(100, third.Research);
        }

        [Test]
        public void MigratedPlayer_EarnsOnlyForNewLevels()
        {
            var store = new MemoryProgressStore();
            store.SaveRaw(MetaTestData.V1Json(5));
            var (_, progression, meta) = Load(store);
            Assert.AreEqual(0, MetaTestData.Complete(progression, meta, 2).Total, "replaying a migrated level");
            Assert.AreEqual(20, MetaTestData.Complete(progression, meta, 5).Total, "first clear of a new level");
            Assert.AreEqual(120, meta.Research);
        }

        [Test]
        public void V1Save_WithNothingCompleted_MigratesSafely()
        {
            var store = new MemoryProgressStore();
            store.SaveRaw("{\"version\":1,\"highestUnlockedIndex\":0,\"completedLevelIds\":[]}");
            var (repo, progression, meta) = Load(store);
            Assert.IsTrue(repo.Migrated);
            Assert.AreEqual(0, meta.Research);
            Assert.IsTrue(progression.IsUnlocked(0));
            Assert.IsFalse(progression.IsUnlocked(1));
        }

        [Test]
        public void CorruptSave_StillFailsSafely()
        {
            var path = MetaTestData.TempPath();
            try
            {
                File.WriteAllText(path, "{ definitely not json");
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Could not read"));
                var (repo, progression, meta) = Load(new JsonFileProgressStore(path));
                Assert.IsFalse(repo.Migrated);
                Assert.AreEqual(0, meta.Research);
                Assert.IsTrue(progression.IsUnlocked(0));
                Assert.IsFalse(progression.IsUnlocked(1));
            }
            finally
            {
                MetaTestData.Cleanup(path);
            }
        }

        [Test]
        public void StandaloneProgression_DoesNotBumpOldVersion()
        {
            // Code paths without the migrator must never mark an old save as current (that would skip the rewards).
            var store = new MemoryProgressStore();
            store.SaveRaw(MetaTestData.V1Json(3));
            var standalone = new LevelProgression(MetaTestData.Ids, store);
            standalone.MarkCompleted(3);
            var (_, _, meta) = Load(store);
            Assert.AreEqual(80, meta.Research, "4 completed levels migrated later");
        }
    }

    public class EquipmentTests
    {
        private ProgressRepository _repo;
        private MetaProgression _meta;
        private MemoryProgressStore _store;
        private static EquipmentDefinition Microscope => LabEquipment.Microscope;

        [SetUp]
        public void SetUp()
        {
            _store = new MemoryProgressStore();
            _repo = MetaTestData.MigratingRepo(_store);
            _meta = new MetaProgression(_repo);
        }

        [Test]
        public void Microscope_StartsAtLevel1_WithCosts100Then200()
        {
            Assert.AreEqual(1, _meta.GetLevel(Microscope));
            Assert.AreEqual(3, Microscope.MaxLevel);
            Assert.AreEqual(100, Microscope.GetUpgradeCost(1));
            Assert.AreEqual(200, Microscope.GetUpgradeCost(2));
            Assert.AreEqual(-1, Microscope.GetUpgradeCost(3));
        }

        [Test]
        public void Upgrade_DeductsExactCost()
        {
            _meta.DevAddResearch(350);
            Assert.AreEqual(UpgradeResult.Upgraded, _meta.TryUpgrade(Microscope));
            Assert.AreEqual(2, _meta.GetLevel(Microscope));
            Assert.AreEqual(250, _meta.Research);
            Assert.AreEqual(UpgradeResult.Upgraded, _meta.TryUpgrade(Microscope));
            Assert.AreEqual(3, _meta.GetLevel(Microscope));
            Assert.AreEqual(50, _meta.Research);
        }

        [Test]
        public void InsufficientResearch_RejectsAndDeductsNothing()
        {
            _meta.DevAddResearch(99);
            int saves = _store.SaveCount;
            Assert.IsFalse(_meta.CanUpgrade(Microscope));
            Assert.AreEqual(UpgradeResult.InsufficientResearch, _meta.TryUpgrade(Microscope));
            Assert.AreEqual(99, _meta.Research);
            Assert.AreEqual(1, _meta.GetLevel(Microscope));
            Assert.AreEqual(saves, _store.SaveCount, "failed upgrade writes nothing");
        }

        [Test]
        public void Microscope_CannotExceedLevel3()
        {
            _meta.DevAddResearch(1000);
            _meta.TryUpgrade(Microscope);
            _meta.TryUpgrade(Microscope);
            Assert.AreEqual(UpgradeResult.MaxLevel, _meta.TryUpgrade(Microscope));
            Assert.AreEqual(3, _meta.GetLevel(Microscope));
            Assert.AreEqual(700, _meta.Research);
            _meta.DevSetLevel(Microscope, 9);
            Assert.AreEqual(3, _meta.GetLevel(Microscope), "dev tools clamp too");
        }

        [Test]
        public void EquipmentLevel_PersistsAfterReload()
        {
            _meta.DevAddResearch(120);
            _meta.TryUpgrade(Microscope);
            var reloaded = new MetaProgression(MetaTestData.MigratingRepo(_store));
            Assert.AreEqual(2, reloaded.GetLevel(Microscope));
            Assert.AreEqual(20, reloaded.Research);
        }

        [Test]
        public void SeenUnlocks_AreRecordedOnce()
        {
            Assert.IsTrue(_meta.MarkUnlockSeen(LabEquipment.IncubatorId));
            Assert.IsFalse(_meta.MarkUnlockSeen(LabEquipment.IncubatorId));
            Assert.IsTrue(new MetaProgression(MetaTestData.MigratingRepo(_store)).HasSeenUnlock(LabEquipment.IncubatorId));
        }

        [Test]
        public void ResetMeta_KeepsClaimsSoNothingIsEarnedTwice()
        {
            var progression = new LevelProgression(MetaTestData.Ids, _repo);
            MetaTestData.Complete(progression, _meta, 0);
            _meta.DevResetMeta();
            Assert.AreEqual(0, _meta.Research);
            Assert.AreEqual(0, MetaTestData.Complete(progression, _meta, 0).Total);
            Assert.IsTrue(progression.IsCompleted(0), "puzzle progress untouched");
        }
    }

    public class LabLevelTests
    {
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        public void LabLevel_FollowsMicroscope(int microscope, int lab)
        {
            Assert.AreEqual(lab, LabEquipment.LabLevelFromMicroscope(microscope));
        }

        [Test]
        public void MetaLabLevel_TracksUpgrades()
        {
            var meta = new MetaProgression(MetaTestData.MigratingRepo(new MemoryProgressStore()));
            Assert.AreEqual(1, meta.LabLevel);
            meta.DevAddResearch(300);
            meta.TryUpgrade(LabEquipment.Microscope);
            Assert.AreEqual(2, meta.LabLevel);
            meta.TryUpgrade(LabEquipment.Microscope);
            Assert.AreEqual(3, meta.LabLevel);
        }

        [Test]
        public void UnlockRules()
        {
            Assert.IsFalse(LabEquipment.IsIncubatorUnlocked(firstExperimentComplete: false));
            Assert.IsTrue(LabEquipment.IsIncubatorUnlocked(firstExperimentComplete: true));
            Assert.IsFalse(LabEquipment.IsAnalyzerUnlocked(2));
            Assert.IsTrue(LabEquipment.IsAnalyzerUnlocked(3));
        }
    }
}
