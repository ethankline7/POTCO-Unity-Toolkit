using UnityEngine;
using POTCO.Editor;
using System.Collections.Generic;
using System.Linq;

namespace CaveGenerator.Algorithms
{
    public static class CaveValidationAlgorithm
    {
        public static Transform GetCavePieceFromConnector(Transform connector)
        {
            // Walk up the hierarchy to find the CavePiece_ wrapper
            Transform current = connector;
            while (current != null)
            {
                // Look specifically for the CavePiece_ wrapper
                if (current.name.StartsWith("CavePiece_"))
                {
                    return current;
                }
                current = current.parent;
            }
            return null; // Couldn't find cave piece wrapper
        }
        
        public static bool ValidateConnectionQuality(Transform fromConnector, Transform toConnector, float maxDistance = 2.0f, float maxAngle = 60f)
        {
            float connectionDistance = Vector3.Distance(fromConnector.position, toConnector.position);
            float connectionAngle = Vector3.Angle(fromConnector.forward, -toConnector.forward);
            
            DebugLogger.LogProceduralGeneration($"🔍 Connection Quality Check: Distance={connectionDistance:F3}m, Angle={connectionAngle:F1}°");
            DebugLogger.LogProceduralGeneration($"   From connector: {fromConnector.name} at {fromConnector.position}, Dir: {fromConnector.forward}");
            DebugLogger.LogProceduralGeneration($"   To connector: {toConnector.name} at {toConnector.position}, Dir: {toConnector.forward}");
            
            // Reject connections that are too far off
            if (connectionDistance > maxDistance || connectionAngle > maxAngle)
            {
                DebugLogger.LogWarningProceduralGeneration($"❌ Rejected poor connection: Distance={connectionDistance:F3}m, Angle={connectionAngle:F1}° between {fromConnector.name} and {toConnector.name}");
                DebugLogger.LogWarningProceduralGeneration($"   Thresholds: Distance must be ≤{maxDistance}m, Angle must be ≤{maxAngle}°");
                return false;
            }
            
            return true;
        }

        public static bool CheckOverlap(GameObject newPiece, List<GameObject> existingPieces, float tolerance, out string reason)
        {
            reason = "";

            // Get all renderers from the new piece to calculate bounds
            var newRenderers = newPiece.GetComponentsInChildren<Renderer>();
            if (newRenderers.Length == 0)
            {
                reason = "New piece has no renderers to calculate bounds";
                return true; // Allow if no renderers (can't calculate bounds)
            }

            // Calculate bounds of the new piece
            Bounds newBounds = newRenderers[0].bounds;
            foreach (var renderer in newRenderers)
            {
                newBounds.Encapsulate(renderer.bounds);
            }

            // Calculate acceptable overlap percentage based on tolerance
            // tolerance=0.5 allows ~15% overlap, tolerance=1.0 allows ~25%, tolerance=2.0 allows ~40%
            float maxAllowedOverlapPercent = 10f + (tolerance * 15f);

            DebugLogger.LogProceduralGeneration($"🔍 Overlap Check: New piece bounds center={newBounds.center}, size={newBounds.size}");
            DebugLogger.LogProceduralGeneration($"   Tolerance={tolerance}m allows up to {maxAllowedOverlapPercent:F1}% overlap");

            // Check against all existing pieces
            foreach (var existingPiece in existingPieces)
            {
                if (existingPiece == null) continue;

                var existingRenderers = existingPiece.GetComponentsInChildren<Renderer>();
                if (existingRenderers.Length == 0) continue;

                // Calculate bounds of existing piece
                Bounds existingBounds = existingRenderers[0].bounds;
                foreach (var renderer in existingRenderers)
                {
                    existingBounds.Encapsulate(renderer.bounds);
                }

                // Check if bounds intersect
                if (newBounds.Intersects(existingBounds))
                {
                    // Calculate overlap volume to determine severity
                    Vector3 overlapMin = Vector3.Max(newBounds.min, existingBounds.min);
                    Vector3 overlapMax = Vector3.Min(newBounds.max, existingBounds.max);
                    Vector3 overlapSize = overlapMax - overlapMin;
                    float overlapVolume = overlapSize.x * overlapSize.y * overlapSize.z;

                    float newVolume = newBounds.size.x * newBounds.size.y * newBounds.size.z;
                    float overlapPercentage = (overlapVolume / newVolume) * 100f;

                    // Only reject if overlap exceeds the tolerance threshold
                    if (overlapPercentage > maxAllowedOverlapPercent)
                    {
                        reason = $"Overlaps with {existingPiece.name}: {overlapPercentage:F1}% overlap (max allowed: {maxAllowedOverlapPercent:F1}%)";
                        DebugLogger.LogWarningProceduralGeneration($"❌ {reason}");
                        DebugLogger.LogWarningProceduralGeneration($"   New bounds: {newBounds.center}, size={newBounds.size}");
                        DebugLogger.LogWarningProceduralGeneration($"   Existing bounds: {existingBounds.center}, size={existingBounds.size}");
                        return false; // Reject excessive overlap
                    }
                    else
                    {
                        DebugLogger.LogProceduralGeneration($"✅ Acceptable overlap with {existingPiece.name}: {overlapPercentage:F1}% (within {maxAllowedOverlapPercent:F1}% tolerance)");
                    }
                }
            }

            DebugLogger.LogProceduralGeneration($"✅ No significant overlaps detected");
            return true; // No overlap detected
        }
    }
}