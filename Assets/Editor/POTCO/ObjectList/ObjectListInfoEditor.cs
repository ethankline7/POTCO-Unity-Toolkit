using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using POTCO;
using WorldDataExporter.Utilities;

namespace POTCO.Editor
{
    [CustomEditor(typeof(ObjectListInfo)), CanEditMultipleObjects]
    public class ObjectListInfoEditor : UnityEditor.Editor
    {
        private SerializedProperty objectTypeProp;
        private SerializedProperty objectIdProp;
        private SerializedProperty modelPathProp;
        private SerializedProperty hasVisualBlockProp;
        private SerializedProperty disableCollisionProp;
        private SerializedProperty instancedProp;
        private SerializedProperty holidayProp;
        private SerializedProperty visSizeProp;
        private SerializedProperty isGroupProp;
        private SerializedProperty autoDetectOnStartProp;
        private SerializedProperty autoGenerateIdProp;
        
        private List<string> availableObjectTypes;
        private int selectedTypeIndex = 0;
        
        private void OnEnable()
        {
            objectTypeProp = serializedObject.FindProperty("objectType");
            objectIdProp = serializedObject.FindProperty("objectId");
            modelPathProp = serializedObject.FindProperty("modelPath");
            hasVisualBlockProp = serializedObject.FindProperty("hasVisualBlock");
            disableCollisionProp = serializedObject.FindProperty("disableCollision");
            instancedProp = serializedObject.FindProperty("instanced");
            holidayProp = serializedObject.FindProperty("holiday");
            visSizeProp = serializedObject.FindProperty("visSize");
            isGroupProp = serializedObject.FindProperty("isGroup");
            autoDetectOnStartProp = serializedObject.FindProperty("autoDetectOnStart");
            autoGenerateIdProp = serializedObject.FindProperty("autoGenerateId");
            
            LoadAvailableObjectTypes();
        }
        
