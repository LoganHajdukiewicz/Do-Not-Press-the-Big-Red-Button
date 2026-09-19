using UnityEngine;
using UnityEngine.Events;

namespace BigRedButton
{
    /// <summary>
    /// Opens a trapdoor when the player walks into the area above it.
    /// Day 15: stepping towards the green button drops the player onto a big red button.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrapdoorTrigger : MonoBehaviour
    {
        [Header("Trapdoor")]
        [Tooltip("The floor panel that disappears. Usually a plain box.")]
        [SerializeField] private GameObject lid;
        [Tooltip("Seconds between the player entering and the floor giving way.")]
        [SerializeField, Min(0f)] private float delay = 0.15f;
        [Tooltip("Closes again after this many seconds. 0 leaves it open.")]
        [SerializeField, Min(0f)] private float closeAfter;

        [Header("Trigger area")]
        [Tooltip("Centre of the area that opens the trapdoor, in world space.")]
        [SerializeField] private Vector3 triggerCentre;
        [Tooltip("How far from the centre counts as standing on it.")]
        [SerializeField, Min(0.2f)] private float triggerRadius = 2f;
        [Tooltip("Ignores the player while they are above this height, so falling in is safe.")]
        [SerializeField] private float maxTriggerHeight = 1.5f;

        [Header("Behaviour")]
        [Tooltip("Only springs once. Uncheck to let it reset every time.")]
        [SerializeField] private bool oneShot = true;
        [Tooltip("Optional object whose press disarms the trap, such as the safe button.")]
        [SerializeField] private GameObject safeButton;

        [Header("Sound")]
        [SerializeField] private AudioClip openClip;
        [SerializeField, Range(0f, 1f)] private float openVolume = 0.9f;

        [Header("Events")]
        [SerializeField] private UnityEvent onOpened = new UnityEvent();

        private Transform player;
        private float timer;
        private float openTime;
        private bool arming;
        private bool opened;
        private bool disarmed;

        public bool IsOpen => opened;
        public UnityEvent OnOpened => onOpened;

        private void Awake()
        {
            if (safeButton == null)
                return;

            // Pressing the safe button means the player never needed to walk into the trap.
            var button = safeButton.GetComponentInChildren<ButtonInteractable>();
            if (button != null)
                button.Pressed += Disarm;
        }

        private void OnDestroy()
        {
            if (safeButton == null)
                return;
            var button = safeButton.GetComponentInChildren<ButtonInteractable>();
            if (button != null)
                button.Pressed -= Disarm;
        }

        /// <summary>Stops the trap from springing at all.</summary>
        public void Disarm() => disarmed = true;

        /// <summary>Opens the trapdoor now, for example from another event.</summary>
        public void Open()
        {
            if (opened || lid == null)
                return;

            opened = true;
            openTime = Time.time;
            lid.SetActive(false);

            if (openClip != null)
                AudioSource.PlayClipAtPoint(openClip, lid.transform.position, openVolume);
            onOpened.Invoke();
        }

        public void Close()
        {
            if (lid == null)
                return;

            opened = false;
            arming = false;
            timer = 0f;
            lid.SetActive(true);
        }

        private void Update()
        {
            if (opened)
            {
                if (closeAfter > 0f && Time.time - openTime >= closeAfter)
                    Close();
                return;
            }

            if (disarmed || lid == null)
                return;

            if (player == null)
            {
                var controller = FindFirstObjectByType<FirstPersonController>();
                if (controller == null)
                    return;
                player = controller.transform;
            }

            Vector3 offset = player.position - triggerCentre;
            float height = offset.y;
            offset.y = 0f;
            bool standingOnIt = offset.magnitude <= triggerRadius &&
                height >= -0.5f && height <= maxTriggerHeight;

            if (!standingOnIt)
            {
                if (!oneShot)
                {
                    arming = false;
                    timer = 0f;
                }

                return;
            }

            arming = true;
            timer += Time.deltaTime;
            if (timer >= delay)
                Open();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = arming ? Color.yellow : new Color(1f, 0.4f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(triggerCentre, triggerRadius);
        }
    }
}
