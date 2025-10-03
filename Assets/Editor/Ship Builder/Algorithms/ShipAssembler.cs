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

            // Apply style textures if a preset was selected
            if (config.styleID > 0)
            {
                ApplyHullTexture(shipRoot, config.styleID);
                ApplyStyleTextures(shipRoot, config.styleID);
            }

            // Apply logo overlay if specified
            Debug.Log($"Logo ID from config: {config.logoID}");
            if (config.logoID > 0)
            {
                ApplySailLogo(shipRoot, config.logoID);
            }
            else
            {
                Debug.LogWarning("No logo selected (logoID <= 0), skipping logo application");
            }

            // Add ship controller if requested
            if (config.addShipController)
            {
                var controller = shipRoot.AddComponent<POTCO.ShipController>();
                Debug.Log("Added ShipController component");
            }

            Debug.Log($"Ship assembled successfully: {shipName}");

            // Register this ship for automatic scene editing
            SceneEditing.ShipComponentVisualizer.RegisterBuiltShip(shipRoot);

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

                // If this is a mast, add MastTypeInfo component and set double-sided materials
                if (categoryName == "Masts" && componentName.Contains("pir_r_shp_mst_") && !componentName.EndsWith("_skeleton"))
                {
                    // Extract mast type from component name
                    // e.g., "pir_r_shp_mst_main_tri" -> "main_tri"
                    string mastType = ExtractMastType(componentName);
                    var mastInfo = component.AddComponent<POTCO.MastTypeInfo>();
                    mastInfo.mastType = mastType;
                    Debug.Log($"Added MastTypeInfo to {locatorName}: {mastType}");

                    // Set all materials to double-sided for mast visibility
                    SetMaterialsDoubleSided(component);
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

        private string ExtractMastType(string componentName)
        {
            // Extract mast type from component name
            // e.g., "pir_r_shp_mst_main_tri" -> "main_tri"
            if (componentName.Contains("pir_r_shp_mst_"))
            {
                int startIndex = componentName.IndexOf("pir_r_shp_mst_") + "pir_r_shp_mst_".Length;
                string remainder = componentName.Substring(startIndex);

                // Split and take first two parts (e.g., "main_tri")
                string[] parts = remainder.Split('_');
                if (parts.Length >= 2)
                {
                    return parts[0] + "_" + parts[1];
                }

                return remainder; // Fallback to whole remainder
            }

            return "unknown";
        }

        private void SetMaterialsDoubleSided(GameObject obj)
        {
            // Get all renderers (both MeshRenderer and SkinnedMeshRenderer)
            var meshRenderers = obj.GetComponentsInChildren<MeshRenderer>(true);
            var skinnedRenderers = obj.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            // Process MeshRenderers
            foreach (var renderer in meshRenderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null)
                    {
                        material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    }
                }
            }

            // Process SkinnedMeshRenderers
            foreach (var renderer in skinnedRenderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null)
                    {
                        material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    }
                }
            }

            Debug.Log($"Set all materials to double-sided for {obj.name}");
        }

        private void ApplyStyleTextures(GameObject ship, int styleID)
        {
            string textureName = database.GetSailTexture(styleID);
            if (string.IsNullOrEmpty(textureName))
            {
                Debug.LogWarning($"No sail texture found for style ID {styleID}");
                return;
            }

            // Try loading from multiple phase folders
            Texture2D texture = null;
            string[] searchPaths = new string[]
            {
                $"phase_2/maps/{textureName}",
                $"phase_3/maps/{textureName}",
                $"phase_4/maps/{textureName}",
                $"phase_5/maps/{textureName}",
                $"phase_6/maps/{textureName}",
                $"phase_3/models/shipparts/{textureName}",
                $"phase_4/models/shipparts/{textureName}",
                $"phase_3/models/textureCards/{textureName}",
                $"phase_4/models/textureCards/{textureName}"
            };

            foreach (string path in searchPaths)
            {
                texture = Resources.Load<Texture2D>(path);
                if (texture != null)
                {
                    Debug.Log($"Loaded style texture from: {path}");
                    break;
                }
            }

            if (texture == null)
            {
                Debug.LogWarning($"Could not find texture: {textureName} in Resources folders");
                return;
            }

            // Find all renderers in the ship
            var allRenderers = ship.GetComponentsInChildren<Renderer>(true);
            int texturesApplied = 0;

            foreach (var renderer in allRenderers)
            {
                // Check if this renderer is under a "sails" group (lowercase)
                Transform current = renderer.transform;
                bool isUnderSails = false;
                while (current != null)
                {
                    if (current.name == "sails")
                    {
                        isUnderSails = true;
                        break;
                    }
                    current = current.parent;
                }

                if (!isUnderSails) continue; // Only apply to objects under "sails" group

                Debug.Log($"⛵ Found sail object: {renderer.gameObject.name}");

                Material[] sharedMats = renderer.sharedMaterials;
                Material[] newMats = new Material[sharedMats.Length];
                bool anyChanged = false;

                for (int i = 0; i < sharedMats.Length; i++)
                {
                    if (sharedMats[i] == null)
                    {
                        newMats[i] = null;
                        continue;
                    }

                    Debug.Log($"  Old material: {sharedMats[i].name}, texture: {(sharedMats[i].mainTexture != null ? sharedMats[i].mainTexture.name : "null")}");

                    // Copy the existing material (preserves shader, UVs, and all settings)
                    Material newMat = new Material(sharedMats[i]);
                    newMat.name = sharedMats[i].name + "_styled";

                    // Overlay the style texture using blend texture
                    if (newMat.HasProperty("_BlendTex"))
                    {
                        newMat.SetTexture("_BlendTex", texture);
                    }

                    Debug.Log($"  New material: {newMat.name}, base texture: {(newMat.mainTexture != null ? newMat.mainTexture.name : "null")}, blend texture: {texture.name}");

                    newMats[i] = newMat;
                    texturesApplied++;
                    anyChanged = true;
                }

                // Update renderer materials if we modified any
                if (anyChanged)
                {
                    renderer.sharedMaterials = newMats;
                    Debug.Log($"  ✅ Updated renderer materials");
                }
            }

            Debug.Log($"Applied style texture '{textureName}' to {texturesApplied} sail materials (Style ID: {styleID})");
        }

        private void ApplyHullTexture(GameObject ship, int styleID)
        {
            string textureName = database.GetStyleTexture(styleID);
            if (string.IsNullOrEmpty(textureName))
            {
                Debug.LogWarning($"No hull texture found for style ID {styleID}");
                return;
            }

            // Try loading from multiple phase folders
            Texture2D texture = null;
            string[] searchPaths = new string[]
            {
                $"phase_2/maps/{textureName}",
                $"phase_3/maps/{textureName}",
                $"phase_4/maps/{textureName}",
                $"phase_5/maps/{textureName}",
                $"phase_6/maps/{textureName}",
                $"phase_3/models/shipparts/{textureName}",
                $"phase_4/models/shipparts/{textureName}",
                $"phase_3/models/textureCards/{textureName}",
                $"phase_4/models/textureCards/{textureName}"
            };

            foreach (string path in searchPaths)
            {
                texture = Resources.Load<Texture2D>(path);
                if (texture != null)
                {
                    Debug.Log($"Loaded hull texture from: {path}");
                    break;
                }
            }

            if (texture == null)
            {
                Debug.LogWarning($"Could not find hull texture: {textureName} in Resources folders");
                return;
            }

            // Find Hull object
            Transform hullTransform = ship.transform.Find("Hull");
            if (hullTransform == null)
            {
                Debug.LogWarning("Hull object not found in ship");
                return;
            }

            // Apply to all renderers in Hull (excluding sails)
            var allRenderers = hullTransform.GetComponentsInChildren<Renderer>(true);
            int texturesApplied = 0;

            foreach (var renderer in allRenderers)
            {
                // Skip if this renderer is under a "sails" or "transparent" group
                Transform current = renderer.transform;
                bool isUnderSails = false;
                bool isUnderTransparent = false;
                while (current != null && current != hullTransform)
                {
                    if (current.name == "sails")
                    {
                        isUnderSails = true;
                        break;
                    }
                    if (current.name == "transparent")
                    {
                        isUnderTransparent = true;
                        break;
                    }
                    current = current.parent;
                }

                if (isUnderSails || isUnderTransparent) continue; // Skip sail objects and transparent objects

                Material[] sharedMats = renderer.sharedMaterials;
                Material[] newMats = new Material[sharedMats.Length];
                bool anyChanged = false;

                for (int i = 0; i < sharedMats.Length; i++)
                {
                    if (sharedMats[i] == null)
                    {
                        newMats[i] = null;
                        continue;
                    }

                    // Copy the existing material
                    Material newMat = new Material(sharedMats[i]);
                    newMat.name = sharedMats[i].name + "_hull_styled";
                    newMat.mainTexture = texture;

                    newMats[i] = newMat;
                    texturesApplied++;
                    anyChanged = true;
                }

                if (anyChanged)
                {
                    renderer.sharedMaterials = newMats;
                }
            }

            Debug.Log($"Applied hull texture '{textureName}' to {texturesApplied} hull materials (Style ID: {styleID})");
        }

        private void ApplySailLogo(GameObject ship, int logoID)
        {
            string logoTextureName = database.GetLogoTexture(logoID);
            if (string.IsNullOrEmpty(logoTextureName))
            {
                Debug.LogWarning($"No logo texture found for logo ID {logoID}");
                return;
            }

            // Try loading from multiple phase folders
            Texture2D logoTexture = null;
            string[] searchPaths = new string[]
            {
                $"phase_2/maps/{logoTextureName}",
                $"phase_3/maps/{logoTextureName}",
                $"phase_4/maps/{logoTextureName}",
                $"phase_5/maps/{logoTextureName}",
                $"phase_6/maps/{logoTextureName}",
                $"phase_3/models/shipparts/{logoTextureName}",
                $"phase_4/models/shipparts/{logoTextureName}",
                $"phase_3/models/textureCards/{logoTextureName}",
                $"phase_4/models/textureCards/{logoTextureName}"
            };

            foreach (string path in searchPaths)
            {
                logoTexture = Resources.Load<Texture2D>(path);
                if (logoTexture != null)
                {
                    Debug.Log($"Loaded logo texture from: {path}");
                    break;
                }
            }

            if (logoTexture == null)
            {
                Debug.LogWarning($"Could not find logo texture: {logoTextureName} in Resources folders");
                return;
            }

            // Find all renderers in the ship
            var allRenderers = ship.GetComponentsInChildren<Renderer>(true);
            int logosApplied = 0;

            foreach (var renderer in allRenderers)
            {
                // Check if this renderer is under a "sails" group (lowercase)
                Transform current = renderer.transform;
                bool isUnderSails = false;
                string mastType = null;

                while (current != null)
                {
                    if (current.name == "sails")
                    {
                        isUnderSails = true;
                        // Get mast type from parent hierarchy (e.g., "main_tri", "main_square", "fore_multi")
                        if (current.parent != null && current.parent.parent != null)
                        {
                            mastType = current.parent.parent.name;
                        }
                        break;
                    }
                    current = current.parent;
                }

                if (!isUnderSails) continue; // Only apply to objects under "sails" group

                // Determine which sail index should get the logo based on mast type
                int targetSailIndex = 0; // Default to sail_0
                if (mastType != null && mastType.Contains("fore_multi"))
                {
                    targetSailIndex = 1; // fore_multi uses sail_1
                }

                // Check if this is the correct sail for logo application
                string sailName = renderer.gameObject.name;
                if (!sailName.StartsWith("sail_"))
                {
                    continue; // Skip non-sail objects
                }

                // Extract sail index from name (e.g., "sail_0" -> 0)
                string sailIndexStr = sailName.Replace("sail_", "");
                if (!int.TryParse(sailIndexStr, out int sailIndex))
                {
                    continue;
                }

                // Only apply logo to the target sail
                if (sailIndex != targetSailIndex)
                {
                    continue;
                }

                Debug.Log($"🏴 Applying logo to {sailName} under {mastType}");

                Material[] sharedMats = renderer.sharedMaterials;
                Material[] newMats = new Material[sharedMats.Length];
                bool anyChanged = false;

                for (int i = 0; i < sharedMats.Length; i++)
                {
                    if (sharedMats[i] == null)
                    {
                        newMats[i] = null;
                        continue;
                    }

                    // Copy the existing material (should already have style texture from ApplyStyleTextures)
                    Material mat = new Material(sharedMats[i]);
                    if (!mat.name.Contains("_styled"))
                    {
                        mat.name = sharedMats[i].name + "_styled";
                    }

                    // Debug: Log all available texture properties
                    Shader shader = mat.shader;
                    Debug.Log($"  Material shader: {shader.name}");
                    for (int propIdx = 0; propIdx < shader.GetPropertyCount(); propIdx++)
                    {
                        if (shader.GetPropertyType(propIdx) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                        {
                            string propName = shader.GetPropertyName(propIdx);
                            Debug.Log($"    Texture property: {propName}");
                        }
                    }

                    // Apply logo as second blend texture
                    if (mat.HasProperty("_BlendTex2"))
                    {
                        mat.SetTexture("_BlendTex2", logoTexture);
                        logosApplied++;
                        anyChanged = true;
                        newMats[i] = mat;
                        Debug.Log($"    ✅ Applied logo overlay to: {renderer.gameObject.name}");
                    }
                    else
                    {
                        Debug.LogWarning($"    Material does not have _BlendTex2 property");
                        newMats[i] = mat;
                    }
                }

                // Update renderer materials if we modified any
                if (anyChanged)
                {
                    renderer.sharedMaterials = newMats;
                }
            }

            Debug.Log($"Applied logo overlay '{logoTextureName}' to {logosApplied} sail materials (Logo ID: {logoID})");
        }
    }
}
