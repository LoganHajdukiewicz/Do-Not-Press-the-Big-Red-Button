namespace BigRedButton
{
    /// <summary>Immutable state captured before any accepted-click listeners change the button.</summary>
    public readonly struct ButtonPress
    {
        public int Number { get; }
        public bool WasDisabled { get; }
        public bool WasGreen { get; }
        public bool CompletesSequence { get; }

        public ButtonPress(int number, bool wasDisabled, bool wasGreen, bool completesSequence)
        {
            Number = number;
            WasDisabled = wasDisabled;
            WasGreen = wasGreen;
            CompletesSequence = completesSequence;
        }
    }
}
