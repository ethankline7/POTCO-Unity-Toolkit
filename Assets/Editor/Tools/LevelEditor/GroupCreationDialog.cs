using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using POTCO;

namespace POTCO.Editor
{
    public class GroupCreationDialog : EditorWindow
    {
        [System.Serializable]
        public class GroupItem
        {
            public string prefabPath;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale = Vector3.one;
            public string objectType;
            [System.NonSerialized]
            public GameObject sourceObject; // Don't serialize GameObject references
        }

        public enum PivotType
        {
            Center,
            Bottom,
            Custom
        }

        [System.Serializable]
        public class GroupData
        {
            public string name;
            public string category;
            public string subcategory;
            public List<GroupItem> items;
            public PivotType pivotType = PivotType.Bottom;
            public Vector3 customPivotOffset;
            [System.NonSerialized]
            public Texture2D customThumbnail; // Don't serialize Texture2D, save separately
        }

        private GroupData groupData;
        private GameObject[] selectedObjects;
        private System.Action<GroupData> onGroupCreated;
        private Vector2 scrollPosition;
        private bool previewEnabled = true;
        private GameObject previewParent;
        private Material previewMaterial;


        public static void ShowDialog(GameObject[] objects, System.Action<GroupData> callback)
        {
            var window = GetWindow<GroupCreationDialog>("Create Group", true);
            window.minSize = new Vector2(400, 600);
            window.maxSize = new Vector2(400, 800);
            window.selectedObjects = objects;
            window.onGroupCreated = callback;
            window.InitializeGroupData();
            window.CreatePreviewMaterial();
            window.Show();
        }

        private void InitializeGroupData()
        {
            groupData = new GroupData();
            groupData.name = "New Group";
            groupData.category = "Groups";
            groupData.subcategory = "Custom Groups";
            groupData.items = new List<GroupItem>();

            if (selectedObjects != null && selectedObjects.Length > 0)
            {
                // Calculate center point for pivot reference
                Vector3 centerPoint = CalculateCenterPoint(selectedObjects);

                // Create group items from selected objects
                foreach (var obj in selectedObjects)
                {
                    var item = new GroupItem();
                    item.sourceObject = obj;

                    // Get prefab path if available
                    var prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj);
                    if (!string.IsNullOrEmpty(prefabAssetPath))
                    {
                        item.prefabPath = prefabAssetPath;
                    }
                    else
                    {
                        // Handle non-prefab objects (like lights)
                        if (obj.GetComponent<Light>() != null)
                        {
                            // For lights, we'll save them differently
                            item.prefabPath = ""; // No prefab path for lights
                        }
                        else
                        {
                            Debug.LogWarning($"Object '{obj.name}' is not a prefab instance. Groups work best with prefab instances.");
                            item.prefabPath = "";
                        }
                    }

                    // Calculate relative position from center point
                    item.localPosition = obj.transform.position - centerPoint;
                    item.localRotation = obj.transform.rotation;
                    item.localScale = obj.transform.localScale;

                    // Get object type from ObjectListInfo component
                    var objectListInfo = obj.GetComponent<ObjectListInfo>();
                    item.objectType = objectListInfo != null ? objectListInfo.objectType : "MISC_OBJ";

                    groupData.items.Add(item);
                }

                // Auto-generate name based on objects
                if (selectedObjects.Length <= 3)
                {
                    groupData.name = string.Join(" + ", selectedObjects.Take(3).Select(o => o.name));
                }
                else
                {
                    groupData.name = $"{selectedObjects[0].name} + {selectedObjects.Length - 1} others";
                }
            }
        }

        private Vector3 CalculateCenterPoint(GameObject[] objects)
        {
            if (objects.Length == 0) return Vector3.zero;

            Vector3 sum = Vector3.zero;
            foreach (var obj in objects)
            {
                sum += obj.transform.position;
            }
            return sum / objects.Length;
        }

