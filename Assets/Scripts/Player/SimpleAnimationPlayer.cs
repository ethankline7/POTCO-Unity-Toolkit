/// <summary>
/// Simple animation player - no AnimatorController needed!
/// Automatically detects gender (fp_ or mp_) and plays animations based on player state
/// </summary>
using UnityEngine;
using System.Collections.Generic;

namespace Player
{
    public enum GenderType
    {
        Male,
        Female
    }

    [RequireComponent(typeof(Animation))]
    public class SimpleAnimationPlayer : MonoBehaviour
    {
        [Header("Gender Detection")]
        [SerializeField] private bool autoDetectGender = true;
        [Tooltip("Manually override gender detection if auto-detection fails")]
        [SerializeField] private bool manualGenderOverride = false;
        [SerializeField] private GenderType manualGender = GenderType.Male;

        [Header("Detected Gender (Read-Only)")]
        [SerializeField] private string genderPrefix = "mp_"; // mp_ for male, fp_ for female

        [Header("Animation Clips")]
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private AnimationClip walkClip;
        [SerializeField] private AnimationClip runClip;
        [SerializeField] private AnimationClip walkBackClip;
        [SerializeField] private AnimationClip runBackClip;
        [SerializeField] private AnimationClip strafeLeftClip;
        [SerializeField] private AnimationClip strafeRightClip;
        [SerializeField] private AnimationClip runDiagonalLeftClip;
        [SerializeField] private AnimationClip runDiagonalRightClip;
        [SerializeField] private AnimationClip walkBackDiagonalLeftClip;
        [SerializeField] private AnimationClip walkBackDiagonalRightClip;
        [SerializeField] private AnimationClip turnLeftClip;
        [SerializeField] private AnimationClip turnRightClip;
        [SerializeField] private AnimationClip spinLeftClip;  // Female turn animation
        [SerializeField] private AnimationClip spinRightClip; // Female turn animation
        [SerializeField] private AnimationClip jumpClip;
        [SerializeField] private AnimationClip swimClip;

        private Animation animComponent;
        private PlayerController playerController;
        private string currentAnim = "";
        private bool isInitialized = false;
        private bool wasGrounded = true; // Track if we were grounded last frame
        private bool wasFalling = false; // Track if we were falling last frame for landing detection
        private bool isInJumpAir = false; // Track if we're in the air portion of jump
        private bool jumpAnimReversing = false; // Track if jump animation is playing backwards
        private bool isPlayingLanding = false; // Track if we're playing the landing animation

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();

            // Use manual override if enabled
            if (manualGenderOverride)
            {
                genderPrefix = manualGender == GenderType.Female ? "fp_" : "mp_";
                Debug.Log($"🎭 Manual gender override: {manualGender} ({genderPrefix} prefix)");
            }
            // Otherwise auto-detect gender from model name
            else if (autoDetectGender)
            {
                DetectGender();
            }
        }

        /// <summary>
        /// Set gender explicitly before initialization (used by spawner scripts)
        /// Call this immediately after AddComponent and before Start()
        /// </summary>
        public void SetGender(string gender)
        {
            if (gender == "f" || gender == "female")
            {
                genderPrefix = "fp_";
                manualGenderOverride = true;
                manualGender = GenderType.Female;
                Debug.Log($"🎭 Gender set programmatically: Female (fp_ prefix)");
            }
            else
            {
                genderPrefix = "mp_";
                manualGenderOverride = true;
                manualGender = GenderType.Male;
                Debug.Log($"🎭 Gender set programmatically: Male (mp_ prefix)");
            }
        }

