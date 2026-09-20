using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BigRedButton.Editor
{
    /// <summary>
    /// Builds the start menu scene and puts it first in build settings, so the game
    /// opens on the front page instead of a day.
    /// </summary>
    public static class StartMenuSetup
    {
        public const string ScenePath = "Assets/Scenes/Start Menu.unity";

        [MenuItem("Tools/Big Red Button/Build Start Menu")]
        public static void BuildStartMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var host = new GameObject("Start Menu");
            host.AddComponent<StartMenu>();

            // A camera, so the scene renders its clear colour rather than nothing at all.
            var cameraObject = new GameObject("Menu Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.06f, 0.07f);
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            MakeFirstInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"Built \"{ScenePath}\" and made it the first scene in build settings. " +
                "Press Play from it: START loads Day 1, which plays the Opening.mp3 black " +
                "screen before the room appears. Team Player Mode sends red-button presses " +
                "back to Day 1.");
        }

        /// <summary>
        /// Puts the menu at index 0 and keeps every other registered scene, so a build
        /// and the Play button both begin on the front page.
        /// </summary>
        [MenuItem("Tools/Big Red Button/Make Start Menu The First Scene")]
        public static void MakeFirstInBuildSettings()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.LogError($"\"{ScenePath}\" does not exist yet. Run " +
                    "Tools > Big Red Button > Build Start Menu first.");
                return;
            }

            var others = EditorBuildSettings.scenes
                .Where(s => s.path != ScenePath)
                .ToList();
            var ordered = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            ordered.AddRange(others);
            EditorBuildSettings.scenes = ordered.ToArray();

            // Also make the Editor's Play button start here, whichever scene is open.
            SceneAsset menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            EditorSceneManager.playModeStartScene = menu;
            Debug.Log($"\"{ScenePath}\" is now scene 0 and the Play Mode start scene. " +
                "To edit a single day without going through the menu, clear " +
                "Edit > Project Settings > Editor > Play Mode Start Scene, or use " +
                "Tools > Big Red Button > Play From The Open Scene Instead.");
        }

        [MenuItem("Tools/Big Red Button/Play From The Open Scene Instead")]
        public static void ClearPlayModeStartScene()
        {
            EditorSceneManager.playModeStartScene = null;
            Debug.Log("Play mode now starts from whichever scene is open. Builds still " +
                "begin at the start menu, because it remains scene 0 in build settings.");
        }
    }
}
