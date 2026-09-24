using System;
using System.IO;
using UnityEngine;

namespace CozyLab.Puzzle.Progression
{
    /// <summary>Where progress lives. Swappable for tests or a future cloud backend.</summary>
    public interface IProgressStore
    {
        /// <summary>Returns saved data, or null when nothing (valid) is stored.</summary>
        ProgressData Load();
        void Save(ProgressData data);
        void Delete();
    }

    /// <summary>JSON file on the device (Application.persistentDataPath). Writes are atomic.</summary>
    public sealed class JsonFileProgressStore : IProgressStore
    {
        public const string DefaultFileName = "cozylab_progress.json";

        public string FilePath { get; }

        public JsonFileProgressStore(string filePath)
        {
            FilePath = filePath;
        }

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, DefaultFileName);

        public ProgressData Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var json = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(json)) return null;
                var data = JsonUtility.FromJson<ProgressData>(json);
                if (data == null) return null;
                if (data.completedLevelIds == null) data.completedLevelIds = new System.Collections.Generic.List<string>();
                return data;
            }
            catch (Exception e)
            {
                // A corrupt save should never block the game; start fresh and keep a copy for debugging.
                Debug.LogWarning($"[Progress] Could not read '{FilePath}': {e.Message}. Starting fresh.");
                TryBackupCorrupt();
                return null;
            }
        }

        public void Save(ProgressData data)
        {
            try
            {
                var directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                var temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, prettyPrint: true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temp, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Progress] Could not save '{FilePath}': {e.Message}");
            }
        }

        public void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
                if (File.Exists(FilePath + ".tmp")) File.Delete(FilePath + ".tmp");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Progress] Could not delete '{FilePath}': {e.Message}");
            }
        }

        private void TryBackupCorrupt()
        {
            try
            {
                File.Copy(FilePath, FilePath + ".corrupt", overwrite: true);
            }
            catch
            {
                // Best effort only.
            }
        }
    }

    /// <summary>Non-persistent store for tests and editor previews.</summary>
    public sealed class MemoryProgressStore : IProgressStore
    {
        private string _json;

        public int SaveCount { get; private set; }

        public ProgressData Load() => string.IsNullOrEmpty(_json) ? null : JsonUtility.FromJson<ProgressData>(_json);

        public void Save(ProgressData data)
        {
            _json = JsonUtility.ToJson(data);
            SaveCount++;
        }

        public void Delete() => _json = null;
    }
}