        private void Start()
        {
            // Find Animation component AFTER PlayerController sets up hierarchy
            Debug.Log("🔍 SimpleAnimationPlayer searching for Animation component...");

            // Check Model child first (created by PlayerController)
            Transform modelChild = transform.Find("Model");
            if (modelChild != null)
            {
                Debug.Log($"   Found Model child at: {modelChild.name}");
                animComponent = modelChild.GetComponent<Animation>();
                if (animComponent != null)
                {
                    Debug.Log($"✅ Found Animation component on Model child");
                }
            }
            else
            {
                Debug.Log("   No Model child found");
            }

            // If not found, check this object
            if (animComponent == null)
            {
                animComponent = GetComponent<Animation>();
                if (animComponent != null)
                {
                    Debug.Log($"✅ Found Animation component on this object");
                }
            }

            // If still not found, search all children
            if (animComponent == null)
            {
                animComponent = GetComponentInChildren<Animation>();
                if (animComponent != null)
                {
                    Debug.Log($"✅ Found Animation component on child: {animComponent.gameObject.name}");
                }
            }

            if (animComponent == null)
            {
                Debug.LogError("❌ No Animation component found anywhere!");
                return;
            }

            // Auto-load animations
            LoadAnimations();

            if (isInitialized)
            {
                Debug.Log($"✅ SimpleAnimationPlayer initialized with gender prefix: {genderPrefix}");

                // Build list of loaded animations
                System.Text.StringBuilder loadedAnims = new System.Text.StringBuilder("   Loaded: ");
                if (idleClip != null) loadedAnims.Append("idle ");
                if (walkClip != null) loadedAnims.Append("walk ");
                if (runClip != null) loadedAnims.Append("run ");
                if (walkBackClip != null) loadedAnims.Append("walk_back ");
                if (runBackClip != null) loadedAnims.Append("run_back ");
                if (strafeLeftClip != null) loadedAnims.Append("strafe_left ");
                if (strafeRightClip != null) loadedAnims.Append("strafe_right ");
                if (runDiagonalLeftClip != null) loadedAnims.Append("run_diagonal_left ");
                if (runDiagonalRightClip != null) loadedAnims.Append("run_diagonal_right ");
                if (walkBackDiagonalLeftClip != null) loadedAnims.Append("walk_back_diagonal_left ");
                if (walkBackDiagonalRightClip != null) loadedAnims.Append("walk_back_diagonal_right ");
                if (turnLeftClip != null) loadedAnims.Append("turn_left ");
                if (turnRightClip != null) loadedAnims.Append("turn_right ");
                if (spinLeftClip != null) loadedAnims.Append("spin_left ");
                if (spinRightClip != null) loadedAnims.Append("spin_right ");
                if (jumpClip != null) loadedAnims.Append("jump ");
                if (swimClip != null) loadedAnims.Append("swim ");

                Debug.Log(loadedAnims.ToString());

                // Warn about missing optional animations
                if (walkBackClip == null)
                    Debug.LogWarning($"⚠️ No backward walk animation found. Tried: {genderPrefix}walk_back, {genderPrefix}walk_backward, {genderPrefix}walkback");
                if (strafeLeftClip == null)
                    Debug.LogWarning($"⚠️ No left strafe animation found. Tried: {genderPrefix}strafe_left, {genderPrefix}walk_left, {genderPrefix}strafeleft");
                if (strafeRightClip == null)
                    Debug.LogWarning($"⚠️ No right strafe animation found. Tried: {genderPrefix}strafe_right, {genderPrefix}walk_right, {genderPrefix}straferight");
                if (turnLeftClip == null)
                    Debug.LogWarning($"⚠️ No left turn animation found. Tried: {genderPrefix}turn_left");
                if (turnRightClip == null)
                    Debug.LogWarning($"⚠️ No right turn animation found. Tried: {genderPrefix}turn_right");
            }
        }

