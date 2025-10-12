using UnityEngine;
using System.Collections.Generic;

namespace POTCO.VisZones
{
    /// <summary>
    /// Runtime visibility table data for VisZone system
    /// Stores which zones are visible from each zone
    /// </summary>
    [System.Serializable]
    public class VisZoneEntry
    {
        public string zoneName;
        public List<string> visibleZones = new List<string>();      // Zones visible from this zone
        public List<string> objectUids = new List<string>();        // Object UIDs in this zone
        public List<string> fortVisZones = new List<string>();      // Fort-specific vis zones (optional)
    }

    /// <summary>
    /// Component that stores the complete Vis Table for an area/island
    /// Attach to the root of an imported island scene
    /// </summary>
    public class VisZoneData : MonoBehaviour
    {
        [Tooltip("Name of the area/island this Vis Table belongs to")]
        public string areaName;

        [Tooltip("Complete visibility table for all zones in this area")]
        public List<VisZoneEntry> visTable = new List<VisZoneEntry>();

        /// <summary>
        /// Complete visibility information for a zone
        /// Matches POTCO Vis Table structure: (zones, objectUIDs, namedStatics)
        /// </summary>
        public struct VisibilitySet
        {
            public List<string> zones;          // All zones to show (Z + visTable[Z][0] + visHelper[Z])
            public List<string> objectUIDs;     // Object UIDs to show (visTable[Z][1])
            public List<string> namedStatics;   // Named statics to show (visTable[Z][2])
        }

        /// <summary>
        /// Get visible zones for a specific zone name
        /// Returns Z + visTable[Z][0] (forward neighbors) + visHelper[Z] (reverse neighbors)
        /// Implements bidirectional visibility: show zones that can see you AND zones you can see
        /// </summary>
        public List<string> GetVisibleZones(string zoneName)
        {
            VisZoneEntry entry = visTable.Find(e => e.zoneName == zoneName);
            if (entry != null)
            {
                // Start with the zone itself plus all zones visible from it (forward visibility)
                List<string> result = new List<string> { zoneName };
                result.AddRange(entry.visibleZones);

                int forwardCount = entry.visibleZones.Count;

                // Add reverse visibility: zones that can see THIS zone (visHelper concept)
                // If zone B lists A as a neighbor, then when player is in A, B should also be visible
                int reverseCount = 0;
                foreach (var otherEntry in visTable)
                {
                    // Skip if this is the current zone or already in the visible list
                    if (otherEntry.zoneName == zoneName || result.Contains(otherEntry.zoneName))
                        continue;

                    // Check if this other zone can see our current zone
                    if (otherEntry.visibleZones.Contains(zoneName))
                    {
                        result.Add(otherEntry.zoneName);
                        reverseCount++;
                    }
                }

                Debug.Log($"[VisZoneData] Zone '{zoneName}' visibility: {forwardCount} forward neighbors + {reverseCount} reverse neighbors = {result.Count} total");

                return result;
            }

            Debug.LogWarning($"[VisZoneData] Zone '{zoneName}' not found in Vis Table! Using zone only.");
            return new List<string> { zoneName }; // Return at least the zone itself
        }

        /// <summary>
        /// Get complete visibility set for a zone: zones + object UIDs + named statics
        /// Implements full POTCO Vis Table pattern: visTable[Z] = (zones, objectUIDs, namedStatics)
        /// </summary>
        public VisibilitySet GetCompleteVisibilitySet(string zoneName)
        {
            VisibilitySet result = new VisibilitySet
            {
                zones = new List<string>(),
                objectUIDs = new List<string>(),
                namedStatics = new List<string>()
            };

            VisZoneEntry entry = visTable.Find(e => e.zoneName == zoneName);
            if (entry != null)
            {
                // Get all visible zones (same as GetVisibleZones)
                result.zones = GetVisibleZones(zoneName);

                // Get object UIDs for this zone (visTable[Z][1])
                result.objectUIDs = new List<string>(entry.objectUids);

                // Get named statics for this zone (visTable[Z][2])
                result.namedStatics = new List<string>(entry.fortVisZones);

                Debug.Log($"[VisZoneData] Complete visibility for '{zoneName}': {result.zones.Count} zones, {result.objectUIDs.Count} UIDs, {result.namedStatics.Count} statics");
            }
            else
            {
                Debug.LogWarning($"[VisZoneData] Zone '{zoneName}' not found in Vis Table!");
                result.zones.Add(zoneName); // At least include the zone itself
            }

            return result;
        }

        /// <summary>
        /// Check if a zone exists in the vis table
        /// </summary>
        public bool HasZone(string zoneName)
        {
            return visTable.Exists(e => e.zoneName == zoneName);
        }

        /// <summary>
        /// Get all zone names in this area
        /// </summary>
        public List<string> GetAllZoneNames()
        {
            List<string> zoneNames = new List<string>();
            foreach (var entry in visTable)
            {
                zoneNames.Add(entry.zoneName);
            }
            return zoneNames;
        }
    }
}
