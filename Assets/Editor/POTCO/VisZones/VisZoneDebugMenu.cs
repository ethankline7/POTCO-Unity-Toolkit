using UnityEngine;
using UnityEditor;
using POTCO.VisZones;

namespace POTCO.Editor
{
    public static class VisZoneDebugMenu
    {
        [MenuItem("POTCO/VisZones/Debug Scene Info")]
        public static void DebugSceneInfo()
        {
            Debug.Log("=== VisZone Debug Info ===");

            // Check for VisZoneManager
            VisZoneManager manager = Object.FindFirstObjectByType<VisZoneManager>();
            if (manager != null)
            {
                Debug.Log($"✅ VisZoneManager found on: {manager.gameObject.name}");
                Debug.Log($"   Sections: {manager.zoneSections.Count}");
                if (manager.visZoneData != null)
                {
                    Debug.Log($"   Vis Table Zones: {manager.visZoneData.visTable.Count}");
                    Debug.Log("");
                    Debug.Log("📋 Vis Table Structure (visTable[Z][0] = neighbors):");
                    foreach (var zone in manager.visZoneData.visTable)
                    {
                        string neighbors = zone.visibleZones.Count > 0
                            ? string.Join(", ", zone.visibleZones)
                            : "none";
                        Debug.Log($"   '{zone.zoneName}' → [{neighbors}]");
                        Debug.Log($"      Total visible when in {zone.zoneName}: {zone.visibleZones.Count + 1} zones (self + neighbors)");
                    }
                }
                else
                {
                    Debug.LogWarning("   ⚠️ VisZoneData is null!");
                }
            }
            else
            {
                Debug.LogWarning("❌ No VisZoneManager found in scene!");
                Debug.LogWarning("   Make sure you imported with 'Enable VisZones' checked");
            }

            // Check for VisZoneSections
            VisZoneSection[] sections = Object.FindObjectsByType<VisZoneSection>(FindObjectsSortMode.None);
            Debug.Log($"📦 Found {sections.Length} VisZone sections in scene");
            foreach (var section in sections)
            {
                Debug.Log($"   - {section.zoneName} at {section.gameObject.name}");
            }

            // Check for collision zones
            Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            int collisionZoneCount = 0;
            foreach (var t in allTransforms)
            {
                if (t.name.StartsWith("collision_zone_"))
                {
                    collisionZoneCount++;
                    Debug.Log($"   🔷 Collision zone: {t.name}");
                }
            }
            Debug.Log($"🔷 Found {collisionZoneCount} collision_zone_* transforms in scene");

            // Check for objects with VisZone assigned
            ObjectListInfo[] allObjects = Object.FindObjectsByType<ObjectListInfo>(FindObjectsSortMode.None);
            int objectsWithVisZone = 0;
            foreach (var obj in allObjects)
            {
                if (!string.IsNullOrEmpty(obj.visZone))
                {
                    objectsWithVisZone++;
                }
            }
            Debug.Log($"📋 Found {objectsWithVisZone} objects with VisZone assigned (out of {allObjects.Length} total)");

            Debug.Log("=========================");
        }

        [MenuItem("POTCO/VisZones/List All VisZone Assignments")]
        public static void ListVisZoneAssignments()
        {
            Debug.Log("=== All VisZone Assignments ===");

            ObjectListInfo[] allObjects = Object.FindObjectsByType<ObjectListInfo>(FindObjectsSortMode.None);
            int count = 0;

            foreach (var obj in allObjects)
            {
                if (!string.IsNullOrEmpty(obj.visZone))
                {
                    Debug.Log($"   {obj.gameObject.name} -> Zone: '{obj.visZone}', VisSize: '{obj.visSize}'");
                    count++;
                }
            }

            Debug.Log($"Total: {count} objects with VisZone assignments");
            Debug.Log("===============================");
        }

        [MenuItem("POTCO/VisZones/Show Vis Table Structure")]
        public static void ShowVisTableStructure()
        {
            Debug.Log("=== Vis Table Structure (What SHOULD be visible) ===");
            Debug.Log("");

            VisZoneManager manager = Object.FindFirstObjectByType<VisZoneManager>();
            if (manager == null || manager.visZoneData == null)
            {
                Debug.LogError("❌ No VisZoneManager or VisZoneData found!");
                return;
            }

            Debug.Log($"📋 Vis Table for: {manager.visZoneData.areaName}");
            Debug.Log($"Total zones: {manager.visZoneData.visTable.Count}");
            Debug.Log("");

            foreach (var entry in manager.visZoneData.visTable)
            {
                Debug.Log($"🔹 When in zone '{entry.zoneName}':");
                Debug.Log($"   Total visible: {entry.visibleZones.Count + 1} zones (self + {entry.visibleZones.Count} neighbors)");

                if (entry.visibleZones.Count > 0)
                {
                    Debug.Log($"   Neighbors: {string.Join(", ", entry.visibleZones)}");
                }
                else
                {
                    Debug.Log($"   Neighbors: (none - only this zone visible)");
                }

                Debug.Log("");
            }

            Debug.Log("💡 VISIBILITY RULES:");
            Debug.Log("   When in zone Z, you see:");
            Debug.Log("   1. Zone Z (yourself)");
            Debug.Log("   2. Forward visibility: Zones Z can see (visTable[Z])");
            Debug.Log("   3. Reverse visibility: Zones that can see Z (visHelper)");
            Debug.Log("");
            Debug.Log("   This bidirectional system prevents zones from disappearing");
            Debug.Log("   when moving between neighboring zones.");
            Debug.Log("");
            Debug.Log("==============================");
        }

    }
}
