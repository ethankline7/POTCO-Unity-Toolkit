/// <summary>
/// Applies facial morph parameters to character head bones.
/// Similar to BodyShapeApplier but for facial DNA values.
/// Non-destructive - stores original transforms for reset.
/// </summary>
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CharacterOG.Models;

namespace CharacterOG.Runtime.Systems
{
    public class FacialMorphApplier
    {
        private Transform headRoot;
        private Transform rigRoot;
        private FacialMorphDatabase database;

        private Dictionary<Transform, Vector3> originalPositions = new();
        private Dictionary<Transform, Quaternion> originalRotations = new();
        private Dictionary<Transform, Vector3> originalScales = new();
        private Dictionary<string, Transform> boneCache = new();

        private Dictionary<string, float> currentMorphs = new();

        // BLEND SHAPE TO BONE TRANSFORM CONVERSION SCALE
        // POTCO uses blend shapes (vertex morphs). We're approximating with bone transforms.
        // Adjust this value to match POTCO's visual appearance:
        // - Too high = exaggerated features
        // - Too low = subtle/no effect
        // Recommended range: 0.1 to 0.5
        private const float BLEND_TO_BONE_SCALE = 0.25f;

        /// <summary>
        /// Initialize applier.
        /// </summary>
        /// <param name="headRoot">Root transform of the head (e.g., "def_neck")</param>
        /// <param name="database">Facial morph database for the character's gender</param>
        /// <param name="rigRoot">Optional rig root to search for bones (if null, uses headRoot)</param>
        public FacialMorphApplier(Transform headRoot, FacialMorphDatabase database, Transform rigRoot = null)
        {
            this.headRoot = headRoot;
            this.database = database;
            this.rigRoot = rigRoot ?? headRoot;

            Debug.Log($"[FacialMorphApplier] Initialized with headRoot='{headRoot?.name}', rigRoot='{this.rigRoot?.name}', gender='{database?.gender}', BLEND_TO_BONE_SCALE={BLEND_TO_BONE_SCALE}");

            BuildBoneCache();
        }

        /// <summary>Apply all facial morphs from DNA</summary>
        public void ApplyMorphs(Dictionary<string, float> morphValues)
        {
            if (morphValues == null || morphValues.Count == 0)
            {
                return;
            }

            if (database == null || database.morphs.Count == 0)
            {
                Debug.LogWarning($"[FacialMorphApplier] Cannot apply {morphValues.Count} facial morphs - morph database is empty");
                return;
            }

            currentMorphs = new Dictionary<string, float>(morphValues);

            // Reset to original transforms first
            ResetToOriginal();

            int totalApplied = 0;
            int totalSkipped = 0;
            int totalTransformsApplied = 0;
            int totalBonesNotFound = 0;

            foreach (var kvp in morphValues)
            {
                string morphName = kvp.Key;
                float morphValue = kvp.Value;

                // Skip morphs with value of 0 (no change)
                if (Mathf.Approximately(morphValue, 0f))
                {
                    totalSkipped++;
                    continue;
                }

                var morphDef = database.GetMorph(morphName);
                if (morphDef == null)
                {
                    Debug.LogWarning($"[FacialMorphApplier] Morph '{morphName}' not found in database");
                    totalSkipped++;
                    continue;
                }

                // Apply morph based on sign
                var transforms = morphValue > 0 ? morphDef.positiveTransforms : morphDef.negativeTransforms;
                float absValue = Mathf.Abs(morphValue);

                Debug.Log($"[FacialMorphApplier] Processing morph '{morphName}' = {morphValue} (using {(morphValue > 0 ? "positive" : "negative")} transforms, count={transforms.Count})");

                int transformsForThisMorph = 0;
                foreach (var boneTransform in transforms)
                {
                    bool applied = ApplyBoneTransform(boneTransform, absValue);
                    if (applied)
                        transformsForThisMorph++;
                    else
                        totalBonesNotFound++;
                }

                totalTransformsApplied += transformsForThisMorph;
                totalApplied++;
            }

            Debug.Log($"[FacialMorphApplier] Applied {totalApplied} morphs (skipped {totalSkipped} zero-value), {totalTransformsApplied} bone transforms applied, {totalBonesNotFound} bones not found");

            if (totalBonesNotFound > 0)
            {
                Debug.LogWarning($"[FacialMorphApplier] {totalBonesNotFound} bones were not found in the character model - facial morphs may be incomplete");
            }
        }