        private void Update()
        {
            if (!isInitialized || playerController == null) return;

            // Check if landing animation has finished
            if (isPlayingLanding && jumpClip != null)
            {
                AnimationState jumpState = animComponent["jump"];
                if (jumpState != null)
                {
                    // Landing animation is from 63% to 100%
                    // Check if we've reached the end or animation stopped
                    if (jumpState.time >= jumpState.length - 0.05f || !jumpState.enabled)
                    {
                        // Landing animation finished
                        isPlayingLanding = false;
                        // Restore jump WrapMode for future jumps
                        jumpState.wrapMode = WrapMode.ClampForever;
                    }
                }
            }

            UpdateAnimation();

            // Handle jump in-air ping-pong looping (34%-50% back and forth)
            // Only loop when actually falling (not just slightly off ground)
            if (isInJumpAir && playerController.IsFalling && jumpClip != null)
            {
                AnimationState jumpState = animComponent["jump"];
                if (jumpState != null && jumpState.enabled)
                {
                    // Ping-pong between 34% and 50% of the animation (the in-air idle portion)
                    float jumpLength = jumpState.length;
                    float midStart = jumpLength * 0.34f;
                    float midEnd = jumpLength * 0.5f;

                    // Check if we're in the takeoff phase (before 34%)
                    if (jumpState.time < midStart && !jumpAnimReversing)
                    {
                        // Still in takeoff - play at normal speed
                        jumpState.speed = 1f;
                    }
                    else if (!jumpAnimReversing)
                    {
                        // In air loop - playing forward
                        if (jumpState.time >= midEnd)
                        {
                            // Reached end, reverse direction
                            jumpAnimReversing = true;
                            jumpState.speed = -0.3f; // Play backwards slowly for smooth motion
                        }
                        else
                        {
                            jumpState.speed = 0.3f; // Play forwards slowly for smooth motion
                        }
                    }
                    else
                    {
                        // In air loop - playing backward
                        if (jumpState.time <= midStart)
                        {
                            // Reached start, reverse direction
                            jumpAnimReversing = false;
                            jumpState.speed = 0.3f; // Play forwards slowly for smooth motion
                        }
                        else
                        {
                            jumpState.speed = -0.3f; // Play backwards slowly for smooth motion
                        }
                    }
                }
            }
        }

