using UnityEngine;
using System.Collections.Generic;

namespace POTCO
{
    /// <summary>
    /// Animation player for animals/creatures using parsed animation state data from creature .py files
    /// Plays animation sequences based on state (LandRoam, WaterRoam, etc.)
    /// </summary>
    [RequireComponent(typeof(Animation))]
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

        private Animation animComponent;
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
            animComponent = GetComponent<Animation>();
            if (animComponent == null)
            {
                Debug.LogError($"[AnimalAnimationPlayer] No Animation component found on {gameObject.name}");
                enabled = false;
                return;
            }

            // Get CharacterController from parent (for movement detection)
            characterController = GetComponentInParent<CharacterController>();

            // Debug: List all available animations on this component
            Debug.Log($"[AnimalAnimationPlayer] Available animations on {gameObject.name}:");
            foreach (AnimationState state in animComponent)
            {
                Debug.Log($"  - {state.name} (clip: {state.clip?.name})");
            }

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
                isMoving = characterController.velocity.magnitude > 0.1f;
            }

            // Play appropriate animation based on movement
            if (isMoving && !wasMoving)
            {
                // Started moving - play walk
                PlayAnimationByName("walk");
            }
            else if (!isMoving && wasMoving)
            {
                // Stopped moving - play idle
                PlayAnimationByName("idle");
            }
        }

        /// <summary>
        /// Play animation by name (e.g., "idle", "walk")
        /// </summary>
        private void PlayAnimationByName(string animName)
        {
            string fullAnimName = string.IsNullOrEmpty(animationPrefix) ? animName : $"{animationPrefix}_{animName}";

            // Don't replay if already playing
            if (lastPlayedAnim == fullAnimName && animComponent.IsPlaying(fullAnimName))
                return;

            AnimationState state = animComponent[fullAnimName];
            if (state != null)
            {
                animComponent.CrossFade(fullAnimName, 0.2f);
                lastPlayedAnim = fullAnimName;
                Debug.Log($"🐾 [AnimalAnimationPlayer] Playing '{fullAnimName}' on {gameObject.name}");
            }
            else
            {
                // Try to find any animation with this name as fallback
                foreach (AnimationState s in animComponent)
                {
                    if (s.name.Contains(animName))
                    {
                        animComponent.CrossFade(s.name, 0.2f);
                        lastPlayedAnim = s.name;
                        Debug.Log($"🐾 [AnimalAnimationPlayer] Playing '{s.name}' on {gameObject.name} (fallback match)");
                        return;
                    }
                }
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

            AnimationState state = animComponent[fullAnimName];
            if (state == null)
            {
                Debug.LogWarning($"[AnimalAnimationPlayer] Animation '{fullAnimName}' not found on {gameObject.name}");
                return;
            }

            // Set play rate (negative = reverse)
            state.speed = playRate;

            // Play the animation
            animComponent.Play(fullAnimName);

            // Track timing
            currentAnimName = fullAnimName;
            currentAnimDuration = state.length / Mathf.Abs(playRate);
            stateTimer = 0f;

            Debug.Log($"🐾 [AnimalAnimationPlayer] Playing '{fullAnimName}' at rate {playRate}x on {gameObject.name}");
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
