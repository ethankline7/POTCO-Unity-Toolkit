using UnityEngine;
using UnityEditor;
using POTCO.VisZones;
using System.Collections.Generic;
using System.Linq;

namespace POTCO.Editor
{
    public static class VisZoneValidation
    {
        [MenuItem("POTCO/VisZones/Validate Scene Setup")]
        public static void ValidateSceneSetup()
        {
            Debug.Log("=== VisZone Validation Report ===");
            Debug.Log("");

            // Find VisZoneManager
            VisZoneManager manager = Object.FindFirstObjectByType<VisZoneManager>();
            if (manager == null)
            {
                Debug.LogError("❌ No VisZoneManager found in scene!");
                return;
            }

            Debug.Log($"✓ VisZoneManager found on: {manager.gameObject.name}");
            Debug.Log("");

            // Check VisZoneData
            if (manager.visZoneData == null)
            {
                Debug.LogError("❌ VisZoneManager.visZoneData is null!");
                return;
            }

            Debug.Log($"✓ VisZoneData found with {manager.visZoneData.visTable.Count} zones");
            Debug.Log("");

            // Check sections
            Debug.Log($"📦 VisZoneManager has {manager.zoneSections.Count} sections registered:");
            foreach (var section in manager.zoneSections)
            {
                if (section != null)
                {
                    int childCount = section.transform.childCount;
                    Debug.Log($"   - {section.zoneName}: {childCount} children, Active={section.gameObject.activeSelf}");
                }
            }
            Debug.Log("");

            // Find all objects NOT parented to sections
            ObjectListInfo[] allObjects = Object.FindObjectsByType<ObjectListInfo>(FindObjectsSortMode.None);

            List<ObjectListInfo> objectsWithVisZone = new List<ObjectListInfo>();
            List<ObjectListInfo> objectsWithoutVisZone = new List<ObjectListInfo>();
            List<ObjectListInfo> largeObjects = new List<ObjectListInfo>();

            foreach (var obj in allObjects)
            {
                if (!string.IsNullOrEmpty(obj.visSize) && obj.visSize == "Large")
                {
                    largeObjects.Add(obj);
                }
                else if (!string.IsNullOrEmpty(obj.visZone))
                {
                    objectsWithVisZone.Add(obj);
                }
                else
                {
                    objectsWithoutVisZone.Add(obj);
                }
            }

            Debug.Log($"📋 Object Distribution:");
            Debug.Log($"   Objects with VisZone assigned: {objectsWithVisZone.Count}");
            Debug.Log($"   Objects with VisSize='Large': {largeObjects.Count} (always visible)");
            Debug.Log($"   Objects with NO VisZone: {objectsWithoutVisZone.Count} (always visible!)");
            Debug.Log("");

            if (objectsWithoutVisZone.Count > 0)
            {
                Debug.LogWarning($"⚠️ WARNING: {objectsWithoutVisZone.Count} objects have NO VisZone assigned!");
                Debug.LogWarning("   These objects will remain ALWAYS VISIBLE because they're not parented to any section.");
                Debug.LogWarning("   First 10 objects without VisZone:");
                for (int i = 0; i < Mathf.Min(10, objectsWithoutVisZone.Count); i++)
                {
                    var obj = objectsWithoutVisZone[i];
                    bool isParentedToSection = IsParentedToSection(obj.transform, manager);
                    Debug.LogWarning($"      - {obj.gameObject.name} (VisSize='{obj.visSize}') Parented to section: {isParentedToSection}");
                }
            }

            Debug.Log("");

            // Check for objects parented to sections
            Debug.Log("🔍 Checking section membership:");
            foreach (var section in manager.zoneSections)
            {
                if (section != null)
                {
                    int childCount = section.transform.childCount;
                    if (childCount > 0)
                    {
                        Debug.Log($"   Section '{section.zoneName}': {childCount} objects");
                    }
                    else
                    {
                        Debug.LogWarning($"   ⚠️ Section '{section.zoneName}': NO OBJECTS (section is empty!)");
                    }
                }
            }

            Debug.Log("");

            // In play mode, check current visibility
            if (Application.isPlaying)
            {
                string currentZone = manager.GetCurrentZone();
                var visibleZones = manager.GetVisibleZones();

                Debug.Log("🎮 RUNTIME STATUS:");
                Debug.Log($"   Current Zone: {currentZone}");
                Debug.Log($"   Visible Zones ({visibleZones.Count}): {string.Join(", ", visibleZones)}");
                Debug.Log("");

                // Check which sections are actually active
                Debug.Log("📊 Section Active Status:");
                int activeCount = 0;
                int inactiveCount = 0;
                foreach (var section in manager.zoneSections)
                {
                    if (section != null)
                    {
                        bool isActive = section.gameObject.activeSelf;
                        bool shouldBeActive = visibleZones.Contains(section.zoneName);

                        string status = isActive ? "ACTIVE" : "INACTIVE";
                        string expected = shouldBeActive ? "should be ACTIVE" : "should be INACTIVE";

                        if (isActive != shouldBeActive)
                        {
                            Debug.LogError($"   ❌ {section.zoneName}: {status} but {expected}!");
                        }
                        else
                        {
                            Debug.Log($"   ✓ {section.zoneName}: {status} (correct)");
                        }

                        if (isActive) activeCount++;
                        else inactiveCount++;
                    }
                }
                Debug.Log($"   Summary: {activeCount} active, {inactiveCount} inactive");
            }
            else
            {
                Debug.Log("💡 Enter Play Mode to see runtime visibility status");
            }

            Debug.Log("");
            Debug.Log("============================");
        }

        private static bool IsParentedToSection(Transform obj, VisZoneManager manager)
        {
            Transform current = obj.parent;
            while (current != null)
            {
                foreach (var section in manager.zoneSections)
                {
                    if (section != null && section.transform == current)
                    {
                        return true;
                    }
                }
                current = current.parent;
            }
            return false;
        }
    }
}
