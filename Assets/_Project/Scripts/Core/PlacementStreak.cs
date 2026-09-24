namespace CozyLab.Puzzle.Core
{
    /// <summary>
    /// Counts consecutive successful placements. Pure logic; the presentation decides how to celebrate a tier.
    /// </summary>
    public sealed class PlacementStreak
    {
        public const int TierNone = 0;
        public const int TierGood = 1;
        public const int TierGreat = 2;
        public const int TierPerfect = 3;

        public int Count { get; private set; }

        /// <summary>Registers a valid placement and returns the celebration tier for it.</summary>
        public int RegisterValid(bool isFinalPlacement)
        {
            Count++;
            return GetTier(Count, isFinalPlacement);
        }

        /// <summary>An invalid drop, undo or restart breaks the streak.</summary>
        public void Break() => Count = 0;

        /// <summary>
        /// 2nd consecutive placement = Good, 3rd = Great, 4th or later = Perfect.
        /// The final placement of a puzzle is Perfect whenever it continues a streak.
        /// </summary>
        public static int GetTier(int streak, bool isFinalPlacement)
        {
            if (streak < 2) return TierNone;
            if (isFinalPlacement || streak >= 4) return TierPerfect;
            return streak == 2 ? TierGood : TierGreat;
        }
    }
}
