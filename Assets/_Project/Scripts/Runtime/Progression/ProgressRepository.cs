using System;

namespace CozyLab.Puzzle.Progression
{
    /// <summary>
    /// Owns the single save payload shared by puzzle progression and meta progression:
    /// loads it once, upgrades old versions through an optional migration step, and saves it.
    /// </summary>
    public sealed class ProgressRepository
    {
        private readonly IProgressStore _store;

        public ProgressData Data { get; private set; }
        /// <summary>Version found on disk (0 when there was no save).</summary>
        public int LoadedVersion { get; }
        /// <summary>True if this load upgraded an older save (and wrote it back).</summary>
        public bool Migrated { get; }

        /// <param name="migrateToCurrent">
        /// Upgrades an older payload in place. Runs only when the stored version is older than
        /// <see cref="ProgressData.CurrentVersion"/>; the upgraded data is saved immediately so it never runs twice.
        /// Without a migrator an old payload keeps its version (so a later load can still migrate it).
        /// </param>
        public ProgressRepository(IProgressStore store, Action<ProgressData> migrateToCurrent = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));

            var loaded = _store.Load();
            if (loaded == null)
            {
                Data = ProgressData.CreateFresh();
                return;
            }

            loaded.Normalize();
            Data = loaded;
            LoadedVersion = loaded.version;

            if (loaded.version < ProgressData.CurrentVersion && migrateToCurrent != null)
            {
                migrateToCurrent(Data);
                Data.Normalize();
                Data.version = ProgressData.CurrentVersion;
                _store.Save(Data);
                Migrated = true;
            }
        }

        public void Save() => _store.Save(Data);

        /// <summary>Deletes everything (puzzle and meta) back to a fresh install.</summary>
        public void ResetAll()
        {
            _store.Delete();
            Data = ProgressData.CreateFresh();
        }
    }
}
