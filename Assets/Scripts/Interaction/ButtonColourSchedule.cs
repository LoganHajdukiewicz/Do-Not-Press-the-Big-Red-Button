using UnityEngine;

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
        [Tooltip("Seconds spent looking red before switching.")]
        [SerializeField, Min(0.1f)] private float redDuration = 10f;
        [Tooltip("Seconds spent looking green before switching back.")]
        [SerializeField, Min(0.1f)] private float greenDuration = 10f;
        [Tooltip("Seconds the colour takes to change. 0 switches instantly.")]
        [SerializeField, Min(0f)] private float blendDuration = 0.6f;
        [SerializeField] private bool startGreen;
        [Tooltip("Stops switching once the button has been pressed.")]
        [SerializeField] private bool stopOnPress = true;

        private ButtonAppearance appearance;
        private ButtonInteractable button;
        private float timer;
        private bool showingGreen;
        private bool running = true;

        public bool LooksGreen => appearance != null && appearance.Greenness > 0.5f;

        private void Awake()
        {
            appearance = GetComponent<ButtonAppearance>();
            button = GetComponent<ButtonInteractable>();
            showingGreen = startGreen;
            appearance.Greenness = showingGreen ? 1f : 0f;

            if (stopOnPress && button != null)
                button.OnPressed.AddListener(Stop);
        }

        public void Stop() => running = false;

        private void Update()
        {
            if (!running)
                return;

            timer += Time.deltaTime;
            float hold = showingGreen ? greenDuration : redDuration;
            if (timer >= hold)
            {
                timer = 0f;
                showingGreen = !showingGreen;
            }

            float target = showingGreen ? 1f : 0f;
            appearance.Greenness = blendDuration <= 0f
                ? target
                : Mathf.MoveTowards(appearance.Greenness, target, Time.deltaTime / blendDuration);
        }
    }
}