        private void DetectGender()
        {
            Debug.Log("🔍 Starting gender detection...");

            // Method 0: Check for CharacterGenderData component (highest priority)
            CharacterOG.Runtime.CharacterGenderData genderData = GetComponent<CharacterOG.Runtime.CharacterGenderData>();
            if (genderData != null)
            {
                string gender = genderData.GetGender();
                genderPrefix = genderData.GetGenderPrefix();
                manualGender = gender == "f" ? GenderType.Female : GenderType.Male;
                Debug.Log($"🎭 Detected {(gender == "f" ? "FEMALE" : "MALE")} character from CharacterGenderData component ({genderPrefix} prefix)");
                return;
            }

            // Method 1: Check GameObject name and parent names (going UP the hierarchy)
            Transform current = transform;
            int hierarchyLevel = 0;
            while (current != null)
            {
                string objName = current.name.ToLower();
                Debug.Log($"  Checking parent hierarchy level {hierarchyLevel}: '{current.name}'");

                if (objName.Contains("fp_") || objName.Contains("female"))
                {
                    genderPrefix = "fp_";
                    manualGender = GenderType.Female;
                    Debug.Log($"🎭 Detected FEMALE character from parent hierarchy name: '{current.name}' (fp_ prefix)");
                    return;
                }
                else if (objName.Contains("mp_") || objName.Contains("male"))
                {
                    genderPrefix = "mp_";
                    manualGender = GenderType.Male;
                    Debug.Log($"🎭 Detected MALE character from parent hierarchy name: '{current.name}' (mp_ prefix)");
                    return;
                }
                current = current.parent;
                hierarchyLevel++;
            }

            // Method 1.5: Check ALL child GameObject names (going DOWN the hierarchy)
            // .egg files often create child objects with gender-specific names
            Transform[] allChildren = GetComponentsInChildren<Transform>();
            Debug.Log($"  Found {allChildren.Length} child transforms");

            foreach (Transform child in allChildren)
            {
                if (child == transform) continue; // Skip self

                string childName = child.name.ToLower();

                if (childName.Contains("fp_") || childName.Contains("female"))
                {
                    genderPrefix = "fp_";
                    manualGender = GenderType.Female;
                    Debug.Log($"🎭 Detected FEMALE character from child object name: '{child.name}' (fp_ prefix)");
                    return;
                }
                else if (childName.Contains("mp_") || childName.Contains("male"))
                {
                    genderPrefix = "mp_";
                    manualGender = GenderType.Male;
                    Debug.Log($"🎭 Detected MALE character from child object name: '{child.name}' (mp_ prefix)");
                    return;
                }
            }

            // Method 2: Check SkinnedMeshRenderer mesh names
            SkinnedMeshRenderer[] skinnedMeshes = GetComponentsInChildren<SkinnedMeshRenderer>();
            Debug.Log($"  Found {skinnedMeshes.Length} SkinnedMeshRenderers");

            foreach (SkinnedMeshRenderer smr in skinnedMeshes)
            {
                if (smr.sharedMesh != null)
                {
                    string meshName = smr.sharedMesh.name.ToLower();
                    Debug.Log($"  Checking mesh: '{smr.sharedMesh.name}'");

                    if (meshName.Contains("fp_") || meshName.Contains("female"))
                    {
                        genderPrefix = "fp_";
                        manualGender = GenderType.Female;
                        Debug.Log($"🎭 Detected FEMALE character from mesh name: '{smr.sharedMesh.name}' (fp_ prefix)");
                        return;
                    }
                    else if (meshName.Contains("mp_") || meshName.Contains("male"))
                    {
                        genderPrefix = "mp_";
                        manualGender = GenderType.Male;
                        Debug.Log($"🎭 Detected MALE character from mesh name: '{smr.sharedMesh.name}' (mp_ prefix)");
                        return;
                    }
                }
            }

            // Method 3: Check MeshFilter mesh names
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
            Debug.Log($"  Found {meshFilters.Length} MeshFilters");

            foreach (MeshFilter mf in meshFilters)
            {
                if (mf.sharedMesh != null)
                {
                    string meshName = mf.sharedMesh.name.ToLower();
                    Debug.Log($"  Checking mesh: '{mf.sharedMesh.name}'");

                    if (meshName.Contains("fp_") || meshName.Contains("female"))
                    {
                        genderPrefix = "fp_";
                        manualGender = GenderType.Female;
                        Debug.Log($"🎭 Detected FEMALE character from mesh name: '{mf.sharedMesh.name}' (fp_ prefix)");
                        return;
                    }
                    else if (meshName.Contains("mp_") || meshName.Contains("male"))
                    {
                        genderPrefix = "mp_";
                        manualGender = GenderType.Male;
                        Debug.Log($"🎭 Detected MALE character from mesh name: '{mf.sharedMesh.name}' (mp_ prefix)");
                        return;
                    }
                }
            }

            // Default to male if not detected
            genderPrefix = "mp_";
            manualGender = GenderType.Male;
            Debug.LogWarning("⚠️ Could not detect gender from names. Using default MALE character (mp_ prefix)");
            Debug.LogWarning("   If this is wrong, enable 'Manual Gender Override' in Inspector and set to Female");
        }

