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

        [Tooltip("All zones player is currently inside (for overlap handling)")]
        [SerializeField]
        private List<string> currentPlayerZones = new List<string>();

        [Tooltip("Zones currently visible")]
        [SerializeField]
        private List<string> currentlyVisibleZones = new List<string>();

        private Dictionary<string, VisZoneSection> zoneSectionDict = new Dictionary<string, VisZoneSection>();
        private Dictionary<string, GameObject> objectUidDict = new Dictionary<string, GameObject>();
        private Dictionary<string, GameObject> namedStaticDict = new Dictionary<string, GameObject>();

        // Store original renderer states for Large objects and named statics (preserves character clothing, etc.)
        private Dictionary<Renderer, bool> objectRendererStates = new Dictionary<Renderer, bool>();

        // Cache of physically overlapping zones (generated at startup)
        // Maps ZoneName -> List of names of other zones that physically intersect/overlap it
        private Dictionary<string, List<string>> overlappingZoneMap = new Dictionary<string, List<string>>();

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
            
            // Build physical overlap map to ensure nested/overlapping zones are always visible together
            BuildOverlappingZoneMap();

            // Initially hide all sections
            foreach (var section in zoneSections)
            {
                section.Hide();
            }

            // NOTE: Large objects (tracked by UID) start VISIBLE by default
            // They will be hidden when entering zones that don't reference them
            // This matches POTCO behavior where Large objects are always visible unless explicitly hidden
        }

        /// <summary>
        /// Build a map of which zones physically overlap/intersect each other.
        /// This ensures that nested zones (e.g., a room inside a larger area) remain visible
        /// even if the VisTable data doesn't explicitly link them.
        /// </summary>
        private void BuildOverlappingZoneMap()
        {
            overlappingZoneMap.Clear();
            
            if (zoneSections.Count == 0) return;

            // O(N^2) check - fine for startup since N is usually small (<100)
            for (int i = 0; i < zoneSections.Count; i++)
            {
                var sectionA = zoneSections[i];
                if (sectionA == null || string.IsNullOrEmpty(sectionA.zoneName)) continue;

                // Ensure list exists
                if (!overlappingZoneMap.ContainsKey(sectionA.zoneName))
                {
                    overlappingZoneMap[sectionA.zoneName] = new List<string>();
                }

                Bounds boundsA = sectionA.zoneBounds;
                // If bounds are zero/empty, try to get from collider
                if (boundsA.size == Vector3.zero && sectionA.zoneCollider != null)
                {
                    boundsA = sectionA.zoneCollider.bounds;
                }

                for (int j = i + 1; j < zoneSections.Count; j++)
                {
                    var sectionB = zoneSections[j];
                    if (sectionB == null || string.IsNullOrEmpty(sectionB.zoneName)) continue;

                    Bounds boundsB = sectionB.zoneBounds;
                    if (boundsB.size == Vector3.zero && sectionB.zoneCollider != null)
                    {
                        boundsB = sectionB.zoneCollider.bounds;
                    }

                    // Check intersection
                    if (boundsA.Intersects(boundsB))
                    {
                        // Add bidirectional link
                        overlappingZoneMap[sectionA.zoneName].Add(sectionB.zoneName);
                        
                        if (!overlappingZoneMap.ContainsKey(sectionB.zoneName))
                        {
                            overlappingZoneMap[sectionB.zoneName] = new List<string>();
                        }
                        overlappingZoneMap[sectionB.zoneName].Add(sectionA.zoneName);
                        
                        // Debug.Log($"[VisZoneManager] Overlap detected: {sectionA.zoneName} <-> {sectionB.zoneName}");
                    }
                }
            }
            
            Debug.Log($"[VisZoneManager] Built overlap map for {overlappingZoneMap.Count} zones");
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
        /// Set all zones player is currently inside (handles overlapping zones)
        /// When in multiple zones, props are NOT hidden if ANY zone wants them visible
        /// </summary>
        public void SetCurrentZones(List<string> zoneNames)
        {
            currentPlayerZones = new List<string>(zoneNames);

            // Set primary zone as first in list
            if (zoneNames.Count > 0)
            {
                currentZone = zoneNames[0];
            }

            UpdateVisibilityForMultipleZones();
        }

        /// <summary>
        /// Update visibility for a specific zone (public for editor use)
        /// Implements full POTCO Vis Table algorithm:
        /// - Show/hide zone sections (visTable[Z][0])
        /// - Show/hide object UIDs (visTable[Z][1])
        /// - Show/hide named statics (visTable[Z][2])
        /// </summary>
        /// <param name="zoneName">Zone to update visibility for</param>
        /// <param name="originalStates">Optional: Dictionary to store original states (for editor preview restoration)</param>
        /// <param name="originalStaticStates">Optional: Dictionary to store original named static states (for editor preview restoration)</param>
        public void UpdateVisibilityForZone(string zoneName, Dictionary<VisZoneSection, bool> originalStates = null, Dictionary<GameObject, bool> originalStaticStates = null)
        {
            if (visZoneData == null || string.IsNullOrEmpty(zoneName))
            {
                Debug.LogWarning($"[VisZoneManager] Cannot update visibility: visZoneData={visZoneData != null}, zoneName={zoneName}");
                return;
            }

            // Get complete visibility set for zone
            VisZoneData.VisibilitySet visSet = visZoneData.GetCompleteVisibilitySet(zoneName);

            // Add physically overlapping zones (Nested/Intersecting zones)
            if (overlappingZoneMap.TryGetValue(zoneName, out List<string> overlaps))
            {
                foreach (string overlapZone in overlaps)
                {
                    if (!visSet.zones.Contains(overlapZone))
                    {
                        visSet.zones.Add(overlapZone);
                    }
                }
            }

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
                    // Save original state if dictionary provided
                    if (originalStates != null && !originalStates.ContainsKey(section))
                    {
                        originalStates[section] = section.gameObject.activeSelf;
                    }

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
                // Save original state if dictionary provided
                if (originalStates != null && !originalStates.ContainsKey(kvp.Value))
                {
                    originalStates[kvp.Value] = kvp.Value.gameObject.activeSelf;
                }

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
            // Large objects stay at root level and are controlled by UID
            // ============================================================

            // Force-show objects by UID (even if their parent section is hidden)
            foreach (string uid in visSet.objectUIDs)
            {
                if (objectUidDict.TryGetValue(uid, out GameObject obj))
                {
                    if (!IsObjectVisible(obj))
                    {
                        ShowObject(obj);
                        uidsShown++;
                    }
                }
            }

            // Hide objects whose UIDs are NOT in the current zone's visibility set
            // These are Large objects that shouldn't be visible from this zone
            foreach (var kvp in objectUidDict)
            {
                string uid = kvp.Key;
                GameObject obj = kvp.Value;

                // Skip if this UID should be visible
                if (visSet.objectUIDs.Contains(uid))
                    continue;

                // Check if this is actually a Large object
                POTCO.ObjectListInfo objInfo = obj.GetComponent<POTCO.ObjectListInfo>();
                if (objInfo == null || objInfo.visSize != "Large")
                    continue; // Not a Large object, skip

                // Check if this object is inside a zone section (should not be for Large objects)
                bool inZoneSection = IsObjectInZoneSection(obj);

                // Only hide Large objects (those NOT in zone sections)
                // Normal objects inside sections are controlled by section visibility
                if (!inZoneSection && IsObjectVisible(obj))
                {
                    HideObject(obj);
                    uidsHidden++;
                }
            }

            // ============================================================
            // PART 3: Show/Hide Named Statics (visTable[Z][2])
            // ============================================================

            // Show named statics that should be visible from this zone
            foreach (string staticName in visSet.namedStatics)
            {
                if (namedStaticDict.TryGetValue(staticName, out GameObject obj))
                {
                    // Save original state if dictionary provided
                    if (originalStaticStates != null && !originalStaticStates.ContainsKey(obj))
                    {
                        originalStaticStates[obj] = IsObjectVisible(obj);
                    }

                    if (!IsObjectVisible(obj))
                    {
                        ShowObject(obj);
                        staticsShown++;
                    }
                }
            }

            // Hide named statics that should NOT be visible
            foreach (var kvp in namedStaticDict)
            {
                // Save original state if dictionary provided
                if (originalStaticStates != null && !originalStaticStates.ContainsKey(kvp.Value))
                {
                    originalStaticStates[kvp.Value] = IsObjectVisible(kvp.Value);
                }

                if (!visSet.namedStatics.Contains(kvp.Key))
                {
                    // Check if this static is inside a zone section
                    bool inZoneSection = IsObjectInZoneSection(kvp.Value);

                    // Only hide if it's NOT in a zone section (independent statics)
                    if (!inZoneSection && IsObjectVisible(kvp.Value))
                    {
                        HideObject(kvp.Value);
                        staticsHidden++;
                    }
                }
            }

            // Update current state (for runtime use)
            if (zoneName == currentZone)
            {
                currentlyVisibleZones = visSet.zones;
            }

            // Log when something actually changed
            if (zonesShown > 0 || zonesHidden > 0 || uidsShown > 0 || uidsHidden > 0 || staticsShown > 0 || staticsHidden > 0)
            {
                Debug.Log($"[VisZoneManager] Zone '{zoneName}' visibility update:\n" +
                         $"  Zones: +{zonesShown} -{zonesHidden} ({visSet.zones.Count} total)\n" +
                         $"  UIDs:  +{uidsShown} -{uidsHidden} ({visSet.objectUIDs.Count} total)\n" +
                         $"  Statics: +{staticsShown} -{staticsHidden} ({visSet.namedStatics.Count} total)");
            }
        }

        /// <summary>
        /// Restore zone visibility to original states (for editor preview exit)
        /// </summary>
        /// <param name="originalStates">Original section states to restore</param>
        /// <param name="originalStaticStates">Original named static states to restore</param>
        public void RestoreVisibilityStates(Dictionary<VisZoneSection, bool> originalStates, Dictionary<GameObject, bool> originalStaticStates)
        {
            int sectionsRestored = 0;
            int staticsRestored = 0;

            // Restore sections
            foreach (var kvp in originalStates)
            {
                if (kvp.Key != null)
                {
                    if (kvp.Value)
                    {
                        kvp.Key.Show();
                    }
                    else
                    {
                        kvp.Key.Hide();
                    }
                    sectionsRestored++;
                }
            }

            // Restore named statics
            foreach (var kvp in originalStaticStates)
            {
                if (kvp.Key != null)
                {
                    if (kvp.Value)
                    {
                        ShowObject(kvp.Key);
                    }
                    else
                    {
                        HideObject(kvp.Key);
                    }
                    staticsRestored++;
                }
            }

            Debug.Log($"[VisZoneManager] Restored {sectionsRestored} sections and {staticsRestored} named statics to original state");
        }

        /// <summary>
        /// Update visibility based on current zone (private wrapper for runtime use)
        /// </summary>
        private void UpdateVisibility()
        {
            UpdateVisibilityForZone(currentZone);
        }

        /// <summary>
        /// Update visibility when player is in multiple zones
        /// Combines visibility from all zones - if ANY zone wants something visible, keep it visible
        /// </summary>
        private void UpdateVisibilityForMultipleZones()
        {
            if (visZoneData == null || currentPlayerZones.Count == 0)
            {
                Debug.LogWarning($"[VisZoneManager] Cannot update visibility: visZoneData={visZoneData != null}, zones={currentPlayerZones.Count}");
                return;
            }

            // If only in one zone, use standard logic
            if (currentPlayerZones.Count == 1)
            {
                UpdateVisibilityForZone(currentPlayerZones[0]);
                return;
            }

            // In multiple zones - combine visibility sets from all zones
            Debug.Log($"[VisZoneManager] Player in {currentPlayerZones.Count} zones: {string.Join(", ", currentPlayerZones)} - combining visibility");

            HashSet<string> combinedZones = new HashSet<string>();
            HashSet<string> combinedUIDs = new HashSet<string>();
            HashSet<string> combinedStatics = new HashSet<string>();

            // Collect visibility from ALL zones player is in (union)
            foreach (string zoneName in currentPlayerZones)
            {
                // 1. Get standard logical visibility (VisTable)
                VisZoneData.VisibilitySet visSet = visZoneData.GetCompleteVisibilitySet(zoneName);

                foreach (string zone in visSet.zones)
                    combinedZones.Add(zone);

                foreach (string uid in visSet.objectUIDs)
                    combinedUIDs.Add(uid);

                foreach (string staticName in visSet.namedStatics)
                    combinedStatics.Add(staticName);
                    
                // 2. Add physically overlapping zones (Nested/Intersecting zones)
                // This ensures that if Zone B is inside Zone A, being in Zone A allows seeing Zone B
                if (overlappingZoneMap.TryGetValue(zoneName, out List<string> overlaps))
                {
                    foreach (string overlapZone in overlaps)
                    {
                        combinedZones.Add(overlapZone);
                    }
                }
            }

            int zonesShown = 0, zonesHidden = 0;
            int uidsShown = 0, uidsHidden = 0;
            int staticsShown = 0, staticsHidden = 0;

            // ============================================================
            // PART 1: Show/Hide Zone Sections (combined from all zones)
            // ============================================================

            // Show zones that ANY zone wants visible
            foreach (string zone in combinedZones)
            {
                if (zoneSectionDict.TryGetValue(zone, out VisZoneSection section))
                {
                    if (!section.IsVisible)
                    {
                        section.Show();
                        zonesShown++;
                    }
                }
            }

            // Hide zones that NO zone wants visible
            foreach (var kvp in zoneSectionDict)
            {
                if (!combinedZones.Contains(kvp.Key))
                {
                    if (kvp.Value.IsVisible)
                    {
                        kvp.Value.Hide();
                        zonesHidden++;
                    }
                }
            }

            // ============================================================
            // PART 2: Show/Hide Object UIDs (combined from all zones)
            // Large objects: show if ANY zone wants them visible
            // ============================================================

            // Show objects whose UIDs are in ANY active zone
            foreach (string uid in combinedUIDs)
            {
                if (objectUidDict.TryGetValue(uid, out GameObject obj))
                {
                    if (!IsObjectVisible(obj))
                    {
                        ShowObject(obj);
                        uidsShown++;
                    }
                }
            }

            // Hide objects whose UIDs are NOT in ANY active zone
            foreach (var kvp in objectUidDict)
            {
                string uid = kvp.Key;
                GameObject obj = kvp.Value;

                // Skip if ANY zone wants this UID visible
                if (combinedUIDs.Contains(uid))
                    continue;

                // Check if this is actually a Large object
                POTCO.ObjectListInfo objInfo = obj.GetComponent<POTCO.ObjectListInfo>();
                if (objInfo == null || objInfo.visSize != "Large")
                    continue; // Not a Large object, skip

                // Check if this object is inside a zone section
                bool inZoneSection = IsObjectInZoneSection(obj);

                // Only hide Large objects (those NOT in zone sections)
                if (!inZoneSection && IsObjectVisible(obj))
                {
                    HideObject(obj);
                    uidsHidden++;
                }
            }

            // ============================================================
            // PART 3: Show/Hide Named Statics (combined from all zones)
            // ============================================================

            // Show statics that ANY zone wants visible
            foreach (string staticName in combinedStatics)
            {
                if (namedStaticDict.TryGetValue(staticName, out GameObject obj))
                {
                    if (!IsObjectVisible(obj))
                    {
                        ShowObject(obj);
                        staticsShown++;
                    }
                }
            }

            // Hide statics that NO zone wants visible
            foreach (var kvp in namedStaticDict)
            {
                if (!combinedStatics.Contains(kvp.Key))
                {
                    bool inZoneSection = IsObjectInZoneSection(kvp.Value);

                    if (!inZoneSection && IsObjectVisible(kvp.Value))
                    {
                        HideObject(kvp.Value);
                        staticsHidden++;
                    }
                }
            }

            // Update current state
            currentlyVisibleZones = new List<string>(combinedZones);

            // Log results
            Debug.Log($"[VisZoneManager] Multi-zone visibility update:\n" +
                     $"  Combined from {currentPlayerZones.Count} zones\n" +
                     $"  Zones: +{zonesShown} -{zonesHidden} ({combinedZones.Count} total visible)\n" +
                     $"  UIDs:  +{uidsShown} -{uidsHidden} ({combinedUIDs.Count} total)\n" +
                     $"  Statics: +{staticsShown} -{staticsHidden} ({combinedStatics.Count} total)");
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
        /// Hide object by disabling renderers (preserves component data unlike SetActive)
        /// Stores original renderer states to preserve character clothing, etc.
        /// </summary>
        private void HideObject(GameObject obj)
        {
            // Disable all renderers on this object and its children
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    // Store original state before disabling (only if not already stored)
                    if (!objectRendererStates.ContainsKey(renderer))
                    {
                        objectRendererStates[renderer] = renderer.enabled;
                    }

                    renderer.enabled = false;
                }
            }
        }

        /// <summary>
        /// Show object by restoring renderers to original state
        /// Preserves character clothing by restoring stored states instead of blindly enabling all
        /// </summary>
        private void ShowObject(GameObject obj)
        {
            // Restore all renderers on this object and its children to original state
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    // Restore original state if we have it stored, otherwise default to enabled
                    if (objectRendererStates.TryGetValue(renderer, out bool originalState))
                    {
                        renderer.enabled = originalState;
                    }
                    else
                    {
                        // No stored state - this renderer was probably always visible
                        renderer.enabled = true;
                    }
                }
            }
        }

        /// <summary>
        /// Check if object is visible (any renderer enabled)
        /// </summary>
        private bool IsObjectVisible(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null && renderer.enabled)
                {
                    return true;
                }
            }
            return false;
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

        /// <summary>
        /// Ensure all dictionaries are built (for editor use when Awake hasn't been called)
        /// </summary>
        public void EnsureDictionariesBuilt()
        {
            if (zoneSectionDict.Count == 0)
            {
                BuildSectionDictionary();
            }
            if (objectUidDict.Count == 0)
            {
                BuildObjectUidDictionary();
            }
            if (namedStaticDict.Count == 0)
            {
                BuildNamedStaticDictionary();
            }
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
