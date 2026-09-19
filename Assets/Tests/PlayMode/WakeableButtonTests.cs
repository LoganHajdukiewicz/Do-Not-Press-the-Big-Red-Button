using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BigRedButton.Tests
{
    /// <summary>
    /// The sleeping button must be grey, wake to green on the first press, and only
    /// advance the day on a later press.
    /// </summary>
    public sealed class WakeableButtonTests
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

            GameObject host = Track(new GameObject("Day"));
            host.AddComponent<DayTitle>();
            level = host.AddComponent<DayLevel>();
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

        private (ButtonInteractable button, ButtonAppearance look, ButtonSound sound,
            WakeableButton wake) BuildSleepingButton(bool startDisabledInScene)
        {
            GameObject cap = Track(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
            cap.name = "Sleeping Button";
            var button = cap.AddComponent<ButtonInteractable>();
            Set(button, "cooldown", 0f);
            Set(button, "prompt", "Press the green button");

            var look = cap.AddComponent<ButtonAppearance>();
            // Mirrors how the scene is saved on disk.
            Set(look, "startDisabled", startDisabledInScene);
            Set(look, "greenness", 0f);

            var sound = cap.AddComponent<ButtonSound>();
            Set(sound, "pressClip", AudioClip.Create("press", 4410, 1, 44100, false));
            Set(sound, "greenClip", AudioClip.Create("ding", 4410, 1, 44100, false));
            Set(sound, "greenDelay", 0f);

            var wake = cap.AddComponent<WakeableButton>();
            var state = cap.AddComponent<StatefulDayButton>();
            Set(state, "day", level);
            return (button, look, sound, wake);
        }

        [UnityTest]
        public IEnumerator SleepingButtonLooksGreyNotRed()
        {
            var (_, look, sound, wake) = BuildSleepingButton(startDisabledInScene: false);
            yield return null;

            Assert.That(wake.IsAwake, Is.False);
            Assert.That(look.IsDisabledLook, Is.True,
                "It must be grey, even though the saved scene says start disabled is off.");

            Color grey = look.CurrentColour;
            Assert.That(Mathf.Abs(grey.r - grey.g), Is.LessThan(0.08f),
                "Grey means red and green are close, not a red tint.");
            Assert.That(Mathf.Abs(grey.g - grey.b), Is.LessThan(0.08f));
            Assert.That(sound.CountsAsGreen, Is.False, "A grey button does not ding.");
        }

        [UnityTest]
        public IEnumerator FirstPressWakesItToGreenAndDoesNotAdvanceTheDay()
        {
            var (button, look, sound, wake) = BuildSleepingButton(startDisabledInScene: true);
            yield return null;

            button.Interact(null);
            yield return null;

            Assert.That(wake.IsAwake, Is.True, "The first press wakes it.");
            Assert.That(loaded, Is.Empty, "The waking press must not advance the day.");
            Assert.That(look.IsDisabledLook, Is.False, "It is no longer grey.");
            Assert.That(look.Greenness, Is.EqualTo(1f), "It wakes up green.");
            Assert.That(sound.CountsAsGreen, Is.True, "Now it will ding.");
            Assert.That(button.Prompt, Is.EqualTo("Press the green button"));
        }

        [UnityTest]
        public IEnumerator SecondPressAdvancesTheDay()
        {
            var (button, _, _, _) = BuildSleepingButton(startDisabledInScene: true);
            yield return null;

            button.Interact(null);
            yield return null;
            Assert.That(loaded, Is.Empty);

            button.Interact(null);
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 3" }),
                "Only the press after waking ends the day.");
        }

        [UnityTest]
        public IEnumerator ExtraWakePressesAreRespected()
        {
            var (button, look, _, wake) = BuildSleepingButton(startDisabledInScene: true);
            Set(wake, "pressesToWake", 3);
            yield return null;

            for (int i = 0; i < 3; i++)
            {
                Assert.That(wake.IsAwake, Is.False, $"Still asleep before press {i + 1}.");
                button.Interact(null);
                yield return null;
                Assert.That(loaded, Is.Empty, "No waking press advances the day.");
            }

            Assert.That(wake.IsAwake, Is.True);
            Assert.That(look.Greenness, Is.EqualTo(1f));

            button.Interact(null);
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 3" }));
        }

        [UnityTest]
        public IEnumerator ItCanBeConfiguredToWakeUpRed()
        {
            var (button, look, _, wake) = BuildSleepingButton(startDisabledInScene: true);
            Set(wake, "wakesUpAs", 0f);
            yield return null;

            button.Interact(null);
            yield return null;
            Assert.That(look.Greenness, Is.Zero, "It woke up red.");
            Assert.That(loaded, Is.Empty);

            // Now it is a red button, so pressing it restarts the day.
            button.Interact(null);
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 2" }));
        }

        [UnityTest]
        public IEnumerator SuppressedPressStaysSuppressedEvenWhenAListenerWakesIt()
        {
            // Guards the ordering bug: the inspector event must be decided before the
            // plain Pressed listeners run, or the waking press leaks through.
            GameObject cap = Track(GameObject.CreatePrimitive(PrimitiveType.Cylinder));
            var button = cap.AddComponent<ButtonInteractable>();
            Set(button, "cooldown", 0f);
            button.SuppressEvents = true;

            int inspectorCalls = 0;
            button.OnPressed.AddListener(() => inspectorCalls++);
            button.Pressed += () => button.SuppressEvents = false;

            yield return null;
            button.Interact(null);
            Assert.That(inspectorCalls, Is.Zero,
                "Turning suppression off during the press must not run that same press.");

            button.Interact(null);
            Assert.That(inspectorCalls, Is.EqualTo(1), "The next press runs normally.");
        }

        [UnityTest]
        public IEnumerator WakingReportsItsEvents()
        {
            var (button, _, _, wake) = BuildSleepingButton(startDisabledInScene: true);
            int woken = 0;
            int asleepPresses = 0;
            wake.OnWoken.AddListener(() => woken++);
            wake.OnPressedWhileAsleep.AddListener(() => asleepPresses++);
            yield return null;

            button.Interact(null);
            Assert.That(woken, Is.EqualTo(1));
            Assert.That(asleepPresses, Is.EqualTo(1));

            button.Interact(null);
            Assert.That(woken, Is.EqualTo(1), "It only wakes once.");
            Assert.That(asleepPresses, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
