using System.Collections.Generic;
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
        private static readonly List<CursorRepellingButton> active = new List<CursorRepellingButton>();
        private ButtonInteractable button;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => active.Clear();

        private void OnEnable()
        {
            if (!active.Contains(this))
                active.Add(this);
        }

        public bool IsPushing { get; private set; }
        public UnityEvent OnStartedPushing => onStartedPushing;

        private void Awake()
        {
            button = GetComponentInChildren<ButtonInteractable>();
            if (stopOnPress && button != null)
                button.OnPressed.AddListener(Stop);
        }

        public void Stop()
        {
            running = false;
            IsPushing = false;
        }

        /// <summary>Called by the player after input look, before its interaction ray.</summary>
        public static void ApplyAll(FirstPersonController controller, Camera camera, float deltaTime)
        {
            // Iterate backwards so a Stop/disable event can remove the current magnet.
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (i >= active.Count)
                    continue;
                var magnet = active[i];
                if (magnet == null)
                    continue;
                if ((magnet.playerRoot != null && magnet.playerRoot != controller.transform) ||
                    (magnet.viewer != null && magnet.viewer != camera))
                    continue;
                Vector2 offset = controller.enabled && controller.HasControl
                    ? magnet.GetLookOffset(camera, deltaTime) : Vector2.zero;
                bool wasPushing = magnet.IsPushing;
                magnet.IsPushing = offset.sqrMagnitude > 0f;
                controller.ApplyLookOffset(offset.x, offset.y);
                if (magnet.IsPushing && !wasPushing)
                    magnet.onStartedPushing.Invoke();
            }
        }

        /// <summary>Yaw/pitch deflection in degrees. Does not alter transforms.</summary>
        public Vector2 GetLookOffset(Camera camera, float deltaTime)
        {
            if (!running || !isActiveAndEnabled || camera == null || deltaTime <= 0f)
                return Vector2.zero;
            Vector3 toButton = transform.position - camera.transform.position;
            float distance = toButton.magnitude;
            if (distance < 0.05f || distance > maxDistance || distance <= safeDistance)
                return Vector2.zero;

            Vector3 local = camera.transform.InverseTransformDirection(toButton / distance);
            if (local.z <= aimTolerance)
                return Vector2.zero;
            float strength = strongerWhenCloser ? Mathf.InverseLerp(aimTolerance, 1f, local.z) : 1f;
            // Positive pitch looks down. Repel vertically as well as horizontally.
            Vector2 away = new Vector2(-local.x, local.y);
            if (away.sqrMagnitude < 0.000001f)
                away = attractInstead ? Vector2.zero : Vector2.right;
            else
                away.Normalize();

            float amount = pushStrength * strength * deltaTime;
            if (attractInstead)
            {
                // Attraction must stop on target rather than overshooting every frame.
                amount = Mathf.Min(amount, Mathf.Acos(Mathf.Clamp(local.z, -1f, 1f)) * Mathf.Rad2Deg);
                away = -away;
            }
            return away * amount + Vector2.down * (verticalPush * strength * deltaTime);
        }

        private void OnDisable()
        {
            active.Remove(this);
            IsPushing = false;
        }

        private void OnDestroy()
        {
            if (button != null)
                button.OnPressed.RemoveListener(Stop);
        }
    }
}
