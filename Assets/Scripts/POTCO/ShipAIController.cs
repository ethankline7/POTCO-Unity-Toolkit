using UnityEngine;
using System.Collections;

namespace POTCO
{
    /// <summary>
    /// Ship AI Controller with clear behavior modes
    /// States: Patrol → Approach → Combat → Retreat
    /// Combat Stances: Circler, Sniper, Brawler
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ShipAIController : MonoBehaviour
    {
        #region Inspector Settings

        [Header("AI State (Read-Only)")]
        [SerializeField] private AIState currentState = AIState.Patrol;
        [SerializeField] private CombatStance combatStance = CombatStance.Circler;

        [Header("Detection Settings")]
        [Tooltip("How far ship can detect player")]
        public float detectionRange = 80f;
        [Tooltip("Distance to engage at")]
        public float engagementDistance = 50f;
        [Tooltip("Maximum chase distance from spawn")]
        public float maxChaseDistance = 200f;

        [Header("Movement Settings")]
        [Tooltip("Ship movement speed")]
        public float moveSpeed = 30f;
        [Tooltip("Ship rotation speed (degrees/sec)")]
        public float rotateSpeed = 20f;
        [Tooltip("How fast ship accelerates")]
        public float acceleration = 8f;

        [Header("Combat Settings")]
        [Tooltip("Preferred distance for broadside combat")]
        public float combatDistance = 40f;
        [Tooltip("Minimum distance before backing off")]
        public float minCombatDistance = 20f;
        [Tooltip("Maximum angle from perpendicular to fire (degrees)")]
        public float maxFiringAngle = 30f;
        [Tooltip("Time between combat behavior changes")]
        public float tacticChangeInterval = 8f;

        [Header("Ram Settings")]
        [Tooltip("Damage dealt when ramming")]
        public float ramDamage = 100f;
        [Tooltip("Ram duration (seconds)")]
        public float ramDuration = 5f;
        [Tooltip("Speed multiplier during ram")]
        public float ramSpeedMultiplier = 1.8f;

        [Header("Patrol Settings")]
        [Tooltip("Patrol radius around spawn")]
        public float patrolRadius = 100f;
        [Tooltip("Time at each patrol point")]
        public float patrolWaitTime = 10f;

        [Header("Retreat Settings")]
        [Tooltip("Health % to trigger retreat")]
        [Range(0f, 1f)] public float retreatThreshold = 0.25f;

        [Header("References")]
        public Transform playerTransform;

        #endregion

        #region Private Variables

        // Core references
        private Rigidbody rb;
        private Rigidbody playerRb;
        private ShipHealth shipHealth;
        private AIBroadside aiBroadside;

        // State tracking
        private Vector3 spawnPosition;
        private Vector3 currentWaypoint;
        private Vector3 lastKnownPlayerPosition;
        private float currentSpeed = 0f;
        private float stateEnterTime = 0f;
        private float lastTacticChangeTime = 0f;

        // Combat tracking
        private int circleDirection = 1; // 1 = clockwise, -1 = counterclockwise
        private bool isRamming = false;
        private Vector3 lastPlayerPosition;
        private float playerMovementThreshold = 5f; // Speed threshold to determine if player is sailing straight
        private float lastSniperTime = -999f; // Cooldown for sniper mode
        private float sniperCooldown = 15f; // Cooldown between sniper volleys

        // Obstacle avoidance
        private Vector3 avoidanceDirection = Vector3.zero;
        private float avoidanceWeight = 0f;
        private float obstacleDetectionRange = 30f;
        private BoxCollider hullCollider;

        #endregion

        #region Enums

        public enum AIState
        {
            Patrol,     // Wandering near spawn
            Approach,   // Moving to engagement range
            Combat,     // Active combat (uses combat stance)
            Ram,        // Ramming attack
            Retreat     // Low health escape
        }

        public enum CombatStance
        {
            Circler,    // Circles player, fires opportunistically
            Sniper,     // Lines up perfect shots, holds position
            Brawler     // Aggressive close combat with rams
        }

        #endregion

        #region Initialization

        private void Start()
        {
            // Initialize components
            rb = GetComponent<Rigidbody>();
            shipHealth = GetComponent<ShipHealth>();
            aiBroadside = GetComponent<AIBroadside>();
            spawnPosition = transform.position;

            // Configure rigidbody
            if (rb != null)
            {
                rb.useGravity = false;
                rb.isKinematic = true; // Kinematic since we use manual movement
                rb.linearDamping = 1f;
                rb.angularDamping = 2f;
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }

            // Add ship hull collider for collision detection
            AddShipHullCollider();

            // Find player
            if (playerTransform == null)
            {
                playerTransform = FindPlayer();
            }

            if (playerTransform != null)
            {
                // Get rigidbody from the target (ship or player character)
                playerRb = playerTransform.GetComponent<Rigidbody>();
                lastPlayerPosition = playerTransform.position;
            }

            // Pick initial combat stance based on randomization
            ChooseCombatStance();

            // Start patrol
            currentWaypoint = GetRandomPatrolPoint();
            stateEnterTime = Time.time;

            Debug.Log($"[{gameObject.name}] Initialized with stance: {combatStance}");
        }

        /// <summary>
        /// Randomly choose a combat stance based on personality
        /// </summary>
        private void ChooseCombatStance()
        {
            float roll = Random.value;

            if (roll < 0.4f)
                combatStance = CombatStance.Circler;
            else if (roll < 0.7f)
                combatStance = CombatStance.Sniper;
            else
                combatStance = CombatStance.Brawler;

            circleDirection = Random.value > 0.5f ? 1 : -1;
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            // Continuously re-check player collision ignore every frame until successful
            if (hullCollider != null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player == null)
                {
                    Player.PlayerController pc = FindAnyObjectByType<Player.PlayerController>();
                    if (pc != null) player = pc.gameObject;
                }

                // If player exists, ignore collision every frame (ensures it stays ignored)
                if (player != null)
                {
                    Collider[] playerColliders = player.GetComponentsInChildren<Collider>();
                    foreach (Collider playerCollider in playerColliders)
                    {
                        if (playerCollider != null && hullCollider != null)
                        {
                            Physics.IgnoreCollision(hullCollider, playerCollider, true);
                        }
                    }
                }
            }

            // Find player if lost or re-check if player switched to/from ship
            if (playerTransform == null || Time.frameCount % 120 == 0) // Re-check every 2 seconds
            {
                Transform newTarget = FindPlayer();
                if (newTarget != playerTransform)
                {
                    playerTransform = newTarget;
                    if (playerTransform != null)
                    {
                        playerRb = playerTransform.GetComponent<Rigidbody>();
                        lastPlayerPosition = playerTransform.position;
                        Debug.Log($"[{gameObject.name}] Updated target to: {playerTransform.name}");
                    }
                }
                if (playerTransform == null) return;
            }

            // Check for retreat
            if (ShouldRetreat() && currentState != AIState.Retreat && currentState != AIState.Ram)
            {
                ChangeState(AIState.Retreat);
            }

            // State machine
            switch (currentState)
            {
                case AIState.Patrol:
                    UpdatePatrol();
                    break;
                case AIState.Approach:
                    UpdateApproach();
                    break;
                case AIState.Combat:
                    UpdateCombat();
                    break;
                case AIState.Ram:
                    UpdateRam();
                    break;
                case AIState.Retreat:
                    UpdateRetreat();
                    break;
            }

            // Apply movement
            ApplyMovement();
        }

