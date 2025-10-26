/// <summary>
/// Simple animation player - no AnimatorController needed!
/// Automatically detects gender (fp_ or mp_) and plays animations based on player state
/// </summary>
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

        [Header("Animation Transitions")]
        [Tooltip("Duration for animation crossfade transitions")]
        [SerializeField] private float transitionDuration = 0.15f;

        [Header("Bone Sticking Fix - Manual Reset")]
        [Tooltip("Enable manual bone reset to prevent sticking during transitions")]
        [SerializeField] private bool enableManualBoneReset = true;
        [Tooltip("Duration to lerp orphaned bones back to rest pose")]
        [SerializeField] private float resetDuration = 0.3f;

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
        private POTCO.NPCController npcController;
        private string currentAnim = "";
        private bool isInitialized = false;
        private bool wasGrounded = true; // Track if we were grounded last frame
        private bool wasFalling = false; // Track if we were falling last frame for landing detection
        private bool isInJumpAir = false; // Track if we're in the air portion of jump
        private bool jumpAnimReversing = false; // Track if jump animation is playing backwards
        private bool isPlayingLanding = false; // Track if we're playing the landing animation

        // Manual bone reset system
        private Dictionary<string, Transform> cachedBones = new Dictionary<string, Transform>();
        private Dictionary<string, Quaternion> restPoseRotations = new Dictionary<string, Quaternion>();
        private Dictionary<string, HashSet<string>> animationBones = new Dictionary<string, HashSet<string>>();
        private Coroutine currentResetCoroutine = null;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            npcController = GetComponent<POTCO.NPCController>();

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

        /// <summary>
        /// Helper class to wrap either PlayerController or NPCController
        /// Both now have the same API so this makes SimpleAnimationPlayer controller-agnostic
        /// </summary>
        private class ControllerAdapter
        {
            public bool IsGrounded { get; set; }
            public bool IsSwimming { get; set; }
            public float CurrentSpeed { get; set; }
            public Vector3 Velocity { get; set; }
            public Vector2 MoveInput { get; set; }
            public float TurnInput { get; set; }
            public float StrafeInput { get; set; }
            public bool IsFreeLooking { get; set; }
            public bool IsRunning { get; set; }
            public bool IsFalling { get; set; }
        }

        /// <summary>
        /// Get the active controller (PlayerController or NPCController)
        /// Returns null if neither is active
        /// </summary>
        private ControllerAdapter GetActiveController()
        {
            // Check PlayerController first (possession mode)
            if (playerController != null && playerController.enabled)
            {
                return new ControllerAdapter
                {
                    IsGrounded = playerController.IsGrounded,
                    IsSwimming = playerController.IsSwimming,
                    CurrentSpeed = playerController.CurrentSpeed,
                    Velocity = playerController.Velocity,
                    MoveInput = playerController.MoveInput,
                    TurnInput = playerController.TurnInput,
                    StrafeInput = playerController.StrafeInput,
                    IsFreeLooking = playerController.IsFreeLooking,
                    IsRunning = playerController.IsRunning,
                    IsFalling = playerController.IsFalling
                };
            }

            // Check NPCController (normal NPC mode)
            if (npcController != null && npcController.enabled)
            {
                return new ControllerAdapter
                {
                    IsGrounded = npcController.IsGrounded,
                    IsSwimming = npcController.IsSwimming,
                    CurrentSpeed = npcController.CurrentSpeed,
                    Velocity = npcController.Velocity,
                    MoveInput = npcController.MoveInput,
                    TurnInput = npcController.TurnInput,
                    StrafeInput = npcController.StrafeInput,
                    IsFreeLooking = npcController.IsFreeLooking,
                    IsRunning = npcController.IsRunning,
                    IsFalling = npcController.IsFalling
                };
            }

            return null;
        }

        private void Start()
        {
            // Find Animation component AFTER PlayerController sets up hierarchy
            Debug.Log($"🔍 SimpleAnimationPlayer searching for Animation component on {gameObject.name}...");

            // Check Model child first (created by PlayerController or NPCController)
            Transform modelChild = transform.Find("Model");
            if (modelChild != null)
            {
                Debug.Log($"   Found Model child at: {modelChild.name}");

                // Animation might be on Model or on first child of Model
                animComponent = modelChild.GetComponent<Animation>();
                if (animComponent != null)
                {
                    Debug.Log($"✅ Found Animation component on Model child");
                }
                else if (modelChild.childCount > 0)
                {
                    animComponent = modelChild.GetChild(0).GetComponent<Animation>();
                    if (animComponent != null)
                    {
                        Debug.Log($"✅ Found Animation component on Model's first child: {animComponent.gameObject.name}");
                    }
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
            }

            // If still not found, search all children
            if (animComponent == null)
            {
                animComponent = GetComponentInChildren<Animation>();
            }

            if (animComponent == null)
            {
                Debug.LogError($"Animation component not found on {gameObject.name}");
                return;
            }

            // Check if animations are already loaded (from NPCAnimationPlayer before it was disabled)
            int existingClipCount = 0;
            foreach (AnimationState state in animComponent)
            {
                existingClipCount++;
            }

            Debug.Log($"   Found {existingClipCount} existing animation clips already loaded");

            if (existingClipCount > 0)
            {
                Debug.Log("🔗 Using existing animations (likely from NPCAnimationPlayer)");
                MapExistingNPCAnimations();
            }
            else
            {
                Debug.Log("📥 Loading new animations from Resources");
                LoadAnimations();
            }

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
            if (!isInitialized) return;

            // Get active controller (PlayerController or NPCController)
            ControllerAdapter controller = GetActiveController();
            if (controller == null) return;

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

            UpdateAnimation(controller);

            // Handle jump in-air ping-pong looping (34%-50% back and forth)
            // Only loop when actually falling (not just slightly off ground)
            if (isInJumpAir && controller.IsFalling && jumpClip != null)
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

        private void MapExistingNPCAnimations()
        {
            Debug.Log("🔗 Mapping existing NPC animations to SimpleAnimationPlayer...");

            // List all available animations
            Debug.Log("   Available animations:");
            foreach (AnimationState state in animComponent)
            {
                if (state != null && state.clip != null)
                {
                    Debug.Log($"      - {state.name}");
                }
            }

            // Map existing clips to player animation fields
            foreach (AnimationState state in animComponent)
            {
                if (state == null || state.clip == null) continue;

                string clipName = state.name.ToLower();

                // Map idle
                if (clipName.Contains("idle") && idleClip == null)
                {
                    idleClip = state.clip;
                    Debug.Log($"   ✓ Mapped idle: {state.name}");
                }
                // Map walk
                else if (clipName.Contains("walk") && !clipName.Contains("back") && walkClip == null)
                {
                    walkClip = state.clip;
                    Debug.Log($"   ✓ Mapped walk: {state.name}");
                }
                // Map run (if available)
                else if (clipName.Contains("run") && !clipName.Contains("back") && runClip == null)
                {
                    runClip = state.clip;
                    Debug.Log($"   ✓ Mapped run: {state.name}");
                }
                // Map spin left (for strafing)
                else if (clipName.Contains("spin") && clipName.Contains("left") && strafeLeftClip == null)
                {
                    strafeLeftClip = state.clip;
                    Debug.Log($"   ✓ Mapped strafe_left: {state.name}");
                }
                // Map spin right (for strafing)
                else if (clipName.Contains("spin") && clipName.Contains("right") && strafeRightClip == null)
                {
                    strafeRightClip = state.clip;
                    Debug.Log($"   ✓ Mapped strafe_right: {state.name}");
                }
            }

            // If run not found, use walk
            if (runClip == null && walkClip != null)
            {
                runClip = walkClip;
                Debug.Log("   ℹ Using walk animation for run (no run animation found)");
            }

            // Add standard clip names for PlayAnimation to use (only if they don't already exist)
            if (idleClip != null && !animComponent.GetClip("idle"))
            {
                animComponent.AddClip(idleClip, "idle");
            }
            if (walkClip != null && !animComponent.GetClip("walk"))
            {
                animComponent.AddClip(walkClip, "walk");
            }
            if (runClip != null && !animComponent.GetClip("run"))
            {
                animComponent.AddClip(runClip, "run");
            }
            if (strafeLeftClip != null && !animComponent.GetClip("strafe_left"))
            {
                animComponent.AddClip(strafeLeftClip, "strafe_left");
            }
            if (strafeRightClip != null && !animComponent.GetClip("strafe_right"))
            {
                animComponent.AddClip(strafeRightClip, "strafe_right");
            }

            // Check if we have minimum required animations
            if (idleClip != null && walkClip != null)
            {
                isInitialized = true;

                // Set up manual bone reset system if enabled
                if (enableManualBoneReset)
                {
                    SetupManualBoneReset();
                }

                PlayAnimation("idle");
                Debug.Log("✅ Successfully mapped NPC animations to SimpleAnimationPlayer!");
            }
            else
            {
                Debug.LogError($"❌ Missing required animations! idle: {(idleClip != null ? "✓" : "✗")}, walk: {(walkClip != null ? "✓" : "✗")}");
                isInitialized = false;
            }
        }

        private void LoadAnimations()
        {
            Debug.Log($"🔍 Loading animations with prefix: {genderPrefix}");

            // Search all phase directories
            string[] phases = { "phase_2", "phase_3", "phase_4", "phase_5", "phase_6" };
            string[] searchPaths = { "char", "models/char" };

            // Only auto-load if not manually assigned in Inspector
            if (idleClip == null) idleClip = FindAndLoadClip("idle", phases, searchPaths);
            if (walkClip == null) walkClip = FindAndLoadClip("walk", phases, searchPaths);
            if (runClip == null) runClip = FindAndLoadClip("run", phases, searchPaths);

            // Load directional animations (try multiple naming conventions)
            if (walkBackClip == null)
            {
                walkBackClip = FindAndLoadClip("walk_back", phases, searchPaths);
                if (walkBackClip == null) walkBackClip = FindAndLoadClip("walk_backward", phases, searchPaths);
                if (walkBackClip == null) walkBackClip = FindAndLoadClip("walkback", phases, searchPaths);
            }

            if (runBackClip == null)
            {
                runBackClip = FindAndLoadClip("run_back", phases, searchPaths);
                if (runBackClip == null) runBackClip = FindAndLoadClip("run_backward", phases, searchPaths);
                if (runBackClip == null) runBackClip = FindAndLoadClip("runback", phases, searchPaths);
            }

            if (strafeLeftClip == null)
            {
                strafeLeftClip = FindAndLoadClip("strafe_left", phases, searchPaths);
                if (strafeLeftClip == null) strafeLeftClip = FindAndLoadClip("walk_left", phases, searchPaths);
                if (strafeLeftClip == null) strafeLeftClip = FindAndLoadClip("strafeleft", phases, searchPaths);
            }

            if (strafeRightClip == null)
            {
                strafeRightClip = FindAndLoadClip("strafe_right", phases, searchPaths);
                if (strafeRightClip == null) strafeRightClip = FindAndLoadClip("walk_right", phases, searchPaths);
                if (strafeRightClip == null) strafeRightClip = FindAndLoadClip("straferight", phases, searchPaths);
            }

            if (runDiagonalLeftClip == null) runDiagonalLeftClip = FindAndLoadClip("run_diagonal_left", phases, searchPaths);
            if (runDiagonalRightClip == null) runDiagonalRightClip = FindAndLoadClip("run_diagonal_right", phases, searchPaths);
            if (walkBackDiagonalLeftClip == null) walkBackDiagonalLeftClip = FindAndLoadClip("walk_back_diagonal_left", phases, searchPaths);
            if (walkBackDiagonalRightClip == null) walkBackDiagonalRightClip = FindAndLoadClip("walk_back_diagonal_right", phases, searchPaths);

            if (turnLeftClip == null) turnLeftClip = FindAndLoadClip("turn_left", phases, searchPaths);
            if (turnRightClip == null) turnRightClip = FindAndLoadClip("turn_right", phases, searchPaths);

            if (spinLeftClip == null) spinLeftClip = FindAndLoadClip("spin_left", phases, searchPaths);
            if (spinRightClip == null) spinRightClip = FindAndLoadClip("spin_right", phases, searchPaths);

            if (jumpClip == null) jumpClip = FindAndLoadClip("jump", phases, searchPaths);
            if (swimClip == null) swimClip = FindAndLoadClip("swim", phases, searchPaths);

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

                // Set up manual bone reset system
                if (enableManualBoneReset)
                {
                    SetupManualBoneReset();
                }

                PlayAnimation("idle");
            }
            else
            {
                Debug.LogError($"❌ Failed to load required animations! Check that {genderPrefix}idle and {genderPrefix}walk exist in Resources/phase_*/char/");
                isInitialized = false;
            }
        }

        private void SetupManualBoneReset()
        {
            Debug.Log("🦴 Setting up manual bone reset system...");

            // Play idle and sample to get the rest pose
            animComponent.Play("idle");
            animComponent.Sample();

            // Cache all bone transforms and their idle pose
            // Bones are pure Transform nodes - they don't have MonoBehaviour components
            Transform[] allTransforms = animComponent.GetComponentsInChildren<Transform>();
            foreach (Transform bone in allTransforms)
            {
                // Skip transforms that have MonoBehaviour components (game logic objects, not bones)
                // Exception: Animation component is allowed since it's on the model root
                MonoBehaviour[] components = bone.GetComponents<MonoBehaviour>();
                bool hasNonAnimationComponents = false;
                foreach (MonoBehaviour comp in components)
                {
                    if (!(comp is Animation))
                    {
                        hasNonAnimationComponents = true;
                        break;
                    }
                }

                if (hasNonAnimationComponents)
                {
                    Debug.Log($"   Skipping non-bone (has components): {bone.name}");
                    continue;
                }

                string bonePath = GetRelativePath(animComponent.transform, bone);
                if (!string.IsNullOrEmpty(bonePath))
                {
                    cachedBones[bonePath] = bone;
                    restPoseRotations[bonePath] = bone.localRotation;
                }
            }

            Debug.Log($"   Cached {cachedBones.Count} bones with their idle pose");

            Debug.Log($"✅ Manual bone reset system ready!");
        }

        private string GetRelativePath(Transform root, Transform bone)
        {
            if (bone == root) return "";

            string path = bone.name;
            Transform parent = bone.parent;

            while (parent != null && parent != root)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        /// <summary>
        /// Runtime bone analysis - works in both Editor and Builds
        /// Samples animation and detects which bones move from rest pose
        /// </summary>
        private void AnalyzeAnimationBonesRuntime(string animName, AnimationClip clip)
        {
            var bones = new HashSet<string>();

            // Sample this animation to see which bones it affects
            animComponent.Play(animName);
            animComponent.Sample();

            // Check which bones moved from rest pose
            foreach (var kvp in cachedBones)
            {
                string bonePath = kvp.Key;
                Transform bone = kvp.Value;

                if (!restPoseRotations.ContainsKey(bonePath)) continue;

                Quaternion restRot = restPoseRotations[bonePath];
                Quaternion animRot = bone.localRotation;

                // If rotation differs by more than 0.01 degrees, bone is animated
                float angleDiff = Quaternion.Angle(restRot, animRot);
                if (angleDiff > 0.01f)
                {
                    bones.Add(bonePath);
                }
            }

            animationBones[animName] = bones;
            Debug.Log($"   {animName}: {bones.Count} bones");
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

        private void UpdateAnimation(ControllerAdapter controller)
        {
            // Don't change animation while landing animation is playing
            if (isPlayingLanding)
            {
                return;
            }

            string targetAnim = "idle";

            // Determine which animation to play based on controller state
            if (controller.IsSwimming)
            {
                targetAnim = "swim";
            }
            else if (controller.IsGrounded)
            {
                float speed = controller.CurrentSpeed;
                Vector2 input = controller.MoveInput;
                float turnInput = controller.TurnInput;
                float strafeInput = controller.StrafeInput;
                bool isFreeLooking = controller.IsFreeLooking;
                bool isRunning = controller.IsRunning;

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
                    // All characters use spin animations for turning
                    if (turnInput < -0.1f)
                    {
                        // Turning left (A key)
                        targetAnim = spinLeftClip != null ? "spin_left" : "idle";
                    }
                    else if (turnInput > 0.1f)
                    {
                        // Turning right (D key)
                        targetAnim = spinRightClip != null ? "spin_right" : "idle";
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
                if (wasFalling && !controller.IsFalling)
                {
                    // Just landed - play landing animation (end portion of jump clip)
                    if (jumpClip != null)
                    {
                        PlayLandingAnimation();
                        isInJumpAir = false;
                    }
                }
                else if (!controller.IsGrounded)
                {
                    // Not grounded (in air or slightly off ground)
                    if (jumpClip != null)
                    {
                        // If just left ground (was grounded, now not grounded)
                        if (wasGrounded && !controller.IsGrounded)
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
                        if (controller.IsFalling)
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

            // Only switch if different
            if (targetAnim != currentAnim)
            {
                PlayAnimation(targetAnim);
            }

            // Track state for next frame
            wasGrounded = controller.IsGrounded;
            wasFalling = controller.IsFalling;
        }

        private void PlayAnimation(string animName)
        {
            Debug.Log($"[SimpleAnimationPlayer] PlayAnimation called: {animName} on {gameObject.name}");

            if (!animComponent.GetClip(animName))
            {
                Debug.LogWarning($"[SimpleAnimationPlayer] Animation '{animName}' not found on {gameObject.name}");

                // Fallback to idle if animation doesn't exist
                if (animName != "idle" && animComponent.GetClip("idle"))
                {
                    Debug.Log($"[SimpleAnimationPlayer] Falling back to idle");
                    animName = "idle";
                }
                else
                {
                    Debug.LogError($"[SimpleAnimationPlayer] No animation to play!");
                    return;
                }
            }

            // Manual bone reset - ONLY when transitioning TO idle
            // Reset ALL bones that the previous animation moved
            if (enableManualBoneReset && !string.IsNullOrEmpty(currentAnim) && currentAnim != animName && animName == "idle")
            {
                // Stop previous reset coroutine if still running
                if (currentResetCoroutine != null)
                {
                    StopCoroutine(currentResetCoroutine);
                }

                // Reset ALL bones from previous animation to rest pose (idle pose)
                currentResetCoroutine = StartCoroutine(ResetAllBonesToIdle(currentAnim));
            }

            // Use CrossFade for smooth transitions
            animComponent.CrossFade(animName, transitionDuration);
            currentAnim = animName;
        }

        /// <summary>
        /// Reset ALL bones to idle pose - simple and reliable
        /// No complex detection needed, just reset everything
        /// </summary>
        private IEnumerator ResetAllBonesToIdle(string fromAnim)
        {
            Debug.Log($"🦴 Resetting ALL {cachedBones.Count} bones to idle pose");

            // Store starting rotations of ALL bones
            Dictionary<string, Quaternion> startRotations = new Dictionary<string, Quaternion>();
            foreach (var kvp in cachedBones)
            {
                startRotations[kvp.Key] = kvp.Value.localRotation;
            }

            // Lerp ALL bones from current rotation → idle pose over resetDuration
            float elapsed = 0f;
            while (elapsed < resetDuration)
            {
                float t = elapsed / resetDuration;
                // Use smoothstep for nicer easing
                t = t * t * (3f - 2f * t);

                foreach (var kvp in cachedBones)
                {
                    string bonePath = kvp.Key;
                    Transform bone = kvp.Value;

                    if (restPoseRotations.ContainsKey(bonePath))
                    {
                        bone.localRotation = Quaternion.Slerp(
                            startRotations[bonePath],
                            restPoseRotations[bonePath],
                            t
                        );
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Final snap ALL bones to idle pose
            foreach (var kvp in cachedBones)
            {
                if (restPoseRotations.ContainsKey(kvp.Key))
                {
                    kvp.Value.localRotation = restPoseRotations[kvp.Key];
                }
            }

            Debug.Log($"✅ All bones reset to idle pose");
            currentResetCoroutine = null;
        }

        private IEnumerator ResetOrphanedBones(string fromAnim, string toAnim)
        {
            // DEBUG: Check if bone data exists
            if (!animationBones.ContainsKey(fromAnim))
            {
                Debug.LogWarning($"⚠️ Missing bone data for '{fromAnim}' - skipping bone reset");
                yield break;
            }

            if (!animationBones.ContainsKey(toAnim))
            {
                Debug.LogWarning($"⚠️ Missing bone data for '{toAnim}' - skipping bone reset");
                yield break;
            }

            var fromBones = animationBones[fromAnim];
            var toBones = animationBones[toAnim];
            var orphanedBones = new List<string>();

            Debug.Log($"📊 {fromAnim} animates {fromBones.Count} bones, {toAnim} animates {toBones.Count} bones");

            foreach (var bone in fromBones)
            {
                if (!toBones.Contains(bone))
                {
                    orphanedBones.Add(bone);
                }
            }

            if (orphanedBones.Count == 0)
            {
                Debug.Log($"✅ No orphaned bones between {fromAnim} → {toAnim}");
                yield break;
            }

            Debug.Log($"🦴 Resetting {orphanedBones.Count} orphaned bones: {fromAnim} → {toAnim}");

            // Store starting rotations
            Dictionary<string, Quaternion> startRotations = new Dictionary<string, Quaternion>();
            foreach (var bonePath in orphanedBones)
            {
                if (cachedBones.ContainsKey(bonePath))
                {
                    startRotations[bonePath] = cachedBones[bonePath].localRotation;
                }
            }

            // Lerp bones from current rotation → rest pose over resetDuration
            float elapsed = 0f;
            while (elapsed < resetDuration)
            {
                float t = elapsed / resetDuration;
                // Use smoothstep for nicer easing
                t = t * t * (3f - 2f * t);

                foreach (var bonePath in orphanedBones)
                {
                    if (cachedBones.ContainsKey(bonePath) && restPoseRotations.ContainsKey(bonePath))
                    {
                        cachedBones[bonePath].localRotation = Quaternion.Slerp(
                            startRotations[bonePath],
                            restPoseRotations[bonePath],
                            t
                        );
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Final snap to rest pose
            foreach (var bonePath in orphanedBones)
            {
                if (cachedBones.ContainsKey(bonePath) && restPoseRotations.ContainsKey(bonePath))
                {
                    cachedBones[bonePath].localRotation = restPoseRotations[bonePath];
                }
            }

            currentResetCoroutine = null;
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
