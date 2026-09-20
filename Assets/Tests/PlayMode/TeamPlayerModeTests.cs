using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BigRedButton.Tests
{
    /// <summary>
    /// Team Player Mode: a red button sends the worker back to Day 1 instead of
    /// repeating the day they were on.
    /// </summary>
    public sealed class TeamPlayerModeTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<string> loaded = new List<string>();

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        [SetUp]
        public void SetUp()
        {
            DayFlow.ResetForTests();
            GameSettings.ResetForTests();
            loaded.Clear();
            DayFlow.SceneLoader = name => loaded.Add(name);
            DayFlow.SceneExists = _ => true;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null)
                    Object.DestroyImmediate(objects[i]);
            objects.Clear();
            DayFlow.ResetForTests();
            GameSettings.ResetForTests();
        }

        private DayLevel BuildDay(int dayNumber)
        {
            var host = new GameObject($"Day {dayNumber}");
            objects.Add(host);
            host.AddComponent<DayTitle>();
            var day = host.AddComponent<DayLevel>();
            Set(day, "dayNumber", dayNumber);
            Set(day, "delayBeforeNextDay", 0f);
            return day;
        }

        [Test]
        public void OffByDefaultSoAFailedDayRepeatsItself()
        {
            Assert.That(GameSettings.TeamPlayerMode, Is.False);
            Assert.That(GameSettings.DayAfterFailure(14), Is.EqualTo(14));
        }

        [Test]
        public void OnSendsAnyFailedDayBackToDayOne()
        {
            GameSettings.TeamPlayerMode = true;
            Assert.That(GameSettings.DayAfterFailure(14), Is.EqualTo(1));
            Assert.That(GameSettings.DayAfterFailure(31), Is.EqualTo(1));
            Assert.That(GameSettings.DayAfterFailure(1), Is.EqualTo(1),
                "Failing on day one still restarts day one.");
        }

        [UnityTest]
        public IEnumerator RedButtonOnADeepDayReturnsToDayOne()
        {
            GameSettings.TeamPlayerMode = true;
            DayLevel day = BuildDay(14);
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            objects.Add(cap);
            var button = cap.AddComponent<ButtonInteractable>();
            Set(button, "cooldown", 0f);
            cap.AddComponent<ButtonAppearance>().SetRed();
            var resolver = cap.AddComponent<StatefulDayButton>();
            Set(resolver, "day", day);
            yield return null;

            button.Interact(null);
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 1" }),
                "In Team Player Mode a red press costs the whole month.");
            Assert.That(DayFlow.CurrentDay, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SameRedButtonOnlyRepeatsTheDayWithTheModeOff()
        {
            DayLevel day = BuildDay(14);
            yield return null;
            day.FailDay();
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 14" }));
            Assert.That(DayFlow.CurrentDay, Is.EqualTo(14));
        }

        [UnityTest]
        public IEnumerator ModeDoesNotAffectCompletingADay()
        {
            GameSettings.TeamPlayerMode = true;
            DayLevel day = BuildDay(14);
            yield return null;
            day.CompleteDay();
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 15" }),
                "Green still advances normally; only failure is harsher.");
        }

        [UnityTest]
        public IEnumerator PhysicalContactWithARedButtonAlsoRestartsTheMonth()
        {
            GameSettings.TeamPlayerMode = true;
            DayLevel day = BuildDay(20);
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            objects.Add(cap);
            var button = cap.AddComponent<ButtonInteractable>();
            Set(button, "cooldown", 0f);
            cap.AddComponent<ButtonAppearance>().SetRed();
            var resolver = cap.AddComponent<StatefulDayButton>();
            Set(resolver, "day", day);

            var player = new GameObject("Player", typeof(PlayerInteractor));
            objects.Add(player);
            yield return null;

            // "Red button touches" must count, not only pressing E.
            Assert.That(button.TryPressFromContact(player.GetComponent<PlayerInteractor>()), Is.True);
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 1" }));
        }

        [UnityTest]
        public IEnumerator OnlyTheFirstOutcomeCountsInEitherMode()
        {
            GameSettings.TeamPlayerMode = true;
            DayLevel day = BuildDay(9);
            yield return null;
            day.FailDay();
            day.FailDay();
            day.CompleteDay();
            yield return null;
            Assert.That(loaded, Is.EqualTo(new[] { "Day 1" }));
        }
    }
}
