using UnityEngine;
using WorldDataImporter.Utilities;
using WorldDataImporter.Data;
using POTCO;
using POTCO.Editor;
using System.Collections.Generic;
using System.Linq;

namespace WorldDataImporter.Processors
{
    public static class PropertyProcessor
    {
        private static ObjectData currentObjectData;
        private static ImportStatistics currentStats;
        private static ObjectListInfo cachedObjectListInfo;
        private static GameObject cachedGameObject;

        private static ObjectListInfo GetCachedObjectListInfo()
        {
            return cachedObjectListInfo;
        }

        public static void ProcessProperty(string key, string val, GameObject currentGO, GameObject root, bool useEgg, ObjectData objectData = null, ImportStatistics stats = null, ImportSettings settings = null)
        {
            currentObjectData = objectData;
            currentStats = stats;
            
            // Cache ObjectListInfo component lookup for this GameObject
            if (cachedGameObject != currentGO)
            {
                cachedGameObject = currentGO;
                cachedObjectListInfo = currentGO?.GetComponent<ObjectListInfo>();
            }

            switch (key)
            {
                case "Pos":
                    if (currentGO != root)
                    {
                        Vector3 pos = ParsingUtilities.ParseVector3(val);

                        // Store Pos value for NPCs (will check against GridPos later)
                        if (objectData != null && settings?.importNPCs == true && objectData.objectType == "Townsperson")
                        {
                            // Check if GridPos was already processed
                            if (objectData.gridPos.HasValue)
                            {
                                // GridPos already processed, compare now
                                if (Vector3.Distance(pos, objectData.gridPos.Value) < 0.01f)
                                {
                                    // Pos = GridPos, both are world position - DON'T set position here, will convert in SpawnNPC
                                    objectData.hasPos = false;
                                    DebugLogger.LogNPCImport($"📍 Pos {pos} equals GridPos - will convert in SpawnNPC (leaving at 0,0,0 temporarily)");
                                }
                                else
                                {
                                    // Pos != GridPos, Pos is local position
                                    currentGO.transform.localPosition = pos;
                                    objectData.hasPos = true;
                                    DebugLogger.LogNPCImport($"📍 Pos != GridPos - using Pos as local: {pos}");
                                }
                            }
                            else
                            {
                                // GridPos not processed yet, store Pos for later comparison
                                if (objectData.properties == null)
                                    objectData.properties = new System.Collections.Generic.Dictionary<string, string>();
                                objectData.properties["Pos_Vector"] = $"{pos.x},{pos.y},{pos.z}";
                                DebugLogger.LogNPCImport($"📍 Stored Pos for later comparison: {pos}");
                            }
                        }
                        else
                        {
                            // Not an NPC or NPC import disabled, set position normally
                            currentGO.transform.localPosition = pos;
                        }
                    }
                    break;
                case "GridPos":
                    // Store GridPos for NPCs (always world position)
                    if (objectData != null && settings?.importNPCs == true)
                    {
                        objectData.gridPos = ParsingUtilities.ParseVector3(val);
                        DebugLogger.LogNPCImport($"📍 Stored GridPos: {objectData.gridPos}");

                        // Check if we have Pos stored
                        if (objectData.properties != null && objectData.properties.ContainsKey("Pos_Vector"))
                        {
                            string[] posParts = objectData.properties["Pos_Vector"].Split(',');
                            Vector3 posValue = new Vector3(
                                float.Parse(posParts[0]),
                                float.Parse(posParts[1]),
                                float.Parse(posParts[2])
                            );

                            // Check if Pos equals GridPos
                            if (Vector3.Distance(posValue, objectData.gridPos.Value) < 0.01f)
                            {
                                // Pos = GridPos, both are world position - DON'T set position here, will convert in SpawnNPC
                                objectData.hasPos = false;
                                DebugLogger.LogNPCImport($"📍 Pos {posValue} equals GridPos - will convert in SpawnNPC (leaving at 0,0,0 temporarily)");
                            }
                            else
                            {
                                // Pos != GridPos, Pos is local position
                                currentGO.transform.localPosition = posValue;
                                objectData.hasPos = true;
                                DebugLogger.LogNPCImport($"📍 Pos != GridPos - using Pos as local: {posValue}");
                            }
                        }
                        else if (currentGO != root)
                        {
                            // No Pos property exists, only GridPos - DON'T set position here, will convert in SpawnNPC
                            objectData.hasPos = false; // Will be converted in SpawnNPC
                            DebugLogger.LogNPCImport($"📍 Only GridPos (no Pos) - will convert in SpawnNPC: {objectData.gridPos.Value} (leaving at 0,0,0 temporarily)");
                        }
                    }
                    else if (objectData != null)
                    {
                        objectData.gridPos = ParsingUtilities.ParseVector3(val);
                    }
                    break;
                case "Hpr":
                    if (currentGO != root)
                    {
                        Vector3 hpr = ParsingUtilities.ParseVector3(val);
                        currentGO.transform.localEulerAngles = new Vector3(-hpr.z, -hpr.x, -hpr.y);
                    }
                    break;
                case "Scale":
                    if (currentGO != root) currentGO.transform.localScale = ParsingUtilities.ParseVector3(val, Vector3.one);
                    break;
                case "Type":
                    string objectType = ParsingUtilities.ExtractStringValue(val);
                    // Process Type property for ALL objects including root
                    if (currentGO != null)
                    {
                        // Store type as a tag/component instead of in the name
                        // This keeps the object ID clean for the exporter
                        if (objectData != null)
                        {
                            objectData.objectType = objectType;

                            // For Townsperson, use object ID as DNA ID if no DNA property exists
                            if (objectType == "Townsperson" && settings?.importNPCs == true)
                            {
                                if (string.IsNullOrEmpty(objectData.npcDnaId))
                                {
                                    objectData.npcDnaId = objectData.id;
                                    DebugLogger.LogNPCImport($"👤 Using object ID as DNA ID: {objectData.id}");
                                }
                            }

                            // Update the existing ObjectListInfo component only if ImportObjectListData is enabled
                            if (settings != null && settings.importObjectListData)
                            {
                                var typeInfo = GetCachedObjectListInfo();
                                if (typeInfo != null)
                                {
                                    typeInfo.objectType = objectType;
                                }
                                else
                                {
                                    // Fallback: create component if somehow missing
                                    typeInfo = currentGO.AddComponent<ObjectListInfo>();
                                    typeInfo.objectType = objectType;
                                    cachedObjectListInfo = typeInfo; // Update cache
                                }
                            }
                        }

                        if (stats != null)
                        {
                            if (stats.objectTypeCount.ContainsKey(objectType))
                                stats.objectTypeCount[objectType]++;
                            else
                                stats.objectTypeCount[objectType] = 1;
                        }
                    }
                    break;
                case "Name":
                    string objectName = ParsingUtilities.ExtractStringValue(val);
                    if (!string.IsNullOrEmpty(objectName))
                    {
                        currentGO.name = objectName;
                    }
                    break;
                case "Model":
                    if (ParsingUtilities.ExtractModelPath(val, out string modelPath))
                    {
                        // Skip holiday models if holiday import is disabled
                        if (settings != null && !settings.importHolidayObjects && modelPath.Contains("_hol_"))
                        {
                            DebugLogger.LogWorldImporter($"🎄 Skipped holiday model: {modelPath}");
                            break;
                        }
                        
                        var instance = AssetUtilities.InstantiatePrefab(modelPath, currentGO, useEgg, stats);
                        
                        // Update the ObjectListInfo with model path only if ImportObjectListData is enabled
                        if (settings != null && settings.importObjectListData)
                        {
                            var typeInfo = GetCachedObjectListInfo();
                            if (typeInfo != null)
                            {
                                typeInfo.modelPath = modelPath;
                                typeInfo.hasVisualBlock = true;
                            }
                        }
                        
                        // Rename the instance to the model name for better hierarchy
                        if (instance != null)
                        {
                            string modelName = System.IO.Path.GetFileNameWithoutExtension(modelPath);
                            instance.name = modelName;
                            
                            // Also update parent object name to be more descriptive (only if ImportObjectListData is enabled)
                            if (!string.IsNullOrEmpty(modelName) && settings != null && settings.importObjectListData)
                            {
                                var typeInfo = GetCachedObjectListInfo();
                                if (typeInfo != null)
                                {
                                    currentGO.name = typeInfo.GetDisplayName();
                                }
                            }
                        }
                        
                        // Apply any pending visual modifications (only if enabled)
                        if (instance != null && objectData != null)
                        {
                            // Handle collision settings
                            if (settings?.importCollisions == false)
                            {
                                // Remove all collisions if import collisions is disabled
                                AssetUtilities.RemoveCollisions(instance, stats);
                            }
                            else if (objectData.disableCollision.HasValue && objectData.disableCollision.Value)
                            {
                                // Disable collisions if this specific object has DisableCollision=True
                                AssetUtilities.SetCollisionEnabled(instance, false, stats);
                            }

                            // Apply visual color if it was set (now that the model is instantiated)
                            // IMPORTANT: Never apply color to root GameObject (it should only have Model, not Color)
                            if (objectData.visualColor.HasValue && settings?.importObjectListData == true && currentGO != root)
                            {
                                // IMPORTANT: Get ObjectListInfo directly from currentGO (not cache) to ensure correct GameObject
                                var typeInfo = currentGO.GetComponent<ObjectListInfo>();
                                if (typeInfo != null)
                                {
                                    // Ensure the color is set on ObjectListInfo
                                    typeInfo.visualColor = objectData.visualColor.Value;

                                    // Add or get VisualColorHandler
                                    VisualColorHandler colorHandler = currentGO.GetComponent<VisualColorHandler>();
                                    if (colorHandler == null)
                                    {
                                        colorHandler = currentGO.AddComponent<VisualColorHandler>();
                                    }

                                    // Apply color immediately to the newly instantiated model
                                    colorHandler.ApplyVisualColor(objectData.visualColor.Value);

                                    // Mark dirty for serialization
                                    UnityEditor.EditorUtility.SetDirty(currentGO);
                                    UnityEditor.EditorUtility.SetDirty(typeInfo);
                                    UnityEditor.EditorUtility.SetDirty(colorHandler);

                                    DebugLogger.LogWorldImporter($"🎨 Applied Visual color to model {modelPath} on GameObject {currentGO.name}: {objectData.visualColor.Value}");

                                    if (stats != null) stats.visualColorsApplied++;
                                }
                            }
                            else if (objectData.visualColor.HasValue && currentGO == root)
                            {
                                DebugLogger.LogWorldImporter($"⚠️ BLOCKED: Attempted to apply Visual color to root GameObject {currentGO.name} - Root should never have visual colors!");
                            }
                        }
                    }
                    
                    // Create light if this is a Light - Dynamic object
                    if (objectData != null && objectData.objectType == "Light - Dynamic" && settings?.addLighting == true)
                    {
                        AssetUtilities.CreateLight(currentGO, objectData, stats);
                    }
                    break;
                case "DisableCollision":
                    if (ParsingUtilities.ParseBool(val, out bool disableCollision) && objectData != null)
                    {
                        // Always store the collision setting, but only apply it if processing is enabled
                        objectData.disableCollision = disableCollision;
                        
                        // Store in ObjectListInfo only if ImportObjectListData is enabled
                        if (settings != null && settings.importObjectListData)
                        {
                            var typeInfo = GetCachedObjectListInfo();
                            if (typeInfo != null)
                            {
                                typeInfo.disableCollision = disableCollision;
                            }
                        }
                    }
                    break;
                case "Holiday":
                    string holiday = ParsingUtilities.ExtractStringValue(val);
                    if (objectData != null) objectData.holiday = holiday;
                    
                    // Store in ObjectListInfo only if ImportObjectListData is enabled
                    if (settings != null && settings.importObjectListData)
                    {
                        var holidayTypeInfo = GetCachedObjectListInfo();
                        if (holidayTypeInfo != null)
                        {
                            holidayTypeInfo.holiday = holiday;
                        }
                    }
                    break;
                case "Instanced":
                    if (ParsingUtilities.ParseBool(val, out bool instanced) && objectData != null)
                    {
                        objectData.isInstanced = instanced;
                        
                        // Store in ObjectListInfo only if ImportObjectListData is enabled
                        if (settings != null && settings.importObjectListData)
                        {
                            var instancedTypeInfo = GetCachedObjectListInfo();
                            if (instancedTypeInfo != null)
                            {
                                instancedTypeInfo.instanced = instanced;
                            }
                        }
                    }
                    break;
                case "VisSize":
                    string visSize = ParsingUtilities.ExtractStringValue(val);
                    if (objectData != null) objectData.visSize = visSize;

                    // Store in ObjectListInfo only if ImportObjectListData is enabled
                    if (settings != null && settings.importObjectListData)
                    {
                        var visSizeTypeInfo = GetCachedObjectListInfo();
                        if (visSizeTypeInfo != null)
                        {
                            visSizeTypeInfo.visSize = visSize;
                        }
                    }
                    break;
                case "VisZone":
                    string visZone = ParsingUtilities.ExtractStringValue(val);
                    if (objectData != null) objectData.visZone = visZone;

                    // Store in ObjectListInfo only if ImportObjectListData is enabled
                    if (settings != null && settings.importObjectListData)
                    {
                        var visZoneTypeInfo = GetCachedObjectListInfo();
                        if (visZoneTypeInfo != null)
                        {
                            visZoneTypeInfo.visZone = visZone;
                        }
                    }
                    break;
                case "Visual":
                    // Handle nested Visual properties - will be processed by lines following this
                    break;
                case "Color":
                    // Parse Visual color from POTCO format and store it
                    // It will be applied after the Model is instantiated
                    // IMPORTANT: Root GameObject should NEVER have a visual color
                    if (ParsingUtilities.ParseColor(val, out Color visualColor))
                    {
                        if (currentGO == root)
                        {
                            DebugLogger.LogWorldImporter($"⚠️ WARNING: Color property found on ROOT GameObject {currentGO.name} - This should never happen! Skipping color assignment.");
                            break;
                        }

                        if (objectData != null)
                        {
                            objectData.visualColor = visualColor;
                            DebugLogger.LogWorldImporter($"🎨 Stored Visual color for {currentGO.name}: {visualColor}");
                        }

                        // Also store in ObjectListInfo if it exists
                        // IMPORTANT: Get ObjectListInfo directly from currentGO (not cache) to ensure correct GameObject
                        if (settings != null && settings.importObjectListData && currentGO != null)
                        {
                            var typeInfo = currentGO.GetComponent<ObjectListInfo>();
                            if (typeInfo != null)
                            {
                                typeInfo.visualColor = visualColor;
                                DebugLogger.LogWorldImporter($"🎨 Set visualColor on ObjectListInfo for GameObject: {currentGO.name}");
                            }
                        }
                    }
                    break;
                case "LightType":
                    if (objectData != null)
                    {
                        objectData.lightType = ParsingUtilities.ExtractStringValue(val);
                    }
                    break;
                case "Intensity":
                    if (objectData != null && float.TryParse(ParsingUtilities.ExtractStringValue(val), out float intensity))
                    {
                        objectData.intensity = intensity;
                    }
                    break;
                case "Attenuation":
                    if (objectData != null && float.TryParse(ParsingUtilities.ExtractStringValue(val), out float attenuation))
                    {
                        objectData.attenuation = attenuation;
                    }
                    break;
                case "ConeAngle":
                    if (objectData != null && float.TryParse(ParsingUtilities.ExtractStringValue(val), out float coneAngle))
                    {
                        objectData.coneAngle = coneAngle;
                    }
                    break;
                case "DropOff":
                    if (objectData != null && float.TryParse(ParsingUtilities.ExtractStringValue(val), out float dropOff))
                    {
                        objectData.dropOff = dropOff;
                    }
                    break;
                case "Flickering":
                    if (ParsingUtilities.ParseBool(val, out bool flickering) && objectData != null)
                    {
                        objectData.flickering = flickering;
                    }
                    break;
                case "FlickRate":
                    if (objectData != null && float.TryParse(val, out float flickRate))
                    {
                        objectData.flickRate = flickRate;
                    }
                    break;
                case "DNA":
                    // Store DNA ID for NPC spawning (overrides object ID)
                    if (objectData != null && settings?.importNPCs == true)
                    {
                        string dnaId = ParsingUtilities.ExtractStringValue(val);
                        objectData.npcDnaId = dnaId;
                        DebugLogger.LogNPCImport($"👤 Stored NPC DNA ID: {dnaId} (overriding object ID)");
                    }
                    break;
                case "CustomModel":
                    // Store custom model path for NPCs (ignore "None")
                    if (objectData != null && settings?.importNPCs == true)
                    {
                        string customModel = ParsingUtilities.ExtractStringValue(val);
                        if (customModel != "None")
                        {
                            objectData.npcCustomModel = customModel;
                            DebugLogger.LogNPCImport($"👤 Stored NPC custom model: {customModel}");
                        }
                    }
                    break;
                case "AnimSet":
                    // Store animation set for NPCs
                    if (objectData != null && settings?.importNPCs == true)
                    {
                        string animSet = ParsingUtilities.ExtractStringValue(val);
                        objectData.npcAnimSet = animSet;
                        objectData.isReadyForNPCSpawn = true; // Mark as ready for spawning
                        DebugLogger.LogNPCImport($"🎬 Stored NPC AnimSet: {animSet}");
                    }
                    break;
                case "Category":
                    // Store NPC category
                    if (objectData != null && settings?.importNPCs == true)
                    {
                        string category = ParsingUtilities.ExtractStringValue(val);
                        objectData.npcCategory = category;
                        DebugLogger.LogNPCImport($"📂 Stored NPC Category: {category}");
                    }
                    break;
                case "Patrol Radius":
                    // Store patrol radius
                    if (objectData != null && settings?.importNPCs == true && float.TryParse(ParsingUtilities.ExtractStringValue(val), out float patrolRadius))
                    {
                        objectData.npcPatrolRadius = patrolRadius;
                        DebugLogger.LogNPCImport($"🚶 Stored NPC Patrol Radius: {patrolRadius}");
                    }
                    break;
                case "Start State":
                    // Store start state
                    if (objectData != null && settings?.importNPCs == true)
                    {
                        string startState = ParsingUtilities.ExtractStringValue(val);
                        objectData.npcStartState = startState;
                        DebugLogger.LogNPCImport($"🎬 Stored NPC Start State: {startState}");
                    }
                    break;
                case "Team":
                    // Store team
                    if (objectData != null && settings?.importNPCs == true)
                    {
                        string team = ParsingUtilities.ExtractStringValue(val);
                        objectData.npcTeam = team;
                        DebugLogger.LogNPCImport($"👥 Stored NPC Team: {team}");
                    }
                    break;
                case "Aggro Radius":
                    // Store aggro radius
                    if (objectData != null && settings?.importNPCs == true && float.TryParse(ParsingUtilities.ExtractStringValue(val), out float aggroRadius))
                    {
                        objectData.npcAggroRadius = aggroRadius;
                        DebugLogger.LogNPCImport($"⚔️ Stored NPC Aggro Radius: {aggroRadius}");
                    }
                    break;
                case "Greeting Animation":
                    // Store greeting animation
                    if (objectData != null && settings?.importNPCs == true)
                    {
                        string greetingAnim = ParsingUtilities.ExtractStringValue(val);
                        objectData.npcGreetingAnim = greetingAnim;
                        DebugLogger.LogNPCImport($"👋 Stored NPC Greeting Animation: {greetingAnim}");
                    }
                    break;
                case "Notice Animation 1":
                    // Store notice animation 1
                    if (objectData != null && settings?.importNPCs == true)
                    {
                        string noticeAnim1 = ParsingUtilities.ExtractStringValue(val);
                        objectData.npcNoticeAnim1 = noticeAnim1;
                        DebugLogger.LogNPCImport($"👀 Stored NPC Notice Animation 1: {noticeAnim1}");
                    }
                    break;
                case "Notice Animation 2":
                    // Store notice animation 2
                    if (objectData != null && settings?.importNPCs == true)
                    {
                        string noticeAnim2 = ParsingUtilities.ExtractStringValue(val);
                        objectData.npcNoticeAnim2 = noticeAnim2;
                        DebugLogger.LogNPCImport($"👀 Stored NPC Notice Animation 2: {noticeAnim2}");
                    }
                    break;
            }

            // Note: NPC spawning is now handled after all properties are processed
            // (moved to SceneBuildingAlgorithm when object is complete)
        }

