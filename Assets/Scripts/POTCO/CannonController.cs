using UnityEngine;

namespace POTCO
{
    /// <summary>
    /// Player-controlled cannon system matching POTCO specs
    /// Handles cannon rig (hNode, pivot, cannonExitPoint), aiming, and firing
    /// Based on: POTCO_Source/pirates/battle/Cannon.py and POTCO_Source/pirates/pirate/CannonCamera.py
    /// </summary>
    public class CannonController : MonoBehaviour
    {
        [Header("Cannon Rig (Auto-Detected Bones)")]
        [Tooltip("Yaw rotation bone - def_cannon_turn")]
        public Transform hNode;
        [Tooltip("Pitch rotation bone - def_cannon_updown")]
        public Transform pivot;
        [Tooltip("Muzzle point where projectiles spawn")]
        public Transform cannonExitPoint;

        [Header("POTCO Rotation Limits")]
        [Tooltip("Minimum yaw angle (left)")]
        public float minH = -60f;
        [Tooltip("Maximum yaw angle (right)")]
        public float maxH = 60f;
        [Tooltip("Minimum pitch angle (down)")]
        public float minP = -22.2f;
        [Tooltip("Maximum pitch angle (up)")]
        public float maxP = 6.66f;

        [Header("Mouse Sensitivity (POTCO defaults)")]
        [Tooltip("Yaw sensitivity")]
        public float sensH = 0.07f;
        [Tooltip("Pitch sensitivity")]
        public float sensP = 0.03f;

        [Header("Camera Settings (POTCO specs)")]
        [Tooltip("Cannon camera FOV")]
        public float cannonFOV = 50f;
        [Tooltip("Camera parent offset from cannon (above and slightly back)")]
        public Vector3 camParentOffset = new Vector3(0f, 2.06f, -5.39f);

        [Header("Interaction")]
        [Tooltip("Distance player must be within to use cannon")]
        public float interactionDistance = 5f;
        [Tooltip("Key to enter/exit cannon mode")]
        public KeyCode interactKey = KeyCode.LeftShift;
        [Tooltip("Key to fire cannon")]
        public KeyCode fireKey = KeyCode.Mouse0;

        [Header("Projectile")]
        [Tooltip("Cannonball prefab to spawn")]
        public GameObject cannonballPrefab;
        [Tooltip("Initial velocity of cannonball")]
        public float muzzleVelocity = 50f;
        [Tooltip("Cooldown between shots (seconds)")]
        public float fireCooldown = 2f;

        // Internal state
        private bool isControlling = false;
        private Transform playerTransform;
        private Player.PlayerController playerController;
        private Player.PlayerCamera playerCamera;
        private Camera mainCamera;

        // Camera state backup
        private float originalFOV;
        private Vector3 originalCameraLocalPosition;
        private Quaternion originalCameraLocalRotation;
        private Transform originalCameraParent;

        // Cannon state
        private float currentYaw = 0f;
        private float currentPitch = 0f;
        private float lastFireTime = -999f;

        private void Start()
        {
            mainCamera = Camera.main;

            // Auto-create cannon rig if not present
            if (hNode == null || pivot == null || cannonExitPoint == null)
            {
                CreateCannonRig();
            }

            // Try to load cannonball prefab if not assigned
            if (cannonballPrefab == null)
            {
                GameObject loadedPrefab = Resources.Load<GameObject>("phase_3/models/ammunition/cannonball");
                if (loadedPrefab != null)
                {
                    cannonballPrefab = loadedPrefab;
                    Debug.Log($"✅ Auto-loaded cannonball prefab from Resources");
                }
                else
                {
                    Debug.LogWarning($"⚠️ Cannonball prefab not found at phase_3/models/ammunition/cannonball");
                }
            }
        }

        private void Update()
        {
            if (!isControlling)
            {
                CheckForPlayerNearby();
            }
            else
            {
                HandleCannonControls();
                HandleExitControl();
            }
        }

