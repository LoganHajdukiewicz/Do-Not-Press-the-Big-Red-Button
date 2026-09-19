using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BigRedButton.Tests
{
    public sealed class StatefulDayButtonTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<string> loaded = new List<string>();
        private DayLevel level;

        [SetUp]
        public void SetUp()
        {
            DayFlow.ResetForTests();
            loaded.Clear();
            DayFlow.SceneLoader = name => loaded.Add(name);
            DayFlow.SceneExists = _ => true;

            GameObject dayHost = Track(new GameObject("Day"));
            dayHost.AddComponent<DayTitle>();
            level = dayHost.AddComponent<DayLevel>();
            Set(level, "dayNumber", 2);
            Set(level, "delayBeforeNextDay", 0f);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null)
                    Object.DestroyImmediate(objects[i]);
            objects.Clear();
            DayFlow.ResetForTests();
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        private GameObject Track(GameObject item)
        {
            objects.Add(item);
            return item;
        }

        private (ButtonInteractable button, ButtonAppearance look, StatefulDayButton state) Build()
        {
            GameObject cap = Track(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
            cap.name = "Stateful button";
            cap.transform.position = new Vector3(0f, 1f, 3f);
            var button = cap.AddComponent<ButtonInteractable>();
            Set(button, "cooldown", 0f);
            var look = cap.AddComponent<ButtonAppearance>();
            var state = cap.AddComponent<StatefulDayButton>();
            Set(state, "day", level);
            return (button, look, state);
        }

        [UnityTest]
        public IEnumerator PressingItWhileGreenCompletesTheDay()
        {
            var (button, look, state) = Build();
            yield return null;
            look.SetGreen();
            Assert.That(state.LooksGreen, Is.True);

            button.Interact(null);
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 3" }), "Green advances to the next day.");
        }

        [UnityTest]
        public IEnumerator PressingItWhileRedRestartsTheDay()
        {
            var (button, look, state) = Build();
            yield return null;
            look.SetRed();
            Assert.That(state.LooksGreen, Is.False);

            button.Interact(null);
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 2" }), "Red repeats the same day.");
        }

        [UnityTest]
        public IEnumerator TheSameButtonGivesOppositeResultsAsItsColourChanges()
        {
            var (button, look, _) = Build();
            var schedule = button.gameObject.AddComponent<ButtonColourSchedule>();
            Set(schedule, "redDuration", 0.15f);
            Set(schedule, "greenDuration", 5f);
            Set(schedule, "blendDuration", 0f);
            Set(schedule, "stopOnPress", false);
            yield return null;

            // While it is still red, it fails.
            button.Interact(null);
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 2" }));

            // Once the timer turns it green, the same button advances instead.
            yield return new WaitForSeconds(0.3f);
            Assert.That(look.Greenness, Is.EqualTo(1f), "The timer switched it to green.");
            button.ResetButton();
            button.Interact(null);
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 2", "Day 3" }));
        }

        [UnityTest]
        public IEnumerator BlendedColourUsesTheHalfwayThreshold()
        {
            var (button, look, state) = Build();
            yield return null;

            look.Greenness = 0.49f;
            Assert.That(state.LooksGreen, Is.False, "Just under halfway still counts as red.");
            look.Greenness = 0.51f;
            Assert.That(state.LooksGreen, Is.True, "Just over halfway counts as green.");

            button.Interact(null);
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 3" }));
        }

        [UnityTest]
        public IEnumerator GreyButtonDoesNotResolveTheDayAtAll()
        {
            var (button, look, _) = Build();
            yield return null;
            look.IsDisabledLook = true;

            button.Interact(null);
            yield return null;
            Assert.That(loaded, Is.Empty, "A grey button is neither answer.");
        }

        [UnityTest]
        public IEnumerator WakingASleepingButtonDoesNotResolveTheDay()
        {
            var (button, _, _) = Build();
            Set(button, "prompt", "Press the green button");
            button.gameObject.AddComponent<WakeableButton>();
            yield return null;

            // First press only wakes it up.
            button.Interact(null);
            yield return null;
            Assert.That(loaded, Is.Empty, "The wake-up press must not end the day.");

            // It wakes up green, so the next press completes the day.
            button.Interact(null);
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 3" }));
        }

        [UnityTest]
        public IEnumerator WatchedButtonFailsTheDayOnceItHasTurnedRed()
        {
            GameObject cameraObject = Track(new GameObject("Camera", typeof(Camera)));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = Vector3.zero;
            cameraObject.transform.rotation = Quaternion.identity;

            var (button, look, _) = Build();
            button.transform.position = new Vector3(0f, 0f, 4f);
            var gaze = button.gameObject.AddComponent<ButtonGazeColour>();
            Set(gaze, "mode", (int)ButtonGazeColour.GazeMode.TurnsRedWhenWatched);
            Set(gaze, "hoverDelay", 0.1f);
            Set(gaze, "blendDuration", 0f);
            Set(gaze, "viewer", cameraObject.transform);

            yield return new WaitForSeconds(0.3f);
            Assert.That(look.Greenness, Is.Zero, "Staring at it turned it red.");

            button.Interact(null);
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 2" }),
                "Pressing the shy button while red restarts the day.");
        }

        [UnityTest]
        public IEnumerator StateEventsFireForInspectorWiring()
        {
            var (button, look, state) = Build();
            Set(state, "day", null); // Events only, no day attached.
            int greenPresses = 0;
            int redPresses = 0;
            state.OnPressedWhileGreen.AddListener(() => greenPresses++);
            state.OnPressedWhileRed.AddListener(() => redPresses++);
            yield return null;

            look.SetRed();
            button.Interact(null);
            button.ResetButton();
            look.SetGreen();
            button.Interact(null);

            Assert.That(redPresses, Is.EqualTo(1));
            Assert.That(greenPresses, Is.EqualTo(1));
            Assert.That(loaded, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
