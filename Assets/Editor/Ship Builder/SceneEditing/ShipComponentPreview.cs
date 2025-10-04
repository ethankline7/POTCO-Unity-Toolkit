using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace POTCO.ShipBuilder.SceneEditing
{
    /// <summary>
    /// Manages preview and cycling of ship components at selected location
    /// </summary>
    public static class ShipComponentPreview
    {
        private static List<GameObject> availableComponents = new List<GameObject>();
        private static int currentPreviewIndex = -1;
        private static GameObject previewInstance;
        private static bool isPreviewActive = false;
        private static ShipComponentDatabase componentDatabase;
        private static GameObject previewingForComponent; // Track which component we're previewing for

        public static bool IsPreviewActive => isPreviewActive;
        public static GameObject CurrentPreviewComponent => currentPreviewIndex >= 0 && currentPreviewIndex < availableComponents.Count ? availableComponents[currentPreviewIndex] : null;
        public static int CurrentIndex => currentPreviewIndex;
        public static int TotalComponents => availableComponents.Count;

        static ShipComponentPreview()
        {
            componentDatabase = new ShipComponentDatabase();
            componentDatabase.Initialize();
        }

        public static void StartPreview()
        {
            if (!ShipComponentSelector.HasSelection)
            {
                Debug.LogWarning("No ship component selected!");
                return;
            }

            // Store which component we're previewing for
            previewingForComponent = ShipComponentSelector.SelectedComponent;

            LoadAvailableComponents();

            if (availableComponents.Count == 0)
            {
                Debug.LogWarning($"No components found for type: {ShipComponentSelector.SelectedType}");
                return;
            }

            isPreviewActive = true;

            // Find current component in the list
            GameObject currentComponent = ShipComponentSelector.SelectedComponent;
            string currentName = GetComponentModelName(currentComponent);

            Debug.Log($"🔍 Looking for model name: '{currentName}' in {availableComponents.Count} available components");

            currentPreviewIndex = availableComponents.FindIndex(c => c.name == currentName);
            if (currentPreviewIndex < 0)
            {
                Debug.LogWarning($"⚠️ Could not find '{currentName}' in component list, defaulting to first component");
                currentPreviewIndex = 0;
            }
            else
            {
                Debug.Log($"✅ Found '{currentName}' at index {currentPreviewIndex}");
            }

            UpdatePreview();

            Debug.Log($"👻 Preview mode activated! Use ← → to cycle components, Enter to apply, Escape to cancel");
            Debug.Log($"📦 {availableComponents.Count} components available for {ShipComponentSelector.SelectedType}");
        }

        private static void LoadAvailableComponents()
        {
            availableComponents.Clear();

            string prefix = ShipComponentSelector.GetComponentPrefix();
            if (string.IsNullOrEmpty(prefix))
            {
                Debug.LogWarning("Could not determine component prefix");
                return;
            }

            Debug.Log($"🔍 Loading components with prefix: '{prefix}' for type: {ShipComponentSelector.SelectedType}");

            string[] componentNames = componentDatabase.GetComponentsByPrefix(prefix);
            Debug.Log($"📋 Found {componentNames.Length} component names: {string.Join(", ", componentNames)}");

            foreach (string componentName in componentNames)
            {
                // Skip the <None> entry
                if (componentName == "<None>") continue;

                GameObject prefab = componentDatabase.GetComponentPrefab(componentName);
                if (prefab != null)
                {
                    availableComponents.Add(prefab);
                    Debug.Log($"  ✅ Loaded prefab: {prefab.name}");
                }
                else
                {
                    Debug.LogWarning($"  ❌ Failed to load prefab: {componentName}");
                }
            }

            availableComponents = availableComponents.OrderBy(c => c.name).ToList();
            Debug.Log($"🎯 Final component list: {availableComponents.Count} prefabs ready for preview");
        }

        public static void NextComponent()
        {
            if (!isPreviewActive || availableComponents.Count == 0) return;

            // Check if selection changed since we started preview
            if (ValidatePreviewSelection())
            {
                currentPreviewIndex = (currentPreviewIndex + 1) % availableComponents.Count;
                UpdatePreview();
                Debug.Log($"➡️ Next: {CurrentPreviewComponent?.name} ({currentPreviewIndex + 1}/{availableComponents.Count})");
            }
        }

        public static void PreviousComponent()
        {
            if (!isPreviewActive || availableComponents.Count == 0) return;

            // Check if selection changed since we started preview
            if (ValidatePreviewSelection())
            {
                currentPreviewIndex--;
                if (currentPreviewIndex < 0)
                    currentPreviewIndex = availableComponents.Count - 1;

                UpdatePreview();
                Debug.Log($"⬅️ Previous: {CurrentPreviewComponent?.name} ({currentPreviewIndex + 1}/{availableComponents.Count})");
            }
        }

        private static bool ValidatePreviewSelection()
        {
            // Check if the selected component is still the same one we started previewing for
            if (previewingForComponent != ShipComponentSelector.SelectedComponent)
            {
                Debug.LogWarning($"⚠️ Selection changed during preview! Restarting preview for new component.");
                ClearPreview();
                StartPreview();
                return false; // Don't continue with the current cycle operation
            }
            return true;
        }

        public static void UpdatePreview()
        {
            if (!isPreviewActive || !ShipComponentSelector.HasSelection)
            {
                ClearPreview();
                return;
            }

            // Clean up old preview - check for any existing preview objects
            CleanupAllPreviews();

            if (currentPreviewIndex < 0 || currentPreviewIndex >= availableComponents.Count)
            {
                Debug.LogWarning($"Invalid preview index: {currentPreviewIndex} (total: {availableComponents.Count})");
                return;
            }

            // Get the selected component's transform info
            GameObject selectedComponent = ShipComponentSelector.SelectedComponent;
            if (selectedComponent == null) return;

            // Create new preview instance
            GameObject prefab = availableComponents[currentPreviewIndex];
            Debug.Log($"🔨 Creating preview of '{prefab.name}' at {selectedComponent.name}");

            previewInstance = Object.Instantiate(prefab);
            previewInstance.name = $"[PREVIEW] {prefab.name}";
            previewInstance.hideFlags = HideFlags.HideAndDontSave;

            // Position preview at the same location as selected component
            previewInstance.transform.position = selectedComponent.transform.position;
            previewInstance.transform.rotation = selectedComponent.transform.rotation;
            previewInstance.transform.localScale = selectedComponent.transform.localScale;

            // If selected component has a parent, parent the preview to the same
            if (selectedComponent.transform.parent != null)
            {
                previewInstance.transform.SetParent(selectedComponent.transform.parent);
                previewInstance.transform.SetSiblingIndex(selectedComponent.transform.GetSiblingIndex());
            }

            // Make preview semi-transparent
            MakePreviewTransparent(previewInstance);

            // Hide the original component temporarily
            selectedComponent.SetActive(false);

            SceneView.RepaintAll();
        }

        private static void CleanupAllPreviews()
        {
            // Clean up the tracked preview instance
            if (previewInstance != null)
            {
                Object.DestroyImmediate(previewInstance);
                previewInstance = null;
            }

            // Only do a full scan if we're in preview mode (to catch any orphaned previews)
            if (isPreviewActive)
            {
                // Clean up any orphaned preview objects
                GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (GameObject obj in allObjects)
                {
                    if (obj != null && obj.name.StartsWith("[PREVIEW]"))
                    {
                        Debug.LogWarning($"🧹 Cleaning up orphaned preview: {obj.name}");
                        Object.DestroyImmediate(obj);
                    }
                }
            }
        }

        private static void MakePreviewTransparent(GameObject preview)
        {
            var renderers = preview.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                var transparentMaterials = new Material[materials.Length];

                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null)
                    {
                        transparentMaterials[i] = new Material(materials[i]);

                        // Set to transparent mode
                        transparentMaterials[i].SetFloat("_Mode", 3); // Transparent
                        transparentMaterials[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        transparentMaterials[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        transparentMaterials[i].SetInt("_ZWrite", 0);
                        transparentMaterials[i].DisableKeyword("_ALPHATEST_ON");
                        transparentMaterials[i].EnableKeyword("_ALPHABLEND_ON");
                        transparentMaterials[i].DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        transparentMaterials[i].renderQueue = 3000;

                        // Get original color and blend with cyan tint
                        Color originalColor = transparentMaterials[i].HasProperty("_Color") ?
                            transparentMaterials[i].GetColor("_Color") : Color.white;

                        // Blend 70% original with 30% cyan tint
                        Color tintColor = new Color(0.3f, 1f, 1f, 1f);
                        Color blendedColor = Color.Lerp(originalColor, tintColor, 0.3f);
                        blendedColor.a = 0.6f; // 60% transparency

                        transparentMaterials[i].SetColor("_Color", blendedColor);
                    }
                }

                renderer.sharedMaterials = transparentMaterials;
            }
        }

        public static void ApplyCurrentPreview()
        {
            if (!isPreviewActive || currentPreviewIndex < 0 || currentPreviewIndex >= availableComponents.Count)
            {
                return;
            }

            GameObject selectedComponent = ShipComponentSelector.SelectedComponent;
            if (selectedComponent == null) return;

            GameObject newComponent = availableComponents[currentPreviewIndex];

            // Swap the component
            SwapComponent(selectedComponent, newComponent);

            Debug.Log($"✅ Applied {newComponent.name} to {ShipComponentSelector.SelectedLocatorName}");

            // Clear preview and selection
            ClearPreview();
            ShipComponentSelector.ClearSelection();
        }

        private static void SwapComponent(GameObject oldComponent, GameObject newComponentPrefab)
        {
            // Store transform info
            Transform parent = oldComponent.transform.parent;
            Vector3 position = oldComponent.transform.position;
            Quaternion rotation = oldComponent.transform.rotation;
            Vector3 scale = oldComponent.transform.localScale;
            int siblingIndex = oldComponent.transform.GetSiblingIndex();
            string oldName = oldComponent.name;

            // Instantiate new component
            GameObject newComponent = PrefabUtility.InstantiatePrefab(newComponentPrefab) as GameObject;

            // Apply transform
            newComponent.transform.SetParent(parent);
            newComponent.transform.position = position;
            newComponent.transform.rotation = rotation;
            newComponent.transform.localScale = scale;
            newComponent.transform.SetSiblingIndex(siblingIndex);
            newComponent.name = oldName; // Keep the locator name

            // Mark for undo
            Undo.RegisterCreatedObjectUndo(newComponent, "Swap Ship Component");
            Undo.DestroyObjectImmediate(oldComponent);

            // Select the new component
            Selection.activeGameObject = newComponent;
        }

        private static string GetComponentModelName(GameObject component)
        {
            // Try to find the actual model name from children, excluding collisions
            foreach (Transform child in component.transform)
            {
                string childName = child.name;
                if (childName.StartsWith("pir_") &&
                    !childName.Contains("_collision"))
                {
                    return childName;
                }
            }

            // Check the component itself
            if (component.name.StartsWith("pir_") &&
                !component.name.Contains("_collision"))
            {
                return component.name;
            }

            // For location_ components, return the name as-is
            return component.name;
        }

        public static void ClearPreview()
        {
            // Use the comprehensive cleanup method
            CleanupAllPreviews();

            // Restore original component visibility
            if (ShipComponentSelector.HasSelection && ShipComponentSelector.SelectedComponent != null)
            {
                ShipComponentSelector.SelectedComponent.SetActive(true);
            }

            isPreviewActive = false;
            currentPreviewIndex = -1;
            previewingForComponent = null;
            availableComponents.Clear();

            SceneView.RepaintAll();
        }
    }
}