        /// <summary>
        /// Find cannon bones in model hierarchy
        /// Looks for def_cannon_turn (yaw) and def_cannon_updown (pitch)
        /// </summary>
        private void CreateCannonRig()
        {
            Debug.Log($"🔧 Finding cannon bones for {gameObject.name}...");

            // Find def_cannon_turn bone (yaw/horizontal rotation)
            if (hNode == null)
            {
                hNode = FindChildRecursive(transform, "def_cannon_turn");
                if (hNode != null)
                {
                    Debug.Log($"✅ Found def_cannon_turn bone at: {GetTransformPath(hNode)}");
                }
                else
                {
                    Debug.LogWarning($"⚠️ def_cannon_turn bone not found, creating fallback");
                    GameObject hNodeObj = new GameObject("hNode_fallback");
                    hNodeObj.transform.SetParent(transform);
                    hNodeObj.transform.localPosition = Vector3.zero;
                    hNodeObj.transform.localRotation = Quaternion.identity;
                    hNode = hNodeObj.transform;
                }
            }

            // Find def_cannon_updown bone (pitch/vertical rotation)
            if (pivot == null)
            {
                pivot = FindChildRecursive(transform, "def_cannon_updown");
                if (pivot != null)
                {
                    Debug.Log($"✅ Found def_cannon_updown bone at: {GetTransformPath(pivot)}");
                }
                else
                {
                    Debug.LogWarning($"⚠️ def_cannon_updown bone not found, creating fallback under hNode");
                    GameObject pivotObj = new GameObject("pivot_fallback");
                    pivotObj.transform.SetParent(hNode);
                    pivotObj.transform.localPosition = Vector3.zero;
                    pivotObj.transform.localRotation = Quaternion.identity;
                    pivot = pivotObj.transform;
                }
            }

            // Create cannonExitPoint at muzzle (find barrel end or create)
            if (cannonExitPoint == null)
            {
                // Try to find existing muzzle marker
                Transform muzzle = FindChildRecursive(transform, "muzzle");
                if (muzzle == null) muzzle = FindChildRecursive(transform, "def_muzzle");
                if (muzzle == null) muzzle = FindChildRecursive(transform, "cannon_exit");

                if (muzzle != null)
                {
                    cannonExitPoint = muzzle;
                    Debug.Log($"✅ Found muzzle point at: {GetTransformPath(cannonExitPoint)}");
                }
                else
                {
                    // Create exit point at end of barrel
                    GameObject exitPointObj = new GameObject("cannonExitPoint");
                    exitPointObj.transform.SetParent(pivot);
                    exitPointObj.transform.localPosition = new Vector3(0f, 0f, 2f); // Forward along Z
                    exitPointObj.transform.localRotation = Quaternion.identity;
                    cannonExitPoint = exitPointObj.transform;
                    Debug.Log($"✅ Created cannonExitPoint at barrel end");
                }
            }

            Debug.Log($"✅ Cannon rig setup complete for {gameObject.name}");
        }