        private void LoadAnimations()
        {
            Debug.Log($"🔍 Loading animations with prefix: {genderPrefix}");

            // Search all phase directories
            string[] phases = { "phase_2", "phase_3", "phase_4", "phase_5", "phase_6" };
            string[] searchPaths = { "char", "models/char" };

            // Load each animation type
            idleClip = FindAndLoadClip("idle", phases, searchPaths);
            walkClip = FindAndLoadClip("walk", phases, searchPaths);
            runClip = FindAndLoadClip("run", phases, searchPaths);

            // Load directional animations (try multiple naming conventions)
            walkBackClip = FindAndLoadClip("walk_back", phases, searchPaths);
            if (walkBackClip == null) walkBackClip = FindAndLoadClip("walk_backward", phases, searchPaths);
            if (walkBackClip == null) walkBackClip = FindAndLoadClip("walkback", phases, searchPaths);

            runBackClip = FindAndLoadClip("run_back", phases, searchPaths);
            if (runBackClip == null) runBackClip = FindAndLoadClip("run_backward", phases, searchPaths);
            if (runBackClip == null) runBackClip = FindAndLoadClip("runback", phases, searchPaths);

            strafeLeftClip = FindAndLoadClip("strafe_left", phases, searchPaths);
            if (strafeLeftClip == null) strafeLeftClip = FindAndLoadClip("walk_left", phases, searchPaths);
            if (strafeLeftClip == null) strafeLeftClip = FindAndLoadClip("strafeleft", phases, searchPaths);

            strafeRightClip = FindAndLoadClip("strafe_right", phases, searchPaths);
            if (strafeRightClip == null) strafeRightClip = FindAndLoadClip("walk_right", phases, searchPaths);
            if (strafeRightClip == null) strafeRightClip = FindAndLoadClip("straferight", phases, searchPaths);

            runDiagonalLeftClip = FindAndLoadClip("run_diagonal_left", phases, searchPaths);
            runDiagonalRightClip = FindAndLoadClip("run_diagonal_right", phases, searchPaths);
            walkBackDiagonalLeftClip = FindAndLoadClip("walk_back_diagonal_left", phases, searchPaths);
            walkBackDiagonalRightClip = FindAndLoadClip("walk_back_diagonal_right", phases, searchPaths);

            turnLeftClip = FindAndLoadClip("turn_left", phases, searchPaths);
            turnRightClip = FindAndLoadClip("turn_right", phases, searchPaths);

            spinLeftClip = FindAndLoadClip("spin_left", phases, searchPaths);
            spinRightClip = FindAndLoadClip("spin_right", phases, searchPaths);

            jumpClip = FindAndLoadClip("jump", phases, searchPaths);
            swimClip = FindAndLoadClip("swim", phases, searchPaths);

            // Add clips to Animation component and set them to loop
            if (idleClip != null)
            {
                animComponent.AddClip(idleClip, "idle");
                animComponent["idle"].wrapMode = WrapMode.Loop;
            }
            if (walkClip != null)
            {
                animComponent.AddClip(walkClip, "walk");
                animComponent["walk"].wrapMode = WrapMode.Loop;
            }
            if (runClip != null)
            {
                animComponent.AddClip(runClip, "run");
                animComponent["run"].wrapMode = WrapMode.Loop;
            }
            if (walkBackClip != null)
            {
                animComponent.AddClip(walkBackClip, "walk_back");
                animComponent["walk_back"].wrapMode = WrapMode.Loop;
            }
            if (runBackClip != null)
            {
                animComponent.AddClip(runBackClip, "run_back");
                animComponent["run_back"].wrapMode = WrapMode.Loop;
            }
            if (strafeLeftClip != null)
            {
                animComponent.AddClip(strafeLeftClip, "strafe_left");
                animComponent["strafe_left"].wrapMode = WrapMode.Loop;
            }
            if (strafeRightClip != null)
            {
                animComponent.AddClip(strafeRightClip, "strafe_right");
                animComponent["strafe_right"].wrapMode = WrapMode.Loop;
            }
            if (runDiagonalLeftClip != null)
            {
                animComponent.AddClip(runDiagonalLeftClip, "run_diagonal_left");
                animComponent["run_diagonal_left"].wrapMode = WrapMode.Loop;
            }
            if (runDiagonalRightClip != null)
            {
                animComponent.AddClip(runDiagonalRightClip, "run_diagonal_right");
                animComponent["run_diagonal_right"].wrapMode = WrapMode.Loop;
            }
            if (walkBackDiagonalLeftClip != null)
            {
                animComponent.AddClip(walkBackDiagonalLeftClip, "walk_back_diagonal_left");
                animComponent["walk_back_diagonal_left"].wrapMode = WrapMode.Loop;
            }
            if (walkBackDiagonalRightClip != null)
            {
                animComponent.AddClip(walkBackDiagonalRightClip, "walk_back_diagonal_right");
                animComponent["walk_back_diagonal_right"].wrapMode = WrapMode.Loop;
            }
            if (turnLeftClip != null)
            {
                animComponent.AddClip(turnLeftClip, "turn_left");
                animComponent["turn_left"].wrapMode = WrapMode.Loop;
                animComponent["turn_left"].speed = 0.5f; // 50% slower
            }
            if (turnRightClip != null)
            {
                animComponent.AddClip(turnRightClip, "turn_right");
                animComponent["turn_right"].wrapMode = WrapMode.Loop;
                animComponent["turn_right"].speed = 0.5f; // 50% slower
            }
            if (spinLeftClip != null)
            {
                animComponent.AddClip(spinLeftClip, "spin_left");
                animComponent["spin_left"].wrapMode = WrapMode.Loop;
            }
            if (spinRightClip != null)
            {
                animComponent.AddClip(spinRightClip, "spin_right");
                animComponent["spin_right"].wrapMode = WrapMode.Loop;
            }
            if (jumpClip != null)
            {
                animComponent.AddClip(jumpClip, "jump");
                animComponent["jump"].wrapMode = WrapMode.ClampForever; // Hold last frame
            }
            if (swimClip != null)
            {
                animComponent.AddClip(swimClip, "swim");
                animComponent["swim"].wrapMode = WrapMode.Loop;
            }

            // Check if we have minimum required animations
            if (idleClip != null && walkClip != null)
            {
                isInitialized = true;
                PlayAnimation("idle");
            }
            else
            {
                Debug.LogError($"❌ Failed to load required animations! Check that {genderPrefix}idle and {genderPrefix}walk exist in Resources/phase_*/char/");
                isInitialized = false;
            }
        }