        #endregion

        #region State Updates

        /// <summary>
        /// Patrol: Wander around spawn point
        /// </summary>
        private void UpdatePatrol()
        {
            // Check for player
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            bool canSee = CanSeePlayer();

            // DEBUG: Log every frame in patrol to see what's happening
            if (Time.frameCount % 60 == 0) // Every 60 frames (~1 second)
            {
                Debug.Log($"[{gameObject.name}] PATROL DEBUG - Distance: {distanceToPlayer:F1}m, DetectionRange: {detectionRange}, CanSee: {canSee}, PlayerNull: {playerTransform == null}");
            }

            if (distanceToPlayer <= detectionRange && canSee)
            {
                Debug.Log($"[{gameObject.name}] Player detected at {distanceToPlayer:F1}m!");
                lastKnownPlayerPosition = playerTransform.position;
                ChangeState(AIState.Approach);
                return;
            }

            // Navigate to patrol waypoint
            NavigateToPoint(currentWaypoint, moveSpeed * 0.6f, true);

            // Pick new waypoint when reached or timeout
            if (Vector3.Distance(transform.position, currentWaypoint) < 15f ||
                Time.time > stateEnterTime + patrolWaitTime)
            {
                currentWaypoint = GetRandomPatrolPoint();
                stateEnterTime = Time.time;
            }
        }

