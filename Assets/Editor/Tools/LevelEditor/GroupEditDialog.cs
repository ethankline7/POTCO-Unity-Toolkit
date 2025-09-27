using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using POTCO;

namespace POTCO.Editor
{
    public class GroupEditDialog : EditorWindow
    {
        [System.Serializable]
        public class GroupEditData
        {
            public string originalName;
            public string newName;
            public string newCategory;
            public string newSubcategory;
            public GameObject prefab;
            public ObjectListInfo groupInfo;
        }

        private GroupEditData editData;
        private Vector2 scrollPosition;
        private List<string> availableCategories = new List<string>();
        private int selectedCategoryIndex = 0;

        public static void ShowDialog(string name, string category, string subcategory, GameObject prefab)
        {
            var window = GetWindow<GroupEditDialog>("Edit Group", true);
            window.minSize = new Vector2(400, 300);
            window.maxSize = new Vector2(400, 500);
            window.InitializeEditData(name, category, subcategory, prefab);
            window.LoadAvailableCategories();
            window.Show();
        }

        private void InitializeEditData(string name, string category, string subcategory, GameObject prefab)
        {
            editData = new GroupEditData();
            editData.originalName = name;
            editData.newName = name;
            editData.newCategory = category;
            editData.newSubcategory = subcategory;
            editData.prefab = prefab;

            if (editData.prefab != null)
            {
                editData.groupInfo = editData.prefab.GetComponent<ObjectListInfo>();
            }
        }

