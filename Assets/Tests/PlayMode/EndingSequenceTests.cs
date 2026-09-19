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

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null)
                    Object.DestroyImmediate(objects[i]);
            objects.Clear();
            WorkerIdentity.ResetForTests();
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        private EndingSequence BuildEnding()
        {
            GameObject host = new GameObject("Day 31");
            objects.Add(host);
            var ending = host.AddComponent<EndingSequence>();
            Set(ending, "sunshineFadeIn", 0.1f);
            Set(ending, "sunshineHold", 0.1f);
            Set(ending, "cutToBlack", 0.05f);
            Set(ending, "darkHold", 0.1f);
            Set(ending, "cardFadeIn", 0.1f);
            return ending;
        }

        [UnityTest]
        public IEnumerator EndingRunsFromSunshineThroughDarknessToTheCard()
        {
            EndingSequence ending = BuildEnding();
            int started = 0, shots = 0, cards = 0;
            ending.OnEndingStarted.AddListener(() => started++);
            ending.OnGunshot.AddListener(() => shots++);
            ending.OnCardShown.AddListener(() => cards++);
            yield return null;

            Assert.That(ending.HasStarted, Is.False, "It waits until the player reaches the door.");
            Assert.That(ending.OverlayAlpha, Is.Zero, "The screen starts clear.");

            ending.Begin();
            Assert.That(started, Is.EqualTo(1));

            // Sunshine floods in first.
            yield return new WaitForSecondsRealtime(0.08f);
            Assert.That(ending.OverlayAlpha, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            Assert.That(ending.OverlayColour.r, Is.GreaterThan(0.8f), "The bleach is warm, not black.");
            Assert.That(ending.HasFiredGunshot, Is.False);

            // Then the gunshot and the cut to black.
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(shots, Is.EqualTo(1), "The gunshot fires once.");
            Assert.That(ending.OverlayColour.r, Is.LessThan(0.2f), "The screen goes dark.");
            Assert.That(ending.OverlayAlpha, Is.EqualTo(1f));

            // Then the card.
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(cards, Is.EqualTo(1));
            Assert.That(ending.CardAlpha, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator CardNamesTheWorker()
        {
            EndingSequence ending = BuildEnding();
            WorkerIdentity.FirstName = "{WORKER-FIRSTNAME}";
            WorkerIdentity.LastName = "{WORKER-LASTNAME}";
            yield return null;

            Assert.That(ending.CardText, Does.Contain("Employee of the Year"));
            Assert.That(ending.CardText, Does.Contain("{WORKER-FIRSTNAME} {WORKER-LASTNAME}"),
                "The card uses the worker's name.");
            Assert.That(ending.CardText, Does.Not.Contain("{WORKER-"),
                "The name tokens must be filled in, not shown raw.");
        }

        [UnityTest]
        public IEnumerator WalkingIntoTheDoorwayStartsTheEnding()
        {
            GameObject player = new GameObject("Player");
            objects.Add(player);
            player.AddComponent<CharacterController>();
            player.AddComponent<PlayerInteractor>();
            player.AddComponent<FirstPersonController>();
            player.transform.position = Vector3.zero;

            EndingSequence ending = BuildEnding();
            Set(ending, "doorwayCentre", new Vector3(0f, 0f, 7f));
            Set(ending, "doorwayRadius", 1.5f);

            yield return null;
            Assert.That(ending.HasStarted, Is.False, "Standing in the room is not enough.");

            player.transform.position = new Vector3(0f, 0f, 7f);
            yield return null;
            Assert.That(ending.HasStarted, Is.True, "Reaching the doorway begins the ending.");
        }

        [UnityTest]
        public IEnumerator EndingOnlyBeginsOnce()
        {
            EndingSequence ending = BuildEnding();
            int started = 0;
            ending.OnEndingStarted.AddListener(() => started++);
            yield return null;

            ending.Begin();
            ending.Begin();
            ending.Begin();
            Assert.That(started, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator EndingFreezesThePlayer()
        {
            GameObject player = new GameObject("Player");
            objects.Add(player);
            var standIn = player.AddComponent<FirstPersonHUD>();

            EndingSequence ending = BuildEnding();
            Set(ending, "frozenDuringEnding", standIn);
            yield return null;

            ending.Begin();
            Assert.That(standIn.enabled, Is.False,
                "The player cannot walk back inside once the ending starts.");
        }

        [Test]
        public void WorkerNameFormattingHandlesBothTokenStyles()
        {
            WorkerIdentity.FirstName = "ANNE";
            WorkerIdentity.LastName = "KOWALSKI";
            Assert.That(WorkerIdentity.Format("{WORKER-FIRSTNAME} {WORKER-LASTNAME}"),
                Is.EqualTo("ANNE KOWALSKI"));
            Assert.That(WorkerIdentity.Format("HELLO WORKER-FIRSTNAME WORKER-LASTNAME."),
                Is.EqualTo("HELLO ANNE KOWALSKI."), "The opening narration style also works.");
            Assert.That(WorkerIdentity.FullName, Is.EqualTo("ANNE KOWALSKI"));

            // Blank input keeps the previous name rather than emptying the card.
            WorkerIdentity.FirstName = "   ";
            Assert.That(WorkerIdentity.FirstName, Is.EqualTo("ANNE"));
        }
    }
}
