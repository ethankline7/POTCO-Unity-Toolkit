using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace POTCO
{
    /// <summary>
    /// AI broadside firing system for enemy ships
    /// Plays cannon animations and spawns projectiles
    /// </summary>
    public class AIBroadside : MonoBehaviour
    {
        [Header("Broadside Cannons")]
        [Tooltip("Left side broadside cannons")]
        public List<GameObject> leftBroadsideCannons = new List<GameObject>();
        [Tooltip("Right side broadside cannons")]
        public List<GameObject> rightBroadsideCannons = new List<GameObject>();

        [Header("Projectile Settings")]
        [Tooltip("Cannonball prefab")]
        public GameObject cannonballPrefab;
        [Tooltip("How long cannonball takes to reach target (seconds)")]
        public float flightTime = 2.5f;
        [Tooltip("Min delay between firing each cannon")]
        public float minCannonDelay = 0.1f;
        [Tooltip("Max delay between firing each cannon")]
        public float maxCannonDelay = 0.4f;
        [Tooltip("Random offset for dodgeability (meters)")]
        public float randomOffset = 5f;

        [Header("Cooldown")]
        [Tooltip("Cooldown between volleys")]
        public float volleyCooldown = 8f;
        private float lastFireTime = -999f;

        [Header("Angle Validation")]
        [Tooltip("Maximum angle deviation before stopping volley (degrees)")]
        public float maxAngleDeviation = 45f;

        // Animation clips
        private AnimationClip cannonOpenClip;
        private AnimationClip cannonFireClip;
        private AnimationClip cannonCloseClip;

        // Internal state
        private bool isFiring = false;
        private bool currentFiringSide = false; // true = left, false = right

        private void Start()
        {
            Debug.Log($"[AIBroadside] Start() called for {gameObject.name}");
            Debug.Log($"[AIBroadside] Initial cannon counts - Left: {leftBroadsideCannons.Count}, Right: {rightBroadsideCannons.Count}");

            LoadAnimationClips();
            LoadCannonballPrefab();

            Debug.Log($"[AIBroadside] Initialization complete. Clips loaded: open={cannonOpenClip != null}, fire={cannonFireClip != null}, close={cannonCloseClip != null}");
            Debug.Log($"[AIBroadside] Cannonball prefab loaded: {cannonballPrefab != null}");
        }

        /// <summary>
        /// Load animation clips from Resources
        /// </summary>
        private void LoadAnimationClips()
        {
            cannonOpenClip = Resources.Load<AnimationClip>("phase_3/models/shipParts/pir_a_shp_can_broadside_open");
            cannonFireClip = Resources.Load<AnimationClip>("phase_3/models/shipParts/pir_a_shp_can_broadside_fire");
            cannonCloseClip = Resources.Load<AnimationClip>("phase_3/models/shipParts/pir_a_shp_can_broadside_close");

            if (cannonOpenClip == null || cannonFireClip == null || cannonCloseClip == null)
            {
                Debug.LogWarning($"[AIBroadside] Failed to load some animation clips for {gameObject.name}");
            }
        }

        /// <summary>
        /// Load cannonball prefab from Resources
        /// </summary>
        private void LoadCannonballPrefab()
        {
            if (cannonballPrefab == null)
            {
                cannonballPrefab = Resources.Load<GameObject>("phase_3/models/ammunition/cannonball");
                if (cannonballPrefab == null)
                {
                    Debug.LogWarning($"[AIBroadside] Failed to load cannonball prefab for {gameObject.name}");
                }
            }
        }

        /// <summary>
        /// Check if ready to fire (cooldown elapsed)
        /// </summary>
        public bool CanFire()
        {
            return !isFiring && Time.time >= lastFireTime + volleyCooldown;
        }

        /// <summary>
        /// Check if currently firing a volley
        /// </summary>
        public bool IsFiring()
        {
            return isFiring;
        }

        /// <summary>
        /// Fire broadside cannons
        /// </summary>
        /// <param name="isLeftSide">True to fire left side, false for right side</param>
        public void FireBroadside(bool isLeftSide)
        {
            Debug.Log($"[AIBroadside] FireBroadside called for {gameObject.name}, side: {(isLeftSide ? "left" : "right")}");

            if (!CanFire())
            {
                Debug.Log($"[AIBroadside] {gameObject.name} on cooldown, cannot fire yet. isFiring={isFiring}, lastFireTime={lastFireTime}, Time.time={Time.time}, cooldown={volleyCooldown}");
                return;
            }

            List<GameObject> cannonsToFire = isLeftSide ? leftBroadsideCannons : rightBroadsideCannons;

            Debug.Log($"[AIBroadside] Left cannons: {leftBroadsideCannons.Count}, Right cannons: {rightBroadsideCannons.Count}");

            if (cannonsToFire.Count == 0)
            {
                Debug.LogWarning($"[AIBroadside] No cannons found on {(isLeftSide ? "left" : "right")} side of {gameObject.name}");
                return;
            }

            // Store which side we're firing for validation
            currentFiringSide = isLeftSide;

            Debug.Log($"[AIBroadside] {gameObject.name} firing {(isLeftSide ? "left" : "right")} broadside with {cannonsToFire.Count} cannons");
            StartCoroutine(FireBroadsideCannons(cannonsToFire));
        }

        /// <summary>
        /// Fire all cannons in sequence, checking angle before each shot
        /// </summary>
        private IEnumerator FireBroadsideCannons(List<GameObject> cannons)
        {
            isFiring = true;
            lastFireTime = Time.time;

            int cannonsFired = 0;
            foreach (GameObject cannon in cannons)
            {
                if (cannon != null)
                {
                    // Check if we still have a clear shot before firing each cannon
                    if (!HasClearShot())
                    {
                        Debug.Log($"[AIBroadside] {gameObject.name} lost clear shot after firing {cannonsFired} cannons. Stopping volley.");
                        break;
                    }

                    StartCoroutine(PlayCannonSequence(cannon));
                    cannonsFired++;
                    yield return new WaitForSeconds(Random.Range(minCannonDelay, maxCannonDelay));
                }
            }

            Debug.Log($"[AIBroadside] {gameObject.name} volley complete. Fired {cannonsFired}/{cannons.Count} cannons.");
            isFiring = false;
        }

        /// <summary>
        /// Play cannon animation sequence and spawn projectile
        /// </summary>
        private IEnumerator PlayCannonSequence(GameObject cannon)
        {
            // Find muzzle point
            Transform muzzle = FindMuzzlePoint(cannon.transform);
            if (muzzle == null)
            {
                Debug.LogWarning($"[AIBroadside] No muzzle point found for {cannon.name}");
                yield break;
            }

            // Get or add Animation component
            Animation anim = cannon.GetComponent<Animation>();
            if (anim == null)
            {
                Debug.Log($"[AIBroadside] Adding Animation component to {cannon.name}");
                anim = cannon.AddComponent<Animation>();
            }

            // Disable auto-play to prevent looping
            anim.playAutomatically = false;

            // Try to play animations if clips are available
            bool hasAnimations = cannonOpenClip != null && cannonFireClip != null && cannonCloseClip != null;

            if (hasAnimations)
            {
                // Add clips to animation component with WrapMode.Once (no looping)
                if (!anim.GetClip("open"))
                {
                    anim.AddClip(cannonOpenClip, "open");
                    anim["open"].wrapMode = WrapMode.Once;
                }
                if (!anim.GetClip("fire"))
                {
                    anim.AddClip(cannonFireClip, "fire");
                    anim["fire"].wrapMode = WrapMode.Once;
                }
                if (!anim.GetClip("close"))
                {
                    anim.AddClip(cannonCloseClip, "close");
                    anim["close"].wrapMode = WrapMode.Once;
                }

                // Stop any playing animations first
                anim.Stop();

                // Open cannons
                anim.Play("open");
                yield return new WaitForSeconds(cannonOpenClip.length);

                // Fire animation
                anim.Play("fire");
                yield return new WaitForSeconds(cannonFireClip.length * 0.3f);
                SpawnCannonball(muzzle);
                yield return new WaitForSeconds(cannonFireClip.length * 0.7f);

                // Close cannons
                anim.Play("close");
                yield return new WaitForSeconds(cannonCloseClip.length);

                // Stop animation completely when done
                anim.Stop();
            }
            else
            {
                // No animations available - just spawn projectile after short delay
                Debug.Log($"[AIBroadside] No animations available, firing {cannon.name} without animation");
                yield return new WaitForSeconds(0.2f);
                SpawnCannonball(muzzle);
                yield return new WaitForSeconds(0.3f);
            }
        }

        /// <summary>
        /// Find the muzzle/exit point of a cannon
        /// </summary>
        private Transform FindMuzzlePoint(Transform cannon)
        {
            // Try common muzzle point names
            Transform muzzle = FindChildRecursive(cannon, "muzzle");
            if (muzzle == null) muzzle = FindChildRecursive(cannon, "def_muzzle");
            if (muzzle == null) muzzle = FindChildRecursive(cannon, "cannon_exit");
            if (muzzle == null) muzzle = FindChildRecursive(cannon, "cannonExitPoint");

            // If still not found, look for def_cannon_updown (pitch bone) as fallback
            if (muzzle == null) muzzle = FindChildRecursive(cannon, "def_cannon_updown");

            // Last resort: use the cannon transform itself
            if (muzzle == null) muzzle = cannon;

            return muzzle;
        }

        /// <summary>
        /// Recursively find child by name
        /// </summary>
        private Transform FindChildRecursive(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase))
                    return child;

                Transform found = FindChildRecursive(child, childName);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Spawn a cannonball projectile
        /// </summary>
        private void SpawnCannonball(Transform muzzle)
        {
            if (cannonballPrefab == null)
            {
                Debug.LogWarning($"[AIBroadside] Cannot spawn cannonball - prefab not loaded");
                return;
            }

            // Spawn cannonball at muzzle position
            GameObject cannonball = Instantiate(cannonballPrefab, muzzle.position, muzzle.rotation);

            // Make cannonball more visible for testing
            cannonball.transform.localScale = Vector3.one * 2.5f; // Bigger cannonball

            // Calculate exact velocity to hit target
            Vector3 launchVelocity = CalculateLaunchVelocity(muzzle.position);

            // Add or get Rigidbody
            Rigidbody rb = cannonball.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = cannonball.AddComponent<Rigidbody>();
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            // Ensure cannonball has a collider
            Collider cannonballCollider = cannonball.GetComponent<Collider>();
            if (cannonballCollider == null)
            {
                SphereCollider sphere = cannonball.AddComponent<SphereCollider>();
                sphere.radius = 0.15f; // Small sphere for cannonball
                cannonballCollider = sphere;
                Debug.Log($"[AIBroadside] Added SphereCollider to cannonball");
            }

            // Launch projectile with calculated velocity
            rb.linearVelocity = launchVelocity;
            rb.useGravity = true;

            // Add CannonProjectile component for collision handling
            CannonProjectile projectile = cannonball.GetComponent<CannonProjectile>();
            if (projectile == null)
            {
                projectile = cannonball.AddComponent<CannonProjectile>();
            }

            // Add bright trail renderer for visibility
            TrailRenderer trail = cannonball.GetComponent<TrailRenderer>();
            if (trail == null)
            {
                trail = cannonball.AddComponent<TrailRenderer>();
                trail.time = 1.5f; // Trail duration
                trail.startWidth = 0.8f;
                trail.endWidth = 0.2f;
                trail.material = new Material(Shader.Find("Sprites/Default"));
                trail.startColor = new Color(1f, 0.5f, 0f, 1f); // Bright orange
                trail.endColor = new Color(1f, 0.2f, 0f, 0f); // Fade to transparent
                trail.numCornerVertices = 5;
                trail.numCapVertices = 5;
                projectile.trail = trail;
            }

            // Add glowing light for visibility
            Light pointLight = cannonball.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = new Color(1f, 0.6f, 0.2f); // Orange glow
            pointLight.intensity = 3f;
            pointLight.range = 15f;
            pointLight.shadows = LightShadows.None; // No shadows for performance

            // Make material emissive if possible
            Renderer renderer = cannonball.GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                renderer.material.EnableKeyword("_EMISSION");
                renderer.material.SetColor("_EmissionColor", new Color(2f, 1f, 0.5f) * 2f); // Bright emission
            }

            // Ignore collisions with ALL colliders on this ship (including children)
            // Get the root ship transform
            Transform shipRoot = transform.root;
            Collider[] shipColliders = shipRoot.GetComponentsInChildren<Collider>(true);

            int ignoredCount = 0;
            foreach (Collider shipCollider in shipColliders)
            {
                if (shipCollider != null && cannonballCollider != null)
                {
                    Physics.IgnoreCollision(cannonballCollider, shipCollider);
                    ignoredCount++;
                }
            }

            Debug.Log($"[AIBroadside] Spawned VISIBLE cannonball from {muzzle.name}, ignored {ignoredCount} ship colliders");
        }

        /// <summary>
        /// Calculate exact launch velocity to hit the player's ship (or player if no ship exists)
        /// Uses projectile motion physics: position = start + velocity*time - 0.5*gravity*time^2
        /// </summary>
        private Vector3 CalculateLaunchVelocity(Vector3 firePosition)
        {
            // First, look for player ship (always target the ship if it exists)
            Transform targetTransform = null;
            Rigidbody targetRb = null;

            ShipController playerShip = FindAnyObjectByType<ShipController>();
            if (playerShip != null)
            {
                // Target the player's ship
                targetTransform = playerShip.transform;
                targetRb = playerShip.GetComponent<Rigidbody>();
                Debug.Log($"[AIBroadside] Targeting player ship: {targetTransform.name}");
            }
            else
            {
                // No ship found, fall back to player character
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player == null)
                {
                    Debug.LogWarning($"[AIBroadside] No player ship or player character found");
                    return Vector3.forward * 50f; // Default velocity
                }
                targetTransform = player.transform;
                targetRb = player.GetComponent<Rigidbody>();
            }

            // Get target position and velocity
            Vector3 playerPosition = targetTransform.position;
            Rigidbody playerRb = targetRb;

            // Predict where player will be when cannonball arrives
            Vector3 targetPosition = playerPosition;
            if (playerRb != null)
            {
                // Add player's movement during flight time
                targetPosition += playerRb.linearVelocity * flightTime;
                Debug.Log($"[AIBroadside] Player moving at {playerRb.linearVelocity.magnitude:F1} m/s, predicting {flightTime}s ahead");
            }

            // Add random offset for dodgeability
            Vector3 randomSpread = new Vector3(
                Random.Range(-randomOffset, randomOffset),
                0f,
                Random.Range(-randomOffset, randomOffset)
            );
            targetPosition += randomSpread;

            // Calculate displacement
            Vector3 displacement = targetPosition - firePosition;
            float distance = new Vector3(displacement.x, 0, displacement.z).magnitude;

            Debug.Log($"[AIBroadside] Target distance: {distance:F1}m, height diff: {displacement.y:F1}m");

            // Calculate velocity components using projectile motion
            // Horizontal velocity: vx = displacement.x / time
            float vx = displacement.x / flightTime;
            float vz = displacement.z / flightTime;

            // Vertical velocity: vy = (displacement.y / time) + (0.5 * gravity * time)
            float gravity = Mathf.Abs(Physics.gravity.y);
            float vy = (displacement.y / flightTime) + (0.5f * gravity * flightTime);

            Vector3 launchVelocity = new Vector3(vx, vy, vz);
            float speed = launchVelocity.magnitude;
            float angle = Mathf.Asin(vy / speed) * Mathf.Rad2Deg;

            Debug.Log($"[AIBroadside] Launch velocity: {speed:F1} m/s at {angle:F1}° angle");

            // Debug visualization
            Debug.DrawRay(firePosition, launchVelocity.normalized * 30f, Color.red, 2f);
            Debug.DrawLine(firePosition, targetPosition, Color.green, 2f);

            return launchVelocity;
        }

        /// <summary>
        /// Check if the current firing side still has a clear shot to the player's ship
        /// </summary>
        private bool HasClearShot()
        {
            // First, look for player ship (always target the ship if it exists)
            Transform targetTransform = null;

            ShipController playerShip = FindAnyObjectByType<ShipController>();
            if (playerShip != null)
            {
                targetTransform = playerShip.transform;
            }
            else
            {
                // Fall back to player character
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) return false;
                targetTransform = player.transform;
            }

            Vector3 toPlayer = (targetTransform.position - transform.position).normalized;

            // Check if player is still on the correct side
            float side = Vector3.Dot(toPlayer, transform.right);
            bool playerOnLeftSide = side > 0; // Flipped for POTCO ships facing backwards
            bool playerOnRightSide = side < 0;

            // Verify player is on the side we're currently firing
            if (currentFiringSide && !playerOnLeftSide)
            {
                Debug.Log($"[AIBroadside] Player no longer on LEFT side (side dot: {side:F2})");
                return false;
            }
            if (!currentFiringSide && !playerOnRightSide)
            {
                Debug.Log($"[AIBroadside] Player no longer on RIGHT side (side dot: {side:F2})");
                return false;
            }

            // Check if player is at a perpendicular angle (not too far forward/back)
            // Calculate angle from ship's bow to player
            float angleFromForward = Vector3.Angle(-transform.forward, toPlayer);

            // Calculate how far from perpendicular (90 degrees) we are
            float angleFromPerpendicular = Mathf.Abs(angleFromForward - 90f);

            // Player must be within maxAngleDeviation degrees of perpendicular
            if (angleFromPerpendicular > maxAngleDeviation)
            {
                Debug.Log($"[AIBroadside] Player angle too extreme (angle from perpendicular: {angleFromPerpendicular:F1}°, max: {maxAngleDeviation}°)");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get total number of cannons
        /// </summary>
        public int GetTotalCannons()
        {
            return leftBroadsideCannons.Count + rightBroadsideCannons.Count;
        }

        /// <summary>
        /// Debug: Fire left broadside
        /// </summary>
        [ContextMenu("Fire Left Broadside")]
        public void DebugFireLeft()
        {
            FireBroadside(true);
        }

        /// <summary>
        /// Debug: Fire right broadside
        /// </summary>
        [ContextMenu("Fire Right Broadside")]
        public void DebugFireRight()
        {
            FireBroadside(false);
        }
    }
}
