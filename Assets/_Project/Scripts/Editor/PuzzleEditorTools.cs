using System.IO;
using CozyLab.Puzzle.Core;
using CozyLab.Puzzle.Data;
using CozyLab.Puzzle.Flow;
using CozyLab.Puzzle.Progression;
using UnityEditor;
using UnityEngine;

namespace CozyLab.Puzzle.Editor
{
    public static class PuzzleEditorTools
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

        [MenuItem("CozyLab/Validate All Levels")]
        public static void ValidateAllLevels()
        {
            int levels = 0, failures = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(LevelDefinition)))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                if (level == null) continue;
                levels++;

                var problems = level.Validate(checkSolvable: true);
                if (problems.Count == 0)
                {
                    var shapes = new System.Collections.Generic.List<PolyominoShape>();
                    foreach (var entry in level.Pieces) shapes.Add(entry.shape.CreateShape());
                    int solutions = PuzzleSolver.CountSolutions(level.Width, level.Height, level.BuildRequiredMask(), shapes, 1000);
                    Debug.Log($"[CozyLab] ✓ {path}  ({level.Width}x{level.Height}, {level.Pieces.Count} pieces, " +
                              $"{(solutions >= 1000 ? "1000+" : solutions.ToString())} solutions)", level);
                    continue;
                }
                failures++;
                foreach (var problem in problems) Debug.LogError($"[CozyLab] ✗ {path}: {problem}", level);
            }

            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(LevelCatalog)))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var problem in catalog.Validate(checkSolvable: false))
                {
                    failures++;
                    Debug.LogError($"[CozyLab] ✗ Catalog {catalog.name}: {problem}", catalog);
                }
            }
            Debug.Log($"[CozyLab] Validated {levels} level(s), {failures} problem(s).");
        }

        // ------------------------------------------------------------ development progress tools

        [MenuItem("CozyLab/Progress/Reset Progress")]
        public static void ResetProgress()
        {
            if (Application.isPlaying && GameFlow.Active != null)
            {
                GameFlow.Active.ResetProgress();
                return;
            }
            new JsonFileProgressStore(JsonFileProgressStore.DefaultPath).Delete();
            Debug.Log($"[CozyLab] Progress file deleted: {JsonFileProgressStore.DefaultPath}");
        }

        [MenuItem("CozyLab/Progress/Unlock All Levels (Play Mode)")]
        public static void UnlockAll()
        {
            if (GameFlow.Active != null) GameFlow.Active.UnlockAllLevels();
        }

        [MenuItem("CozyLab/Progress/Unlock All Levels (Play Mode)", true)]
        private static bool UnlockAllValidate() => Application.isPlaying && GameFlow.Active != null;

        [MenuItem("CozyLab/Progress/Show Save File")]
        public static void ShowSaveFile()
        {
            var path = JsonFileProgressStore.DefaultPath;
            if (File.Exists(path)) EditorUtility.RevealInFinder(path);
            else Debug.Log($"[CozyLab] No save yet. It will be written to: {path}");
        }

        /// <summary>
        /// Re-applies the project's mobile settings: portrait only, no auto-rotation, game scene in the build.
        /// (These are also committed in ProjectSettings; this is a safety net if the editor overwrote them.)
        /// </summary>
        [MenuItem("CozyLab/Apply Portrait Player Settings")]
        public static void ApplyPortraitSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.useAnimatedAutorotation = false;

            var scenes = EditorBuildSettings.scenes;
            bool hasScene = false;
            foreach (var scene in scenes) hasScene |= scene.path == GameScenePath;
            if (!hasScene)
            {
                var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes);
                list.Insert(0, new EditorBuildSettingsScene(GameScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[CozyLab] Portrait-only orientation applied and Game scene is in Build Settings.");
        }
    }
}
