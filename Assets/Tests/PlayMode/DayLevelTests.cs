using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BigRedButton.Tests
{
    public sealed class DayLevelTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<string> loaded = new List<string>();
        private DayLevel level;
        private DayTitle title;
        private int highestExistingDay;

        [SetUp]
        public void SetUp()
        {
            DayFlow.ResetForTests();
            loaded.Clear();
            highestExistingDay = 3;
            DayFlow.SceneLoader = name => loaded.Add(name);
            DayFlow.SceneExists = name =>
            {
                for (int day = DayFlow.FirstDay; day <= highestExistingDay; day++)
                    if (DayFlow.SceneNameForDay(day) == name)
                        return true;
                return false;
            };
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

        private void CreateDay(int day, float delay = 0f)
        {
            var host = new GameObject("Day " + day);
            objects.Add(host);
            title = host.AddComponent<DayTitle>();
            level = host.AddComponent<DayLevel>();
            var fields = typeof(DayLevel).GetField("dayNumber", BindingFlags.Instance | BindingFlags.NonPublic);
            fields.SetValue(level, day);
            typeof(DayLevel).GetField("delayBeforeNextDay", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(level, delay);
        }

        [UnityTest]
        public IEnumerator DayTitleShowsThenFadesOut()
        {
            CreateDay(2);
            SetTitleTiming(0.05f, 0.05f, 0.1f);
            yield return null;
            Assert.That(DayFlow.CurrentDay, Is.EqualTo(2), "The scene reports its own day number.");
            Assert.That(title.IsPlaying, Is.True);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(title.CurrentAlpha, Is.GreaterThan(0.9f), "The title holds at full opacity.");
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.That(title.CurrentAlpha, Is.LessThan(0.9f).And.GreaterThan(0f), "It fades gradually.");
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(title.IsPlaying, Is.False);
            Assert.That(title.CurrentAlpha, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CompletingADayLoadsTheNextDay()
        {
            CreateDay(1);
            yield return null;
            level.CompleteDay();
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 2" }));
        }

        [UnityTest]
        public IEnumerator ButtonPressCanCompleteTheDay()
        {
            CreateDay(1);
            GameObject buttonObject = new GameObject("Green Button");
            objects.Add(buttonObject);
            var button = buttonObject.AddComponent<ButtonInteractable>();
            button.OnPressed.AddListener(level.CompleteDay);
            yield return null;
            button.Interact(null);
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 2" }));
        }

        [UnityTest]
        public IEnumerator OnlyOneOutcomeHappensPerDay()
        {
            CreateDay(1, 0.15f);
            yield return null;
            level.CompleteDay();
            level.CompleteDay();
            level.FailDay();
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(loaded, Is.EqualTo(new[] { "Day 2" }));
        }

        [UnityTest]
        public IEnumerator FailingRepeatsTheSameDay()
        {
            CreateDay(2);
            yield return null;
            level.FailDay();
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 2" }));
        }

        [UnityTest]
        public IEnumerator FinalDayRaisesItsOwnEventWithoutLoading()
        {
            CreateDay(highestExistingDay);
            int endings = 0;
            level.OnFinalDayCompleted.AddListener(() => endings++);
            yield return null;
            level.CompleteDay();
            yield return null;
            Assert.That(loaded, Is.Empty);
            Assert.That(endings, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DelayIsRespectedBeforeLoading()
        {
            CreateDay(1, 0.25f);
            yield return null;
            level.CompleteDay();
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(loaded, Is.Empty, "The next day waits for the delay.");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(loaded, Is.EqualTo(new[] { "Day 2" }));
        }

        [UnityTest]
        public IEnumerator TitleStillFadesWhileGameplayIsPaused()
        {
            CreateDay(1);
            SetTitleTiming(0f, 0.05f, 0.05f);
            float previous = Time.timeScale;
            Time.timeScale = 0f;
            try
            {
                yield return null;
                yield return new WaitForSecondsRealtime(0.25f);
                Assert.That(title.IsPlaying, Is.False);
            }
            finally
            {
                Time.timeScale = previous;
            }
        }

        private void SetTitleTiming(float fadeIn, float hold, float fadeOut)
        {
            void Set(string field, float value) =>
                typeof(DayTitle).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(title, value);
            Set("fadeInDuration", fadeIn);
            Set("holdDuration", hold);
            Set("fadeOutDuration", fadeOut);
            title.Play(level.DayNumber);
        }
    }
}
