using System;
using UnityEngine;

namespace BigRedButton
{
    /// <summary>
    /// Options chosen on the start menu. Lives in memory for the session, like the
    /// day progress, so the menu and the day scenes agree without a save file.
    /// </summary>
    public static class GameSettings
    {
        private static bool teamPlayerMode;

        /// <summary>
        /// On: pressing a red button sends the worker back to Day 1 instead of repeating
        /// the current day. The company calls this being a team player.
        /// </summary>
        public static bool TeamPlayerMode
        {
            get => teamPlayerMode;
            set
            {
                if (teamPlayerMode == value)
                    return;
                teamPlayerMode = value;
                TeamPlayerModeChanged?.Invoke(value);
            }
        }

        public static event Action<bool> TeamPlayerModeChanged;

        /// <summary>The day a failed day sends the player to.</summary>
        public static int DayAfterFailure(int currentDay) =>
            TeamPlayerMode ? DayFlow.FirstDay : Mathf.Max(DayFlow.FirstDay, currentDay);

        public static void ResetForTests()
        {
            teamPlayerMode = false;
            TeamPlayerModeChanged = null;
        }
    }
}
