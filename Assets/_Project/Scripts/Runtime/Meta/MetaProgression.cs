using System;
using CozyLab.Puzzle.Progression;

namespace CozyLab.Puzzle.Meta
{
    /// <summary>
    /// Optional Lab meta progression: Research balance, one-time rewards and equipment levels.
    /// Shares the save with puzzle progression but never gates puzzle content.
    /// </summary>
    public sealed class MetaProgression
    {
        private readonly ProgressRepository _repo;

        /// <summary>Raised after any change to Research, equipment or seen unlocks.</summary>
        public event Action Changed;

        public MetaProgression(ProgressRepository repository)
        {
            _repo = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        private ProgressData Data => _repo.Data;

        public int Research => Data.research;

        public bool IsLevelRewardClaimed(string levelId) => Data.claimedLevelRewards.Contains(levelId);

        public bool IsExperimentRewardClaimed(string experimentId) => Data.claimedExperimentRewards.Contains(experimentId);

        /// <summary>
        /// Call after a level is marked completed. Grants first-clear (+20) and experiment completion (+100)
        /// Research exactly once; replays return an empty award. Saves immediately when anything is granted.
        /// </summary>
        public ResearchAward AwardCompletion(ExperimentInfo experiment, string levelId)
        {
            var award = ResearchRules.Claim(Data, experiment, levelId);
            if (award.Total > 0) Commit();
            return award;
        }

        // ---------------------------------------------------------------- equipment

        public int GetLevel(EquipmentDefinition equipment)
        {
            foreach (var e in Data.equipment)
            {
                if (e.id == equipment.Id) return Math.Max(1, Math.Min(e.level, equipment.MaxLevel));
            }
            return 1;
        }

        public int GetUpgradeCost(EquipmentDefinition equipment) => equipment.GetUpgradeCost(GetLevel(equipment));

        public bool CanUpgrade(EquipmentDefinition equipment)
        {
            int cost = GetUpgradeCost(equipment);
            return cost >= 0 && Research >= cost;
        }

        /// <summary>Validates balance, deducts Research and raises the level. Nothing changes on failure.</summary>
        public UpgradeResult TryUpgrade(EquipmentDefinition equipment)
        {
            int level = GetLevel(equipment);
            int cost = equipment.GetUpgradeCost(level);
            if (cost < 0) return UpgradeResult.MaxLevel;
            if (Research < cost) return UpgradeResult.InsufficientResearch;

            Data.research -= cost;
            SetLevelInternal(equipment, level + 1);
            Commit();
            return UpgradeResult.Upgraded;
        }

        public int LabLevel => LabEquipment.LabLevelFromMicroscope(GetLevel(LabEquipment.Microscope));

        // ---------------------------------------------------------------- lab reveals

        public bool HasSeenUnlock(string id) => Data.seenLabUnlocks.Contains(id);

        /// <summary>Records that an unlock reveal was shown. Returns true the first time only.</summary>
        public bool MarkUnlockSeen(string id)
        {
            if (Data.seenLabUnlocks.Contains(id)) return false;
            Data.seenLabUnlocks.Add(id);
            Commit();
            return true;
        }

        // ---------------------------------------------------------------- development helpers

        public void DevAddResearch(int amount)
        {
            Data.research = Math.Max(0, Data.research + amount);
            Commit();
        }

        public void DevClearResearch()
        {
            Data.research = 0;
            Commit();
        }

        public void DevSetLevel(EquipmentDefinition equipment, int level)
        {
            SetLevelInternal(equipment, Math.Max(1, Math.Min(level, equipment.MaxLevel)));
            Commit();
        }

        /// <summary>Clears Research, equipment and seen unlocks. Reward claims are kept so nothing is earned twice.</summary>
        public void DevResetMeta()
        {
            Data.research = 0;
            Data.equipment.Clear();
            Data.seenLabUnlocks.Clear();
            Commit();
        }

        /// <summary>Lets observers refresh after an external change (e.g. a full progress reset).</summary>
        public void NotifyChanged() => Changed?.Invoke();

        private void SetLevelInternal(EquipmentDefinition equipment, int level)
        {
            foreach (var e in Data.equipment)
            {
                if (e.id != equipment.Id) continue;
                e.level = level;
                return;
            }
            Data.equipment.Add(new EquipmentSave { id = equipment.Id, level = level });
        }

        private void Commit()
        {
            _repo.Save();
            Changed?.Invoke();
        }
    }
}
