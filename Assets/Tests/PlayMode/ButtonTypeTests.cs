using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BigRedButton.Tests
{
    public sealed class ButtonTypeTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null)
                    Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        private GameObject Track(GameObject item)
        {
            objects.Add(item);
            return item;
        }

        private GameObject CreateCap(string name = "Button")
        {
            GameObject cap = Track(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
            cap.name = name;
            cap.transform.position = new Vector3(0f, 1f, 3f);
            cap.AddComponent<ButtonInteractable>();
            return cap;
        }

        [UnityTest]
        public IEnumerator EveryButtonPlaysThePressSoundAndGreenAlsoDings()
        {
            GameObject cap = CreateCap();
            var sound = cap.AddComponent<ButtonSound>();
            var press = AudioClip.Create("press", 4410, 1, 44100, false);
            var ding = AudioClip.Create("ding", 4410, 1, 44100, false);
            Set(sound, "pressClip", press);
            Set(sound, "extraClip", ding);
            Set(sound, "extraDelay", 0f);
            yield return null;

            var source = cap.GetComponent<AudioSource>();
            Assert.That(source, Is.Not.Null, "The sound component provides its own AudioSource.");
            cap.GetComponent<ButtonInteractable>().Interact(null);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SuppressedButtonStillClicksButDoesNotRunItsEvent()
        {
            GameObject cap = CreateCap();
            var button = cap.GetComponent<ButtonInteractable>();
            Set(button, "cooldown", 0f);

            int plainPresses = 0;
            int inspectorPresses = 0;
            button.Pressed += () => plainPresses++;
            button.OnPressed.AddListener(() => inspectorPresses++);

            button.SuppressEvents = true;
            yield return null;
            button.Interact(null);
            Assert.That(plainPresses, Is.EqualTo(1), "Sounds still hear the press.");
            Assert.That(inspectorPresses, Is.Zero, "The level wiring stays silent.");

            button.SuppressEvents = false;
            button.Interact(null);
            Assert.That(inspectorPresses, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AppearanceBlendsBetweenRedAndGreenPerButton()
        {
            GameObject first = CreateCap("First");
            GameObject second = CreateCap("Second");
            var a = first.AddComponent<ButtonAppearance>();
            var b = second.AddComponent<ButtonAppearance>();
            yield return null;

            a.SetGreen();
            b.SetRed();
            Assert.That(a.CurrentColour.g, Is.GreaterThan(a.CurrentColour.r));
            Assert.That(b.CurrentColour.r, Is.GreaterThan(b.CurrentColour.g),
                "Tinting one button must not recolour another.");

            a.Greenness = 0.5f;
            Assert.That(a.Greenness, Is.EqualTo(0.5f));
            a.IsDisabledLook = true;
            Color grey = a.CurrentColour;
            Assert.That(Mathf.Abs(grey.r - grey.g), Is.LessThan(0.05f), "The disabled look is grey.");
        }

        [UnityTest]
        public IEnumerator TimedColourButtonSwitchesAndStopsWhenPressed()
        {
            GameObject cap = CreateCap();
            var appearance = cap.AddComponent<ButtonAppearance>();
            var schedule = cap.AddComponent<ButtonColourSchedule>();
            Set(schedule, "redDuration", 0.15f);
            Set(schedule, "greenDuration", 5f);
            Set(schedule, "blendDuration", 0f);
            yield return null;
            Assert.That(appearance.Greenness, Is.Zero, "It starts red.");

            yield return new WaitForSeconds(0.3f);
            Assert.That(schedule.LooksGreen, Is.True, "It turns green after its delay.");

            cap.GetComponent<ButtonInteractable>().Interact(null);
            float afterPress = appearance.Greenness;
            yield return new WaitForSeconds(0.2f);
            Assert.That(appearance.Greenness, Is.EqualTo(afterPress), "Pressing stops the cycle.");
        }

        [UnityTest]
        public IEnumerator WatchedButtonTurnsRedThenRecoversWhenLookedAway()
        {
            GameObject cameraObject = Track(new GameObject("Camera", typeof(Camera)));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = Vector3.zero;
            cameraObject.transform.rotation = Quaternion.identity;

            GameObject cap = CreateCap();
            cap.transform.position = new Vector3(0f, 0f, 4f); // Straight ahead of the camera.
            var appearance = cap.AddComponent<ButtonAppearance>();
            var gaze = cap.AddComponent<ButtonGazeColour>();
            Set(gaze, "mode", (int)ButtonGazeColour.GazeMode.TurnsRedWhenWatched);
            Set(gaze, "hoverDelay", 0.1f);
            Set(gaze, "blendDuration", 0f);
            Set(gaze, "viewer", cameraObject.transform);
            yield return null;
            Assert.That(appearance.Greenness, Is.EqualTo(1f), "It looks green until watched.");

            yield return new WaitForSeconds(0.25f);
            Assert.That(gaze.IsWatched, Is.True);
            Assert.That(appearance.Greenness, Is.Zero, "Watching it turns it red.");

            cameraObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            yield return null;
            yield return null;
            Assert.That(appearance.Greenness, Is.EqualTo(1f), "Looking away lets it recover.");
        }

        [UnityTest]
        public IEnumerator HeadingButtonIsRedOneWayAndGreenTheOther()
        {
            GameObject cameraObject = Track(new GameObject("Camera", typeof(Camera)));
            cameraObject.transform.rotation = Quaternion.identity;

            GameObject cap = CreateCap();
            var appearance = cap.AddComponent<ButtonAppearance>();
            var gaze = cap.AddComponent<ButtonGazeColour>();
            Set(gaze, "mode", (int)ButtonGazeColour.GazeMode.FollowsPlayerHeading);
            Set(gaze, "redHeading", Vector3.forward);
            Set(gaze, "viewer", cameraObject.transform);
            yield return null;
            Assert.That(appearance.Greenness, Is.LessThan(0.05f), "Facing north it is red.");

            cameraObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            yield return null;
            Assert.That(appearance.Greenness, Is.EqualTo(0.5f).Within(0.05f), "Halfway is a blend.");

            cameraObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            yield return null;
            Assert.That(appearance.Greenness, Is.GreaterThan(0.95f), "Facing south it is green.");
        }

        [UnityTest]
        public IEnumerator SleepingButtonIgnoresTheLevelUntilWoken()
        {
            GameObject cap = CreateCap();
            var button = cap.GetComponent<ButtonInteractable>();
            Set(button, "cooldown", 0f);
            Set(button, "prompt", "Press the green button");
            var appearance = cap.AddComponent<ButtonAppearance>();
            var wake = cap.AddComponent<WakeableButton>();

            int completions = 0;
            button.OnPressed.AddListener(() => completions++);
            yield return null;

            Assert.That(wake.IsAwake, Is.False);
            Assert.That(appearance.IsDisabledLook, Is.True, "It starts grey.");
            Assert.That(button.Prompt, Is.EqualTo("Press button (no response)"));

            button.Interact(null);
            Assert.That(wake.IsAwake, Is.True, "The first press wakes it.");
            Assert.That(completions, Is.Zero, "That press does not count for the level.");
            Assert.That(appearance.IsDisabledLook, Is.False);
            Assert.That(button.Prompt, Is.EqualTo("Press the green button"), "The real prompt returns.");

            button.Interact(null);
            Assert.That(completions, Is.EqualTo(1), "Now it works.");
        }

        [UnityTest]
        public IEnumerator ChasingButtonApproachesThePlayerAndStopsWhenPressed()
        {
            GameObject player = Track(new GameObject("Player"));
            player.AddComponent<CharacterController>();
            player.AddComponent<PlayerInteractor>();
            player.AddComponent<FirstPersonController>();
            player.transform.position = Vector3.zero;

            GameObject assembly = Track(new GameObject("Chaser"));
            assembly.transform.position = new Vector3(0f, 0f, 8f);
            GameObject cap = CreateCap();
            cap.transform.SetParent(assembly.transform, true);
            var mover = assembly.AddComponent<ButtonMover>();
            Set(mover, "mode", (int)ButtonMover.MoveMode.ChasePlayer);
            Set(mover, "speed", 6f);
            Set(mover, "player", player.transform);

            float start = assembly.transform.position.z;
            yield return new WaitForSeconds(0.2f);
            float closer = assembly.transform.position.z;
            Assert.That(closer, Is.LessThan(start), "It moves towards the player.");

            cap.GetComponent<ButtonInteractable>().Interact(null);
            yield return new WaitForSeconds(0.15f);
            Assert.That(assembly.transform.position.z, Is.EqualTo(closer).Within(0.2f),
                "Pressing it stops the chase.");
        }

        [UnityTest]
        public IEnumerator PatrollingButtonMovesAndTurnsAround()
        {
            GameObject assembly = Track(new GameObject("Patrol"));
            assembly.transform.position = Vector3.zero;
            var mover = assembly.AddComponent<ButtonMover>();
            Set(mover, "mode", (int)ButtonMover.MoveMode.Patrol);
            Set(mover, "speed", 8f);
            Set(mover, "patrolOffset", new Vector3(0f, 0f, 1f));
            Set(mover, "patrolPause", 0.1f);
            yield return new WaitForSeconds(0.1f);
            Assert.That(assembly.transform.position.z, Is.GreaterThan(0f));
            yield return new WaitForSeconds(0.5f);
            Assert.That(assembly.transform.position.z, Is.LessThan(1.01f), "It stays within its route.");
        }

        [UnityTest]
        public IEnumerator TalkingButtonSpeaksNearThePlayerThenFallsSilentOnPress()
        {
            GameObject player = Track(new GameObject("Player"));
            player.AddComponent<CharacterController>();
            player.AddComponent<PlayerInteractor>();
            player.AddComponent<FirstPersonController>();
            player.transform.position = Vector3.zero;

            GameObject cap = CreateCap();
            cap.transform.position = new Vector3(0f, 0f, 2f);
            var talking = cap.AddComponent<TalkingButton>();
            Set(talking, "speaksWhen", (int)TalkingButton.Trigger.PlayerIsNear);
            Set(talking, "secondsPerLine", 0.6f);
            Set(talking, "triggerDistance", 5f);
            typeof(TalkingButton).GetField("lines", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(talking, new List<string> { "Please do not press me.", "Press the red one." });

            yield return null;
            yield return null;
            Assert.That(talking.IsTalking, Is.True, "It starts talking when the player is near.");
            Assert.That(talking.CurrentLine, Is.EqualTo("Please do not press me."));

            yield return new WaitForSeconds(0.7f);
            Assert.That(talking.CurrentLine, Is.EqualTo("Press the red one."), "It moves to the next line.");

            cap.GetComponent<ButtonInteractable>().Interact(null);
            Assert.That(talking.IsTalking, Is.False, "Pressing it ends the argument.");
            Assert.That(talking.CurrentLine, Is.Empty);
        }

        [UnityTest]
        public IEnumerator MagneticButtonPushesTheAimAwayAndStopsWhenPressed()
        {
            GameObject player = Track(new GameObject("Player"));
            player.AddComponent<CharacterController>();
            player.AddComponent<PlayerInteractor>();
            player.AddComponent<FirstPersonController>();
            player.transform.position = Vector3.zero;

            GameObject cameraObject = Track(new GameObject("Camera", typeof(Camera)));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = Vector3.up * 1.6f;

            GameObject cap = CreateCap();
            cap.transform.position = new Vector3(0f, 1.6f, 5f); // Dead ahead.
            var repel = cap.AddComponent<CursorRepellingButton>();
            Set(repel, "pushStrength", 120f);

            float startYaw = player.transform.eulerAngles.y;
            yield return new WaitForSeconds(0.25f);
            Assert.That(repel.IsPushing, Is.True, "Aiming at it starts the push.");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(startYaw, player.transform.eulerAngles.y)),
                Is.GreaterThan(1f), "The view is pushed off target.");

            cap.GetComponent<ButtonInteractable>().Interact(null);
            yield return null;
            Assert.That(repel.IsPushing, Is.False, "Pressing it stops the push.");
        }
    }
}
