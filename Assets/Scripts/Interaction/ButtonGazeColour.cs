using UnityEngine;

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

        [SerializeField] private GazeMode mode = GazeMode.TurnsRedWhenWatched;
        [Tooltip("Seconds of looking at it before the colour starts changing.")]
        [SerializeField, Min(0f)] private float hoverDelay = 0.5f;
        [Tooltip("Seconds the colour change takes once it begins.")]
        [SerializeField, Min(0f)] private float blendDuration = 0.25f;
        [Tooltip("How closely the player must be aiming at it to count as watching.")]
        [SerializeField, Range(0.8f, 0.999f)] private float aimTolerance = 0.985f;

        [Header("Heading mode")]
        [Tooltip("The heading where the button looks fully red.")]
        [SerializeField] private Vector3 redHeading = Vector3.forward;

        [Tooltip("Player camera. Found automatically when left empty.")]
        [SerializeField] private Transform viewer;

        private ButtonAppearance appearance;
        private float hoverTime;

        public bool IsWatched { get; private set; }

        private void Awake()
        {
            appearance = GetComponent<ButtonAppearance>();
            appearance.Greenness = mode == GazeMode.TurnsRedWhenWatched ? 1f : 0f;
        }

        private void Update()
        {
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
            IsWatched = toButton.sqrMagnitude > 0.0001f &&
                Vector3.Dot(viewer.forward, toButton.normalized) >= aimTolerance;

            hoverTime = IsWatched ? hoverTime + Time.deltaTime : 0f;
            // Looking away lets it recover, so the player can try again.
            float target = IsWatched && hoverTime >= hoverDelay ? 0f : 1f;
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

            // Fully red facing the reference heading, fully green facing the opposite way.
            float angle = Vector3.Angle(facing.normalized, reference.normalized);
            appearance.Greenness = Mathf.Clamp01(angle / 180f);
        }
    }
}