        private void CreatePreviewMaterial()
        {
            if (previewMaterial == null)
            {
                previewMaterial = new Material(Shader.Find("Standard"));
                previewMaterial.color = new Color(0.3f, 0.8f, 1f, 0.6f);
                previewMaterial.SetFloat("_Mode", 3); // Transparent mode
                previewMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                previewMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                previewMaterial.SetInt("_ZWrite", 0);
                previewMaterial.DisableKeyword("_ALPHATEST_ON");
                previewMaterial.EnableKeyword("_ALPHABLEND_ON");
                previewMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                previewMaterial.renderQueue = 3000;
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawBasicSettings();
            EditorGUILayout.Space(10);


            DrawObjectsList();
            EditorGUILayout.Space(10);


            DrawButtons();

            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal("Box");
            GUILayout.Label("📦", GUILayout.Width(30));
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Create Object Group", EditorStyles.boldLabel);
            GUILayout.Label($"Creating group from {selectedObjects?.Length ?? 0} selected objects", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBasicSettings()
        {
            EditorGUILayout.LabelField("Basic Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("Box");

            groupData.name = EditorGUILayout.TextField("Group Name", groupData.name);
            groupData.category = EditorGUILayout.TextField("Category", groupData.category);
            groupData.subcategory = EditorGUILayout.TextField("Subcategory", groupData.subcategory);

            EditorGUILayout.EndVertical();
        }


        private void DrawObjectsList()
        {
            EditorGUILayout.LabelField("Objects in Group", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("Box");

            if (groupData.items != null && groupData.items.Count > 0)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));

                for (int i = 0; i < groupData.items.Count; i++)
                {
                    var item = groupData.items[i];
                    EditorGUILayout.BeginHorizontal("Box");

                    // Object icon and name
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("📦", GUILayout.Width(20));

                    string displayName = item.sourceObject != null ? item.sourceObject.name : "Unknown Object";
                    if (!string.IsNullOrEmpty(item.prefabPath))
                    {
                        // Show appropriate label based on file type
                        if (item.prefabPath.EndsWith(".egg", System.StringComparison.OrdinalIgnoreCase))
                        {
                            displayName += " (EGG Model)";
                        }
                        else
                        {
                            displayName += " (Prefab)";
                        }
                    }
                    GUILayout.Label(displayName, EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();

                    GUILayout.FlexibleSpace();

                    // Object type
                    GUILayout.Label($"Type: {item.objectType}", EditorStyles.miniLabel, GUILayout.Width(100));

                    // Remove button
                    if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(20)))
                    {
                        groupData.items.RemoveAt(i);
                        i--; // Adjust index after removal
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("No objects selected for group creation.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }



        private void DrawButtons()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
            {
                DestroyScenePreview();
                Close();
            }

            GUILayout.FlexibleSpace();

            GUI.enabled = !string.IsNullOrEmpty(groupData.name) && groupData.items != null && groupData.items.Count > 0;
            if (GUILayout.Button("✅ Create Group", GUILayout.Height(30), GUILayout.Width(120)))
            {
                CreateGroup();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }


        private void CreateScenePreview()
        {
            DestroyScenePreview();

            if (selectedObjects == null || selectedObjects.Length == 0) return;

            previewParent = new GameObject("Group Preview");

            // Add preview materials to all objects
            foreach (var obj in selectedObjects)
            {
                var renderers = obj.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    var materials = renderer.materials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i] = previewMaterial;
                    }
                    renderer.materials = materials;
                }
            }
        }

        private void DestroyScenePreview()
        {
            if (previewParent != null)
            {
                DestroyImmediate(previewParent);
                previewParent = null;
            }

            // Restore original materials if needed
            // This would require storing original materials, but for now we'll rely on scene refresh
        }

        private void CreateGroup()
        {
            if (onGroupCreated != null)
            {
                onGroupCreated.Invoke(groupData);
            }

            DestroyScenePreview();
            Close();
        }

        private void OnDestroy()
        {
            DestroyScenePreview();

            if (previewMaterial != null)
            {
                DestroyImmediate(previewMaterial);
            }
        }
    }
}