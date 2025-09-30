using UnityEngine;
using WorldDataImporter.Utilities;
using WorldDataImporter.Data;
using POTCO;
using POTCO.Editor;

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
                    if (currentGO != root) currentGO.transform.localPosition = ParsingUtilities.ParseVector3(val);
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
                            if (objectData.visualColor.HasValue && settings?.importObjectListData == true)
                            {
                                var typeInfo = GetCachedObjectListInfo();
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

                                    DebugLogger.LogWorldImporter($"🎨 Applied Visual color to model {modelPath}: {objectData.visualColor.Value}");

                                    if (stats != null) stats.visualColorsApplied++;
                                }
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
                case "Visual":
                    // Handle nested Visual properties - will be processed by lines following this
                    break;
                case "Color":
                    // Parse Visual color from POTCO format and store it
                    // It will be applied after the Model is instantiated
                    if (ParsingUtilities.ParseColor(val, out Color visualColor))
                    {
                        if (objectData != null)
                        {
                            objectData.visualColor = visualColor;
                            DebugLogger.LogWorldImporter($"🎨 Stored Visual color for {currentGO.name}: {visualColor}");
                        }

                        // Also store in ObjectListInfo if it exists
                        if (settings != null && settings.importObjectListData)
                        {
                            var typeInfo = GetCachedObjectListInfo();
                            if (typeInfo != null)
                            {
                                typeInfo.visualColor = visualColor;
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
            }
        }
    }
}