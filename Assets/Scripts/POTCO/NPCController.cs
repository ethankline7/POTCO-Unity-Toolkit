using UnityEngine;
using System.Collections;

namespace POTCO
{
    /// <summary>
    /// NPC AI Controller with FSM (LandRoam, Notice, Greeting)
    /// Reuses CharacterController movement logic from PlayerController
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NPCData))]
    public class NPCController : MonoBehaviour
    {
        #region State Enum
        public enum NPCState
        {
            LandRoam,   // Default idle/walk/patrol state
            Notice,     // Player entered detection radius - turn to face
            Greeting    // Playing greeting animation
        }
        #endregion

        #region Inspector Fields
        [Header("State (Read-Only)")]
        [SerializeField] private NPCState currentState = NPCState.LandRoam;

        [Header("Detection")]
        [Tooltip("Distance to notice player (turn to face)")]
        [SerializeField] private float noticeDistance = 5f;
        [Tooltip("Distance to greet player (play greeting animation)")]
        [SerializeField] private float greetDistance = 3f;
        [Tooltip("Angle cone for noticing player (degrees from forward)")]
        [SerializeField] private float noticeConeAngle = 120f;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float turnSpeed = 90f;
        [SerializeField] private float gravity = 8.73f;

        [Header("Patrol")]
        [Tooltip("Enable/disable patrol movement")]
        [SerializeField] private bool enablePatrol = false;
        [Tooltip("Wait time at each patrol point")]
        [SerializeField] private float patrolWaitTime = 3f;
        [Tooltip("Chance to stay idle instead of patrolling (0-1)")]
        [SerializeField] private float idleChance = 0.5f;

        [Header("Model Setup")]
        [Tooltip("POTCO models face backwards - set to 180 to flip")]
        [SerializeField] private float modelRotationOffset = 180f;
        [SerializeField] private bool autoSetupModelHierarchy = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        #endregion

        #region Private Variables
        private CharacterController controller;
        private NPCData npcData;
        private Transform playerTransform;
        private Vector3 velocity;
        private bool isGrounded;

        // AI state
        private float stateEnterTime;
        private Vector3 spawnPosition;
        private Vector3 currentWaypoint;
        private float waypointArrivalTime;
        private bool isIdleAtWaypoint;

        // Rotation
        private Quaternion targetRotation;
        private bool isTurningToPlayer;
        private float currentTurnDirection = 0f; // -1 = left, 1 = right, 0 = not turning

        // Stationary lock
        private Vector3 lockedPosition;
        private Quaternion lockedRotation;
        private bool positionLocked = false;
        #endregion

        #region Initialization
        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            npcData = GetComponent<NPCData>();

            // Auto-setup model hierarchy (same as PlayerController)
            if (autoSetupModelHierarchy && Mathf.Abs(modelRotationOffset) > 0.1f)
            {
                SetupModelHierarchy();
            }
        }

        private void Start()
        {
            spawnPosition = transform.position;
            currentState = NPCState.LandRoam;
            stateEnterTime = Time.time;

            // Pick initial waypoint if patrol is enabled
            if (npcData != null && npcData.patrolRadius > 0.1f)
            {
                currentWaypoint = GetRandomPatrolPoint();
            }
            else
            {
                // No patrol - stay at spawn
                currentWaypoint = spawnPosition;
                isIdleAtWaypoint = true;
            }
        }
        #endregion

        #region Update Loop
        private void Update()
        {
            // Re-find player if lost (check every 60 frames for performance)
            if (Time.frameCount % 60 == 0 && playerTransform == null)
            {
                playerTransform = FindPlayer();
            }

            // Lock position for stationary NPCs
            if (npcData != null && npcData.isStationary)
            {
                if (!positionLocked)
                {
                    // First time detecting stationary - lock current position and rotation
                    lockedPosition = transform.position;
                    lockedRotation = transform.rotation;
                    positionLocked = true;

                    // Disable CharacterController to prevent collision-based movement
                    if (controller != null && controller.enabled)
                    {
                        controller.enabled = false;
                        Debug.Log($"🔒 Disabled CharacterController for stationary NPC: {gameObject.name}");
                    }

                    // Also check for and disable any Rigidbody components
                    Rigidbody rb = GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = true;
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        Debug.Log($"🔒 Set Rigidbody to kinematic for stationary NPC: {gameObject.name}");
                    }

                    Debug.Log($"🔒 Position and rotation locked for stationary NPC: {gameObject.name}");
                }
                else
                {
                    // Force position and rotation to locked values (prevents any movement or rotation)
                    if (Vector3.Distance(transform.position, lockedPosition) > 0.001f)
                    {
                        transform.position = lockedPosition;
                        Debug.LogWarning($"⚠️ Stationary NPC {gameObject.name} was moved! Resetting to locked position.");
                    }
                    if (Quaternion.Angle(transform.rotation, lockedRotation) > 0.01f)
                    {
                        transform.rotation = lockedRotation;
                    }
                }

                // Stationary NPCs still handle FSM state transitions but don't move or rotate
                switch (currentState)
                {
                    case NPCState.LandRoam:
                        // Check for player detection (transition to Notice)
                        if (playerTransform != null && ShouldNoticePlayer())
                        {
                            ChangeState(NPCState.Notice);
                        }
                        break;
                    case NPCState.Notice:
                        UpdateNotice();
                        break;
                    case NPCState.Greeting:
                        UpdateGreeting();
                        break;
                }

                return; // Skip movement and gravity
            }

            isGrounded = controller.isGrounded;

            // Handle FSM
            switch (currentState)
            {
                case NPCState.LandRoam:
                    UpdateLandRoam();
                    break;
                case NPCState.Notice:
                    UpdateNotice();
                    break;
                case NPCState.Greeting:
                    UpdateGreeting();
                    break;
            }

            // Apply gravity
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }
            velocity.y -= gravity * Time.deltaTime;

            // Apply movement
            controller.Move(velocity * Time.deltaTime);
        }
        #endregion

        #region FSM States
        /// <summary>
        /// LandRoam: Default patrol and idle behavior
        /// </summary>
        private void UpdateLandRoam()
        {
            // Check for player detection
            if (playerTransform != null && ShouldNoticePlayer())
            {
                ChangeState(NPCState.Notice);
                return;
            }

            // Stationary NPCs never move or rotate (contextual animations like sitting, bar_wipe, etc.)
            if (npcData != null && npcData.isStationary)
            {
                velocity.x = 0;
                velocity.z = 0;
                return;
            }

            // Only patrol if enabled
            if (!enablePatrol)
            {
                // Just stand idle
                velocity.x = 0;
                velocity.z = 0;
                return;
            }

            // Patrol or idle behavior
            if (isIdleAtWaypoint)
            {
                // Waiting at waypoint
                if (Time.time - waypointArrivalTime > patrolWaitTime)
                {
                    // Pick new waypoint or stay idle
                    if (npcData != null && npcData.patrolRadius > 0.1f && Random.value > idleChance)
                    {
                        currentWaypoint = GetRandomPatrolPoint();
                        isIdleAtWaypoint = false;
                    }
                }
            }
            else
            {
                // Moving to waypoint
                float distanceToWaypoint = Vector3.Distance(transform.position, currentWaypoint);
                if (distanceToWaypoint < 0.5f)
                {
                    // Arrived at waypoint
                    isIdleAtWaypoint = true;
                    waypointArrivalTime = Time.time;
                }
                else
                {
                    // Navigate toward waypoint
                    NavigateToPoint(currentWaypoint, walkSpeed);
                }
            }
        }

        /// <summary>
        /// Notice: Player entered detection range - turn to face them
        /// </summary>
        private void UpdateNotice()
        {
            // Check if player left detection range
            if (playerTransform == null || !ShouldNoticePlayer())
            {
                ChangeState(NPCState.LandRoam);
                return;
            }

            // Check if player is close enough to greet
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer <= greetDistance && !string.IsNullOrEmpty(npcData?.greetingAnimation))
            {
                ChangeState(NPCState.Greeting);
                return;
            }

            // Don't rotate if NPC is stationary (completely locked in place)
            if (npcData != null && npcData.isStationary)
            {
                return;
            }

            // Turn to face player
            // Model child has 180° local rotation, so parent must face AWAY from target for model to face TOWARD target
            Vector3 toPlayer = (playerTransform.position - transform.position);
            toPlayer.y = 0;
            if (toPlayer != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(toPlayer) * Quaternion.Euler(0f, 180f, 0f);

                // Calculate turn direction before rotating
                float angleDifference = Quaternion.Angle(transform.rotation, targetRot);
                if (angleDifference > 1f) // Only consider turning if angle is significant
                {
                    // Use signed angle to determine left (-) or right (+)
                    Vector3 cross = Vector3.Cross(transform.forward, toPlayer);
                    currentTurnDirection = cross.y > 0 ? 1f : -1f; // Positive Y = turning right, Negative Y = turning left
                }
                else
                {
                    currentTurnDirection = 0f; // Not turning
                }

                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }
            else
            {
                currentTurnDirection = 0f;
            }
        }

        /// <summary>
        /// Greeting: Playing greeting animation for player
        /// </summary>
        private void UpdateGreeting()
        {
            // Check if player left greeting range
            if (playerTransform == null)
            {
                ChangeState(NPCState.LandRoam);
                return;
            }

            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer > greetDistance * 1.5f)
            {
                ChangeState(NPCState.Notice);
                return;
            }

            // Don't rotate if NPC is stationary (completely locked in place)
            if (npcData != null && npcData.isStationary)
            {
                return;
            }

            // Continue facing player during greeting
            // Model child has 180° local rotation, so parent must face AWAY from target for model to face TOWARD target
            Vector3 toPlayer = (playerTransform.position - transform.position);
            toPlayer.y = 0;
            if (toPlayer != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(toPlayer) * Quaternion.Euler(0f, 180f, 0f);

                // Calculate turn direction before rotating
                float angleDifference = Quaternion.Angle(transform.rotation, targetRot);
                if (angleDifference > 1f) // Only consider turning if angle is significant
                {
                    // Use signed angle to determine left (-) or right (+)
                    Vector3 cross = Vector3.Cross(transform.forward, toPlayer);
                    currentTurnDirection = cross.y > 0 ? 1f : -1f; // Positive Y = turning right, Negative Y = turning left
                }
                else
                {
                    currentTurnDirection = 0f; // Not turning
                }

                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }
            else
            {
                currentTurnDirection = 0f;
            }

            // Greeting animation is handled by NPCAnimationPlayer
            // This state just ensures the NPC stays facing the player
        }
        #endregion

        #region Detection Helpers
        private bool ShouldNoticePlayer()
        {
            if (playerTransform == null) return false;

            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer > noticeDistance) return false;

            // Check angle cone
            // Model faces OPPOSITE of parent's forward (due to 180° offset), so check angle from -forward
            Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
            float angle = Vector3.Angle(-transform.forward, toPlayer);
            return angle < noticeConeAngle;
        }
        #endregion

        #region Navigation
        private void NavigateToPoint(Vector3 targetPoint, float speed)
        {
            Vector3 direction = (targetPoint - transform.position);
            direction.y = 0;
            direction.Normalize();

            if (direction != Vector3.zero)
            {
                // Rotate toward target
                // Model child has 180° local rotation, so parent must face AWAY from target for model to face TOWARD target
                Quaternion targetRot = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 180f, 0f);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);

                // Move forward
                velocity = direction * speed;
            }
        }

        private Vector3 GetRandomPatrolPoint()
        {
            if (npcData == null) return spawnPosition;

            Vector2 randomCircle = Random.insideUnitCircle * npcData.patrolRadius;
            return spawnPosition + new Vector3(randomCircle.x, 0, randomCircle.y);
        }
        #endregion

        #region State Management
        private void ChangeState(NPCState newState)
        {
            if (currentState != newState)
            {
                Debug.Log($"[{gameObject.name}] State: {currentState} → {newState}");
                currentState = newState;
                stateEnterTime = Time.time;
            }
        }
        #endregion

        #region Utility
        private Transform FindPlayer()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Player.PlayerController pc = FindAnyObjectByType<Player.PlayerController>();
                if (pc != null)
                {
                    if (pc.tag == "Untagged") pc.tag = "Player";
                    player = pc.gameObject;
                }
            }
            return player?.transform;
        }

        private void SetupModelHierarchy()
        {
            // Check if Model child already exists
            Transform existingModel = transform.Find("Model");
            if (existingModel != null) return;

            // Store current rotation - this is the spawn rotation from world data
            Quaternion originalRotation = transform.rotation;

            // Create Model child
            GameObject modelChild = new GameObject("Model");
            modelChild.transform.SetParent(transform);
            modelChild.transform.localPosition = Vector3.zero;
            modelChild.transform.localRotation = Quaternion.Euler(0f, modelRotationOffset, 0f);
            modelChild.transform.localScale = Vector3.one;

            // Move all children except GroundCheck to Model
            System.Collections.Generic.List<Transform> childrenToMove = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in transform)
            {
                if (child != modelChild.transform && child.name != "GroundCheck")
                {
                    childrenToMove.Add(child);
                }
            }

            foreach (Transform child in childrenToMove)
            {
                child.SetParent(modelChild.transform);
            }

            // Adjust parent rotation to compensate for Model's 180° offset
            // We want: originalRotation = parent.rotation + Model.localRotation
            // So: parent.rotation = originalRotation - Model.localRotation
            // Which is: parent.rotation = originalRotation - 180°
            transform.rotation = originalRotation * Quaternion.Euler(0f, -modelRotationOffset, 0f);
        }
        #endregion

        #region Public API
        public NPCState CurrentState => currentState;
        public bool IsGrounded => isGrounded;
        // Return HORIZONTAL speed only (ignore Y/gravity)
        public float CurrentSpeed => new Vector3(velocity.x, 0, velocity.z).magnitude;
        // Return turn direction: -1 = left, 1 = right, 0 = not turning
        public float TurnDirection => currentTurnDirection;
        #endregion

        #region Gizmos
        private void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;

            Vector3 spawnPos = Application.isPlaying ? spawnPosition : transform.position;

            // Patrol radius
            Gizmos.color = Color.green;
            if (npcData != null && npcData.patrolRadius > 0.1f)
            {
                DrawCircle(spawnPos, npcData.patrolRadius, 32);
            }

            // Notice distance
            Gizmos.color = Color.yellow;
            DrawCircle(transform.position, noticeDistance, 32);

            // Greet distance
            Gizmos.color = Color.cyan;
            DrawCircle(transform.position, greetDistance, 16);

            // Notice cone
            Gizmos.color = Color.yellow;
            Vector3 forward = -transform.forward; // Model faces opposite of parent's forward
            Vector3 rightBound = Quaternion.Euler(0, noticeConeAngle, 0) * forward;
            Vector3 leftBound = Quaternion.Euler(0, -noticeConeAngle, 0) * forward;
            Gizmos.DrawRay(transform.position, rightBound * noticeDistance);
            Gizmos.DrawRay(transform.position, leftBound * noticeDistance);

            // Current waypoint
            if (Application.isPlaying)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(currentWaypoint, 0.5f);
                Gizmos.DrawLine(transform.position, currentWaypoint);
            }
        }

        private void DrawCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }
        #endregion
    }
}
