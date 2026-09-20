using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BigRedButton.Tests
{
    public sealed class DialogueProgressionTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private ButtonInteractable button;
        private TalkingButton dialogue;
        private ButtonSound sound;
        private AudioClip ding;
        private int outcomes;

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private void Build(bool onParent = false)
        {
            var host = new GameObject("Talking assembly");
            objects.Add(host);
            var cap = new GameObject("Button");
            cap.transform.SetParent(host.transform);
            button = cap.AddComponent<ButtonInteractable>();
            Set(button, "cooldown", 0f);
            cap.AddComponent<ButtonAppearance>().SetGreen();
            sound = cap.AddComponent<ButtonSound>();
            ding = AudioClip.Create("Ding", 4410, 1, 44100, false);
            Set(sound, "greenClip", ding);
            Set(sound, "greenDelay", 0f);
            dialogue = (onParent ? host : cap).AddComponent<TalkingButton>();
            Set(dialogue, "speaksWhen", TalkingButton.Trigger.ButtonPress);
            Set(dialogue, "lines", new List<string> { "First", "Second", "Last" });
            outcomes = 0;
            button.OnPressed.AddListener(() => outcomes++);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var host in objects)
                if (host != null)
                    Object.DestroyImmediate(host);
            objects.Clear();
            if (ding != null)
                Object.DestroyImmediate(ding);
            DayFlow.ResetForTests();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClicksPresentEveryLineThenAcknowledgeBeforeOutcome(bool onParent)
        {
            Build(onParent);
            Set(button, "oneShot", true);
            foreach (string line in new[] { "First", "Second", "Last" })
            {
                button.Interact(null);
                Assert.That(dialogue.CurrentLine, Is.EqualTo(line));
                Assert.That(outcomes, Is.Zero);
                Assert.That(sound.LastLayerScheduled, Is.Null);
                Assert.That(button.CanInteract, Is.True, "Dialogue must not consume One Shot.");
            }
            button.Interact(null);
            Assert.That(outcomes, Is.EqualTo(1));
            Assert.That(dialogue.DialogueExhausted, Is.True);
            Assert.That(dialogue.CurrentLine, Is.Empty);
            Assert.That(sound.LastLayerScheduled, Is.SameAs(ding));
            Assert.That(button.CanInteract, Is.False);
            button.ResetButton();
            button.Interact(null);
            Assert.That(dialogue.CurrentLine, Is.EqualTo("First"));
            Assert.That(outcomes, Is.EqualTo(1));
        }

        [TestCase(TalkingButton.Trigger.PlayerIsNear)]
        [TestCase(TalkingButton.Trigger.PlayerLooksAtIt)]
        [TestCase(TalkingButton.Trigger.DayStart)]
        public void FirstLineAlreadyPresentedByTriggerIsNotRepeatedOnClick(TalkingButton.Trigger trigger)
        {
            Build();
            Set(dialogue, "speaksWhen", trigger);
            Set(dialogue, "loop", true); // Old saved talking days have Loop enabled.
            dialogue.StartTalking();
            Assert.That(dialogue.CurrentLine, Is.EqualTo("First"));
            button.Interact(null);
            Assert.That(dialogue.CurrentLine, Is.EqualTo("Second"));
            button.Interact(null);
            Assert.That(dialogue.CurrentLine, Is.EqualTo("Last"));
            Assert.That(outcomes, Is.Zero);
            button.Interact(null);
            Assert.That(outcomes, Is.EqualTo(1), "Legacy Loop must not trap the player forever.");
        }

        [UnityTest]
        public IEnumerator DialogueCannotExpireOrAdvanceByWaiting()
        {
            Build();
            Set(dialogue, "secondsPerLine", 0.01f);
            button.Interact(null);
            yield return new WaitForSeconds(0.1f);
            Assert.That(dialogue.CurrentLine, Is.EqualTo("First"));
            Assert.That(outcomes, Is.Zero);
            button.Interact(null);
            Assert.That(dialogue.CurrentLine, Is.EqualTo("Second"));
        }

        [Test]
        public void CooldownDoesNotSkipDialogueAndRequiredCountStillApplies()
        {
            Build();
            Set(button, "requiredPresses", 6);
            Set(button, "cooldown", 100f);
            button.Interact(null);
            button.Interact(null);
            Assert.That(dialogue.CurrentLine, Is.EqualTo("First"));
            button.ResetButton();
            Set(button, "cooldown", 0f);
            for (int i = 0; i < 5; i++)
                button.Interact(null);
            Assert.That(dialogue.DialogueExhausted, Is.True);
            Assert.That(outcomes, Is.Zero);
            button.Interact(null);
            Assert.That(outcomes, Is.EqualTo(1));
        }

        [Test]
        public void WakeClicksAdvanceDialogueButDoNotResolveOrDing()
        {
            Build();
            var wake = button.gameObject.AddComponent<WakeableButton>();
            Set(wake, "pressesToWake", 2);
            for (int i = 0; i < 3; i++)
            {
                button.Interact(null);
                Assert.That(outcomes, Is.Zero);
                Assert.That(sound.LastLayerScheduled, Is.Null);
            }
            Assert.That(wake.IsAwake, Is.True);
            Assert.That(dialogue.CurrentLine, Is.EqualTo("Last"));
            button.Interact(null);
            Assert.That(outcomes, Is.EqualTo(1));
        }

        [Test]
        public void EmptyOrDisabledDialogueDoesNotBlockTheButton()
        {
            Build();
            Set(dialogue, "lines", new List<string>());
            button.Interact(null);
            Assert.That(outcomes, Is.EqualTo(1));
            Set(dialogue, "lines", new List<string> { "Do not block while disabled" });
            dialogue.enabled = false;
            button.Interact(null);
            Assert.That(outcomes, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator DayLoadsOnlyAfterLastLineIsAcknowledged()
        {
            Build();
            DayFlow.ResetForTests();
            var loaded = new List<string>();
            DayFlow.SceneExists = _ => true;
            DayFlow.SceneLoader = name => loaded.Add(name);
            var host = new GameObject("Day 8");
            objects.Add(host);
            var day = host.AddComponent<DayLevel>();
            Set(day, "dayNumber", 8);
            Set(day, "delayBeforeNextDay", 0f);
            button.OnPressed.AddListener(day.CompleteDay);
            yield return null;
            for (int i = 0; i < 3; i++)
            {
                button.Interact(null);
                yield return null;
                Assert.That(loaded, Is.Empty);
            }
            button.Interact(null);
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 9" }));
        }
    }
}
