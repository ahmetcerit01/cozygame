using CozyLab.Puzzle.Core;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Feedback;
using NUnit.Framework;
using UnityEngine;

namespace CozyLab.Puzzle.Tests
{
    public class PlacementStreakTests
    {
        [Test]
        public void ConsecutivePlacements_EscalateTiers()
        {
            var streak = new PlacementStreak();
            Assert.AreEqual(PlacementStreak.TierNone, streak.RegisterValid(false), "1st");
            Assert.AreEqual(PlacementStreak.TierGood, streak.RegisterValid(false), "2nd");
            Assert.AreEqual(PlacementStreak.TierGreat, streak.RegisterValid(false), "3rd");
            Assert.AreEqual(PlacementStreak.TierPerfect, streak.RegisterValid(true), "4th / final");
        }

        [Test]
        public void FinalPlacement_IsPerfectOnlyWhenContinuingAStreak()
        {
            Assert.AreEqual(PlacementStreak.TierPerfect, PlacementStreak.GetTier(2, isFinalPlacement: true));
            Assert.AreEqual(PlacementStreak.TierNone, PlacementStreak.GetTier(1, isFinalPlacement: true));
            Assert.AreEqual(PlacementStreak.TierPerfect, PlacementStreak.GetTier(5, isFinalPlacement: false));
        }

        [Test]
        public void Break_ResetsStreak()
        {
            var streak = new PlacementStreak();
            streak.RegisterValid(false);
            streak.RegisterValid(false);
            streak.Break();
            Assert.AreEqual(0, streak.Count);
            Assert.AreEqual(PlacementStreak.TierNone, streak.RegisterValid(false));
            Assert.AreEqual(PlacementStreak.TierGood, streak.RegisterValid(false));
        }
    }

    public class FeedbackDataTests
    {
        [Test]
        public void Theme_MapsTiersToLabels()
        {
            var theme = ScriptableObject.CreateInstance<PuzzleTheme>();
            Assert.IsNull(theme.GetComboLabel(PlacementStreak.TierNone));
            Assert.AreEqual("GOOD!", theme.GetComboLabel(PlacementStreak.TierGood));
            Assert.AreEqual("GREAT!", theme.GetComboLabel(PlacementStreak.TierGreat));
            Assert.AreEqual("PERFECT!", theme.GetComboLabel(PlacementStreak.TierPerfect));
            Object.DestroyImmediate(theme);
        }

        [Test]
        public void EmptyAudioSet_ReturnsNoClips()
        {
            var audio = ScriptableObject.CreateInstance<PuzzleAudioSet>();
            foreach (PuzzleFeedbackEvent e in System.Enum.GetValues(typeof(PuzzleFeedbackEvent)))
                Assert.IsNull(audio.GetClip(e));
            Object.DestroyImmediate(audio);
        }

        [Test]
        public void Haptics_AreSafeNoOpsInEditor()
        {
            Assert.DoesNotThrow(() =>
            {
                Haptics.Selection();
                Haptics.Play(Haptics.Impact.Light);
                Haptics.Play(Haptics.Notification.Success);
            });
        }
    }
}
