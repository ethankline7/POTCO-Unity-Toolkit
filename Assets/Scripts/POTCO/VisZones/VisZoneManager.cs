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
        private Dictionary<string, GameObject> objectUidDict = new Dictionary<string, GameObject>();
        private Dictionary<string, GameObject> namedStaticDict = new Dictionary<string, GameObject>();

        private void Awake()
        {
            // Auto-detect VisZoneData if not set
            if (visZoneData == null)
            {
                visZoneData = GetComponent<VisZoneData>();
            }

            // Build dictionaries for fast lookups
            BuildSectionDictionary();
            BuildObjectUidDictionary();
            BuildNamedStaticDictionary();

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
        /// Build fast lookup dictionary for objects by UID (visTable[Z][1])
        /// </summary>
        private void BuildObjectUidDictionary()
        {
            objectUidDict.Clear();

            // Find all ObjectListInfo components in the scene (they store object UIDs)
            POTCO.ObjectListInfo[] allObjects = FindObjectsByType<POTCO.ObjectListInfo>(FindObjectsSortMode.None);

            foreach (var obj in allObjects)
            {
                if (!string.IsNullOrEmpty(obj.objectId))
                {
                    objectUidDict[obj.objectId] = obj.gameObject;
                }
            }

            Debug.Log($"[VisZoneManager] Built UID dictionary with {objectUidDict.Count} objects");
        }

        /// <summary>
        /// Build fast lookup dictionary for named static chunks (visTable[Z][2])
        /// Data-driven approach: only index GameObjects whose names appear in the imported visTable
        /// </summary>
        private void BuildNamedStaticDictionary()
        {
            namedStaticDict.Clear();

            if (visZoneData == null)
            {
                Debug.LogWarning("[VisZoneManager] Cannot build named static dictionary: visZoneData is null");
                return;
            }

            // Step 1: Collect all unique named static names from imported world data
            HashSet<string> namedStaticsInData = new HashSet<string>();
            foreach (var entry in visZoneData.visTable)
            {
                foreach (string staticName in entry.fortVisZones)
                {
                    namedStaticsInData.Add(staticName);
                }
            }

            if (namedStaticsInData.Count == 0)
            {
                Debug.Log("[VisZoneManager] No named statics found in visTable data");
                return;
            }

            // Step 2: Find GameObjects in scene whose names match the imported data
            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            foreach (var obj in allObjects)
            {
                string name = obj.name;

                // Check if this GameObject's name is in the imported data
                if (namedStaticsInData.Contains(name))
                {
                    namedStaticDict[name] = obj;
                }
            }

            Debug.Log($"[VisZoneManager] Built named static dictionary: {namedStaticDict.Count}/{namedStaticsInData.Count} objects found in scene");

            // Warn if any named statics from data are missing in scene
            if (namedStaticDict.Count < namedStaticsInData.Count)
            {
                foreach (string staticName in namedStaticsInData)
                {
                    if (!namedStaticDict.ContainsKey(staticName))
                    {
                        Debug.LogWarning($"[VisZoneManager] Named static '{staticName}' in visTable but not found in scene!");
                    }
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
        /// Implements full POTCO Vis Table algorithm:
        /// - Show/hide zone sections (visTable[Z][0])
        /// - Show/hide object UIDs (visTable[Z][1])
        /// - Show/hide named statics (visTable[Z][2])
        /// </summary>
        private void UpdateVisibility()
        {
            if (visZoneData == null || string.IsNullOrEmpty(currentZone))
            {
                Debug.LogWarning($"[VisZoneManager] Cannot update visibility: visZoneData={visZoneData != null}, currentZone={currentZone}");
                return;
            }

            // Get complete visibility set for current zone
            VisZoneData.VisibilitySet visSet = visZoneData.GetCompleteVisibilitySet(currentZone);

            int zonesShown = 0, zonesHidden = 0;
            int uidsShown = 0, uidsHidden = 0;
            int staticsShown = 0, staticsHidden = 0;

            // ============================================================
            // PART 1: Show/Hide Zone Sections (visTable[Z][0])
            // ============================================================

            // Show zones that should be visible
            foreach (string zone in visSet.zones)
            {
                if (zoneSectionDict.TryGetValue(zone, out VisZoneSection section))
                {
                    if (!section.IsVisible)
                    {
                        section.Show();
                        zonesShown++;
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
                if (!visSet.zones.Contains(kvp.Key))
                {
                    if (kvp.Value.IsVisible)
                    {
                        kvp.Value.Hide();
                        zonesHidden++;
                    }
                }
            }

            // ============================================================
            // PART 2: Show/Hide Object UIDs (visTable[Z][1])
            // ============================================================
            // The UID list contains cross-zone objects that should be visible
            // even when their parent zone section is hidden. These are objects
            // that appear "across zone boundaries" (e.g., a pier that spans zones).
            // We ONLY force-show these objects; hiding is handled by section visibility.

            // Force-show objects by UID (even if their parent section is hidden)
            foreach (string uid in visSet.objectUIDs)
            {
                if (objectUidDict.TryGetValue(uid, out GameObject obj))
                {
                    if (!obj.activeSelf)
                    {
                        obj.SetActive(true);
                        uidsShown++;
                    }
                }
            }

            // NOTE: We don't hide objects by UID. Object visibility is primarily
            // controlled by zone sections. The UID list only ADDS visibility,
            // it doesn't REMOVE it. This matches POTCO behavior.

            // ============================================================
            // PART 3: Show/Hide Named Statics (visTable[Z][2])
            // ============================================================
            // Named statics are environmental pieces (rocks, barriers) that can be
            // shared across zones. Like UIDs, they might be inside zone sections.

            // Show named statics that should be visible from this zone
            foreach (string staticName in visSet.namedStatics)
            {
                if (namedStaticDict.TryGetValue(staticName, out GameObject obj))
                {
                    if (!obj.activeSelf)
                    {
                        obj.SetActive(true);
                        staticsShown++;
                    }
                }
            }

            // Hide named statics that should NOT be visible
            // Only hide if not inside a zone section (section visibility takes precedence)
            foreach (var kvp in namedStaticDict)
            {
                if (!visSet.namedStatics.Contains(kvp.Key))
                {
                    // Check if this static is inside a zone section
                    bool inZoneSection = IsObjectInZoneSection(kvp.Value);

                    // Only hide if it's NOT in a zone section (independent statics)
                    if (!inZoneSection && kvp.Value.activeSelf)
                    {
                        kvp.Value.SetActive(false);
                        staticsHidden++;
                    }
                }
            }

            currentlyVisibleZones = visSet.zones;

            // Log when something actually changed
            if (zonesShown > 0 || zonesHidden > 0 || uidsShown > 0 || uidsHidden > 0 || staticsShown > 0 || staticsHidden > 0)
            {
                Debug.Log($"[VisZoneManager] Zone '{currentZone}' visibility update:\n" +
                         $"  Zones: +{zonesShown} -{zonesHidden} ({visSet.zones.Count} total)\n" +
                         $"  UIDs:  +{uidsShown} -{uidsHidden} ({visSet.objectUIDs.Count} total)\n" +
                         $"  Statics: +{staticsShown} -{staticsHidden} ({visSet.namedStatics.Count} total)");
            }
        }

        /// <summary>
        /// Check if an object is inside a visible zone section
        /// </summary>
        private bool IsObjectInVisibleZone(GameObject obj)
        {
            Transform current = obj.transform;

            // Walk up the hierarchy to see if any parent is a visible zone section
            while (current != null)
            {
                VisZoneSection section = current.GetComponent<VisZoneSection>();
                if (section != null)
                {
                    return currentlyVisibleZones.Contains(section.zoneName);
                }

                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// Check if an object is inside any zone section (visible or not)
        /// </summary>
        private bool IsObjectInZoneSection(GameObject obj)
        {
            Transform current = obj.transform;

            // Walk up the hierarchy to see if any parent is a zone section
            while (current != null)
            {
                VisZoneSection section = current.GetComponent<VisZoneSection>();
                if (section != null)
                {
                    return true; // Found a zone section parent
                }

                current = current.parent;
            }

            return false; // Not inside any zone section
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
