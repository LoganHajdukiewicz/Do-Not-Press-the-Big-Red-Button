using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BigRedButton.Editor
{
    /// <summary>Uses a locally imported Asset Store material without redistributing the pack.</summary>
    public static class Day31SkyboxSetup
    {
        public const string ConfiguredPath = "Assets/LevelMaterials/Day 31 Skybox.mat";
        private const string ScenePath = "Assets/Scenes/Days/Day 31.unity";

        [MenuItem("Tools/Big Red Button/Day 31/Apply Selected Skybox Material")]
        public static void ApplySelected()
        {
            Material source = Selection.activeObject as Material;
            if (source == null || source.shader == null || !source.shader.name.StartsWith("Skybox/"))
            {
                EditorUtility.DisplayDialog("Day 31 skybox",
                    "Import Render Knight's Fantasy Skybox FREE using Package Manager > My Assets. " +
                    "Select a daytime skybox MATERIAL in the Project window, then run this command again.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (!AssetDatabase.IsValidFolder("Assets/LevelMaterials"))
                AssetDatabase.CreateFolder("Assets", "LevelMaterials");
            var configured = AssetDatabase.LoadAssetAtPath<Material>(ConfiguredPath);
            if (configured == null)
            {
                configured = new Material(source);
                AssetDatabase.CreateAsset(configured, ConfiguredPath);
            }
            else if (source != configured)
                EditorUtility.CopySerialized(source, configured);
            EditorUtility.SetDirty(configured);
            AssetDatabase.SaveAssets();

            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                ApplyConfiguredSkybox();
                // Old generated scenes had an opaque rectangle where the sky should be.
                GameObject panel = GameObject.Find("Outside");
                if (panel != null)
                    panel.SetActive(false);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
            Debug.Log("Applied the selected skybox to Day 31 only. No days were rebuilt.");
        }

        public static void ApplyConfiguredSkybox()
        {
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                camera.clearFlags = CameraClearFlags.Skybox;
            Material configured = AssetDatabase.LoadAssetAtPath<Material>(ConfiguredPath);
            if (configured != null)
                RenderSettings.skybox = configured;
            else
                Debug.LogWarning("Fantasy Skybox FREE is not configured. Import it, select a daytime " +
                    "skybox material, then use Tools > Big Red Button > Day 31 > Apply Selected Skybox Material.");
        }
    }
}
