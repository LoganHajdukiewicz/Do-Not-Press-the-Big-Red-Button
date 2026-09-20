using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BigRedButton.Tests
{
    public sealed class EndingSequenceTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private PlayerInteractor controls;
        private float previousTimeScale;
        private string firstName, lastName;

        [SetUp]
        public void SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            firstName = WorkerIdentity.FirstName;
            lastName = WorkerIdentity.LastName;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null)
                    Object.DestroyImmediate(objects[i]);
            objects.Clear();
            Time.timeScale = previousTimeScale;
            WorkerIdentity.FirstName = firstName;
            WorkerIdentity.LastName = lastName;
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        // Deterministic timing checks, without sleeps/frame-rate dependent assertions.
        private static void Advance(EndingSequence ending, float seconds) =>
            typeof(EndingSequence).GetMethod("AdvanceSequence", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ending, new object[] { seconds });

        private EndingSequence BuildEnding()
        {
            var player = new GameObject("Test player");
            objects.Add(player);
            player.transform.position = new Vector3(0f, 0f, -4.5f);
            // A harmless stand-in for control enable/disable assertions. No invalid
            // FirstPersonController with missing camera/input asset in the test fixture.
            controls = player.AddComponent<PlayerInteractor>();
            var host = new GameObject("Day 31");
            objects.Add(host);
            var ending = host.AddComponent<EndingSequence>();
            Set(ending, "player", player.transform);
            Set(ending, "frozenDuringEnding", controls);
            return ending;
        }

        [Test]
        public void FullFiveSecondsOfExplorationBeforeAnyFade()
        {
            var ending = BuildEnding();
            int fades = 0;
            ending.OnFadeStarted.AddListener(() => fades++);
            ending.Begin();
            Assert.That(controls.enabled, Is.True);
            Assert.That(ending.IsExploring, Is.True);
            Advance(ending, 4.5f);
            Assert.That(ending.OverlayAlpha, Is.Zero);
            Assert.That(ending.IsExploring, Is.True);
            Assert.That(fades, Is.Zero);
            Assert.That(controls.enabled, Is.True);
            Advance(ending, 0.5f);
            Assert.That(ending.IsFading, Is.True);
            Assert.That(fades, Is.EqualTo(1));
            Assert.That(ending.OverlayAlpha, Is.Zero, "Fade starts after five seconds, not at the door.");
            Assert.That(controls.enabled, Is.True);
        }

        [Test]
        public void SmoothBlackFadeThenMonthCardWithoutAudioOrWhiteFlash()
        {
            var ending = BuildEnding();
            int cards = 0;
            ending.OnCardShown.AddListener(() => cards++);
            ending.Begin();
            Advance(ending, 5f);
            Advance(ending, 1f);
            Assert.That(ending.OverlayAlpha, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(ending.OverlayColour, Is.EqualTo(Color.black));
            Assert.That(controls.enabled, Is.True, "Movement remains available during the fade.");
            Assert.That(ending.CardAlpha, Is.Zero);
            Advance(ending, 1f);
            Assert.That(ending.OverlayAlpha, Is.EqualTo(1f));
            Assert.That(controls.enabled, Is.False, "Only freeze once the world is completely hidden.");
            Assert.That(cards, Is.Zero);
            Advance(ending, 0.5f);
            Assert.That(cards, Is.EqualTo(1));
            Advance(ending, 2.5f);
            Assert.That(ending.CardAlpha, Is.EqualTo(1f));
            Assert.That(ending.CardText, Does.StartWith("Employee of the Month"));
            Assert.That(ending.GetComponents<AudioSource>(), Is.Empty);
            Advance(ending, 20f);
            Assert.That(cards, Is.EqualTo(1));
            Assert.That(ending.OverlayAlpha, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator DoorwayStartsTimerOnlyOnceAndOnlyOutside()
        {
            var ending = BuildEnding();
            int started = 0;
            ending.OnEndingStarted.AddListener(() => started++);
            yield return null;
            Assert.That(ending.HasStarted, Is.False);
            controls.transform.position = new Vector3(0f, 0f, 6.8f);
            yield return null;
            Assert.That(ending.HasStarted, Is.False, "Still inside the room.");
            controls.transform.position = new Vector3(0f, 0f, 7.6f);
            yield return null;
            Assert.That(ending.IsExploring, Is.True);
            Assert.That(controls.enabled, Is.True);
            controls.transform.position = new Vector3(30f, 0f, 30f);
            ending.Begin();
            yield return null;
            Assert.That(started, Is.EqualTo(1), "Walking away cannot restart the timer.");
        }

        [Test]
        public void RepeatedBeginDoesNotResetExplorationTimer()
        {
            var ending = BuildEnding();
            ending.Begin();
            Advance(ending, 4f);
            ending.Begin();
            Advance(ending, 1f);
            Assert.That(ending.IsFading, Is.True);
        }

        [Test]
        public void PauseDoesNotConsumeExplorationTime()
        {
            var ending = BuildEnding();
            ending.Begin();
            Time.timeScale = 0f;
            Advance(ending, 100f);
            Assert.That(ending.IsExploring, Is.True);
            Assert.That(ending.OverlayAlpha, Is.Zero);
            Time.timeScale = 1f;
            Advance(ending, 4f);
            Assert.That(ending.IsExploring, Is.True);
            Advance(ending, 1f);
            Assert.That(ending.IsFading, Is.True);
        }

        [Test]
        public void UnavailableControlsDoNotConsumeExplorationTime()
        {
            var ending = BuildEnding();
            ending.Begin();
            controls.enabled = false;
            Advance(ending, 100f);
            Assert.That(ending.IsExploring, Is.True);
            controls.enabled = true;
            Advance(ending, 5f);
            Assert.That(ending.IsFading, Is.True);
        }

        [Test]
        public void DisablingEndingRestoresControlsItFroze()
        {
            var ending = BuildEnding();
            ending.Begin();
            Advance(ending, 5f);
            Advance(ending, 2f);
            Assert.That(controls.enabled, Is.False);
            ending.enabled = false;
            Assert.That(controls.enabled, Is.True);
        }

        [Test]
        public void CardNamesTheWorker()
        {
            var ending = BuildEnding();
            WorkerIdentity.FirstName = "ANNE";
            WorkerIdentity.LastName = "KOWALSKI";
            Assert.That(ending.CardText, Is.EqualTo("Employee of the Month\nANNE KOWALSKI"));
            Assert.That(ending.CardText, Does.Not.Contain("{WORKER-"));
        }

        [Test]
        public void ZeroDurationSettingsStillReachTheCard()
        {
            var ending = BuildEnding();
            Set(ending, "explorationDuration", 0f);
            Set(ending, "fadeToBlackDuration", 0f);
            Set(ending, "darkHold", 0f);
            Set(ending, "cardFadeIn", 0f);
            ending.Begin();
            for (int i = 0; i < 4; i++)
                Advance(ending, 0.01f);
            Assert.That(ending.CardAlpha, Is.EqualTo(1f));
            Assert.That(ending.OverlayAlpha, Is.EqualTo(1f));
        }

        [Test]
        public void WorkerNameFormattingHandlesBothTokenStyles()
        {
            WorkerIdentity.FirstName = "ANNE";
            WorkerIdentity.LastName = "KOWALSKI";
            Assert.That(WorkerIdentity.Format("{WORKER-FIRSTNAME} {WORKER-LASTNAME}"),
                Is.EqualTo("ANNE KOWALSKI"));
            Assert.That(WorkerIdentity.Format("HELLO WORKER-FIRSTNAME WORKER-LASTNAME."),
                Is.EqualTo("HELLO ANNE KOWALSKI."));
            WorkerIdentity.FirstName = "   ";
            Assert.That(WorkerIdentity.FirstName, Is.EqualTo("ANNE"));
        }
    }
}
