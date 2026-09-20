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
    /// Adds LED office lights to every ceiling in every scene, in place. This edits
    /// existing scenes rather than regenerating them, so the finished Days 1-5 and any
    /// hand-made changes in later days survive.
    /// </summary>
    public static class CeilingLightSetup
    {
        [MenuItem("Tools/Big Red Button/Add LED Office Lights To Every Level")]
        public static void AddToEveryScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            List<string> scenePaths = AssetDatabase.FindAssets("t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.StartsWith("Assets/Scenes/"))
                .OrderBy(path => path)
                .ToList();
            if (scenePaths.Count == 0)
            {
                Debug.LogWarning("No scenes found under Assets/Scenes.");
                return;
            }

            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            int scenesChanged = 0, ceilingsLit = 0, scenesWithoutCeiling = 0;
            try
            {
                foreach (string path in scenePaths)
                {
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    int lit = LightCeilingsInOpenScene();
                    if (lit == 0)
                    {
                        scenesWithoutCeiling++;
                        continue;
                    }

                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    scenesChanged++;
                    ceilingsLit += lit;
                }
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Fitted LED office lights to {ceilingsLit} ceiling(s) across " +
                $"{scenesChanged} scene(s). {scenesWithoutCeiling} scene(s) had no ceiling. " +
                "Existing layouts were kept; no day was regenerated. Run this again after " +
                "resizing a room, and it will refit that room's panels.");
        }

        /// <summary>
        /// Fits panels to every ceiling in the open scene. Returns how many were lit.
        /// Re-running replaces the previous fittings instead of stacking more.
        /// </summary>
        public static int LightCeilingsInOpenScene()
        {
            int lit = 0;
            foreach (GameObject candidate in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (!IsCeiling(candidate))
                    continue;

                var lights = candidate.GetComponent<OfficeCeilingLights>();
                if (lights == null)
                    lights = candidate.AddComponent<OfficeCeilingLights>();

                // Persist the fittings in the scene, so a saved level looks right without
                // waiting for play mode, and lightmapping/preview reflect the real room.
                lights.Build();
                lit++;
            }

            return lit;
        }

        private static bool IsCeiling(GameObject candidate)
        {
            // Name-based, because the ceilings are plain generated boxes with no marker.
            if (!candidate.name.StartsWith("Ceiling"))
                return false;
            return candidate.GetComponent<Renderer>() != null;
        }

        [MenuItem("Tools/Big Red Button/Add LED Office Lights To Current Scene")]
        public static void AddToCurrentScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            int lit = LightCeilingsInOpenScene();
            if (lit == 0)
            {
                Debug.LogWarning($"\"{scene.name}\" has no object named \"Ceiling\". " +
                    "Select the ceiling and add the Office Ceiling Lights component by hand, " +
                    "or rename it to start with \"Ceiling\".");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Fitted LED office lights to {lit} ceiling(s) in \"{scene.name}\". " +
                "Save the scene to keep them.");
        }

        [MenuItem("Tools/Big Red Button/Remove LED Office Lights From Every Level")]
        public static void RemoveFromEveryScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            int cleared = 0;
            try
            {
                foreach (string path in AssetDatabase.FindAssets("t:Scene")
                             .Select(AssetDatabase.GUIDToAssetPath)
                             .Where(path => path.StartsWith("Assets/Scenes/")))
                {
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    bool changed = false;
                    foreach (OfficeCeilingLights lights in
                             Object.FindObjectsByType<OfficeCeilingLights>(FindObjectsSortMode.None))
                    {
                        lights.Clear();
                        Object.DestroyImmediate(lights);
                        changed = true;
                    }

                    if (!changed)
                        continue;
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    cleared++;
                }
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }

            Debug.Log($"Removed the LED office lights from {cleared} scene(s).");
        }

        /// <summary>Used by the day builder so newly generated rooms are already fitted.</summary>
        public static void FitCeiling(GameObject ceiling)
        {
            if (ceiling == null)
                return;
            var lights = ceiling.GetComponent<OfficeCeilingLights>();
            if (lights == null)
                lights = ceiling.AddComponent<OfficeCeilingLights>();
            lights.Build();
        }
    }
}
