using System.Collections.Generic;
using NUnit.Framework;

namespace BigRedButton.Tests
{
    public sealed class DayFlowTests
    {
        private readonly List<string> loaded = new List<string>();
        private readonly List<int> started = new List<int>();
        private int highestExistingDay;
        private int completedEvents;

        [SetUp]
        public void SetUp()
        {
            DayFlow.ResetForTests();
            loaded.Clear();
            started.Clear();
            completedEvents = 0;
            highestExistingDay = 3;
            DayFlow.SceneLoader = name => loaded.Add(name);
            DayFlow.SceneExists = name =>
            {
                for (int day = DayFlow.FirstDay; day <= highestExistingDay; day++)
                    if (DayFlow.SceneNameForDay(day) == name)
                        return true;
                return false;
            };
            DayFlow.DayStarted += started.Add;
            DayFlow.SequenceCompleted += () => completedEvents++;
        }

        [TearDown]
        public void TearDown() => DayFlow.ResetForTests();

        [Test]
        public void GameStartsOnDayOne()
        {
            Assert.That(DayFlow.CurrentDay, Is.EqualTo(1));
            Assert.That(DayFlow.SceneNameForDay(1), Is.EqualTo("Day 1"));
        }

        [Test]
        public void EachDayLeadsToTheNext()
        {
            DayFlow.ReportDayStarted(1);
            Assert.That(DayFlow.LoadNextDay(), Is.True);
            Assert.That(loaded, Is.EqualTo(new[] { "Day 2" }));
            DayFlow.ReportDayStarted(2);
            Assert.That(DayFlow.LoadNextDay(), Is.True);
            Assert.That(loaded, Is.EqualTo(new[] { "Day 2", "Day 3" }));
            Assert.That(DayFlow.CurrentDay, Is.EqualTo(3));
            Assert.That(started, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void MissingNextDayReportsCompletionInsteadOfLoading()
        {
            DayFlow.ReportDayStarted(highestExistingDay);
            Assert.That(DayFlow.LoadNextDay(), Is.False);
            Assert.That(loaded, Is.Empty);
            Assert.That(completedEvents, Is.EqualTo(1));
            Assert.That(DayFlow.CurrentDay, Is.EqualTo(highestExistingDay),
                "The final day must stay current when there is no next day.");
        }

        [Test]
        public void ReloadAndResetUseTheCurrentDay()
        {
            DayFlow.ReportDayStarted(2);
            DayFlow.ReloadCurrentDay();
            Assert.That(loaded, Is.EqualTo(new[] { "Day 2" }));
            DayFlow.ResetToFirstDay();
            Assert.That(DayFlow.CurrentDay, Is.EqualTo(1));
            Assert.That(DayFlow.HighestDayReached, Is.EqualTo(2), "Progress tracking is kept.");
        }

        [Test]
        public void UnregisteredOrInvalidDaysAreRejected()
        {
            Assert.That(DayFlow.LoadDay(99), Is.False);
            Assert.That(loaded, Is.Empty);
            Assert.That(DayFlow.DayExists(0), Is.False);
            Assert.That(DayFlow.SceneNameForDay(0), Is.EqualTo("Day 1"));
        }

        [Test]
        public void SceneNameFormatCanBeCustomized()
        {
            DayFlow.SceneNameFormat = "Level_{0}";
            Assert.That(DayFlow.SceneNameForDay(4), Is.EqualTo("Level_4"));
        }
    }
}
