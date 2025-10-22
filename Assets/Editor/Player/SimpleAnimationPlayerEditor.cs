using UnityEngine;
using UnityEditor;
using Player;
using System.Reflection;

namespace PlayerEditor
{
    /// <summary>
    /// Custom editor for SimpleAnimationPlayer
    /// Auto-fills animation clips and allows preview in Edit mode
    /// </summary>
    [CustomEditor(typeof(SimpleAnimationPlayer))]
    public class SimpleAnimationPlayerEditor : Editor
    {
        private SerializedProperty genderPrefixProp;
        private SerializedProperty idleClipProp;
        private SerializedProperty walkClipProp;
        private SerializedProperty runClipProp;
        private SerializedProperty walkBackClipProp;
        private SerializedProperty runBackClipProp;
        private SerializedProperty strafeLeftClipProp;
        private SerializedProperty strafeRightClipProp;
        private SerializedProperty runDiagonalLeftClipProp;
        private SerializedProperty runDiagonalRightClipProp;
        private SerializedProperty walkBackDiagonalLeftClipProp;
        private SerializedProperty walkBackDiagonalRightClipProp;
        private SerializedProperty turnLeftClipProp;
        private SerializedProperty turnRightClipProp;
        private SerializedProperty spinLeftClipProp;
        private SerializedProperty spinRightClipProp;
        private SerializedProperty jumpClipProp;
        private SerializedProperty swimClipProp;

        private Animation animComponent;
        private AnimationClip lastPreviewedClip;

        private void OnEnable()
        {
            // Get serialized properties
            genderPrefixProp = serializedObject.FindProperty("genderPrefix");
            idleClipProp = serializedObject.FindProperty("idleClip");
            walkClipProp = serializedObject.FindProperty("walkClip");
            runClipProp = serializedObject.FindProperty("runClip");
            walkBackClipProp = serializedObject.FindProperty("walkBackClip");
            runBackClipProp = serializedObject.FindProperty("runBackClip");
            strafeLeftClipProp = serializedObject.FindProperty("strafeLeftClip");
            strafeRightClipProp = serializedObject.FindProperty("strafeRightClip");
            runDiagonalLeftClipProp = serializedObject.FindProperty("runDiagonalLeftClip");
            runDiagonalRightClipProp = serializedObject.FindProperty("runDiagonalRightClip");
            walkBackDiagonalLeftClipProp = serializedObject.FindProperty("walkBackDiagonalLeftClip");
            walkBackDiagonalRightClipProp = serializedObject.FindProperty("walkBackDiagonalRightClip");
            turnLeftClipProp = serializedObject.FindProperty("turnLeftClip");
            turnRightClipProp = serializedObject.FindProperty("turnRightClip");
            spinLeftClipProp = serializedObject.FindProperty("spinLeftClip");
            spinRightClipProp = serializedObject.FindProperty("spinRightClip");
            jumpClipProp = serializedObject.FindProperty("jumpClip");
            swimClipProp = serializedObject.FindProperty("swimClip");

            // Find Animation component
            SimpleAnimationPlayer player = (SimpleAnimationPlayer)target;

            // Check Model child first
            Transform modelChild = player.transform.Find("Model");
            if (modelChild != null)
            {
                animComponent = modelChild.GetComponent<Animation>();
            }

            if (animComponent == null)
            {
                animComponent = player.GetComponent<Animation>();
            }

            if (animComponent == null)
            {
                animComponent = player.GetComponentInChildren<Animation>();
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw default inspector first
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            // Auto-fill button
            if (GUILayout.Button("Auto-Fill Animation Clips", GUILayout.Height(30)))
            {
                AutoFillAnimations();
            }

            EditorGUILayout.Space(5);

            // Preview section
            EditorGUILayout.LabelField("Animation Preview", EditorStyles.boldLabel);

            if (animComponent == null)
            {
                EditorGUILayout.HelpBox("No Animation component found. Make sure the character has an Animation component.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Preview Idle"))
                    PreviewAnimation(idleClipProp.objectReferenceValue as AnimationClip, "idle");

                if (GUILayout.Button("Preview Walk"))
                    PreviewAnimation(walkClipProp.objectReferenceValue as AnimationClip, "walk");

                if (GUILayout.Button("Preview Run"))
                    PreviewAnimation(runClipProp.objectReferenceValue as AnimationClip, "run");

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Preview Jump"))
                    PreviewAnimation(jumpClipProp.objectReferenceValue as AnimationClip, "jump");

                if (GUILayout.Button("Preview Strafe L"))
                    PreviewAnimation(strafeLeftClipProp.objectReferenceValue as AnimationClip, "strafe_left");

                if (GUILayout.Button("Preview Strafe R"))
                    PreviewAnimation(strafeRightClipProp.objectReferenceValue as AnimationClip, "strafe_right");

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                if (GUILayout.Button("Stop Preview"))
                {
                    StopPreview();
                }
            }

            serializedObject.ApplyModifiedProperties();

            // Auto-preview when clips change
            if (GUI.changed && animComponent != null)
            {
                AutoPreviewOnChange();
            }
        }

