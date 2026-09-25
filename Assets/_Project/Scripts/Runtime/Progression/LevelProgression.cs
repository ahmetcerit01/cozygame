using System;
using System.Collections.Generic;

namespace CozyLab.Puzzle.Progression
{
    /// <summary>
    /// Linear unlock rules for one chapter: level 0 is always open, completing level N unlocks N+1,
    /// completed levels stay replayable. Knows nothing about scenes, UI or puzzles.
    /// </summary>
    public sealed class LevelProgression
    {
        private readonly IReadOnlyList<string> _levelIds;
        private readonly ProgressRepository _repo;

        // Always read through the repository: a full reset swaps the payload object.
        private ProgressData _data => _repo.Data;

        public event Action Changed;

        public int LevelCount => _levelIds.Count;
        public int HighestUnlockedIndex => _data.highestUnlockedIndex;
        public int CompletedCount { get; private set; }
        public bool IsChapterComplete => LevelCount > 0 && CompletedCount == LevelCount;

        /// <summary>The level the player should play next: first unlocked, not yet completed level.</summary>
        public int CurrentLevelIndex
        {
            get
            {
                for (int i = 0; i <= HighestUnlockedIndex && i < LevelCount; i++)
                {
                    if (!IsCompleted(i)) return i;
                }
                return Math.Min(HighestUnlockedIndex, LevelCount - 1);
            }
        }

        /// <summary>Standalone progression with its own repository (no meta migration).</summary>
        public LevelProgression(IReadOnlyList<string> levelIds, IProgressStore store)
            : this(levelIds, new ProgressRepository(store))
        {
        }

        /// <summary>Progression sharing a repository (and save file) with other systems.</summary>
        public LevelProgression(IReadOnlyList<string> levelIds, ProgressRepository repository)
        {
            _levelIds = levelIds ?? throw new ArgumentNullException(nameof(levelIds));
            _repo = repository ?? throw new ArgumentNullException(nameof(repository));
            Sanitize(_repo.Data);
            RecountCompleted();
        }

        /// <summary>Level ids in play order.</summary>
        public IReadOnlyList<string> LevelIds => _levelIds;

        public string GetLevelId(int index) => index >= 0 && index < LevelCount ? _levelIds[index] : null;

        public bool IsUnlocked(int index) => index >= 0 && index < LevelCount && index <= _data.highestUnlockedIndex;

        public bool IsCompleted(int index) =>
            index >= 0 && index < LevelCount && _data.completedLevelIds.Contains(_levelIds[index]);

        /// <summary>Unlocked levels (including completed ones) can be played; locked ones are rejected.</summary>
        public bool CanPlay(int index) => IsUnlocked(index);

        /// <summary>
        /// Records a completion. Returns false (and changes nothing) for locked or out-of-range levels.
        /// Replaying an already completed level is allowed and keeps progress unchanged.
        /// </summary>
        public bool MarkCompleted(int index)
        {
            if (!CanPlay(index)) return false;

            bool changed = false;
            var id = _levelIds[index];
            if (!_data.completedLevelIds.Contains(id))
            {
                _data.completedLevelIds.Add(id);
                changed = true;
            }

            int unlock = Math.Min(index + 1, LevelCount - 1);
            if (unlock > _data.highestUnlockedIndex)
            {
                _data.highestUnlockedIndex = unlock;
                changed = true;
            }

            if (changed) Commit();
            return true;
        }

        /// <summary>Wipes progress back to a fresh install (development / settings use).</summary>
        public void Reset()
        {
            _repo.ResetAll();
            RecountCompleted();
            Changed?.Invoke();
        }

        /// <summary>Development helper: unlocks every level without marking them completed.</summary>
        public void UnlockAll()
        {
            _data.highestUnlockedIndex = Math.Max(0, LevelCount - 1);
            Commit();
        }

        /// <summary>A copy of the current save payload (for debugging / tests).</summary>
        public ProgressData Snapshot() => new ProgressData
        {
            version = _data.version,
            highestUnlockedIndex = _data.highestUnlockedIndex,
            completedLevelIds = new List<string>(_data.completedLevelIds),
        };

        private void Commit()
        {
            _repo.Save();
            RecountCompleted();
            Changed?.Invoke();
        }

        private void RecountCompleted()
        {
            int count = 0;
            for (int i = 0; i < LevelCount; i++)
            {
                if (_data.completedLevelIds.Contains(_levelIds[i])) count++;
            }
            CompletedCount = count;
        }

        /// <summary>Clamps loaded data to the current catalog (levels may have been added or removed).</summary>
        private void Sanitize(ProgressData data)
        {
            data.Normalize();

            int maxIndex = Math.Max(0, LevelCount - 1);
            data.highestUnlockedIndex = Math.Max(0, Math.Min(data.highestUnlockedIndex, maxIndex));

            // Every completed level implies the next one is unlocked (repairs older/partial saves).
            for (int i = 0; i < LevelCount; i++)
            {
                if (data.completedLevelIds.Contains(_levelIds[i]))
                    data.highestUnlockedIndex = Math.Max(data.highestUnlockedIndex, Math.Min(i + 1, maxIndex));
            }
        }
    }
}
