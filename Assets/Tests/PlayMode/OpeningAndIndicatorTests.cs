using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BigRedButton.Tests
{
    public sealed class OpeningAndIndicatorTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();

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

        private (ButtonInteractable button, GameObject indicator) CreateButtonWithIndicator()
        {
            var host = new GameObject("Button");
            objects.Add(host);
            var indicator = new GameObject("Indicator");
            objects.Add(indicator);
            var button = host.AddComponent<ButtonInteractable>();
            Set(button, "cooldown", 0f);
            Set(button, "pressedIndicator", indicator);
            return (button, indicator);
        }

        [UnityTest]
        public IEnumerator IndicatorAppearsOnPressAndStartsHidden()
        {
            var (button, indicator) = CreateButtonWithIndicator();
            yield return null;
            Assert.That(indicator.activeSelf, Is.False, "The indicator starts hidden.");
            button.Interact(null);
            Assert.That(indicator.activeSelf, Is.True);
            button.ResetButton();
            Assert.That(indicator.activeSelf, Is.False, "Resetting the button hides it again.");
        }

        [UnityTest]
        public IEnumerator IndicatorCanBeTurnedOffInTheInspector()
        {
            var (button, indicator) = CreateButtonWithIndicator();
            Set(button, "showPressedIndicator", false);
            yield return null;
            int presses = 0;
            button.OnPressed.AddListener(() => presses++);
            button.Interact(null);
            Assert.That(indicator.activeSelf, Is.False, "The indicator stays hidden when switched off.");
            Assert.That(presses, Is.EqualTo(1), "The button still works with the indicator off.");
        }

        [UnityTest]
        public IEnumerator IndicatorToggleAppliesImmediatelyWhilePressed()
        {
            var (button, indicator) = CreateButtonWithIndicator();
            yield return null;
            button.Interact(null);
            Assert.That(indicator.activeSelf, Is.True);
            button.ShowPressedIndicator = false;
            Assert.That(indicator.activeSelf, Is.False);
            button.ShowPressedIndicator = true;
            Assert.That(indicator.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator ButtonWithoutAnIndicatorStillWorks()
        {
            var host = new GameObject("Plain button");
            objects.Add(host);
            var button = host.AddComponent<ButtonInteractable>();
            int presses = 0;
            button.OnPressed.AddListener(() => presses++);
            yield return null;
            button.Interact(null);
            Assert.That(presses, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator OpeningStartsFullyBlackThenClears()
        {
            var host = new GameObject("Opening");
            objects.Add(host);
            var opening = host.AddComponent<OpeningSequence>();
            Set(opening, "fallbackNarrationLength", 0.2f);
            Set(opening, "holdAfterNarration", 0f);
            Set(opening, "fadeOutDuration", 0.2f);
            Set(opening, "textStartDelay", 0.05f);
            Set(opening, "textRevealDuration", 0.1f);

            int finished = 0;
            opening.OnOpeningFinished.AddListener(() => finished++);
            yield return null;
            Assert.That(opening.BlackAlpha, Is.EqualTo(1f), "The game opens on a fully black screen.");

            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(opening.VisibleText, Is.EqualTo("DO NOT PRESS THE BIG RED BUTTON"),
                "The warning finishes appearing while the screen is still black.");

            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(opening.IsRunning, Is.False);
            Assert.That(opening.BlackAlpha, Is.Zero, "The black screen fades away completely.");
            Assert.That(finished, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator OpeningRevealsTheWarningTextGradually()
        {
            var host = new GameObject("Opening");
            objects.Add(host);
            var opening = host.AddComponent<OpeningSequence>();
            Set(opening, "fallbackNarrationLength", 2f);
            Set(opening, "textStartDelay", 0f);
            Set(opening, "textRevealDuration", 1f);
            yield return null;
            Assert.That(opening.VisibleText.Length, Is.LessThan(31));
            yield return new WaitForSecondsRealtime(0.3f);
            int partial = opening.VisibleText.Length;
            Assert.That(partial, Is.GreaterThan(0).And.LessThan(31), "Text appears as it is spoken.");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(opening.VisibleText.Length, Is.GreaterThan(partial));
        }

        [UnityTest]
        public IEnumerator OpeningRestoresPlayerControlWhenItFinishes()
        {
            // A stand-in for the player: the opening only toggles "enabled", and a real
            // FirstPersonController needs a full input asset and camera rig to stay enabled.
            var playerObject = new GameObject("Player");
            objects.Add(playerObject);
            var controller = playerObject.AddComponent<FirstPersonHUD>();

            var host = new GameObject("Opening");
            objects.Add(host);
            var opening = host.AddComponent<OpeningSequence>();
            Set(opening, "frozenDuringOpening", controller);
            Set(opening, "fallbackNarrationLength", 0.1f);
            Set(opening, "holdAfterNarration", 0f);
            Set(opening, "fadeOutDuration", 0.1f);
            yield return null;
            Assert.That(controller.enabled, Is.False, "The player cannot move during the opening.");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(opening.IsRunning, Is.False);
            Assert.That(controller.enabled, Is.True, "Control returns once the screen clears.");
        }

        [UnityTest]
        public IEnumerator OpeningCanBeFinishedEarly()
        {
            var host = new GameObject("Opening");
            objects.Add(host);
            var opening = host.AddComponent<OpeningSequence>();
            Set(opening, "fallbackNarrationLength", 30f);
            yield return null;
            opening.Finish();
            Assert.That(opening.IsRunning, Is.False);
            Assert.That(opening.BlackAlpha, Is.Zero);
            opening.Finish(); // Calling twice must not raise the event again.
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TitleWaitsForTheOpeningWhenPlayOnStartIsOff()
        {
            DayFlow.ResetForTests();
            DayFlow.SceneExists = _ => true;
            DayFlow.SceneLoader = _ => { };

            var host = new GameObject("Day");
            objects.Add(host);
            var title = host.AddComponent<DayTitle>();
            Set(title, "playOnStart", false);
            var level = host.AddComponent<DayLevel>();
            Set(level, "dayNumber", 1);
            yield return null;
            Assert.That(title.IsPlaying, Is.False, "The title holds until the opening finishes.");
            title.PlayCurrentDay();
            Assert.That(title.IsPlaying, Is.True);
        }

        [UnityTest]
        public IEnumerator TimedRevealHidesThenShowsTheTarget()
        {
            var target = new GameObject("Hidden button");
            objects.Add(target);
            var host = new GameObject("Day");
            objects.Add(host);
            var reveal = host.AddComponent<TimedReveal>();
            Set(reveal, "target", target);
            Set(reveal, "delay", 0.2f);

            int revealed = 0;
            reveal.OnRevealed.AddListener(() => revealed++);
            yield return null;
            Assert.That(target.activeSelf, Is.False, "The target is hidden at the start of the day.");
            yield return new WaitForSeconds(0.35f);
            Assert.That(target.activeSelf, Is.True);
            Assert.That(revealed, Is.EqualTo(1));
        }
    }
}
