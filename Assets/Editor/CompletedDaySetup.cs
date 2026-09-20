using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BigRedButton.Editor
{
    /// <summary>
    /// Archives finished day scenes. The rebuild tool consults these snapshots, so a
    /// completed day is protected because it was explicitly saved as complete rather
    /// than because its number falls inside a hard-coded range.
    /// </summary>
    public static class CompletedDaySetup
    {
        private const string DayFolder = "Assets/Scenes/Days";
        private const string CompletedFolder = "Assets/Scenes/Completed";
        private static readonly Regex DayName = new Regex(@"^Day (\d+)$");

        /// <summary>Archive location for the immutable completed version of a day.</summary>
        public static string CompletedPath(int day) =>
            $"{CompletedFolder}/Completed Day {day}.unity";

        public static bool IsCompleted(int day) => File.Exists(CompletedPath(day));

        [MenuItem("Tools/Big Red Button/Completed Days/Save Current Day As Completed Snapshot")]
        public static void SaveCurrentDayAsCompletedSnapshot()
        {
            Scene active = SceneManager.GetActiveScene();
            Match match = DayName.Match(active.name);
            if (!match.Success || !active.path.StartsWith(DayFolder + "/"))
            {
                Debug.LogError("Open a scene named \"Day N\" under Assets/Scenes/Days first. " +
                    "Only a playable day can be saved as a completed snapshot.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            int day = int.Parse(match.Groups[1].Value);
            string target = CompletedPath(day);
            if (File.Exists(target) && !EditorUtility.DisplayDialog("Replace completed snapshot?",
                    $"A completed snapshot already exists for Day {day}. Replace it with the " +
                    "currently open version?", "Replace Snapshot", "Keep Existing"))
                return;

            Directory.CreateDirectory(CompletedFolder);
            if (File.Exists(target))
                AssetDatabase.DeleteAsset(target);
            if (!AssetDatabase.CopyAsset(active.path, target))
            {
                Debug.LogError($"Could not save completed snapshot \"{target}\".");
                return;
            }

            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            Debug.Log($"Saved Day {day} as a completed snapshot at \"{target}\". " +
                "Future Rebuild All Uncompleted Days runs will skip it.");
        }

        [MenuItem("Tools/Big Red Button/Completed Days/Open Completed Snapshot Folder")]
        public static void OpenCompletedSnapshotFolder()
        {
            Directory.CreateDirectory(CompletedFolder);
            AssetDatabase.Refresh();
            Object folder = AssetDatabase.LoadAssetAtPath<Object>(CompletedFolder);
            if (folder != null)
                EditorGUIUtility.PingObject(folder);
        }
    }
}
