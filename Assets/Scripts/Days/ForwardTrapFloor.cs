using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace BigRedButton
{
    /// <summary>Day 15: the floor deletes itself on W, not on proximity to a doorway.</summary>
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    public sealed class ForwardTrapFloor : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private FirstPersonController player;
        [Tooltip("W springs the trap.")]
        [SerializeField] private bool wKeyTriggers = true;
        [Tooltip("The Up arrow springs the trap too, since it also walks forward.")]
        [SerializeField] private bool upArrowTriggers = true;
        [Tooltip("Gamepad forward movement also springs the trap.")]
        [SerializeField] private bool gamepadForwardAlsoTriggers = true;
        [SerializeField, Range(0.1f, 1f)] private float forwardThreshold = 0.5f;
        [Tooltip("Completing the day disarms the floor during the transition.")]
        [SerializeField] private ButtonInteractable safeButton;

        [Header("Sound and events")]
        [SerializeField] private AudioClip collapseClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.9f;
        [SerializeField] private UnityEvent onCollapsed = new UnityEvent();

        private bool disarmed;
        private bool forwardWasHeld = true; // Require a fresh stick push after scene load.
        public bool HasCollapsed { get; private set; }
        public UnityEvent OnCollapsed => onCollapsed;

        private void Start()
        {
            if (player == null)
                player = FindFirstObjectByType<FirstPersonController>();
            if (safeButton != null)
                safeButton.OnPressed.AddListener(Disarm);
        }

        private void Update()
        {
            bool forward = gamepadForwardAlsoTriggers && Gamepad.current != null &&
                Gamepad.current.leftStick.ReadValue().y >= forwardThreshold;
            bool playable = player != null && player.isActiveAndEnabled && player.HasControl &&
                Time.timeScale > 0f;
            Keyboard keys = Keyboard.current;
            // Either forward key springs it, so arrow-key players are not exempt.
            bool keyPressed = keys != null &&
                ((wKeyTriggers && keys.wKey.wasPressedThisFrame) ||
                    (upArrowTriggers && keys.upArrowKey.wasPressedThisFrame));
            ProcessInput(keyPressed, forward, playable);
        }

        /// <summary>Explicit edge handling also keeps pause/resume from consuming held input.</summary>
        public void ProcessInput(bool forwardKeyPressed, bool forwardHeld, bool hasControl)
        {
            bool freshForward = forwardHeld && !forwardWasHeld;
            forwardWasHeld = forwardHeld;
            if (hasControl && !disarmed && (forwardKeyPressed || freshForward))
                Collapse();
        }

        public void Disarm() => disarmed = true;

        public void Collapse()
        {
            if (HasCollapsed || disarmed)
                return;
            HasCollapsed = true;
            // Destroy is deferred. Remove collisions immediately so this frame can fall.
            foreach (Collider surface in GetComponentsInChildren<Collider>())
                surface.enabled = false;
            foreach (Renderer surface in GetComponentsInChildren<Renderer>())
                surface.enabled = false;
            if (collapseClip != null)
                AudioSource.PlayClipAtPoint(collapseClip, transform.position, volume);
            onCollapsed.Invoke();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (safeButton != null)
                safeButton.OnPressed.RemoveListener(Disarm);
        }
    }
}
