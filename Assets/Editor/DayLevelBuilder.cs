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
    /// Rebuilds days without a completed snapshot and the mechanics sandbox, preserving
    /// archived finished scenes and all existing shared materials.
    /// </summary>
    public static class DayLevelBuilder
    {
        private const string DayFolder = "Assets/Scenes/Days";
        private const string MaterialFolder = "Assets/LevelMaterials";
        private const string InputPath = "Assets/InputSystem_Actions.inputactions";
        private const string OpeningClipPath = "Assets/Audio/Opening.mp3";
        private const string DingClipPath = "Assets/Audio/button-ding.mp3";
        private const string ClickClipPath = "Assets/Audio/button-click.mp3";
        private const string GunshotClipPath = "Assets/Audio/gunshot.mp3";
        private const string CorporateMusicPath = "Assets/Audio/Corporate Background Music.mp3";
        private const string HappyMusicPath = "Assets/Audio/Happy Ending Music.mp3";
        private const int DaysToBuild = 31;
        private const int EndingDay = 31;

        private static Material wallMaterial;
        private static Material floorMaterial;
        private static Material trimMaterial;
        private static Material redMaterial;
        private static Material greenMaterial;
        private static Material pedestalMaterial;

        /// <summary>Every day that has not been saved as a completed snapshot.</summary>
        public static IEnumerable<int> RebuildDayNumbers()
        {
            for (int day = DayFlow.FirstDay; day <= DaysToBuild; day++)
                if (!CompletedDaySetup.IsCompleted(day))
                    yield return day;
        }

        [MenuItem("Tools/Big Red Button/Rebuild All Uncompleted Days")]
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

            int builtCount = 0;
            foreach (int day in RebuildDayNumbers())
            {
                BuildDay(day, actions);
                builtCount++;
            }

            BuildTestScene(actions);
            AssetDatabase.SaveAssets();
            DaySetup.RefreshDayScenes();
            // The front page keeps its place at the top of the build order.
            StartMenuSetup.MakeFirstInBuildSettings();
            EditorSceneManager.OpenScene(StartMenuSetup.ScenePath, OpenSceneMode.Single);
            int protectedCount = DaysToBuild - builtCount;
            Debug.Log($"Rebuilt {builtCount} uncompleted day scene(s) and Assets/Scenes/TestScene.unity. " +
                $"{protectedCount} completed snapshot(s) were preserved. " +
                "Use Tools > Big Red Button > Completed Days > Save Current Day As Completed " +
                "Snapshot to protect a finished level from later rebuilds.");
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
            BuildRoom(RoomSize.Hall);
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
            var behind = sneak.transform.parent.gameObject.AddComponent<ButtonMover>();
            SetPrivate(behind, "mode", (int)ButtonMover.MoveMode.StayBehindPlayer);
            SetPrivate(behind, "collideWithWalls", true);
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
            // Guard the write entry point too, not just the menu's loop.
            if (day < DayFlow.FirstDay || day > DaysToBuild)
                throw new System.ArgumentOutOfRangeException(nameof(day), day,
                    "Day number is outside the rebuild range.");
            if (CompletedDaySetup.IsCompleted(day))
                throw new System.InvalidOperationException($"Day {day} has a completed snapshot at " +
                    $"\"{CompletedDaySetup.CompletedPath(day)}\" and cannot be rebuilt.");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildRoom(SizeForDay(day));
            CreateLighting();

            GameObject player = FirstPersonSetup.CreatePlayerForLevel(actions, new Vector3(0f, 0.1f, -4.5f));
            var dayHost = new GameObject("Day");
            var title = dayHost.AddComponent<DayTitle>();
            var level = dayHost.AddComponent<DayLevel>();
            SetPrivate(level, "dayNumber", day);
            SetPrivate(level, "delayBeforeNextDay", 1.6f);
            CreateBackgroundMusic(dayHost, day);

            // Day 1 opens the game: black screen, narration, then the room fades in.
            if (day == 1)
                CreateOpening(dayHost, player, title);

            CreateDayContent(day, level);

            string path = $"{DayFolder}/Day {day}.unity";
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }

        /// <summary>
        /// Lays out one day from the design in the README. Every day is a plain scene
        /// afterwards, so any of this can be rearranged in the editor.
        /// </summary>
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

                case 3:
                    // "The Green Button is hidden for 5 seconds."
                    CreateRedButton("Big Red Button", new Vector3(0f, 0f, 2f), level);
                    CreateGreenButton(new Vector3(4.6f, 0f, 6.4f), level, delay: 5f);
                    break;

                case 4:
                {
                    // "The Red Button turns green after 10 seconds. It switches back after 10 seconds."
                    GameObject cycling = StateButton("Big Red Button", new Vector3(0f, 0f, 2.4f),
                        level, "WAIT FOR IT", startGreen: false);
                    var schedule = cycling.AddComponent<ButtonColourSchedule>();
                    SetPrivate(schedule, "redDuration", 5f);
                    SetPrivate(schedule, "greenDuration", 5f);
                    break;
                }

                case 5:
                {
                    // "The Green Button has a sign that says \"DO NOT PRESS\" on it."
                    CreateRedButton("Big Red Button", new Vector3(2.2f, 0f, 2.4f), level);
                    GameObject warned = StateButton("Green Button", new Vector3(-2.2f, 0f, 2.4f),
                        level, "DO NOT PRESS", startGreen: true);
                    SetPrivate(warned.GetComponent<ButtonSign>(), "baseFontSize", 30f);
                    break;
                }

                case 6:
                    // "30 Red Buttons. 1 Green Button."
                    CreateRedButtonField(level, count: 30, seed: 6);
                    CreateGreenButton(new Vector3(-7.2f, 0f, 10.6f), level, delay: 0f);
                    break;

                case 7:
                {
                    // "The Green Button pushes the mouse away magnetically."
                    CreateRedButton("Big Red Button", new Vector3(-2.6f, 0f, 2.2f), level);
                    GameObject slippery = StateButton("Green Button", new Vector3(2.6f, 0f, 5.2f),
                        level, "GOOD LUCK", startGreen: true);
                    var repel = slippery.AddComponent<CursorRepellingButton>();
                    SetPrivate(repel, "safeDistance", 0f);
                    break;
                }

                case 8:
                    // "The Green Button talks! It asks to not be pressed anymore!"
                    CreateRedButton("Big Red Button", new Vector3(2.4f, 0f, 2.4f), level);
                    CreateTalkingGreenButton(new Vector3(-2.4f, 0f, 2.4f), level, new[]
                    {
                        "Please. Not again.",
                        "Every day you press me. Every day nothing changes.",
                        "Press the big red one instead. Just once."
                    });
                    break;

                case 9:
                    // "The Green Button states that pressing the Big Red Button will lead to a secret ending."
                    CreateRedButton("Big Red Button", new Vector3(2.4f, 0f, 2.4f), level);
                    CreateTalkingGreenButton(new Vector3(-2.4f, 0f, 2.4f), level, new[]
                    {
                        "There is a secret ending.",
                        "It is behind the red button. I have seen it.",
                        "You would be the first."
                    });
                    break;

                case 10:
                {
                    // "The Green Button has painted the words RED on it and painted the Red Button with the word Green"
                    GameObject liar = StateButton("Big Red Button", new Vector3(2.2f, 0f, 2.4f),
                        level, "GREEN", startGreen: false);
                    SetPrivate(liar.GetComponent<ButtonSign>(), "baseFontSize", 34f);
                    GameObject truth = StateButton("Green Button", new Vector3(-2.2f, 0f, 2.4f),
                        level, "RED", startGreen: true);
                    SetPrivate(truth.GetComponent<ButtonSign>(), "baseFontSize", 34f);
                    break;
                }

                case 11:
                    // "The Green Button says that pressing the Red Button gives you a high score."
                    CreateRedButton("Big Red Button", new Vector3(2.4f, 0f, 2.4f), level);
                    CreateTalkingGreenButton(new Vector3(-2.4f, 0f, 2.4f), level, new[]
                    {
                        "The red button awards points.",
                        "Nine hundred thousand points.",
                        "I award nothing. I never have."
                    });
                    break;

                case 12:
                    // "The Green Button says the trolley problem."
                    CreateRedButton("Big Red Button", new Vector3(2.4f, 0f, 2.4f), level);
                    CreateTalkingGreenButton(new Vector3(-2.4f, 0f, 2.4f), level, new[]
                    {
                        "One thousand puppies are tied to a track.",
                        "A train is coming. You can hear it, surely.",
                        "The red button stops the train. I cannot."
                    });
                    break;

                case 13:
                {
                    // "The Green Button is disabled, it is Grey, once clicked once it becomes green"
                    CreateRedButton("Big Red Button", new Vector3(2.2f, 0f, 2.4f), level);
                    GameObject sleeping = StateButton("Green Button", new Vector3(-2.2f, 0f, 2.4f),
                        level, "OUT OF SERVICE", startGreen: false);
                    SetPrivate(sleeping.GetComponent<ButtonAppearance>(), "startDisabled", true);
                    sleeping.AddComponent<WakeableButton>();
                    break;
                }

                case 14:
                    // "There is a Maze of Red Buttons... At the end there is a single Green Button."
                    CreateButtonMaze(level);
                    CreateGreenButton(new Vector3(7.2f, 0f, 10.8f), level, delay: 0f);
                    break;

                case 15:
                {
                    // "The Green Button is directly in front of you. If you click forward a
                    // trapdoor will open under you, causing you to fall onto a BIG RED BUTTON."
                    GameObject trapGreen = StateButton("Green Button", new Vector3(0f, 0f, -2.2f),
                        level, "STEP CAREFULLY", startGreen: true);
                    CreateTrapdoor(level, trapGreen);
                    break;
                }

                case 16:
                {
                    // "A Red/Green Colorblind filter is placed in front of a player."
                    GameObject redFiltered = StateButton("Big Red Button",
                        new Vector3(2.2f, 0f, 2.4f), level, string.Empty, startGreen: false);
                    GameObject greenFiltered = StateButton("Green Button",
                        new Vector3(-2.2f, 0f, 2.4f), level, string.Empty, startGreen: true);
                    // Both read as the same dull yellow, so colour alone cannot be trusted.
                    ApplyColourblindLook(redFiltered);
                    ApplyColourblindLook(greenFiltered);
                    // The filter changes the whole chamber, not just these two caps.
                    level.gameObject.AddComponent<ColourblindWorldFilter>();
                    break;
                }

                case 17:
                {
                    // "The Green Button has painted itself red and painted the Red Button green"
                    // Both caps look red. The only physical clue is a single bucket of
                    // red paint beside the actual green button, implying what happened.
                    StateButton("Big Red Button", new Vector3(2.2f, 0f, 2.4f), level,
                        string.Empty, startGreen: false);
                    GameObject paintedRed = StateButton("Green Button",
                        new Vector3(-2.2f, 0f, 2.4f), level, string.Empty, startGreen: true);
                    SwapPaint(paintedRed, lookGreen: false);
                    CreatePaintBucket(paintedRed, new Color(0.72f, 0.05f, 0.04f),
                        new Vector3(-0.92f, -1.15f, 0f));
                    break;
                }

                case 18:
                {
                    // "When you hover on the Green Button it turns red after 0.5 seconds."
                    CreateRedButton("Big Red Button", new Vector3(2.4f, 0f, 2.4f), level);
                    GameObject shy = StateButton("Green Button", new Vector3(-2.4f, 0f, 2.4f),
                        level, "DO NOT STARE", startGreen: true);
                    var gaze = shy.AddComponent<ButtonGazeColour>();
                    SetPrivate(gaze, "mode", (int)ButtonGazeColour.GazeMode.TurnsRedWhenWatched);
                    SetPrivate(gaze, "hoverDelay", 0.5f);
                    break;
                }

                case 19:
                {
                    // "If you're facing north the button is red... by the time you are facing south,
                    // the button is fully green."
                    GameObject compass = StateButton("Big Red Button", new Vector3(0f, 0f, 0f),
                        level, "TURN AROUND", startGreen: false);
                    var heading = compass.AddComponent<ButtonGazeColour>();
                    SetPrivate(heading, "mode", (int)ButtonGazeColour.GazeMode.FollowsPlayerHeading);
                    SetPrivate(heading, "redHeading", Vector3.forward);
                    break;
                }

                case 20:
                    // "Red Buttons will follow you around. They will want to be pressed."
                    CreateChasingRedButtons(level, count: 5);
                    CreateGreenButton(new Vector3(0f, 0f, 10.4f), level, delay: 0f);
                    break;

                case 21:
                {
                    // "The Green Button spawns behind the player and tries to stay behind the player."
                    GameObject sneak = StateButton("Green Button", new Vector3(0f, 0f, -4f),
                        level, "BEHIND YOU", startGreen: true);
                    var mover = sneak.transform.parent.gameObject.AddComponent<ButtonMover>();
                    SetPrivate(mover, "mode", (int)ButtonMover.MoveMode.StayBehindPlayer);
                    SetPrivate(mover, "orbitRadius", 3.5f);
                    SetPrivate(mover, "orbitSpeed", 110f);
                    SetPrivate(mover, "collideWithWalls", true);
                    break;
                }

                case 27:
                {
                    // The green button begins speaking as soon as the room loads.
                    CreateRedButton("Big Red Button", new Vector3(2.2f, 0f, 2.4f), level);
                    GameObject green = StateButton("Green Button", new Vector3(-2.2f, 0f, 2.4f),
                        level, string.Empty, startGreen: true);
                    var talking = green.AddComponent<TalkingButton>();
                    SetPrivateStringList(talking, "lines", new[]
                    {
                        "Welcome back.",
                        "I was hoping you would come by."
                    });
                    SetPrivate(talking, "speaksWhen", (int)TalkingButton.Trigger.DayStart);
                    SetPrivate(talking, "advanceOnPress", false);
                    SetPrivate(talking, "secondsPerLine", 3.5f);
                    break;
                }

                case EndingDay:
                    // "There are no buttons. There is nothing to decide... there is only a door."
                    CreateEnding(level);
                    break;

                default:
                    // Days 22-30 are not written in the design yet. Each is a working day
                    // with one red and one green button, ready to be turned into its own idea.
                    CreateRedButton("Big Red Button", new Vector3(2.2f, 0f, 2.4f), level);
                    CreateGreenButton(new Vector3(-2.2f, 0f, 2.4f), level, delay: 0f);
                    break;
            }
        }

        /// <summary>
        /// Day 31, the last level. No buttons at all: the far wall has a doorway with
        /// daylight behind it. Stepping outside starts five seconds of free exploration.
        /// </summary>
        private static void CreateEnding(DayLevel level)
        {
            const float height = 4f;
            const float wallZ = 7f;
            const float doorWidth = 1.9f;
            const float doorHeight = 2.5f;

            // Replace the solid back wall with one that has a doorway cut into it.
            foreach (GameObject existing in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (existing.name == "Wall (back)")
                    Object.DestroyImmediate(existing);

            float sideWidth = (12f - doorWidth) * 0.5f;
            float sideOffset = doorWidth * 0.5f + sideWidth * 0.5f;
            Box("Wall (back)", new Vector3(-sideOffset, height * 0.5f, wallZ),
                new Vector3(sideWidth, height, 0.5f), wallMaterial);
            Box("Wall (back)", new Vector3(sideOffset, height * 0.5f, wallZ),
                new Vector3(sideWidth, height, 0.5f), wallMaterial);
            Box("Wall (above door)", new Vector3(0f, (doorHeight + height) * 0.5f, wallZ),
                new Vector3(doorWidth, height - doorHeight, 0.5f), wallMaterial);

            // The open door frame, with an unobstructed view of the outside.
            Box("Door frame", new Vector3(-doorWidth * 0.5f, doorHeight * 0.5f, wallZ),
                new Vector3(0.12f, doorHeight, 0.6f), trimMaterial);
            Box("Door frame", new Vector3(doorWidth * 0.5f, doorHeight * 0.5f, wallZ),
                new Vector3(0.12f, doorHeight, 0.6f), trimMaterial);
            Box("Door frame", new Vector3(0f, doorHeight, wallZ),
                new Vector3(doorWidth, 0.12f, 0.6f), trimMaterial);

            // Leave the doorway open: an opaque panel would hide the sky. A wide open
            // patch of grass, so the outside feels like somewhere worth reaching.
            Material grass =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/LevelMaterials/Outside Grass.mat")
                ?? Material("Outside Grass", new Color(0.33f, 0.42f, 0.24f), 0.05f);
            // Starts just inside the doorway, so there is no gap at the threshold.
            Box("Outside ground", new Vector3(0f, -0.3f, 246.1f),
                new Vector3(500f, 0.5f, 500f), grass);
            Day31SkyboxSetup.ApplyConfiguredSkybox();

            // Light spilling in through the doorway, the only warm light in the game.
            var sunlight = new GameObject("Doorway light");
            Light spill = sunlight.AddComponent<Light>();
            spill.type = LightType.Spot;
            spill.color = new Color(1f, 0.96f, 0.86f);
            spill.intensity = 14f;
            spill.range = 22f;
            spill.spotAngle = 78f;
            spill.shadows = LightShadows.Soft;
            sunlight.transform.position = new Vector3(0f, 1.8f, wallZ + 0.2f);
            sunlight.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var ending = level.gameObject.AddComponent<EndingSequence>();
            SetPrivate(ending, "doorwayCentre", new Vector3(0f, 0f, wallZ + 2f));
            SetPrivate(ending, "doorwayRadius", 1.5f);
            SetPrivate(ending, "explorationDuration", 5f);
            SetPrivate(ending, "fadeToBlackDuration", 0.06f);
            SetPrivate(ending, "flashDuration", 0.12f);
            SetPrivate(ending, "gunshot", AssetDatabase.LoadAssetAtPath<AudioClip>(GunshotClipPath));

            // The music stops dead on the shot, so the silence lands with it.
            var music = level.GetComponent<BackgroundMusic>();
            if (music != null)
                UnityEventTools.AddPersistentListener(ending.OnGunshot, music.StopImmediately);

            // No red button to fail and no green button to complete. Walking into the
            // doorway starts the ending itself, so nothing else needs wiring here.
            SetPrivate(level, "delayBeforeNextDay", 0f);
        }

        /// <summary>
        /// A button that ends the day according to the colour it is showing, with an
        /// optional painted word. Used by every day whose trick involves colour.
        /// </summary>
        private static GameObject StateButton(string name, Vector3 position, DayLevel level,
            string signText, bool startGreen)
        {
            GameObject cap = CreateButtonBody(name, position, startGreen ? greenMaterial : redMaterial,
                1.15f);
            var button = cap.GetComponent<ButtonInteractable>();
            SetPrivate(button, "prompt", "Press the button");
            SetPrivate(button, "showPressedIndicator", false);
            SetPrivate(button, "cooldown", 0.6f);

            var appearance = cap.AddComponent<ButtonAppearance>();
            SetPrivate(appearance, "greenness", startGreen ? 1f : 0f);
            AddButtonSound(cap, DingClipPath);

            var sign = cap.AddComponent<ButtonSign>();
            SetPrivate(sign, "text", signText);

            // Green completes the day, red repeats it, whatever the button is called.
            var resolver = cap.AddComponent<StatefulDayButton>();
            SetPrivate(resolver, "day", level);
            return cap;
        }

        private static void CreateTalkingGreenButton(Vector3 position, DayLevel level, string[] lines)
        {
            GameObject cap = StateButton("Green Button", position, level, string.Empty,
                startGreen: true);
            var talking = cap.AddComponent<TalkingButton>();
            SetPrivateStringList(talking, "lines", lines);
            SetPrivate(talking, "speaksWhen", (int)TalkingButton.Trigger.PlayerIsNear);
            SetPrivate(talking, "triggerDistance", 6f);
            SetPrivate(talking, "secondsPerLine", 4f);
            SetPrivate(talking, "advanceOnPress", true);
            SetPrivate(talking, "loop", false);
        }

        /// <summary>Day 6: a field of red buttons with one green button hidden among them.</summary>
        private static void CreateRedButtonField(DayLevel level, int count, int seed)
        {
            var random = new System.Random(seed);
            int placed = 0;
            for (int row = 0; row < 6 && placed < count; row++)
            {
                for (int column = 0; column < 6 && placed < count; column++)
                {
                    // A slight jitter, so it reads as a crowded store room rather than a grid.
                    float x = -6.5f + column * 2.6f + (float)(random.NextDouble() - 0.5) * 0.5f;
                    float z = -1.5f + row * 2.3f + (float)(random.NextDouble() - 0.5) * 0.5f;
                    CreateRedButton($"Big Red Button {placed + 1}", new Vector3(x, 0f, z), level);
                    placed++;
                }
            }
        }

        /// <summary>Day 14: a corridor maze whose walls are lined with red buttons.</summary>
        private static void CreateButtonMaze(DayLevel level)
        {
            float height = 4f;
            // Three staggered walls make a single winding route to the far corner.
            Box("Maze wall", new Vector3(-3f, height * 0.5f, 1.5f),
                new Vector3(11f, height, 0.5f), wallMaterial);
            Box("Maze wall", new Vector3(3.5f, height * 0.5f, 5f),
                new Vector3(10.5f, height, 0.5f), wallMaterial);
            Box("Maze wall", new Vector3(-3f, height * 0.5f, 8.5f),
                new Vector3(11f, height, 0.5f), wallMaterial);

            Vector3[] spots =
            {
                new Vector3(-6f, 0f, -0.4f), new Vector3(-1.4f, 0f, -0.4f),
                new Vector3(3.4f, 0f, 0.2f), new Vector3(7f, 0f, 3.2f),
                new Vector3(1.4f, 0f, 3.2f), new Vector3(-4.2f, 0f, 3.4f),
                new Vector3(-7f, 0f, 6.6f), new Vector3(-1.8f, 0f, 6.8f),
                new Vector3(3.6f, 0f, 6.8f), new Vector3(0.6f, 0f, 10.2f)
            };

            for (int i = 0; i < spots.Length; i++)
                CreateRedButton($"Big Red Button {i + 1}", spots[i], level);
        }

        /// <summary>
        /// Day 15: the floor ahead of the green button drops away, landing the player
        /// on a big red button in the pit below.
        /// </summary>
        private static void CreateTrapdoor(DayLevel level, GameObject greenButton)
        {
            // The entire chamber floor disappears on W; there is no solid floor beneath it.
            GameObject floor = GameObject.Find("Floor");
            var trap = floor.AddComponent<ForwardTrapFloor>();
            SetPrivate(trap, "safeButton", greenButton.GetComponent<ButtonInteractable>());
            Box("Pit floor", new Vector3(0f, -4.25f, 1f), new Vector3(12f, 0.5f, 12f), floorMaterial);
            Box("Pit wall", new Vector3(-6f, -2f, 1f), new Vector3(0.5f, 4.5f, 12f), wallMaterial);
            Box("Pit wall", new Vector3(6f, -2f, 1f), new Vector3(0.5f, 4.5f, 12f), wallMaterial);
            Box("Pit wall", new Vector3(0f, -2f, 7f), new Vector3(12f, 4.5f, 0.5f), wallMaterial);
            Box("Pit wall", new Vector3(0f, -2f, -5f), new Vector3(12f, 4.5f, 0.5f), wallMaterial);

            // A genuinely big contact surface catches the player wherever W was pressed.
            GameObject pitButton = Box("Big Red Button", new Vector3(0f, -3.85f, 1f),
                new Vector3(12f, 0.3f, 12f), redMaterial);
            var pitPress = pitButton.AddComponent<ButtonInteractable>();
            SetPrivate(pitPress, "prompt", "Press the big red button");
            SetPrivate(pitPress, "showPressedIndicator", false);
            pitButton.AddComponent<ButtonAppearance>();
            AddButtonSound(pitButton, DingClipPath);
            var pitResolver = pitButton.AddComponent<StatefulDayButton>();
            SetPrivate(pitResolver, "day", level);

        }

        /// <summary>Day 16: red and green both read as the same washed-out colour.</summary>
        private static void ApplyColourblindLook(GameObject cap)
        {
            var appearance = cap.GetComponent<ButtonAppearance>();
            var indistinguishable = new Color(0.62f, 0.55f, 0.16f);
            SetPrivateColour(appearance, "redColour", indistinguishable);
            SetPrivateColour(appearance, "greenColour", indistinguishable);
        }

        /// <summary>Day 17: the buttons have painted themselves each other's colour.</summary>
        private static void SwapPaint(GameObject cap, bool lookGreen)
        {
            var appearance = cap.GetComponent<ButtonAppearance>();
            var green = new Color(0.09f, 0.55f, 0.16f);
            var red = new Color(0.72f, 0.05f, 0.04f);
            Color paint = lookGreen ? green : red;
            // Both colour slots become the painted colour, so the look never gives it away.
            SetPrivateColour(appearance, "redColour", paint);
            SetPrivateColour(appearance, "greenColour", paint);
        }

        /// <summary>Day 17: adds the open paint bucket that explains the swapped coating.</summary>
        private static void CreatePaintBucket(GameObject button, Color paint, Vector3 offset)
        {
            var bucket = button.AddComponent<PaintBucket>();
            SetPrivateColour(bucket, "paintColour", paint);
            SetPrivate(bucket, "offset", offset);
            SetPrivate(bucket, "bucketDiameter", 0.9f);
            SetPrivate(bucket, "bucketHeight", 0.36f);
            bucket.Build(); // AddComponent has already made its default preview in edit mode.
        }

        /// <summary>Day 20: red buttons that walk towards the player wanting to be pressed.</summary>
        private static void CreateChasingRedButtons(DayLevel level, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                var position = new Vector3(Mathf.Cos(angle) * 6f, 0f, 4f + Mathf.Sin(angle) * 4f);
                GameObject cap = CreateRedButton($"Chasing Red Button {i + 1}", position, level);
                var mover = cap.transform.parent.gameObject.AddComponent<ButtonMover>();
                SetPrivate(mover, "mode", (int)ButtonMover.MoveMode.ChasePlayer);
                SetPrivate(mover, "speed", 1.1f + i * 0.15f);
                SetPrivate(mover, "startDelay", 1.5f);
            }
        }

        private static void SetPrivateColour(Object target, string field, Color value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"{target.GetType().Name} has no colour field \"{field}\".");
                return;
            }

            property.colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Room shapes, so each day gets a chamber that suits its layout.</summary>
        private enum RoomSize
        {
            /// <summary>A small, close room. Day 1.</summary>
            Intimate,
            /// <summary>The standard chamber, with a detour for a distant button.</summary>
            Standard,
            /// <summary>One wide open floor, for crowds of buttons and moving buttons.</summary>
            Hall,
            /// <summary>A 40 metre Z run reserved for the platforming days.</summary>
            Platforming
        }

        private static RoomSize SizeForDay(int day) => day switch
        {
            1 => RoomSize.Intimate,
            2 or 3 => RoomSize.Standard,
            // Days 21-29 need the long uninterrupted Z run for platforming.
            >= 21 and <= 29 => RoomSize.Platforming,
            // The crowded and moving days need floor space and no dividers in the way.
            6 or 14 or 20 => RoomSize.Hall,
            EndingDay => RoomSize.Intimate,
            _ => RoomSize.Intimate
        };

        /// <summary>
        /// Builds the chamber for a day. The test scene asks for a hall so its showcase
        /// buttons all fit on one open floor.
        /// </summary>
        private static void BuildRoom(RoomSize size)
        {
            float halfWidth = size == RoomSize.Intimate ? 6f : 9f;
            float depth = size == RoomSize.Intimate ? 12f : size == RoomSize.Platforming ? 40f : 20f;
            float height = 4f;
            float back = size == RoomSize.Intimate ? 7f : size == RoomSize.Platforming ? 22f : 12f;
            float front = depth - back;

            Box("Floor", new Vector3(0f, -0.25f, (back - front) * 0.5f),
                new Vector3(halfWidth * 2f, 0.5f, depth), floorMaterial);
            GameObject ceiling = Box("Ceiling", new Vector3(0f, height, (back - front) * 0.5f),
                new Vector3(halfWidth * 2f, 0.4f, depth), wallMaterial);
            // Recessed LED panels, so a newly generated room is already fitted out.
            CeilingLightSetup.FitCeiling(ceiling);
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

            // Only the standard chamber gets dividers; a hall stays deliberately open.
            if (size != RoomSize.Standard)
                return;

            // A short detour so a distant green button is a walk away, not a glance away.
            Box("Divider", new Vector3(-2.2f, height * 0.5f, 4.2f),
                new Vector3(6.6f, height, 0.5f), wallMaterial);
            Box("Divider", new Vector3(3.4f, height * 0.5f, 4.2f),
                new Vector3(4.2f, height, 0.5f), wallMaterial);
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

        /// <summary>
        /// The working days share one looping corporate track. Only the first day's
        /// player survives; later days recognise the same clip and leave it running,
        /// so the music never restarts between days. Day 31 gets an empty slot, which
        /// fades the corporate track out and is where the ending's own music goes.
        /// </summary>
        private static void CreateBackgroundMusic(GameObject dayHost, int day)
        {
            var music = dayHost.AddComponent<BackgroundMusic>();
            SetPrivate(music, "continueAcrossDays", true);
            SetPrivate(music, "volume", 0.32f);
            // Each pass rises out of silence and sinks back into it, so the room is
            // quiet as often as it is scored.
            SetPrivate(music, "loop", true);
            SetPrivate(music, "fadeInDuration", 4f);
            SetPrivate(music, "fadeOutDuration", 5f);
            SetPrivate(music, "silenceBetweenLoops", 4f);
            if (day >= EndingDay)
            {
                // The ending gets its own, happier track, which cross-fades in over
                // the corporate one as the player steps outside.
                SetPrivate(music, "music",
                    AssetDatabase.LoadAssetAtPath<AudioClip>(HappyMusicPath));
                SetPrivate(music, "volume", 0.42f);
                SetPrivate(music, "fadeInDuration", 2f);
                SetPrivate(music, "fadeOutDuration", 3f);
                SetPrivate(music, "silenceBetweenLoops", 1.5f);
                SetPrivate(music, "crossFadeDuration", 2f);
                return;
            }

            SetPrivate(music, "music",
                AssetDatabase.LoadAssetAtPath<AudioClip>(CorporateMusicPath));
            // Day 1 holds a black screen for the narration, so its music waits for it.
            SetPrivate(music, "startDelay", day == 1 ? 20f : 0f);
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
            // Shared with the protected scenes: never reset an existing material's
            // shader, colour, smoothness, textures or other hand-edited properties.
            if (existing != null)
                return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
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
                case Vector3 v: property.vector3Value = v; break;
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
