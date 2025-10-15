using UnityEngine;
using System.Collections;

namespace POTCO
{
    /// <summary>
    /// Advanced Ship AI Controller with 8 distinct combat states
    /// States: Patrol → Chase → Flank/Circle/Sniper/Ram/Feint → Panic
    /// All states fire opportunistically when broadsides are aligned
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ShipAIController : MonoBehaviour
    {
        #region Inspector Settings

        [Header("AI State (Read-Only)")]
        [SerializeField] private AIState currentState = AIState.Patrol;

        [Header("Detection & Aggro")]
        [Tooltip("How far ship can detect player")]
        public float detectionRange = 1000f;
        [Tooltip("Maximum chase distance from spawn")]
        public float maxChaseDistance = 1500f;

        [Header("Movement")]
        public float moveSpeed = 90f;
        public float rotateSpeed = 20f;
        public float acceleration = 10f;

        [Header("Combat Distances")]
        [Tooltip("Flank-and-Broadside range (150-300m)")]
        public float flankMinDistance = 300f;
        public float flankMaxDistance = 500f;
        [Tooltip("Sniper range (500-1200m)")]
        public float sniperMinDistance = 700f;
        public float sniperMaxDistance = 1300f;
        [Tooltip("Circle orbit range")]
        public float circleMinDistance = 400f;
        public float circleMaxDistance = 700f;

        [Header("Broadside Settings")]
        [Tooltip("Maximum angle from perpendicular to fire (degrees)")]
        public float maxFiringAngle = 15f;
        [Tooltip("Time to aim and charge sniper shot")]
        public float sniperAimTime = 2f;

        [Header("Ram Settings")]
        public float ramDamage = 100f;
        public float ramSpeedMultiplier = 1.8f;
        public float ramMinTargetSpeed = 5f; // Ram only if target is slow

        [Header("Patrol Settings")]
        public float patrolRadius = 1000f;
        public float patrolWaitTime = 10f;

        [Header("Health Thresholds")]
        [Range(0f, 1f)] public float panicHealthThreshold = 0.25f;

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
        private float currentSpeed = 0f;
        private float stateEnterTime = 0f;
        private float stateTimer = 0f;

        // Circle/Orbit variables
        private float circleTargetDistance;
        private float circleAngleOffset;
        private float circleNoiseTime;
        private float circleNoiseFrequency = 0.3f;
        private float circleNoiseAmplitude = 20f;

        // Sniper variables
        private bool isSniperAiming = false;
        private float sniperAimStartTime = 0f;

        // Feint variables
        private Vector3 feintReturnPosition;

        // Obstacle avoidance (kept from original)
        private Vector3 avoidanceDirection = Vector3.zero;
        private float avoidanceWeight = 0f;
        private float obstacleDetectionRange = 30f;
        private BoxCollider hullCollider;

        #endregion

        #region Enums

        public enum AIState
        {
            Patrol,         // Wandering and scanning
            Chase,          // Intercepting target
            FlankBroadside, // Main attack: 150-300m broadside volleys
            Circle,         // Realistic imperfect orbit
            Sniper,         // Long range 500-1200m precision shots
            Ram,            // Collision attack on slow targets
            Feint,          // Fake flee then turn
            Panic           // Low health escape
        }

        #endregion

        #region Initialization

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            shipHealth = GetComponent<ShipHealth>();
            aiBroadside = GetComponent<AIBroadside>();
            spawnPosition = transform.position;

            // Configure rigidbody
            if (rb != null)
            {
                rb.useGravity = false;
                rb.isKinematic = true;
                rb.linearDamping = 1f;
                rb.angularDamping = 2f;
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }

            AddShipHullCollider();

            // Find player
            if (playerTransform == null)
            {
                playerTransform = FindPlayer();
            }

            if (playerTransform != null)
            {
                playerRb = playerTransform.GetComponent<Rigidbody>();
            }

            // Initialize circle variables
            circleTargetDistance = Random.Range(circleMinDistance, circleMaxDistance);
            circleAngleOffset = Random.Range(0f, 360f);
            circleNoiseTime = Random.Range(0f, 100f);

            // Start patrol
            currentWaypoint = GetRandomPatrolPoint();
            stateEnterTime = Time.time;

            Debug.Log($"[{gameObject.name}] AI initialized - Ready for combat");
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            // Handle player collision ignore
            if (hullCollider != null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player == null)
                {
                    Player.PlayerController pc = FindAnyObjectByType<Player.PlayerController>();
                    if (pc != null) player = pc.gameObject;
                }

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

            // Re-find player if lost
            if (playerTransform == null || Time.frameCount % 120 == 0)
            {
                Transform newTarget = FindPlayer();
                if (newTarget != playerTransform)
                {
                    playerTransform = newTarget;
                    if (playerTransform != null)
                    {
                        playerRb = playerTransform.GetComponent<Rigidbody>();
                    }
                }
                if (playerTransform == null) return;
            }

            stateTimer = Time.time - stateEnterTime;

            // Check for panic (overrides all states)
            if (ShouldPanic() && currentState != AIState.Panic)
            {
                ChangeState(AIState.Panic);
            }

            // State machine
            switch (currentState)
            {
                case AIState.Patrol:
                    UpdatePatrol();
                    break;
                case AIState.Chase:
                    UpdateChase();
                    break;
                case AIState.FlankBroadside:
                    UpdateFlankBroadside();
                    break;
                case AIState.Circle:
                    UpdateCircle();
                    break;
                case AIState.Sniper:
                    UpdateSniper();
                    break;
                case AIState.Ram:
                    UpdateRam();
                    break;
                case AIState.Feint:
                    UpdateFeint();
                    break;
                case AIState.Panic:
                    UpdatePanic();
                    break;
            }

            // OPPORTUNISTIC FIRING - fire ANY time broadsides are lined up
            TryOpportunisticFire();

            // Apply movement
            ApplyMovement();
        }

