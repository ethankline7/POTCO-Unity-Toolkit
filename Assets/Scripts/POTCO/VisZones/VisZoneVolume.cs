using UnityEngine;
using System;

namespace POTCO.VisZones
{
    /// <summary>
    /// Marker component for VisZone collision volumes
    /// Lives on collision_zone_* GameObjects with trigger colliders
    /// Stores zone metadata for editor authoring
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class VisZoneVolume : MonoBehaviour
    {
        [Header("Zone Identity")]
        [Tooltip("Name of this zone (extracted from collision_zone_<name>)")]
        public string zoneName;

        [Tooltip("Unique GUID for this zone (for export and tracking)")]
        public string zoneGuid;

        [Header("Editor Visualization")]
        [Tooltip("Display color for gizmos and editor UI")]
        public Color displayColor = Color.cyan;

        [Tooltip("Author notes for this zone")]
        [TextArea(3, 6)]
        public string authorNotes = "";

        [Header("References")]
        [Tooltip("Reference to the trigger collider on this GameObject")]
        public Collider zoneCollider;

        [Tooltip("Reference to the corresponding Section root")]
        public VisZoneSection sectionRoot;

        private void Awake()
        {
            // Auto-extract zone name from GameObject name if not set
            if (string.IsNullOrEmpty(zoneName))
            {
                ExtractZoneName();
            }

            // Generate GUID if not set
            if (string.IsNullOrEmpty(zoneGuid))
            {
                GenerateGuid();
            }

            // Auto-find collider if not set
            if (zoneCollider == null)
            {
                zoneCollider = GetComponent<Collider>();
            }
        }

        private void OnValidate()
        {
            // Auto-extract zone name when component is added or modified in editor
            if (string.IsNullOrEmpty(zoneName))
            {
                ExtractZoneName();
            }

            // Auto-find collider
            if (zoneCollider == null)
            {
                zoneCollider = GetComponent<Collider>();
            }
        }

        /// <summary>
        /// Extract zone name from collision_zone_* GameObject name
        /// </summary>
        private void ExtractZoneName()
        {
            if (gameObject.name.StartsWith("collision_zone_"))
            {
                zoneName = gameObject.name.Substring("collision_zone_".Length);
            }
            else
            {
                zoneName = gameObject.name;
            }
        }

        /// <summary>
        /// Generate unique GUID for this zone
        /// </summary>
        private void GenerateGuid()
        {
            zoneGuid = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Manually regenerate GUID (for editor context menu)
        /// </summary>
        [ContextMenu("Generate New GUID")]
        public void RegenerateGuid()
        {
            GenerateGuid();
            Debug.Log($"[VisZoneVolume] Generated new GUID for zone '{zoneName}': {zoneGuid}");
        }

        /// <summary>
        /// Get zone bounds from collider
        /// </summary>
        public Bounds GetBounds()
        {
            if (zoneCollider != null)
            {
                return zoneCollider.bounds;
            }

            // Fallback: calculate from renderers
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                return bounds;
            }

            // Last resort: default bounds at position
            return new Bounds(transform.position, Vector3.one * 10f);
        }

        /// <summary>
        /// Find and link corresponding section root
        /// </summary>
        [ContextMenu("Find Section Root")]
        public void FindSectionRoot()
        {
            // Search for Section-<zoneName> in scene
            VisZoneSection[] allSections = FindObjectsByType<VisZoneSection>(FindObjectsSortMode.None);
            foreach (var section in allSections)
            {
                if (section.zoneName == zoneName)
                {
                    sectionRoot = section;
                    Debug.Log($"[VisZoneVolume] Linked zone '{zoneName}' to section at {section.gameObject.name}");
                    return;
                }
            }

            Debug.LogWarning($"[VisZoneVolume] No section found for zone '{zoneName}'");
        }
    }
}
