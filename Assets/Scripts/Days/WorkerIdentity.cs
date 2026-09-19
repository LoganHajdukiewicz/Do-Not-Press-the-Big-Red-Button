using UnityEngine;

namespace BigRedButton
{
    /// <summary>
    /// The worker's name, used by the opening narration and the closing card.
    /// Kept in one place so the ending can address the player by the same name.
    /// </summary>
    public static class WorkerIdentity
    {
        public const string FirstNameToken = "{WORKER-FIRSTNAME}";
        public const string LastNameToken = "{WORKER-LASTNAME}";

        private static string firstName = "{WORKER-FIRSTNAME}";

        private static string lastName = "{WORKER-LASTNAME}";

        public static string FirstName
        {
            get => firstName;
            set => firstName = string.IsNullOrWhiteSpace(value) ? firstName : value.Trim();
        }

        public static string LastName
        {
            get => lastName;
            set => lastName = string.IsNullOrWhiteSpace(value) ? lastName : value.Trim();
        }

        public static string FullName => $"{FirstName} {LastName}";

        /// <summary>Fills in the name tokens used in the design document.</summary>
        public static string Format(string text) => string.IsNullOrEmpty(text)
            ? string.Empty
            : text.Replace(FirstNameToken, FirstName)
                .Replace(LastNameToken, LastName)
                .Replace("WORKER-FIRSTNAME", FirstName)
                .Replace("WORKER-LASTNAME", LastName);

        /// <summary>Test hook: restores the default name.</summary>
        public static void ResetForTests()
        {
            firstName = "{WORKER-FIRSTNAME}";
            lastName = "{WORKER-LASTNAME}";
        }
    }
}
