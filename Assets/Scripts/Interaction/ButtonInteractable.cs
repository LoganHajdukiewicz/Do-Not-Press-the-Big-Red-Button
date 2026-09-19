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

        [Header("Pressed indicator (debug)")]
        [Tooltip("Uncheck to keep the indicator hidden, for example in a finished level.")]
        [SerializeField] private bool showPressedIndicator = true;
        [Tooltip("Optional object shown when this button is pressed. Leave empty for none.")]
        [SerializeField] private GameObject pressedIndicator;

        [SerializeField] private UnityEvent onPressed = new UnityEvent();

        private bool hasBeenPressed;
        private float nextPressTime = float.NegativeInfinity;
        private string originalPrompt;
        private bool hasOriginalPrompt;

        public override string Prompt => prompt;
        public override bool CanInteract => base.CanInteract && interactable &&
            !(oneShot && hasBeenPressed) && Time.time >= nextPressTime;
        public UnityEvent OnPressed => onPressed;
        public int LastPressedFrame { get; private set; } = -1;

        /// <summary>
        /// Fires on every accepted press, even while <see cref="SuppressEvents"/> is on.
        /// Sounds and internal behaviours use this so a dead-feeling button still clicks.
        /// </summary>
        public event Action Pressed;

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

            hasBeenPressed = true;
            LastPressedFrame = Time.frameCount;
            nextPressTime = Time.time + cooldown;
            ApplyIndicator(true);

            // Decided before Pressed runs. A listener such as a wake-up handler may turn
            // suppression off during that press, and this press must still stay suppressed.
            bool runInspectorEvent = !SuppressEvents;
            Pressed?.Invoke();
            if (runInspectorEvent)
                onPressed.Invoke();
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
