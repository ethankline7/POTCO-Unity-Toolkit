using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace POTCO
{
    /// <summary>
    /// NPC animation player using CustomAnims.py definitions
    /// Loads animations from CustomAnimsParser and supports prop attachment to weapon_right joint
    /// </summary>
    [RequireComponent(typeof(NPCController))]
    [RequireComponent(typeof(NPCData))]
    public class NPCAnimationPlayer : MonoBehaviour
    {
        [Header("Animation Transitions")]
        [SerializeField] private float transitionDuration = 0.15f;

        [Header("Detected Gender (Read-Only)")]
        [SerializeField] private string genderPrefix = "mp_"; // mp_ for male, fp_ for female

        [Header("Animation Clips (Auto-Loaded from CustomAnims.py)")]
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private AnimationClip walkClip;
        [SerializeField] private AnimationClip greetingClip;
        [SerializeField] private AnimationClip noticeClip;
        [SerializeField] private AnimationClip spinLeftClip;
        [SerializeField] private AnimationClip spinRightClip;

        [Header("AnimSet Clips (From CustomAnims.py)")]
        [SerializeField] private AnimationClip animSetIdle;       // idles
        [SerializeField] private AnimationClip animSetIntoLook;   // interactInto
        [SerializeField] private AnimationClip animSetLookIdle;   // interact
        [SerializeField] private AnimationClip animSetOutofLook;  // interactOutof

        [Header("Props (From CustomAnims.py)")]
        [SerializeField] private GameObject attachedProp;

        private Animation animComponent;
        private NPCController npcController;
        private NPCData npcData;
        private string currentAnim = "";
        private bool isInitialized = false;
        private float greetingStartTime = 0f;
        private bool hasPlayedGreeting = false;
        private NPCController.NPCState previousState = NPCController.NPCState.LandRoam;
        private bool hasPlayedIntoLook = false;
        private float intoLookStartTime = 0f;
        private bool isPlayingOutof = false;
        private float outofStartTime = 0f;

        // def_ bone protection system
        private struct BoneTransformData
        {
            public Transform bone;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }
        private List<BoneTransformData> lockedDefBones = new List<BoneTransformData>();

        private void Awake()
        {
            npcController = GetComponent<NPCController>();
            npcData = GetComponent<NPCData>();

            // Initialize CustomAnims parser
            CustomAnimsParser.Initialize();
        }

        private void Start()
        {
            // Find Animation component - should be on character root
            Debug.Log($"🔍 NPCAnimationPlayer searching for Animation component on: {gameObject.name}");

            // PRIORITY 1: Check if there's a "Model" wrapper (created by NPCController)
            Transform modelChild = transform.Find("Model");
            if (modelChild != null && modelChild.childCount > 0)
            {
                GameObject characterRoot = modelChild.GetChild(0).gameObject;
                Debug.Log($"   Found character under Model wrapper: {characterRoot.name}");
                animComponent = characterRoot.GetComponent<Animation>();
                if (animComponent != null)
                {
                    Debug.Log($"✅ Found Animation component on character root: {characterRoot.name}");
                }
            }

            // PRIORITY 2: Check first direct child
            if (animComponent == null && transform.childCount > 0)
            {
                GameObject firstChild = transform.GetChild(0).gameObject;
                Debug.Log($"   Checking first child: {firstChild.name}");
                animComponent = firstChild.GetComponent<Animation>();
                if (animComponent != null)
                {
                    Debug.Log($"✅ Found Animation component on first child: {firstChild.name}");
                }
            }

            // PRIORITY 3: Check this object (fallback)
            if (animComponent == null)
            {
                animComponent = GetComponent<Animation>();
                if (animComponent != null)
                {
                    Debug.Log($"✅ Found Animation component on this object");
                }
            }

            // PRIORITY 4: Search all children (last resort)
            if (animComponent == null)
            {
                animComponent = GetComponentInChildren<Animation>();
                if (animComponent != null)
                {
                    Debug.Log($"✅ Found Animation component on descendant: {animComponent.gameObject.name}");
                }
            }

            if (animComponent == null)
            {
                Debug.LogError($"❌ No Animation component found on NPC: {gameObject.name}");
                Debug.LogError($"   Children: {string.Join(", ", GetComponentsInChildren<Transform>().Select(t => t.name))}");
                return;
            }

            // Detect gender
            DetectGender();

            // Load animations using CustomAnims.py
            LoadAnimations();

            // Lock def_ bones to prevent animations from modifying body shape
            LockDefBones();

            if (isInitialized)
            {
                Debug.Log($"✅ NPCAnimationPlayer initialized with gender prefix: {genderPrefix}");
                string initialAnim = GetIdleAnimation();
                PlayAnimation(initialAnim);
            }
        }

        private float lastUpdateLog = 0f;

        private void Update()
        {
            if (!isInitialized || npcController == null)
            {
                if (Time.time - lastUpdateLog > 5f)
                {
                    Debug.LogWarning($"⚠️ NPCAnimationPlayer Update() running but not initialized on {gameObject.name}");
                    lastUpdateLog = Time.time;
                }
                return;
            }

            UpdateAnimation();
        }

        private void LateUpdate()
        {
            // Force def_ bones back to their original transforms every frame
            // This prevents animations from permanently modifying body shape
            // Only update bones that have actually changed for better performance
            foreach (var boneData in lockedDefBones)
            {
                if (boneData.bone != null)
                {
                    // Only set if changed (avoids unnecessary transform updates)
                    if (boneData.bone.localPosition != boneData.localPosition)
                    {
                        boneData.bone.localPosition = boneData.localPosition;
                    }
                    if (boneData.bone.localRotation != boneData.localRotation)
                    {
                        boneData.bone.localRotation = boneData.localRotation;
                    }
                    if (boneData.bone.localScale != boneData.localScale)
                    {
                        boneData.bone.localScale = boneData.localScale;
                    }
                }
            }
        }

        /// <summary>
        /// Detect gender from CharacterGenderData, hierarchy, or mesh names
        /// </summary>
        private void DetectGender()
        {
            // Check for CharacterGenderData component (highest priority)
            // Search in children because it's on the character model, not the NPC parent
            CharacterOG.Runtime.CharacterGenderData genderData = GetComponentInChildren<CharacterOG.Runtime.CharacterGenderData>();
            if (genderData != null)
            {
                string gender = genderData.GetGender();
                genderPrefix = genderData.GetGenderPrefix();
                Debug.Log($"🎭 Detected {(gender == "f" ? "FEMALE" : "MALE")} NPC from CharacterGenderData ({genderPrefix})");
                return;
            }

            // Check hierarchy and mesh names
            Transform[] allChildren = GetComponentsInChildren<Transform>();
            foreach (Transform child in allChildren)
            {
                string childName = child.name.ToLower();
                if (childName.Contains("fp_") || childName.Contains("female"))
                {
                    genderPrefix = "fp_";
                    Debug.Log($"🎭 Detected FEMALE NPC from child: '{child.name}' (fp_ prefix)");
                    return;
                }
                else if (childName.Contains("mp_") || childName.Contains("male"))
                {
                    genderPrefix = "mp_";
                    Debug.Log($"🎭 Detected MALE NPC from child: '{child.name}' (mp_ prefix)");
                    return;
                }
            }

            // Default to male
            genderPrefix = "mp_";
            Debug.LogWarning("⚠️ Could not detect NPC gender. Using default MALE (mp_ prefix)");
        }

        /// <summary>
        /// Find all def_ bones and lock their initial transforms
        /// </summary>
        private void LockDefBones()
        {
            Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
            int lockedCount = 0;

            foreach (Transform t in allTransforms)
            {
                if (t.name.StartsWith("def_"))
                {
                    BoneTransformData boneData = new BoneTransformData
                    {
                        bone = t,
                        localPosition = t.localPosition,
                        localRotation = t.localRotation,
                        localScale = t.localScale
                    };
                    lockedDefBones.Add(boneData);
                    lockedCount++;
                }
            }

            if (lockedCount > 0)
            {
                Debug.Log($"🔒 Locked {lockedCount} def_ bones to prevent body shape corruption");
            }
        }

        /// <summary>
        /// Load animations using CustomAnims.py definitions
        /// </summary>
        private void LoadAnimations()
        {
            string[] phases = { "phase_2", "phase_3", "phase_4", "phase_5", "phase_6" };
            string[] searchPaths = { "char", "models/char" };

            Debug.Log($"🎬 Loading animations with gender prefix: {genderPrefix}");

            // Check for custom model prefix (e.g., "js" from "models/char/js_2000")
            string customModelPrefix = GetCustomModelPrefix();
            if (!string.IsNullOrEmpty(customModelPrefix))
            {
                Debug.Log($"✅ Detected custom model prefix: {customModelPrefix}");
                Debug.Log($"   Will load animations as: {customModelPrefix}_idle, {customModelPrefix}_walk, etc.");
            }

            // Load required animations (idle/walk/spin) with custom model prefix if available
            idleClip = FindAndLoadClip("idle", phases, searchPaths, customModelPrefix);
            walkClip = FindAndLoadClip("walk", phases, searchPaths, customModelPrefix);
            spinLeftClip = FindAndLoadClip("spin_left", phases, searchPaths, customModelPrefix);
            spinRightClip = FindAndLoadClip("spin_right", phases, searchPaths, customModelPrefix);

            // Load AnimSet from CustomAnims.py
            if (npcData != null && !string.IsNullOrEmpty(npcData.animSet) && npcData.animSet != "default")
            {
                string animSetName = npcData.animSet;
                Debug.Log($"📋 Loading AnimSet from CustomAnims.py: {animSetName}");

                CustomAnimData animData = CustomAnimsParser.GetAnimSet(animSetName);
                if (animData != null)
                {
                    Debug.Log($"✅ Found AnimSet definition in CustomAnims.py: {animSetName}");
                    Debug.Log($"   Idles: {string.Join(", ", animData.idles)}");
                    Debug.Log($"   InteractInto: {string.Join(", ", animData.interactInto)}");
                    Debug.Log($"   Interact: {string.Join(", ", animData.interact)}");
                    Debug.Log($"   InteractOutof: {string.Join(", ", animData.interactOutof)}");
                    Debug.Log($"   Props: {animData.props.Count}");

                    // Load animations from CustomAnimData
                    if (animData.idles.Count > 0)
                    {
                        animSetIdle = FindAndLoadClip(animData.idles[0], phases, searchPaths, customModelPrefix);
                    }
                    if (animData.interactInto.Count > 0)
                    {
                        animSetIntoLook = FindAndLoadClip(animData.interactInto[0], phases, searchPaths, customModelPrefix);
                    }
                    if (animData.interact.Count > 0)
                    {
                        animSetLookIdle = FindAndLoadClip(animData.interact[0], phases, searchPaths, customModelPrefix);
                    }
                    if (animData.interactOutof.Count > 0)
                    {
                        animSetOutofLook = FindAndLoadClip(animData.interactOutof[0], phases, searchPaths, customModelPrefix);
                    }

                    // Check if NPC should be stationary (has AnimSet variations)
                    if (animSetIdle != null || animSetIntoLook != null || animSetLookIdle != null || animSetOutofLook != null)
                    {
                        npcData.isStationary = true;
                        Debug.Log($"   Marking NPC as STATIONARY (has AnimSet variations)");
                    }

                    // Attach props to weapon_right joint
                    if (animData.props.Count > 0)
                    {
                        AttachProp(animData.props[0]);
                    }
                }
                else
                {
                    Debug.LogWarning($"⚠️ AnimSet '{animSetName}' not found in CustomAnims.py");
                }
            }

            // Load optional greeting and notice animations
            if (npcData != null)
            {
                if (!string.IsNullOrEmpty(npcData.greetingAnimation))
                {
                    greetingClip = FindAndLoadClip(npcData.greetingAnimation, phases, searchPaths, customModelPrefix);
                }

                if (!string.IsNullOrEmpty(npcData.noticeAnimation1))
                {
                    noticeClip = FindAndLoadClip(npcData.noticeAnimation1, phases, searchPaths, customModelPrefix);
                }
            }

            // Add clips to Animation component
            if (idleClip != null)
            {
                animComponent.AddClip(idleClip, "idle");
                animComponent["idle"].wrapMode = WrapMode.Loop;
                Debug.Log($"   Added 'idle' clip to Animation component");
            }

            if (walkClip != null)
            {
                animComponent.AddClip(walkClip, "walk");
                animComponent["walk"].wrapMode = WrapMode.Loop;
                Debug.Log($"   Added 'walk' clip to Animation component");
            }

            if (spinLeftClip != null)
            {
                animComponent.AddClip(spinLeftClip, "spin_left");
                animComponent["spin_left"].wrapMode = WrapMode.Loop;
                Debug.Log($"   Added 'spin_left' clip to Animation component");
            }

            if (spinRightClip != null)
            {
                animComponent.AddClip(spinRightClip, "spin_right");
                animComponent["spin_right"].wrapMode = WrapMode.Loop;
                Debug.Log($"   Added 'spin_right' clip to Animation component");
            }

            if (animSetIdle != null)
            {
                animComponent.AddClip(animSetIdle, "animset_idle");
                animComponent["animset_idle"].wrapMode = WrapMode.Loop;
                Debug.Log($"   Added 'animset_idle' clip to Animation component");
            }

            if (animSetIntoLook != null)
            {
                animComponent.AddClip(animSetIntoLook, "animset_into_look");
                animComponent["animset_into_look"].wrapMode = WrapMode.Once;
                Debug.Log($"   Added 'animset_into_look' clip to Animation component");
            }

            if (animSetLookIdle != null)
            {
                animComponent.AddClip(animSetLookIdle, "animset_look_idle");
                animComponent["animset_look_idle"].wrapMode = WrapMode.Loop;
                Debug.Log($"   Added 'animset_look_idle' clip to Animation component");
            }

            if (animSetOutofLook != null)
            {
                animComponent.AddClip(animSetOutofLook, "animset_outof_look");
                animComponent["animset_outof_look"].wrapMode = WrapMode.Once;
                Debug.Log($"   Added 'animset_outof_look' clip to Animation component");
            }

            if (greetingClip != null)
            {
                animComponent.AddClip(greetingClip, "greeting");
                animComponent["greeting"].wrapMode = WrapMode.Once;
                Debug.Log($"   Added 'greeting' clip to Animation component");
            }

            if (noticeClip != null)
            {
                animComponent.AddClip(noticeClip, "notice");
                animComponent["notice"].wrapMode = WrapMode.Once;
                Debug.Log($"   Added 'notice' clip to Animation component");
            }

            // Enable Animation component
            animComponent.playAutomatically = false;
            animComponent.enabled = true;
            Debug.Log($"✅ Animation component enabled: {animComponent.enabled}");

            // Check if we have minimum required animations
            bool hasAnimSet = (animSetIdle != null || animSetIntoLook != null || animSetLookIdle != null || animSetOutofLook != null);
            if (idleClip != null || hasAnimSet)
            {
                isInitialized = true;
                Debug.Log($"✅ NPCAnimationPlayer initialized successfully!");
                Debug.Log($"   AnimSet ({npcData?.animSet}): {(hasAnimSet ? "✓" : "✗")}");
                Debug.Log($"   Idle: {(idleClip != null ? "✓" : "✗")}");
                Debug.Log($"   Walk: {(walkClip != null ? "✓" : "✗")}");
                Debug.Log($"   Greeting: {(greetingClip != null ? "✓" : "✗")}");
                Debug.Log($"   Notice: {(noticeClip != null ? "✓" : "✗")}");
            }
            else
            {
                Debug.LogError($"❌ Failed to load required animations for NPC! Check that {genderPrefix}idle exists in Resources/phase_*/char/");
                isInitialized = false;
            }
        }

        /// <summary>
        /// Attach a prop to the weapon_right joint in the character rig
        /// </summary>
        private void AttachProp(PropData propData)
        {
            Debug.Log($"🎯 Attempting to attach prop: {propData.modelPath}");

            // Find weapon_right joint in skeleton
            Transform weaponRight = FindWeaponRightJoint();
            if (weaponRight == null)
            {
                Debug.LogWarning($"⚠️ Could not find weapon_right joint in character rig - skipping prop attachment");
                return;
            }

            // Try to load prop model from Resources (search through phases)
            GameObject propPrefab = FindAndLoadProp(propData.modelPath);
            if (propPrefab == null)
            {
                Debug.Log($"ℹ️ Prop not found in any phase folder: {propData.modelPath}");
                return;
            }

            // Instantiate and attach prop
            attachedProp = Instantiate(propPrefab, weaponRight);
            attachedProp.transform.localPosition = Vector3.zero;
            attachedProp.transform.localRotation = Quaternion.identity;
            attachedProp.transform.localScale = Vector3.one;
            attachedProp.name = $"Prop_{propData.modelPath.Substring(propData.modelPath.LastIndexOf('/') + 1)}";

            Debug.Log($"✅ Attached prop to weapon_right: {attachedProp.name}");
        }

        /// <summary>
        /// Find and load a prop model from Resources, searching through phase folders
        /// </summary>
        private GameObject FindAndLoadProp(string propPath)
        {
            // propPath is like "models/handheld/mug_high"
            string[] phases = { "phase_2", "phase_3", "phase_4", "phase_5", "phase_6" };

            // Try each phase
            foreach (string phase in phases)
            {
                string fullPath = $"{phase}/{propPath}";
                GameObject propPrefab = Resources.Load<GameObject>(fullPath);
                if (propPrefab != null)
                {
                    Debug.Log($"✅ Loaded prop from: {fullPath}");
                    return propPrefab;
                }
            }

            // Try without phase prefix (direct path)
            GameObject directProp = Resources.Load<GameObject>(propPath);
            if (directProp != null)
            {
                Debug.Log($"✅ Loaded prop from: {propPath}");
                return directProp;
            }

            Debug.LogWarning($"⚠️ Could not find prop in any phase: {propPath}");
            return null;
        }

        /// <summary>
        /// Find the weapon_right joint in the character skeleton
        /// </summary>
        private Transform FindWeaponRightJoint()
        {
            // Search for "weapon_right" transform in all children
            Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTransforms)
            {
                if (t.name.ToLower() == "weapon_right" || t.name.ToLower() == "weaponright")
                {
                    Debug.Log($"✅ Found weapon_right joint: {t.name}");
                    return t;
                }
            }

            Debug.LogWarning($"⚠️ weapon_right joint not found in {gameObject.name}");
            return null;
        }

        private AnimationClip FindAndLoadClip(string animName, string[] phases, string[] searchPaths, string customModelPrefix = null)
        {
            // PRIORITY 1: Try with custom model prefix (e.g., "js_idle")
            if (!string.IsNullOrEmpty(customModelPrefix))
            {
                string customPrefixedName = $"{customModelPrefix}_{animName}";
                foreach (string phase in phases)
                {
                    foreach (string path in searchPaths)
                    {
                        string fullPath = $"{phase}/{path}/{customPrefixedName}";
                        AnimationClip clip = Resources.Load<AnimationClip>(fullPath);
                        if (clip != null)
                        {
                            Debug.Log($"✅ Loaded custom model anim: {fullPath}");
                            return clip;
                        }
                    }
                }
            }

            // PRIORITY 2: Try with gender prefix (e.g., "mp_idle")
            string genderPrefixedName = genderPrefix + animName;
            foreach (string phase in phases)
            {
                foreach (string path in searchPaths)
                {
                    string fullPath = $"{phase}/{path}/{genderPrefixedName}";
                    AnimationClip clip = Resources.Load<AnimationClip>(fullPath);
                    if (clip != null)
                    {
                        Debug.Log($"✅ Loaded NPC anim: {fullPath}");
                        return clip;
                    }
                }
            }

            // PRIORITY 3: Try without prefix as fallback
            foreach (string phase in phases)
            {
                foreach (string path in searchPaths)
                {
                    string fullPath = $"{phase}/{path}/{animName}";
                    AnimationClip clip = Resources.Load<AnimationClip>(fullPath);
                    if (clip != null)
                    {
                        Debug.Log($"✅ Loaded NPC anim (no prefix): {fullPath}");
                        return clip;
                    }
                }
            }

            string prefixInfo = !string.IsNullOrEmpty(customModelPrefix) ? $"{customModelPrefix}_, " : "";
            Debug.LogWarning($"⚠️ Could not find NPC animation: {prefixInfo}{genderPrefixedName} or {animName}");
            return null;
        }

        /// <summary>
        /// Extract custom model prefix from character hierarchy (e.g., "js" from "js_2000")
        /// </summary>
        private string GetCustomModelPrefix()
        {
            // Look for custom model names in hierarchy (js_2000, jr_2000, etc.)
            Transform[] allChildren = GetComponentsInChildren<Transform>();
            foreach (Transform child in allChildren)
            {
                string childName = child.name.ToLower();

                // Match pattern: prefix_number (e.g., js_2000, jr_2000)
                if (childName.Contains("_"))
                {
                    string[] parts = childName.Split('_');
                    if (parts.Length >= 2 && int.TryParse(parts[1], out _))
                    {
                        // Found a pattern like "js_2000" - return "js"
                        return parts[0];
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Update animation based on NPCController state
        /// </summary>
        private void UpdateAnimation()
        {
            string targetAnim = GetIdleAnimation();
            NPCController.NPCState currentState = npcController.CurrentState;

            // Detect state transitions
            bool justEnteredNotice = (previousState == NPCController.NPCState.LandRoam && currentState == NPCController.NPCState.Notice);
            bool justLeftNotice = (previousState == NPCController.NPCState.Notice && currentState == NPCController.NPCState.LandRoam);

            switch (currentState)
            {
                case NPCController.NPCState.LandRoam:
                    if (npcController.CurrentSpeed > 0.5f && walkClip != null)
                    {
                        targetAnim = "walk";
                        hasPlayedGreeting = false;
                        hasPlayedIntoLook = false;
                        isPlayingOutof = false;
                    }
                    else
                    {
                        // Start playing outof animation when leaving Notice state
                        if (justLeftNotice && animSetOutofLook != null)
                        {
                            targetAnim = "animset_outof_look";
                            hasPlayedIntoLook = false;
                            isPlayingOutof = true;
                            outofStartTime = Time.time;
                        }
                        // Continue playing outof animation until it finishes
                        else if (isPlayingOutof && animSetOutofLook != null)
                        {
                            float outofLength = animSetOutofLook.length;
                            if (Time.time - outofStartTime >= outofLength)
                            {
                                // Outof animation finished, switch to idle
                                targetAnim = GetIdleAnimation();
                                isPlayingOutof = false;
                            }
                            else
                            {
                                // Keep playing outof animation
                                targetAnim = "animset_outof_look";
                            }
                        }
                        else
                        {
                            targetAnim = GetIdleAnimation();
                            isPlayingOutof = false;
                        }
                        hasPlayedGreeting = false;
                    }
                    break;

                case NPCController.NPCState.Notice:
                    // Check if NPC is turning - play spin animation
                    bool playingSpinAnim = false;
                    if (npcController.TurnDirection > 0 && spinRightClip != null)
                    {
                        targetAnim = "spin_right";
                        playingSpinAnim = true;
                    }
                    else if (npcController.TurnDirection < 0 && spinLeftClip != null)
                    {
                        targetAnim = "spin_left";
                        playingSpinAnim = true;
                    }

                    // If not playing spin animation, use normal Notice logic
                    if (!playingSpinAnim)
                    {
                        if (justEnteredNotice && animSetIntoLook != null && !hasPlayedIntoLook)
                        {
                            targetAnim = "animset_into_look";
                            hasPlayedIntoLook = true;
                            intoLookStartTime = Time.time;
                        }
                        else if (hasPlayedIntoLook && animSetIntoLook != null)
                        {
                            float intoLookLength = animSetIntoLook.length;
                            if (Time.time - intoLookStartTime >= intoLookLength && animSetLookIdle != null)
                            {
                                targetAnim = "animset_look_idle";
                            }
                            else
                            {
                                targetAnim = "animset_into_look";
                            }
                        }
                        else if (animSetLookIdle != null)
                        {
                            targetAnim = "animset_look_idle";
                        }
                        else if (noticeClip != null && currentAnim != "notice")
                        {
                            targetAnim = "notice";
                        }
                        else
                        {
                            targetAnim = GetIdleAnimation();
                        }
                    }
                    break;

                case NPCController.NPCState.Greeting:
                    // Check if NPC is turning - play spin animation
                    bool playingSpinAnimGreeting = false;
                    if (npcController.TurnDirection > 0 && spinRightClip != null)
                    {
                        targetAnim = "spin_right";
                        playingSpinAnimGreeting = true;
                    }
                    else if (npcController.TurnDirection < 0 && spinLeftClip != null)
                    {
                        targetAnim = "spin_left";
                        playingSpinAnimGreeting = true;
                    }

                    // If not playing spin animation, use normal Greeting logic
                    if (!playingSpinAnimGreeting)
                    {
                        if (greetingClip != null && !hasPlayedGreeting)
                        {
                            targetAnim = "greeting";
                            hasPlayedGreeting = true;
                            greetingStartTime = Time.time;
                        }
                        else if (hasPlayedGreeting && greetingClip != null)
                        {
                            float greetingLength = greetingClip.length;
                            if (Time.time - greetingStartTime >= greetingLength)
                            {
                                targetAnim = animSetLookIdle != null ? "animset_look_idle" : GetIdleAnimation();
                            }
                            else
                            {
                                targetAnim = "greeting";
                            }
                        }
                        else
                        {
                            targetAnim = animSetLookIdle != null ? "animset_look_idle" : GetIdleAnimation();
                        }
                    }
                    break;
            }

            previousState = currentState;

            if (targetAnim != currentAnim)
            {
                PlayAnimation(targetAnim);
            }
        }

        private string GetIdleAnimation()
        {
            return animSetIdle != null ? "animset_idle" : "idle";
        }

        private void PlayAnimation(string animName)
        {
            if (!animComponent.GetClip(animName))
            {
                Debug.LogWarning($"⚠️ Animation clip '{animName}' not found on {gameObject.name}");
                string fallbackAnim = GetIdleAnimation();
                if (animName != fallbackAnim && animComponent.GetClip(fallbackAnim))
                {
                    Debug.Log($"   Falling back to {fallbackAnim} animation");
                    animName = fallbackAnim;
                }
                else
                {
                    Debug.LogError($"❌ No animations available!");
                    return;
                }
            }

            Debug.Log($"🎬 Playing animation: {animName} on {gameObject.name}");
            animComponent.Stop();
            animComponent.Play(animName);

            if (animComponent.IsPlaying(animName))
            {
                Debug.Log($"✅ Animation '{animName}' is NOW PLAYING");
            }
            else
            {
                Debug.LogError($"❌ Animation '{animName}' FAILED TO PLAY!");
            }

            currentAnim = animName;
        }

        // Public API
        public string GenderPrefix => genderPrefix;
    }
}
