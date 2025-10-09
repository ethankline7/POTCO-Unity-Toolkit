/// <summary>
/// Builds AnimatorController for player from parsed POTCO animation data
/// Parses BipedAnimationMixer.py, Biped.py, and EmoteGlobals.py at editor time
/// </summary>
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace Player.Editor
{
    public static class AnimatorBuilder
    {
        // Parsed data
        private static HashSet<string> locomotionKeys = new HashSet<string>();
        private static Dictionary<string, string> emoteIdToClipName = new Dictionary<string, string>();
        private static List<(string logical, string fallback)> defaultPairs = new List<(string, string)>();

        [MenuItem("POTCO/Player/Build Player AnimatorController")]
        public static void BuildPlayerAnimatorController()
        {
            Debug.Log("🎮 Building Player AnimatorController from POTCO sources...");

            // Step 1: Parse POTCO files
            ParsePOTCOAnimationData();

            // Step 2: Resolve animation clips
            var resolvedClips = ResolveAnimationClips();

            // Step 3: Build/update AnimatorController
            BuildAnimatorController(resolvedClips);

            Debug.Log("✅ Player AnimatorController build complete!");
        }

        private static void ParsePOTCOAnimationData()
        {
            Debug.Log("📖 Parsing POTCO animation sources...");

            string basePath = Path.Combine(Application.dataPath, "Editor", "POTCO_Source");

            // Parse BipedAnimationMixer.py for locomotion keys
            string mixerPath = Path.Combine(basePath, "pirate", "BipedAnimationMixer.py");
            ParseBipedAnimationMixer(mixerPath);

            // Parse Biped.py for fallback pairs
            string bipedPath = Path.Combine(basePath, "pirate", "Biped.py");
            ParseBipedFallbacks(bipedPath);

            // Parse EmoteGlobals.py for emote mapping
            string emotePath = Path.Combine(basePath, "piratebase", "EmoteGlobals.py");
            ParseEmoteGlobals(emotePath);

            Debug.Log($"✅ Parsed: {locomotionKeys.Count} locomotion keys, {emoteIdToClipName.Count} emotes, {defaultPairs.Count} fallback pairs");
        }

        private static void ParseBipedAnimationMixer(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"❌ BipedAnimationMixer.py not found at {filePath}");
                return;
            }

            string content = File.ReadAllText(filePath);

            // Extract keys from AnimRankings dictionary
            // Pattern: 'key': (LOOP['...'], ...)
            var pattern = @"'(\w+)':\s*\(";
            var matches = Regex.Matches(content, pattern);

            foreach (Match match in matches)
            {
                string key = match.Groups[1].Value;

                // Add core locomotion and swimming keys
                if (IsLocomotionKey(key))
                {
                    locomotionKeys.Add(key);
                }
            }

            Debug.Log($"📋 Found {locomotionKeys.Count} locomotion keys from BipedAnimationMixer.py");
        }

        private static bool IsLocomotionKey(string key)
        {
            // Core locomotion and swimming keys
            string[] coreKeys = {
                "idle", "walk", "run", "turn_left", "turn_right", "strafe_left", "strafe_right", "jump",
                "swim", "swim_left", "swim_right", "swim_back"
            };

            return coreKeys.Contains(key);
        }

        private static void ParseBipedFallbacks(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"❌ Biped.py not found at {filePath}");
                return;
            }

            string content = File.ReadAllText(filePath);

            // Extract DefaultAnimList tuples
            // Pattern: ('logical', 'fallback')
            var pattern = @"\('(\w+)',\s*'(\w+)'\)";
            var matches = Regex.Matches(content, pattern);

            foreach (Match match in matches)
            {
                string logical = match.Groups[1].Value;
                string fallback = match.Groups[2].Value;
                defaultPairs.Add((logical, fallback));
            }

            Debug.Log($"📋 Found {defaultPairs.Count} fallback pairs from Biped.py");
        }

        private static void ParseEmoteGlobals(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"❌ EmoteGlobals.py not found at {filePath}");
                return;
            }

            string content = File.ReadAllText(filePath);

            // Extract emote mappings
            // Pattern: EMOTE_NAME = integer, then emotes = { EMOTE_NAME: { 'anim': 'anim_name' } }
            // We'll parse the emotes dictionary entries
            var pattern = @"(EMOTE_\w+):\s*\{\s*'anim':\s*'([^']+)'";
            var matches = Regex.Matches(content, pattern);

            foreach (Match match in matches)
            {
                string emoteId = match.Groups[1].Value;
                string animName = match.Groups[2].Value;
                emoteIdToClipName[emoteId] = animName;
            }

            Debug.Log($"📋 Found {emoteIdToClipName.Count} emote mappings from EmoteGlobals.py");
        }

        private static Dictionary<string, AnimationClip> ResolveAnimationClips()
        {
            Debug.Log("🔍 Resolving animation clips from Resources/phase_*/char/**...");

            var resolvedClips = new Dictionary<string, AnimationClip>();

            // Search paths for phase directories
            string[] phases = { "phase_2", "phase_3", "phase_4", "phase_5", "phase_6" };

            foreach (var key in locomotionKeys)
            {
                AnimationClip clip = FindClip(key, phases);
                if (clip != null)
                {
                    resolvedClips[key] = clip;
                    Debug.Log($"✅ Resolved: '{key}' -> {clip.name}");
                }
                else
                {
                    Debug.LogWarning($"⚠️ Missing clip for locomotion key: '{key}'");
                }
            }

            // Resolve emote clips
            foreach (var kvp in emoteIdToClipName)
            {
                string clipName = kvp.Value;
                AnimationClip clip = FindClip(clipName, phases);
                if (clip != null)
                {
                    resolvedClips[clipName] = clip;
                }
            }

            Debug.Log($"✅ Resolved {resolvedClips.Count} total clips");
            return resolvedClips;
        }

        private static AnimationClip FindClip(string key, string[] phases)
        {
            // Try both male and female prefixes
            string[] prefixes = { "mp_", "fp_", "" };

            foreach (var phase in phases)
            {
                foreach (var prefix in prefixes)
                {
                    string searchPattern = $"{prefix}{key}";

                    // Search in char subdirectories
                    AnimationClip clip = Resources.Load<AnimationClip>($"{phase}/char/{searchPattern}");
                    if (clip != null) return clip;

                    // Try models/char path
                    clip = Resources.Load<AnimationClip>($"{phase}/models/char/{searchPattern}");
                    if (clip != null) return clip;
                }
            }

            // Try fallback pairs
            foreach (var (logical, fallback) in defaultPairs)
            {
                if (logical == key && fallback != key)
                {
                    AnimationClip fallbackClip = FindClip(fallback, phases);
                    if (fallbackClip != null)
                    {
                        Debug.Log($"🔄 Using fallback: '{key}' -> '{fallback}'");
                        return fallbackClip;
                    }
                }
            }

            return null;
        }

        private static void BuildAnimatorController(Dictionary<string, AnimationClip> clips)
        {
            Debug.Log("🏗️ Building AnimatorController...");

            string controllerPath = "Assets/Animations/Player/Player.controller";
            string directory = Path.GetDirectoryName(controllerPath);

            // Create directory if it doesn't exist
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            // Create or load controller
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                Debug.Log($"✅ Created new AnimatorController at {controllerPath}");
            }
            else
            {
                Debug.Log($"♻️ Updating existing AnimatorController at {controllerPath}");
            }

            // Clear existing parameters and layers
            controller.parameters = new AnimatorControllerParameter[0];
            controller.layers = new AnimatorControllerLayer[0];

            // Add parameters
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsSwimming", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("EmoteId", AnimatorControllerParameterType.Int);

            // Create base layer
            AnimatorControllerLayer baseLayer = new AnimatorControllerLayer
            {
                name = "Base Layer",
                defaultWeight = 1f,
                stateMachine = new AnimatorStateMachine()
            };
            baseLayer.stateMachine.name = "Base Layer";
            baseLayer.stateMachine.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(baseLayer.stateMachine, controller);

            // Add Ground sub-state machine
            CreateGroundStateMachine(controller, baseLayer.stateMachine, clips);

            // Add Swimming sub-state machine
            CreateSwimmingStateMachine(controller, baseLayer.stateMachine, clips);

            // Add layer to controller
            controller.AddLayer(baseLayer);

            // Save
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"✅ AnimatorController saved to {controllerPath}");
        }

        private static void CreateGroundStateMachine(AnimatorController controller, AnimatorStateMachine rootMachine, Dictionary<string, AnimationClip> clips)
        {
            Debug.Log("🏃 Creating Ground state machine...");

            // Create Ground sub-state machine
            AnimatorStateMachine groundSM = rootMachine.AddStateMachine("Ground", new Vector3(300, 0, 0));

            // Create locomotion blend tree
            if (clips.ContainsKey("idle") && clips.ContainsKey("walk") && clips.ContainsKey("run"))
            {
                AnimatorState locomotionState = groundSM.AddState("Locomotion", new Vector3(250, 100, 0));
                BlendTree locomotionBlend = new BlendTree();
                locomotionBlend.name = "Locomotion";
                locomotionBlend.blendParameter = "Speed";
                locomotionBlend.blendType = BlendTreeType.Simple1D;
                locomotionBlend.hideFlags = HideFlags.HideInHierarchy;

                locomotionBlend.AddChild(clips["idle"], 0f);
                locomotionBlend.AddChild(clips["walk"], 0.4f);
                locomotionBlend.AddChild(clips["run"], 1.0f);

                AssetDatabase.AddObjectToAsset(locomotionBlend, controller);
                locomotionState.motion = locomotionBlend;

                groundSM.defaultState = locomotionState;
            }

            // Add jump state
            if (clips.ContainsKey("jump"))
            {
                AnimatorState jumpState = groundSM.AddState("Jump", new Vector3(250, 200, 0));
                jumpState.motion = clips["jump"];

                // AnyState -> Jump on Jump trigger
                AnimatorStateTransition jumpTransition = groundSM.AddAnyStateTransition(jumpState);
                jumpTransition.AddCondition(AnimatorConditionMode.If, 0, "Jump");
                jumpTransition.duration = 0.1f;
            }
        }

        private static void CreateSwimmingStateMachine(AnimatorController controller, AnimatorStateMachine rootMachine, Dictionary<string, AnimationClip> clips)
        {
            Debug.Log("🏊 Creating Swimming state machine...");

            // Create Swimming sub-state machine
            AnimatorStateMachine swimSM = rootMachine.AddStateMachine("Swimming", new Vector3(300, 150, 0));

            // Create swimming 2D blend tree
            if (clips.ContainsKey("swim"))
            {
                AnimatorState swimState = swimSM.AddState("Swim Blend", new Vector3(250, 100, 0));
                BlendTree swimBlend = new BlendTree();
                swimBlend.name = "Swim Blend";
                swimBlend.blendParameter = "MoveX";
                swimBlend.blendParameterY = "MoveY";
                swimBlend.blendType = BlendTreeType.SimpleDirectional2D;
                swimBlend.hideFlags = HideFlags.HideInHierarchy;

                swimBlend.AddChild(clips["swim"], new Vector2(0, 1));

                if (clips.ContainsKey("swim_left"))
                    swimBlend.AddChild(clips["swim_left"], new Vector2(-1, 0));
                if (clips.ContainsKey("swim_right"))
                    swimBlend.AddChild(clips["swim_right"], new Vector2(1, 0));
                if (clips.ContainsKey("swim_back"))
                    swimBlend.AddChild(clips["swim_back"], new Vector2(0, -1));

                AssetDatabase.AddObjectToAsset(swimBlend, controller);
                swimState.motion = swimBlend;

                swimSM.defaultState = swimState;
            }
        }
    }
}
#endif