        private AnimationClip FindAndLoadClip(string animName, string[] phases, string[] searchPaths)
        {
            // Try with gender prefix first
            string prefixedName = genderPrefix + animName;

            foreach (string phase in phases)
            {
                foreach (string path in searchPaths)
                {
                    string fullPath = $"{phase}/{path}/{prefixedName}";
                    AnimationClip clip = Resources.Load<AnimationClip>(fullPath);

                    if (clip != null)
                    {
                        Debug.Log($"✅ Loaded: {fullPath}");
                        return clip;
                    }
                }
            }

            // Try without prefix as fallback
            foreach (string phase in phases)
            {
                foreach (string path in searchPaths)
                {
                    string fullPath = $"{phase}/{path}/{animName}";
                    AnimationClip clip = Resources.Load<AnimationClip>(fullPath);

                    if (clip != null)
                    {
                        Debug.Log($"✅ Loaded (no prefix): {fullPath}");
                        return clip;
                    }
                }
            }

            Debug.LogWarning($"⚠️ Could not find animation: {prefixedName} or {animName}");
            return null;
        }

        private void UpdateAnimation()
        {
            // Don't change animation while landing animation is playing
            if (isPlayingLanding)
            {
                return;
            }

            string targetAnim = "idle";

            // Determine which animation to play based on player state
            if (playerController.IsSwimming)
            {
                targetAnim = "swim";
            }
            else if (playerController.IsGrounded)
            {
                float speed = playerController.CurrentSpeed;
                Vector2 input = playerController.MoveInput;
                float turnInput = playerController.TurnInput;
                float strafeInput = playerController.StrafeInput;
                bool isFreeLooking = playerController.IsFreeLooking;
                bool isRunning = playerController.IsRunning;

                // Check if moving
                if (speed > 0.5f && (input.magnitude > 0.1f || Mathf.Abs(strafeInput) > 0.1f))
                {

                    if (isFreeLooking)
                    {
                        // FREE-LOOK MODE: Strafing and diagonal animations

                        // Check if moving diagonally (both X and Y inputs)
                        bool isDiagonal = Mathf.Abs(input.x) > 0.1f && Mathf.Abs(input.y) > 0.1f;

                        if (isDiagonal)
                        {
                            // Diagonal movement
                            if (input.y > 0.1f && input.x < -0.1f)
                            {
                                // Forward + Left (W + A)
                                targetAnim = runDiagonalLeftClip != null ? "run_diagonal_left" : "walk";
                            }
                            else if (input.y > 0.1f && input.x > 0.1f)
                            {
                                // Forward + Right (W + D)
                                targetAnim = runDiagonalRightClip != null ? "run_diagonal_right" : "walk";
                            }
                            else if (input.y < -0.1f && input.x < -0.1f)
                            {
                                // Backward + Left (S + A)
                                targetAnim = walkBackDiagonalLeftClip != null ? "walk_back_diagonal_left" : "walk_back";
                            }
                            else if (input.y < -0.1f && input.x > 0.1f)
                            {
                                // Backward + Right (S + D)
                                targetAnim = walkBackDiagonalRightClip != null ? "walk_back_diagonal_right" : "walk_back";
                            }
                        }
                        else if (Mathf.Abs(input.y) > Mathf.Abs(input.x))
                        {
                            // Moving forward or backward only
                            if (input.y > 0.1f)
                            {
                                // Forward (W key)
                                targetAnim = isRunning && runClip != null ? "run" : "walk";
                            }
                            else if (input.y < -0.1f)
                            {
                                // Backward (S key)
                                if (isRunning && runBackClip != null)
                                {
                                    targetAnim = "run_back";
                                }
                                else if (walkBackClip != null)
                                {
                                    targetAnim = "walk_back";
                                }
                                else
                                {
                                    // Fallback to forward walk if no back animation
                                    targetAnim = "walk";
                                }
                            }
                        }
                        else
                        {
                            // Moving left or right (strafe) only
                            if (input.x < -0.1f)
                            {
                                // Strafe left (A key)
                                targetAnim = strafeLeftClip != null ? "strafe_left" : "walk";
                            }
                            else if (input.x > 0.1f)
                            {
                                // Strafe right (D key)
                                targetAnim = strafeRightClip != null ? "strafe_right" : "walk";
                            }
                        }
                    }
                    else
                    {
                        // NORMAL MODE: Forward/backward movement with W/S, strafing with Q/E
                        // (A/D turns character)

                        // Check if strafing with Q/E
                        if (Mathf.Abs(strafeInput) > 0.1f)
                        {
                            if (strafeInput < -0.1f)
                            {
                                // Strafe left (Q key)
                                targetAnim = strafeLeftClip != null ? "strafe_left" : "walk";
                            }
                            else if (strafeInput > 0.1f)
                            {
                                // Strafe right (E key)
                                targetAnim = strafeRightClip != null ? "strafe_right" : "walk";
                            }
                        }
                        else if (input.y > 0.1f)
                        {
                            // Forward (W key) - no turning
                            targetAnim = isRunning && runClip != null ? "run" : "walk";
                        }
                        else if (input.y < -0.1f)
                        {
                            // Backward (S key)
                            if (isRunning && runBackClip != null)
                            {
                                targetAnim = "run_back";
                            }
                            else if (walkBackClip != null)
                            {
                                targetAnim = "walk_back";
                            }
                            else
                            {
                                targetAnim = "walk";
                            }
                        }
                    }
                }
                else if (!isFreeLooking && Mathf.Abs(turnInput) > 0.1f)
                {
                    // TURNING IN PLACE (normal mode only, not moving)
                    // Female characters use spin animations, males use turn animations
                    if (turnInput < -0.1f)
                    {
                        // Turning left (A key)
                        if (genderPrefix == "fp_")
                        {
                            targetAnim = spinLeftClip != null ? "spin_left" : (turnLeftClip != null ? "turn_left" : "idle");
                        }
                        else
                        {
                            targetAnim = turnLeftClip != null ? "turn_left" : "idle";
                        }
                    }
                    else if (turnInput > 0.1f)
                    {
                        // Turning right (D key)
                        if (genderPrefix == "fp_")
                        {
                            targetAnim = spinRightClip != null ? "spin_right" : (turnRightClip != null ? "turn_right" : "idle");
                        }
                        else
                        {
                            targetAnim = turnRightClip != null ? "turn_right" : "idle";
                        }
                    }
                }
                else
                {
                    targetAnim = "idle";
                }
            }
            else
            {
                // In air
                // Detect landing (was falling, now grounded or back on ground)
                if (wasFalling && !playerController.IsFalling)
                {
                    // Just landed - play landing animation (end portion of jump clip)
                    if (jumpClip != null)
                    {
                        PlayLandingAnimation();
                        isInJumpAir = false;
                    }
                }
                else if (!playerController.IsGrounded)
                {
                    // Not grounded (in air or slightly off ground)
                    if (jumpClip != null)
                    {
                        // If just left ground (was grounded, now not grounded)
                        if (wasGrounded && !playerController.IsGrounded)
                        {
                            // First frame in air - play jump from start (takeoff)
                            AnimationState jumpState = animComponent["jump"];
                            if (jumpState != null)
                            {
                                jumpState.speed = 1f; // Ensure normal speed
                            }
                            animComponent.Play("jump");
                            currentAnim = "jump";
                            isInJumpAir = true;
                            jumpAnimReversing = false; // Start in forward direction
                        }

                        // Only show jump animation if actually falling (not just tiny bumps)
                        if (playerController.IsFalling)
                        {
                            // The Update method will handle ping-pong looping the middle portion
                            targetAnim = "jump";
                        }
                        else
                        {
                            // Slightly off ground but not falling - keep current animation
                            targetAnim = currentAnim;
                        }
                    }
                }
                else
                {
                    // Grounded but in the else branch? Fallback to idle
                    targetAnim = "idle";
                }
            }

            // Track grounded and falling state for next frame
            wasGrounded = playerController.IsGrounded;
            wasFalling = playerController.IsFalling;

            // Only switch if different
            if (targetAnim != currentAnim)
            {
                PlayAnimation(targetAnim);
            }
        }