        /// <summary>Apply a single bone transform with morph value multiplier</summary>
        /// <returns>True if bone was found and transform was applied</returns>
        private bool ApplyBoneTransform(BoneTransform boneTransform, float morphValue)
        {
            if (!boneCache.TryGetValue(boneTransform.boneName, out Transform bone))
            {
                // Bone not found - this is normal for some bones that may not exist in all models
                Debug.Log($"[FacialMorphApplier] Bone '{boneTransform.boneName}' NOT FOUND in cache");
                return false;
            }

            // Store original if not already stored
            if (!originalPositions.ContainsKey(bone))
            {
                originalPositions[bone] = bone.localPosition;
                originalRotations[bone] = bone.localRotation;
                originalScales[bone] = bone.localScale;
            }

            // Calculate delta
            // For translations (TX/TY/TZ) and rotations (RX/RY/RZ): use value directly
            // For scales (SX/SY/SZ): POTCO values like 1.1 mean "scale to 1.1x", so we need delta from 1.0
            float delta;
            bool isScale = boneTransform.transformType >= TransformType.SX; // SX, SY, SZ

            if (isScale)
            {
                // Scale values in ControlShapes are target scales (e.g., 1.1 = 110% scale)
                // Convert to additive delta: (targetScale - 1.0) * morphValue * blendToBoneScale
                delta = (boneTransform.value - 1.0f) * morphValue * BLEND_TO_BONE_SCALE;
            }
            else
            {
                // Translations and rotations are direct deltas: baseValue * morphValue * blendToBoneScale
                delta = boneTransform.value * morphValue * BLEND_TO_BONE_SCALE;
            }

            // Capture BEFORE values
            Vector3 beforePos = bone.localPosition;
            Quaternion beforeRot = bone.localRotation;
            Vector3 beforeScale = bone.localScale;

            // Apply transform based on type
            // COORDINATE CONVERSION: POTCO (Panda3D) uses Y=forward, Z=up. Unity uses Y=up, Z=forward. Swap Y and Z.
            switch (boneTransform.transformType)
            {
                case TransformType.TX:
                    bone.localPosition += new Vector3(delta, 0, 0);
                    Debug.Log($"[FacialMorphApplier] '{bone.name}' TX: {beforePos} + ({delta},0,0) = {bone.localPosition} [value={boneTransform.value} * morph={morphValue}]");
                    break;
                case TransformType.TY:
                    // POTCO TY (forward/back) → Unity TZ (forward/back)
                    bone.localPosition += new Vector3(0, 0, delta);
                    Debug.Log($"[FacialMorphApplier] '{bone.name}' TY: {beforePos} + (0,0,{delta}) = {bone.localPosition} [value={boneTransform.value} * morph={morphValue}] [POTCO Y→Unity Z]");
                    break;
                case TransformType.TZ:
                    // POTCO TZ (up/down) → Unity TY (up/down)
                    bone.localPosition += new Vector3(0, delta, 0);
                    Debug.Log($"[FacialMorphApplier] '{bone.name}' TZ: {beforePos} + (0,{delta},0) = {bone.localPosition} [value={boneTransform.value} * morph={morphValue}] [POTCO Z→Unity Y]");
                    break;
                case TransformType.RX:
                    bone.localRotation *= Quaternion.Euler(delta, 0, 0);
                    Debug.Log($"[FacialMorphApplier] '{bone.name}' RX: {beforeRot.eulerAngles} + ({delta},0,0) = {bone.localRotation.eulerAngles} [value={boneTransform.value} * morph={morphValue}]");
                    break;
                case TransformType.RY:
                    // POTCO RY (pitch around forward axis) → Unity RZ (pitch around forward axis)
                    bone.localRotation *= Quaternion.Euler(0, 0, delta);
                    Debug.Log($"[FacialMorphApplier] '{bone.name}' RY: {beforeRot.eulerAngles} + (0,0,{delta}) = {bone.localRotation.eulerAngles} [value={boneTransform.value} * morph={morphValue}] [POTCO Y→Unity Z]");
                    break;
                case TransformType.RZ:
                    // POTCO RZ (roll around up axis) → Unity RY (roll around up axis)
                    bone.localRotation *= Quaternion.Euler(0, delta, 0);
                    Debug.Log($"[FacialMorphApplier] '{bone.name}' RZ: {beforeRot.eulerAngles} + (0,{delta},0) = {bone.localRotation.eulerAngles} [value={boneTransform.value} * morph={morphValue}] [POTCO Z→Unity Y]");
                    break;
                case TransformType.SX:
                    bone.localScale += new Vector3(delta, 0, 0);
                    Debug.Log($"[FacialMorphApplier] '{bone.name}' SX: {beforeScale} + ({delta},0,0) = {bone.localScale} [value={boneTransform.value} * morph={morphValue}]");
                    break;
                case TransformType.SY:
                    // POTCO SY (scale forward) → Unity SZ (scale forward)
                    bone.localScale += new Vector3(0, 0, delta);
                    Debug.Log($"[FacialMorphApplier] '{bone.name}' SY: {beforeScale} + (0,0,{delta}) = {bone.localScale} [value={boneTransform.value} * morph={morphValue}] [POTCO Y→Unity Z]");
                    break;
                case TransformType.SZ:
                    // POTCO SZ (scale up) → Unity SY (scale up)
                    bone.localScale += new Vector3(0, delta, 0);
                    Debug.Log($"[FacialMorphApplier] '{bone.name}' SZ: {beforeScale} + (0,{delta},0) = {bone.localScale} [value={boneTransform.value} * morph={morphValue}] [POTCO Z→Unity Y]");
                    break;
            }

            return true;
        }

