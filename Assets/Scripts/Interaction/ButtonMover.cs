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
            Patrol,
            /// <summary>Travels one fixed straight route, then stops at its end.</summary>
            OneWayPath
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

        [Header("Wall collision (stay behind)")]
        [Tooltip("Sweep the whole assembly against walls. It can slide along walls, but cannot pass through corners.")]
        [SerializeField] private bool collideWithWalls = true;
        [SerializeField] private LayerMask wallLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.001f)] private float wallSkin = 0.03f;

        [Header("Patrol")]
        [SerializeField] private Vector3 patrolOffset = new Vector3(0f, 0f, 6f);
        [Tooltip("Seconds it waits at each end of the route.")]
        [SerializeField, Min(0f)] private float patrolPause = 0.5f;
        [Tooltip("Treats the offset as world space rather than relative to its rotation.")]
        [SerializeField] private bool patrolInWorldSpace = true;

        [Header("One-way path")]
        [Tooltip("Fixed displacement travelled in a straight line before stopping.")]
        [SerializeField] private Vector3 oneWayPathOffset = new Vector3(0f, 0f, -6f);
        [Tooltip("Treats the one-way path offset as world space.")]
        [SerializeField] private bool oneWayPathInWorldSpace = true;

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
        private Vector3 oneWayEnd;
        private float patrolTimer;
        private float delayTimer;
        private float startHeight;
        private bool towardsEnd = true;
        private bool running = true;
        private bool announcedArrival;
        private ButtonInteractable button;
        private bool hasCollisionShape;
        private Vector3 collisionCentre;
        private float collisionRadius;
        private float collisionHeight;

        public bool IsBlockedByWall { get; private set; }

        public bool IsMoving { get; private set; }
        public UnityEngine.Events.UnityEvent OnReachedPlayer => onReachedPlayer;

        private void Awake()
        {
            patrolStart = transform.position;
            patrolEnd = patrolStart + (patrolInWorldSpace
                ? patrolOffset : transform.TransformVector(patrolOffset));
            oneWayEnd = patrolStart + (oneWayPathInWorldSpace
                ? oneWayPathOffset : transform.TransformVector(oneWayPathOffset));
            startHeight = transform.position.y;

            CacheCollisionShape();
            button = GetComponentInChildren<ButtonInteractable>();
            if (stopOnPress && button != null)
                button.OnPressed.AddListener(Stop);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.OnPressed.RemoveListener(Stop);
        }

        public void Stop()
        {
            running = false;
            IsMoving = false;
        }

        private void Update()
        {
            IsMoving = false;
            IsBlockedByWall = false;
            if (!running || Time.timeScale <= 0f)
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

            if (mode == MoveMode.OneWayPath)
            {
                UpdateOneWayPath();
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
            MoveWithWalls(Vector3.MoveTowards(transform.position, destination,
                speed * orbitSpeedMultiplier * Time.deltaTime));

            if (!faceThePlayer)
                return;
            Vector3 facePlayer = player.position - transform.position;
            facePlayer.y = 0f;
            if (facePlayer.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(facePlayer.normalized, Vector3.up);
        }

        private void CacheCollisionShape()
        {
            Physics.SyncTransforms();
            Bounds bounds = default;
            hasCollisionShape = false;
            foreach (Collider part in GetComponentsInChildren<Collider>())
            {
                if (!part.enabled || part.isTrigger)
                    continue;
                if (!hasCollisionShape)
                    bounds = part.bounds;
                else
                    bounds.Encapsulate(part.bounds);
                hasCollisionShape = true;
            }
            if (!hasCollisionShape)
                return;

            // Conservative upright box covers pedestal, housing and cap, including
            // their corners at every yaw. Its bottom is raised slightly off the floor.
            collisionRadius = Mathf.Max(0.05f,
                new Vector2(bounds.extents.x, bounds.extents.z).magnitude);
            collisionHeight = Mathf.Max(0.05f, bounds.size.y - wallSkin * 2f);
            Vector3 centre = new Vector3(bounds.center.x,
                bounds.min.y + wallSkin + collisionHeight * 0.5f, bounds.center.z);
            collisionCentre = transform.InverseTransformPoint(centre);
        }

        private void MoveWithWalls(Vector3 destination)
        {
            Vector3 original = transform.position;
            IsBlockedByWall = false;
            if (!collideWithWalls || !hasCollisionShape)
            {
                transform.position = destination;
                IsMoving = (destination - original).sqrMagnitude > 0.000001f;
                return;
            }

            Physics.SyncTransforms();
            Vector3 position = original;
            Vector3 remaining = destination - original;
            Vector3 centreOffset = transform.TransformPoint(collisionCentre) - original;
            Vector3 halfExtents = new Vector3(collisionRadius, collisionHeight * 0.5f, collisionRadius);
            // A sweep, then up to two wall slides. No teleporting to the desired orbit
            // position: when both walls block a corner, the button stays there.
            for (int slide = 0; slide < 3 && remaining.sqrMagnitude > 0.000001f; slide++)
            {
                float distance = remaining.magnitude;
                Vector3 direction = remaining / distance;
                Vector3 centre = position + centreOffset;
                RaycastHit nearest = default;
                float nearestDistance = distance + wallSkin;
                foreach (RaycastHit hit in Physics.BoxCastAll(centre, halfExtents,
                    direction, Quaternion.identity, distance + wallSkin, wallLayers,
                    QueryTriggerInteraction.Ignore))
                {
                    Collider obstacle = hit.collider;
                    if (obstacle == null || obstacle.transform.IsChildOf(transform) ||
                        (player != null && obstacle.transform.IsChildOf(player)) ||
                        obstacle.GetComponentInParent<PlayerInteractor>() != null ||
                        Physics.GetIgnoreLayerCollision(gameObject.layer, obstacle.gameObject.layer) ||
                        Vector3.Dot(hit.normal, direction) >= -0.0001f)
                        continue;
                    if (hit.distance <= nearestDistance)
                    {
                        nearest = hit;
                        nearestDistance = hit.distance;
                    }
                }
                if (nearest.collider == null)
                {
                    position += remaining;
                    break;
                }
                IsBlockedByWall = true;
                float travel = Mathf.Clamp(nearestDistance - wallSkin, 0f, distance);
                position += direction * travel;
                remaining = Vector3.ProjectOnPlane(remaining - direction * travel, nearest.normal);
                if (lockHeight)
                    remaining.y = 0f;
            }
            transform.position = position;
            IsMoving = (position - original).sqrMagnitude > 0.000001f;
        }

        private void UpdateOneWayPath()
        {
            Vector3 original = transform.position;
            transform.position = Vector3.MoveTowards(original, oneWayEnd, speed * Time.deltaTime);
            IsMoving = (transform.position - original).sqrMagnitude > 0.000001f;
            if (Vector3.Distance(transform.position, oneWayEnd) <= 0.01f)
                running = false;
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
