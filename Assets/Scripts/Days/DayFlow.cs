using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BigRedButton
{
    /// <summary>Tracks which day the player is on and moves between day scenes.</summary>
    public static class DayFlow
    {
        public const int FirstDay = 1;

        /// <summary>Scene name pattern for each day, for example "Day 3".</summary>
        public static string SceneNameFormat { get; set; } = "Day {0}";

        public static int CurrentDay { get; private set; } = FirstDay;
        public static int HighestDayReached { get; private set; } = FirstDay;

        /// <summary>Raised when a day scene starts, with the day number.</summary>
        public static event Action<int> DayStarted;

        /// <summary>Raised instead of loading when the last day has been completed.</summary>
        public static event Action SequenceCompleted;

        // Replaceable so automated tests can verify progression without loading scenes.
        public static Action<string> SceneLoader = name => SceneManager.LoadScene(name);
        public static Func<string, bool> SceneExists = Application.CanStreamedLevelBeLoaded;

        public static string SceneNameForDay(int day) =>
            string.Format(SceneNameFormat, Mathf.Max(FirstDay, day));

        public static bool DayExists(int day) =>
            day >= FirstDay && SceneExists(SceneNameForDay(day));

        /// <summary>Called by the day scene itself; does not load anything.</summary>
        public static void ReportDayStarted(int day)
        {
            CurrentDay = Mathf.Max(FirstDay, day);
            HighestDayReached = Mathf.Max(HighestDayReached, CurrentDay);
            DayStarted?.Invoke(CurrentDay);
        }

        /// <summary>Loads a specific day. Returns false when that day scene is missing.</summary>
        public static bool LoadDay(int day)
        {
            day = Mathf.Max(FirstDay, day);
            if (!DayExists(day))
            {
                Debug.LogWarning($"Day {day} scene \"{SceneNameForDay(day)}\" is not in the build " +
                    "settings. Use Tools > Big Red Button > Refresh Day Scene List.");
                return false;
            }

            CurrentDay = day;
            HighestDayReached = Mathf.Max(HighestDayReached, day);
            SceneLoader(SceneNameForDay(day));
            return true;
        }

        /// <summary>Advances one day, or reports the end of the sequence.</summary>
        public static bool LoadNextDay()
        {
            if (LoadDay(CurrentDay + 1))
                return true;

            SequenceCompleted?.Invoke();
            return false;
        }

        public static void ReloadCurrentDay() => LoadDay(CurrentDay);

        /// <summary>Returns to day one. Call before loading the first day from a menu.</summary>
        public static void ResetToFirstDay() => CurrentDay = FirstDay;

        /// <summary>Test hook: restores the default day state and scene hooks.</summary>
        public static void ResetForTests()
        {
            CurrentDay = FirstDay;
            HighestDayReached = FirstDay;
            SceneNameFormat = "Day {0}";
            DayStarted = null;
            SequenceCompleted = null;
            SceneLoader = name => SceneManager.LoadScene(name);
            SceneExists = Application.CanStreamedLevelBeLoaded;
        }
    }
}
