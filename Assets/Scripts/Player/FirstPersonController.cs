using UnityEngine;
using UnityEngine.InputSystem;

namespace BigRedButton
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController), typeof(PlayerInteractor))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Camera playerCamera;

        [Header("Movement (metres / seconds)")]
        [SerializeField, Min(0f)] private float walkSpeed = 4f;
        [SerializeField, Min(0f)] private float runSpeed = 7f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -20f;
        [SerializeField, Min(1f)] private float terminalSpeed = 50f;

        [Header("Look")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.1f;
        [SerializeField, Min(0f)] private float stickSensitivity = 150f;
        [SerializeField, Range(1f, 89f)] private float pitchLimit = 85f;
        [SerializeField] private bool invertY;

        private CharacterController character;
        private PlayerInteractor interactor;
        private PlayerButtonContact buttonContact;
        private InputActionAsset ownedActions;
        private InputAction move;
        private InputAction look;
        private InputAction jump;
        private InputAction sprint;
        private InputAction interact;
        private float verticalSpeed;
        private float pitch;
        private bool captured;
        private bool hasFocus = true;

        public bool HasControl => captured && hasFocus && Cursor.lockState == CursorLockMode.Locked;
        public bool UsingGamepad { get; private set; }
        public bool IsGrounded => character != null && character.isGrounded;
        public bool IsRunning { get; private set; }
        public string InteractionHint => UsingGamepad ? "Y / Triangle" : "E";

        private void Awake()
        {
            character = GetComponent<CharacterController>();
            interactor = GetComponent<PlayerInteractor>();
            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>();

            if (inputActions == null || playerCamera == null ||
                playerCamera.transform == transform || !playerCamera.transform.IsChildOf(transform))
            {
                Debug.LogError("FirstPersonController needs an input action asset and a child camera.", this);
                enabled = false;
                return;
            }

            // A moved Scene-view camera is easy to leave tilted, scaled, or inside
            // geometry. The first-person rig is always a 1.6m child camera; restoring
            // it here prevents that accidental edit from becoming a wobbly Play Mode view.
            RestoreCameraRig();

            // Own the enabled state; never disable the project's shared input asset.
            ownedActions = Instantiate(inputActions);
            move = ownedActions.FindAction("Player/Move");
            look = ownedActions.FindAction("Player/Look");
            jump = ownedActions.FindAction("Player/Jump");
            sprint = ownedActions.FindAction("Player/Sprint");
            interact = ownedActions.FindAction("Player/Interact");
            if (move == null || look == null || jump == null || sprint == null || interact == null)
            {
                Debug.LogError("The Player map needs Move, Look, Jump, Sprint and Interact actions.", this);
                enabled = false;
                return;
            }

            pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, playerCamera.transform.localEulerAngles.x),
                -pitchLimit, pitchLimit);
            interactor.SetCamera(playerCamera);

            // Existing scenes/prefabs gain collision interaction without being rebuilt.
            buttonContact = GetComponent<PlayerButtonContact>();
            if (buttonContact == null)
                buttonContact = gameObject.AddComponent<PlayerButtonContact>();
            buttonContact.enabled = enabled;
        }

        private void RestoreCameraRig()
        {
            Transform rig = playerCamera.transform;
            rig.localPosition = new Vector3(0f, 1.6f, 0f);
            rig.localRotation = Quaternion.identity;
            rig.localScale = Vector3.one;
            playerCamera.nearClipPlane = 0.03f;
            playerCamera.fieldOfView = 75f;
            playerCamera.clearFlags = CameraClearFlags.SolidColor;
            playerCamera.backgroundColor = new Color(0.12f, 0.17f, 0.23f);
        }

        private void OnEnable()
        {
            if (move == null || look == null || jump == null || sprint == null || interact == null)
                return;
            move.actionMap.Enable();
            if (buttonContact != null)
                buttonContact.enabled = true;
            SetCursorCaptured(true);
        }

        private void OnDisable()
        {
            if (ownedActions != null)
                ownedActions.Disable();
            if (buttonContact != null)
                buttonContact.enabled = false;
            verticalSpeed = 0f;
            IsRunning = false;
            SetCursorCaptured(false);
        }

        private void OnDestroy()
        {
            if (ownedActions != null)
                Destroy(ownedActions);
        }

        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            if (!focused)
                SetCursorCaptured(false);
        }

        public void SetCursorCaptured(bool value)
        {
            captured = value;
            Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !value;
            if (!value && interactor != null)
                interactor.ClearFocus();
        }

        private void Update()
        {
            if (!hasFocus)
                return;

            bool toggleCapture = Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                Gamepad.current?.startButton.wasPressedThisFrame == true;
            bool resume = !HasControl && Mouse.current?.leftButton.wasPressedThisFrame == true;
            if (toggleCapture || resume)
            {
                SetCursorCaptured(resume || !HasControl);
                return; // A click used to capture the cursor never interacts.
            }

            if (Time.timeScale <= 0f || !character.enabled)
            {
                IsRunning = false;
                interactor.ClearFocus();
                return;
            }

            if (HasControl)
            {
                UpdateInputDevice();
                UpdateLook();
            }
            else
                interactor.ClearFocus();

            // Deflect the actual controller look before movement and the interaction ray.
            // LateUpdate camera edits were too late for E and lost pitch on the next frame.
            CursorRepellingButton.ApplyAll(this, playerCamera, Time.deltaTime);
            UpdateMovement();
            if (!HasControl)
                return;

            interactor.RefreshFocus();
            if (interact.WasPerformedThisFrame())
                interactor.TryInteract();
        }

        private void UpdateInputDevice()
        {
            if (look.ReadValue<Vector2>().sqrMagnitude > 0.001f)
                UsingGamepad = look.activeControl?.device is Gamepad;
            else if (move.ReadValue<Vector2>().sqrMagnitude > 0.001f)
                UsingGamepad = move.activeControl?.device is Gamepad;
            if (interact.WasPerformedThisFrame())
                UsingGamepad = interact.activeControl?.device is Gamepad;
        }

        private void UpdateLook()
        {
            Vector2 delta = look.ReadValue<Vector2>();
            // Mouse delta already represents this frame's displacement. Sticks are rates.
            float scale = look.activeControl?.device is Pointer
                ? mouseSensitivity : stickSensitivity * Time.deltaTime;
            ApplyLookOffset(delta.x * scale, delta.y * scale * (invertY ? 1f : -1f));
        }

        /// <summary>Applies yaw and pitch in degrees, retaining the controller's pitch clamp.</summary>
        public void ApplyLookOffset(float yawDegrees, float pitchDegrees)
        {
            if (playerCamera == null)
                return;
            transform.Rotate(Vector3.up, yawDegrees, Space.Self);
            pitch = Mathf.Clamp(pitch + pitchDegrees, -pitchLimit, pitchLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void UpdateMovement()
        {
            Vector2 input = HasControl ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            IsRunning = HasControl && sprint.IsPressed() && input.sqrMagnitude > 0.001f;
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) *
                (IsRunning ? runSpeed : walkSpeed);

            if (character.isGrounded && verticalSpeed <= 0f)
            {
                verticalSpeed = -2f;
                if (HasControl && jump.WasPerformedThisFrame())
                    verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalSpeed = Mathf.Max(verticalSpeed + gravity * Time.deltaTime, -terminalSpeed);
            CollisionFlags collisions = character.Move((horizontal + Vector3.up * verticalSpeed) * Time.deltaTime);
            if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
                verticalSpeed = 0f;
            if ((collisions & CollisionFlags.Below) != 0 && verticalSpeed < 0f)
                verticalSpeed = -2f;
        }

        private void OnValidate()
        {
            walkSpeed = Mathf.Max(0f, walkSpeed);
            runSpeed = Mathf.Max(walkSpeed, runSpeed);
            jumpHeight = Mathf.Max(0f, jumpHeight);
            gravity = Mathf.Min(-0.01f, gravity);
            terminalSpeed = Mathf.Max(1f, terminalSpeed);
        }
    }
}
