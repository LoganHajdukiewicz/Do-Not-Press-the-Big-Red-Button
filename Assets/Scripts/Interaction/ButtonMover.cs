using UnityEngine;

namespace BigRedButton
{
    /// <summary>
    /// Moves a button around the room. Covers the moving-button days:
    /// Day 20: "Red Buttons will follow you around. They will want to be pressed."
    /// Day 21: the green button stays behind the player, so they must turn quickly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ButtonMover : MonoBehaviour
    {
        public enum MoveMode
        {
            /// <summary>Walks towards the player and presses against them.</summary>
            ChasePlayer,
            /// <summary>Circles the player to stay behind their back.</summary>
            StayBehindPlayer,
            /// <summary>Slides back and forth between two points.</summary>
            Patrol
        }

        [Header("Movement")]
        [SerializeField] private MoveMode mode = MoveMode.ChasePlayer;
        [SerializeField, Min(0f)] private float speed = 2.2f;
        [Tooltip("Seconds before it starts moving, after the day begins.")]
        [SerializeField, Min(0f)] private float startDelay;
        [Tooltip("Turns to face the player as it moves.")]
        [SerializeField] private bool faceThePlayer = true;

        [Header("Chase")]
        [Tooltip("Stops this far from the player. Small values let it touch them.")]
        [SerializeField, Min(0f)] private float stopDistance = 0.45f;
        [Tooltip("Gives up beyond this distance. 0 means it never gives up.")]
        [SerializeField, Min(0f)] private float loseInterestDistance;
        [Tooltip("Speed multiplier once it is close, so it closes in for the press.")]
        [SerializeField, Range(0.5f, 3f)] private float closeInSpeedMultiplier = 1f;

        [Header("Stay behind")]
        [Tooltip("How far behind the player it tries to sit.")]
        [SerializeField, Min(0.5f)] private float orbitRadius = 3f;
        [Tooltip("Degrees per second it circles the player.")]
        [SerializeField, Min(0f)] private float orbitSpeed = 130f;
        [Tooltip("Speed multiplier while circling, relative to Speed.")]
        [SerializeField, Range(0.5f, 4f)] private float orbitSpeedMultiplier = 2f;

        [Header("Patrol")]
        [SerializeField] private Vector3 patrolOffset = new Vector3(0f, 0f, 6f);
        [Tooltip("Seconds it waits at each end of the route.")]
        [SerializeField, Min(0f)] private float patrolPause = 0.5f;
        [Tooltip("Treats the offset as world space rather than relative to its rotation.")]
        [SerializeField] private bool patrolInWorldSpace = true;

        [Header("Behaviour")]
        [Tooltip("Stops moving once the button has been pressed.")]
        [SerializeField] private bool stopOnPress = true;
        [Tooltip("Keeps its starting height, so it slides rather than floats up stairs.")]
        [SerializeField] private bool lockHeight = true;
        [Tooltip("Player transform. Found automatically when left empty.")]
        [SerializeField] private Transform player;

        [Header("Events")]
        [SerializeField] private UnityEngine.Events.UnityEvent onReachedPlayer =
            new UnityEngine.Events.UnityEvent();

        private Vector3 patrolStart;
        private Vector3 patrolEnd;
        private float patrolTimer;
        private float delayTimer;
        private float startHeight;
        private bool towardsEnd = true;
        private bool running = true;
        private bool announcedArrival;

        public bool IsMoving { get; private set; }
        public UnityEngine.Events.UnityEvent OnReachedPlayer => onReachedPlayer;

        private void Awake()
        {
            patrolStart = transform.position;
            patrolEnd = patrolStart + (patrolInWorldSpace
                ? patrolOffset : transform.TransformVector(patrolOffset));
            startHeight = transform.position.y;

            var button = GetComponentInChildren<ButtonInteractable>();
            if (stopOnPress && button != null)
                button.OnPressed.AddListener(Stop);
        }

        public void Stop() => running = false;

        private void Update()
        {
            IsMoving = false;
            if (!running)
                return;

            if (delayTimer < startDelay)
            {
                delayTimer += Time.deltaTime;
                return;
            }

            if (mode == MoveMode.Patrol)
            {
                UpdatePatrol();
                return;
            }

            if (player == null)
            {
                var controller = FindFirstObjectByType<FirstPersonController>();
                if (controller == null)
                    return;
                player = controller.transform;
            }

            if (mode == MoveMode.ChasePlayer)
                UpdateChase();
            else
                UpdateStayBehind();
        }

        private void UpdateChase()
        {
            Vector3 target = player.position;
            if (lockHeight)
                target.y = startHeight; // Slide along the floor rather than floating up.
            Vector3 toPlayer = target - transform.position;
            float distance = toPlayer.magnitude;

            if (loseInterestDistance > 0f && distance > loseInterestDistance)
                return;

            if (distance <= stopDistance)
            {
                if (!announcedArrival)
                {
                    announcedArrival = true;
                    onReachedPlayer.Invoke();
                }

                return;
            }

            announcedArrival = false;
            float currentSpeed = distance <= stopDistance * 3f
                ? speed * closeInSpeedMultiplier : speed;
            transform.position = Vector3.MoveTowards(transform.position, target,
                currentSpeed * Time.deltaTime);
            IsMoving = true;

            if (faceThePlayer && toPlayer.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(-toPlayer.normalized, Vector3.up);
        }

        private void UpdateStayBehind()
        {
            Vector3 behind = player.position - player.forward * orbitRadius;
            behind.y = lockHeight ? startHeight : transform.position.y;

            // Rotate around the player towards the spot behind them, so it slips out of view.
            Vector3 fromPlayer = transform.position - player.position;
            fromPlayer.y = 0f;
            Vector3 wanted = behind - player.position;
            if (fromPlayer.sqrMagnitude < 0.0001f || wanted.sqrMagnitude < 0.0001f)
                return;

            Quaternion step = Quaternion.RotateTowards(
                Quaternion.LookRotation(fromPlayer.normalized, Vector3.up),
                Quaternion.LookRotation(wanted.normalized, Vector3.up),
                orbitSpeed * Time.deltaTime);

            Vector3 offset = step * Vector3.forward * orbitRadius;
            Vector3 destination = player.position + offset;
            destination.y = lockHeight ? startHeight : transform.position.y;
            transform.position = Vector3.MoveTowards(transform.position, destination,
                speed * orbitSpeedMultiplier * Time.deltaTime);
            IsMoving = true;

            if (!faceThePlayer)
                return;
            Vector3 facePlayer = player.position - transform.position;
            facePlayer.y = 0f;
            if (facePlayer.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(facePlayer.normalized, Vector3.up);
        }

        private void UpdatePatrol()
        {
            Vector3 target = towardsEnd ? patrolEnd : patrolStart;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            IsMoving = true;
            if (Vector3.Distance(transform.position, target) > 0.01f)
                return;

            IsMoving = false;

            patrolTimer += Time.deltaTime;
            if (patrolTimer < patrolPause)
                return;

            patrolTimer = 0f;
            towardsEnd = !towardsEnd;
        }
    }
}
