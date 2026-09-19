using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BigRedButton.Editor
{
    /// <summary>
    /// Builds the first playable days from the design in the README: a clean, quiet
    /// test-chamber room with one green button that ends the day and red buttons that do not.
    /// </summary>
    public static class DayLevelBuilder
    {
        private const string DayFolder = "Assets/Scenes/Days";
        private const string MaterialFolder = "Assets/LevelMaterials";
        private const string InputPath = "Assets/InputSystem_Actions.inputactions";
        private const string OpeningClipPath = "Assets/Audio/Opening.mp3";
        private const string DingClipPath = "Assets/Audio/button-ding.mp3";
        private const string ClickClipPath = "Assets/Audio/button-click.mp3";
        private const int DaysToBuild = 3;

        private static Material wallMaterial;
        private static Material floorMaterial;
        private static Material trimMaterial;
        private static Material redMaterial;
        private static Material greenMaterial;
        private static Material pedestalMaterial;

        [MenuItem("Tools/Big Red Button/Build Days 1-3")]
        public static void BuildFirstDays()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (actions == null)
            {
                Debug.LogError("Could not load " + InputPath);
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            CreateMaterials();
            Directory.CreateDirectory(DayFolder);
            AssetDatabase.Refresh();

            var built = new List<string>();
            for (int day = 1; day <= DaysToBuild; day++)
                built.Add(BuildDay(day, actions));

            BuildTestScene(actions);
            AssetDatabase.SaveAssets();
            DaySetup.RefreshDayScenes();
            EditorSceneManager.OpenScene(built[0], OpenSceneMode.Single);
            Debug.Log($"Built {built.Count} day scenes in {DayFolder} and rewrote " +
                "Assets/Scenes/TestScene.unity. Press Play from \"Day 1\" for the real opening, " +
                "or from TestScene to try the mechanics with indicators on.");
        }

        [MenuItem("Tools/Big Red Button/Rebuild Test Scene")]
        public static void RebuildTestScene()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (actions == null)
            {
                Debug.LogError("Could not load " + InputPath);
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            CreateMaterials();
            string path = BuildTestScene(actions);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log($"Rewrote \"{path}\" using the current button and day configuration.");
        }

        /// <summary>
        /// A sandbox for testing the mechanics: the same wiring as a real day, but with the
        /// pressed indicators switched on and the day set to repeat instead of advancing away.
        /// </summary>
        private static string BuildTestScene(InputActionAsset actions)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // A wider, open room, so the showcase buttons all fit with space to move.
            BuildRoom(2, withDividers: false);
            CreateLighting();

            FirstPersonSetup.CreatePlayerForLevel(actions, new Vector3(0f, 0.1f, -6f));
            var dayHost = new GameObject("Day");
            dayHost.AddComponent<DayTitle>();
            var level = dayHost.AddComponent<DayLevel>();
            SetPrivate(level, "dayNumber", 1);
            SetPrivate(level, "delayBeforeNextDay", 1.6f);

            // Jump platforms, so the controller's movement is still testable here.
            for (int i = 0; i < 3; i++)
            {
                float height = 0.4f * (i + 1);
                Box("Jump platform " + (i + 1), new Vector3(-7.4f, height * 0.5f, i * 2f - 2f),
                    new Vector3(1.8f, height, 1.6f), trimMaterial);
            }

            GameObject red = CreateRedButton("Big Red Button", new Vector3(1.1f, 0f, 2.2f), level);
            GameObject green = CreateGreenButton(new Vector3(-1.1f, 0f, 2.2f), level, delay: 0f);

            // A floor button, to test pressing by standing on it.
            GameObject floorButton = CreateFloorButton(new Vector3(3.6f, 0f, 0f), level);

            AddIndicator(red.GetComponent<ButtonInteractable>(), redMaterial);
            AddIndicator(green.GetComponentInChildren<ButtonInteractable>(), greenMaterial);
            AddIndicator(floorButton.GetComponent<ButtonInteractable>(), greenMaterial);

            BuildButtonShowcase(level);

            const string path = "Assets/Scenes/TestScene.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        /// <summary>
        /// One of each special button from the design, laid out along the room so every
        /// behaviour can be tried in one place. Labels say what each one does.
        /// </summary>
        private static void BuildButtonShowcase(DayLevel level)
        {
            // Day 4: red now, green in a few seconds, then back again.
            GameObject timed = SpecialButton("Timed Colour Button", new Vector3(-4.6f, 0f, 5.4f),
                "GREEN ON A TIMER - PRESS IT WHILE GREEN");
            var schedule = timed.AddComponent<ButtonColourSchedule>();
            SetPrivate(schedule, "redDuration", 4f);
            SetPrivate(schedule, "greenDuration", 4f);
            // Pressing it while green ends the day; while red it restarts.
            ResolveByColour(timed, level);

            // Day 18: looking at it makes it turn red.
            GameObject shy = SpecialButton("Shy Button", new Vector3(-1.6f, 0f, 5.4f),
                "TURNS RED WHEN WATCHED");
            var gaze = shy.AddComponent<ButtonGazeColour>();
            SetPrivate(gaze, "mode", (int)ButtonGazeColour.GazeMode.TurnsRedWhenWatched);
            ResolveByColour(shy, level);

            // Day 19: colour depends on which way the player is facing.
            GameObject compass = SpecialButton("Compass Button", new Vector3(1.6f, 0f, 5.4f),
                "COLOUR FOLLOWS YOUR HEADING");
            var heading = compass.AddComponent<ButtonGazeColour>();
            SetPrivate(heading, "mode", (int)ButtonGazeColour.GazeMode.FollowsPlayerHeading);
            ResolveByColour(compass, level);

            // Day 13: grey and inert until it is woken up.
            GameObject sleeping = SpecialButton("Sleeping Button", new Vector3(4.6f, 0f, 5.4f),
                "PRESS ONCE TO WAKE IT");
            // Grey in the saved scene too, so it never shows red before the first press.
            SetPrivate(sleeping.GetComponent<ButtonAppearance>(), "startDisabled", true);
            sleeping.AddComponent<WakeableButton>();
            // Wakes up green, so the second press is the one that ends the day.
            ResolveByColour(sleeping, level);

            // Day 5: a sign telling you not to press the button you need.
            GameObject warned = SpecialButton("Warned Green Button", new Vector3(-4.6f, 0f, 0.2f),
                "DO NOT PRESS");
            SetStartGreen(warned);
            ResolveByColour(warned, level);

            // Day 7: pushes your aim away as you try to point at it.
            GameObject slippery = SpecialButton("Magnetic Button", new Vector3(-4.6f, 0f, -2.6f),
                "PUSHES YOUR AIM AWAY");
            SetStartGreen(slippery);
            slippery.AddComponent<CursorRepellingButton>();
            ResolveByColour(slippery, level);

            // Days 8-12: the button that argues with you.
            GameObject talker = SpecialButton("Talking Button", new Vector3(4.6f, 0f, 0.2f),
                "TALKS WHEN YOU APPROACH");
            SetStartGreen(talker);
            ResolveByColour(talker, level);
            var talking = talker.AddComponent<TalkingButton>();
            SetPrivateStringList(talking, "lines", new[]
            {
                "Please. Do not press me again.",
                "Press the big red button instead.",
                "There is a secret ending. You would like it."
            });

            // Day 20: walks towards the player and presses against them.
            GameObject chaser = SpecialButton("Chasing Red Button", new Vector3(4.6f, 0f, -2.6f),
                "FOLLOWS YOU - STAYS RED");
            var chase = chaser.AddComponent<ButtonMover>();
            SetPrivate(chase, "mode", (int)ButtonMover.MoveMode.ChasePlayer);
            SetPrivate(chase, "speed", 1.5f);
            ResolveByColour(chaser, level);

            // Day 21: slips around to stay behind your back.
            GameObject sneak = SpecialButton("Sneaking Green Button", new Vector3(0f, 0f, -2.6f),
                "STAYS BEHIND YOU");
            SetStartGreen(sneak);
            var behind = sneak.AddComponent<ButtonMover>();
            SetPrivate(behind, "mode", (int)ButtonMover.MoveMode.StayBehindPlayer);
            ResolveByColour(sneak, level);

            // A plain moving button, for platform-style days.
            GameObject patrol = SpecialButton("Patrolling Button", new Vector3(1.6f, 0f, -2.6f),
                "SLIDES BACK AND FORTH");
            var patrolMover = patrol.AddComponent<ButtonMover>();
            SetPrivate(patrolMover, "mode", (int)ButtonMover.MoveMode.Patrol);
            SetPrivate(patrolMover, "speed", 1.6f);

            // Every showcase press is logged, so the Console shows which state was pressed.
            foreach (GameObject item in new[]
                     {
                         timed, shy, compass, sleeping, warned, slippery, talker, chaser, sneak, patrol
                     })
            {
                var button = item.GetComponentInChildren<ButtonInteractable>();
                if (button != null)
                    UnityEventTools.AddPersistentListener(button.OnPressed, button.LogPress);
            }
        }

        /// <summary>
        /// Makes a colour-changing button honour the colour it is showing: pressing it
        /// while green completes the day, while red it restarts the day.
        /// </summary>
        private static void ResolveByColour(GameObject cap, DayLevel level)
        {
            var resolver = cap.AddComponent<StatefulDayButton>();
            SetPrivate(resolver, "day", level);
        }

        private static void SetStartGreen(GameObject cap)
        {
            var appearance = cap.GetComponent<ButtonAppearance>();
            if (appearance != null)
                SetPrivate(appearance, "greenness", 1f);
        }

        /// <summary>
        /// A button on a pedestal with a sign above it and its own colour control, ready
        /// for one of the special behaviours to be attached.
        /// </summary>
        private static GameObject SpecialButton(string name, Vector3 position, string signText)
        {
            GameObject cap = CreateButtonBody(name, position, redMaterial, 1.15f);
            var button = cap.GetComponent<ButtonInteractable>();
            SetPrivate(button, "prompt", "Press " + name.ToLowerInvariant());
            SetPrivate(button, "cooldown", 0.6f);
            SetPrivate(button, "showPressedIndicator", false);

            cap.AddComponent<ButtonAppearance>();
            // The ding is given to every showcase button; ButtonSound only plays it
            // when the button is actually showing green as it is pressed.
            AddButtonSound(cap, DingClipPath);

            var sign = cap.AddComponent<ButtonSign>();
            SetPrivate(sign, "text", signText);
            return cap;
        }

        private static void SetPrivateStringList(Object target, string field, string[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || !property.isArray)
            {
                Debug.LogError($"{target.GetType().Name} has no list field \"{field}\".");
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).stringValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A flat green pad that presses when the player walks or lands on it.</summary>
        private static GameObject CreateFloorButton(Vector3 position, DayLevel level)
        {
            Box("Floor Button housing", position + Vector3.up * 0.02f,
                new Vector3(1.8f, 0.04f, 1.8f), trimMaterial);
            GameObject pad = Box("Floor Button", position + Vector3.up * 0.06f,
                new Vector3(1.5f, 0.08f, 1.5f), greenMaterial);

            var button = pad.AddComponent<ButtonInteractable>();
            SetPrivate(button, "prompt", "Step on the floor button");
            SetPrivate(button, "cooldown", 1f);

            var padLook = pad.AddComponent<ButtonAppearance>();
            SetPrivate(padLook, "greenness", 1f);
            AddButtonSound(pad, DingClipPath);
            UnityEventTools.AddPersistentListener(button.OnPressed, button.LogPress);
            return pad;
        }

        /// <summary>
        /// Adds a small light above a button and links it to that button's own
        /// "Show Pressed Indicator" field, so it can be switched off in the inspector.
        /// </summary>
        private static void AddIndicator(ButtonInteractable button, Material colour)
        {
            Vector3 position = button.transform.position + Vector3.up * 0.55f;
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicator.name = button.name + " pressed indicator";
            indicator.transform.position = position;
            indicator.transform.localScale = Vector3.one * 0.22f;
            indicator.GetComponent<Renderer>().sharedMaterial = colour;
            Object.DestroyImmediate(indicator.GetComponent<Collider>());
            indicator.transform.SetParent(button.transform.parent != null
                ? button.transform.parent : button.transform, true);
            indicator.SetActive(false);

            SetPrivate(button, "showPressedIndicator", true);
            SetPrivate(button, "pressedIndicator", indicator);
        }

        private static string BuildDay(int day, InputActionAsset actions)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildRoom(day);
            CreateLighting();

            GameObject player = FirstPersonSetup.CreatePlayerForLevel(actions, new Vector3(0f, 0.1f, -4.5f));
            var dayHost = new GameObject("Day");
            var title = dayHost.AddComponent<DayTitle>();
            var level = dayHost.AddComponent<DayLevel>();
            SetPrivate(level, "dayNumber", day);
            SetPrivate(level, "delayBeforeNextDay", 1.6f);

            // Day 1 opens the game: black screen, narration, then the room fades in.
            if (day == 1)
                CreateOpening(dayHost, player, title);

            CreateDayContent(day, level);

            string path = $"{DayFolder}/Day {day}.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        private static void CreateDayContent(int day, DayLevel level)
        {
            switch (day)
            {
                case 1:
                    // "There is a big Red Button, directly to the left is a Green Button."
                    CreateRedButton("Big Red Button", new Vector3(1.1f, 0f, 2.2f), level);
                    CreateGreenButton(new Vector3(-1.1f, 0f, 2.2f), level, delay: 0f);
                    break;

                case 2:
                    // "The Red Button is directly in front of the player. The Green Button is farther away."
                    CreateRedButton("Big Red Button", new Vector3(0f, 0f, 1.6f), level);
                    CreateGreenButton(new Vector3(-5.4f, 0f, 8.4f), level, delay: 0f);
                    break;

                default:
                    // "The Green Button is hidden for 5 seconds."
                    CreateRedButton("Big Red Button", new Vector3(0f, 0f, 2f), level);
                    CreateGreenButton(new Vector3(4.6f, 0f, 6.4f), level, delay: 5f);
                    break;
            }
        }

        /// <summary>
        /// Builds the chamber. <paramref name="withDividers"/> is off for the test scene,
        /// which needs one open floor for the showcase buttons.
        /// </summary>
        private static void BuildRoom(int day, bool withDividers = true)
        {
            float halfWidth = day == 1 ? 6f : 9f;
            float depth = day == 1 ? 12f : 20f;
            float height = 4f;
            float back = day == 1 ? 7f : 12f;
            float front = depth - back;

            Box("Floor", new Vector3(0f, -0.25f, (back - front) * 0.5f),
                new Vector3(halfWidth * 2f, 0.5f, depth), floorMaterial);
            Box("Ceiling", new Vector3(0f, height, (back - front) * 0.5f),
                new Vector3(halfWidth * 2f, 0.4f, depth), wallMaterial);
            Box("Wall (back)", new Vector3(0f, height * 0.5f, back),
                new Vector3(halfWidth * 2f, height, 0.5f), wallMaterial);
            Box("Wall (front)", new Vector3(0f, height * 0.5f, -front),
                new Vector3(halfWidth * 2f, height, 0.5f), wallMaterial);
            Box("Wall (left)", new Vector3(-halfWidth, height * 0.5f, (back - front) * 0.5f),
                new Vector3(0.5f, height, depth), wallMaterial);
            Box("Wall (right)", new Vector3(halfWidth, height * 0.5f, (back - front) * 0.5f),
                new Vector3(0.5f, height, depth), wallMaterial);

            // Quiet panel lines, the way a clean test chamber is broken into sections.
            for (float z = -front + 2f; z < back; z += 4f)
            {
                Box("Trim", new Vector3(-halfWidth + 0.26f, 1.15f, z),
                    new Vector3(0.08f, 0.06f, 3.6f), trimMaterial);
                Box("Trim", new Vector3(halfWidth - 0.26f, 1.15f, z),
                    new Vector3(0.08f, 0.06f, 3.6f), trimMaterial);
            }

            if (!withDividers)
                return;

            if (day >= 2)
            {
                // A short detour so the green button is a walk away, not a glance away.
                Box("Divider", new Vector3(-2.2f, height * 0.5f, 4.2f),
                    new Vector3(6.6f, height, 0.5f), wallMaterial);
                Box("Divider", new Vector3(3.4f, height * 0.5f, 4.2f),
                    new Vector3(4.2f, height, 0.5f), wallMaterial);
            }

            if (day >= 3)
                Box("Divider", new Vector3(1.6f, height * 0.5f, 9.2f),
                    new Vector3(0.5f, height, 6.2f), wallMaterial);
        }

        private static GameObject CreateRedButton(string name, Vector3 position, DayLevel level)
        {
            GameObject cap = CreateButtonBody(name, position, redMaterial, 1.15f);
            var button = cap.GetComponent<ButtonInteractable>();
            SetPrivate(button, "prompt", "Press the big red button");
            SetPrivate(button, "showPressedIndicator", false);
            SetPrivate(button, "oneShot", false);

            // Declares itself red, so the ding correctly stays silent for this one.
            cap.AddComponent<ButtonAppearance>();
            AddButtonSound(cap, DingClipPath);

            // The red button is the thing you were told not to press: it fails the day.
            UnityEventTools.AddPersistentListener(button.OnPressed, level.FailDay);
            return cap;
        }

        private static GameObject CreateGreenButton(Vector3 position, DayLevel level, float delay)
        {
            GameObject cap = CreateButtonBody("Green Button", position, greenMaterial, 1.15f);
            var button = cap.GetComponent<ButtonInteractable>();
            SetPrivate(button, "prompt", "Press the green button");
            SetPrivate(button, "oneShot", true);
            SetPrivate(button, "showPressedIndicator", false);

            // Every button clicks; a button in a green state also dings.
            var appearance = cap.AddComponent<ButtonAppearance>();
            SetPrivate(appearance, "greenness", 1f);
            AddButtonSound(cap, DingClipPath);

            // This is the wiring that was missing: the green button ends the day.
            UnityEventTools.AddPersistentListener(button.OnPressed, level.CompleteDay);

            GameObject assembly = cap.transform.parent.gameObject;
            if (delay > 0f)
            {
                // The timer lives on the Day object so it keeps running while the button is hidden.
                var reveal = level.gameObject.AddComponent<TimedReveal>();
                SetPrivate(reveal, "delay", delay);
                SetPrivate(reveal, "target", assembly);
            }

            return assembly;
        }

        private static GameObject CreateButtonBody(string name, Vector3 position, Material colour,
            float capHeight)
        {
            var root = new GameObject(name + " assembly");
            root.transform.position = position;

            Box(name + " pedestal", position + Vector3.up * 0.5f, new Vector3(0.9f, 1f, 0.9f),
                pedestalMaterial).transform.SetParent(root.transform, true);
            Box(name + " housing", position + Vector3.up * 1.03f, new Vector3(1f, 0.08f, 1f),
                trimMaterial).transform.SetParent(root.transform, true);

            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = name;
            cap.transform.position = position + Vector3.up * capHeight;
            cap.transform.localScale = new Vector3(0.7f, 0.11f, 0.7f);
            cap.GetComponent<Renderer>().sharedMaterial = colour;
            cap.transform.SetParent(root.transform, true);
            cap.AddComponent<ButtonInteractable>();
            return cap;
        }

        /// <summary>
        /// Gives a button the shared press sound, plus an optional extra layer such as
        /// the green button's ding. Every button clicks when it is pressed.
        /// </summary>
        private static ButtonSound AddButtonSound(GameObject host, string extraClipPath)
        {
            var press = AssetDatabase.LoadAssetAtPath<AudioClip>(ClickClipPath);
            if (press == null)
                Debug.LogWarning($"Missing \"{ClickClipPath}\"; buttons will have no press sound.");

            AudioClip extra = null;
            if (!string.IsNullOrEmpty(extraClipPath))
            {
                extra = AssetDatabase.LoadAssetAtPath<AudioClip>(extraClipPath);
                if (extra == null)
                    Debug.LogWarning($"Missing \"{extraClipPath}\"; that layer will be silent.");
            }

            var sound = host.AddComponent<ButtonSound>();
            SetPrivate(sound, "pressClip", press);
            SetPrivate(sound, "greenClip", extra);
            return sound;
        }

        private static void CreateOpening(GameObject dayHost, GameObject player, DayTitle title)
        {
            var opening = dayHost.AddComponent<OpeningSequence>();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(OpeningClipPath);
            if (clip == null)
                Debug.LogWarning($"Missing \"{OpeningClipPath}\"; the opening will run silently.");

            SetPrivate(opening, "openingNarration", clip);
            SetPrivate(opening, "frozenDuringOpening", player.GetComponent<FirstPersonController>());

            // The DAY 1 title waits for the opening instead of playing under the black screen.
            SetPrivate(title, "playOnStart", false);
            UnityEventTools.AddPersistentListener(opening.OnOpeningFinished, title.PlayCurrentDay);
        }

        private static void CreateLighting()
        {
            var sun = new GameObject("Directional Light");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.55f;
            light.color = new Color(0.92f, 0.95f, 1f);
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(58f, 18f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.53f, 0.58f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.23f, 0.26f);
        }

        private static GameObject Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        private static void CreateMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets", "LevelMaterials");

            wallMaterial = Material("Chamber Wall", new Color(0.82f, 0.82f, 0.8f), 0.35f);
            floorMaterial = Material("Chamber Floor", new Color(0.46f, 0.47f, 0.49f), 0.25f);
            trimMaterial = Material("Chamber Trim", new Color(0.3f, 0.31f, 0.33f), 0.5f);
            pedestalMaterial = Material("Button Pedestal", new Color(0.63f, 0.64f, 0.65f), 0.4f);
            redMaterial = Material("Button Red", new Color(0.72f, 0.05f, 0.04f), 0.3f);
            greenMaterial = Material("Button Green", new Color(0.09f, 0.55f, 0.16f), 0.3f);
        }

        private static Material Material(string name, Color colour, float smoothness)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (existing != null)
            {
                existing.shader = shader;
                existing.color = colour;
                existing.SetFloat("_Smoothness", smoothness);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var material = new Material(shader) { color = colour };
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SetPrivate(Object target, string field, object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"{target.GetType().Name} has no serialized field \"{field}\".");
                return;
            }

            switch (value)
            {
                case int i: property.intValue = i; break;
                case float f: property.floatValue = f; break;
                case bool b: property.boolValue = b; break;
                case string s: property.stringValue = s; break;
                case null: property.objectReferenceValue = null; break;
                case Object o: property.objectReferenceValue = o; break;
                default:
                    Debug.LogError($"Unsupported value type for \"{field}\".");
                    return;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
