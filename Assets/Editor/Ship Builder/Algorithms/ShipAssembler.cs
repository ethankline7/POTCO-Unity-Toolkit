using UnityEngine;
using System.Collections.Generic;

namespace POTCO.ShipBuilder
{
    public class ShipAssembler
    {
        private ShipComponentDatabase database;
        private ShipConfiguration config;

        public ShipAssembler(ShipComponentDatabase componentDatabase)
        {
            database = componentDatabase;
        }

        public GameObject BuildShip(ShipConfiguration configuration, string shipName)
        {
            config = configuration;

            if (string.IsNullOrEmpty(config.baseHullName))
            {
                Debug.LogError("No base hull selected!");
                return null;
            }

            // Create root ship object
            GameObject shipRoot = new GameObject(shipName);

            // Load and attach base hull
            GameObject hullObject = database.LoadShipHull(config.baseHullName);
            if (hullObject == null)
            {
                Debug.LogError($"Failed to load hull: {config.baseHullName}");
                Object.DestroyImmediate(shipRoot);
                return null;
            }

            hullObject.name = "Hull";
            hullObject.transform.SetParent(shipRoot.transform);

            // Add collisions to hull if requested
            if (config.generateCollisions)
            {
                AddMeshCollidersRecursive(hullObject.transform);
            }

            // Load logic model to get attachment point transforms
            GameObject logicObject = database.LoadShipLogic(config.baseHullLogicName);
            if (logicObject == null)
            {
                Debug.LogError($"Failed to load logic model: {config.baseHullLogicName}");
                Object.DestroyImmediate(shipRoot);
                return null;
            }

            Transform locatorsParent = FindChildRecursive(logicObject.transform, "locators");
            if (locatorsParent == null)
            {
                Debug.LogError($"No 'locators' group found in logic model: {config.baseHullLogicName}");
                Object.DestroyImmediate(logicObject);
                Object.DestroyImmediate(shipRoot);
                return null;
            }

            // Build locator dictionary for quick lookup
            // Locators are nested in groups, so we need to go through each group
            Dictionary<string, Transform> locators = new Dictionary<string, Transform>();
            foreach (Transform group in locatorsParent)
            {
                // Each group contains the actual locator transforms
                foreach (Transform locator in group)
                {
                    locators[locator.name] = locator;
                }
            }

            // Attach components to their locators and track mast locator names
            Dictionary<GameObject, string> mastToLocatorMap = new Dictionary<GameObject, string>();
            GameObject mastsParent = AttachComponentsWithTracking(shipRoot.transform, config.masts, locators, "Masts", mastToLocatorMap);
            AttachComponents(shipRoot.transform, config.cannonsBroadsideLeft, locators, "Cannons_Broadside_Left");
            AttachComponents(shipRoot.transform, config.cannonsBroadsideRight, locators, "Cannons_Broadside_Right");
            AttachComponents(shipRoot.transform, config.cannonsDeck, locators, "Cannons_Deck");
            AttachComponents(shipRoot.transform, config.bowsprits, locators, "Bowsprits");

            // Position rope ladder joints on masts using the mapping
            if (mastsParent != null)
            {
                PositionRopeLadders(mastsParent.transform, locators, mastToLocatorMap);
            }

            // Attach auto-assigned components (wheel, repair spots, rams)
            AttachComponents(shipRoot.transform, config.repairSpots, locators, "Repair_Spots");
            AttachComponents(shipRoot.transform, config.rams, locators, "Rams");

            // Attach wheel (single component)
            if (!string.IsNullOrEmpty(config.wheel) && config.wheel != "<None>")
            {
                Transform wheelLocator = null;
                if (locators.ContainsKey("location_wheel"))
                    wheelLocator = locators["location_wheel"];
                else if (locators.ContainsKey("wheel"))
                    wheelLocator = locators["wheel"];

                if (wheelLocator != null)
                {
                    GameObject wheelComponent = database.LoadComponent(config.wheel);
                    if (wheelComponent != null)
                    {
                        AttachComponentAtLocator(wheelComponent, wheelLocator, shipRoot.transform, "Wheel");
                    }
                }
            }

            // Clean up logic object (we only needed it for the transforms)
            Object.DestroyImmediate(logicObject);

            Debug.Log($"Ship assembled successfully: {shipName}");
            return shipRoot;
        }