        private void LoadAvailableObjectTypes()
        {
            try
            {
                // Try to get object types from ObjectListParser first
                var rawObjectTypes = ObjectListParser.GetAllObjectTypes();
                if (rawObjectTypes == null || rawObjectTypes.Count == 0)
                {
                    throw new System.Exception("ObjectListParser returned empty list");
                }
                
                // Apply UI display mapping for user-friendly names
                availableObjectTypes = new List<string>();
                foreach (string type in rawObjectTypes)
                {
                    if (type == "MODULAR_OBJ")
                    {
                        availableObjectTypes.Add("Cave_Pieces");
                    }
                    else
                    {
                        availableObjectTypes.Add(type);
                    }
                }
                
                availableObjectTypes.Sort();
                
                // Find current selection index
                string currentType = objectTypeProp.stringValue;
                DebugLogger.LogAutoObjectList($"🔍 Looking for current type '{currentType}' in dropdown with {availableObjectTypes.Count} options");
                selectedTypeIndex = availableObjectTypes.IndexOf(currentType);
                
                // If not found, try to find MISC_OBJ as a fallback
                if (selectedTypeIndex < 0)
                {
                    DebugLogger.LogAutoObjectList($"⚠️ Current type '{currentType}' not found in dropdown, trying MISC_OBJ as fallback");
                    selectedTypeIndex = availableObjectTypes.IndexOf("MISC_OBJ");
                    if (selectedTypeIndex < 0) 
                    {
                        DebugLogger.LogAutoObjectList($"⚠️ MISC_OBJ not found either, defaulting to index 0");
                        selectedTypeIndex = 0;
                    }
                    else
                    {
                        DebugLogger.LogAutoObjectList($"✅ Found MISC_OBJ at index {selectedTypeIndex}");
                    }
                }
                else
                {
                    DebugLogger.LogAutoObjectList($"✅ Found current type '{currentType}' at index {selectedTypeIndex}");
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.LogWarningAutoObjectList($"Could not load object types from ObjectListParser: {ex.Message}");
                // Fall back to basic types from the runtime detector
                availableObjectTypes = POTCOObjectTypeDetector.GetBasicObjectTypes();
                availableObjectTypes.Sort();
                
                string currentType = objectTypeProp.stringValue;
                selectedTypeIndex = availableObjectTypes.IndexOf(currentType);
                if (selectedTypeIndex < 0) selectedTypeIndex = 0;
            }
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            ObjectListInfo objectListInfo = (ObjectListInfo)target;
            bool isMultiSelection = targets.Length > 1;

            EditorGUILayout.LabelField("ObjectList Info", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Auto-Detection Settings (supports multi-editing)
            EditorGUILayout.LabelField("Auto-Detection", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(autoDetectOnStartProp, new GUIContent("Auto-Detect on Start", "Automatically detect properties when component starts"));
            EditorGUILayout.PropertyField(autoGenerateIdProp, new GUIContent("Auto-Generate ID", "Automatically generate object ID when needed"));

            // Auto-detect buttons (supports multi-editing)
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔍 Auto-Detect All Properties", GUILayout.Height(25)))
            {
                if (isMultiSelection)
                {
                    foreach (ObjectListInfo target in targets)
                    {
                        ObjectListIntegration.AutoDetectAllProperties(target);
                        EditorUtility.SetDirty(target);
                    }
                }
                else
                {
                    ObjectListIntegration.AutoDetectAllProperties(objectListInfo);
                    EditorUtility.SetDirty(objectListInfo);
                }
                serializedObject.Update();
                LoadAvailableObjectTypes(); // Refresh the dropdown
            }
            if (GUILayout.Button("🆔 Generate New ID", GUILayout.Height(25)))
            {
                if (isMultiSelection)
                {
                    foreach (ObjectListInfo target in targets)
                    {
                        target.GenerateObjectId();
                        EditorUtility.SetDirty(target);
                    }
                }
                else
                {
                    objectListInfo.GenerateObjectId();
                    EditorUtility.SetDirty(objectListInfo);
                }
                serializedObject.Update();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            
            // Object Information (supports multi-editing)
            EditorGUILayout.LabelField("Object Information", EditorStyles.boldLabel);

            // Object Type Dropdown (supports multi-editing)
            if (availableObjectTypes != null && availableObjectTypes.Count > 0)
            {
                EditorGUI.BeginChangeCheck();
                selectedTypeIndex = EditorGUILayout.Popup("Object Type", selectedTypeIndex, availableObjectTypes.ToArray());
                if (EditorGUI.EndChangeCheck() && selectedTypeIndex >= 0 && selectedTypeIndex < availableObjectTypes.Count)
                {
                    objectTypeProp.stringValue = availableObjectTypes[selectedTypeIndex];
                }
            }
            else
            {
                EditorGUILayout.PropertyField(objectTypeProp, new GUIContent("Object Type"));
            }

            // Object ID with status indicator (supports multi-editing)
            if (!isMultiSelection)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(objectIdProp, new GUIContent("Object ID"));
                if (string.IsNullOrEmpty(objectIdProp.stringValue))
                {
                    EditorGUILayout.LabelField("❌", GUILayout.Width(20));
                }
                else
                {
                    EditorGUILayout.LabelField("✅", GUILayout.Width(20));
                }
                EditorGUILayout.EndHorizontal();

                // Model Path with status indicator
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(modelPathProp, new GUIContent("Model Path"));
                if (string.IsNullOrEmpty(modelPathProp.stringValue))
                {
                    EditorGUILayout.LabelField("❌", GUILayout.Width(20));
                }
                else
                {
                    EditorGUILayout.LabelField("✅", GUILayout.Width(20));
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // For multi-selection, show the fields without status indicators
                EditorGUILayout.PropertyField(objectIdProp, new GUIContent("Object ID"));
                EditorGUILayout.PropertyField(modelPathProp, new GUIContent("Model Path"));
            }

            EditorGUILayout.Space();
            
            // Visual Properties (supports multi-editing)
            EditorGUILayout.LabelField("Visual Properties", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(hasVisualBlockProp, new GUIContent("Has Visual Block"));

            // Handle nullable Color manually for multi-object editing
            EditorGUILayout.BeginHorizontal();
            bool hasColor = objectListInfo.visualColor.HasValue;

            // For multi-selection, show mixed value state if colors differ
            bool mixedValues = false;
            if (isMultiSelection)
            {
                bool? firstHasColor = ((ObjectListInfo)targets[0]).visualColor.HasValue;
                foreach (ObjectListInfo target in targets)
                {
                    if (target.visualColor.HasValue != firstHasColor)
                    {
                        mixedValues = true;
                        break;
                    }
                }
            }

            EditorGUI.showMixedValue = mixedValues;
            bool newHasColor = EditorGUILayout.Toggle("Use Visual Color", hasColor);
            EditorGUI.showMixedValue = false;

            if (newHasColor != hasColor || mixedValues)
            {
                if (isMultiSelection)
                {
                    foreach (ObjectListInfo target in targets)
                    {
                        if (newHasColor)
                        {
                            target.visualColor = Color.white;
                        }
                        else
                        {
                            target.visualColor = null;
                        }
                        target.UpdateVisualColor();
                        EditorUtility.SetDirty(target);
                    }
                }
                else
                {
                    if (newHasColor)
                    {
                        objectListInfo.visualColor = Color.white;
                    }
                    else
                    {
                        objectListInfo.visualColor = null;
                    }
                    objectListInfo.UpdateVisualColor();
                    EditorUtility.SetDirty(objectListInfo);
                }
                SceneView.RepaintAll();
            }

            if (newHasColor)
            {
                Color currentColor = objectListInfo.visualColor ?? Color.white;

                // Check for mixed color values in multi-selection
                bool colorMixedValues = false;
                if (isMultiSelection)
                {
                    Color? firstColor = ((ObjectListInfo)targets[0]).visualColor;
                    foreach (ObjectListInfo target in targets)
                    {
                        if (target.visualColor != firstColor)
                        {
                            colorMixedValues = true;
                            break;
                        }
                    }
                }

                EditorGUI.showMixedValue = colorMixedValues;
                Color newColor = EditorGUILayout.ColorField(currentColor);
                EditorGUI.showMixedValue = false;

                if (newColor != currentColor || colorMixedValues)
                {
                    if (isMultiSelection)
                    {
                        foreach (ObjectListInfo target in targets)
                        {
                            target.visualColor = newColor;
                            target.UpdateVisualColor();
                            EditorUtility.SetDirty(target);
                        }
                    }
                    else
                    {
                        objectListInfo.visualColor = newColor;
                        objectListInfo.UpdateVisualColor();
                        EditorUtility.SetDirty(objectListInfo);
                    }
                    SceneView.RepaintAll();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            
            // Object Properties
            EditorGUILayout.LabelField("Object Properties", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(disableCollisionProp, new GUIContent("Disable Collision"));
            EditorGUILayout.PropertyField(instancedProp, new GUIContent("Instanced"));
            EditorGUILayout.PropertyField(holidayProp, new GUIContent("Holiday"));
            EditorGUILayout.PropertyField(visSizeProp, new GUIContent("Vis Size"));

            EditorGUILayout.Space();

            // Group Settings (disabled for multi-selection)
            EditorGUILayout.LabelField("Group Settings", EditorStyles.boldLabel);

            if (isMultiSelection)
            {
                EditorGUILayout.HelpBox("Group Settings editing is disabled when multiple objects are selected.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.PropertyField(isGroupProp, new GUIContent("Group", "Mark as group - only exports position/rotation and holiday/visSize if set"));

                if (isGroupProp.boolValue)
                {
                    EditorGUILayout.HelpBox("📦 Group Mode: Only position, rotation, holiday, and visSize will be exported.", MessageType.Info);
                }
            }

            EditorGUILayout.Space();
            
            // Export Status
            EditorGUILayout.LabelField("Export Status", EditorStyles.boldLabel);

            if (isMultiSelection)
            {
                // Count ready vs not ready objects
                int readyCount = 0;
                int totalCount = targets.Length;

                foreach (ObjectListInfo target in targets)
                {
                    bool ready = !string.IsNullOrEmpty(target.objectId) &&
                                (target.isGroup || !string.IsNullOrEmpty(target.objectType));
                    if (ready) readyCount++;
                }

                if (readyCount == totalCount)
                {
                    EditorGUILayout.HelpBox($"✅ All {totalCount} objects ready to export!", MessageType.Info);
                }
                else if (readyCount > 0)
                {
                    EditorGUILayout.HelpBox($"⚠️ {readyCount}/{totalCount} objects ready to export", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox($"❌ None of the {totalCount} objects are ready to export", MessageType.Error);
                }
            }
            else
            {
                // Groups only need objectId, regular objects need both objectId and objectType
                bool readyToExport = !string.IsNullOrEmpty(objectIdProp.stringValue) &&
                                    (isGroupProp.boolValue || !string.IsNullOrEmpty(objectTypeProp.stringValue));

                if (readyToExport)
                {
                    EditorGUILayout.HelpBox("✅ Ready to export!", MessageType.Info);
                }
                else
                {
                    string issues = "";
                    if (string.IsNullOrEmpty(objectIdProp.stringValue)) issues += "• Missing Object ID\n";
                    if (!isGroupProp.boolValue && string.IsNullOrEmpty(objectTypeProp.stringValue)) issues += "• Missing Object Type (not required for groups)\n";

                    EditorGUILayout.HelpBox($"❌ Cannot export:\n{issues}", MessageType.Warning);
                }

                // Display detected info
                if (!string.IsNullOrEmpty(modelPathProp.stringValue))
                {
                    string modelName = System.IO.Path.GetFileNameWithoutExtension(modelPathProp.stringValue);
                    EditorGUILayout.HelpBox($"📋 Detected Model: {modelName}", MessageType.None);
                }
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}