        /// <summary>
        /// Approach: Close distance to engagement range
        /// </summary>
        private void UpdateApproach()
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            float distanceFromSpawn = Vector3.Distance(transform.position, spawnPosition);

            // Lost player
            if (!CanSeePlayer() || distanceToPlayer > detectionRange * 1.5f)
            {
                Debug.Log($"[{gameObject.name}] Lost player, returning to patrol");
                ChangeState(AIState.Patrol);
                currentWaypoint = spawnPosition;
                return;
            }

            // Too far from spawn
            if (distanceFromSpawn > maxChaseDistance)
            {
                Debug.Log($"[{gameObject.name}] Too far from spawn, retreating");
                ChangeState(AIState.Patrol);
                currentWaypoint = spawnPosition;
                return;
            }

            // Reached engagement distance
            if (distanceToPlayer <= engagementDistance)
            {
                Debug.Log($"[{gameObject.name}] Engagement distance reached! Entering combat as {combatStance}");
                ChangeState(AIState.Combat);
                return;
            }

            // Chase player
            Vector3 targetPosition = PredictPlayerPosition();
            NavigateToPoint(targetPosition, moveSpeed, true);
        }

        /// <summary>
        /// Combat: Active fighting using combat stance
        /// </summary>
        private void UpdateCombat()
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            // Lost player
            if (!CanSeePlayer() || distanceToPlayer > detectionRange * 2f)
            {
                Debug.Log($"[{gameObject.name}] Lost player in combat");
                ChangeState(AIState.Approach);
                return;
            }

            // Too far - return to approach
            if (distanceToPlayer > engagementDistance * 1.8f)
            {
                Debug.Log($"[{gameObject.name}] Player too far, re-engaging");
                ChangeState(AIState.Approach);
                return;
            }

            // Update last known position
            lastKnownPlayerPosition = playerTransform.position;

            // Check if sniper mode should be used (player fleeing and far away)
            bool playerFleeing = IsPlayerFleeing();
            bool sniperReady = Time.time > lastSniperTime + sniperCooldown;

            if (playerFleeing && distanceToPlayer > combatDistance * 1.5f && sniperReady)
            {
                // Force sniper mode when player is fleeing far away
                ExecuteSniperBehavior(distanceToPlayer);
            }
            else
            {
                // Execute normal combat stance behavior
                switch (combatStance)
                {
                    case CombatStance.Circler:
                        ExecuteCirclerBehavior(distanceToPlayer);
                        break;
                    case CombatStance.Sniper:
                        // Skip sniper if on cooldown or player not fleeing
                        if (sniperReady)
                            ExecuteSniperBehavior(distanceToPlayer);
                        else
                            ExecuteCirclerBehavior(distanceToPlayer); // Fall back to circling
                        break;
                    case CombatStance.Brawler:
                        ExecuteBrawlerBehavior(distanceToPlayer);
                        break;
                }
            }

            // Fire opportunistically in any mode if angle is right
            TryFireBroadsides();

            // Randomly change tactics occasionally
            if (Time.time > lastTacticChangeTime + tacticChangeInterval && Random.value < 0.3f)
            {
                ChooseCombatStance();
                lastTacticChangeTime = Time.time;
                Debug.Log($"[{gameObject.name}] Switching to {combatStance} stance");
            }
        }

        /// <summary>
        /// Ram: Aggressive ramming attack
        /// </summary>
        private void UpdateRam()
        {
            Vector3 interceptPoint = CalculateInterceptPoint();
            NavigateToPoint(interceptPoint, moveSpeed * ramSpeedMultiplier, false);

            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer > engagementDistance * 2f)
            {
                Debug.Log($"[{gameObject.name}] Ram target escaped");
                isRamming = false;
                ChangeState(AIState.Combat);
            }
        }

        /// <summary>
        /// Retreat: Low health escape
        /// </summary>
        private void UpdateRetreat()
        {
            // Head back to spawn with evasive maneuvers
            Vector3 retreatPoint = spawnPosition;

            // Add zigzag
            float zigzag = Mathf.Sin(Time.time * 3f) * 10f;
            Vector3 perpendicular = Vector3.Cross((spawnPosition - transform.position).normalized, Vector3.up);
            retreatPoint += perpendicular * zigzag;

            NavigateToPoint(retreatPoint, moveSpeed * 1.2f, true);

            // Fire opportunistically while retreating
            TryFireBroadsides();

            // Reached spawn or healed enough
            if (Vector3.Distance(transform.position, spawnPosition) < 20f ||
                (shipHealth != null && shipHealth.GetHealthPercent() > retreatThreshold + 0.3f))
            {
                Debug.Log($"[{gameObject.name}] Retreat complete, resuming patrol");
                ChangeState(AIState.Patrol);
                currentWaypoint = GetRandomPatrolPoint();
            }
        }

        #endregion

        #region Combat Stance Behaviors

        /// <summary>
        /// Circler: Maintains circular orbit around player, fires when lined up
        /// </summary>
        private void ExecuteCirclerBehavior(float distanceToPlayer)
        {
            // Maintain combat distance
            Vector3 targetPosition = GetCirclingPosition(distanceToPlayer);

            // Adjust speed based on distance
            float speed = moveSpeed * 0.5f;
            if (distanceToPlayer < minCombatDistance)
                speed = moveSpeed * 0.8f; // Speed up to back off
            else if (distanceToPlayer > combatDistance * 1.3f)
                speed = moveSpeed * 0.7f; // Speed up to close distance

            NavigateToPoint(targetPosition, speed, true);
        }

        /// <summary>
        /// Sniper: Stops, turns broadside, fires from distance when player is fleeing
        /// </summary>
        private void ExecuteSniperBehavior(float distanceToPlayer)
        {
            // Stop and turn broadside
            Vector3 holdPosition = GetBroadsidePosition(distanceToPlayer);

            // Check if we have a good shot
            if (HasGoodBroadsideAngle())
            {
                // Hold perfectly still for accurate shot
                NavigateToPoint(transform.position, 0f, false);
                Debug.Log($"[{gameObject.name}] Sniper: Holding position, firing at range {distanceToPlayer:F1}m");

                // Fire and mark sniper cooldown
                if (aiBroadside != null && aiBroadside.CanFire())
                {
                    lastSniperTime = Time.time;
                }
            }
            else
            {
                // Turn to get broadside angle, move slowly
                NavigateToPoint(holdPosition, moveSpeed * 0.2f, false);
                Debug.Log($"[{gameObject.name}] Sniper: Turning for broadside angle");
            }
        }

        /// <summary>
        /// Brawler: Aggressive close combat with frequent rams
        /// </summary>
        private void ExecuteBrawlerBehavior(float distanceToPlayer)
        {
            // Try to ram if opportunity arises
            if (!isRamming && ShouldRam())
            {
                Debug.Log($"[{gameObject.name}] Brawler: Initiating ram!");
                StartCoroutine(RamAttack());
                return;
            }

            // Get close and aggressive
            float aggressiveDistance = combatDistance * 0.7f;

            if (distanceToPlayer > aggressiveDistance)
            {
                // Chase aggressively
                Vector3 predictedPos = PredictPlayerPosition();
                NavigateToPoint(predictedPos, moveSpeed * 0.9f, true);
            }
            else if (distanceToPlayer < minCombatDistance * 0.8f)
            {
                // Too close, back off slightly
                Vector3 awayFromPlayer = (transform.position - playerTransform.position).normalized;
                Vector3 backoffPoint = transform.position + awayFromPlayer * 20f;
                NavigateToPoint(backoffPoint, moveSpeed * 0.7f, true);
            }
            else
            {
                // Circle at close range
                Vector3 circlePos = GetCirclingPosition(distanceToPlayer);
                NavigateToPoint(circlePos, moveSpeed * 0.6f, true);
            }
        }

        #endregion

        #region Combat Helpers

        /// <summary>
        /// Try to fire broadsides if conditions are met
        /// </summary>
        private void TryFireBroadsides()
        {
            if (aiBroadside == null || !aiBroadside.CanFire()) return;

            bool fireLeftSide;
            if (CanFireBroadsides(out fireLeftSide))
            {
                aiBroadside.FireBroadside(fireLeftSide);
            }
        }

        /// <summary>
        /// Check if we can fire broadsides
        /// </summary>
        private bool CanFireBroadsides(out bool fireLeftSide)
        {
            fireLeftSide = false;
            if (playerTransform == null) return false;

            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            // Extended range check (allow long-range sniper shots)
            if (distanceToPlayer < minCombatDistance * 0.8f)
                return false; // Too close

            // Angle check - must be perpendicular (broadside angle)
            Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
            float forwardDot = Vector3.Dot(-transform.forward, toPlayer);

            // Player must be perpendicular (not in front or behind)
            if (Mathf.Abs(forwardDot) > Mathf.Cos(maxFiringAngle * Mathf.Deg2Rad))
                return false;

            // Determine side (POTCO ships face backwards)
            float side = Vector3.Dot(toPlayer, transform.right);
            fireLeftSide = side > 0;

            return true;
        }

        /// <summary>
        /// Check if we have a good broadside angle
        /// </summary>
        private bool HasGoodBroadsideAngle()
        {
            if (playerTransform == null) return false;

            Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
            float forwardDot = Vector3.Dot(-transform.forward, toPlayer);

            // Good broadside is close to 90 degrees (perpendicular)
            return Mathf.Abs(forwardDot) < 0.3f; // Within ~72-108 degrees
        }

        /// <summary>
        /// Get a circling position around the player
        /// </summary>
        private Vector3 GetCirclingPosition(float currentDistance)
        {
            Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
            Vector3 perpendicular = Vector3.Cross(Vector3.up, toPlayer) * circleDirection;

            // Aim for combat distance
            float targetDistance = combatDistance;
            return playerTransform.position + perpendicular * targetDistance;
        }

        /// <summary>
        /// Get broadside position perpendicular to player
        /// </summary>
        private Vector3 GetBroadsidePosition(float currentDistance)
        {
            Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
            Vector3 perpendicular = Vector3.Cross(Vector3.up, toPlayer);

            // Stay on current side
            float currentSide = Vector3.Dot(perpendicular, transform.right);
            float side = currentSide > 0 ? 1f : -1f;

            return playerTransform.position + perpendicular * side * combatDistance;
        }

        /// <summary>
        /// Get intercept position ahead of player for chase
        /// </summary>
        private Vector3 GetInterceptBroadsidePosition()
        {
            if (playerRb == null) return GetBroadsidePosition(combatDistance);

            // Predict where player will be
            Vector3 playerVelocity = playerRb.linearVelocity;
            float interceptTime = 3f;
            Vector3 futurePlayerPos = playerTransform.position + playerVelocity * interceptTime;

            // Get perpendicular to predicted path
            Vector3 playerDirection = playerVelocity.normalized;
            if (playerDirection == Vector3.zero)
                playerDirection = playerTransform.forward;

            Vector3 perpendicular = Vector3.Cross(Vector3.up, playerDirection);

            // Choose closer side
            Vector3 toShip = (transform.position - futurePlayerPos).normalized;
            float side = Vector3.Dot(perpendicular, toShip) > 0 ? 1f : -1f;

            return futurePlayerPos + perpendicular * side * combatDistance * 0.8f;
        }

        /// <summary>
        /// Check if player is sailing in a straight line
        /// </summary>
        private bool IsPlayerSailingStraight()
        {
            if (playerRb == null) return false;

            float speed = playerRb.linearVelocity.magnitude;

            // If moving slowly, not sailing straight
            if (speed < playerMovementThreshold)
                return false;

            // Check if velocity matches forward direction (not turning much)
            Vector3 velocityDir = playerRb.linearVelocity.normalized;
            Vector3 forwardDir = playerTransform.forward;
            float alignment = Vector3.Dot(velocityDir, forwardDir);

            return alignment > 0.9f; // Pretty straight
        }

        /// <summary>
        /// Check if player is fleeing (sailing away from us)
        /// </summary>
        private bool IsPlayerFleeing()
        {
            if (playerRb == null) return false;

            float speed = playerRb.linearVelocity.magnitude;

            // Must be moving
            if (speed < playerMovementThreshold)
                return false;

            // Check if player is moving away from us
            Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
            Vector3 playerDirection = playerRb.linearVelocity.normalized;

            // If player's velocity is pointing away from us (negative dot product), they're fleeing
            float fleeingAlignment = Vector3.Dot(playerDirection, toPlayer);

            return fleeingAlignment > 0.7f; // Moving away from us
        }

        /// <summary>
        /// Predict player position based on velocity
        /// </summary>
        private Vector3 PredictPlayerPosition()
        {
            if (playerRb == null) return playerTransform.position;

            float predictionTime = 2f;
            Vector3 predicted = playerTransform.position + playerRb.linearVelocity * predictionTime;
            predicted.y = playerTransform.position.y;
            return predicted;
        }

        /// <summary>
        /// Calculate intercept point for ramming
        /// </summary>
        private Vector3 CalculateInterceptPoint()
        {
            if (playerRb == null) return playerTransform.position;

            float distance = Vector3.Distance(transform.position, playerTransform.position);
            float timeToIntercept = distance / (moveSpeed * ramSpeedMultiplier);

            Vector3 intercept = playerTransform.position + playerRb.linearVelocity * timeToIntercept;
            intercept.y = playerTransform.position.y;
            return intercept;
        }

        /// <summary>
        /// Should we attempt a ram?
        /// </summary>
        private bool ShouldRam()
        {
            if (isRamming) return false;

            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            // Only ram at close-medium range
            if (distanceToPlayer < minCombatDistance * 0.7f) return false; // Too close
            if (distanceToPlayer > combatDistance) return false; // Too far

            // Check alignment - must be facing player
            Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
            float alignment = Vector3.Dot(-transform.forward, toPlayer);

            // Well aligned?
            if (alignment < 0.85f) return false;

            // Higher chance for ram if well aligned (check every frame, not per-deltaTime)
            return Random.value < 0.05f; // 5% chance per frame when aligned
        }

        /// <summary>
        /// Should we retreat based on health?
        /// </summary>
        private bool ShouldRetreat()
        {
            if (shipHealth == null) return false;
            return shipHealth.GetHealthPercent() <= retreatThreshold;
        }

        #endregion

        #region Movement & Navigation

        /// <summary>
        /// Navigate to a target point
        /// </summary>
        private void NavigateToPoint(Vector3 targetPoint, float targetSpeed, bool avoidObstacles)
        {
            // Obstacle avoidance
            if (avoidObstacles)
            {
                DetectObstacles();
            }

            // Direction to target
            Vector3 direction = (targetPoint - transform.position);
            direction.y = 0;
            direction.Normalize();

            // Blend with avoidance (much stronger for ship collisions)
            if (avoidanceWeight > 0 && avoidObstacles)
            {
                direction = Vector3.Lerp(direction, avoidanceDirection, avoidanceWeight);
                direction.Normalize();
            }

            // Rotate towards target (POTCO ships face backwards)
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(-direction);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRotation,
                    rotateSpeed * Time.deltaTime
                );
            }

            // Adjust speed based on angle
            float angleToTarget = Vector3.Angle(-transform.forward, direction);
            float speedMultiplier = 1f;

            if (angleToTarget > 45f)
                speedMultiplier = 0.3f;
            else if (angleToTarget > 20f)
                speedMultiplier = 0.6f;

            // Dramatically slow down near ships (much stronger avoidance)
            if (avoidanceWeight > 0.7f)
                speedMultiplier *= 0.1f; // Almost stop when very close to ships
            else if (avoidanceWeight > 0.4f)
                speedMultiplier *= 0.3f; // Slow significantly when near ships
            else if (avoidanceWeight > 0f)
                speedMultiplier *= 0.6f; // Moderate slowdown

            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed * speedMultiplier, acceleration * Time.deltaTime);
        }

        /// <summary>
        /// Apply movement to rigidbody
        /// </summary>
        private void ApplyMovement()
        {
            if (rb != null)
            {
                Vector3 movement = -transform.forward * currentSpeed * Time.deltaTime;
                rb.MovePosition(rb.position + movement);
            }
        }

        /// <summary>
        /// Detect obstacles ahead - prioritizes ship collision detection
        /// </summary>
        private void DetectObstacles()
        {
            avoidanceWeight = 0f;
            avoidanceDirection = Vector3.zero;

            int rayCount = 7;
            float arcAngle = 90f;
            float minShipDistance = 15f; // Minimum safe distance from other ships

            for (int i = 0; i < rayCount; i++)
            {
                float angle = -arcAngle / 2f + (arcAngle / (rayCount - 1)) * i;
                Vector3 direction = Quaternion.Euler(0, angle, 0) * -transform.forward;

                RaycastHit hit;
                if (Physics.Raycast(transform.position + Vector3.up * 5f, direction, out hit, obstacleDetectionRange))
                {
                    // Check if we hit a ship (AI or player)
                    ShipController otherPlayerShip = hit.collider.GetComponentInParent<ShipController>();
                    ShipAIController otherAIShip = hit.collider.GetComponentInParent<ShipAIController>();

                    bool hitShip = false;

                    // Ignore self
                    if (otherPlayerShip != null && otherPlayerShip.transform.root == transform)
                        continue;
                    if (otherAIShip != null && otherAIShip.transform.root == transform)
                        continue;

                    // Check if we hit a ship
                    if (otherPlayerShip != null || otherAIShip != null)
                    {
                        hitShip = true;
                    }

                    // Ignore player ship when it's our target (allow close combat)
                    if (playerTransform != null && hit.collider.transform.root == playerTransform.root)
                        continue;

                    Vector3 avoidDir = (transform.position - hit.point).normalized;
                    avoidDir.y = 0;

                    float weight = 1f - (hit.distance / obstacleDetectionRange);

                    // Apply stronger avoidance for ships
                    if (hitShip)
                    {
                        // DRAMATICALLY increase weight when very close to another ship (exponential curve)
                        if (hit.distance < minShipDistance)
                        {
                            // Square the weight to make it exponentially stronger
                            float closenessFactor = 1f - (hit.distance / minShipDistance);
                            weight = Mathf.Lerp(weight, 1f, closenessFactor * closenessFactor);
                        }

                        // Ships get 2x avoidance priority vs terrain
                        weight *= 2.0f;

                        Debug.DrawRay(transform.position + Vector3.up * 5f, direction * hit.distance, Color.red);
                    }
                    else
                    {
                        Debug.DrawRay(transform.position + Vector3.up * 5f, direction * hit.distance, Color.yellow);
                    }

                    avoidanceDirection += avoidDir * weight;
                    avoidanceWeight += weight;
                }
            }

            if (avoidanceWeight > 0)
            {
                avoidanceDirection.Normalize();
                avoidanceWeight = Mathf.Clamp01(avoidanceWeight);
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Change to a new state
        /// </summary>
        private void ChangeState(AIState newState)
        {
            if (currentState != newState)
            {
                Debug.Log($"[{gameObject.name}] State: {currentState} → {newState}");
                currentState = newState;
                stateEnterTime = Time.time;
            }
        }

        /// <summary>
        /// Get random patrol point near spawn
        /// </summary>
        private Vector3 GetRandomPatrolPoint()
        {
            Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
            return spawnPosition + new Vector3(randomCircle.x, 0, randomCircle.y);
        }

        /// <summary>
        /// Check line of sight to player
        /// </summary>
        private bool CanSeePlayer()
        {
            if (playerTransform == null)
            {
                Debug.LogWarning($"[{gameObject.name}] CanSeePlayer: playerTransform is NULL!");
                return false;
            }

            // TEMPORARY: Skip raycast for testing - always return true if player exists
            return true;

            /* ORIGINAL CODE - UNCOMMENT TO RE-ENABLE LINE OF SIGHT:
            Vector3 rayStart = transform.position + Vector3.up * 5f;
            Vector3 direction = playerTransform.position - transform.position;
            RaycastHit hit;

            if (Physics.Raycast(rayStart, direction.normalized, out hit, detectionRange * 1.2f))
            {
                bool hasPlayerTag = hit.collider.CompareTag("Player");
                bool sameRoot = hit.collider.transform.root == playerTransform.root;

                // DEBUG: Show what we hit
                if (Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[{gameObject.name}] CanSeePlayer RAYCAST HIT: {hit.collider.name} (Tag: {hit.collider.tag}, HasPlayerTag: {hasPlayerTag}, SameRoot: {sameRoot})");
                }

                return hasPlayerTag || sameRoot;
            }

            // Raycast didn't hit anything
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"[{gameObject.name}] CanSeePlayer: Raycast hit NOTHING (distance: {direction.magnitude:F1}m, max: {detectionRange * 1.2f:F1}m)");
            }

            return false;
            */
        }

        /// <summary>
        /// Find player in scene - returns ship if player is sailing, otherwise returns player character
        /// </summary>
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

            if (player == null) return null;

            // Check if player is controlling a ship (parented to ship with ShipController)
            if (player.transform.parent != null)
            {
                ShipController shipController = player.transform.parent.GetComponent<ShipController>();
                if (shipController != null)
                {
                    // Player is sailing - target the ship instead
                    Debug.Log($"[{gameObject.name}] Player is sailing {player.transform.parent.name}, targeting ship");
                    return player.transform.parent;
                }
            }

            // Player is on foot
            return player.transform;
        }

        /// <summary>
        /// Ram attack coroutine
        /// </summary>
        private IEnumerator RamAttack()
        {
            isRamming = true;
            AIState previousState = currentState;
            ChangeState(AIState.Ram);

            yield return new WaitForSeconds(ramDuration);

            isRamming = false;
            ChangeState(previousState);
            Debug.Log($"[{gameObject.name}] Ram complete, returning to {previousState}");
        }

        /// <summary>
        /// Add ship hull collider for collision detection (same as ShipController)
        /// </summary>
        private void AddShipHullCollider()
        {
            // Check if hull collider already exists on root
            hullCollider = GetComponent<BoxCollider>();
            if (hullCollider != null)
            {
                Debug.Log($"[{gameObject.name}] Ship hull collider already exists");
                IgnorePlayerCollision(hullCollider);
                return;
            }

            // Calculate bounds from all mesh renderers
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[{gameObject.name}] No renderers found to calculate ship bounds");
                return;
            }

            Bounds combinedBounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                // Skip masts and sails for hull calculation
                if (renderer.name.ToLower().Contains("mast") ||
                    renderer.name.ToLower().Contains("sail"))
                    continue;

                combinedBounds.Encapsulate(renderer.bounds);
            }

            // Add box collider to root (where Rigidbody is)
            hullCollider = gameObject.AddComponent<BoxCollider>();
            hullCollider.center = transform.InverseTransformPoint(combinedBounds.center);
            hullCollider.size = new Vector3(
                combinedBounds.size.x / transform.lossyScale.x,
                combinedBounds.size.y / transform.lossyScale.y,
                combinedBounds.size.z / transform.lossyScale.z
            );

            // Ignore collision with player
            IgnorePlayerCollision(hullCollider);

            Debug.Log($"[{gameObject.name}] ✅ Added ship hull collider - Size: {hullCollider.size}, Center: {hullCollider.center}");
        }

        private void IgnorePlayerCollision(Collider shipCollider)
        {
            if (shipCollider == null) return;

            // Find player and ignore collision with hull
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Player.PlayerController pc = FindAnyObjectByType<Player.PlayerController>();
                if (pc != null) player = pc.gameObject;
            }

            if (player != null)
            {
                // Get all colliders on player (CharacterController and any others)
                Collider[] playerColliders = player.GetComponentsInChildren<Collider>();
                foreach (Collider playerCollider in playerColliders)
                {
                    if (playerCollider != null)
                    {
                        Physics.IgnoreCollision(shipCollider, playerCollider, true);
                    }
                }
            }
            else
            {
                // Player not found yet - will retry in Update()
                Debug.LogWarning($"[{gameObject.name}] Player not found for collision ignore - will retry");
            }
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            Vector3 spawnPos = Application.isPlaying ? spawnPosition : transform.position;

            // Patrol radius
            Gizmos.color = Color.green;
            DrawCircle(spawnPos, patrolRadius, 32);

            // Detection range
            Gizmos.color = Color.yellow;
            DrawCircle(transform.position, detectionRange, 32);

            // Combat distance
            Gizmos.color = Color.red;
            DrawCircle(transform.position, combatDistance, 32);

            // State indicator
            Gizmos.color = GetStateColor();
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 15f, 3f);

            if (Application.isPlaying && playerTransform != null)
            {
                // Line to player
                Gizmos.color = CanSeePlayer() ? Color.cyan : Color.gray;
                Gizmos.DrawLine(transform.position, playerTransform.position);

                // Current waypoint
                Gizmos.color = Color.white;
                Gizmos.DrawSphere(currentWaypoint, 2f);
            }
        }

        private Color GetStateColor()
        {
            switch (currentState)
            {
                case AIState.Patrol: return Color.green;
                case AIState.Approach: return Color.yellow;
                case AIState.Combat: return Color.red;
                case AIState.Ram: return Color.magenta;
                case AIState.Retreat: return Color.white;
                default: return Color.gray;
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