        /// <summary>
        /// Recursively find child transform by name
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
        /// Get full hierarchy path of transform
        /// </summary>
        private string GetTransformPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null && t.parent != transform)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }

        private void CheckForPlayerNearby()
        {
            if (mainCamera == null) return;

            // Find player if not cached
            if (playerTransform == null)
            {
                playerTransform = FindPlayer();
                if (playerTransform == null) return;
            }

            // Check distance to cannon
            float distance = Vector3.Distance(playerTransform.position, transform.position);

            if (distance <= interactionDistance)
            {
                // Show prompt every 30 frames
                if (Time.frameCount % 30 == 0)
                {
                    Debug.Log($"Press {interactKey} to use cannon!");
                }

                if (Input.GetKeyDown(interactKey))
                {
                    EnterCannonControl();
                }
            }
        }

        private Transform FindPlayer()
        {
            // Try Player tag
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) return player.transform;

            // Try PlayerController component
            Player.PlayerController pc = FindAnyObjectByType<Player.PlayerController>();
            if (pc != null)
            {
                if (pc.tag == "Untagged") pc.tag = "Player";
                return pc.transform;
            }

            return null;
        }

        private void EnterCannonControl()
        {
            isControlling = true;

            // Cache player components
            playerController = playerTransform.GetComponent<Player.PlayerController>();
            playerCamera = mainCamera.GetComponent<Player.PlayerCamera>();

            // Disable player movement and camera
            if (playerController != null) playerController.enabled = false;
            if (playerCamera != null) playerCamera.enabled = false;

            // Save original camera state
            originalFOV = mainCamera.fieldOfView;
            originalCameraParent = mainCamera.transform.parent;
            originalCameraLocalPosition = mainCamera.transform.localPosition;
            originalCameraLocalRotation = mainCamera.transform.localRotation;

            // Set cannon camera - parent to pivot so it rotates with the cannon
            mainCamera.fieldOfView = cannonFOV;
            mainCamera.transform.SetParent(pivot); // Parent to pivot bone so camera follows cannon rotation
            mainCamera.transform.localPosition = camParentOffset;
            mainCamera.transform.localRotation = Quaternion.identity;

            // Initialize cannon rotation to forward
            currentYaw = 0f;
            currentPitch = 0f;
            hNode.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
            pivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);

            // Lock cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Debug.Log($"✅ Entered cannon control mode - FOV: {cannonFOV}, Yaw limits: [{minH}, {maxH}], Pitch limits: [{minP}, {maxP}]");
        }

        private void HandleCannonControls()
        {
            // Update camera settings live (allows tweaking in Inspector during Play mode)
            mainCamera.fieldOfView = cannonFOV;
            mainCamera.transform.localPosition = camParentOffset;

            // Mouse input for aiming (POTCO sensitivity values)
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            // Update yaw (left/right) - fixed inverted controls
            currentYaw += mouseX * sensH * 100f; // Multiply by 100 to match POTCO feel
            currentYaw = Mathf.Clamp(currentYaw, minH, maxH);

            // Update pitch (up/down) - inverted for proper feel
            currentPitch += -mouseY * sensP * 100f; // Negative for natural aiming
            currentPitch = Mathf.Clamp(currentPitch, minP, maxP);

            // Apply rotations (camera follows automatically since it's parented to pivot)
            hNode.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
            pivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);

            // Fire cannon
            if (Input.GetKeyDown(fireKey) && Time.time >= lastFireTime + fireCooldown)
            {
                FireCannon();
            }
        }

        private void FireCannon()
        {
            lastFireTime = Time.time;

            if (cannonballPrefab == null)
            {
                Debug.LogWarning("⚠️ Cannot fire - cannonball prefab not assigned!");
                return;
            }

            // Spawn cannonball at muzzle
            GameObject cannonball = Instantiate(cannonballPrefab, cannonExitPoint.position, cannonExitPoint.rotation);

            // Get current heading and pitch from rig
            float fireH = hNode.localRotation.eulerAngles.y;
            float fireP = pivot.localRotation.eulerAngles.x;

            // Calculate fire direction
            Vector3 fireDirection = cannonExitPoint.forward;

            // Add Rigidbody if not present
            Rigidbody rb = cannonball.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = cannonball.AddComponent<Rigidbody>();
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            // Launch projectile
            rb.linearVelocity = fireDirection * muzzleVelocity;
            rb.useGravity = true;

            // Add CannonProjectile component for collision handling
            CannonProjectile projectile = cannonball.GetComponent<CannonProjectile>();
            if (projectile == null)
            {
                projectile = cannonball.AddComponent<CannonProjectile>();
            }

            // Play effects (TODO: Add muzzle flash and sound)
            Debug.Log($"🔥 Fired cannon! H: {fireH:F1}°, P: {fireP:F1}°");

            // Simple recoil animation (scale barrel slightly)
            if (pivot != null)
            {
                StartCoroutine(RecoilAnimation());
            }
        }

        private System.Collections.IEnumerator RecoilAnimation()
        {
            Vector3 originalScale = pivot.localScale;
            Vector3 recoilScale = originalScale + new Vector3(0f, -0.2f, 0f); // Slight compression

            // Recoil
            float recoilTime = 0.1f;
            float elapsed = 0f;
            while (elapsed < recoilTime)
            {
                pivot.localScale = Vector3.Lerp(originalScale, recoilScale, elapsed / recoilTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Spring back
            elapsed = 0f;
            float returnTime = 0.3f;
            while (elapsed < returnTime)
            {
                pivot.localScale = Vector3.Lerp(recoilScale, originalScale, elapsed / returnTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            pivot.localScale = originalScale;
        }

        private void HandleExitControl()
        {
            if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(KeyCode.Escape))
            {
                ExitCannonControl();
            }
        }

        private void ExitCannonControl()
        {
            isControlling = false;

            // Restore camera
            if (mainCamera != null)
            {
                mainCamera.fieldOfView = originalFOV;
                mainCamera.transform.SetParent(originalCameraParent);
                mainCamera.transform.localPosition = originalCameraLocalPosition;
                mainCamera.transform.localRotation = originalCameraLocalRotation;
            }

            // Re-enable player components
            if (playerController != null) playerController.enabled = true;
            if (playerCamera != null) playerCamera.enabled = true;

            // Unlock cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Debug.Log("✅ Exited cannon control mode");
        }

        private void OnDrawGizmosSelected()
        {
            // Draw interaction range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionDistance);

            // Draw cannon rig
            if (hNode != null && pivot != null && cannonExitPoint != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(hNode.position, pivot.position);
                Gizmos.DrawWireSphere(hNode.position, 0.2f);

                Gizmos.color = Color.green;
                Gizmos.DrawLine(pivot.position, cannonExitPoint.position);
                Gizmos.DrawWireSphere(pivot.position, 0.15f);

                Gizmos.color = Color.red;
                Gizmos.DrawRay(cannonExitPoint.position, cannonExitPoint.forward * 2f);
                Gizmos.DrawWireSphere(cannonExitPoint.position, 0.1f);
            }
        }
    }
}
