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
    /// <summary>
    /// Axis permutation modes - which Unity axis each POTCO axis maps to
    /// </summary>
    public enum AxisPermutation
    {
        XYZ = 0,  // POTCO X→Unity X, Y→Y, Z→Z
        XZY = 1,  // POTCO X→Unity X, Y→Z, Z→Y
        YXZ = 2,  // POTCO X→Unity Y, Y→X, Z→Z
        YZX = 3,  // POTCO X→Unity Y, Y→Z, Z→X
        ZXY = 4,  // POTCO X→Unity Z, Y→X, Z→Y
        ZYX = 5,  // POTCO X→Unity Z, Y→Y, Z→X
    }

    /// <summary>
    /// Sign pattern - which axes to negate
    /// </summary>
    public struct SignPattern
    {
        public bool negateX;
        public bool negateY;
        public bool negateZ;

        public SignPattern(bool x, bool y, bool z)
        {
            negateX = x;
            negateY = y;
            negateZ = z;
        }

        public override string ToString() => $"{(negateX ? "-" : "+")}X{(negateY ? "-" : "+")}Y{(negateZ ? "-" : "+")}Z";
    }

    /// <summary>
    /// Complete coordinate conversion combining permutation and signs
    /// </summary>
    public struct CoordinateConversion
    {
        public AxisPermutation permutation;
        public SignPattern signs;

        public CoordinateConversion(AxisPermutation perm, SignPattern sign)
        {
            permutation = perm;
            signs = sign;
        }

        public override string ToString() => $"{permutation}_{signs}";
    }

    public class FacialMorphApplier
    {
        // Dampening factors - Set to 1.0 to match POTCO's original behavior
        private const float TRANSLATION_SCALE = 1.0f;  // 100% - POTCO original
        private const float ROTATION_SCALE = 1.0f;      // 100% - POTCO original
        private const float SCALE_DAMPENING = 1.0f;     // 100% - POTCO original

        // Coordinate conversion for facial morphs
        // XZY for ALL transform types: POTCO Z=up → Unity Y=up
        // ControlShapes axes are in POTCO coordinate system
        // POTCO: X=right, Y=forward, Z=up → Unity: X=right, Y=up, Z=forward
        // Examples: POTCO TZ (up) → Unity TY (up), POTCO RY (pitch) → Unity RZ (pitch)
        // Rotation: ALL negated (matches CoordinateConverter)
        public static AxisPermutation CurrentPermutation = AxisPermutation.XZY;  // All transforms

        private Transform headRoot;
        private Transform rigRoot;
        private FacialMorphDatabase database;

        private Dictionary<Transform, Vector3> originalPositions = new();
        private Dictionary<Transform, Quaternion> originalRotations = new();
        private Dictionary<Transform, Vector3> originalScales = new();
        private Dictionary<string, Transform> boneCache = new();

        // Track accumulated rotation deltas (in Euler angles) for proper additive application
        private Dictionary<Transform, Vector3> rotationDeltas = new();

        private Dictionary<string, float> currentMorphs = new();

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

            Debug.Log($"[FacialMorphApplier] Initialized with headRoot='{headRoot?.name}', rigRoot='{this.rigRoot?.name}', gender='{database?.gender}' " +
                     $"(POTCO ORIGINAL MODE: trans={TRANSLATION_SCALE}, rot={ROTATION_SCALE}, scale={SCALE_DAMPENING})");

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

            // Clear rotation deltas for new application
            rotationDeltas.Clear();

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

                // POTCO ORIGINAL: Use morph values directly without smoothing/clamping
                // morphValue is already the slider value (-1.0 to 1.0)
                // This gets multiplied by base transform values in ControlShapes

                var morphDef = database.GetMorph(morphName);
                if (morphDef == null)
                {
                    Debug.LogWarning($"[FacialMorphApplier] Morph '{morphName}' not found in database");
                    totalSkipped++;
                    continue;
                }

                // Apply morph based on sign
                var transforms = morphValue > 0 ? morphDef.positiveTransforms : morphDef.negativeTransforms;

                Debug.Log($"[FacialMorphApplier] Processing morph '{morphName}' = {morphValue} (using {(morphValue > 0 ? "positive" : "negative")} transforms, count={transforms.Count})");

                int transformsForThisMorph = 0;
                foreach (var boneTransform in transforms)
                {
                    bool applied = ApplyBoneTransform(boneTransform, morphValue);
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

            // Apply accumulated rotation deltas (POTCO: hprF = hprI + hprDelta)
            foreach (var kvp in rotationDeltas)
            {
                Transform bone = kvp.Key;
                Vector3 eulerDelta = kvp.Value;

                if (bone != null && originalRotations.ContainsKey(bone))
                {
                    // Convert original rotation to Euler, add delta, convert back
                    Vector3 originalEuler = originalRotations[bone].eulerAngles;
                    Vector3 finalEuler = originalEuler + eulerDelta;
                    bone.localRotation = Quaternion.Euler(finalEuler);

                    Debug.Log($"[FacialMorphApplier] '{bone.name}' Final Rotation: {originalEuler} + {eulerDelta} = {finalEuler}");
                }
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

            // Calculate delta using POTCO's formula with Unity-side dampening
            // POTCO: dr = base * r
            //        if (r < 0 && isScale): add = dr / base = r
            //        else: add = dr = base * r
            float delta;
            bool isScale = boneTransform.transformType >= TransformType.SX; // SX, SY, SZ
            bool isRotation = boneTransform.transformType >= TransformType.RX && boneTransform.transformType <= TransformType.RZ;

            if (isScale && morphValue < 0)
            {
                // Negative scale: dr = base * r, add = dr / base = r
                // Simplified: just add morphValue directly
                delta = morphValue * SCALE_DAMPENING;
            }
            else if (isScale)
            {
                // Positive scale: delta = base * r * dampening
                delta = boneTransform.value * morphValue * SCALE_DAMPENING;
            }
            else if (isRotation)
            {
                // Rotation: delta = base * r * dampening
                delta = boneTransform.value * morphValue * ROTATION_SCALE;
            }
            else
            {
                // Translation: delta = base * r * dampening
                delta = boneTransform.value * morphValue * TRANSLATION_SCALE;
            }

            // Capture BEFORE values
            Vector3 beforePos = bone.localPosition;
            Quaternion beforeRot = bone.localRotation;
            Vector3 beforeScale = bone.localScale;

            // Apply transform based on type using selected coordinate conversion mode
            ApplyTransformWithMode(bone, boneTransform.transformType, delta, beforePos, beforeScale, boneTransform.value, morphValue);

            return true;
        }

        /// <summary>Apply transform with selected coordinate conversion mode</summary>
        private void ApplyTransformWithMode(Transform bone, TransformType transformType, float delta, Vector3 beforePos, Vector3 beforeScale, float baseValue, float morphValue)
        {
            string modeName = $"{CurrentPermutation}";

            switch (transformType)
            {
                case TransformType.TX:
                    ApplyPositionDelta(bone, delta, 0, beforePos, baseValue, morphValue, "TX", modeName);
                    break;
                case TransformType.TY:
                    ApplyPositionDelta(bone, delta, 1, beforePos, baseValue, morphValue, "TY", modeName);
                    break;
                case TransformType.TZ:
                    ApplyPositionDelta(bone, delta, 2, beforePos, baseValue, morphValue, "TZ", modeName);
                    break;
                case TransformType.RX:
                    ApplyRotationDelta(bone, delta, 0, baseValue, morphValue, "RX", modeName);
                    break;
                case TransformType.RY:
                    ApplyRotationDelta(bone, delta, 1, baseValue, morphValue, "RY", modeName);
                    break;
                case TransformType.RZ:
                    ApplyRotationDelta(bone, delta, 2, baseValue, morphValue, "RZ", modeName);
                    break;
                case TransformType.SX:
                    ApplyScaleDelta(bone, delta, 0, beforeScale, baseValue, morphValue, "SX", modeName);
                    break;
                case TransformType.SY:
                    ApplyScaleDelta(bone, delta, 1, beforeScale, baseValue, morphValue, "SY", modeName);
                    break;
                case TransformType.SZ:
                    ApplyScaleDelta(bone, delta, 2, beforeScale, baseValue, morphValue, "SZ", modeName);
                    break;
            }
        }

        /// <summary>Apply position delta based on conversion mode</summary>
        private void ApplyPositionDelta(Transform bone, float delta, int potcoAxis, Vector3 beforePos, float baseValue, float morphValue, string axisName, string modeName)
        {
            int unityAxis = ConvertAxis(potcoAxis, CurrentPermutation);

            // Positions are NOT negated in POTCO (only rotations are negated)
            float finalDelta = delta;

            Vector3 deltaVec = Vector3.zero;
            deltaVec[unityAxis] = finalDelta;
            bone.localPosition += deltaVec;
            Debug.Log($"[FacialMorphApplier] '{bone.name}' {axisName}: {beforePos} + delta[{unityAxis}]={finalDelta} = {bone.localPosition} [mode={modeName}]");
        }

        /// <summary>Apply rotation delta based on conversion mode</summary>
        private void ApplyRotationDelta(Transform bone, float delta, int potcoAxis, float baseValue, float morphValue, string axisName, string modeName)
        {
            // Get Unity axis - some bones need special axis mapping
            int unityAxis = GetRotationUnityAxis(bone.name, potcoAxis);

            if (!rotationDeltas.ContainsKey(bone))
                rotationDeltas[bone] = Vector3.zero;

            // POTCO→Unity rotation conversion: ALL rotations are negated (matches CoordinateConverter)
            // This is the ONLY transform type that gets negated
            float finalDelta = -delta;

            // Special case: Normalize ear rotation so both ears move at the same speed
            // POTCO has left=-20, right=-160, but we want them to move symmetrically
            if (bone.name.Contains("ear") && potcoAxis == 0) // earFlap uses RX
            {
                // Use a fixed magnitude for both ears, ignore POTCO's asymmetric base values
                float normalizedMagnitude = 90f; // Midpoint between 20 and 160

                // Left ear: negative rotation (flaps one way)
                // Right ear: positive rotation (flaps opposite way, creating symmetric outward motion)
                if (bone.name.Contains("right"))
                {
                    finalDelta = normalizedMagnitude * morphValue; // Positive direction
                }
                else
                {
                    finalDelta = -normalizedMagnitude * morphValue; // Negative direction
                }

                Debug.Log($"[FacialMorphApplier] Normalized ear rotation for '{bone.name}': {finalDelta} (morphValue={morphValue})");
            }

            Vector3 deltaVec = rotationDeltas[bone];
            deltaVec[unityAxis] += finalDelta;
            rotationDeltas[bone] = deltaVec;
            Debug.Log($"[FacialMorphApplier] '{bone.name}' {axisName}: accumulating delta[{unityAxis}]={finalDelta} (from {delta}) [bone-specific axis={unityAxis}]");
        }

        /// <summary>Get Unity axis for rotation based on bone name and POTCO axis</summary>
        private int GetRotationUnityAxis(string boneName, int potcoAxis)
        {
            // Special cases: Some bones need specific Unity axes regardless of POTCO axis

            // earFlap (POTCO RX) → Unity Y rotation (axis 1)
            if (boneName.Contains("ear") && potcoAxis == 0) // POTCO RX
            {
                Debug.Log($"[FacialMorphApplier] earFlap override: POTCO RX → Unity RY (axis 1)");
                return 1; // Unity Y axis
            }

            // noseNostrilAngle (POTCO RY) → Unity X rotation (axis 0)
            if (boneName.Contains("nose") && potcoAxis == 1) // POTCO RY
            {
                Debug.Log($"[FacialMorphApplier] noseNostrilAngle override: POTCO RY → Unity RX (axis 0)");
                return 0; // Unity X axis
            }

            // eyeCorner: Use standard XYZ (no swap)
            if (boneName.Contains("eyesocket"))
            {
                return ConvertAxis(potcoAxis, AxisPermutation.XYZ);
            }

            // Default: Use standard XZY conversion for other bones
            return ConvertAxis(potcoAxis, AxisPermutation.XZY);
        }

        /// <summary>Apply scale delta based on conversion mode</summary>
        private void ApplyScaleDelta(Transform bone, float delta, int potcoAxis, Vector3 beforeScale, float baseValue, float morphValue, string axisName, string modeName)
        {
            int unityAxis = ConvertAxis(potcoAxis, CurrentPermutation);

            // Scales are NOT negated in POTCO (only rotations are negated)
            float finalDelta = delta;

            Vector3 deltaVec = Vector3.zero;
            deltaVec[unityAxis] = finalDelta;
            bone.localScale += deltaVec;
            Debug.Log($"[FacialMorphApplier] '{bone.name}' {axisName}: {beforeScale} + delta[{unityAxis}]={finalDelta} = {bone.localScale} [mode={modeName}]");
        }

        /// <summary>Convert POTCO axis (0=X, 1=Y, 2=Z) to Unity axis based on permutation</summary>
        private int ConvertAxis(int potcoAxis, AxisPermutation permutation)
        {
            switch (permutation)
            {
                case AxisPermutation.XYZ:
                    // X→X, Y→Y, Z→Z
                    return potcoAxis;

                case AxisPermutation.XZY:
                    // X→X, Y→Z, Z→Y (standard POTCO)
                    return potcoAxis == 0 ? 0 : (potcoAxis == 1 ? 2 : 1);

                case AxisPermutation.YXZ:
                    // X→Y, Y→X, Z→Z
                    return potcoAxis == 0 ? 1 : (potcoAxis == 1 ? 0 : 2);

                case AxisPermutation.YZX:
                    // X→Y, Y→Z, Z→X
                    return potcoAxis == 0 ? 1 : (potcoAxis == 1 ? 2 : 0);

                case AxisPermutation.ZXY:
                    // X→Z, Y→X, Z→Y
                    return potcoAxis == 0 ? 2 : (potcoAxis == 1 ? 0 : 2);

                case AxisPermutation.ZYX:
                    // X→Z, Y→Y, Z→X
                    return potcoAxis == 0 ? 2 : (potcoAxis == 2 ? 0 : 1);

                default:
                    return potcoAxis;
            }
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
