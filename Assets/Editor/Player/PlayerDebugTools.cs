/// <summary>
/// Debug tools for diagnosing Player Controller and Animator issues
/// </summary>
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace Player.Editor
{
    public static class PlayerDebugTools
    {
        [MenuItem("POTCO/Player/Debug/Check Selected Player Setup")]
        public static void CheckPlayerSetup()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select a player character in the scene.", "OK");
                return;
            }

            Debug.Log("=== PLAYER SETUP DIAGNOSTICS ===");
            Debug.Log($"Checking: {selected.name}");

            // Check CharacterController
            CharacterController cc = selected.GetComponent<CharacterController>();
            if (cc != null)
            {
                Debug.Log($"✅ CharacterController found - Height: {cc.height}, Radius: {cc.radius}");
            }
            else
            {
                Debug.LogError("❌ CharacterController missing!");
            }

            // Check Animator
            Animator animator = selected.GetComponent<Animator>();
            if (animator != null)
            {
                Debug.Log($"✅ Animator found");

                if (animator.runtimeAnimatorController != null)
                {
                    Debug.Log($"✅ AnimatorController: {animator.runtimeAnimatorController.name}");

                    // List all parameters
                    Debug.Log("📋 Animator Parameters:");
                    foreach (var param in animator.parameters)
                    {
                        string type = param.type.ToString();
                        Debug.Log($"  - {param.name} ({type})");
                    }

                    // Check for animation clips
                    AnimatorController ac = animator.runtimeAnimatorController as AnimatorController;
                    if (ac != null)
                    {
                        Debug.Log("📊 Checking animation clips in controller...");
                        CheckAnimatorClips(ac);
                    }
                }
                else
                {
                    Debug.LogError("❌ No AnimatorController assigned!");
                }

                // Check avatar
                if (animator.avatar != null)
                {
                    Debug.Log($"✅ Avatar: {animator.avatar.name} (Valid: {animator.avatar.isValid}, Human: {animator.avatar.isHuman})");
                }
                else
                {
                    Debug.LogWarning("⚠️ No Avatar assigned (may be OK for legacy animations)");
                }
            }
            else
            {
                Debug.LogError("❌ Animator missing!");
            }

            // Check PlayerController script
            Player.PlayerController pc = selected.GetComponent<Player.PlayerController>();
            if (pc != null)
            {
                Debug.Log($"✅ PlayerController script found");
            }
            else
            {
                Debug.LogWarning("⚠️ PlayerController script missing!");
            }

            // Check model rotation
            Debug.Log($"📐 Current rotation: {selected.transform.rotation.eulerAngles}");
            Debug.Log($"📍 Current position: {selected.transform.position}");

            Debug.Log("=== END DIAGNOSTICS ===");
        }

        private static void CheckAnimatorClips(AnimatorController controller)
        {
            int clipCount = 0;
            int nullClips = 0;

            foreach (var layer in controller.layers)
            {
                CheckStateMachine(layer.stateMachine, ref clipCount, ref nullClips);
            }

            Debug.Log($"📊 Found {clipCount} animation clips total");
            if (nullClips > 0)
            {
                Debug.LogError($"❌ Found {nullClips} NULL clips! Animations will not play!");
                Debug.LogError("   → Run 'POTCO > Player > Build Player AnimatorController' to fix");
            }
        }

        private static void CheckStateMachine(AnimatorStateMachine stateMachine, ref int clipCount, ref int nullClips)
        {
            foreach (var state in stateMachine.states)
            {
                if (state.state.motion != null)
                {
                    if (state.state.motion is AnimationClip)
                    {
                        clipCount++;
                        AnimationClip clip = state.state.motion as AnimationClip;
                        Debug.Log($"  ✅ {state.state.name}: {clip.name} ({clip.length:F2}s)");
                    }
                    else if (state.state.motion is BlendTree)
                    {
                        BlendTree tree = state.state.motion as BlendTree;
                        CheckBlendTree(tree, ref clipCount, ref nullClips);
                    }
                }
                else
                {
                    nullClips++;
                    Debug.LogError($"  ❌ {state.state.name}: NULL motion!");
                }
            }

            // Check sub-state machines
            foreach (var subSM in stateMachine.stateMachines)
            {
                CheckStateMachine(subSM.stateMachine, ref clipCount, ref nullClips);
            }
        }

        private static void CheckBlendTree(BlendTree tree, ref int clipCount, ref int nullClips)
        {
            Debug.Log($"  🌳 BlendTree: {tree.name}");
            foreach (var child in tree.children)
            {
                if (child.motion != null)
                {
                    if (child.motion is AnimationClip)
                    {
                        clipCount++;
                        AnimationClip clip = child.motion as AnimationClip;
                        Debug.Log($"    ✅ {clip.name} ({clip.length:F2}s)");
                    }
                    else if (child.motion is BlendTree)
                    {
                        CheckBlendTree(child.motion as BlendTree, ref clipCount, ref nullClips);
                    }
                }
                else
                {
                    nullClips++;
                    Debug.LogError($"    ❌ NULL motion in blend tree!");
                }
            }
        }

        [MenuItem("POTCO/Player/Debug/Enable Animator Debug Logging")]
        public static void EnableAnimatorDebug()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select a player character in the scene.", "OK");
                return;
            }

            Player.PlayerController pc = selected.GetComponent<Player.PlayerController>();
            if (pc != null)
            {
                SerializedObject so = new SerializedObject(pc);
                so.FindProperty("debugAnimator").boolValue = true;
                so.ApplyModifiedProperties();
                Debug.Log("✅ Animator debug logging enabled! Check console during play mode.");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Selected object doesn't have PlayerController script.", "OK");
            }
        }

        [MenuItem("POTCO/Player/Debug/Fix Model Rotation (Flip 180°)")]
        public static void FlipModelRotation()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select a player character in the scene.", "OK");
                return;
            }

            Undo.RecordObject(selected.transform, "Flip Model Rotation");
            selected.transform.Rotate(0f, 180f, 0f);
            Debug.Log($"🔄 Flipped model rotation 180°. New rotation: {selected.transform.rotation.eulerAngles}");
        }

        [MenuItem("POTCO/Player/Debug/Reset Model Rotation")]
        public static void ResetModelRotation()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select a player character in the scene.", "OK");
                return;
            }

            Undo.RecordObject(selected.transform, "Reset Model Rotation");
            selected.transform.rotation = Quaternion.identity;
            Debug.Log($"🔄 Reset model rotation. New rotation: {selected.transform.rotation.eulerAngles}");
        }
    }
}
#endif
