using System;
using System.Collections.Generic;
using UnityEngine;

namespace POTCO.ShipBuilder
{
    [Serializable]
    public class ShipConfiguration
    {
        public string baseHullName = "";
        public string baseHullLogicName = "";

        // Build options
        public bool generateCollisions = true;

        // Component mappings: locator name -> component name
        public Dictionary<string, string> masts = new Dictionary<string, string>();
        public Dictionary<string, string> cannonsBroadsideLeft = new Dictionary<string, string>();
        public Dictionary<string, string> cannonsBroadsideRight = new Dictionary<string, string>();
        public Dictionary<string, string> cannonsDeck = new Dictionary<string, string>();
        public Dictionary<string, string> bowsprits = new Dictionary<string, string>();

        // Auto-assigned components (no UI customization needed)
        public string wheel = "";
        public Dictionary<string, string> repairSpots = new Dictionary<string, string>();
        public Dictionary<string, string> rams = new Dictionary<string, string>();

        // Additional components (can be expanded)
        public Dictionary<string, string> customComponents = new Dictionary<string, string>();

        public void InitializeFromHull(ShipComponentDatabase database)
        {
            if (string.IsNullOrEmpty(baseHullName)) return;

            baseHullLogicName = baseHullName + "_logic";

            // Clear existing component mappings
            masts.Clear();
            cannonsBroadsideLeft.Clear();
            cannonsBroadsideRight.Clear();
            cannonsDeck.Clear();
            bowsprits.Clear();
            repairSpots.Clear();
            rams.Clear();
            customComponents.Clear();

            Debug.Log($"[Ship Builder] Initializing ship from hull: {baseHullName}");
            Debug.Log($"[Ship Builder] Looking for logic model: {baseHullLogicName}");

            // Load the logic model to find attachment points
            GameObject logicModel = database.LoadShipLogic(baseHullLogicName);
            if (logicModel == null)
            {
                Debug.LogError($"[Ship Builder] Failed to load logic model: {baseHullLogicName}");
                return;
            }

            Debug.Log($"[Ship Builder] Logic model loaded successfully: {logicModel.name}");
            Debug.Log($"[Ship Builder] Logic model hierarchy:");
            PrintHierarchy(logicModel.transform, 0);

            // Find all locators
            Transform locators = FindChildRecursive(logicModel.transform, "locators");
            if (locators == null)
            {
                Debug.LogWarning($"[Ship Builder] No 'locators' found in {baseHullLogicName}");
                UnityEngine.Object.DestroyImmediate(logicModel);
                return;
            }

            Debug.Log($"[Ship Builder] Found locators parent with {locators.childCount} children");

            // Determine ship type from hull name
            bool isSloop = baseHullName.Contains("_slp_");

            // Parse locators - need to go through group nodes first
            foreach (Transform group in locators)
            {
                string groupName = group.name;
                Debug.Log($"[Ship Builder] Processing group: {groupName} with {group.childCount} children");

                // Process each locator inside this group
                foreach (Transform locator in group)
                {
                    string locatorName = locator.name;
                    Debug.Log($"[Ship Builder]   - Found locator: {locatorName}");

                    if (locatorName.StartsWith("broadside_left_"))
                    {
                        cannonsBroadsideLeft[locatorName] = "pir_r_shp_can_broadside_plain";
                    }
                    else if (locatorName.StartsWith("broadside_right_"))
                    {
                        cannonsBroadsideRight[locatorName] = "pir_r_shp_can_broadside_plain";
                    }
                    else if (locatorName.StartsWith("deck_cannon_") || locatorName.StartsWith("cannon_"))
                    {
                        cannonsDeck[locatorName] = "pir_r_shp_can_deck_plain";
                    }
                    else if (locatorName.StartsWith("location_mainmast") || locatorName.Contains("mainmast"))
                    {
                        // Sloops use triangular sails, others use square
                        masts[locatorName] = isSloop ? "pir_r_shp_mst_main_tri" : "pir_r_shp_mst_main_square";
                    }
                    else if (locatorName.StartsWith("location_foremast") || locatorName.Contains("foremast"))
                    {
                        masts[locatorName] = isSloop ? "pir_r_shp_mst_fore_tri" : "pir_r_shp_mst_fore_multi";
                    }
                    else if (locatorName.StartsWith("location_aftmast") || locatorName.Contains("aftmast"))
                    {
                        masts[locatorName] = "pir_r_shp_mst_aft_tri";
                    }
                    else if (locatorName == "location_wheel" || locatorName == "wheel" || locatorName.Contains("wheel"))
                    {
                        wheel = "pir_m_shp_prt_wheel";
                    }
                    else if (locatorName == "location_bowsprit" || locatorName.Contains("bowsprit"))
                    {
                        bowsprits[locatorName] = "prow_angel_zero";
                    }
                    else if (locatorName.StartsWith("repair_spot_") || locatorName.Contains("repair"))
                    {
                        repairSpots[locatorName] = "repair_spot_wood";
                    }
                    else if (locatorName == "location_ram" || locatorName.Contains("ram"))
                    {
                        rams[locatorName] = "pir_m_shp_ram_spike";
                    }
                    else
                    {
                        // Store other locators in customComponents for future expansion
                        customComponents[locatorName] = "";
                    }
                }
            }

            Debug.Log($"[Ship Builder] Initialization complete:");
            Debug.Log($"  - Masts: {masts.Count}");
            Debug.Log($"  - Broadside Left: {cannonsBroadsideLeft.Count}");
            Debug.Log($"  - Broadside Right: {cannonsBroadsideRight.Count}");
            Debug.Log($"  - Deck Cannons: {cannonsDeck.Count}");
            Debug.Log($"  - Bowsprits: {bowsprits.Count}");
            Debug.Log($"  - Wheel: {wheel}");
            Debug.Log($"  - Repair Spots: {repairSpots.Count}");
            Debug.Log($"  - Rams: {rams.Count}");

            UnityEngine.Object.DestroyImmediate(logicModel);
        }

        private void PrintHierarchy(Transform t, int depth)
        {
            string indent = new string(' ', depth * 2);
            Debug.Log($"{indent}- {t.name}");
            foreach (Transform child in t)
            {
                if (depth < 3) // Limit depth to avoid spam
                    PrintHierarchy(child, depth + 1);
            }
        }

        private Transform FindChildRecursive(Transform parent, string childName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == childName)
                    return child;

                Transform found = FindChildRecursive(child, childName);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
