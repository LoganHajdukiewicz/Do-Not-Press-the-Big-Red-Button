using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BigRedButton.Editor
{
    /// <summary>Keeps day scenes registered in build settings and creates new day scenes.</summary>
    public static class DaySetup
    {
        private const string DayFolder = "Assets/Scenes/Days";
        private static readonly Regex DayPattern = new Regex(@"^Day (\d+)$");

        [MenuItem("Tools/Big Red Button/Refresh Day Scene List")]
        public static void RefreshDayScenes()
        {
            List<(int day, string path)> days = FindDayScenes();
            if (days.Count == 0)
            {
                Debug.LogWarning("No scenes named \"Day <number>\" were found. Use " +
                    "Tools > Big Red Button > Create Next Day Scene.");
                return;
            }

            // Keep the player's other scenes enabled and in place; only reorder day scenes.
            var kept = EditorBuildSettings.scenes
                .Where(s => !DayPattern.IsMatch(Path.GetFileNameWithoutExtension(s.path)))
                .ToList();
            var ordered = days.Select(d => new EditorBuildSettingsScene(d.path, true)).ToList();
            EditorBuildSettings.scenes = kept.Concat(ordered).ToArray();

            string missing = string.Join(", ", Enumerable.Range(DayFlow.FirstDay, days[^1].day)
                .Where(day => days.All(d => d.day != day)).Select(day => "Day " + day));
            Debug.Log($"Registered {days.Count} day scene(s) up to Day {days[^1].day}." +
                (string.IsNullOrEmpty(missing) ? string.Empty : $" Missing in between: {missing}."));
        }

        [MenuItem("Tools/Big Red Button/Create Next Day Scene")]
        public static void CreateNextDayScene()
        {
            List<(int day, string path)> days = FindDayScenes();
            int nextDay = days.Count == 0 ? DayFlow.FirstDay : days[^1].day + 1;
            string sourcePath = days.Count == 0 ? FindExistingGameplayScene() : days[^1].path;
            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogError("No scene found to copy. Open or create a gameplay scene first, " +
                    "for example with Tools > Big Red Button > Create Controller Test Scene.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Directory.CreateDirectory(DayFolder);
            AssetDatabase.Refresh();
            string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{DayFolder}/Day {nextDay}.unity");
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            {
                Debug.LogError($"Could not copy \"{sourcePath}\" to \"{targetPath}\".");
                return;
            }

            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport);
            Scene scene = EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Single);
            ConfigureDayScene(scene, nextDay);
            EditorSceneManager.SaveScene(scene);
            RefreshDayScenes();
            Debug.Log($"Created \"{targetPath}\" from \"{Path.GetFileName(sourcePath)}\". " +
                $"Wire this day's correct button On Pressed to DayLevel.CompleteDay, " +
                "then edit the layout for Day " + nextDay + ".");
        }

        [MenuItem("Tools/Big Red Button/Set Up Current Scene As A Day")]
        public static void SetUpCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogError("Save the current scene first so its day number can be detected.");
                return;
            }

            Match match = DayPattern.Match(Path.GetFileNameWithoutExtension(scene.path));
            int day = match.Success ? int.Parse(match.Groups[1].Value) : DayFlow.FirstDay;
            DayLevel level = ConfigureDayScene(scene, day);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = level.gameObject;
            Debug.Log($"\"{scene.name}\" is set up as Day {level.DayNumber}. " +
                "Connect the correct button's On Pressed event to DayLevel.CompleteDay, " +
                "and optionally the wrong button to DayLevel.FailDay. Rename the scene " +
                "\"Day <number>\" so it joins the day order.");
        }

        private static DayLevel ConfigureDayScene(Scene scene, int day)
        {
            DayLevel level = Object.FindObjectsByType<DayLevel>(FindObjectsSortMode.None).FirstOrDefault();
            if (level == null)
            {
                var host = new GameObject("Day");
                host.AddComponent<DayTitle>();
                level = host.AddComponent<DayLevel>();
            }

            var serialized = new SerializedObject(level);
            serialized.FindProperty("dayNumber").intValue = Mathf.Max(DayFlow.FirstDay, day);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            return level;
        }

        private static List<(int day, string path)> FindDayScenes()
        {
            var days = new List<(int day, string path)>();
            foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Match match = DayPattern.Match(Path.GetFileNameWithoutExtension(path));
                if (match.Success)
                    days.Add((int.Parse(match.Groups[1].Value), path));
            }

            days.Sort((a, b) => a.day.CompareTo(b.day));
            return days;
        }

        private static string FindExistingGameplayScene()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(active.path))
                return active.path;

            return AssetDatabase.FindAssets("t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.EndsWith("TestScene.unity"));
        }
    }
}
