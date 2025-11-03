using UnityEngine;
using System.Collections.Generic;

namespace POTCO
{
    /// <summary>
    /// Animation player for animals/creatures using parsed animation state data from creature .py files
    /// Plays animation sequences based on state (LandRoam, WaterRoam, etc.)
    /// </summary>
    [RequireComponent(typeof(RuntimeAnimatorPlayer))]
    public class AnimalAnimationPlayer : MonoBehaviour
    {
        [Header("Animation State Data")]
        [Tooltip("Current animation state (e.g., LandRoam, WaterRoam)")]
        public string currentState = "LandRoam";

        [Header("Animation Sequences (Parsed from .py)")]
        [Tooltip("Animation sequences for each state")]
        public List<AnimationStateSequence> stateSequences = new List<AnimationStateSequence>();

        [Header("Animation Naming")]
        [Tooltip("Model prefix for animation names (e.g., 'chicken_hi', 'rooster_hi')")]
        public string animationPrefix = "";

        [Header("Animation Blending")]
        [Tooltip("Time in seconds to blend from idle to walk animation")]
        [Range(0.0f, 1.0f)]
        public float idleToWalkBlendTime = 0.3f;

        [Tooltip("Time in seconds to blend from walk to idle animation")]
        [Range(0.0f, 1.0f)]
        public float walkToIdleBlendTime = 0.3f;

        [Tooltip("Default blend time for other animation transitions")]
        [Range(0.0f, 1.0f)]
        public float defaultBlendTime = 0.2f;

        private RuntimeAnimatorPlayer animComponent;
        private CharacterController characterController;
        private AnimationStateSequence activeSequence;
        private int currentAnimIndex = 0;
        private float stateTimer = 0f;
        private float currentAnimDuration = 0f;
        private string currentAnimName = "";
        private bool isMoving = false;
        private string lastPlayedAnim = "";

        [System.Serializable]
        public class AnimationStateSequence
        {
            public string stateName;
            public List<AnimationStep> animations = new List<AnimationStep>();
        }

        [System.Serializable]
        public class AnimationStep
        {
            public string animationName;
            public float playRate = 1.0f; // 1.0 = normal, -1.0 = reverse
        }

        void Start()
        {
            animComponent = GetComponent<RuntimeAnimatorPlayer>();
            if (animComponent == null)
            {
                // Create RuntimeAnimatorPlayer if not found
                animComponent = gameObject.AddComponent<RuntimeAnimatorPlayer>();
                animComponent.Initialize();
            }

            // Get CharacterController from parent (for movement detection)
            characterController = GetComponentInParent<CharacterController>();

            if (characterController != null)
            {
                Debug.Log($"🐾 [AnimalAnimationPlayer] Found CharacterController on parent: {characterController.gameObject.name}");
            }
            else
            {
                Debug.LogWarning($"⚠️ [AnimalAnimationPlayer] CharacterController NOT FOUND on {gameObject.name} or parents!");
            }

            Debug.Log($"[AnimalAnimationPlayer] RuntimeAnimatorPlayer initialized on {gameObject.name}");

            // Start with idle animation
            PlayAnimationByName("idle");
        }

        void Update()
        {
            if (animComponent == null)
                return;

            // Detect if animal is moving
            bool wasMoving = isMoving;
            if (characterController != null)
            {
                float velocityMag = characterController.velocity.magnitude;
                isMoving = velocityMag > 0.1f;

                // Log every 60 frames to avoid spam
                if (Time.frameCount % 60 == 0)
                {
                    Debug.Log($"🐾 [AnimalAnimationPlayer] {gameObject.name} - Velocity: {velocityMag:F3}, IsMoving: {isMoving}");
                }
            }
            else if (Time.frameCount % 120 == 0) // Log less frequently if no controller
            {
                Debug.LogWarning($"⚠️ [AnimalAnimationPlayer] {gameObject.name} - CharacterController is NULL, cannot detect movement!");
            }

            // Play appropriate animation based on movement
            if (isMoving && !wasMoving)
            {
                // Started moving - play walk
                Debug.Log($"🐾 [AnimalAnimationPlayer] {gameObject.name} STARTED MOVING - playing walk");
                PlayAnimationByName("walk");
            }
            else if (!isMoving && wasMoving)
            {
                // Stopped moving - play idle
                Debug.Log($"🐾 [AnimalAnimationPlayer] {gameObject.name} STOPPED MOVING - playing idle");
                PlayAnimationByName("idle");
            }
        }

