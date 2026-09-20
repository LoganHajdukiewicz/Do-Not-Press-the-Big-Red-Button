using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BigRedButton.Tests
{
    public sealed class ButtonClickSequenceTests
    {
        private readonly List<Object> objects = new List<Object>();
        private ButtonInteractable button;
        private ButtonAppearance look;
        private ButtonSound sound;
        private AudioClip ding;

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        private void Build(bool sleeping = false, bool soundFirst = false)
        {
            var host = new GameObject("Click sequence");
            objects.Add(host);
            button = host.AddComponent<ButtonInteractable>();
            Set(button, "cooldown", 0f);
            look = host.AddComponent<ButtonAppearance>();
            look.SetGreen();
            if (soundFirst)
                sound = host.AddComponent<ButtonSound>();
            if (sleeping)
                host.AddComponent<WakeableButton>();
            if (!soundFirst)
                sound = host.AddComponent<ButtonSound>();
            ding = AudioClip.Create("Test ding", 4410, 1, 44100, false);
            objects.Add(ding);
            Set(sound, "greenClip", ding);
            Set(sound, "greenDelay", 0f);
        }

        private TalkingButton AddDialogue(params string[] lines)
        {
            var talking = button.gameObject.AddComponent<TalkingButton>();
            Set(talking, "speaksWhen", TalkingButton.Trigger.ButtonPress);
            Set(talking, "lines", new List<string>(lines));
            return talking;
        }

        [TearDown]
        public void TearDown()
        {
            // Destroy components before their clips.
            foreach (Object item in objects)
                if (item != null)
                    Object.DestroyImmediate(item);
            objects.Clear();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void WakeClickNeverDingsInEitherComponentOrder(bool soundFirst)
        {
            Build(sleeping: true, soundFirst: soundFirst);
            Set(sound, "dingOnlyWhenGreen", false); // Even the override cannot ding while grey.
            int outcomes = 0;
            button.OnPressed.AddListener(() => outcomes++);
            button.Interact(null);
            Assert.That(look.IsDisabledLook, Is.False, "Wake callback has already run.");
            Assert.That(button.LastPress.WasDisabled, Is.True);
            Assert.That(sound.LastLayerScheduled, Is.Null);
            Assert.That(outcomes, Is.Zero);
            button.Interact(null);
            Assert.That(sound.LastLayerScheduled, Is.SameAs(ding));
            Assert.That(outcomes, Is.EqualTo(1));
        }

        [Test]
        public void GreyOrSuppressedPressNeverDingsEvenWithAlwaysDingEnabled()
        {
            Build();
            Set(sound, "dingOnlyWhenGreen", false);
            look.IsDisabledLook = true;
            button.Interact(null);
            Assert.That(sound.LastLayerScheduled, Is.Null);
            look.IsDisabledLook = false;
            button.SuppressEvents = true;
            button.Interact(null);
            Assert.That(sound.LastLayerScheduled, Is.Null);
        }

        [Test]
        public void OnlyRequiredActiveClickCompletesAndDings()
        {
            Build();
            Set(button, "requiredPresses", 3);
            int outcomes = 0;
            button.OnPressed.AddListener(() => outcomes++);
            for (int i = 1; i < 3; i++)
            {
                button.Interact(null);
                Assert.That(button.AcceptedPressCount, Is.EqualTo(i));
                Assert.That(outcomes, Is.Zero);
                Assert.That(sound.LastLayerScheduled, Is.Null);
                Assert.That(button.PressesRemaining, Is.EqualTo(3 - i));
            }
            button.Interact(null);
            Assert.That(outcomes, Is.EqualTo(1));
            Assert.That(sound.LastLayerScheduled, Is.SameAs(ding));
            // Repeatable buttons start another sequence of three.
            button.Interact(null);
            Assert.That(outcomes, Is.EqualTo(1));
            Assert.That(sound.LastLayerScheduled, Is.Null);
        }

        [Test]
        public void OneShotSurvivesWakeClicksAndPartialClicksUntilCompletion()
        {
            Build(sleeping: true);
            Set(button, "oneShot", true);
            Set(button, "requiredPresses", 2);
            Set(button.GetComponent<WakeableButton>(), "pressesToWake", 2);
            for (int i = 0; i < 3; i++)
            {
                button.Interact(null);
                Assert.That(button.CanInteract, Is.True);
                Assert.That(sound.LastLayerScheduled, Is.Null);
            }
            button.Interact(null);
            Assert.That(button.CanInteract, Is.False);
            Assert.That(button.LastPress.CompletesSequence, Is.True);
            Assert.That(sound.LastLayerScheduled, Is.SameAs(ding));
            button.ResetButton();
            Assert.That(button.CanInteract, Is.True);
            Assert.That(button.AcceptedPressCount, Is.Zero);
        }

        [Test]
        public void EachClickShowsItsOwnDialogueIncludingWakeAndFinalClicks()
        {
            Build(sleeping: true);
            Set(button.GetComponent<WakeableButton>(), "pressesToWake", 2);
            var talking = AddDialogue("Nothing happens.", "Fine. I am awake.", "Happy now?");
            button.Interact(null);
            Assert.That(talking.CurrentLine, Is.EqualTo("Nothing happens."));
            button.Interact(null);
            Assert.That(talking.CurrentLine, Is.EqualTo("Fine. I am awake."));
            button.Interact(null);
            Assert.That(talking.CurrentLine, Is.EqualTo("Happy now?"),
                "Default Silence On Press must not erase the final click's popup.");
            button.Interact(null);
            Assert.That(talking.CurrentLine, Is.Empty);
            button.ResetButton();
            button.Interact(null);
            Assert.That(talking.CurrentLine, Is.EqualTo("Nothing happens."));
        }

        [Test]
        public void CooldownAndLockedClicksDoNotCountOrAdvanceDialogue()
        {
            Build();
            Set(button, "requiredPresses", 3);
            Set(button, "cooldown", 100f);
            var talking = AddDialogue("First", "Second", "Third");
            button.Interact(null);
            button.Interact(null);
            Assert.That(button.AcceptedPressCount, Is.EqualTo(1));
            Assert.That(talking.CurrentLine, Is.EqualTo("First"));
            button.ResetButton();
            button.SetInteractable(false);
            button.Interact(null);
            Assert.That(button.AcceptedPressCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ClickPopupExpiresWithoutAutomaticallyShowingNextLine()
        {
            Build();
            var talking = AddDialogue("First", "Second");
            Set(talking, "secondsPerLine", 0.05f);
            button.Interact(null);
            yield return new WaitForSeconds(0.15f);
            Assert.That(talking.CurrentLine, Is.Empty);
            button.Interact(null);
            Assert.That(talking.CurrentLine, Is.EqualTo("Second"));
        }

        [Test]
        public void ContactUsesTheSameClickCounterAndDialogue()
        {
            Build();
            Set(button, "requiredPresses", 2);
            var talking = AddDialogue("One", "Two");
            var player = new GameObject("Interactor", typeof(PlayerInteractor));
            objects.Add(player);
            Assert.That(button.TryPressFromContact(player.GetComponent<PlayerInteractor>()), Is.True);
            Assert.That(talking.CurrentLine, Is.EqualTo("One"));
            Assert.That(button.LastPress.CompletesSequence, Is.False);
            Assert.That(button.TryPressFromContact(player.GetComponent<PlayerInteractor>()), Is.True);
            Assert.That(talking.CurrentLine, Is.EqualTo("Two"));
            Assert.That(button.LastPress.CompletesSequence, Is.True);
        }

        [Test]
        public void DialogueCanSkipBlankClicksAndLoopExplicitly()
        {
            Build();
            var talking = AddDialogue("First", "");
            Set(talking, "loop", true);
            button.Interact(null);
            Assert.That(talking.CurrentLine, Is.EqualTo("First"));
            button.Interact(null);
            Assert.That(talking.CurrentLine, Is.Empty);
            button.Interact(null);
            Assert.That(talking.CurrentLine, Is.EqualTo("First"));
        }

        [Test]
        public void DialogueVoiceDoesNotShareTheButtonSoundSource()
        {
            Build();
            var talking = AddDialogue("Spoken click");
            Set(talking, "lineClips", new List<AudioClip> { ding });
            button.Interact(null);
            Assert.That(button.GetComponents<AudioSource>().Length, Is.EqualTo(2));
            Assert.That(sound.LastLayerScheduled, Is.SameAs(ding));
        }

        [Test]
        public void OutcomeAndDingUseTheSameSnapshotAndConfiguredThreshold()
        {
            Build();
            var state = button.gameObject.AddComponent<StatefulDayButton>();
            Set(state, "day", null);
            Set(state, "greenThreshold", 0.8f);
            look.Greenness = 0.6f;
            int reds = 0, greens = 0;
            state.OnPressedWhileRed.AddListener(() => reds++);
            state.OnPressedWhileGreen.AddListener(() => greens++);
            // Mutate live colour before sound and outcome listeners run.
            button.Pressed += look.SetGreen;
            button.Interact(null);
            Assert.That(reds, Is.EqualTo(1));
            Assert.That(greens, Is.Zero);
            Assert.That(sound.LastLayerScheduled, Is.Null);
            button.Interact(null);
            Assert.That(greens, Is.EqualTo(1));
            Assert.That(sound.LastLayerScheduled, Is.SameAs(ding));
        }
    }
}
