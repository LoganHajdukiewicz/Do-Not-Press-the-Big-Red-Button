using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BigRedButton.Tests
{
    /// <summary>
    /// Checks the built chain of days: that they exist, are registered in order, and
    /// that each one leads to the next.
    /// </summary>
    public sealed class DayChainTests
    {
        private const int ExpectedDays = 31;

        private static List<int> BuiltDayNumbers()
        {
            var days = new List<int>();
            foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
            {
                string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                if (name.StartsWith("Day ") && int.TryParse(name.Substring(4), out int number))
                    days.Add(number);
            }

            days.Sort();
            return days;
        }

        [Test]
        public void ThirtyDaysExistWithNoGaps()
        {
            List<int> days = BuiltDayNumbers();
            if (days.Count == 0)
                Assert.Ignore("No day scenes yet. Run Tools > Big Red Button > Build Days 1-31.");

            Assert.That(days, Is.EqualTo(Enumerable.Range(1, ExpectedDays).ToList()),
                "Days must run 1 to 30 with no gaps and no duplicates.");
        }

        [Test]
        public void EveryDayIsRegisteredInBuildSettingsInOrder()
        {
            List<int> days = BuiltDayNumbers();
            if (days.Count == 0)
                Assert.Ignore("No day scenes yet. Run Tools > Big Red Button > Build Days 1-31.");

            var registered = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => Path.GetFileNameWithoutExtension(s.path))
                .Where(n => n.StartsWith("Day "))
                .Select(n => int.Parse(n.Substring(4)))
                .ToList();

            Assert.That(registered, Is.EquivalentTo(days),
                "Every day scene must be enabled in build settings, or it cannot be loaded.");
            Assert.That(registered, Is.Ordered,
                "Day 10 must not sort before Day 2; the list is numeric, not alphabetical.");
        }

        [Test]
        public void EachDayLeadsToTheNextAllTheWayThrough()
        {
            List<int> days = BuiltDayNumbers();
            if (days.Count == 0)
                Assert.Ignore("No day scenes yet. Run Tools > Big Red Button > Build Days 1-31.");

            // Walk the chain the way the game does, without loading any scenes.
            DayFlow.ResetForTests();
            try
            {
                var visited = new List<int>();
                DayFlow.SceneExists = name =>
                {
                    if (!name.StartsWith("Day ") || !int.TryParse(name.Substring(4), out int number))
                        return false;
                    return days.Contains(number);
                };
                DayFlow.SceneLoader = _ => { };

                DayFlow.ReportDayStarted(DayFlow.FirstDay);
                visited.Add(DayFlow.CurrentDay);
                while (DayFlow.LoadNextDay())
                {
                    DayFlow.ReportDayStarted(DayFlow.CurrentDay);
                    visited.Add(DayFlow.CurrentDay);
                }

                Assert.That(visited, Is.EqualTo(days),
                    "Starting at day one and completing each day must reach the ending.");
                Assert.That(DayFlow.CurrentDay, Is.EqualTo(days[^1]));
            }
            finally
            {
                DayFlow.ResetForTests();
            }
        }

        [Test]
        public void EveryDaySceneHasExactlyOneDayLevelWithTheRightNumber()
        {
            List<int> days = BuiltDayNumbers();
            if (days.Count == 0)
                Assert.Ignore("No day scenes yet. Run Tools > Big Red Button > Build Days 1-31.");

            foreach (int day in days)
            {
                string path = $"Assets/Scenes/Days/Day {day}.unity";
                string text = File.Exists(path) ? File.ReadAllText(path) : null;
                Assert.That(text, Is.Not.Null, $"Missing scene file for day {day}.");

                int levels = CountOccurrences(text, "BigRedButton.DayLevel");
                Assert.That(levels, Is.EqualTo(1),
                    $"Day {day} must have exactly one DayLevel, found {levels}.");
                Assert.That(text, Does.Contain($"dayNumber: {day}"),
                    $"Day {day}'s DayLevel must be set to day number {day}.");
            }
        }

        [Test]
        public void EveryDayHasAWayToCompleteIt()
        {
            List<int> days = BuiltDayNumbers();
            if (days.Count == 0)
                Assert.Ignore("No day scenes yet. Run Tools > Big Red Button > Build Days 1-31.");

            foreach (int day in days)
            {
                string path = $"Assets/Scenes/Days/Day {day}.unity";
                string text = File.ReadAllText(path);

                // Either a button wired straight to CompleteDay, one that resolves by
                // colour, or the final day, which ends at the door instead of a button.
                bool wiredDirectly = text.Contains("CompleteDay");
                bool resolvesByColour = text.Contains("BigRedButton.StatefulDayButton");
                bool isTheEnding = text.Contains("BigRedButton.EndingSequence");
                Assert.That(wiredDirectly || resolvesByColour || isTheEnding, Is.True,
                    $"Day {day} has no way to finish it.");
            }
        }

        [Test]
        public void TheLastDayIsTheEndingWithNoButtons()
        {
            List<int> days = BuiltDayNumbers();
            if (days.Count == 0)
                Assert.Ignore("No day scenes yet. Run Tools > Big Red Button > Build Days 1-31.");

            string path = $"Assets/Scenes/Days/Day {days[^1]}.unity";
            string text = File.ReadAllText(path);

            Assert.That(days[^1], Is.EqualTo(ExpectedDays), "The month ends on day 31.");
            Assert.That(text, Does.Contain("BigRedButton.EndingSequence"),
                "The last day must run the ending.");
            Assert.That(text, Does.Not.Contain("BigRedButton.ButtonInteractable"),
                "There are no buttons on the last day. There is nothing to decide.");
        }

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int index = text.IndexOf(value, System.StringComparison.Ordinal);
            while (index >= 0)
            {
                count++;
                index = text.IndexOf(value, index + value.Length, System.StringComparison.Ordinal);
            }

            return count;
        }
    }
}