        /// <summary>
        /// Play animation by name (e.g., "idle", "walk") with smooth crossfade
        /// </summary>
        private void PlayAnimationByName(string animName)
        {
            string fullAnimName = string.IsNullOrEmpty(animationPrefix) ? animName : $"{animationPrefix}_{animName}";

            // Don't replay if already playing
            if (lastPlayedAnim == fullAnimName && animComponent.IsPlaying(fullAnimName))
                return;

            // Determine blend time based on transition type
            float blendTime = defaultBlendTime;
            if (lastPlayedAnim.Contains("idle") && animName == "walk")
            {
                blendTime = idleToWalkBlendTime;
            }
            else if (lastPlayedAnim.Contains("walk") && animName == "idle")
            {
                blendTime = walkToIdleBlendTime;
            }

            if (animComponent.HasClip(fullAnimName))
            {
                animComponent.CrossFade(fullAnimName, blendTime);
                lastPlayedAnim = fullAnimName;
                Debug.Log($"🐾 [AnimalAnimationPlayer] Playing '{fullAnimName}' on {gameObject.name} (blend: {blendTime:F2}s)");
            }
            else
            {
                Debug.LogWarning($"[AnimalAnimationPlayer] Animation '{fullAnimName}' not found on {gameObject.name}");
            }
        }

        /// <summary>
        /// Set the current animation state (e.g., "LandRoam", "WaterRoam")
        /// </summary>
        public void SetState(string stateName)
        {
            currentState = stateName;
            activeSequence = stateSequences.Find(s => s.stateName == stateName);

            if (activeSequence == null)
            {
                Debug.LogWarning($"[AnimalAnimationPlayer] State '{stateName}' not found on {gameObject.name}");
                return;
            }

            // Start from the beginning of the sequence
            currentAnimIndex = 0;
            PlayNextAnimationInSequence();
        }

        /// <summary>
        /// Play the next animation in the current state's sequence
        /// </summary>
        private void PlayNextAnimationInSequence()
        {
            if (activeSequence == null || activeSequence.animations.Count == 0)
                return;

            // Get next animation in sequence (loop around)
            AnimationStep step = activeSequence.animations[currentAnimIndex];
            currentAnimIndex = (currentAnimIndex + 1) % activeSequence.animations.Count;

            // Play the animation
            PlayAnimation(step.animationName, step.playRate);
        }

        /// <summary>
        /// Play a specific animation with a given play rate
        /// </summary>
        private void PlayAnimation(string animName, float playRate)
        {
            if (animComponent == null)
                return;

            // Build full animation name with prefix (e.g., "chicken" + "_" + "idle" = "chicken_idle")
            string fullAnimName = string.IsNullOrEmpty(animationPrefix) ? animName : $"{animationPrefix}_{animName}";

            if (!animComponent.HasClip(fullAnimName))
            {
                Debug.LogWarning($"[AnimalAnimationPlayer] Animation '{fullAnimName}' not found on {gameObject.name}");
                return;
            }

            // Note: Playables API doesn't support playRate directly
            // Play the animation
            animComponent.Play(fullAnimName);

            // Track timing
            currentAnimName = fullAnimName;
            stateTimer = 0f;

            Debug.Log($"🐾 [AnimalAnimationPlayer] Playing '{fullAnimName}' on {gameObject.name}");
        }

        /// <summary>
        /// Initialize animation sequences from parsed creature data
        /// </summary>
        public void InitializeFromCreatureData(Dictionary<string, List<(string animName, float playRate)>> animStates)
        {
            stateSequences.Clear();

            foreach (var kvp in animStates)
            {
                AnimationStateSequence sequence = new AnimationStateSequence
                {
                    stateName = kvp.Key
                };

                foreach (var (animName, playRate) in kvp.Value)
                {
                    sequence.animations.Add(new AnimationStep
                    {
                        animationName = animName,
                        playRate = playRate
                    });
                }

                stateSequences.Add(sequence);
            }

            Debug.Log($"[AnimalAnimationPlayer] Initialized {stateSequences.Count} animation states on {gameObject.name}");
        }

#if UNITY_EDITOR
        /// <summary>
        /// Draw gizmo labels in scene view
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (activeSequence != null)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2f,
                    $"State: {currentState}\nAnim: {currentAnimName}\n{currentAnimIndex}/{activeSequence.animations.Count}"
                );
            }
        }
#endif
    }
}