        private void PlayAnimation(string animName)
        {
            if (!animComponent.GetClip(animName))
            {
                // Fallback to idle if animation doesn't exist
                if (animName != "idle" && animComponent.GetClip("idle"))
                {
                    animName = "idle";
                }
                else
                {
                    return;
                }
            }

            // Use CrossFade for smooth transitions
            animComponent.CrossFade(animName, 0.15f);
            currentAnim = animName;
        }

        private void PlayLandingAnimation()
        {
            // Play the jump animation starting from 63% (the landing portion)
            AnimationState jumpState = animComponent["jump"];
            if (jumpState != null)
            {
                // Set to play from 63% of animation (landing part: 63%-100%)
                jumpState.time = jumpState.length * 0.63f;
                jumpState.speed = 1f; // Reset speed to normal (in case it was reversed)
                jumpState.wrapMode = WrapMode.Once; // Play once, don't loop
                jumpState.enabled = true;
                jumpState.weight = 1.0f;

                animComponent.Play("jump");
                currentAnim = "jump";
                jumpAnimReversing = false; // Reset reversing flag
                isPlayingLanding = true; // Mark that we're playing landing animation
            }
        }

        // Public API
        public string GenderPrefix => genderPrefix;

        public void PlayEmote(string emoteName)
        {
            // Try to load and play emote
            string[] phases = { "phase_2", "phase_3", "phase_4", "phase_5", "phase_6" };
            string[] searchPaths = { "char", "models/char" };

            AnimationClip emoteClip = FindAndLoadClip(emoteName, phases, searchPaths);
            if (emoteClip != null)
            {
                animComponent.AddClip(emoteClip, emoteName);
                animComponent.CrossFade(emoteName, 0.2f);
                currentAnim = emoteName;
            }
        }
    }
}