        /// <summary>Reset all bones to original transforms</summary>
        public void ResetToOriginal()
        {
            foreach (var kvp in originalPositions)
            {
                if (kvp.Key != null)
                    kvp.Key.localPosition = kvp.Value;
            }

            foreach (var kvp in originalRotations)
            {
                if (kvp.Key != null)
                    kvp.Key.localRotation = kvp.Value;
            }

            foreach (var kvp in originalScales)
            {
                if (kvp.Key != null)
                    kvp.Key.localScale = kvp.Value;
            }
        }

        /// <summary>Get current active morphs</summary>
        public Dictionary<string, float> GetCurrentMorphs() => currentMorphs;

        private void BuildBoneCache()
        {
            if (rigRoot == null)
            {
                Debug.LogError("FacialMorphApplier: Rig root is null");
                return;
            }

            boneCache.Clear();

            // Cache all transforms from rig root hierarchy by name
            var allTransforms = rigRoot.GetComponentsInChildren<Transform>(includeInactive: true);

            foreach (var t in allTransforms)
            {
                if (!boneCache.ContainsKey(t.name))
                {
                    boneCache[t.name] = t;
                }
            }

            Debug.Log($"[FacialMorphApplier] Cached {boneCache.Count} bones from rig root '{rigRoot.name}'");

            // Log specifically if we found common facial bones
            var facialBonePatterns = new[] { "def_trs_", "trs_face_", "trs_left_", "trs_right_", "trs_mid_", "jaw", "forehead", "cheek", "nose", "eye" };
            var foundFacialBones = boneCache.Keys.Where(name => facialBonePatterns.Any(pattern => name.Contains(pattern))).ToList();
            Debug.Log($"[FacialMorphApplier] Found {foundFacialBones.Count} facial-related bones: {string.Join(", ", foundFacialBones)}");
        }

        /// <summary>Get diagnostic info</summary>
        public string GetDiagnosticInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"FacialMorphApplier for {headRoot?.name}");
            sb.AppendLine($"Gender: {database?.gender ?? "None"}");
            sb.AppendLine($"Cached Bones: {boneCache.Count}");
            sb.AppendLine($"Active Morphs: {currentMorphs.Count}");
            sb.AppendLine();

            if (currentMorphs.Count > 0)
            {
                sb.AppendLine($"Current Morph Values:");
                foreach (var kvp in currentMorphs)
                {
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value:F3}");
                }
            }

            return sb.ToString();
        }
    }
}