        private GameObject AttachComponents(Transform shipRoot, Dictionary<string, string> componentMap,
            Dictionary<string, Transform> locators, string categoryName)
        {
            return AttachComponentsWithTracking(shipRoot, componentMap, locators, categoryName, null);
        }

        private GameObject AttachComponentsWithTracking(Transform shipRoot, Dictionary<string, string> componentMap,
            Dictionary<string, Transform> locators, string categoryName, Dictionary<GameObject, string> componentToLocatorMap)
        {
            if (componentMap == null || componentMap.Count == 0) return null;

            // Create category parent
            GameObject categoryParent = new GameObject(categoryName);
            categoryParent.transform.SetParent(shipRoot);

            foreach (var kvp in componentMap)
            {
                string locatorName = kvp.Key;
                string componentName = kvp.Value;

                if (string.IsNullOrEmpty(componentName) || componentName == "<None>")
                    continue;

                if (!locators.ContainsKey(locatorName))
                {
                    Debug.LogWarning($"Locator not found: {locatorName}");
                    continue;
                }

                GameObject component = database.LoadComponent(componentName);
                if (component == null)
                {
                    Debug.LogWarning($"Failed to load component: {componentName}");
                    continue;
                }

                AttachComponentAtLocator(component, locators[locatorName], categoryParent.transform, locatorName);

                // Add collisions if requested
                if (config.generateCollisions)
                {
                    AddMeshCollidersRecursive(component.transform);
                }

                // Track which locator this component was attached to
                if (componentToLocatorMap != null)
                {
                    componentToLocatorMap[component] = locatorName;
                }
            }

            return categoryParent;
        }

        private void AttachComponentAtLocator(GameObject component, Transform locator, Transform parent, string componentName)
        {
            component.name = componentName;
            component.transform.SetParent(parent);

            // Copy transform from locator
            component.transform.position = locator.position;
            component.transform.rotation = locator.rotation;
            component.transform.localScale = locator.localScale;
        }

        private void AddMeshCollidersRecursive(Transform obj)
        {
            // Check if this object has a MeshFilter
            MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                // Only add if it doesn't already have a collider
                if (obj.GetComponent<MeshCollider>() == null)
                {
                    MeshCollider collider = obj.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = meshFilter.sharedMesh;
                }
            }

            // Recurse through children
            foreach (Transform child in obj)
            {
                AddMeshCollidersRecursive(child);
            }
        }

        private void PositionRopeLadders(Transform mastsParent, Dictionary<string, Transform> locators, Dictionary<GameObject, string> mastToLocatorMap)
        {
            // Search through all masts for ladder joints
            foreach (Transform mastTransform in mastsParent)
            {
                GameObject mastObj = mastTransform.gameObject;

                // Get the locator name this mast was attached to
                if (!mastToLocatorMap.ContainsKey(mastObj))
                    continue;

                string mastLocatorName = mastToLocatorMap[mastObj];

                // Extract the index from the mast locator name (e.g., "location_mainmast_0" -> "0")
                string indexSuffix = "";
                if (mastLocatorName.Contains("_"))
                {
                    string[] parts = mastLocatorName.Split('_');
                    if (parts.Length > 0)
                    {
                        indexSuffix = parts[parts.Length - 1];
                    }
                }

                // Find ladder joints in this mast
                Transform leftLadder = FindChildRecursive(mastTransform, "def_ladder_0_left");
                Transform rightLadder = FindChildRecursive(mastTransform, "def_ladder_0_right");

                // Position left ladder if found
                if (leftLadder != null)
                {
                    string leftLocatorName = $"location_ropeLadder_left_{indexSuffix}";
                    if (locators.ContainsKey(leftLocatorName))
                    {
                        Transform leftLocator = locators[leftLocatorName];
                        leftLadder.position = leftLocator.position;
                        leftLadder.rotation = leftLocator.rotation;
                        Debug.Log($"Positioned left rope ladder on {mastTransform.name} using {leftLocatorName}");
                    }
                }

                // Position right ladder if found
                if (rightLadder != null)
                {
                    string rightLocatorName = $"location_ropeLadder_right_{indexSuffix}";
                    if (locators.ContainsKey(rightLocatorName))
                    {
                        Transform rightLocator = locators[rightLocatorName];
                        rightLadder.position = rightLocator.position;
                        rightLadder.rotation = rightLocator.rotation;
                        Debug.Log($"Positioned right rope ladder on {mastTransform.name} using {rightLocatorName}");
                    }
                }
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