        public static void SpawnNPC(GameObject currentGO, ObjectData objectData, ImportStatistics stats)
        {
            try
            {
                // Determine if using DNA or CustomModel
                // Note: Object ID is used as DNA ID when no DNA property exists (set in Type case)
                bool useDNA = !string.IsNullOrEmpty(objectData.npcDnaId);
                bool useCustomModel = !string.IsNullOrEmpty(objectData.npcCustomModel);

                GameObject characterModel = null;
                string modelPath = ""; // Define outside scope so it's accessible in both blocks
                string[] alternatePaths = null; // Define outside scope for error logging

                if (useCustomModel)
                {
                    // Use custom model path (e.g., "models/char/js_2000" for Jack Sparrow)
                    modelPath = objectData.npcCustomModel;

                    // Try loading from Resources
                    characterModel = UnityEngine.Resources.Load<GameObject>(modelPath);

                    if (characterModel == null)
                    {
                        // Try alternative paths
                        alternatePaths = new string[]
                        {
                            modelPath,
                            "phase_2/" + modelPath,
                            "phase_3/" + modelPath,
                            "phase_4/" + modelPath,
                            modelPath.Replace("models/", ""),
                            "phase_2/models/" + modelPath.Replace("models/", "")
                        };

                        foreach (string altPath in alternatePaths)
                        {
                            characterModel = UnityEngine.Resources.Load<GameObject>(altPath);
                            if (characterModel != null)
                            {
                                DebugLogger.LogNPCImport($"✅ Loaded custom model from alternate path: {altPath}");
                                modelPath = altPath;
                                break;
                            }
                        }
                    }

                    if (characterModel == null)
                    {
                        DebugLogger.LogNPCImport($"❌ Failed to load custom NPC model: {objectData.npcCustomModel}");
                        DebugLogger.LogNPCImport($"   Tried paths: {string.Join(", ", alternatePaths)}");
                        DebugLogger.LogNPCImport($"   FALLBACK: Will use DNA-based character generation instead");

                        // Fallback to DNA if CustomModel fails
                        useCustomModel = false;
                        useDNA = true;
                    }
                    else
                    {
                        DebugLogger.LogNPCImport($"✅ Loaded custom NPC model: {modelPath}");
                    }
                }

                if (useCustomModel && characterModel != null)
                {

                    // Instantiate the custom model
                    GameObject instance = UnityEditor.PrefabUtility.InstantiatePrefab(characterModel) as GameObject;
                    if (instance == null)
                    {
                        instance = GameObject.Instantiate(characterModel);
                    }

                    instance.name = System.IO.Path.GetFileNameWithoutExtension(modelPath);

                    // Debug: Check position values
                    DebugLogger.LogNPCImport($"🔍 Spawn Check (Custom): gridPos={objectData.gridPos}, hasPos={objectData.hasPos}, currentPos={currentGO.transform.localPosition}");

                    // Position parent GameObject if using GridPos (no Pos was set)
                    if (objectData.gridPos.HasValue && !objectData.hasPos)
                    {
                        // Convert GridPos (world position) to local position
                        if (currentGO.transform.parent != null)
                        {
                            Vector3 parentWorldPos = currentGO.transform.parent.position;
                            Vector3 parentWorldRot = currentGO.transform.parent.eulerAngles;
                            Vector3 localPos = currentGO.transform.parent.InverseTransformPoint(objectData.gridPos.Value);
                            currentGO.transform.localPosition = localPos;
                            DebugLogger.LogNPCImport($"📍 GridPos conversion (custom model):");
                            DebugLogger.LogNPCImport($"  - GridPos (world): {objectData.gridPos.Value}");
                            DebugLogger.LogNPCImport($"  - Parent world pos: {parentWorldPos}");
                            DebugLogger.LogNPCImport($"  - Parent world rot: {parentWorldRot}");
                            DebugLogger.LogNPCImport($"  - Calculated local: {localPos}");
                        }
                        else
                        {
                            currentGO.transform.localPosition = objectData.gridPos.Value;
                        }
                    }
                    else if (objectData.properties != null && objectData.properties.ContainsKey("Pos_Vector"))
                    {
                        // Pos was stored but never applied (no GridPos to compare against)
                        string[] posParts = objectData.properties["Pos_Vector"].Split(',');
                        Vector3 posValue = new Vector3(
                            float.Parse(posParts[0]),
                            float.Parse(posParts[1]),
                            float.Parse(posParts[2])
                        );
                        currentGO.transform.localPosition = posValue;
                        DebugLogger.LogNPCImport($"📍 Applied stored Pos (no GridPos) for custom model: {posValue}");
                    }

                    // Parent NPC model to positioned GameObject
                    instance.transform.SetParent(currentGO.transform, false);
                    DebugLogger.LogNPCImport($"👤 Spawned custom NPC model: {modelPath}");

                    // Add CharacterGenderData for animation system (detect from model path)
                    string gender = modelPath.Contains("/fp_") || modelPath.Contains("fp_") ? "f" : "m";
                    var genderData = instance.GetComponent<CharacterOG.Runtime.CharacterGenderData>();
                    if (genderData == null)
                    {
                        genderData = instance.AddComponent<CharacterOG.Runtime.CharacterGenderData>();
                    }
                    genderData.SetGender(gender);
                    DebugLogger.LogNPCImport($"✅ Set gender to {(gender == "f" ? "FEMALE" : "MALE")} for custom model");
                }

                // Use DNA-based character spawning (either initially or as fallback from failed CustomModel)
                if (useDNA && !useCustomModel)
                {
                    var dataSource = new CharacterOG.Data.PureCSharpBackend.PureCSharpDataSource();

                    if (!dataSource.IsAvailable)
                    {
                        DebugLogger.LogNPCImport($"❌ NPC data source not available");
                        return;
                    }

                    // Load NPC database
                    var npcDatabase = dataSource.LoadNpcDna();

                    // Object ID is used as DNA ID when no DNA property exists
                    if (!npcDatabase.TryGetValue(objectData.npcDnaId, out var pirateDna))
                    {
                        DebugLogger.LogNPCImport($"❌ NPC DNA not found in database: {objectData.npcDnaId}");
                        return;
                    }

                    DebugLogger.LogNPCImport($"👤 Found NPC DNA: {pirateDna.name} ({objectData.npcDnaId})");

                    // Determine model path based on gender
                    modelPath = pirateDna.gender.ToLower() == "f"
                        ? "phase_2/models/char/fp_2000"
                        : "phase_2/models/char/mp_2000";

                    characterModel = UnityEngine.Resources.Load<GameObject>(modelPath);

                    if (characterModel == null)
                    {
                        DebugLogger.LogWorldImporter($"❌ Failed to load character model: {modelPath}");
                        return;
                    }

                    // Instantiate character
                    GameObject instance = UnityEditor.PrefabUtility.InstantiatePrefab(characterModel) as GameObject;
                    if (instance == null)
                    {
                        instance = GameObject.Instantiate(characterModel);
                    }

                    instance.name = pirateDna.name;

                    // Load body shapes for both genders
                    var bodyShapes = new Dictionary<string, CharacterOG.Models.BodyShapeDef>();
                    foreach (var shape in dataSource.LoadBodyShapes("m"))
                        bodyShapes[shape.Key] = shape.Value;
                    foreach (var shape in dataSource.LoadBodyShapes("f"))
                        if (!bodyShapes.ContainsKey(shape.Key))
                            bodyShapes[shape.Key] = shape.Value;

                    // Load clothing catalog and other data
                    var clothingCatalog = dataSource.LoadClothingCatalog(pirateDna.gender);
                    var palettes = dataSource.LoadPalettesAndDyeRules();
                    var jewelryTattoos = dataSource.LoadJewelryAndTattoos(pirateDna.gender);
                    var facialMorphs = dataSource.LoadFacialMorphs(pirateDna.gender);

                    // Auto-find head and body roots (EXACT logic from NPCPreviewWindow + working commit)
                    Transform headRoot = null;
                    Transform bodyRoot = null;

                    Transform[] allTransforms = instance.GetComponentsInChildren<Transform>();

                    // POTCO characters use def_neck as the parent for all facial bones (def_trs_*, trs_face_*, etc.)
                    // The facial morphs modify bones like def_trs_left_forehead, def_trs_mid_jaw, etc which are children of def_neck
                    // POTCO headScale → applied to def_head01
                    string[] headCandidates = { "def_head01", "def_neck", "zz_neck", "def_head", "zz_head" };
                    foreach (var candidate in headCandidates)
                    {
                        var found = System.Array.Find(allTransforms, t => t.name == candidate);
                        if (found != null)
                        {
                            headRoot = found;
                            DebugLogger.LogNPCImport($"Found head root bone: {headRoot.name}");
                            break;
                        }
                    }

                    // POTCO bodyScale → applied to def_scale_jt as GLOBAL scale
                    string[] bodyCandidates = { "def_scale_jt", "def_spine01", "Spine", "spine01", "BodyRoot", "def_spine02" };
                    foreach (var candidate in bodyCandidates)
                    {
                        var found = System.Array.Find(allTransforms, t => t.name == candidate);
                        if (found != null)
                        {
                            bodyRoot = found;
                            break;
                        }
                    }

                    DebugLogger.LogNPCImport($"Auto-Find Roots: Head={headRoot?.name ?? "not found"}, Body={bodyRoot?.name ?? "not found"}");

                    // Create DnaApplier and apply DNA
                    var dnaApplier = new CharacterOG.Runtime.Systems.DnaApplier(
                        instance,
                        bodyShapes,
                        clothingCatalog,
                        palettes,
                        jewelryTattoos,
                        facialMorphs,
                        pirateDna.gender,
                        headRoot,
                        bodyRoot
                    );

                    dnaApplier.ApplyDNA(pirateDna);

                    // Add CharacterColorPersistence for play mode color persistence
                    var colorPersistence = instance.GetComponent<CharacterOG.Runtime.CharacterColorPersistence>();
                    if (colorPersistence == null)
                    {
                        colorPersistence = instance.AddComponent<CharacterOG.Runtime.CharacterColorPersistence>();
                    }

                    // Store applied colors for persistence
                    var (skin, hair, top, bot) = dnaApplier.GetAppliedColors();
                    colorPersistence.StoreColors(skin, hair, top, bot);

                    // Add CharacterGenderData for animation system
                    var genderData = instance.GetComponent<CharacterOG.Runtime.CharacterGenderData>();
                    if (genderData == null)
                    {
                        genderData = instance.AddComponent<CharacterOG.Runtime.CharacterGenderData>();
                    }
                    genderData.SetGender(pirateDna.gender);

                    DebugLogger.LogNPCImport($"✅ Added color persistence and gender data to {pirateDna.name}");

                    // Debug: Check position values
                    DebugLogger.LogNPCImport($"🔍 Spawn Check: gridPos={objectData.gridPos}, hasPos={objectData.hasPos}, currentPos={currentGO.transform.localPosition}");

                    // Position parent GameObject if using GridPos (no Pos was set)
                    if (objectData.gridPos.HasValue && !objectData.hasPos)
                    {
                        // Convert GridPos (world position) to local position relative to parent
                        if (currentGO.transform.parent != null)
                        {
                            Vector3 parentWorldPos = currentGO.transform.parent.position;
                            Vector3 parentWorldRot = currentGO.transform.parent.eulerAngles;
                            Vector3 localPos = currentGO.transform.parent.InverseTransformPoint(objectData.gridPos.Value);
                            currentGO.transform.localPosition = localPos;
                            DebugLogger.LogNPCImport($"📍 GridPos conversion:");
                            DebugLogger.LogNPCImport($"  - GridPos (world): {objectData.gridPos.Value}");
                            DebugLogger.LogNPCImport($"  - Parent world pos: {parentWorldPos}");
                            DebugLogger.LogNPCImport($"  - Parent world rot: {parentWorldRot}");
                            DebugLogger.LogNPCImport($"  - Calculated local: {localPos}");
                        }
                        else
                        {
                            currentGO.transform.localPosition = objectData.gridPos.Value;
                        }
                    }
                    else if (objectData.properties != null && objectData.properties.ContainsKey("Pos_Vector"))
                    {
                        // Pos was stored but never applied (no GridPos to compare against)
                        string[] posParts = objectData.properties["Pos_Vector"].Split(',');
                        Vector3 posValue = new Vector3(
                            float.Parse(posParts[0]),
                            float.Parse(posParts[1]),
                            float.Parse(posParts[2])
                        );
                        currentGO.transform.localPosition = posValue;
                        DebugLogger.LogNPCImport($"📍 Applied stored Pos (no GridPos): {posValue}");
                    }

                    // Parent NPC model to positioned GameObject
                    instance.transform.SetParent(currentGO.transform, false);

                    // Mark all transforms dirty AFTER parenting to save body shape changes (CRITICAL!)
                    UnityEditor.EditorUtility.SetDirty(instance);
                    Transform[] allTransformsToMark = instance.GetComponentsInChildren<Transform>();
                    foreach (Transform t in allTransformsToMark)
                    {
                        UnityEditor.EditorUtility.SetDirty(t);
                    }

                    DebugLogger.LogNPCImport($"✅ Successfully applied NPC '{pirateDna.name}' ({pirateDna.gender})");
                    DebugLogger.LogNPCImport($"👤 Spawned NPC: {pirateDna.name} (DNA: {objectData.npcDnaId})");

                    // Don't apply animation set - NPCAnimationPlayer will handle it
                    // if (!string.IsNullOrEmpty(objectData.npcAnimSet))
                    // {
                    //     ApplyAnimationSet(instance, objectData.npcAnimSet, pirateDna.gender);
                    // }
                }

                // ADD NPC AI COMPONENTS (common for both DNA and CustomModel paths)
                // Must be done BEFORE ApplyAnimationSet so NPCData exists
                AddNPCComponents(currentGO, objectData);

                if (stats != null)
                {
                    // Track NPC spawns in stats
                    if (!stats.objectTypeCount.ContainsKey("NPC_Spawned"))
                        stats.objectTypeCount["NPC_Spawned"] = 0;
                    stats.objectTypeCount["NPC_Spawned"]++;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.LogNPCImport($"❌ Failed to spawn NPC: {ex.Message}");
                Debug.LogError($"NPC spawn error: {ex}");
            }
        }

        /// <summary>
        /// Add NPC AI components (NPCData, CharacterController, NPCController, NPCAnimationPlayer)
        /// </summary>
        private static void AddNPCComponents(GameObject npcParent, ObjectData objectData)
        {
            try
            {
                // Find the character root GameObject (the one spawned in SpawnNPC - has the character name)
                // This is typically the first direct child of npcParent (or npcParent/Model if NPCController created Model wrapper)
                GameObject characterRootObject = null;

                // Check if there's a "Model" child (created by NPCController)
                Transform modelTransform = npcParent.transform.Find("Model");
                if (modelTransform != null && modelTransform.childCount > 0)
                {
                    // Character is child of Model
                    characterRootObject = modelTransform.GetChild(0).gameObject;
                    DebugLogger.LogNPCImport($"📦 Found character under Model wrapper: {characterRootObject.name}");
                }
                else if (npcParent.transform.childCount > 0)
                {
                    // Character is direct child
                    characterRootObject = npcParent.transform.GetChild(0).gameObject;
                    DebugLogger.LogNPCImport($"📦 Found character as direct child: {characterRootObject.name}");
                }

                // Verify this is the character root by checking for SkinnedMeshRenderer in children
                if (characterRootObject != null)
                {
                    SkinnedMeshRenderer smr = characterRootObject.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (smr != null)
                    {
                        DebugLogger.LogNPCImport($"✅ Verified character root has SkinnedMeshRenderer: {smr.name}");
                    }
                    else
                    {
                        DebugLogger.LogNPCImport($"⚠️ No SkinnedMeshRenderer found under {characterRootObject.name}");
                    }
                }

                // Ensure Animation component exists on the character root
                Animation animComponent = null;
                if (characterRootObject != null)
                {
                    animComponent = characterRootObject.GetComponent<Animation>();
                    if (animComponent == null)
                    {
                        animComponent = characterRootObject.AddComponent<Animation>();
                        DebugLogger.LogNPCImport($"✅ Added Animation component to character root: {characterRootObject.name}");
                    }
                    else
                    {
                        DebugLogger.LogNPCImport($"✅ Animation component already exists on: {characterRootObject.name}");
                    }
                }
                else
                {
                    DebugLogger.LogNPCImport($"⚠️ No character root found to add Animation component!");
                }

                // Add NPCData component and transfer world data
                NPCData npcData = npcParent.GetComponent<NPCData>();
                if (npcData == null)
                {
                    npcData = npcParent.AddComponent<NPCData>();
                }

                npcData.npcId = objectData.id;
                npcData.category = objectData.npcCategory ?? "Commoner";
                npcData.team = objectData.npcTeam ?? "Villager";
                npcData.startState = objectData.npcStartState ?? "LandRoam";
                npcData.patrolRadius = objectData.npcPatrolRadius > 0 ? objectData.npcPatrolRadius : 12f;
                npcData.aggroRadius = objectData.npcAggroRadius;
                npcData.animSet = objectData.npcAnimSet ?? "default";
                npcData.greetingAnimation = objectData.npcGreetingAnim ?? "";
                npcData.noticeAnimation1 = objectData.npcNoticeAnim1 ?? "";
                npcData.noticeAnimation2 = objectData.npcNoticeAnim2 ?? "";

                // Add CharacterController for movement
                CharacterController controller = npcParent.GetComponent<CharacterController>();
                if (controller == null)
                {
                    controller = npcParent.AddComponent<CharacterController>();
                    controller.radius = 0.5f;
                    controller.height = 2f;
                    controller.center = new Vector3(0, 1f, 0);
                }

                // Add NPCController for AI
                NPCController npcController = npcParent.GetComponent<NPCController>();
                if (npcController == null)
                {
                    npcController = npcParent.AddComponent<NPCController>();
                }

                // Add NPCAnimationPlayer for animation control
                NPCAnimationPlayer npcAnimPlayer = npcParent.GetComponent<NPCAnimationPlayer>();
                if (npcAnimPlayer == null)
                {
                    npcAnimPlayer = npcParent.AddComponent<NPCAnimationPlayer>();
                }

                DebugLogger.LogNPCImport($"✅ Added NPC AI components to {npcParent.name}");
                DebugLogger.LogNPCImport($"   Category: {npcData.category}, Team: {npcData.team}, Patrol Radius: {npcData.patrolRadius}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.LogNPCImport($"❌ Failed to add NPC components: {ex.Message}");
            }
        }

        private static void ApplyAnimationSet(GameObject character, string animSet, string gender)
        {
            try
            {
                // Animation naming convention: mp_* for male, fp_* for female
                string prefix = gender.ToLower() == "f" ? "fp_" : "mp_";

                // Common animation set names and their corresponding animation files
                // AnimSet from world data (e.g., "bar_talk01", "default", "idle") maps to animation files
                string animationName = $"{prefix}{animSet}";

                // Try to load animation clip from Resources
                AnimationClip clip = UnityEngine.Resources.Load<AnimationClip>($"phase_2/models/char/{animationName}");

                if (clip != null)
                {
                    // Add Animator component if not present
                    Animator animator = character.GetComponent<Animator>();
                    if (animator == null)
                    {
                        animator = character.AddComponent<Animator>();
                    }

                    // Create a simple runtime animator controller
                    var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath($"Assets/GeneratedAnimators/{character.name}_{animSet}.controller");
                    var stateMachine = controller.layers[0].stateMachine;
                    var state = stateMachine.AddState(animSet);
                    state.motion = clip;
                    stateMachine.defaultState = state;

                    animator.runtimeAnimatorController = controller;

                    DebugLogger.LogNPCImport($"🎬 Applied animation: {animationName} to {character.name}");
                }
                else
                {
                    // Animation clip not found, just add Animation component and log
                    Animation anim = character.GetComponent<Animation>();
                    if (anim == null)
                    {
                        anim = character.AddComponent<Animation>();
                    }

                    DebugLogger.LogNPCImport($"⚠️ Animation clip not found: {animationName} (AnimSet: {animSet})");
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.LogNPCImport($"❌ Failed to apply animation set {animSet}: {ex.Message}");
            }
        }
    }
}