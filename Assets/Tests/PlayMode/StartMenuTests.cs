using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BigRedButton.Tests
{
    public sealed class StartMenuTests
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

        private StartMenu BuildMenu()
        {
            var host = new GameObject("Start Menu");
            objects.Add(host);
            return host.AddComponent<StartMenu>();
        }

        [Test]
        public void TitleIsTheGameName()
        {
            StartMenu menu = BuildMenu();
            Assert.That(menu.GameTitle, Is.EqualTo("EMPLOYEE OF THE MONTH"));
        }

        [Test]
        public void StartLoadsDayOneSoTheOpeningPlays()
        {
            StartMenu menu = BuildMenu();
            int startPresses = 0;
            menu.OnStartPressed.AddListener(() => startPresses++);

            menu.StartGame();
            Assert.That(startPresses, Is.EqualTo(1));
            Assert.That(loaded, Is.EqualTo(new[] { "Day 1" }),
                "Day 1 owns the Opening.mp3 black screen, so the menu just loads it.");
            Assert.That(DayFlow.CurrentDay, Is.EqualTo(1));
        }

        [Test]
        public void StartAlwaysBeginsANewMonthFromDayOne()
        {
            DayFlow.LoadDay(17);
            loaded.Clear();
            Assert.That(DayFlow.CurrentDay, Is.EqualTo(17));

            BuildMenu().StartGame();
            Assert.That(loaded, Is.EqualTo(new[] { "Day 1" }),
                "Returning to the menu must not resume a half-finished month.");
        }

        [Test]
        public void StartOnlyFiresOnce()
        {
            StartMenu menu = BuildMenu();
            menu.StartGame();
            menu.StartGame();
            menu.StartGame();
            Assert.That(loaded.Count, Is.EqualTo(1));
            Assert.That(menu.HasStarted, Is.True);
        }

        [Test]
        public void MissingDayScenesLeaveTheMenuUsable()
        {
            DayFlow.SceneExists = _ => false;
            StartMenu menu = BuildMenu();
            LogAssert.ignoreFailingMessages = true;
            menu.StartGame();
            LogAssert.ignoreFailingMessages = false;

            Assert.That(loaded, Is.Empty);
            Assert.That(menu.HasStarted, Is.False,
                "A failed load must not trap the player on a dead front page.");
        }

        [Test]
        public void TeamPlayerModeCanBeSwitchedFromTheMenu()
        {
            StartMenu menu = BuildMenu();
            Assert.That(GameSettings.TeamPlayerMode, Is.False, "Off unless the player asks for it.");

            var changes = new List<bool>();
            GameSettings.TeamPlayerModeChanged += changes.Add;
            menu.SetTeamPlayerMode(true);
            Assert.That(GameSettings.TeamPlayerMode, Is.True);
            menu.SetTeamPlayerMode(true);
            Assert.That(changes, Is.EqualTo(new[] { true }), "Only real changes are announced.");
            menu.SetTeamPlayerMode(false);
            Assert.That(changes, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void TeamPlayerModeSurvivesStartingTheGame()
        {
            StartMenu menu = BuildMenu();
            menu.SetTeamPlayerMode(true);
            menu.StartGame();
            Assert.That(GameSettings.TeamPlayerMode, Is.True,
                "The choice must carry into the day scenes.");
        }

        [Test]
        public void OpeningTheMenuAppliesItsDefaultMode()
        {
            GameSettings.TeamPlayerMode = true;
            var host = new GameObject("Start Menu");
            objects.Add(host);
            var menu = host.AddComponent<StartMenu>();
            // Awake runs on AddComponent and applies the inspector default, which is off.
            Assert.That(GameSettings.TeamPlayerMode, Is.False);
            Assert.That(menu, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator TitleFadesInThenSettles()
        {
            StartMenu menu = BuildMenu();
            Set(menu, "fadeInDuration", 0.2f);
            Assert.That(menu.Alpha, Is.LessThan(0.5f));
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(menu.Alpha, Is.EqualTo(1f));
        }

        [Test]
        public void MenuReleasesTheGameplayCursorLock()
        {
            Cursor.lockState = CursorLockMode.Locked;
            BuildMenu();
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None),
                "The front page is a pointer screen, so the cursor must be free.");
        }
    }
}