        private void AutoFillAnimations()
        {
            string genderPrefix = genderPrefixProp.stringValue;

            if (string.IsNullOrEmpty(genderPrefix))
            {
                Debug.LogWarning("Gender prefix is empty. Using 'mp_' (male) as default.");
                genderPrefix = "mp_";
                genderPrefixProp.stringValue = genderPrefix;
            }

            Debug.Log($"🔍 Auto-filling animations with prefix: {genderPrefix}");

            string[] phases = { "phase_2", "phase_3", "phase_4", "phase_5", "phase_6" };
            string[] searchPaths = { "char", "models/char" };

            // Auto-fill each animation
            idleClipProp.objectReferenceValue = FindClip("idle", genderPrefix, phases, searchPaths);
            walkClipProp.objectReferenceValue = FindClip("walk", genderPrefix, phases, searchPaths);
            runClipProp.objectReferenceValue = FindClip("run", genderPrefix, phases, searchPaths);

            walkBackClipProp.objectReferenceValue = FindClip("walk_back", genderPrefix, phases, searchPaths);
            if (walkBackClipProp.objectReferenceValue == null)
                walkBackClipProp.objectReferenceValue = FindClip("walk_backward", genderPrefix, phases, searchPaths);
            if (walkBackClipProp.objectReferenceValue == null)
                walkBackClipProp.objectReferenceValue = FindClip("walkback", genderPrefix, phases, searchPaths);

            runBackClipProp.objectReferenceValue = FindClip("run_back", genderPrefix, phases, searchPaths);
            if (runBackClipProp.objectReferenceValue == null)
                runBackClipProp.objectReferenceValue = FindClip("run_backward", genderPrefix, phases, searchPaths);
            if (runBackClipProp.objectReferenceValue == null)
                runBackClipProp.objectReferenceValue = FindClip("runback", genderPrefix, phases, searchPaths);

            strafeLeftClipProp.objectReferenceValue = FindClip("strafe_left", genderPrefix, phases, searchPaths);
            if (strafeLeftClipProp.objectReferenceValue == null)
                strafeLeftClipProp.objectReferenceValue = FindClip("walk_left", genderPrefix, phases, searchPaths);
            if (strafeLeftClipProp.objectReferenceValue == null)
                strafeLeftClipProp.objectReferenceValue = FindClip("strafeleft", genderPrefix, phases, searchPaths);

            strafeRightClipProp.objectReferenceValue = FindClip("strafe_right", genderPrefix, phases, searchPaths);
            if (strafeRightClipProp.objectReferenceValue == null)
                strafeRightClipProp.objectReferenceValue = FindClip("walk_right", genderPrefix, phases, searchPaths);
            if (strafeRightClipProp.objectReferenceValue == null)
                strafeRightClipProp.objectReferenceValue = FindClip("straferight", genderPrefix, phases, searchPaths);

            runDiagonalLeftClipProp.objectReferenceValue = FindClip("run_diagonal_left", genderPrefix, phases, searchPaths);
            runDiagonalRightClipProp.objectReferenceValue = FindClip("run_diagonal_right", genderPrefix, phases, searchPaths);
            walkBackDiagonalLeftClipProp.objectReferenceValue = FindClip("walk_back_diagonal_left", genderPrefix, phases, searchPaths);
            walkBackDiagonalRightClipProp.objectReferenceValue = FindClip("walk_back_diagonal_right", genderPrefix, phases, searchPaths);

            turnLeftClipProp.objectReferenceValue = FindClip("turn_left", genderPrefix, phases, searchPaths);
            turnRightClipProp.objectReferenceValue = FindClip("turn_right", genderPrefix, phases, searchPaths);
            spinLeftClipProp.objectReferenceValue = FindClip("spin_left", genderPrefix, phases, searchPaths);
            spinRightClipProp.objectReferenceValue = FindClip("spin_right", genderPrefix, phases, searchPaths);

            jumpClipProp.objectReferenceValue = FindClip("jump", genderPrefix, phases, searchPaths);
            swimClipProp.objectReferenceValue = FindClip("swim", genderPrefix, phases, searchPaths);

            serializedObject.ApplyModifiedProperties();

            Debug.Log("✅ Auto-fill complete!");
        }

        private AnimationClip FindClip(string animName, string genderPrefix, string[] phases, string[] searchPaths)
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
                        return clip;
                    }
                }
            }

            return null;
        }

        private void PreviewAnimation(AnimationClip clip, string clipName)
        {
            if (clip == null || animComponent == null)
            {
                Debug.LogWarning($"Cannot preview {clipName}: clip or Animation component is null");
                return;
            }

            // Stop any existing animation mode
            if (AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
            }

            // Start Animation mode (allows animation in Edit mode)
            AnimationMode.StartAnimationMode();

            // Sample the animation at time 0
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animComponent.gameObject, clip, 0f);
            AnimationMode.EndSampling();

            lastPreviewedClip = clip;

            Debug.Log($"▶️ Previewing: {clipName} (sample at time 0)");
        }

        private void StopPreview()
        {
            if (AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
                Debug.Log("⏹️ Preview stopped");
            }
        }

        private void AutoPreviewOnChange()
        {
            // Auto-preview when user changes a clip
            if (idleClipProp.objectReferenceValue != null && idleClipProp.objectReferenceValue != lastPreviewedClip)
            {
                PreviewAnimation(idleClipProp.objectReferenceValue as AnimationClip, "idle");
            }
            else if (walkClipProp.objectReferenceValue != null && walkClipProp.objectReferenceValue != lastPreviewedClip)
            {
                PreviewAnimation(walkClipProp.objectReferenceValue as AnimationClip, "walk");
            }
            else if (runClipProp.objectReferenceValue != null && runClipProp.objectReferenceValue != lastPreviewedClip)
            {
                PreviewAnimation(runClipProp.objectReferenceValue as AnimationClip, "run");
            }
            else if (jumpClipProp.objectReferenceValue != null && jumpClipProp.objectReferenceValue != lastPreviewedClip)
            {
                PreviewAnimation(jumpClipProp.objectReferenceValue as AnimationClip, "jump");
            }
        }

        private void OnDisable()
        {
            // Stop preview when inspector is closed
            StopPreview();
        }
    }
}
