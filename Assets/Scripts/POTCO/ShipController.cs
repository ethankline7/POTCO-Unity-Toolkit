using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace POTCO
{
    public class ShipController : MonoBehaviour
    {
        [Header("Ship Movement")]
        public float moveSpeed = 60f;
        public float acceleration = 10f;
        public float rotateSpeed = 30f;

        [Header("Ship Bobbing")]
        public bool enableBobbing = true;
        public float bobbingHeightAmount = 0.5f;
        public float bobbingHeightSpeed = 1.0f;
        public float bobbingRotationAmount = 2.0f;
        public float bobbingRotationSpeed = 1.5f;

        [Header("Camera Settings")]
        public Transform thirdPersonCameraPoint;
        public Vector3 cameraOffset = new Vector3(0, 5, -10);
        public float orbitSpeed = 300f;
        public float orbitDistance = 400f;
        public float orbitHeight = 100f;
        public float zoomSpeed = 100f;
        public float minZoomDistance = 50f;
        public float maxZoomDistance = 1000f;

        [Header("Control Settings")]
        public float interactionDistance = 10f;
        public KeyCode enterControlKey = KeyCode.LeftShift;
        public KeyCode exitControlKey = KeyCode.Escape;

        [Header("Animation Settings")]
        public string tiedUpMastAnimation = "tiedup";
        public string rollUpMastAnimation = "rollup";
        public string rollDownMastAnimation = "rolldown";
        public string idleMastAnimation = "idle";

        // Internal state
        private bool isControlling = false;
        private Transform playerTransform;
        private Transform wheelTransform;
        private Camera mainCamera;
        private Vector3 originalCameraPosition;
        private Quaternion originalCameraRotation;
        private Transform originalCameraParent;

        // Ship components
        private class MastAnimationData
        {
            public Animation animation;
            public string mastType; // e.g., "main_tri", "fore_multi", "aft_tri"
            public AnimationClip tiedUpClip;
            public AnimationClip rollUpClip;
            public AnimationClip rollDownClip;
            public AnimationClip idleClip;
        }

        private class RopeLadderData
        {
            public Transform ladderTransform;
            public Vector3 targetLocalPosition;
            public Quaternion targetLocalRotation;
        }

        private List<MastAnimationData> mastAnimations = new List<MastAnimationData>();
        private List<GameObject> leftBroadsideCannons = new List<GameObject>();
        private List<GameObject> rightBroadsideCannons = new List<GameObject>();
        private List<RopeLadderData> ropeLadders = new List<RopeLadderData>();

        // Cannon animation clips
        private AnimationClip cannonOpenClip;
        private AnimationClip cannonFireClip;
        private AnimationClip cannonCloseClip;

        private bool sailsDown = false;
        private bool isRollingDown = false;

        // Camera orbit
        private float currentOrbitAngle = 0f;
        private float currentOrbitPitch = 20f;

        // Bobbing motion
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private float bobbingTime = 0f;

        // Movement acceleration
        private float currentSpeed = 0f;

        void Start()
        {
            mainCamera = Camera.main;
            FindShipComponents();
            LoadAnimationClips();
            CreateCameraPoint();
            AddDeckColliders();

            // Initialize bobbing
            basePosition = transform.position;
            baseRotation = transform.rotation;

            // Start with sails tied up
            PlayMastAnimation("tiedup", WrapMode.Loop);

            // Debug info
            if (wheelTransform == null)
            {
                Debug.LogError("ShipController: Wheel not found! Make sure the ship has a 'Wheel' child object.");
            }
            else
            {
                Debug.Log($"ShipController: Wheel found at {wheelTransform.position}");
            }
        }

        void Update()
        {
            // Apply bobbing motion always
            if (enableBobbing)
            {
                ApplyBobbing();
            }

            if (!isControlling)
            {
                CheckForPlayerNearWheel();
            }
            else
            {
                HandleShipControls();
                HandleCameraOrbit();
                HandleCannonControls();
                HandleExitControl();
            }
        }

        void LateUpdate()
        {
            // Force rope ladder positions after animations have updated
            foreach (var ladderData in ropeLadders)
            {
                if (ladderData.ladderTransform != null)
                {
                    ladderData.ladderTransform.localPosition = ladderData.targetLocalPosition;
                    ladderData.ladderTransform.localRotation = ladderData.targetLocalRotation;
                }
            }
        }

        private void FindShipComponents()
        {
            // Find wheel
            wheelTransform = FindChildRecursive(transform, "Wheel");
            if (wheelTransform != null)
            {
                // Try to find the actual wheel model child
                if (wheelTransform.childCount > 0)
                {
                    wheelTransform = wheelTransform.GetChild(0);
                }
            }

            // Find all masts
            Transform mastsParent = transform.Find("Masts");
            if (mastsParent != null)
            {
                Debug.Log($"[MAST DEBUG] Found Masts parent with {mastsParent.childCount} children");
                foreach (Transform mastLocator in mastsParent)
                {
                    Debug.Log($"[MAST DEBUG] Processing mast locator: {mastLocator.name}");

                    // Find the actual mast model (first child with a SkinnedMeshRenderer or MeshFilter)
                    Transform actualMast = FindMastModel(mastLocator);
                    if (actualMast != null)
                    {
                        Debug.Log($"[MAST DEBUG] Found mast model: {actualMast.name} at path: {GetGameObjectPath(actualMast)}");

                        // Get mast type from MastTypeInfo component
                        string mastType = GetMastType(actualMast);
                        Debug.Log($"[MAST DEBUG] Extracted mast type: {mastType}");

                        // Check what components it has
                        var skinnedMesh = actualMast.GetComponent<SkinnedMeshRenderer>();
                        var meshFilter = actualMast.GetComponent<MeshFilter>();
                        Debug.Log($"[MAST DEBUG] Has SkinnedMeshRenderer: {skinnedMesh != null}, Has MeshFilter: {meshFilter != null}");

                        Animation anim = actualMast.GetComponent<Animation>();
                        if (anim == null)
                        {
                            anim = actualMast.gameObject.AddComponent<Animation>();
                            Debug.Log($"[MAST DEBUG] Added Animation component to {actualMast.name}");
                        }
                        else
                        {
                            Debug.Log($"[MAST DEBUG] Animation component already exists on {actualMast.name}");
                        }

                        // Create mast animation data
                        MastAnimationData mastData = new MastAnimationData
                        {
                            animation = anim,
                            mastType = mastType
                        };

                        mastAnimations.Add(mastData);
                        Debug.Log($"[MAST DEBUG] Added to mastAnimations list (total: {mastAnimations.Count})");

                        // Find and store rope ladder positions
                        Transform leftLadder = FindChildRecursive(actualMast, "def_ladder_0_left");
                        Transform rightLadder = FindChildRecursive(actualMast, "def_ladder_0_right");

                        if (leftLadder != null)
                        {
                            ropeLadders.Add(new RopeLadderData
                            {
                                ladderTransform = leftLadder,
                                targetLocalPosition = leftLadder.localPosition,
                                targetLocalRotation = leftLadder.localRotation
                            });
                            Debug.Log($"Stored left rope ladder position for {actualMast.name}");
                        }

                        if (rightLadder != null)
                        {
                            ropeLadders.Add(new RopeLadderData
                            {
                                ladderTransform = rightLadder,
                                targetLocalPosition = rightLadder.localPosition,
                                targetLocalRotation = rightLadder.localRotation
                            });
                            Debug.Log($"Stored right rope ladder position for {actualMast.name}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[MAST DEBUG] Could not find mast model in: {mastLocator.name}");
                    }
                }
            }
            else
            {
                Debug.LogError("[MAST DEBUG] Could not find Masts parent!");
            }

            // Find left broadside cannons
            Transform leftCannonsParent = transform.Find("Cannons_Broadside_Left");
            if (leftCannonsParent != null)
            {
                foreach (Transform cannon in leftCannonsParent)
                {
                    leftBroadsideCannons.Add(cannon.gameObject);
                }
            }

            // Find right broadside cannons
            Transform rightCannonsParent = transform.Find("Cannons_Broadside_Right");
            if (rightCannonsParent != null)
            {
                foreach (Transform cannon in rightCannonsParent)
                {
                    rightBroadsideCannons.Add(cannon.gameObject);
                }
            }

            Debug.Log($"Ship Controller initialized: {mastAnimations.Count} masts, {leftBroadsideCannons.Count} left cannons, {rightBroadsideCannons.Count} right cannons");
        }

        private void LoadAnimationClips()
        {
            Debug.Log("[ANIM LOAD] Starting to load animation clips...");

            // Load animations for each mast based on its type
            foreach (MastAnimationData mastData in mastAnimations)
            {
                Debug.Log($"[ANIM LOAD] ===== Loading animations for mast type: {mastData.mastType} =====");

                // Construct the paths
                string tiedUpPath = $"phase_3/models/char/pir_a_shp_mst_{mastData.mastType}_{tiedUpMastAnimation}";
                string rollUpPath = $"phase_3/models/char/pir_a_shp_mst_{mastData.mastType}_{rollUpMastAnimation}";
                string rollDownPath = $"phase_3/models/char/pir_a_shp_mst_{mastData.mastType}_{rollDownMastAnimation}";
                string idlePath = $"phase_3/models/char/pir_a_shp_mst_{mastData.mastType}_{idleMastAnimation}";

                Debug.Log($"[ANIM LOAD] Will try to load:");
                Debug.Log($"[ANIM LOAD]   TiedUp: {tiedUpPath}");
                Debug.Log($"[ANIM LOAD]   RollUp: {rollUpPath}");
                Debug.Log($"[ANIM LOAD]   RollDown: {rollDownPath}");
                Debug.Log($"[ANIM LOAD]   Idle: {idlePath}");

                // Load clips specific to this mast type
                mastData.tiedUpClip = LoadAnimationFromResources(tiedUpPath);
                mastData.rollUpClip = LoadAnimationFromResources(rollUpPath);
                mastData.rollDownClip = LoadAnimationFromResources(rollDownPath);
                mastData.idleClip = LoadAnimationFromResources(idlePath);

                Debug.Log($"[ANIM LOAD] Results for {mastData.mastType}:");
                Debug.Log($"[ANIM LOAD]   TiedUp: {mastData.tiedUpClip != null} {(mastData.tiedUpClip != null ? $"({mastData.tiedUpClip.name})" : "")}");
                Debug.Log($"[ANIM LOAD]   RollUp: {mastData.rollUpClip != null} {(mastData.rollUpClip != null ? $"({mastData.rollUpClip.name})" : "")}");
                Debug.Log($"[ANIM LOAD]   RollDown: {mastData.rollDownClip != null} {(mastData.rollDownClip != null ? $"({mastData.rollDownClip.name})" : "")}");
                Debug.Log($"[ANIM LOAD]   Idle: {mastData.idleClip != null} {(mastData.idleClip != null ? $"({mastData.idleClip.name})" : "")}");
            }

            // Load cannon animations
            Debug.Log("[ANIM LOAD] Loading cannon animations...");
            cannonOpenClip = LoadAnimationFromResources("phase_3/models/shipparts/pir_a_shp_can_broadside_open");
            cannonFireClip = LoadAnimationFromResources("phase_3/models/shipparts/pir_a_shp_can_broadside_fire");
            cannonCloseClip = LoadAnimationFromResources("phase_3/models/shipparts/pir_a_shp_can_broadside_close");

            Debug.Log($"[ANIM LOAD] === FINAL RESULTS ===");
            Debug.Log($"[ANIM LOAD] Loaded animations for {mastAnimations.Count} masts");
            Debug.Log($"[ANIM LOAD] Cannon animations - Open: {cannonOpenClip != null}, Fire: {cannonFireClip != null}, Close: {cannonCloseClip != null}");
        }

        private AnimationClip LoadAnimationFromResources(string path)
        {
            Debug.Log($"[ANIM LOAD]   Attempting to load: {path}");

            // If path doesn't start with phase_, search all phases
            if (!path.StartsWith("phase_"))
            {
                string[] phases = { "phase_2", "phase_3", "phase_4", "phase_5", "phase_6" };
                foreach (string phase in phases)
                {
                    string fullPath = $"{phase}/{path}";
                    AnimationClip foundClip = LoadAnimationFromResourcesDirect(fullPath);
                    if (foundClip != null)
                    {
                        Debug.Log($"[ANIM LOAD]   ✓ Found in {phase}");
                        return foundClip;
                    }
                }
                Debug.Log($"[ANIM LOAD]   ✗ Not found in any phase directory");
                return null;
            }
            else
            {
                return LoadAnimationFromResourcesDirect(path);
            }
        }

        private AnimationClip LoadAnimationFromResourcesDirect(string path)
        {
            // Try loading as prefab first
            GameObject animObj = Resources.Load<GameObject>(path);
            if (animObj != null)
            {
                Debug.Log($"[ANIM LOAD]   Loaded GameObject from Resources");

                // Check if it has an Animation component
                Animation anim = animObj.GetComponent<Animation>();
                if (anim != null && anim.clip != null)
                {
                    Debug.Log($"[ANIM LOAD]   ✓ Found Animation component with clip: {anim.clip.name}");
                    return anim.clip;
                }

                // Check all clips in the Animation component
                if (anim != null)
                {
                    Debug.Log($"[ANIM LOAD]   Animation component exists, checking all clips...");
                    foreach (AnimationState state in anim)
                    {
                        Debug.Log($"[ANIM LOAD]   ✓ Found animation clip: {state.clip.name}");
                        return state.clip;
                    }
                    Debug.Log($"[ANIM LOAD]   Animation component has no clips");
                }
                else
                {
                    Debug.Log($"[ANIM LOAD]   GameObject has no Animation component");
                }
            }

            // Try loading as AnimationClip directly
            AnimationClip clip = Resources.Load<AnimationClip>(path);
            if (clip != null)
            {
                Debug.Log($"[ANIM LOAD]   ✓ Loaded AnimationClip directly: {clip.name}");
                return clip;
            }

            Debug.Log($"[ANIM LOAD]   ✗ Failed to load from: {path}");
            return null;
        }

        private void CreateCameraPoint()
        {
            if (thirdPersonCameraPoint == null && wheelTransform != null)
            {
                GameObject camPoint = new GameObject("ThirdPersonCameraPoint");
                camPoint.transform.SetParent(transform);
                camPoint.transform.position = wheelTransform.position + cameraOffset;
                camPoint.transform.LookAt(wheelTransform.position);
                thirdPersonCameraPoint = camPoint.transform;
            }
        }

        private void CheckForPlayerNearWheel()
        {
            if (wheelTransform == null)
            {
                Debug.LogWarning("ShipController: Wheel transform is null");
                return;
            }

            if (mainCamera == null)
            {
                Debug.LogWarning("ShipController: Main camera is null");
                return;
            }

            // Find player
            if (playerTransform == null)
            {
                playerTransform = FindPlayer();
                if (playerTransform == null)
                {
                    Debug.LogWarning("ShipController: Could not find player");
                    return;
                }
            }

            // Check distance to wheel
            float distance = Vector3.Distance(playerTransform.position, wheelTransform.position);

            // Debug distance check every few frames
            if (Time.frameCount % 60 == 0)
            {
                Debug.Log($"Distance to wheel: {distance:F2} (interaction range: {interactionDistance})");
            }

            if (distance <= interactionDistance)
            {
                // Show on-screen prompt
                if (Time.frameCount % 30 == 0)
                {
                    Debug.Log($"Press {enterControlKey} to control the ship!");
                }

                if (Input.GetKeyDown(enterControlKey))
                {
                    Debug.Log("Shift key pressed, entering ship control...");
                    EnterShipControl();
                }
            }
        }

        private Transform FindPlayer()
        {
            // Try to find by Player tag
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                return player.transform;
            }

            // Try to find by main camera's parent/root
            if (mainCamera != null)
            {
                Transform camParent = mainCamera.transform.parent;
                if (camParent != null)
                {
                    // Check if parent has a CharacterController
                    if (camParent.GetComponent<CharacterController>() != null)
                    {
                        // Auto-tag it as Player
                        if (camParent.tag == "Untagged")
                        {
                            camParent.tag = "Player";
                            Debug.Log($"Auto-tagged '{camParent.name}' as Player");
                        }
                        return camParent;
                    }

                    // Try root object
                    Transform root = camParent.root;
                    if (root.GetComponent<CharacterController>() != null)
                    {
                        if (root.tag == "Untagged")
                        {
                            root.tag = "Player";
                            Debug.Log($"Auto-tagged '{root.name}' as Player");
                        }
                        return root;
                    }
                }
            }

            // Search for CharacterController in scene
            CharacterController[] controllers = FindObjectsOfType<CharacterController>();
            if (controllers.Length > 0)
            {
                Transform controller = controllers[0].transform;
                if (controller.tag == "Untagged")
                {
                    controller.tag = "Player";
                    Debug.Log($"Auto-tagged '{controller.name}' as Player");
                }
                return controller;
            }

            Debug.LogWarning("Could not find player - no Player tag, CharacterController, or main camera parent found");
            return null;
        }

        private void EnterShipControl()
        {
            isControlling = true;

            // Save original camera state (local to parent)
            originalCameraPosition = mainCamera.transform.localPosition;
            originalCameraRotation = mainCamera.transform.localRotation;
            originalCameraParent = mainCamera.transform.parent;

            // Set up initial camera position
            currentOrbitAngle = transform.eulerAngles.y + 180f; // Start behind ship
            currentOrbitPitch = 20f;

            // Position camera
            Vector3 shipCenter = transform.position + Vector3.up * orbitHeight;
            Quaternion rotation = Quaternion.Euler(currentOrbitPitch, currentOrbitAngle, 0);
            Vector3 offset = rotation * new Vector3(0, 0, -orbitDistance);

            mainCamera.transform.position = shipCenter + offset;
            mainCamera.transform.LookAt(shipCenter);
            mainCamera.transform.SetParent(null); // Unparent so we can orbit freely

            // Position player behind the wheel
            if (playerTransform != null && wheelTransform != null)
            {
                // Position player behind the wheel, facing the same direction as wheel
                Vector3 wheelPosition = wheelTransform.position;
                Vector3 wheelForward = wheelTransform.forward;

                // Place player at the wheel with 2 unit offset
                playerTransform.position = wheelPosition + wheelForward * 2f;

                // Face the same direction as the wheel (looking out from the ship)
                // The Model child inside has 180° offset, so player parent needs same rotation as wheel
                playerTransform.rotation = wheelTransform.rotation;

                Debug.Log($"Positioned player behind wheel at {playerTransform.position}");
                Debug.Log($"Player rotation: {playerTransform.rotation.eulerAngles}");
                Debug.Log($"Wheel rotation: {wheelTransform.rotation.eulerAngles}");
            }

            // Play wheel_idle animation with correct gender prefix
            if (playerTransform != null)
            {
                Debug.Log("🎬 Starting wheel_idle animation setup...");

                // Get the SimpleAnimationPlayer to determine gender
                Player.SimpleAnimationPlayer animPlayer = playerTransform.GetComponent<Player.SimpleAnimationPlayer>();
                if (animPlayer != null)
                {
                    Debug.Log($"Found SimpleAnimationPlayer, gender prefix: {animPlayer.GenderPrefix}");

                    // Disable SimpleAnimationPlayer so it doesn't interfere with wheel_idle
                    animPlayer.enabled = false;
                    Debug.Log("Disabled SimpleAnimationPlayer");
                }
                else
                {
                    Debug.LogWarning("⚠️ No SimpleAnimationPlayer found on player!");
                }

                // Get the Animation component from Model child (or player itself)
                Animation playerAnim = null;
                Transform modelChild = playerTransform.Find("Model");
                if (modelChild != null)
                {
                    Debug.Log($"Found Model child: {modelChild.name}");
                    playerAnim = modelChild.GetComponent<Animation>();
                    if (playerAnim != null)
                    {
                        Debug.Log("✅ Found Animation component on Model child");
                    }
                }

                if (playerAnim == null)
                {
                    Debug.Log("Searching for Animation in children...");
                    playerAnim = playerTransform.GetComponentInChildren<Animation>();
                    if (playerAnim != null)
                    {
                        Debug.Log($"✅ Found Animation component on: {playerAnim.gameObject.name}");
                    }
                }

                if (playerAnim == null)
                {
                    Debug.LogError("❌ No Animation component found on player!");
                    return;
                }

                if (animPlayer == null)
                {
                    Debug.LogError("❌ No SimpleAnimationPlayer found on player!");
                    return;
                }

                // Get gender prefix from SimpleAnimationPlayer
                string genderPrefix = animPlayer.GenderPrefix;
                Debug.Log($"Using gender prefix: {genderPrefix}");

                // Load wheel_idle animation with gender prefix (searches all phases)
                string wheelIdleAnimName = genderPrefix + "wheel_idle";
                Debug.Log($"Looking for animation: {wheelIdleAnimName}");

                AnimationClip wheelIdleClip = LoadAnimationFromResources($"char/{wheelIdleAnimName}");

                if (wheelIdleClip == null)
                {
                    Debug.Log($"Not found in char/, trying models/char/...");
                    wheelIdleClip = LoadAnimationFromResources($"models/char/{wheelIdleAnimName}");
                }

                if (wheelIdleClip != null)
                {
                    Debug.Log($"✅ Loaded wheel_idle clip: {wheelIdleClip.name}, length: {wheelIdleClip.length}s");

                    // Stop all current animations
                    playerAnim.Stop();
                    Debug.Log("Stopped all current animations");

                    // Remove old wheel_idle clip if it exists
                    if (playerAnim.GetClip("wheel_idle") != null)
                    {
                        playerAnim.RemoveClip("wheel_idle");
                        Debug.Log("Removed old wheel_idle clip");
                    }

                    // Add and play wheel_idle animation
                    playerAnim.AddClip(wheelIdleClip, "wheel_idle");
                    playerAnim["wheel_idle"].wrapMode = WrapMode.Loop;
                    playerAnim["wheel_idle"].enabled = true;
                    playerAnim["wheel_idle"].weight = 1.0f;
                    playerAnim.Play("wheel_idle");

                    // Verify it's playing
                    bool isPlaying = playerAnim.IsPlaying("wheel_idle");
                    Debug.Log($"🎬 Animation playing status: {isPlaying}");
                    Debug.Log($"🎬 Animation state - enabled: {playerAnim["wheel_idle"].enabled}, weight: {playerAnim["wheel_idle"].weight}");
                    Debug.Log($"✅ Playing {wheelIdleAnimName} animation on character at wheel");
                }
                else
                {
                    Debug.LogError($"❌ Could not find {wheelIdleAnimName} animation in Resources!");
                    Debug.LogError($"   Tried: phase_3/char/{wheelIdleAnimName}");
                    Debug.LogError($"   Tried: phase_3/models/char/{wheelIdleAnimName}");
                }
            }

            // Disable player movement but keep collision enabled (parenting handles position)
            if (playerTransform != null)
            {
                // Keep CharacterController ENABLED so collision stays active and follows the ship
                // This ensures the player's collision capsule moves with the rocking ship

                // Disable Player.PlayerController (movement script)
                var newPlayerController = playerTransform.GetComponent<Player.PlayerController>();
                if (newPlayerController != null)
                {
                    newPlayerController.enabled = false;
                    Debug.Log("Disabled Player.PlayerController");
                }

                // Disable Player.PlayerCamera
                var playerCamera = Camera.main?.GetComponent<Player.PlayerCamera>();
                if (playerCamera != null)
                {
                    playerCamera.enabled = false;
                    Debug.Log("Disabled Player.PlayerCamera");
                }

                // Parent player to ship so they move together
                playerTransform.SetParent(transform);
                Debug.Log("Player parented to ship and positioned at wheel");
            }

            Debug.Log("Entered ship control mode");
        }

        private void HandleShipControls()
        {
            // W - Roll down sails and start moving
            if (Input.GetKeyDown(KeyCode.W))
            {
                if (!sailsDown)
                {
                    sailsDown = true;
                    isRollingDown = true;
                    PlayMastAnimation("rolldown", WrapMode.Once);
                    StartCoroutine(SwitchToIdleAfterRollDown());
                    Debug.Log("Rolling down sails - ship will start moving forward");
                }
            }

            // S - Roll up sails and stop moving
            if (Input.GetKeyDown(KeyCode.S))
            {
                if (sailsDown)
                {
                    sailsDown = false;
                    isRollingDown = false;
                    StopAllCoroutines(); // Stop the idle switch coroutine
                    PlayMastAnimation("rollup", WrapMode.Once);
                    StartCoroutine(SwitchToTiedUpAfterRollUp());
                    Debug.Log("Rolling up sails - ship will stop");
                }
            }

            // Continuous automatic movement while sails are down
            if (sailsDown)
            {
                // Gradually accelerate to target speed
                currentSpeed = Mathf.MoveTowards(currentSpeed, moveSpeed, acceleration * Time.deltaTime);

                // Move forward automatically using base rotation (not affected by bobbing)
                Vector3 forwardDirection = baseRotation * Vector3.forward;
                basePosition -= forwardDirection * currentSpeed * Time.deltaTime;
            }
            else
            {
                // Gradually decelerate when sails are up
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, acceleration * Time.deltaTime);

                // Continue moving if still have momentum
                if (currentSpeed > 0f)
                {
                    Vector3 forwardDirection = baseRotation * Vector3.forward;
                    basePosition -= forwardDirection * currentSpeed * Time.deltaTime;
                }
            }

            // Rotation with A/D (only affects Y-axis heading)
            if (Input.GetKey(KeyCode.A))
            {
                baseRotation *= Quaternion.Euler(0, -rotateSpeed * Time.deltaTime, 0);
            }

            if (Input.GetKey(KeyCode.D))
            {
                baseRotation *= Quaternion.Euler(0, rotateSpeed * Time.deltaTime, 0);
            }
        }

        private void ApplyBobbing()
        {
            bobbingTime += Time.deltaTime;

            // Reduce bobbing when not controlling (50% intensity)
            float intensityMultiplier = isControlling ? 1.0f : 0.5f;

            // Calculate vertical bobbing (up and down)
            float yOffset = Mathf.Sin(bobbingTime * bobbingHeightSpeed) * bobbingHeightAmount * intensityMultiplier;

            // Calculate pitch (front-back tilt)
            float pitch = Mathf.Sin(bobbingTime * bobbingRotationSpeed * 0.7f) * bobbingRotationAmount * intensityMultiplier;

            // Calculate roll (side-to-side tilt)
            float roll = Mathf.Sin(bobbingTime * bobbingRotationSpeed * 1.3f) * bobbingRotationAmount * intensityMultiplier;

            // Apply position with bobbing offset
            transform.position = basePosition + new Vector3(0, yOffset, 0);

            // Apply rotation with bobbing
            transform.rotation = baseRotation * Quaternion.Euler(pitch, 0, roll);
        }

        private IEnumerator SwitchToIdleAfterRollDown()
        {
            // Wait for rolldown animation to finish - use the longest animation length
            float maxLength = 0f;
            foreach (MastAnimationData mastData in mastAnimations)
            {
                if (mastData.rollDownClip != null && mastData.rollDownClip.length > maxLength)
                {
                    maxLength = mastData.rollDownClip.length;
                }
            }

            if (maxLength > 0f)
            {
                yield return new WaitForSeconds(maxLength);
            }
            else
            {
                yield return new WaitForSeconds(1f);
            }

            // Switch to idle loop
            isRollingDown = false;
            PlayMastAnimation("idle", WrapMode.Loop);
            Debug.Log("Sails now in idle state");
        }

        private IEnumerator SwitchToTiedUpAfterRollUp()
        {
            // Wait for rollup animation to finish - use the longest animation length
            float maxLength = 0f;
            foreach (MastAnimationData mastData in mastAnimations)
            {
                if (mastData.rollUpClip != null && mastData.rollUpClip.length > maxLength)
                {
                    maxLength = mastData.rollUpClip.length;
                }
            }

            if (maxLength > 0f)
            {
                yield return new WaitForSeconds(maxLength);
            }
            else
            {
                yield return new WaitForSeconds(1f);
            }

            // Switch to tied up loop
            PlayMastAnimation("tiedup", WrapMode.Loop);
            Debug.Log("Sails now tied up");
        }

        private void HandleCameraOrbit()
        {
            // Handle zoom with mouse scroll wheel
            float scrollInput = Input.GetAxis("Mouse ScrollWheel");
            if (scrollInput != 0f)
            {
                orbitDistance -= scrollInput * zoomSpeed;
                orbitDistance = Mathf.Clamp(orbitDistance, minZoomDistance, maxZoomDistance);
            }

            // Allow orbiting with right mouse button
            if (Input.GetMouseButton(1))
            {
                // Get mouse movement
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");

                // Update orbit angles
                currentOrbitAngle += mouseX * orbitSpeed * Time.deltaTime;
                currentOrbitPitch -= mouseY * orbitSpeed * Time.deltaTime;

                // Clamp pitch to avoid flipping
                currentOrbitPitch = Mathf.Clamp(currentOrbitPitch, -80f, 80f);
            }

            // Calculate camera focus point (player character or ship center)
            Vector3 focusPoint;
            if (playerTransform != null)
            {
                // Focus on player character (slightly above their position)
                focusPoint = playerTransform.position + Vector3.up * 3f;
            }
            else
            {
                // Fallback to ship center if player not found
                focusPoint = transform.position + Vector3.up * orbitHeight;
            }

            // Calculate camera orbit position
            Quaternion rotation = Quaternion.Euler(currentOrbitPitch, currentOrbitAngle, 0);
            Vector3 offset = rotation * new Vector3(0, 0, -orbitDistance);

            // Position camera and look at focus point (player character)
            mainCamera.transform.position = focusPoint + offset;
            mainCamera.transform.LookAt(focusPoint);
        }

        private void HandleCannonControls()
        {
            // Left broadside
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                Debug.Log($"Firing {leftBroadsideCannons.Count} left broadside cannons!");
                StartCoroutine(FireBroadsideCannons(leftBroadsideCannons));
            }

            // Right broadside
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                Debug.Log($"Firing {rightBroadsideCannons.Count} right broadside cannons!");
                StartCoroutine(FireBroadsideCannons(rightBroadsideCannons));
            }
        }

        private void HandleExitControl()
        {
            if (Input.GetKeyDown(exitControlKey))
            {
                ExitShipControl();
            }
        }

        private void ExitShipControl()
        {
            isControlling = false;

            // Unparent player from ship
            if (playerTransform != null)
            {
                playerTransform.SetParent(null);
                Debug.Log("Player unparented from ship");
            }

            // Move player back onto the ship at the wheel position (slightly above deck)
            if (playerTransform != null && wheelTransform != null)
            {
                // Position player at the wheel, raised up to avoid falling through
                playerTransform.position = wheelTransform.position + Vector3.up * 2f + Vector3.back * 2f;
                playerTransform.rotation = Quaternion.LookRotation(wheelTransform.forward);
            }

            // Restore camera to original state
            if (mainCamera != null)
            {
                mainCamera.transform.SetParent(originalCameraParent);
                mainCamera.transform.localPosition = originalCameraPosition;
                mainCamera.transform.localRotation = originalCameraRotation;
            }

            // Re-enable player movement components (CharacterController stays enabled)
            if (playerTransform != null)
            {
                // Reset to idle animation before re-enabling SimpleAnimationPlayer
                Animation playerAnim = null;
                Transform modelChild = playerTransform.Find("Model");
                if (modelChild != null)
                {
                    playerAnim = modelChild.GetComponent<Animation>();
                }
                if (playerAnim == null)
                {
                    playerAnim = playerTransform.GetComponentInChildren<Animation>();
                }

                if (playerAnim != null)
                {
                    // Stop wheel_idle animation
                    playerAnim.Stop();

                    // Remove wheel_idle clip
                    if (playerAnim.GetClip("wheel_idle") != null)
                    {
                        playerAnim.RemoveClip("wheel_idle");
                    }

                    // Play idle animation if it exists
                    if (playerAnim.GetClip("idle") != null)
                    {
                        playerAnim.Play("idle");
                        Debug.Log("Reset player to idle animation");
                    }
                }

                // Re-enable Player.PlayerController (movement script)
                var newPlayerController = playerTransform.GetComponent<Player.PlayerController>();
                if (newPlayerController != null)
                {
                    newPlayerController.enabled = true;
                    Debug.Log("Re-enabled Player.PlayerController");
                }

                // Re-enable Player.PlayerCamera
                var playerCamera = Camera.main?.GetComponent<Player.PlayerCamera>();
                if (playerCamera != null)
                {
                    playerCamera.enabled = true;
                    Debug.Log("Re-enabled Player.PlayerCamera");
                }

                // Re-enable SimpleAnimationPlayer to restore normal animations
                Player.SimpleAnimationPlayer animPlayer = playerTransform.GetComponent<Player.SimpleAnimationPlayer>();
                if (animPlayer != null)
                {
                    animPlayer.enabled = true;
                    Debug.Log("Re-enabled SimpleAnimationPlayer");
                }
            }

            Debug.Log("Exited ship control mode - returned to ship");
        }

        private void PlayMastAnimation(string animType, WrapMode wrapMode)
        {
            Debug.Log($"[MAST ANIM] Playing '{animType}' animation on all masts with wrapMode: {wrapMode}");
            Debug.Log($"[MAST ANIM] Number of masts: {mastAnimations.Count}");

            int played = 0;
            foreach (MastAnimationData mastData in mastAnimations)
            {
                if (mastData.animation == null)
                {
                    Debug.LogWarning($"[MAST ANIM] Animation component is null for mast type: {mastData.mastType}");
                    continue;
                }

                // Get the appropriate clip for this animation type
                AnimationClip clip = null;
                string clipName = animType;

                switch (animType.ToLower())
                {
                    case "tiedup":
                        clip = mastData.tiedUpClip;
                        break;
                    case "rollup":
                        clip = mastData.rollUpClip;
                        break;
                    case "rolldown":
                        clip = mastData.rollDownClip;
                        break;
                    case "idle":
                        clip = mastData.idleClip;
                        break;
                }

                if (clip == null)
                {
                    Debug.LogWarning($"[MAST ANIM] No '{animType}' clip found for mast type: {mastData.mastType}");
                    continue;
                }

                Debug.Log($"[MAST ANIM] Playing on {mastData.animation.gameObject.name} (type: {mastData.mastType})");
                Debug.Log($"[MAST ANIM]   Clip: {clip.name}, length: {clip.length}s");

                // Stop all animations first
                mastData.animation.Stop();

                // Clear existing clips
                mastData.animation.clip = clip;

                // Remove old clip if exists
                if (mastData.animation.GetClip(clipName) != null)
                {
                    mastData.animation.RemoveClip(clipName);
                }

                // Add and play new clip
                mastData.animation.AddClip(clip, clipName);
                mastData.animation[clipName].wrapMode = wrapMode;
                mastData.animation[clipName].speed = 1.0f;
                mastData.animation.Play(clipName);

                // Verify it's actually playing
                bool isPlaying = mastData.animation.isPlaying;
                Debug.Log($"[MAST ANIM]   Animation.isPlaying: {isPlaying}");

                if (mastData.animation[clipName] != null)
                {
                    Debug.Log($"[MAST ANIM]   AnimationState - enabled: {mastData.animation[clipName].enabled}, weight: {mastData.animation[clipName].weight}");
                }

                played++;
            }

            Debug.Log($"[MAST ANIM] Completed: played on {played}/{mastAnimations.Count} masts");
        }

        private IEnumerator FireBroadsideCannons(List<GameObject> cannons)
        {
            if (cannonOpenClip == null || cannonFireClip == null || cannonCloseClip == null)
            {
                Debug.LogWarning($"Cannon animation clips not loaded - Open: {cannonOpenClip != null}, Fire: {cannonFireClip != null}, Close: {cannonCloseClip != null}");
                yield break;
            }

            foreach (GameObject cannon in cannons)
            {
                if (cannon != null)
                {
                    StartCoroutine(PlayCannonSequence(cannon));
                }

                // Random delay between 0 and 1.5 seconds
                yield return new WaitForSeconds(Random.Range(0f, 1.5f));
            }

            Debug.Log($"Fired {cannons.Count} cannons");
        }

        private IEnumerator PlayCannonSequence(GameObject cannon)
        {
            Animation anim = cannon.GetComponent<Animation>();
            if (anim == null)
            {
                anim = cannon.AddComponent<Animation>();
            }

            // Open
            anim.Stop();
            anim.clip = cannonOpenClip;
            anim.AddClip(cannonOpenClip, "open");
            anim.wrapMode = WrapMode.Once;
            anim.Play("open");
            yield return new WaitForSeconds(cannonOpenClip.length);

            // Fire
            anim.Stop();
            anim.clip = cannonFireClip;
            anim.AddClip(cannonFireClip, "fire");
            anim.wrapMode = WrapMode.Once;
            anim.Play("fire");
            yield return new WaitForSeconds(cannonFireClip.length);

            // Close
            anim.Stop();
            anim.clip = cannonCloseClip;
            anim.AddClip(cannonCloseClip, "close");
            anim.wrapMode = WrapMode.Once;
            anim.Play("close");
        }

        private Transform FindMastModel(Transform mastLocator)
        {
            // The mastLocator IS the mast model (it was renamed by ShipAssembler)
            // We just need to verify it has the skeletal structure inside

            // Look for a SkinnedMeshRenderer inside to confirm this is a mast
            SkinnedMeshRenderer skinnedMesh = mastLocator.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skinnedMesh != null)
            {
                Debug.Log($"[FindMastModel] Found mast root at {mastLocator.name} (has SkinnedMeshRenderer: {skinnedMesh.name})");
                return mastLocator; // Return the locator itself as it IS the mast root
            }

            Debug.LogWarning($"[FindMastModel] No SkinnedMeshRenderer found in {mastLocator.name} - not a valid mast");
            return null;
        }

        private Transform FindChildRecursive(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child;

                Transform found = FindChildRecursive(child, childName);
                if (found != null)
                    return found;
            }
            return null;
        }

        private string GetGameObjectPath(Transform transform)
        {
            string path = transform.name;
            Transform parent = transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        private string GetMastType(Transform mastRoot)
        {
            // Check for MastTypeInfo component (added by ShipAssembler)
            MastTypeInfo typeInfo = mastRoot.GetComponent<MastTypeInfo>();
            if (typeInfo != null && !string.IsNullOrEmpty(typeInfo.mastType))
            {
                Debug.Log($"[GetMastType] Found MastTypeInfo: {typeInfo.mastType}");
                return typeInfo.mastType;
            }

            // Fallback: try to extract from locator name
            string locatorName = mastRoot.name.ToLower();
            Debug.Log($"[GetMastType] No MastTypeInfo, falling back to locator name: {mastRoot.name}");

            if (locatorName.Contains("mainmast"))
                return "main_tri";
            else if (locatorName.Contains("foremast"))
                return "fore_tri";
            else if (locatorName.Contains("aftmast"))
                return "aft_tri";

            Debug.LogWarning($"[GetMastType] Could not determine mast type, using default: main_tri");
            return "main_tri";
        }

        private void AddDeckColliders()
        {
            Debug.Log("🔧 Adding colliders to ship deck...");
            int colliderCount = 0;

            // Add colliders to all mesh renderers on the ship (deck, hull, etc.)
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
            foreach (MeshFilter meshFilter in meshFilters)
            {
                // Skip if already has a collider
                if (meshFilter.GetComponent<Collider>() != null)
                    continue;

                // Skip masts and cannons (they shouldn't have collision)
                if (meshFilter.name.ToLower().Contains("mast") ||
                    meshFilter.name.ToLower().Contains("cannon") ||
                    meshFilter.name.ToLower().Contains("sail"))
                    continue;

                // Add mesh collider to deck/hull pieces
                if (meshFilter.sharedMesh != null)
                {
                    MeshCollider meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = meshFilter.sharedMesh;
                    meshCollider.convex = false;
                    colliderCount++;
                }
            }

            Debug.Log($"✅ Added {colliderCount} colliders to ship deck - player can now walk on it");
        }

        private void OnDrawGizmosSelected()
        {
            // Draw interaction range around wheel
            if (wheelTransform != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(wheelTransform.position, interactionDistance);
            }
        }
    }
}
