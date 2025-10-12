using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace POTCO.VisZones
{
    /// <summary>
    /// Manages visibility of zone sections based on player location
    /// Attach to the root of an area/island with VisZones
    /// </summary>
    [RequireComponent(typeof(VisZoneData))]
    public class VisZoneManager : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Vis Zone data component (auto-detected)")]
        public VisZoneData visZoneData;

        [Header("Section Management")]
        [Tooltip("All zone sections in the scene (auto-populated)")]
        public List<VisZoneSection> zoneSections = new List<VisZoneSection>();

        [Header("Current State")]
        [Tooltip("Currently active zone (player location)")]
        [SerializeField]
        private string currentZone = "";

        [Tooltip("Zones currently visible")]
        [SerializeField]
        private List<string> currentlyVisibleZones = new List<string>();

        private Dictionary<string, VisZoneSection> zoneSectionDict = new Dictionary<string, VisZoneSection>();

        private void Awake()
        {
            // Auto-detect VisZoneData if not set
            if (visZoneData == null)
            {
                visZoneData = GetComponent<VisZoneData>();
            }

            // Build dictionary for fast section lookup
            BuildSectionDictionary();

            // Initially hide all sections
            foreach (var section in zoneSections)
            {
                section.Hide();
            }
        }

        // Removed Start() - VisZoneSensor now detects the initial zone when player spawns

        /// <summary>
        /// Build fast lookup dictionary for zone sections
        /// </summary>
        private void BuildSectionDictionary()
        {
            zoneSectionDict.Clear();
            foreach (var section in zoneSections)
            {
                if (section != null && !string.IsNullOrEmpty(section.zoneName))
                {
                    zoneSectionDict[section.zoneName] = section;
                }
            }
        }

        /// <summary>
        /// Set the current zone (called by VisZoneSensor when player enters a zone)
        /// </summary>
        public void SetCurrentZone(string zoneName)
        {
            if (currentZone == zoneName)
                return; // Already in this zone

            currentZone = zoneName;
            UpdateVisibility();
        }

        /// <summary>
        /// Update visibility based on current zone
        /// Implements visTable[Z][0] pattern: show Z + all neighbors
        /// </summary>
        private void UpdateVisibility()
        {
            if (visZoneData == null || string.IsNullOrEmpty(currentZone))
            {
                Debug.LogWarning($"[VisZoneManager] Cannot update visibility: visZoneData={visZoneData != null}, currentZone={currentZone}");
                return;
            }

            // Get list of zones that should be visible from current zone (Z + visTable[Z][0] + visHelper[Z])
            List<string> visibleZones = visZoneData.GetVisibleZones(currentZone);

            int shownCount = 0;
            int hiddenCount = 0;

            // Show zones that should be visible (Z + neighbors)
            foreach (string zone in visibleZones)
            {
                if (zoneSectionDict.TryGetValue(zone, out VisZoneSection section))
                {
                    if (!section.IsVisible)
                    {
                        section.Show();
                        shownCount++;
                    }
                }
                else
                {
                    Debug.LogWarning($"[VisZoneManager] Zone '{zone}' in vis table but no section found!");
                }
            }

            // Hide zones that should NOT be visible
            foreach (var kvp in zoneSectionDict)
            {
                if (!visibleZones.Contains(kvp.Key))
                {
                    if (kvp.Value.IsVisible)
                    {
                        kvp.Value.Hide();
                        hiddenCount++;
                    }
                }
            }

            currentlyVisibleZones = visibleZones;

            // Only log when something actually changed
            if (shownCount > 0 || hiddenCount > 0)
            {
                Debug.Log($"[VisZoneManager] Entered zone '{currentZone}': {shownCount} shown, {hiddenCount} hidden, {visibleZones.Count} total visible");
            }
        }

        /// <summary>
        /// Get current zone name
        /// </summary>
        public string GetCurrentZone() => currentZone;

        /// <summary>
        /// Get list of currently visible zones
        /// </summary>
        public List<string> GetVisibleZones() => new List<string>(currentlyVisibleZones);

        /// <summary>
        /// Refresh all sections from hierarchy (useful after import or changes)
        /// </summary>
        [ContextMenu("Refresh Zone Sections")]
        public void RefreshZoneSections()
        {
            zoneSections.Clear();
            zoneSections.AddRange(GetComponentsInChildren<VisZoneSection>(true));
            BuildSectionDictionary();
            Debug.Log($"[VisZoneManager] Refreshed {zoneSections.Count} zone sections");
        }

        private void OnDrawGizmosSelected()
        {
            // Draw debug info in editor
            if (!string.IsNullOrEmpty(currentZone))
            {
                // Draw current zone name at scene origin
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(transform.position, $"Current Zone: {currentZone}");
                #endif
            }
        }
    }
}
