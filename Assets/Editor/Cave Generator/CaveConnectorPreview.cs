using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using POTCO;

namespace CaveGenerator
{
    /// <summary>
    /// Manages preview and cycling of cave pieces at selected connector
    /// </summary>
    public static class CaveConnectorPreview
    {
        private static List<GameObject> cavePieces = new List<GameObject>();
        private static int currentPreviewIndex = -1;
        private static int currentConnectorIndex = 0;
        private static List<Transform> availableConnectors = new List<Transform>();
        private static GameObject previewInstance;
        private static Transform previewConnector;
        private static bool isPreviewActive = false;

        public static bool IsPreviewActive => isPreviewActive;
        public static GameObject CurrentPreviewPiece => currentPreviewIndex >= 0 && currentPreviewIndex < cavePieces.Count ? cavePieces[currentPreviewIndex] : null;
        public static int CurrentIndex => currentPreviewIndex;
        public static int TotalPieces => cavePieces.Count;
        public static int CurrentConnectorIndex => currentConnectorIndex;
        public static int TotalConnectors => availableConnectors.Count;

        static CaveConnectorPreview()
        {
            LoadCavePieces();
        }

        private static void LoadCavePieces()
        {
            cavePieces.Clear();

            // Load all .egg files from cave folder
            string[] eggGuids = AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/Resources" });

            foreach (string guid in eggGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Filter for cave pieces only
                if (path.Contains("pir_m_are_cav") || path.Contains("cave"))
                {
                    GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset != null)
                    {
                        // Verify it has connectors
                        var connectors = asset.GetComponentsInChildren<Transform>(true)
                            .Where(t => t.name.StartsWith("cave_connector_"))
                            .ToList();

                        if (connectors.Count > 0)
                        {
                            cavePieces.Add(asset);
                        }
                    }
                }
            }

            cavePieces = cavePieces.OrderBy(p => p.name).ToList();
            Debug.Log($"🔍 Loaded {cavePieces.Count} cave pieces for preview");
        }

        public static void StartPreview()
        {
            if (!CaveConnectorSelector.HasSelection)
            {
                Debug.LogWarning("No connector selected!");
                return;
            }

            if (cavePieces.Count == 0)
            {
                LoadCavePieces();
            }

            isPreviewActive = true;
            currentPreviewIndex = 0;
            UpdatePreview();

            Debug.Log($"👻 Preview mode activated! Use Arrow Keys to cycle, Enter to place, Escape to cancel");
        }

        public static void NextPiece()
        {
            if (!isPreviewActive || cavePieces.Count == 0) return;

            currentPreviewIndex = (currentPreviewIndex + 1) % cavePieces.Count;
            currentConnectorIndex = 0; // Reset connector selection when changing pieces
            UpdatePreview();
        }

        public static void PreviousPiece()
        {
            if (!isPreviewActive || cavePieces.Count == 0) return;

            currentPreviewIndex--;
            if (currentPreviewIndex < 0)
                currentPreviewIndex = cavePieces.Count - 1;

            currentConnectorIndex = 0; // Reset connector selection when changing pieces
            UpdatePreview();
        }

        public static void NextConnector()
        {
            Debug.Log($"NextConnector called - isPreviewActive: {isPreviewActive}, availableConnectors.Count: {availableConnectors.Count}");

            if (!isPreviewActive || availableConnectors.Count == 0)
            {
                Debug.LogWarning("Cannot cycle connector - preview not active or no connectors available");
                return;
            }

            currentConnectorIndex = (currentConnectorIndex + 1) % availableConnectors.Count;
            UpdatePreview();
            Debug.Log($"🔗 Selected connector {currentConnectorIndex + 1}/{availableConnectors.Count}: {availableConnectors[currentConnectorIndex].name}");
        }

        public static void PreviousConnector()
        {
            Debug.Log($"PreviousConnector called - isPreviewActive: {isPreviewActive}, availableConnectors.Count: {availableConnectors.Count}");

            if (!isPreviewActive || availableConnectors.Count == 0)
            {
                Debug.LogWarning("Cannot cycle connector - preview not active or no connectors available");
                return;
            }

            currentConnectorIndex--;
            if (currentConnectorIndex < 0)
                currentConnectorIndex = availableConnectors.Count - 1;

            UpdatePreview();
            Debug.Log($"🔗 Selected connector {currentConnectorIndex + 1}/{availableConnectors.Count}: {availableConnectors[currentConnectorIndex].name}");
        }

