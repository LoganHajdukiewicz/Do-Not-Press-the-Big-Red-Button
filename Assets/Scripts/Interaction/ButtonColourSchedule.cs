using UnityEngine;
using UnityEngine.Events;

namespace BigRedButton
{
    /// <summary>
    /// Swaps a button's colour back and forth on a timer.
    /// Day 4: "The Red Button turns green after 10 seconds. It switches back after 10 seconds."
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ButtonAppearance))]
    public sealed class ButtonColourSchedule : MonoBehaviour
    {
        [Header("Timing")]
        [Tooltip("Seconds spent looking red before switching.")]
        [SerializeField, Min(0.1f)] private float redDuration = 10f;
        [Tooltip("Seconds spent looking green before switching back.")]
        [SerializeField, Min(0.1f)] private float greenDuration = 10f;
        [Tooltip("Seconds the colour takes to change. 0 switches instantly.")]
        [SerializeField, Min(0f)] private float blendDuration = 0.6f;
        [Tooltip("Seconds before the first switch, on top of the durations above.")]
        [SerializeField, Min(0f)] private float startDelay;

        [Header("Starting state")]
        [SerializeField] private bool startGreen;
        [Tooltip("Number of switches before it stops. 0 runs forever.")]
        [SerializeField, Min(0)] private int switchLimit;

        [Header("Behaviour")]
        [Tooltip("Stops switching once the button has been pressed.")]
        [SerializeField] private bool stopOnPress = true;
        [Tooltip("Uses unscaled time, so it keeps running if the game pauses.")]
        [SerializeField] private bool ignoreTimeScale;

        [Header("Events")]
        [SerializeField] private UnityEvent onTurnedGreen = new UnityEvent();
        [SerializeField] private UnityEvent onTurnedRed = new UnityEvent();

        private ButtonAppearance appearance;
        private ButtonInteractable button;
        private float timer;
        private float delayTimer;
        private int switches;
        private bool showingGreen;
        private bool running = true;

        public bool LooksGreen => appearance != null && appearance.Greenness > 0.5f;
        /// <summary>Seconds until the next colour switch.</summary>
        public float TimeUntilSwitch =>
            Mathf.Max(0f, (showingGreen ? greenDuration : redDuration) - timer);
        public UnityEvent OnTurnedGreen => onTurnedGreen;
        public UnityEvent OnTurnedRed => onTurnedRed;

        private void Awake()
        {
            appearance = GetComponent<ButtonAppearance>();
            button = GetComponent<ButtonInteractable>();
            showingGreen = startGreen;
            appearance.Greenness = showingGreen ? 1f : 0f;

            if (stopOnPress && button != null)
                button.Pressed += Stop;
        }

        private void OnDestroy()
        {
            if (button != null)
                button.Pressed -= Stop;
        }

        public void Stop() => running = false;

        /// <summary>Starts the cycle again, useful from another button's event.</summary>
        public void Resume() => running = true;

        private void Update()
        {
            if (!running)
                return;

            float delta = ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;

            if (delayTimer < startDelay)
            {
                delayTimer += delta;
                return;
            }

            timer += delta;
            float hold = showingGreen ? greenDuration : redDuration;
            if (timer >= hold)
            {
                timer = 0f;
                showingGreen = !showingGreen;
                switches++;
                (showingGreen ? onTurnedGreen : onTurnedRed).Invoke();
                if (switchLimit > 0 && switches >= switchLimit)
                    running = false;
            }

            float target = showingGreen ? 1f : 0f;
            appearance.Greenness = blendDuration <= 0f
                ? target
                : Mathf.MoveTowards(appearance.Greenness, target, delta / blendDuration);
        }
    }
}