        #endregion

        #region State Updates

        /// <summary>
        /// Patrol: Wander and scan for targets
        /// </summary>
        private void UpdatePatrol()
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            // Detect player
            if (distanceToPlayer <= detectionRange)
            {
                Debug.Log($"[{gameObject.name}] Player detected! Engaging...");
                ChangeState(AIState.Chase);
                return;
            }

            // Navigate to waypoint
            NavigateToPoint(currentWaypoint, moveSpeed * 0.6f, true);

            // Pick new waypoint
            if (Vector3.Distance(transform.position, currentWaypoint) < 15f || stateTimer > patrolWaitTime)
            {
                currentWaypoint = GetRandomPatrolPoint();
                stateEnterTime = Time.time;
            }
        }

        /// <summary>
        /// Chase: Intercept target with velocity prediction
        /// </summary>
        private void UpdateChase()
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            float distanceFromSpawn = Vector3.Distance(transform.position, spawnPosition);

            // Lost player or too far
            if (distanceToPlayer > detectionRange * 1.5f || distanceFromSpawn > maxChaseDistance)
            {
                Debug.Log($"[{gameObject.name}] Lost target or too far, returning to patrol");
                ChangeState(AIState.Patrol);
                currentWaypoint = spawnPosition;
                return;
            }

            // Only check for state changes every 2 seconds to avoid rapid switching
            if (stateTimer < 2f)
            {
                // Continue chasing with lead prediction
                NavigateToPoint(PredictInterceptPoint(), moveSpeed, true);
                return;
            }

            // Choose combat state based on distance and opportunity
            if (distanceToPlayer >= sniperMinDistance && distanceToPlayer <= sniperMaxDistance)
            {
                // Sniper range - check if player is fleeing
                if (IsPlayerFleeing() && Random.value < 0.4f)
                {
                    ChangeState(AIState.Sniper);
                    return;
                }
            }

            if (distanceToPlayer >= flankMinDistance && distanceToPlayer <= flankMaxDistance)
            {
                // Flank range - primary combat mode
                if (Random.value < 0.5f)
                {
                    ChangeState(AIState.FlankBroadside);
                    return;
                }
                else
                {
                    ChangeState(AIState.Circle);
                    return;
                }
            }

            if (distanceToPlayer < flankMinDistance)
            {
                // Close range - chance for ram or feint
                if (ShouldRam() && Random.value < 0.3f)
                {
                    ChangeState(AIState.Ram);
                    return;
                }

                if (Random.value < 0.2f)
                {
                    ChangeState(AIState.Feint);
                    return;
                }

                // Default to circle at close range
                ChangeState(AIState.Circle);
                return;
            }

            // Continue chasing with lead prediction (fallback if no state selected)
            NavigateToPoint(PredictInterceptPoint(), moveSpeed, true);
        }

        /// <summary>
        /// Flank-and-Broadside: Position for 60-120° angle, 150-300m distance, chain volleys
        /// </summary>
        private void UpdateFlankBroadside()
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            // Exit conditions
            if (distanceToPlayer > flankMaxDistance * 1.2f)
            {
                ChangeState(AIState.Chase);
                return;
            }

            if (distanceToPlayer < flankMinDistance * 0.8f)
            {
                ChangeState(AIState.Circle);
                return;
            }

            // Timeout after 15 seconds - switch tactics
            if (stateTimer > 15f)
            {
                if (Random.value < 0.5f)
                    ChangeState(AIState.Circle);
                else
                    ChangeState(AIState.Feint);
                return;
            }

            // Position for flank angle (60-120 degrees from target heading)
            Vector3 flankPosition = CalculateFlankPosition(distanceToPlayer);
            NavigateToPoint(flankPosition, moveSpeed * 0.5f, true);
        }

        /// <summary>
        /// Circle: Realistic imperfect orbit around player
        /// </summary>
        private void UpdateCircle()
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            // Exit if too far
            if (distanceToPlayer > circleMaxDistance * 1.5f)
            {
                ChangeState(AIState.Chase);
                return;
            }

            // Timeout after 12 seconds
            if (stateTimer > 12f)
            {
                // Choose next state randomly
                float roll = Random.value;
                if (roll < 0.4f && distanceToPlayer >= flankMinDistance)
                    ChangeState(AIState.FlankBroadside);
                else if (roll < 0.6f)
                    ChangeState(AIState.Feint);
                else
                    ChangeState(AIState.Chase);
                return;
            }

            // Calculate imperfect circle position
            circleNoiseTime += Time.deltaTime * circleNoiseFrequency;

            // Add Perlin noise for realistic imperfection
            float noiseAngle = Mathf.PerlinNoise(circleNoiseTime, 0f) * circleNoiseAmplitude;
            float noiseDistance = Mathf.PerlinNoise(0f, circleNoiseTime) * 30f;

            float currentAngle = circleAngleOffset + (Time.time * 10f) + noiseAngle;
            float currentDistance = circleTargetDistance + noiseDistance;

            Vector3 offset = new Vector3(
                Mathf.Cos(currentAngle * Mathf.Deg2Rad),
                0f,
                Mathf.Sin(currentAngle * Mathf.Deg2Rad)
            ) * currentDistance;

            Vector3 targetPosition = playerTransform.position + offset;

            // Navigate with moderate speed
            NavigateToPoint(targetPosition, moveSpeed * 0.6f, true);
        }

        /// <summary>
        /// Sniper: Long range 500-1200m, aim and fire precise volleys
        /// </summary>
        private void UpdateSniper()
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            // Exit if target too close or too far
            if (distanceToPlayer < sniperMinDistance || distanceToPlayer > sniperMaxDistance * 1.2f)
            {
                isSniperAiming = false;
                ChangeState(AIState.Chase);
                return;
            }

            // Check if we have a good broadside angle
            if (!HasGoodBroadsideAngle())
            {
                // Turn to get broadside angle
                Vector3 broadsidePosition = CalculateBroadsidePosition(distanceToPlayer);
                NavigateToPoint(broadsidePosition, moveSpeed * 0.3f, false);
                isSniperAiming = false;
            }
            else
            {
                // Hold position and aim
                if (!isSniperAiming)
                {
                    isSniperAiming = true;
                    sniperAimStartTime = Time.time;
                    Debug.Log($"[{gameObject.name}] Sniper: Aiming at {distanceToPlayer:F0}m...");
                }

                NavigateToPoint(transform.position, 0f, false);

                // After aim time, fire
                if (Time.time >= sniperAimStartTime + sniperAimTime)
                {
                    Debug.Log($"[{gameObject.name}] Sniper: Firing!");
                    // Fire happens via TryOpportunisticFire()

                    // Reposition after shot
                    isSniperAiming = false;
                    ChangeState(AIState.Circle);
                }
            }
        }

        /// <summary>
        /// Ram: Collision intercept on slow/disabled targets
        /// </summary>
        private void UpdateRam()
        {
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            // Exit if target is too fast now or too far
            if (!ShouldRam() || distanceToPlayer > flankMaxDistance)
            {
                Debug.Log($"[{gameObject.name}] Ram cancelled");
                ChangeState(AIState.Chase);
                return;
            }

            // Timeout after 5 seconds
            if (stateTimer > 5f)
            {
                ChangeState(AIState.FlankBroadside);
                return;
            }

            // Calculate collision intercept
            Vector3 ramPoint = CalculateRamIntercept();
            NavigateToPoint(ramPoint, moveSpeed * ramSpeedMultiplier, false);
        }

        /// <summary>
        /// Feint: Fake flee then quickly turn to flank
        /// </summary>
        private void UpdateFeint()
        {
            // First 3 seconds: fake flee
            if (stateTimer < 3f)
            {
                // Flee away from player
                Vector3 fleeDirection = (transform.position - playerTransform.position).normalized;
                Vector3 fleePoint = transform.position + fleeDirection * 50f;
                NavigateToPoint(fleePoint, moveSpeed * 1.2f, true);
            }
            else if (stateTimer < 6f)
            {
                // Next 3 seconds: turn sharply to flank
                float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
                Vector3 flankPosition = CalculateFlankPosition(distanceToPlayer);
                NavigateToPoint(flankPosition, moveSpeed * 0.8f, true);
            }
            else
            {
                // After 6 seconds, switch to flank or circle
                float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
                if (distanceToPlayer >= flankMinDistance && distanceToPlayer <= flankMaxDistance)
                    ChangeState(AIState.FlankBroadside);
                else
                    ChangeState(AIState.Circle);
            }
        }

        /// <summary>
        /// Panic: Max speed escape when low health
        /// </summary>
        private void UpdatePanic()
        {
            // Head to spawn with evasive zigzag
            Vector3 retreatPoint = spawnPosition;
            float zigzag = Mathf.Sin(Time.time * 3f) * 15f;
            Vector3 perpendicular = Vector3.Cross((spawnPosition - transform.position).normalized, Vector3.up);
            retreatPoint += perpendicular * zigzag;

            NavigateToPoint(retreatPoint, moveSpeed * 1.3f, true);

            // Recovered health or reached spawn
            if (Vector3.Distance(transform.position, spawnPosition) < 20f ||
                (shipHealth != null && shipHealth.GetHealthPercent() > panicHealthThreshold + 0.3f))
            {
                Debug.Log($"[{gameObject.name}] Panic over, resuming patrol");
                ChangeState(AIState.Patrol);
                currentWaypoint = GetRandomPatrolPoint();
            }
        }

        #endregion

        #region Combat Helpers

        /// <summary>
        /// Opportunistic firing - fire ANY time broadsides are lined up
        /// </summary>
        private void TryOpportunisticFire()
        {
            if (aiBroadside == null || !aiBroadside.CanFire()) return;
            if (playerTransform == null) return;

            bool fireLeftSide;
            if (CanFireBroadsides(out fireLeftSide))
            {
                aiBroadside.FireBroadside(fireLeftSide);
                Debug.Log($"[{gameObject.name}] Opportunistic fire - {(fireLeftSide ? "LEFT" : "RIGHT")} broadside!");
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

            // Must be in reasonable range (not too close)
            if (distanceToPlayer < 50f) return false;

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
        /// Check if we have a good broadside angle (perpendicular to target)
        /// </summary>
        private bool HasGoodBroadsideAngle()
        {
            if (playerTransform == null) return false;

            Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
            float forwardDot = Vector3.Dot(-transform.forward, toPlayer);

            // Good broadside is close to 90 degrees (perpendicular)
            return Mathf.Abs(forwardDot) < 0.3f;
        }

        /// <summary>
        /// Calculate flank position (60-120° from target heading)
        /// </summary>
        private Vector3 CalculateFlankPosition(float distance)
        {
            // Get target heading
            Vector3 targetForward = playerTransform.forward;

            // Choose flank angle (60-120 degrees)
            float flankAngle = Random.Range(60f, 120f);
            if (Random.value > 0.5f) flankAngle = -flankAngle;

            // Calculate position at flank angle
            Quaternion flankRotation = Quaternion.Euler(0, flankAngle, 0);
            Vector3 flankDirection = flankRotation * targetForward;

            float targetDistance = Mathf.Clamp(distance, flankMinDistance, flankMaxDistance);
            return playerTransform.position - flankDirection * targetDistance;
        }

        /// <summary>
        /// Calculate broadside position (perpendicular to player)
        /// </summary>
        private Vector3 CalculateBroadsidePosition(float distance)
        {
            Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
            Vector3 perpendicular = Vector3.Cross(Vector3.up, toPlayer);

            // Stay on current side
            float currentSide = Vector3.Dot(perpendicular, transform.right);
            float side = currentSide > 0 ? 1f : -1f;

            return playerTransform.position + perpendicular * side * distance;
        }

        /// <summary>
        /// Predict intercept point for chase
        /// </summary>
        private Vector3 PredictInterceptPoint()
        {
            if (playerRb == null) return playerTransform.position;

            float predictionTime = 3f;
            Vector3 predicted = playerTransform.position + playerRb.linearVelocity * predictionTime;
            predicted.y = playerTransform.position.y;
            return predicted;
        }

        /// <summary>
        /// Calculate ram intercept point
        /// </summary>
        private Vector3 CalculateRamIntercept()
        {
            if (playerRb == null) return playerTransform.position;

            float distance = Vector3.Distance(transform.position, playerTransform.position);
            float timeToIntercept = distance / (moveSpeed * ramSpeedMultiplier);

            Vector3 intercept = playerTransform.position + playerRb.linearVelocity * timeToIntercept;
            intercept.y = playerTransform.position.y;
            return intercept;
        }

        /// <summary>
        /// Check if player is fleeing (moving away from us)
        /// </summary>
        private bool IsPlayerFleeing()
        {
            if (playerRb == null) return false;

            float speed = playerRb.linearVelocity.magnitude;
            if (speed < 5f) return false;

            Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
            Vector3 playerDirection = playerRb.linearVelocity.normalized;

            float fleeingAlignment = Vector3.Dot(playerDirection, toPlayer);
            return fleeingAlignment > 0.7f;
        }

        /// <summary>
        /// Should we attempt a ram?
        /// </summary>
        private bool ShouldRam()
        {
            if (playerRb == null) return false;

            float playerSpeed = playerRb.linearVelocity.magnitude;
            if (playerSpeed > ramMinTargetSpeed) return false;

            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer < 30f || distanceToPlayer > flankMinDistance) return false;

            // Check alignment
            Vector3 toPlayer = (playerTransform.position - transform.position).normalized;
            float alignment = Vector3.Dot(-transform.forward, toPlayer);

            return alignment > 0.85f;
        }

        /// <summary>
        /// Should we panic based on health?
        /// </summary>
        private bool ShouldPanic()
        {
            if (shipHealth == null) return false;
            return shipHealth.GetHealthPercent() <= panicHealthThreshold;
        }

        #endregion

        #region Movement & Navigation

        /// <summary>
        /// Navigate to a target point
        /// </summary>
        private void NavigateToPoint(Vector3 targetPoint, float targetSpeed, bool avoidObstacles)
        {
            // Obstacle avoidance (kept from original)
            if (avoidObstacles)
            {
                DetectObstacles();
            }

            // Direction to target
            Vector3 direction = (targetPoint - transform.position);
            direction.y = 0;
            direction.Normalize();

            // Blend with avoidance
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

            // Slow down near ships
            if (avoidanceWeight > 0.7f)
                speedMultiplier *= 0.1f;
            else if (avoidanceWeight > 0.4f)
                speedMultiplier *= 0.3f;
            else if (avoidanceWeight > 0f)
                speedMultiplier *= 0.6f;

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
        /// Detect obstacles ahead (kept from original)
        /// </summary>
        private void DetectObstacles()
        {
            avoidanceWeight = 0f;
            avoidanceDirection = Vector3.zero;

            int rayCount = 7;
            float arcAngle = 90f;
            float minShipDistance = 15f;

            for (int i = 0; i < rayCount; i++)
            {
                float angle = -arcAngle / 2f + (arcAngle / (rayCount - 1)) * i;
                Vector3 direction = Quaternion.Euler(0, angle, 0) * -transform.forward;

                RaycastHit hit;
                if (Physics.Raycast(transform.position + Vector3.up * 5f, direction, out hit, obstacleDetectionRange))
                {
                    // Ignore cannonballs - don't avoid projectiles
                    if (hit.collider.GetComponent<CannonProjectile>() != null)
                        continue;

                    ShipController otherPlayerShip = hit.collider.GetComponentInParent<ShipController>();
                    ShipAIController otherAIShip = hit.collider.GetComponentInParent<ShipAIController>();

                    bool hitShip = false;

                    if (otherPlayerShip != null && otherPlayerShip.transform.root == transform)
                        continue;
                    if (otherAIShip != null && otherAIShip.transform.root == transform)
                        continue;

                    if (otherPlayerShip != null || otherAIShip != null)
                    {
                        hitShip = true;
                    }

                    if (playerTransform != null && hit.collider.transform.root == playerTransform.root)
                        continue;

                    Vector3 avoidDir = (transform.position - hit.point).normalized;
                    avoidDir.y = 0;

                    float weight = 1f - (hit.distance / obstacleDetectionRange);

                    if (hitShip)
                    {
                        if (hit.distance < minShipDistance)
                        {
                            float closenessFactor = 1f - (hit.distance / minShipDistance);
                            weight = Mathf.Lerp(weight, 1f, closenessFactor * closenessFactor);
                        }
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

        private void ChangeState(AIState newState)
        {
            if (currentState != newState)
            {
                Debug.Log($"[{gameObject.name}] State: {currentState} → {newState}");
                currentState = newState;
                stateEnterTime = Time.time;

                // Reset state-specific variables
                if (newState == AIState.Circle)
                {
                    circleTargetDistance = Random.Range(circleMinDistance, circleMaxDistance);
                    circleAngleOffset = Random.Range(0f, 360f);
                }
            }
        }

        private Vector3 GetRandomPatrolPoint()
        {
            Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
            return spawnPosition + new Vector3(randomCircle.x, 0, randomCircle.y);
        }

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

            // Check if player is controlling a ship
            if (player.transform.parent != null)
            {
                ShipController shipController = player.transform.parent.GetComponent<ShipController>();
                if (shipController != null)
                {
                    return player.transform.parent;
                }
            }

            return player.transform;
        }

        private void AddShipHullCollider()
        {
            hullCollider = GetComponent<BoxCollider>();
            if (hullCollider != null)
            {
                IgnorePlayerCollision(hullCollider);
                return;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds combinedBounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                if (renderer.name.ToLower().Contains("mast") ||
                    renderer.name.ToLower().Contains("sail"))
                    continue;

                combinedBounds.Encapsulate(renderer.bounds);
            }

            hullCollider = gameObject.AddComponent<BoxCollider>();
            hullCollider.center = transform.InverseTransformPoint(combinedBounds.center);
            hullCollider.size = new Vector3(
                combinedBounds.size.x / transform.lossyScale.x,
                combinedBounds.size.y / transform.lossyScale.y,
                combinedBounds.size.z / transform.lossyScale.z
            );

            IgnorePlayerCollision(hullCollider);
        }

        private void IgnorePlayerCollision(Collider shipCollider)
        {
            if (shipCollider == null) return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Player.PlayerController pc = FindAnyObjectByType<Player.PlayerController>();
                if (pc != null) player = pc.gameObject;
            }

            if (player != null)
            {
                Collider[] playerColliders = player.GetComponentsInChildren<Collider>();
                foreach (Collider playerCollider in playerColliders)
                {
                    if (playerCollider != null)
                    {
                        Physics.IgnoreCollision(shipCollider, playerCollider, true);
                    }
                }
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

            // Flank range
            Gizmos.color = Color.red;
            DrawCircle(transform.position, flankMinDistance, 32);
            DrawCircle(transform.position, flankMaxDistance, 32);

            // State indicator
            Gizmos.color = GetStateColor();
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 15f, 3f);

            if (Application.isPlaying && playerTransform != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, playerTransform.position);
            }
        }

        private Color GetStateColor()
        {
            switch (currentState)
            {
                case AIState.Patrol: return Color.green;
                case AIState.Chase: return Color.yellow;
                case AIState.FlankBroadside: return Color.red;
                case AIState.Circle: return Color.blue;
                case AIState.Sniper: return Color.magenta;
                case AIState.Ram: return new Color(1f, 0.5f, 0f); // Orange
                case AIState.Feint: return Color.cyan;
                case AIState.Panic: return Color.white;
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
