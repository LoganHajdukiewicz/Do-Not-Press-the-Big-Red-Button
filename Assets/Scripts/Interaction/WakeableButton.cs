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
        [Tooltip("Presses needed to wake it up before it counts for real.")]
        [SerializeField, Min(1)] private int pressesToWake = 1;
        [Tooltip("Shown while the button is still grey and inert.")]
        [SerializeField] private string asleepPrompt = "Press button (no response)";
        [SerializeField] private UnityEvent onWoken = new UnityEvent();

        private ButtonInteractable button;
        private ButtonAppearance appearance;
        private int wakePresses;

        public bool IsAwake { get; private set; }
        public UnityEvent OnWoken => onWoken;

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
            if (wakePresses < pressesToWake)
                return;

            IsAwake = true;
            appearance.IsDisabledLook = false;
            appearance.SetGreen();
            button.RestorePrompt();
            button.SuppressEvents = false;
            onWoken.Invoke();
        }
    }
}