        private void LoadAvailableCategories()
        {
            // Get existing categories from the PropBrowserWindow
            availableCategories = new List<string>
            {
                "Groups",
                "Buildings",
                "Ships",
                "Weapons",
                "Characters",
                "Caves",
                "Effects",
                "Environment",
                "Props",
                "Furniture",
                "Treasure"
            };

            // Find current category index
            selectedCategoryIndex = availableCategories.IndexOf(editData.newCategory);
            if (selectedCategoryIndex < 0)
            {
                selectedCategoryIndex = 0; // Default to first category
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawEditFields();
            EditorGUILayout.Space(10);

            DrawGroupInfo();
            EditorGUILayout.Space(10);

            DrawButtons();

            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal("Box");
            GUILayout.Label("✏️", GUILayout.Width(30));
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Edit Custom Group", EditorStyles.boldLabel);
            GUILayout.Label($"Editing: {editData.originalName}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawEditFields()
        {
            EditorGUILayout.LabelField("Group Properties", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("Box");

            // Group name
            EditorGUI.BeginChangeCheck();
            editData.newName = EditorGUILayout.TextField("Group Name", editData.newName);
            if (EditorGUI.EndChangeCheck())
            {
                // Validate name (no invalid characters)
                editData.newName = ValidateFileName(editData.newName);
            }

            // Category dropdown
            EditorGUI.BeginChangeCheck();
            selectedCategoryIndex = EditorGUILayout.Popup("Category", selectedCategoryIndex, availableCategories.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                editData.newCategory = availableCategories[selectedCategoryIndex];

                // Auto-set subcategory based on category
                if (editData.newCategory == "Groups")
                {
                    editData.newSubcategory = "Custom Groups";
                }
                else
                {
                    editData.newSubcategory = editData.newCategory; // Default subcategory
                }
            }

            // Subcategory
            editData.newSubcategory = EditorGUILayout.TextField("Subcategory", editData.newSubcategory);

            EditorGUILayout.EndVertical();

            // Show validation messages
            if (string.IsNullOrEmpty(editData.newName))
            {
                EditorGUILayout.HelpBox("Group name cannot be empty.", MessageType.Error);
            }
            else if (HasNameChanged() && GroupNameExists())
            {
                EditorGUILayout.HelpBox("A group with this name already exists.", MessageType.Warning);
            }
            else if (HasChanges())
            {
                EditorGUILayout.HelpBox("Changes detected. Click Apply to save changes.", MessageType.Info);
            }
        }

        private void DrawGroupInfo()
        {
            EditorGUILayout.LabelField("Group Information", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("Box");

            if (editData.prefab != null)
            {
                // Show child count
                int childCount = editData.prefab.transform.childCount;
                EditorGUILayout.LabelField($"Objects in Group: {childCount}");

                // Show prefab path
                string prefabPath = AssetDatabase.GetAssetPath(editData.prefab);
                EditorGUILayout.LabelField($"Prefab Location: {prefabPath}");

                // Show object types in group
                if (childCount > 0)
                {
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(100));
                    for (int i = 0; i < childCount; i++)
                    {
                        Transform child = editData.prefab.transform.GetChild(i);
                        var childInfo = child.GetComponent<ObjectListInfo>();
                        string objectType = childInfo != null ? childInfo.objectType : "Unknown";

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("📦", GUILayout.Width(20));
                        GUILayout.Label($"{child.name}", EditorStyles.miniLabel);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"({objectType})", EditorStyles.miniLabel);
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Group prefab not found.", MessageType.Error);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawButtons()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
            {
                Close();
            }

            GUILayout.FlexibleSpace();

            // Only show apply button if there are changes
            GUI.enabled = HasChanges() && !string.IsNullOrEmpty(editData.newName) && !GroupNameExists();
            if (GUILayout.Button("✅ Apply Changes", GUILayout.Height(30), GUILayout.Width(120)))
            {
                ApplyChanges();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        private bool HasChanges()
        {
            return editData.newName != editData.originalName ||
                   editData.newCategory != GetOriginalCategory() ||
                   editData.newSubcategory != GetOriginalSubcategory();
        }

        private bool HasNameChanged()
        {
            return editData.newName != editData.originalName;
        }

        private string GetOriginalCategory()
        {
            // Get the original category from the PropAsset - we'll need to get this from the groupInfo
            if (editData.groupInfo != null)
            {
                // For now, assume Groups category is the original
                return "Groups";
            }
            return "Groups";
        }

        private string GetOriginalSubcategory()
        {
            return "Custom Groups";
        }

        private bool GroupNameExists()
        {
            if (!HasNameChanged()) return false;

            string groupsFolder = "Assets/Resources/Groups";
            string newPrefabPath = Path.Combine(groupsFolder, $"{editData.newName}.prefab");

            return File.Exists(newPrefabPath);
        }

        private string ValidateFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return fileName;

            // Remove invalid characters for file names
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            // Also remove some additional problematic characters
            fileName = fileName.Replace('<', '_').Replace('>', '_').Replace(':', '_')
                              .Replace('"', '_').Replace('|', '_').Replace('?', '_')
                              .Replace('*', '_');

            return fileName;
        }

        private void ApplyChanges()
        {
            try
            {
                string groupsFolder = "Assets/Resources/Groups";
                string oldPrefabPath = AssetDatabase.GetAssetPath(editData.prefab);
                string newPrefabPath = Path.Combine(groupsFolder, $"{editData.newName}.prefab");

                // Update the ObjectListInfo component
                if (editData.groupInfo != null)
                {
                    editData.groupInfo.modelPath = $"Groups/{editData.newName}";
                    EditorUtility.SetDirty(editData.groupInfo);
                }

                // Rename the prefab if name changed
                if (HasNameChanged())
                {
                    string result = AssetDatabase.RenameAsset(oldPrefabPath, $"{editData.newName}.prefab");
                    if (!string.IsNullOrEmpty(result))
                    {
                        EditorUtility.DisplayDialog("Error", $"Failed to rename prefab: {result}", "OK");
                        return;
                    }
                }

                // Save changes to prefab
                PrefabUtility.SavePrefabAsset(editData.prefab);
                AssetDatabase.Refresh();

                // Notify that changes were applied
                Debug.Log($"✅ Updated group '{editData.originalName}' -> '{editData.newName}' (Category: {editData.newCategory}, Subcategory: {editData.newSubcategory})");

                // Find and refresh PropBrowserWindow if it's open
                var propBrowserWindows = UnityEngine.Resources.FindObjectsOfTypeAll<PropBrowserWindow>();
                foreach (var window in propBrowserWindows)
                {
                    // Use the new public method to force a complete refresh
                    window.ForceRefreshPropList();
                    window.Repaint();
                }

                EditorUtility.DisplayDialog("Success",
                    $"Group '{editData.newName}' has been updated successfully!", "OK");

                Close();
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Error",
                    $"Failed to apply changes: {ex.Message}", "OK");
                Debug.LogError($"Failed to apply group changes: {ex}");
            }
        }
    }
}