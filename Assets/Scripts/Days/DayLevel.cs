using UnityEngine;
using UnityEngine.Events;

namespace BigRedButton
{
    /// <summary>Put one of these in every day scene. Shows the title and advances the day.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DayTitle))]
    public sealed class DayLevel : MonoBehaviour
    {
        [Tooltip("Which day this scene is. Day 1 is the first level.")]
        [SerializeField, Min(DayFlow.FirstDay)] private int dayNumber = DayFlow.FirstDay;
        [Tooltip("Seconds to wait after the day is completed before the next day loads.")]
        [SerializeField, Min(0f)] private float delayBeforeNextDay = 1f;
        [SerializeField] private UnityEvent onDayStarted = new UnityEvent();
        [SerializeField] private UnityEvent onDayCompleted = new UnityEvent();
        [SerializeField] private UnityEvent onDayFailed = new UnityEvent();
        [Tooltip("Raised instead of loading when no later day scene exists.")]
        [SerializeField] private UnityEvent onFinalDayCompleted = new UnityEvent();

        private DayTitle title;
        private float transitionTime;
        private bool advancing;
        private bool failing;

        public int DayNumber => Mathf.Max(DayFlow.FirstDay, dayNumber);
        public bool IsResolved => advancing || failing;
        public UnityEvent OnDayStarted => onDayStarted;
        public UnityEvent OnDayCompleted => onDayCompleted;
        public UnityEvent OnDayFailed => onDayFailed;
        public UnityEvent OnFinalDayCompleted => onFinalDayCompleted;

        private void Awake() => title = GetComponent<DayTitle>();

        private void Start()
        {
            DayFlow.ReportDayStarted(DayNumber);
            // The opening sequence plays the title itself once the black screen clears.
            if (title.PlayOnStart)
                title.Play(DayNumber);
            onDayStarted.Invoke();
        }

        /// <summary>Connect this to the correct button's On Pressed event.</summary>
        public void CompleteDay()
        {
            if (IsResolved)
                return; // One outcome per day; ignore extra presses during the transition.

            advancing = true;
            transitionTime = Time.unscaledTime + delayBeforeNextDay;
            onDayCompleted.Invoke();
        }

        /// <summary>Connect this to the wrong button. Repeats the same day by default.</summary>
        public void FailDay()
        {
            if (IsResolved)
                return;

            failing = true;
            transitionTime = Time.unscaledTime + delayBeforeNextDay;
            onDayFailed.Invoke();
        }

        public void CompleteDayImmediately()
        {
            CompleteDay();
            transitionTime = Time.unscaledTime;
        }

        private void Update()
        {
            if (!IsResolved || Time.unscaledTime < transitionTime)
                return;

            bool wasAdvancing = advancing;
            advancing = false;
            failing = false;

            if (!wasAdvancing)
            {
                DayFlow.ReloadCurrentDay();
                return;
            }

            if (!DayFlow.DayExists(DayNumber + 1))
            {
                onFinalDayCompleted.Invoke();
                return; // Let the scene handle the ending instead of loading a missing day.
            }

            DayFlow.LoadDay(DayNumber + 1);
        }

        private void OnValidate() => dayNumber = Mathf.Max(DayFlow.FirstDay, dayNumber);
    }
}
