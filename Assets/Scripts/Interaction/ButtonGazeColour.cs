using UnityEngine;
using UnityEngine.Events;

namespace BigRedButton
{
    /// <summary>
    /// Ties a button's colour to where the player is looking.
    /// Day 18: "When you hover on the Green Button it turns red after 0.5 seconds."
    /// Day 19: facing one way it is red, turning around makes it green.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ButtonAppearance))]
    public sealed class ButtonGazeColour : MonoBehaviour
    {
        public enum GazeMode
        {
            /// <summary>Looking straight at the button turns it red after a delay.</summary>
            TurnsRedWhenWatched,
            /// <summary>Colour follows the player's compass heading.</summary>
            FollowsPlayerHeading
        }

        [Header("Mode")]
        [SerializeField] private GazeMode mode = GazeMode.TurnsRedWhenWatched;

        [Header("Watched mode")]
        [Tooltip("Seconds of looking at it before the colour starts changing.")]
        [SerializeField, Min(0f)] private float hoverDelay = 0.5f;
        [Tooltip("Seconds the colour change takes once it begins.")]
        [SerializeField, Min(0f)] private float blendDuration = 0.25f;
        [Tooltip("How closely the player must aim to count as watching. Higher is stricter.")]
        [SerializeField, Range(0.8f, 0.999f)] private float aimTolerance = 0.985f;
        [Tooltip("Stops reacting beyond this distance. 0 means any distance.")]
        [SerializeField, Min(0f)] private float maxWatchDistance;
        [Tooltip("Colour it settles on while nobody is watching it.")]
        [SerializeField, Range(0f, 1f)] private float unwatchedGreenness = 1f;
        [Tooltip("Colour it changes to while being watched.")]
        [SerializeField, Range(0f, 1f)] private float watchedGreenness;
        [Tooltip("Seconds of looking away before it starts recovering.")]
        [SerializeField, Min(0f)] private float recoveryDelay;
        [Tooltip("Once watched, never recover. The player only gets one chance.")]
        [SerializeField] private bool latchOnceWatched;

        [Header("Heading mode")]
        [Tooltip("The heading where the button looks fully red.")]
        [SerializeField] private Vector3 redHeading = Vector3.forward;
        [Tooltip("Reverses the heading, so facing the reference is green instead of red.")]
        [SerializeField] private bool invertHeading;
        [Tooltip("Degrees of turn needed for the full colour change.")]
        [SerializeField, Range(30f, 360f)] private float headingSweep = 180f;

        [Header("References")]
        [Tooltip("Player camera. Found automatically when left empty.")]
        [SerializeField] private Transform viewer;
        [Tooltip("Stops all colour changes once the button has been pressed.")]
        [SerializeField] private bool freezeOnPress = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onBecameWatched = new UnityEvent();
        [SerializeField] private UnityEvent onBecameUnwatched = new UnityEvent();

        private ButtonAppearance appearance;
        private float hoverTime;
        private float awayTime;
        private bool wasWatched;
        private bool frozen;

        public bool IsWatched { get; private set; }
        public UnityEvent OnBecameWatched => onBecameWatched;
        public UnityEvent OnBecameUnwatched => onBecameUnwatched;

        private void Awake()
        {
            appearance = GetComponent<ButtonAppearance>();
            appearance.Greenness = mode == GazeMode.TurnsRedWhenWatched ? unwatchedGreenness : 0f;

            if (!freezeOnPress)
                return;
            var button = GetComponent<ButtonInteractable>();
            if (button != null)
                button.Pressed += Freeze;
        }

        private void OnDestroy()
        {
            var button = GetComponent<ButtonInteractable>();
            if (button != null)
                button.Pressed -= Freeze;
        }

        public void Freeze() => frozen = true;

        private void Update()
        {
            if (frozen)
                return;

            if (viewer == null)
            {
                Camera main = Camera.main;
                if (main == null)
                    return;
                viewer = main.transform;
            }

            if (mode == GazeMode.FollowsPlayerHeading)
            {
                UpdateHeadingColour();
                return;
            }

            UpdateHoverColour();
        }

        private void UpdateHoverColour()
        {
            Vector3 toButton = transform.position - viewer.position;
            float distance = toButton.magnitude;
            bool inRange = maxWatchDistance <= 0f || distance <= maxWatchDistance;
            IsWatched = inRange && distance > 0.01f &&
                Vector3.Dot(viewer.forward, toButton / distance) >= aimTolerance;

            if (IsWatched != wasWatched)
            {
                wasWatched = IsWatched;
                (IsWatched ? onBecameWatched : onBecameUnwatched).Invoke();
            }

            if (IsWatched)
            {
                hoverTime += Time.deltaTime;
                awayTime = 0f;
            }
            else
            {
                awayTime += Time.deltaTime;
                if (!latchOnceWatched && awayTime >= recoveryDelay)
                    hoverTime = 0f;
            }

            bool changing = hoverTime >= hoverDelay;
            float target = changing ? watchedGreenness : unwatchedGreenness;
            appearance.Greenness = blendDuration <= 0f
                ? target
                : Mathf.MoveTowards(appearance.Greenness, target, Time.deltaTime / blendDuration);
        }

        private void UpdateHeadingColour()
        {
            Vector3 facing = viewer.forward;
            facing.y = 0f;
            Vector3 reference = redHeading;
            reference.y = 0f;
            if (facing.sqrMagnitude < 0.0001f || reference.sqrMagnitude < 0.0001f)
                return;

            // Fully red facing the reference heading, fully green facing away from it.
            float angle = Vector3.Angle(facing.normalized, reference.normalized);
            float amount = Mathf.Clamp01(angle / Mathf.Max(1f, headingSweep));
            appearance.Greenness = invertHeading ? 1f - amount : amount;
        }
    }
}
