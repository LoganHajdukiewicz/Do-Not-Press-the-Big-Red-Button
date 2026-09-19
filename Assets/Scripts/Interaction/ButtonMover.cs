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

        [SerializeField] private MoveMode mode = MoveMode.ChasePlayer;
        [SerializeField, Min(0f)] private float speed = 2.2f;

        [Header("Chase")]
        [Tooltip("Stops this far from the player. Small values let it touch them.")]
        [SerializeField, Min(0f)] private float stopDistance = 0.45f;

        [Header("Stay behind")]
        [Tooltip("How far behind the player it tries to sit.")]
        [SerializeField, Min(0.5f)] private float orbitRadius = 3f;
        [SerializeField, Min(0f)] private float orbitSpeed = 130f;

        [Header("Patrol")]
        [SerializeField] private Vector3 patrolOffset = new Vector3(0f, 0f, 6f);
        [SerializeField, Min(0.1f)] private float patrolPause = 0.5f;

        [Tooltip("Stops moving once the button has been pressed.")]
        [SerializeField] private bool stopOnPress = true;
        [Tooltip("Player transform. Found automatically when left empty.")]
        [SerializeField] private Transform player;

        private Vector3 patrolStart;
        private Vector3 patrolEnd;
        private float patrolTimer;
        private bool towardsEnd = true;
        private bool running = true;

        private void Awake()
        {
            patrolStart = transform.position;
            patrolEnd = patrolStart + patrolOffset;

            var button = GetComponentInChildren<ButtonInteractable>();
            if (stopOnPress && button != null)
                button.OnPressed.AddListener(Stop);
        }

        public void Stop() => running = false;

        private void Update()
        {
            if (!running)
                return;

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
            target.y = transform.position.y; // Stay on the floor rather than floating up.
            Vector3 toPlayer = target - transform.position;
            if (toPlayer.magnitude <= stopDistance)
                return;

            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(-toPlayer.normalized, Vector3.up);
        }

        private void UpdateStayBehind()
        {
            Vector3 behind = player.position - player.forward * orbitRadius;
            behind.y = transform.position.y;

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
            destination.y = transform.position.y;
            transform.position = Vector3.MoveTowards(transform.position, destination,
                speed * 2f * Time.deltaTime);

            Vector3 facePlayer = player.position - transform.position;
            facePlayer.y = 0f;
            if (facePlayer.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(facePlayer.normalized, Vector3.up);
        }

        private void UpdatePatrol()
        {
            Vector3 target = towardsEnd ? patrolEnd : patrolStart;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            if (Vector3.Distance(transform.position, target) > 0.01f)
                return;

            patrolTimer += Time.deltaTime;
            if (patrolTimer < patrolPause)
                return;

            patrolTimer = 0f;
            towardsEnd = !towardsEnd;
        }
    }
}
