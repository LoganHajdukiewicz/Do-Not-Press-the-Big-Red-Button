using UnityEngine;
using UnityEngine.Events;

namespace BigRedButton
{
    /// <summary>
    /// Pushes the player's aim away as they try to point at this button.
    /// Day 7: "The Green Button pushes the mouse away magnetically."
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CursorRepellingButton : MonoBehaviour
    {
        [Header("Push")]
        [Tooltip("Degrees per second the view is pushed away at full strength.")]
        [SerializeField, Min(0f)] private float pushStrength = 210f;
        [Tooltip("How close the aim must get before the push begins. Lower reacts sooner.")]
        [SerializeField, Range(0.5f, 0.999f)] private float aimTolerance = 0.94f;
        [Tooltip("Stops pushing beyond this distance, so far rooms are unaffected.")]
        [SerializeField, Min(1f)] private float maxDistance = 12f;
        [Tooltip("Extra push straight up, which throws the aim off the button entirely.")]
        [SerializeField, Min(0f)] private float verticalPush;
        [Tooltip("Off makes the push equally hard everywhere in range, rather than ramping up.")]
        [SerializeField] private bool strongerWhenCloser = true;
        [Tooltip("Reverses the push, pulling the aim towards the button instead.")]
        [SerializeField] private bool attractInstead;

        [Header("Behaviour")]
        [Tooltip("Stops once the button has been pressed.")]
        [SerializeField] private bool stopOnPress = true;
        [Tooltip("Ignores the player while they are within this distance, so it stays pressable.")]
        [SerializeField, Min(0f)] private float safeDistance;

        [Header("References")]
        [Tooltip("Player root. Found automatically when left empty.")]
        [SerializeField] private Transform playerRoot;
        [Tooltip("Player camera. Found automatically when left empty.")]
        [SerializeField] private Camera viewer;

        [Header("Events")]
        [SerializeField] private UnityEvent onStartedPushing = new UnityEvent();

        private bool running = true;
        private bool wasPushing;

        public bool IsPushing { get; private set; }
        public UnityEvent OnStartedPushing => onStartedPushing;

        private void Awake()
        {
            var button = GetComponentInChildren<ButtonInteractable>();
            if (stopOnPress && button != null)
                button.OnPressed.AddListener(Stop);
        }

        public void Stop()
        {
            running = false;
            IsPushing = false;
        }

        private void LateUpdate()
        {
            IsPushing = false;
            if (!running)
                return;

            if (viewer == null)
            {
                viewer = Camera.main;
                if (viewer == null)
                    return;
            }

            if (playerRoot == null)
            {
                var controller = viewer.GetComponentInParent<FirstPersonController>();
                if (controller == null)
                    controller = FindFirstObjectByType<FirstPersonController>();
                if (controller == null)
                    return;
                playerRoot = controller.transform;
            }

            Vector3 toButton = transform.position - viewer.transform.position;
            float distance = toButton.magnitude;
            if (distance < 0.05f || distance > maxDistance || distance <= safeDistance)
                return;

            Vector3 direction = toButton / distance;
            float aim = Vector3.Dot(viewer.transform.forward, direction);
            if (aim < aimTolerance)
                return;

            float closeness = strongerWhenCloser
                ? Mathf.InverseLerp(aimTolerance, 1f, aim) : 1f;
            IsPushing = true;
            if (!wasPushing)
                onStartedPushing.Invoke();
            wasPushing = true;

            Vector3 flatToButton = direction;
            flatToButton.y = 0f;
            if (flatToButton.sqrMagnitude < 0.0001f)
                return;

            // Push around the player's own yaw so the controller keeps its pitch clamp.
            float side = Vector3.Dot(playerRoot.right, flatToButton.normalized);
            float sign = side >= 0f ? -1f : 1f;
            if (attractInstead)
                sign = -sign;

            playerRoot.Rotate(Vector3.up, sign * pushStrength * closeness * Time.deltaTime, Space.Self);

            if (verticalPush <= 0f)
                return;

            // Nudge the camera's pitch as well, within the controller's own limits.
            Transform cameraTransform = viewer.transform;
            float pitch = cameraTransform.localEulerAngles.x;
            if (pitch > 180f)
                pitch -= 360f;
            float nudged = Mathf.Clamp(pitch - verticalPush * closeness * Time.deltaTime, -85f, 85f);
            cameraTransform.localRotation = Quaternion.Euler(nudged, 0f, 0f);
        }

        private void OnDisable() => wasPushing = false;
    }
}
