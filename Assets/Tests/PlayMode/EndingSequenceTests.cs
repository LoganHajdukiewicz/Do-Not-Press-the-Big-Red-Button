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
        private AudioClip shot;
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
            if (shot != null)
                Object.DestroyImmediate(shot);
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
            shot = AudioClip.Create("Ending test shot", 4410, 1, 44100, false);
            Set(ending, "gunshot", shot);
            return ending;
        }

        [Test]
        public void FullFiveSecondsOfExplorationBeforeGunshotAndFlash()
        {
            var ending = BuildEnding();
            int shots = 0;
            ending.OnGunshot.AddListener(() => shots++);
            ending.Begin();
            Assert.That(controls.enabled, Is.True);
            Assert.That(ending.IsExploring, Is.True);
            Advance(ending, 4.5f);
            Assert.That(ending.OverlayAlpha, Is.Zero);
            Assert.That(ending.IsExploring, Is.True);
            Assert.That(shots, Is.Zero);
            Assert.That(controls.enabled, Is.True);
            Advance(ending, 0.5f);
            Assert.That(ending.IsFlashing, Is.True);
            Assert.That(shots, Is.EqualTo(1));
            Assert.That(ending.OverlayAlpha, Is.EqualTo(1f));
            Assert.That(ending.OverlayColour, Is.EqualTo(Color.white));
            Assert.That(controls.enabled, Is.False);
        }

        [Test]
        public void OneGunshotWhiteFlashBlackThenMonthCard()
        {
            var ending = BuildEnding();
            int cards = 0, shots = 0;
            ending.OnCardShown.AddListener(() => cards++);
            ending.OnGunshot.AddListener(() => shots++);
            ending.Begin();
            Advance(ending, 5f);
            Assert.That(ending.HasFiredGunshot, Is.True);
            Assert.That(shots, Is.EqualTo(1));
            Assert.That(ending.OverlayColour, Is.EqualTo(Color.white));
            Assert.That(controls.enabled, Is.False);
            Advance(ending, 0.12f);
            Assert.That(ending.IsFading, Is.True);
            Advance(ending, 0.03f);
            Assert.That(ending.OverlayColour.r, Is.EqualTo(0.5f).Within(0.001f));
            Advance(ending, 0.03f);
            Assert.That(ending.OverlayAlpha, Is.EqualTo(1f));
            Assert.That(ending.OverlayColour, Is.EqualTo(Color.black));
            Assert.That(cards, Is.Zero);
            Advance(ending, 0.5f);
            Assert.That(cards, Is.EqualTo(1));
            Advance(ending, 2.5f);
            Assert.That(ending.CardAlpha, Is.EqualTo(1f));
            Assert.That(ending.CardText, Does.StartWith("Employee of the Month"));
            Assert.That(ending.GetComponents<AudioSource>().Length, Is.EqualTo(1));
            Advance(ending, 20f);
            Assert.That(cards, Is.EqualTo(1));
            Assert.That(shots, Is.EqualTo(1));
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
            Assert.That(ending.IsFlashing, Is.True);
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
            Assert.That(ending.IsFlashing, Is.True);
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
            Assert.That(ending.IsFlashing, Is.True);
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

        [UnityTest]
        public IEnumerator GunshotCutsTheMusicDead()
        {
            // Day 31 wires On Gunshot to BackgroundMusic.StopImmediately, so the
            // silence lands with the shot rather than fading afterwards.
            BackgroundMusic.ClearPersistent();
            var host = new GameObject("Day");
            objects.Add(host);
            var track = host.AddComponent<BackgroundMusic>();
            AudioClip happy = AudioClip.Create("Happy", 44100, 1, 44100, false);
            Set(track, "music", happy);
            Set(track, "fadeInDuration", 0f);
            // Keep the pass continuous, so the stop is what silences it, not a fade.
            Set(track, "fadeOutDuration", 0f);
            Set(track, "silenceBetweenLoops", 0f);
            yield return null;

            Assert.That(track.IsPlaying, Is.True, "The ending's music is playing.");

            EndingSequence ending = BuildEnding();
            ending.OnGunshot.AddListener(track.StopImmediately);
            ending.Begin();
            Advance(ending, 5f); // Reaches the gunshot.

            Assert.That(ending.HasFiredGunshot, Is.True);
            Assert.That(track.IsPlaying, Is.False,
                "The music must stop on the shot, not keep playing under the card.");
            BackgroundMusic.ClearPersistent();
            Object.DestroyImmediate(happy);
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
            Set(ending, "flashDuration", 0f);
            Set(ending, "fadeToBlackDuration", 0f);
            Set(ending, "darkHold", 0f);
            Set(ending, "cardFadeIn", 0f);
            ending.Begin();
            for (int i = 0; i < 5; i++)
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
