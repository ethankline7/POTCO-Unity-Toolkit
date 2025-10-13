/// <summary>
/// Player movement controller with CharacterController-based locomotion
/// Drives the Animator with Speed, MoveX, MoveY parameters
/// Design targets: responsive feel, coyote time, swimming support
/// </summary>
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
        [SerializeField] private float gravity = 8.73f;
        [SerializeField] private float jumpVelocity = 7.64f;
        [SerializeField] private float coyoteTime = 0.1f;

        [Header("Swimming")]
        [SerializeField] private float swimSpeed = 3.0f;
        [SerializeField] private float swimGravity = 2f;
        [SerializeField] private LayerMask waterLayer;

        [Header("Collision Setup")]
        [Tooltip("Automatically add mesh colliders to all world props on start")]
        [SerializeField] private bool autoAddMeshColliders = true;
        [Tooltip("Ground check transform for platform detection")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundDistance = 0.4f;
        [SerializeField] private LayerMask groundMask = -1;
        [Tooltip("Maximum height of obstacles the character can step over")]
        [SerializeField] private float stepOffset = 0.5f;
        [Tooltip("Skin width for collision detection (prevents jittering)")]
        [SerializeField] private float skinWidth = 0.08f;
        [Tooltip("Minimum falling velocity to trigger air/falling state")]
        [SerializeField] private float fallingThreshold = 0.5f;

        [Header("Model Setup")]
        [Tooltip("POTCO models often face backwards. Set to 180 to flip model facing. Applied once at start.")]
        [SerializeField] private float modelRotationOffset = 180f;
        [Tooltip("If true, automatically create a 'Model' child and move all visual components to it")]
        [SerializeField] private bool autoSetupModelHierarchy = true;

        [Header("Rotation")]
        [Tooltip("Rotation speed when character aligns with camera (free-look mode)")]
        [SerializeField] private float strafeRotationSpeed = 8f;
        [Tooltip("Rotation speed when character turns with A/D keys (normal mode)")]
        [SerializeField] private float turnRotationSpeed = 90.1f; // Degrees per second

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool debugAnimator = false;

        private CharacterController controller;
        private PlayerCamera playerCamera;
        private Vector3 velocity;
        private Vector3 moveDirection;
        private float currentSpeed;
        private bool isGrounded;
        private float lastGroundedTime;
        private bool isSwimming;

        // Input
        private Vector2 moveInput;
        private bool jumpPressed;
        private bool runToggle = true;
        private float turnInput; // A/D turning when not free-looking
        private float strafeInput; // Q/E strafing

        // Moving platform support
        private Transform currentPlatform;
        private Vector3 lastPlatformPosition;
        private Quaternion lastPlatformRotation;
        private List<Collider> ignoredShipColliders = new List<Collider>();

        // Debug
        private float lastDebugTime;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            // Configure CharacterController collision settings
            controller.stepOffset = stepOffset;
            controller.skinWidth = skinWidth;

            Debug.Log($"✅ CharacterController configured - Step Offset: {stepOffset}, Skin Width: {skinWidth}");

            // Auto-attach VisZoneSensor if not present
            if (GetComponent<POTCO.VisZones.VisZoneSensor>() == null)
            {
                gameObject.AddComponent<POTCO.VisZones.VisZoneSensor>();
                Debug.Log("✅ Auto-attached VisZoneSensor to player");
            }

            // Auto-attach HideLevelGeometry if not present (but don't run it yet)
            if (GetComponent<POTCO.HideLevelGeometry>() == null)
            {
                gameObject.AddComponent<POTCO.HideLevelGeometry>();
                Debug.Log("✅ Auto-attached HideLevelGeometry to player");
            }
        }

        private void Start()
        {
            // Find PlayerCamera
            playerCamera = Camera.main?.GetComponent<PlayerCamera>();
            if (playerCamera == null)
            {
                Debug.LogWarning("⚠️ PlayerCamera not found. Camera-based controls may not work.");
            }

            // Hide level geometry AFTER VisZone system has initialized
            POTCO.HideLevelGeometry hideLevelGeo = GetComponent<POTCO.HideLevelGeometry>();
            if (hideLevelGeo != null)
            {
                hideLevelGeo.HideObjects();
                Debug.Log("✅ HideLevelGeometry executed after VisZone initialization");
            }

            // Setup model hierarchy with rotation offset (proper fix)
            if (autoSetupModelHierarchy && Mathf.Abs(modelRotationOffset) > 0.1f)
            {
                SetupModelHierarchy();
            }

            // Setup ground check
            SetupGroundCheck();

            // Auto-add colliders to world props
            if (autoAddMeshColliders)
            {
                AddMeshCollidersToProps();
            }
        }

        private void Update()
        {
            ProcessInput();
            ProcessMovement();
            ProcessSwimming();
            HandleMovingPlatform();
        }

        private void ProcessInput()
        {
            // Get movement input (WASD / Arrow Keys)
            moveInput.x = Input.GetAxisRaw("Horizontal");
            moveInput.y = Input.GetAxisRaw("Vertical");

            // Strafe input (Q/E keys)
            strafeInput = 0f;
            if (Input.GetKey(KeyCode.Q))
            {
                strafeInput = -1f; // Strafe left
            }
            else if (Input.GetKey(KeyCode.E))
            {
                strafeInput = 1f; // Strafe right
            }

            // Jump input
            if (Input.GetButtonDown("Jump"))
            {
                jumpPressed = true;
            }

            // Run toggle (Left Shift)
            if (Input.GetKeyDown(KeyCode.LeftShift))
            {
                runToggle = !runToggle;
            }
        }

        private void ProcessMovement()
        {
            isGrounded = controller.isGrounded;

            // Coyote time
            if (isGrounded)
            {
                lastGroundedTime = Time.time;
            }

            bool canJump = (Time.time - lastGroundedTime) < coyoteTime;

            // Check if camera is in free-look mode
            bool isFreeLooking = playerCamera != null && playerCamera.IsFreeLooking;

            // Calculate target speed based on actual movement input
            float targetSpeed = 0f;
            if (isFreeLooking)
            {
                // Free-look: any WASD input = movement
                if (moveInput.magnitude > 0.1f)
                {
                    // Check if moving backward (S key)
                    bool movingBackward = moveInput.y < -0.1f;
                    if (movingBackward)
                    {
                        targetSpeed = runToggle ? runBackSpeed : walkBackSpeed;
                    }
                    else
                    {
                        targetSpeed = runToggle ? runSpeed : walkSpeed;
                    }
                }
            }
            else
            {
                // Normal mode: W/S for forward/backward, A/D for turning, Q/E for strafing
                if (Mathf.Abs(moveInput.y) > 0.1f || Mathf.Abs(strafeInput) > 0.1f)
                {
                    // Check if moving backward (S key)
                    bool movingBackward = moveInput.y < -0.1f;
                    if (movingBackward)
                    {
                        targetSpeed = runToggle ? runBackSpeed : walkBackSpeed;
                    }
                    else
                    {
                        targetSpeed = runToggle ? runSpeed : walkSpeed;
                    }
                }
            }

            // Smooth acceleration/deceleration
            float accelRate = targetSpeed > 0.1f ? acceleration : deceleration;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.deltaTime);

            if (isFreeLooking)
            {
                // FREE-LOOK MODE (Right-click held): Strafing movement
                Transform cameraTransform = Camera.main != null ? Camera.main.transform : transform;
                Vector3 forward = cameraTransform.forward;
                Vector3 right = cameraTransform.right;

                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();

                // Calculate move direction based on input (WASD strafing)
                Vector3 inputDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
                if (inputDirection.magnitude > 0.1f)
                {
                    moveDirection = (forward * inputDirection.z + right * inputDirection.x).normalized;

                    // Only rotate character when MOVING during free-look
                    // Add 180° rotation because model is rotated 180° inside
                    Quaternion targetRotation = Quaternion.LookRotation(forward) * Quaternion.Euler(0f, 180f, 0f);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, strafeRotationSpeed * Time.deltaTime);
                }
                else
                {
                    moveDirection = Vector3.zero;
                    // When standing still in free-look, player doesn't rotate (camera orbits freely)
                }
            }
            else
            {
                // NORMAL MODE (No right-click): A/D turns character, W/S moves forward/backward, Q/E strafes

                // A/D keys turn the character (only if not strafing with Q/E)
                turnInput = moveInput.x;
                if (Mathf.Abs(turnInput) > 0.1f)
                {
                    float turnAmount = turnInput * turnRotationSpeed * Time.deltaTime;
                    transform.Rotate(Vector3.up, turnAmount, Space.World);
                }

                // Calculate movement direction
                Vector3 forward = -transform.forward; // Inverted because model is rotated 180° inside
                Vector3 right = -transform.right; // Inverted for same reason

                // W/S keys for forward/backward
                float forwardInput = moveInput.y;

                // Q/E keys for strafe left/right
                if (Mathf.Abs(strafeInput) > 0.1f)
                {
                    // Strafing with Q/E
                    moveDirection = right * strafeInput;
                }
                else if (Mathf.Abs(forwardInput) > 0.1f)
                {
                    // Moving forward/backward with W/S
                    moveDirection = forward * Mathf.Sign(forwardInput);
                }
                else
                {
                    moveDirection = Vector3.zero;
                }
            }

            // Apply movement
            Vector3 planarVelocity = moveDirection * currentSpeed;

            // Gravity and jumping
            if (!isSwimming)
            {
                if (isGrounded && velocity.y < 0)
                {
                    velocity.y = -2f; // Small downward force to keep grounded
                }

                if (jumpPressed && canJump)
                {
                    velocity.y = jumpVelocity;
                }

                velocity.y -= gravity * Time.deltaTime;
            }

            jumpPressed = false;

            // Apply velocity
            controller.Move((planarVelocity + velocity) * Time.deltaTime);
        }

        private void ProcessSwimming()
        {
            // Check if in water (simple sphere check)
            isSwimming = Physics.CheckSphere(transform.position + Vector3.up * 1.5f, 0.5f, waterLayer);

            if (isSwimming)
            {
                // Reduce gravity in water
                velocity.y = Mathf.MoveTowards(velocity.y, 0f, swimGravity * Time.deltaTime);

                // Allow vertical movement
                if (moveInput.magnitude > 0.1f)
                {
                    Vector3 swimDirection = moveDirection;
                    if (Input.GetKey(KeyCode.Space))
                    {
                        swimDirection.y = 1f; // Swim up
                    }
                    else if (Input.GetKey(KeyCode.LeftControl))
                    {
                        swimDirection.y = -1f; // Swim down
                    }

                    velocity = swimDirection.normalized * swimSpeed;
                }
                else
                {
                    velocity = Vector3.MoveTowards(velocity, Vector3.zero, deceleration * Time.deltaTime);
                }
            }
        }

        // Public API for external systems
        public bool IsGrounded => isGrounded;
        public bool IsSwimming => isSwimming;
        public float CurrentSpeed => currentSpeed;
        public Vector3 Velocity => controller.velocity;
        public Vector2 MoveInput => moveInput; // For animation system to detect direction
        public float TurnInput => turnInput; // For turning animations
        public float StrafeInput => strafeInput; // For Q/E strafe animations
        public bool IsFreeLooking => playerCamera != null && playerCamera.IsFreeLooking;
        public bool IsRunning => runToggle; // For animation system to know if running

        /// <summary>
        /// Returns true if player is actually falling (not just slightly off ground)
        /// Uses fallingThreshold to prevent glitchy air animations from tiny bumps
        /// </summary>
        public bool IsFalling => !isGrounded && velocity.y < -fallingThreshold;

        private void SetupModelHierarchy()
        {
            // Check if Model child already exists
            Transform existingModel = transform.Find("Model");
            if (existingModel != null)
            {
                Debug.Log("✅ Model child already exists - skipping hierarchy setup");
                return;
            }

            Debug.Log($"🔧 Setting up model hierarchy with {modelRotationOffset}° rotation offset...");

            // Create Model child
            GameObject modelChild = new GameObject("Model");
            modelChild.transform.SetParent(transform);
            modelChild.transform.localPosition = Vector3.zero;
            modelChild.transform.localRotation = Quaternion.Euler(0f, modelRotationOffset, 0f);
            modelChild.transform.localScale = Vector3.one;

            // Get all children except the one we just created
            List<Transform> childrenToMove = new List<Transform>();
            foreach (Transform child in transform)
            {
                if (child != modelChild.transform && child.name != "GroundCheck")
                {
                    childrenToMove.Add(child);
                }
            }

            // Move all visual children to Model
            foreach (Transform child in childrenToMove)
            {
                child.SetParent(modelChild.transform);
            }

            // Move Animation component to Model child
            Animation animComponent = GetComponent<Animation>();
            if (animComponent != null)
            {
                // Copy animation clips to new component on Model
                Animation newAnimComponent = modelChild.AddComponent<Animation>();

                // Copy all clips
                foreach (AnimationState state in animComponent)
                {
                    newAnimComponent.AddClip(state.clip, state.name);
                }

                // Destroy old component
                Destroy(animComponent);

                Debug.Log("   ✅ Moved Animation component to Model child");
            }

            Debug.Log($"✅ Model hierarchy setup complete - moved {childrenToMove.Count} children to Model with {modelRotationOffset}° offset");
            Debug.Log("   Player GameObject rotation is now clean and works normally with camera");
        }

        private void SetupGroundCheck()
        {
            if (groundCheck == null)
            {
                GameObject groundCheckObj = new GameObject("GroundCheck");
                groundCheckObj.transform.SetParent(transform);
                groundCheckObj.transform.localPosition = new Vector3(0, -1f, 0);
                groundCheck = groundCheckObj.transform;
                Debug.Log("✅ Created GroundCheck transform for platform detection");
            }
        }

        private void HandleMovingPlatform()
        {
            // Detect platform under player
            RaycastHit hit;
            Transform newPlatform = null;

            if (groundCheck != null && Physics.Raycast(groundCheck.position, Vector3.down, out hit, groundDistance + 0.1f, groundMask))
            {
                // Check if the hit object or its parents have a ShipController (it's a moving ship)
                POTCO.ShipController shipController = hit.collider.GetComponentInParent<POTCO.ShipController>();
                if (shipController != null)
                {
                    newPlatform = shipController.transform;
                }
            }

            // Platform changed
            if (newPlatform != currentPlatform)
            {
                // Restore collision with previous platform's colliders
                if (currentPlatform != null && ignoredShipColliders.Count > 0)
                {
                    foreach (Collider shipCollider in ignoredShipColliders)
                    {
                        if (shipCollider != null)
                        {
                            Physics.IgnoreCollision(controller, shipCollider, false);
                        }
                    }
                    ignoredShipColliders.Clear();
                    Debug.Log("🚶 Left moving platform - restored collision");
                }

                currentPlatform = newPlatform;

                if (currentPlatform != null)
                {
                    lastPlatformPosition = currentPlatform.position;
                    lastPlatformRotation = currentPlatform.rotation;

                    // Ignore collision with ship colliders to prevent glitching
                    Collider[] shipColliders = currentPlatform.GetComponentsInChildren<Collider>();
                    foreach (Collider shipCollider in shipColliders)
                    {
                        if (shipCollider != null && shipCollider != controller)
                        {
                            Physics.IgnoreCollision(controller, shipCollider, true);
                            ignoredShipColliders.Add(shipCollider);
                        }
                    }

                    Debug.Log($"🚢 Stepped onto moving platform: {currentPlatform.name} - ignoring {ignoredShipColliders.Count} colliders");
                }
            }

            // Move with platform
            if (currentPlatform != null && isGrounded)
            {
                // Calculate platform movement delta
                Vector3 platformMoveDelta = currentPlatform.position - lastPlatformPosition;

                // Calculate rotation delta
                Quaternion rotationDelta = currentPlatform.rotation * Quaternion.Inverse(lastPlatformRotation);

                // Apply platform movement
                controller.Move(platformMoveDelta);

                // Apply rotation around platform center
                Vector3 offsetFromPlatform = transform.position - currentPlatform.position;
                Vector3 rotatedOffset = rotationDelta * offsetFromPlatform;
                Vector3 rotationMoveDelta = rotatedOffset - offsetFromPlatform;
                controller.Move(rotationMoveDelta);

                // Rotate player with platform (only Y-axis to keep upright)
                Vector3 eulerDelta = rotationDelta.eulerAngles;
                transform.Rotate(Vector3.up, eulerDelta.y, Space.World);

                // Store current platform transform for next frame
                lastPlatformPosition = currentPlatform.position;
                lastPlatformRotation = currentPlatform.rotation;
            }
        }

        private void AddMeshCollidersToProps()
        {
            Debug.Log("🔧 Auto-adding mesh colliders to world props...");

            POTCO.ObjectListInfo[] objectListInfos = FindObjectsByType<POTCO.ObjectListInfo>(FindObjectsSortMode.None);
            int colliderCount = 0;

            foreach (POTCO.ObjectListInfo objectInfo in objectListInfos)
            {
                if (objectInfo.GetComponent<Collider>() == null)
                {
                    MeshFilter meshFilter = objectInfo.GetComponent<MeshFilter>();
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        MeshCollider meshCollider = objectInfo.gameObject.AddComponent<MeshCollider>();
                        meshCollider.sharedMesh = meshFilter.sharedMesh;
                        meshCollider.convex = false;
                        colliderCount++;
                    }
                    else
                    {
                        // Try child mesh filters
                        MeshFilter[] childMeshFilters = objectInfo.GetComponentsInChildren<MeshFilter>();
                        foreach (MeshFilter childMeshFilter in childMeshFilters)
                        {
                            if (childMeshFilter.GetComponent<Collider>() == null && childMeshFilter.sharedMesh != null)
                            {
                                MeshCollider meshCollider = childMeshFilter.gameObject.AddComponent<MeshCollider>();
                                meshCollider.sharedMesh = childMeshFilter.sharedMesh;
                                meshCollider.convex = false;
                                colliderCount++;
                            }
                        }
                    }
                }
            }

            Debug.Log($"✅ Added {colliderCount} mesh colliders to world props");
        }

        // Debug visualization
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            // Draw movement direction
            if (moveDirection.magnitude > 0.1f)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawRay(transform.position + Vector3.up, moveDirection * 2f);
                Gizmos.DrawWireSphere(transform.position + Vector3.up + moveDirection * 2f, 0.2f);
            }

            // Draw forward direction
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 1.5f);

            // Draw input direction
            if (moveInput.magnitude > 0.1f)
            {
                Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, inputDir * 1f);
            }

            // Draw grounded check
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.3f);

            // Draw ground check sphere
            if (groundCheck != null)
            {
                Gizmos.color = isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
            }

            // Draw platform connection
            if (currentPlatform != null && isGrounded)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, currentPlatform.position);
                Gizmos.DrawWireSphere(currentPlatform.position, 0.5f);
            }
        }
    }
}
