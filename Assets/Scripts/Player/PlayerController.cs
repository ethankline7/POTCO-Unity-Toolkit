using UnityEngine;
using System.Collections.Generic;

namespace Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 16.6f;
        [SerializeField] private float runSpeed = 23f;
        [SerializeField] private float walkBackSpeed = 5.32f;
        [SerializeField] private float runBackSpeed = 5.02f;

        [Header("Acceleration")]
        [SerializeField] private float acceleration = 44.35f;
        [SerializeField] private float deceleration = 64.5f;

        [Header("Jump & Gravity")]
        [SerializeField] private float gravity = 20.0f;
        [SerializeField] private float jumpVelocity = 10.0f;
        [SerializeField] private float coyoteTime = 0.15f;
        [Tooltip("Downward force to keep player glued to slopes")]
        [SerializeField] private float stickToGroundForce = 10f;

        [Header("Collision Setup")]
        [Tooltip("Ground check transform for platform detection")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundDistance = 0.4f;
        [SerializeField] private LayerMask groundMask = -1;
        [Tooltip("Maximum height of obstacles the character can step over")]
        [SerializeField] private float stepOffset = 3.73f;
        [Tooltip("Skin width for collision detection (prevents jittering)")]
        [SerializeField] private float skinWidth = 0.08f;
        [Tooltip("Minimum falling velocity to trigger air/falling state")]
        [SerializeField] private float fallingThreshold = 0.5f;
        [Tooltip("Max slope angle (degrees) the player can walk up")]
        [SerializeField] private float maxSlopeAngle = 85f;

        [Header("Model Setup")]
        [Tooltip("POTCO models often face backwards. Set to 180 to flip model facing. Applied once at start.")]
        [SerializeField] private float modelRotationOffset = 180f;
        [Tooltip("If true, automatically apply rotation offset directly to all child objects")]
        [SerializeField] private bool autoSetupModelHierarchy = true;

        [Header("Rotation")]
        [Tooltip("Rotation speed when character aligns with camera (free-look mode)")]
        [SerializeField] private float strafeRotationSpeed = 12f;
        [Tooltip("Rotation speed when character turns with A/D keys (normal mode)")]
        [SerializeField] private float turnRotationSpeed = 120f;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;

        private CharacterController controller;
        private PlayerCamera playerCamera;
        private Vector3 velocity;
        private Vector3 moveDirection;
        private float currentSpeed;
        private bool isGrounded;
        private float lastGroundedTime;
        private float lastJumpTime;

        // Input
        private Vector2 moveInput;
        private bool jumpPressed;
        private bool runToggle = true;
        private float turnInput;
        private float strafeInput;

        // Moving platform support
        private Transform currentPlatform;
        private Vector3 lastPlatformPosition;
        private Quaternion lastPlatformRotation;
        
        // Slope handling
        private Vector3 groundNormal = Vector3.up;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            // Configure CharacterController settings
            controller.stepOffset = stepOffset;
            controller.skinWidth = skinWidth;
            // Small epsilon to prevent micro-stutter/getting stuck
            controller.minMoveDistance = 0.001f; 
            controller.slopeLimit = maxSlopeAngle;
            controller.enabled = true;

            // Auto-attach dependencies if missing
            if (GetComponent<POTCO.VisZones.VisZoneSensor>() == null)
                gameObject.AddComponent<POTCO.VisZones.VisZoneSensor>();

            if (GetComponent<POTCO.HideLevelGeometry>() == null)
                gameObject.AddComponent<POTCO.HideLevelGeometry>();
        }

        private void Start()
        {
            playerCamera = Camera.main?.GetComponent<PlayerCamera>();
            
            POTCO.HideLevelGeometry hideLevelGeo = GetComponent<POTCO.HideLevelGeometry>();
            if (hideLevelGeo != null) hideLevelGeo.HideObjects();

            if (autoSetupModelHierarchy && Mathf.Abs(modelRotationOffset) > 0.1f)
                SetupModelHierarchy();

            SetupGroundCheck();

            if (GetComponent<FreeCameraToggle>() == null)
                gameObject.AddComponent<FreeCameraToggle>();
        }

        private void Update()
        {
            ProcessInput();
            UpdateGroundState();
            ProcessMovement();
        }

        private void LateUpdate()
        {
            HandleMovingPlatform();
        }

        private void ProcessInput()
        {
            moveInput.x = Input.GetAxisRaw("Horizontal");
            moveInput.y = Input.GetAxisRaw("Vertical");

            strafeInput = 0f;
            if (Input.GetKey(KeyCode.Q)) strafeInput = -1f;
            else if (Input.GetKey(KeyCode.E)) strafeInput = 1f;

            if (Input.GetButtonDown("Jump")) jumpPressed = true;
            if (Input.GetKeyDown(KeyCode.LeftShift)) runToggle = !runToggle;
        }

        private void UpdateGroundState()
        {
            // Disable ground check briefly after jumping so we don't snap back to ground
            if (Time.time - lastJumpTime < 0.2f)
            {
                isGrounded = false;
                groundNormal = Vector3.up;
                return;
            }

            // Use SphereCast for better ground detection on slopes and edges
            float radius = controller.radius * 0.9f;
            float dist = (controller.height / 2f) - radius + groundDistance;
            
            groundNormal = Vector3.up;

            if (Physics.SphereCast(transform.position + controller.center, radius, Vector3.down, out RaycastHit hit, dist, groundMask))
            {
                groundNormal = hit.normal;
                float slopeAngle = Vector3.Angle(Vector3.up, groundNormal);
                
                // Only consider grounded if the slope is walkable
                if (slopeAngle <= controller.slopeLimit)
                {
                    isGrounded = true;
                    lastGroundedTime = Time.time;
                }
                else
                {
                    isGrounded = false;
                }
            }
            else
            {
                isGrounded = false;
            }
        }

        private void ProcessMovement()
        {
            bool canJump = (Time.time - lastGroundedTime) < coyoteTime;
            bool isFreeLooking = playerCamera != null && playerCamera.IsFreeLooking;

            // 1. Calculate Target Speed
            float targetSpeed = 0f;
            if (isFreeLooking)
            {
                if (moveInput.magnitude > 0.1f)
                    targetSpeed = (moveInput.y < -0.1f) ? (runToggle ? runBackSpeed : walkBackSpeed) : (runToggle ? runSpeed : walkSpeed);
            }
            else
            {
                if (Mathf.Abs(moveInput.y) > 0.1f || Mathf.Abs(strafeInput) > 0.1f)
                    targetSpeed = (moveInput.y < -0.1f) ? (runToggle ? runBackSpeed : walkBackSpeed) : (runToggle ? runSpeed : walkSpeed);
            }

            float accelRate = targetSpeed > 0.1f ? acceleration : deceleration;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.deltaTime);

            // 2. Calculate Direction
            if (isFreeLooking)
            {
                Transform cameraTransform = Camera.main != null ? Camera.main.transform : transform;
                Vector3 forward = cameraTransform.forward;
                Vector3 right = cameraTransform.right;
                forward.y = 0f; right.y = 0f;
                forward.Normalize(); right.Normalize();

                Vector3 inputDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
                if (inputDirection.magnitude > 0.1f)
                {
                    moveDirection = (forward * inputDirection.z + right * inputDirection.x).normalized;
                    Quaternion targetRotation = Quaternion.LookRotation(forward) * Quaternion.Euler(0f, 180f, 0f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, strafeRotationSpeed * Time.deltaTime);
                }
                else
                {
                    moveDirection = Vector3.zero;
                }
            }
            else
            {
                turnInput = moveInput.x;
                if (Mathf.Abs(turnInput) > 0.1f)
                {
                    float turnAmount = turnInput * turnRotationSpeed * Time.deltaTime;
                    transform.Rotate(Vector3.up, turnAmount, Space.World);
                }

                Vector3 forward = -transform.forward;
                Vector3 right = -transform.right;
                float forwardInput = moveInput.y;

                if (Mathf.Abs(strafeInput) > 0.1f) moveDirection = right * strafeInput;
                else if (Mathf.Abs(forwardInput) > 0.1f) moveDirection = forward * Mathf.Sign(forwardInput);
                else moveDirection = Vector3.zero;
            }

            // 3. Slope Projection (Walk smooth on slopes)
            if (isGrounded && groundNormal != Vector3.up)
            {
                moveDirection = Vector3.ProjectOnPlane(moveDirection, groundNormal).normalized;
            }

            // Disable planar movement from this function if swimming (handled in ProcessSwimming)
            Vector3 planarVelocity = moveDirection * currentSpeed;

            // 4. Gravity & Jumping
            if (isGrounded)
            {
                // Apply continuous downward force to stick to slopes
                velocity.y = -stickToGroundForce;

                if (jumpPressed && canJump)
                {
                    velocity.y = jumpVelocity;
                    isGrounded = false;
                    lastGroundedTime = 0f;
                    lastJumpTime = Time.time; // Prevents ground snap-back
                }
            }
            else
            {
                velocity.y -= gravity * Time.deltaTime;
                
                // Step down logic: if falling slightly (walking down stairs), snap down
                if (velocity.y < 0 && (Time.time - lastGroundedTime) < 0.15f && (Time.time - lastJumpTime) > 0.2f)
                {
                    velocity.y -= stickToGroundForce * 2f * Time.deltaTime;
                }
            }

            jumpPressed = false;
            controller.Move((planarVelocity + velocity) * Time.deltaTime);
        }

        private void HandleMovingPlatform()
        {
            RaycastHit hit;
            Transform newPlatform = null;

            // Check for moving platform under player
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out hit, groundDistance + 1.0f, groundMask))
            {
                newPlatform = hit.transform;
            }

            if (newPlatform != currentPlatform)
            {
                currentPlatform = newPlatform;
                if (currentPlatform != null)
                {
                    lastPlatformPosition = currentPlatform.position;
                    lastPlatformRotation = currentPlatform.rotation;
                    
                    // Ensure we can walk on potentially steep ship decks
                    if (controller.slopeLimit < maxSlopeAngle)
                        controller.slopeLimit = maxSlopeAngle;
                }
            }

            // Apply platform movement to player
            if (currentPlatform != null)
            {
                Vector3 platformMoveDelta = currentPlatform.position - lastPlatformPosition;
                Quaternion rotationDelta = currentPlatform.rotation * Quaternion.Inverse(lastPlatformRotation);

                // Move player with platform
                controller.Move(platformMoveDelta);

                // Rotate player position around platform pivot
                Vector3 offsetFromPlatform = transform.position - currentPlatform.position;
                Vector3 rotatedOffset = rotationDelta * offsetFromPlatform;
                Vector3 rotationMoveDelta = rotatedOffset - offsetFromPlatform;
                controller.Move(rotationMoveDelta);

                // Rotate player facing
                Vector3 eulerDelta = rotationDelta.eulerAngles;
                transform.Rotate(Vector3.up, eulerDelta.y, Space.World);

                lastPlatformPosition = currentPlatform.position;
                lastPlatformRotation = currentPlatform.rotation;
            }
        }

        // Public API
        public bool IsGrounded => isGrounded;
        public float CurrentSpeed => currentSpeed;
        public Vector3 Velocity => controller.velocity;
        public Vector2 MoveInput => moveInput;
        public float TurnInput => turnInput;
        public float StrafeInput => strafeInput;
        public bool IsFreeLooking => playerCamera != null && playerCamera.IsFreeLooking;
        public bool IsRunning => runToggle;
        public bool IsFalling => !isGrounded && velocity.y < -fallingThreshold;

        private void SetupModelHierarchy()
        {
            foreach (Transform child in transform)
            {
                if (child.name != "GroundCheck" && child.name != "Armature")
                {
                    Vector3 currentRotation = child.localEulerAngles;
                    child.localRotation = Quaternion.Euler(currentRotation.x, modelRotationOffset, currentRotation.z);
                }
            }
        }

        private void SetupGroundCheck()
        {
            if (groundCheck == null)
            {
                GameObject groundCheckObj = new GameObject("GroundCheck");
                groundCheckObj.transform.SetParent(transform);
                groundCheckObj.transform.localPosition = new Vector3(0, -1f, 0);
                groundCheck = groundCheckObj.transform;
            }
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            Gizmos.color = isGrounded ? Color.green : Color.red;
            if (controller != null)
                Gizmos.DrawWireSphere(transform.position + Vector3.up * controller.radius, controller.radius * 0.9f);

            if (groundCheck != null)
                Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }
}