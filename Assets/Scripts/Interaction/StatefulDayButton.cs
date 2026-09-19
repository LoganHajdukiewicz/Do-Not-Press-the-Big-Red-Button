using UnityEngine;
using UnityEngine.Events;

namespace BigRedButton
{
    /// <summary>
    /// Resolves a day by the colour the button is showing at the moment it is pressed.
    /// A button that looks green completes the day; the same button looking red fails it.
    /// This is what makes the colour tricks fair: what you see is what you get.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ButtonInteractable), typeof(ButtonAppearance))]
    public sealed class StatefulDayButton : MonoBehaviour
    {
        [Tooltip("The day this button belongs to. Found automatically when left empty.")]
        [SerializeField] private DayLevel day;
        [Tooltip("Greenness above this counts as green. 0.5 is the halfway point.")]
        [SerializeField, Range(0.05f, 0.95f)] private float greenThreshold = 0.5f;
        [Tooltip("A button showing the grey, disabled look does nothing when pressed.")]
        [SerializeField] private bool ignoreWhileDisabledLook = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onPressedWhileGreen = new UnityEvent();
        [SerializeField] private UnityEvent onPressedWhileRed = new UnityEvent();

        private ButtonInteractable button;
        private ButtonAppearance appearance;

        /// <summary>True when the button is currently showing green.</summary>
        public bool LooksGreen => appearance != null && appearance.Greenness >= greenThreshold;
        public UnityEvent OnPressedWhileGreen => onPressedWhileGreen;
        public UnityEvent OnPressedWhileRed => onPressedWhileRed;

        private void Awake()
        {
            button = GetComponent<ButtonInteractable>();
            appearance = GetComponent<ButtonAppearance>();
            if (day == null)
                day = FindFirstObjectByType<DayLevel>();

            // The suppressible event, so a sleeping button's wake-up press does not
            // resolve the day. It only counts once the button is actually awake.
            button.OnPressed.AddListener(Resolve);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.OnPressed.RemoveListener(Resolve);
        }

        private void Resolve()
        {
            // A grey button has no colour to judge, so it is neither answer.
            if (ignoreWhileDisabledLook && appearance.IsDisabledLook)
                return;

            bool green = LooksGreen;
            if (green)
                onPressedWhileGreen.Invoke();
            else
                onPressedWhileRed.Invoke();

            if (day == null)
                return;

            if (green)
                day.CompleteDay();
            else
                day.FailDay();
        }
    }
}
