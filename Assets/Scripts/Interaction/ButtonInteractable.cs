using System;
using UnityEngine;
using UnityEngine.Events;

namespace BigRedButton
{
    [DisallowMultipleComponent]
    public sealed class ButtonInteractable : Interactable
    {
        [SerializeField] private string prompt = "Press button";
        [SerializeField] private bool interactable = true;
        [SerializeField] private bool oneShot;
        [Tooltip("Also press when the player's CharacterController physically touches this button.")]
        [SerializeField] private bool pressOnContact = true;
        [SerializeField, Min(0f)] private float cooldown = 0.25f;

        [Header("Click requirement")]
        [Tooltip("Active clicks needed to fire On Pressed. Grey/wake-up clicks do not count. " +
            "After completion the count restarts, unless One Shot is enabled.")]
        [SerializeField, Min(1)] private int requiredPresses = 1;

        [Header("Pressed indicator (debug)")]
        [Tooltip("Uncheck to keep the indicator hidden, for example in a finished level.")]
        [SerializeField] private bool showPressedIndicator = true;
        [Tooltip("Optional object shown when this button is pressed. Leave empty for none.")]
        [SerializeField] private GameObject pressedIndicator;

        [Tooltip("Runs once the required active clicks have been reached, not for wake-up clicks.")]
        [SerializeField] private UnityEvent onPressed = new UnityEvent();

        private bool hasBeenPressed;
        private bool hasCompleted;
        private bool dispatching;
        private int activePresses;
        private float nextPressTime = float.NegativeInfinity;
        private string originalPrompt;
        private bool hasOriginalPrompt;

        public override string Prompt => prompt;
        public override bool CanInteract => base.CanInteract && interactable && !dispatching &&
            !(oneShot && hasCompleted) && Time.time >= nextPressTime;
        public int RequiredPresses => Mathf.Max(1, requiredPresses);
        public int PressesRemaining => oneShot && hasCompleted ? 0 :
            Mathf.Max(0, RequiredPresses - activePresses);
        public int AcceptedPressCount { get; private set; }
        public ButtonPress LastPress { get; private set; }

        /// <summary>Grey/suppressed presses may still click and wake, but never resolve or ding.</summary>
        public bool IsDeactivated => !isActiveAndEnabled || !interactable || SuppressEvents ||
            GetComponent<ButtonAppearance>()?.IsDisabledLook == true;

        public bool CountsAsGreen
        {
            get
            {
                var appearance = GetComponent<ButtonAppearance>();
                var resolver = GetComponent<StatefulDayButton>();
                return !IsDeactivated && (appearance == null ||
                    appearance.Greenness >= (resolver != null ? resolver.GreenThreshold : 0.5f));
            }
        }
        public UnityEvent OnPressed => onPressed;
        public int LastPressedFrame { get; private set; } = -1;

        /// <summary>
        /// Fires on every accepted press, even while <see cref="SuppressEvents"/> is on.
        /// Legacy internal behaviours (such as wake-up) use this. New state-sensitive
        /// listeners should use PressAccepted, which supplies the pre-click snapshot.
        /// </summary>
        public event Action Pressed;

        /// <summary>Every accepted click, including wake-up/partial clicks, with its original state.</summary>
        public event Action<ButtonPress> PressAccepted;

        /// <summary>
        /// Stops the inspector's On Pressed event from running, without blocking the press
        /// itself. Used by a button that must feel inert until something wakes it up.
        /// </summary>
        public bool SuppressEvents { get; set; }

        /// <summary>Shows or hides the pressed indicator, honouring the inspector toggle.</summary>
        public bool ShowPressedIndicator
        {
            get => showPressedIndicator;
            set
            {
                showPressedIndicator = value;
                ApplyIndicator(value && hasBeenPressed);
            }
        }

        private void Awake() => ApplyIndicator(false);

        public bool TryPressFromContact(PlayerInteractor player)
        {
            if (!pressOnContact || player == null || !player.isActiveAndEnabled || !CanInteract)
                return false;

            Interact(player);
            return true;
        }

        public override void Interact(PlayerInteractor player)
        {
            if (!CanInteract)
                return;

            bool disabledAtPress = IsDeactivated;
            bool greenAtPress = CountsAsGreen;
            if (!disabledAtPress)
                activePresses++;
            bool completes = !disabledAtPress && activePresses >= RequiredPresses;
            if (completes)
            {
                activePresses = 0;
                hasCompleted = true;
            }

            var press = new ButtonPress(++AcceptedPressCount, disabledAtPress, greenAtPress, completes);
            LastPress = press;
            hasBeenPressed = true;
            LastPressedFrame = Time.frameCount;
            nextPressTime = Time.time + cooldown;
            ApplyIndicator(true);

            // Freeze state before wake-up, colour-change or dialogue callbacks can alter it.
            // One Shot is consumed only by a completed sequence, never by a wake-up click.
            dispatching = true;
            try
            {
                Pressed?.Invoke();
                PressAccepted?.Invoke(press);
                if (press.CompletesSequence)
                    onPressed.Invoke();
            }
            finally
            {
                dispatching = false;
            }
        }

        // These methods can also be connected in another button's Inspector UnityEvent.
        public void SetInteractable(bool value) => interactable = value;

        /// <summary>Replaces the prompt, remembering the original so it can be restored.</summary>
        public void SetPrompt(string value)
        {
            if (!hasOriginalPrompt)
            {
                originalPrompt = prompt;
                hasOriginalPrompt = true;
            }

            prompt = value ?? string.Empty;
        }

        public void RestorePrompt()
        {
            if (hasOriginalPrompt)
                prompt = originalPrompt;
        }

        public void ResetButton()
        {
            hasBeenPressed = false;
            hasCompleted = false;
            activePresses = 0;
            AcceptedPressCount = 0;
            if (!dispatching)
                LastPress = default;
            nextPressTime = float.NegativeInfinity;
            ApplyIndicator(false);
        }

        private void ApplyIndicator(bool pressed)
        {
            if (pressedIndicator != null)
                pressedIndicator.SetActive(showPressedIndicator && pressed);
        }

        public void LogPress() => Debug.Log($"{name} pressed.", this);
    }
}
