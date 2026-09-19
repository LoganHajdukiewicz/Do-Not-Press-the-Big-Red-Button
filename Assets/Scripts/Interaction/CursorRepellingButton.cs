using UnityEngine;

namespace BigRedButton
{
    /// <summary>
    /// Pushes the player's aim away as they try to point at this button.
    /// Day 7: "The Green Button pushes the mouse away magnetically."
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CursorRepellingButton : MonoBehaviour
    {
        [Tooltip("Degrees per second the view is pushed away at full strength.")]
        [SerializeField, Min(0f)] private float pushStrength = 70f;
        [Tooltip("How close the aim must get before the push begins.")]
        [SerializeField, Range(0.8f, 0.999f)] private float aimTolerance = 0.94f;
        [Tooltip("Stops pushing beyond this distance, so far rooms are unaffected.")]
        [SerializeField, Min(1f)] private float maxDistance = 12f;
        [Tooltip("Stops once the button has been pressed.")]
        [SerializeField] private bool stopOnPress = true;

        private Transform playerRoot;
        private Camera viewer;
        private bool running = true;

        public bool IsPushing { get; private set; }

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
                    return;
                playerRoot = controller.transform;
            }

            Vector3 toButton = transform.position - viewer.transform.position;
            float distance = toButton.magnitude;
            if (distance < 0.05f || distance > maxDistance)
                return;

            Vector3 direction = toButton / distance;
            float aim = Vector3.Dot(viewer.transform.forward, direction);
            if (aim < aimTolerance)
                return;

            // The closer the aim gets, the harder it is pushed off target.
            float closeness = Mathf.InverseLerp(aimTolerance, 1f, aim);
            IsPushing = true;

            Vector3 flatToButton = direction;
            flatToButton.y = 0f;
            if (flatToButton.sqrMagnitude < 0.0001f)
                return;

            // Push around the player's own yaw so the controller keeps its pitch clamp.
            float side = Vector3.Dot(playerRoot.right, flatToButton.normalized);
            float sign = side >= 0f ? -1f : 1f;
            playerRoot.Rotate(Vector3.up, sign * pushStrength * closeness * Time.deltaTime, Space.Self);
        }
    }
}