        public static void UpdatePreview()
        {
            if (!isPreviewActive || !CaveConnectorSelector.HasSelection)
            {
                ClearPreview();
                return;
            }

            // Clean up old preview
            if (previewInstance != null)
            {
                Object.DestroyImmediate(previewInstance);
            }

            if (currentPreviewIndex < 0 || currentPreviewIndex >= cavePieces.Count)
            {
                return;
            }

            // Create new preview instance
            GameObject prefab = cavePieces[currentPreviewIndex];
            previewInstance = Object.Instantiate(prefab);
            previewInstance.name = $"[PREVIEW] {prefab.name}";
            previewInstance.hideFlags = HideFlags.HideAndDontSave;

            // Find all connectors on the preview piece
            availableConnectors = previewInstance.GetComponentsInChildren<Transform>()
                .Where(t => t.name.StartsWith("cave_connector_"))
                .OrderBy(t => t.name) // Sort by name for consistency
                .ToList();

            Debug.Log($"🔍 UpdatePreview: Found {availableConnectors.Count} connectors on {prefab.name}");

            if (availableConnectors.Count > 0)
            {
                // Clamp connector index to valid range
                currentConnectorIndex = Mathf.Clamp(currentConnectorIndex, 0, availableConnectors.Count - 1);

                // Use the selected connector
                previewConnector = availableConnectors[currentConnectorIndex];

                Debug.Log($"Using connector {currentConnectorIndex + 1}/{availableConnectors.Count}: {previewConnector.name}");

                // Align preview to selected connector
                AlignPreviewToConnector(previewInstance, CaveConnectorSelector.SelectedConnector, previewConnector);
            }
            else
            {
                Debug.LogWarning($"No connectors found on {prefab.name}!");
            }

            // Make all renderers semi-transparent
            MakePreviewTransparent(previewInstance);

            SceneView.RepaintAll();
        }

        private static void AlignPreviewToConnector(GameObject preview, Transform targetConnector, Transform previewConn)
        {
            // Calculate rotation to face opposite direction
            Quaternion targetRotation = Quaternion.LookRotation(-targetConnector.forward, Vector3.up);
            Quaternion connectorRotation = Quaternion.LookRotation(previewConn.forward, Vector3.up);
            Quaternion requiredRotation = targetRotation * Quaternion.Inverse(connectorRotation);

            // Apply rotation
            preview.transform.rotation = requiredRotation * preview.transform.rotation;

            // Position so connectors align
            Vector3 offset = targetConnector.position - previewConn.position;
            preview.transform.position += offset;
        }

        private static void MakePreviewTransparent(GameObject preview)
        {
            var renderers = preview.GetComponentsInChildren<Renderer>();
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

                        // Get original color and blend with slight cyan tint
                        Color originalColor = transparentMaterials[i].HasProperty("_Color") ?
                            transparentMaterials[i].GetColor("_Color") : Color.white;

                        // Blend 70% original color with 30% cyan tint, and set transparency
                        Color tintColor = new Color(0.3f, 1f, 1f, 1f); // Light cyan tint
                        Color blendedColor = Color.Lerp(originalColor, tintColor, 0.3f); // 30% tint
                        blendedColor.a = 0.5f; // 50% transparency

                        transparentMaterials[i].SetColor("_Color", blendedColor);
                    }
                }

                renderer.sharedMaterials = transparentMaterials;
            }
        }

        public static GameObject PlaceCurrentPreview()
        {
            if (!isPreviewActive || currentPreviewIndex < 0 || currentPreviewIndex >= cavePieces.Count)
            {
                return null;
            }

            GameObject prefab = cavePieces[currentPreviewIndex];
            Transform targetConnector = CaveConnectorSelector.SelectedConnector;

            // Create actual instance
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

            // Find connector on new piece
            var availableConnectors = instance.GetComponentsInChildren<Transform>()
                .Where(t => t.name.StartsWith("cave_connector_"))
                .ToList();

            if (availableConnectors.Count > 0)
            {
                var instanceConnector = availableConnectors[Random.Range(0, availableConnectors.Count)];
                AlignPreviewToConnector(instance, targetConnector, instanceConnector);
            }

            // Add ObjectListInfo component if not already present
            if (instance.GetComponent<ObjectListInfo>() == null)
            {
                var info = instance.AddComponent<ObjectListInfo>();

                // Try to extract model path from prefab
                string prefabPath = AssetDatabase.GetAssetPath(prefab);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    info.modelPath = prefabPath.Replace("Assets/Resources/", "").Replace(".prefab", "");
                }

                // Set a generic cave object type
                info.objectType = "Prop";
            }

            Debug.Log($"✅ Placed {prefab.name} at connector {targetConnector.name}");

            // Clear preview and selection
            ClearPreview();
            CaveConnectorSelector.ClearSelection();

            return instance;
        }

        public static void ClearPreview()
        {
            if (previewInstance != null)
            {
                Object.DestroyImmediate(previewInstance);
                previewInstance = null;
            }

            previewConnector = null;
            isPreviewActive = false;
            currentPreviewIndex = -1;

            SceneView.RepaintAll();
        }

        public static void RefreshCavePieces()
        {
            LoadCavePieces();
        }
    }
}
