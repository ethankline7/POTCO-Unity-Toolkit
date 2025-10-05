/// <summary>
/// Applies body shape definitions to character rig.
/// Scales bones and applies head/body size multipliers.
/// Non-destructive - stores original scales for reset.
/// </summary>
using System.Collections.Generic;
using UnityEngine;
using CharacterOG.Models;

namespace CharacterOG.Runtime.Systems
{
    public class BodyShapeApplier
    {
        private Transform rigRoot;
        private Transform headRoot;
        private Transform bodyRoot;

        private Dictionary<Transform, Vector3> originalScales = new();
        private Dictionary<Transform, Vector3> originalPositions = new();
        private Dictionary<string, Transform> boneCache = new();

        private BodyShapeDef currentShape;

        /// <summary>
        /// Initialize applier.
        /// </summary>
        /// <param name="rigRoot">Root of the character rig</param>
        /// <param name="headRoot">Optional transform to scale for head size (e.g., "def_head")</param>
        /// <param name="bodyRoot">Optional transform to scale for body size (e.g., "def_spine01")</param>
        public BodyShapeApplier(Transform rigRoot, Transform headRoot = null, Transform bodyRoot = null)
        {
            this.rigRoot = rigRoot;
            this.headRoot = headRoot;
            this.bodyRoot = bodyRoot;

            BuildBoneCache();
        }

        /// <summary>Apply body shape definition</summary>
        public void ApplyBodyShape(BodyShapeDef shape)
        {
            if (shape == null)
            {
                Debug.LogWarning("BodyShapeApplier: Null shape provided");
                return;
            }

            currentShape = shape;

            // Reset to original scales first
            ResetToOriginal();

            // Apply bone scales
            foreach (var kvp in shape.boneScales)
            {
                string boneName = kvp.Key;
                Vector3 scale = kvp.Value;

                if (boneCache.TryGetValue(boneName, out Transform bone))
                {
                    // Store original if not already stored
                    if (!originalScales.ContainsKey(bone))
                    {
                        originalScales[bone] = bone.localScale;
                    }

                    // Apply scale as multiplier
                    bone.localScale = Vector3.Scale(originalScales[bone], scale);
                }
            }

            // Apply bone offsets (tr_* bones)
            foreach (var kvp in shape.boneOffsets)
            {
                string boneName = kvp.Key;
                Vector3 offset = kvp.Value;

                if (boneCache.TryGetValue(boneName, out Transform bone))
                {
                    // Store original if not already stored
                    if (!originalPositions.ContainsKey(bone))
                    {
                        originalPositions[bone] = bone.localPosition;
                    }

                    // Apply offset
                    bone.localPosition = originalPositions[bone] + offset;
                }
            }

            // Apply head scale
            if (headRoot != null && shape.headScale != 1f)
            {
                if (!originalScales.ContainsKey(headRoot))
                {
                    originalScales[headRoot] = headRoot.localScale;
                }

                headRoot.localScale = originalScales[headRoot] * shape.headScale;
            }

            // Apply body scale
            if (bodyRoot != null && shape.bodyScale != 1f)
            {
                if (!originalScales.ContainsKey(bodyRoot))
                {
                    originalScales[bodyRoot] = bodyRoot.localScale;
                }

                bodyRoot.localScale = originalScales[bodyRoot] * shape.bodyScale;
            }

            // Apply head position offset
            if (headRoot != null && shape.headPosition != Vector3.zero)
            {
                if (!originalPositions.ContainsKey(headRoot))
                {
                    originalPositions[headRoot] = headRoot.localPosition;
                }

                headRoot.localPosition = originalPositions[headRoot] + shape.headPosition;
            }

            Debug.Log($"BodyShapeApplier: Applied shape '{shape.name}' " +
                     $"(head:{shape.headScale}, body:{shape.bodyScale}, " +
                     $"bones:{shape.boneScales.Count}, offsets:{shape.boneOffsets.Count})");
        }

        /// <summary>Apply height bias (overall character scale adjustment)</summary>
        public void ApplyHeightBias(float heightBias)
        {
            if (rigRoot == null)
                return;

            // Height bias is typically a -1 to +1 range
            // Apply as additional scale on rig root
            float heightScale = 1f + (heightBias * 0.2f); // Scale by 20% per bias unit

            if (!originalScales.ContainsKey(rigRoot))
            {
                originalScales[rigRoot] = rigRoot.localScale;
            }

            rigRoot.localScale = originalScales[rigRoot] * heightScale;
        }

        /// <summary>Reset all bones to original scales/positions</summary>
        public void ResetToOriginal()
        {
            foreach (var kvp in originalScales)
            {
                if (kvp.Key != null)
                    kvp.Key.localScale = kvp.Value;
            }

            foreach (var kvp in originalPositions)
            {
                if (kvp.Key != null)
                    kvp.Key.localPosition = kvp.Value;
            }
        }

        /// <summary>Get current active shape</summary>
        public BodyShapeDef GetCurrentShape() => currentShape;

        private void BuildBoneCache()
        {
            if (rigRoot == null)
            {
                Debug.LogError("BodyShapeApplier: Rig root is null");
                return;
            }

            boneCache.Clear();

            // Cache all transforms in hierarchy by name
            var allTransforms = rigRoot.GetComponentsInChildren<Transform>(includeInactive: true);

            foreach (var t in allTransforms)
            {
                if (!boneCache.ContainsKey(t.name))
                {
                    boneCache[t.name] = t;
                }
            }

            Debug.Log($"BodyShapeApplier: Cached {boneCache.Count} bones");
        }

        /// <summary>Get diagnostic info</summary>
        public string GetDiagnosticInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"BodyShapeApplier for {rigRoot?.name}");
            sb.AppendLine($"Head Root: {headRoot?.name ?? "None"}");
            sb.AppendLine($"Body Root: {bodyRoot?.name ?? "None"}");
            sb.AppendLine($"Cached Bones: {boneCache.Count}");
            sb.AppendLine($"Current Shape: {currentShape?.name ?? "None"}");
            sb.AppendLine();

            if (currentShape != null)
            {
                sb.AppendLine($"Shape Details:");
                sb.AppendLine($"  Head Scale: {currentShape.headScale}");
                sb.AppendLine($"  Body Scale: {currentShape.bodyScale}");
                sb.AppendLine($"  Height Bias: {currentShape.heightBias}");
                sb.AppendLine($"  Bone Scales: {currentShape.boneScales.Count}");
                sb.AppendLine($"  Bone Offsets: {currentShape.boneOffsets.Count}");
            }

            return sb.ToString();
        }
    }
}
