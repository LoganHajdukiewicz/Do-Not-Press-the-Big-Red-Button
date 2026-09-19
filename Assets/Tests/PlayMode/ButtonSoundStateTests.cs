using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BigRedButton.Tests
{
    public sealed class ButtonSoundStateTests
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

        private (ButtonInteractable button, ButtonSound sound, ButtonAppearance look) Build(
            bool withAppearance)
        {
            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            objects.Add(cap);
            cap.name = "Sound button";
            var button = cap.AddComponent<ButtonInteractable>();
            Set(button, "cooldown", 0f);
            ButtonAppearance look = withAppearance ? cap.AddComponent<ButtonAppearance>() : null;
            var sound = cap.AddComponent<ButtonSound>();
            Set(sound, "pressClip", AudioClip.Create("press", 4410, 1, 44100, false));
            Set(sound, "greenClip", AudioClip.Create("ding", 4410, 1, 44100, false));
            Set(sound, "greenDelay", 0f);
            return (button, sound, look);
        }

        [UnityTest]
        public IEnumerator PlainGreenButtonWithNoColourControlStillDings()
        {
            var (_, sound, _) = Build(withAppearance: false);
            yield return null;
            Assert.That(sound.CountsAsGreen, Is.True,
                "A button with no colour control is treated as green, so it dings.");
        }

        [UnityTest]
        public IEnumerator ButtonThatTurnsGreenDingsOnceItIsGreen()
        {
            var (button, sound, look) = Build(withAppearance: true);
            yield return null;

            look.SetRed();
            Assert.That(sound.CountsAsGreen, Is.False, "While red it must not ding.");

            look.SetGreen();
            Assert.That(sound.CountsAsGreen, Is.True, "Once it turns green the ding applies.");

            button.Interact(null);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator GreyButtonDoesNotDing()
        {
            var (_, sound, look) = Build(withAppearance: true);
            yield return null;
            look.SetGreen();
            look.IsDisabledLook = true;
            Assert.That(sound.CountsAsGreen, Is.False, "A grey button has no green state.");
        }

        [UnityTest]
        public IEnumerator TimedButtonDingsOnlyDuringItsGreenWindow()
        {
            var (button, sound, look) = Build(withAppearance: true);
            var schedule = button.gameObject.AddComponent<ButtonColourSchedule>();
            Set(schedule, "redDuration", 0.15f);
            Set(schedule, "greenDuration", 5f);
            Set(schedule, "blendDuration", 0f);
            Set(schedule, "stopOnPress", false);
            yield return null;

            Assert.That(sound.CountsAsGreen, Is.False, "It starts red, so no ding.");
            yield return new WaitForSeconds(0.3f);
            Assert.That(look.Greenness, Is.EqualTo(1f));
            Assert.That(sound.CountsAsGreen, Is.True, "The timer turned it green, so it dings.");
        }

        [UnityTest]
        public IEnumerator DingCanBeSetToAlwaysPlay()
        {
            var (button, sound, look) = Build(withAppearance: true);
            Set(sound, "dingOnlyWhenGreen", false);
            yield return null;
            look.SetRed();
            button.Interact(null);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator HalfwayBlendCountsAsGreen()
        {
            var (_, sound, look) = Build(withAppearance: true);
            yield return null;
            look.Greenness = 0.49f;
            Assert.That(sound.CountsAsGreen, Is.False);
            look.Greenness = 0.51f;
            Assert.That(sound.CountsAsGreen, Is.True);
        }

        [UnityTest]
        public IEnumerator SoundSurvivesAPressOnADeactivatedButton()
        {
            var (button, sound, look) = Build(withAppearance: true);
            yield return null;
            look.SetGreen();
            button.gameObject.SetActive(false);
            sound.Play(); // Must not throw when the coroutine cannot start.
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
    }
}
