using UnityEngine;
using UnityEngine.Events;

namespace BigRedButton
{
    /// <summary>
    /// A button that has to be woken up before it works.
    /// Day 13: "The Green Button is disabled, it is Grey, once clicked once it becomes
    /// green and allows you to click it."
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ButtonInteractable), typeof(ButtonAppearance))]
    public sealed class WakeableButton : MonoBehaviour
    {
        [Header("Waking up")]
        [Tooltip("Presses needed to wake it up before it counts for real.")]
        [SerializeField, Min(1)] private int pressesToWake = 1;
        [Tooltip("Shown while the button is still grey and inert.")]
        [SerializeField] private string asleepPrompt = "Press button (no response)";
        [Tooltip("The colour it wakes up as. 1 is green, 0 is red.")]
        [SerializeField, Range(0f, 1f)] private float wakesUpAs = 1f;
        [Tooltip("Seconds after the waking press before it actually wakes.")]
        [SerializeField, Min(0f)] private float wakeDelay;

        [Header("Sound")]
        [Tooltip("Optional sound for a press that does nothing.")]
        [SerializeField] private AudioClip asleepClip;
        [SerializeField, Range(0f, 1f)] private float asleepVolume = 0.7f;

        [Header("Events")]
        [SerializeField] private UnityEvent onWoken = new UnityEvent();
        [Tooltip("Raised on a press that only counted towards waking it.")]
        [SerializeField] private UnityEvent onPressedWhileAsleep = new UnityEvent();

        private ButtonInteractable button;
        private ButtonAppearance appearance;
        private int wakePresses;

        public bool IsAwake { get; private set; }
        public int PressesRemaining => Mathf.Max(0, pressesToWake - wakePresses);
        public UnityEvent OnWoken => onWoken;
        public UnityEvent OnPressedWhileAsleep => onPressedWhileAsleep;

        private void Awake()
        {
            button = GetComponent<ButtonInteractable>();
            appearance = GetComponent<ButtonAppearance>();

            // Starts grey and inert; its real listeners stay untouched until it wakes.
            appearance.IsDisabledLook = true;
            button.SetPrompt(asleepPrompt);
            button.SuppressEvents = true;
            // The plain event still reaches us while the inspector event is suppressed.
            button.Pressed += HandlePress;
        }

        private void OnDestroy()
        {
            if (button != null)
                button.Pressed -= HandlePress;
        }

        private void HandlePress()
        {
            if (IsAwake)
                return;

            wakePresses++;
            onPressedWhileAsleep.Invoke();

            if (asleepClip != null)
                AudioSource.PlayClipAtPoint(asleepClip, transform.position, asleepVolume);

            if (wakePresses < pressesToWake)
                return;

            if (wakeDelay > 0f)
            {
                StartCoroutine(WakeAfterDelay());
                return;
            }

            Wake();
        }

        private System.Collections.IEnumerator WakeAfterDelay()
        {
            yield return new UnityEngine.WaitForSeconds(wakeDelay);
            Wake();
        }

        /// <summary>Wakes the button immediately, for example from another button.</summary>
        public void Wake()
        {
            if (IsAwake)
                return;

            IsAwake = true;
            appearance.IsDisabledLook = false;
            appearance.Greenness = wakesUpAs;
            button.RestorePrompt();
            button.SuppressEvents = false;
            onWoken.Invoke();
        }
    }
}
