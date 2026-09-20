using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BigRedButton.Editor
{
    public static class FirstPersonSetup
    {
        private const string InputPath = "Assets/InputSystem_Actions.inputactions";

        [MenuItem("GameObject/Big Red Button/First Person Player", false, 10)]
        public static void AddPlayer()
        {
            InputActionAsset actions = LoadActions();
            if (actions == null)
                return;

            if (Object.FindFirstObjectByType<Camera>() != null)
                Debug.LogWarning("A camera already exists. Disable the old gameplay camera and AudioListener " +
                    "before playing with the new First Person Player.");

            GameObject player = CreatePlayer(actions, Vector3.up * 0.1f);
            Undo.RegisterCreatedObjectUndo(player, "Create First Person Player");
            Selection.activeGameObject = player;
        }

        [MenuItem("Tools/Big Red Button/Repair First Person Player In Current Scene")]
        public static void RepairPlayerInCurrentScene()
        {
            FirstPersonController controller = Object.FindFirstObjectByType<FirstPersonController>();
            if (controller == null)
            {
                Debug.LogError("No FirstPersonController was found in the open scene. " +
                    "Use GameObject > Big Red Button > First Person Player to create one.");
                return;
            }

            InputActionAsset actions = LoadActions();
            if (actions == null)
                return;

            GameObject player = controller.gameObject;
            Undo.RecordObject(player.transform, "Repair First Person Player");
            player.transform.position = new Vector3(0f, 0.1f, -4.5f);
            player.transform.rotation = Quaternion.identity;
            player.transform.localScale = Vector3.one;
            player.layer = LayerMask.NameToLayer("Ignore Raycast");

            CharacterController character = player.GetComponent<CharacterController>();
            if (character == null)
                character = Undo.AddComponent<CharacterController>(player);
            Undo.RecordObject(character, "Repair Character Controller");
            character.height = 1.8f;
            character.radius = 0.3f;
            character.center = new Vector3(0f, 0.9f, 0f);
            character.stepOffset = 0.3f;
            character.slopeLimit = 45f;
            character.skinWidth = 0.03f;
            character.minMoveDistance = 0f;

            Camera camera = player.GetComponentInChildren<Camera>(true);
            if (camera == null)
            {
                var cameraObject = new GameObject("Player Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(player.transform, false);
                camera = cameraObject.GetComponent<Camera>();
                Undo.RegisterCreatedObjectUndo(cameraObject, "Repair Player Camera");
            }
            Undo.RecordObject(camera.transform, "Repair Player Camera");
            camera.name = "Player Camera";
            camera.tag = "MainCamera";
            camera.transform.SetParent(player.transform, false);
            camera.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            camera.transform.localRotation = Quaternion.identity;
            camera.transform.localScale = Vector3.one;
            camera.nearClipPlane = 0.03f;
            camera.fieldOfView = 75f;
            // Keep Day 31's panorama visible when repairing a player in that scene.
            if (RenderSettings.skybox != null)
                camera.clearFlags = CameraClearFlags.Skybox;
            else
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.17f, 0.23f);
            }
            if (camera.GetComponent<AudioListener>() == null)
                Undo.AddComponent<AudioListener>(camera.gameObject);

            var interactor = player.GetComponent<PlayerInteractor>();
            if (interactor == null)
                interactor = Undo.AddComponent<PlayerInteractor>(player);
            interactor.SetCamera(camera);
            if (player.GetComponent<FirstPersonHUD>() == null)
                Undo.AddComponent<FirstPersonHUD>(player);

            Undo.RecordObject(controller, "Repair First Person Controller");
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("inputActions").objectReferenceValue = actions;
            serialized.FindProperty("playerCamera").objectReferenceValue = camera;
            serialized.FindProperty("walkSpeed").floatValue = 4f;
            serialized.FindProperty("runSpeed").floatValue = 7f;
            serialized.FindProperty("jumpHeight").floatValue = 1.2f;
            serialized.FindProperty("gravity").floatValue = -20f;
            serialized.FindProperty("terminalSpeed").floatValue = 50f;
            serialized.FindProperty("mouseSensitivity").floatValue = 0.1f;
            serialized.FindProperty("stickSensitivity").floatValue = 150f;
            serialized.FindProperty("pitchLimit").floatValue = 85f;
            serialized.FindProperty("invertY").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            controller.enabled = true;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = player;
            Debug.Log("Repaired the First Person Player: restored its safe spawn, controller, " +
                "camera rig, normal movement, and nonverbal crosshair HUD.");
        }

        [MenuItem("Tools/Big Red Button/Create Controller Test Scene")]
        public static void CreateTestScene()
        {
            InputActionAsset actions = LoadActions();
            if (actions == null || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // Work in a new scene, never overwrite the user's existing scene or assets.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material floor = CreateMaterial("Floor", new Color(0.27f, 0.3f, 0.34f));
            Material neutral = CreateMaterial("Concrete", new Color(0.55f, 0.57f, 0.6f));
            Material red = CreateMaterial("Red", new Color(0.85f, 0.05f, 0.04f));
            Material green = CreateMaterial("Green", new Color(0.05f, 0.7f, 0.18f));

            CreatePrimitive("Floor", PrimitiveType.Cube, new Vector3(0f, -0.25f, 0f),
                new Vector3(20f, 0.5f, 20f), floor);
            CreatePrimitive("Back wall", PrimitiveType.Cube, new Vector3(0f, 1.5f, 10f),
                new Vector3(20f, 3f, 0.5f), neutral);
            CreatePrimitive("Front wall", PrimitiveType.Cube, new Vector3(0f, 1.5f, -10f),
                new Vector3(20f, 3f, 0.5f), neutral);
            foreach (float side in new[] { -10f, 10f })
                CreatePrimitive("Side wall", PrimitiveType.Cube, new Vector3(side, 1.5f, 0f),
                    new Vector3(0.5f, 3f, 20f), neutral);

            for (int i = 0; i < 3; i++)
            {
                float height = 0.4f * (i + 1);
                CreatePrimitive("Jump platform " + (i + 1), PrimitiveType.Cube,
                    new Vector3(-4f, height * 0.5f, i * 2f), new Vector3(2f, height, 1.6f), neutral);
            }

            CreateButton("Green Button", new Vector3(-1.2f, 0f, 1.5f), green, neutral);
            CreateButton("Big Red Button", new Vector3(1.2f, 0f, 1.5f), red, neutral);

            var sunlight = new GameObject("Directional Light");
            var light = sunlight.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.shadows = LightShadows.Soft;
            sunlight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.65f);

            GameObject player = CreatePlayer(actions, new Vector3(0f, 0.1f, -3f));
            Selection.activeGameObject = player;
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Controller test scene ready. Save it with File > Save As. " +
                "WASD: walk, Shift: run, Space: jump, E: interact, Esc: release cursor. " +
                "Each button reveals a matching indicator and logs its press.");
        }

        private static InputActionAsset LoadActions()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (actions == null)
                Debug.LogError("Could not load the project's Input System asset at " + InputPath);
            return actions;
        }

        /// <summary>Used by the level builder so every day gets an identical player.</summary>
        internal static GameObject CreatePlayerForLevel(InputActionAsset actions, Vector3 position) =>
            CreatePlayer(actions, position);

        private static GameObject CreatePlayer(InputActionAsset actions, Vector3 position)
        {
            var player = new GameObject("First Person Player");
            player.SetActive(false);
            player.layer = LayerMask.NameToLayer("Ignore Raycast");
            player.transform.position = position;

            var character = player.AddComponent<CharacterController>();
            character.height = 1.8f;
            character.radius = 0.3f;
            character.center = new Vector3(0f, 0.9f, 0f);
            character.stepOffset = 0.3f;
            character.slopeLimit = 45f;
            character.skinWidth = 0.03f;
            character.minMoveDistance = 0f;

            var cameraObject = new GameObject("Player Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.nearClipPlane = 0.03f;
            camera.fieldOfView = 75f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.17f, 0.23f);

            var interactor = player.AddComponent<PlayerInteractor>();
            interactor.SetCamera(camera);
            var controller = player.AddComponent<FirstPersonController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("inputActions").objectReferenceValue = actions;
            serialized.FindProperty("playerCamera").objectReferenceValue = camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            player.AddComponent<FirstPersonHUD>();
            player.SetActive(true);
            return player;
        }

        private static void CreateButton(string name, Vector3 position, Material color, Material neutral)
        {
            GameObject pedestal = CreatePrimitive(name + " pedestal", PrimitiveType.Cube,
                position + Vector3.up * 0.5f, new Vector3(1f, 1f, 1f), neutral);
            GameObject cap = CreatePrimitive(name, PrimitiveType.Cylinder,
                position + Vector3.up * 1.12f, new Vector3(0.75f, 0.12f, 0.75f), color);
            cap.transform.SetParent(pedestal.transform, true);
            var button = cap.AddComponent<ButtonInteractable>();
            var serialized = new SerializedObject(button);
            serialized.FindProperty("prompt").stringValue = "Press " + name;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject indicator = CreatePrimitive(name + " pressed indicator", PrimitiveType.Sphere,
                position + new Vector3(0f, 1.65f, 0.3f), Vector3.one * 0.2f, color);
            indicator.transform.SetParent(pedestal.transform, true);
            Object.DestroyImmediate(indicator.GetComponent<Collider>());
            indicator.SetActive(false);
            UnityEventTools.AddPersistentListener(button.OnPressed, button.LogPress);
            UnityEventTools.AddBoolPersistentListener(button.OnPressed, indicator.SetActive, true);
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 position,
            Vector3 scale, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            return item;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            const string folder = "Assets/ControllerDemoMaterials";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "ControllerDemoMaterials");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
            // Unique paths avoid changing materials already used by a saved demo scene.
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".mat");
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
