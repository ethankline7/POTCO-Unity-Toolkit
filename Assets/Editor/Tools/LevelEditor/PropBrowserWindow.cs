using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using POTCO;
using WorldDataExporter.Utilities;

namespace POTCO.Editor
{
    // Category display is always tabbed - no need for enum with single value

    public class PropBrowserWindow : EditorWindow
    {
        private class PropAsset
        {
            public string name;
            public string path;
            public string category;
            public string subcategory; // New: more detailed organization
            public GameObject prefab;
            public Texture2D thumbnail;
            public bool isFavorite;
            public int useCount;
            public string searchableText;
            public bool thumbnailRequested; // New: track thumbnail requests
            public string objectType; // New: from ObjectList.py

            // Group-specific properties
            public bool isGroup;
            public List<GroupCreationDialog.GroupItem> groupItems;
            public string customThumbnailPath;
            public GroupCreationDialog.PivotType pivotType = GroupCreationDialog.PivotType.Bottom;
            public Vector3 customPivotOffset;
        }

        private class CategoryData
        {
            public string name;
            public List<PropAsset> props = new List<PropAsset>();
            public Dictionary<string, List<PropAsset>> subcategories = new Dictionary<string, List<PropAsset>>();
            public bool isExpanded = true;
        }

        // UI State
        private Vector2 scrollPosition;
        private Vector2 categoryScrollPosition;
        private string searchText = "";
        private string selectedCategory = "All";
        private string selectedSubcategory = "";
        private int thumbnailSize = 64;
        private bool showFavoritesOnly = false;
        private PropAsset selectedProp;
        private bool showEggFiles = true; // Default: show .egg files instead of prefabs
        private bool useObjectListCategories = true; // Default: use ObjectList.py for categorization
        // Category display is now always tabbed
        
        // Performance optimizations
        private List<PropAsset> visibleProps = new List<PropAsset>(); // Only visible items
        private Dictionary<string, CategoryData> categoryData = new Dictionary<string, CategoryData>();
        private HashSet<string> expandedCategories = new HashSet<string>();
        private int visibleStartIndex = 0;
        private int visibleEndIndex = 0;
        private float itemHeight = 80f;
        private int itemsPerRow = 1;
        
        // Data
        private List<PropAsset> allProps = new List<PropAsset>();
        private List<PropAsset> filteredProps = new List<PropAsset>();
        private HashSet<string> favoriteProps = new HashSet<string>();
        private Dictionary<string, int> propUsageCounts = new Dictionary<string, int>();

        // Caching
        private bool needsRefresh = true;
        private bool needsFilterRefresh = true;
        private Dictionary<string, Texture2D> thumbnailCache = new Dictionary<string, Texture2D>();

        // Disk-based thumbnail cache
        private static readonly string CACHE_FOLDER = "Assets/Cache/ObjectBrowserCache";
        private static readonly string CACHE_VERSION_FILE = "Assets/Cache/ObjectBrowserCache/version.txt";
        private static readonly string CURRENT_CACHE_VERSION = "1.0.0";
        private bool thumbnailCacheLoaded = false;

        [MenuItem("POTCO/Level Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<PropBrowserWindow>("Level Editor: Object Browser");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnEnable()
        {
            LoadPreferences();
            LoadCachedThumbnails();
            needsRefresh = true;
        }

        private void OnDisable()
        {
            SavePreferences();
        }

        private void OnGUI()
        {
            if (needsRefresh)
            {
                RefreshPropList();
                needsRefresh = false;
            }

            if (needsFilterRefresh)
            {
                FilterAndOrganizeProps();
                needsFilterRefresh = false;
            }

            Draw();
            
            // Handle lazy thumbnail loading (throttled)
            if (Event.current.type == EventType.Repaint)
            {
                HandleLazyThumbnailLoading();
            }
        }

        private void Draw()
        {
            EditorGUILayout.BeginVertical();

            DrawToolbar();
            EditorGUILayout.Space(5);
            
            EditorGUILayout.BeginHorizontal();
            DrawCategoryPanel();
            DrawVirtualizedPropGrid();
            EditorGUILayout.EndHorizontal();

            DrawStatusBar();

            EditorGUILayout.EndVertical();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Asset type toggle
            EditorGUI.BeginChangeCheck();
            showEggFiles = GUILayout.Toggle(showEggFiles, showEggFiles ? "🥚 .egg" : "📦 Prefab", EditorStyles.toolbarButton, GUILayout.Width(70));
            if (EditorGUI.EndChangeCheck())
            {
                RefreshPropList(); // Full refresh needed when switching asset types
            }

            GUILayout.Space(5);

            // ObjectList categorization toggle
            EditorGUI.BeginChangeCheck();
            useObjectListCategories = GUILayout.Toggle(useObjectListCategories, useObjectListCategories ? "📖 ObjectList" : "📁 Path", EditorStyles.toolbarButton, GUILayout.Width(80));
            if (EditorGUI.EndChangeCheck())
            {
                RefreshPropList(); // Full refresh needed when switching categorization
            }

            GUILayout.Space(5);

            // Search field
            EditorGUI.BeginChangeCheck();
            searchText = EditorGUILayout.TextField(searchText, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            if (EditorGUI.EndChangeCheck())
            {
                needsFilterRefresh = true;
            }

            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                searchText = "";
                needsFilterRefresh = true;
                GUI.FocusControl(null);
            }

            GUILayout.FlexibleSpace();

            // View options
            EditorGUI.BeginChangeCheck();
            showFavoritesOnly = GUILayout.Toggle(showFavoritesOnly, "⭐ Favorites", EditorStyles.toolbarButton);
            if (EditorGUI.EndChangeCheck())
            {
                needsFilterRefresh = true;
            }


            // Surface placement toggle
            EditorGUI.BeginChangeCheck();
            bool surfaceToolEnabled = SurfacePlacementTool.IsEnabled;
            surfaceToolEnabled = GUILayout.Toggle(surfaceToolEnabled, "🎯 Surface", EditorStyles.toolbarButton);
            if (EditorGUI.EndChangeCheck())
            {
                if (surfaceToolEnabled)
                    SurfacePlacementTool.Enable();
                else
                    SurfacePlacementTool.Disable();
            }

            // Thumbnail size slider
            GUILayout.Label("Size:", EditorStyles.miniLabel);
            EditorGUI.BeginChangeCheck();
            thumbnailSize = (int)GUILayout.HorizontalSlider(thumbnailSize, 32, 128, GUILayout.Width(60));
            if (EditorGUI.EndChangeCheck())
            {
                itemHeight = thumbnailSize + 20f;
                CalculateVisibleRange();
            }

            // Refresh thumbnails button
            if (GUILayout.Button(new GUIContent("🔄", "Refresh all thumbnails"), EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                if (Event.current.shift)
                {
                    // Shift+Click = Clear cache and regenerate all thumbnails
                    ClearThumbnailCache();
                    RefreshPropList();
                    EditorUtility.DisplayDialog("Thumbnail Cache Cleared",
                        "All thumbnails will be regenerated. This may take a moment.", "OK");
                }
                else
                {
                    RefreshPropList();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCategoryPanel()
        {
            DrawTabbedCategoryPanel();
        }

        #region Category Display Styles


        private void DrawTabbedCategoryPanel()
        {
            EditorGUILayout.BeginVertical("Box", GUILayout.Width(200));
            
            // Header
            EditorGUILayout.BeginHorizontal("Toolbar");
            GUILayout.Label("📑 Categories", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            
            categoryScrollPosition = EditorGUILayout.BeginScrollView(categoryScrollPosition, GUILayout.Height(position.height - 120));

            // All tab
            bool allSelected = selectedCategory == "All";
            EditorGUILayout.BeginHorizontal();
            if (allSelected) GUI.backgroundColor = new Color(0.8f, 0.9f, 1f);
            if (GUILayout.Button($"🌐 All ({allProps.Count})", EditorStyles.toolbarButton))
            {
                selectedCategory = "All";
                selectedSubcategory = "";
                needsFilterRefresh = true;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // Separator line
            GUILayout.Box("", "horizontalslider", GUILayout.Height(1), GUILayout.ExpandWidth(true));
            EditorGUILayout.Space(2);

            // Category tabs - arranged in horizontal groups when possible
            var categories = categoryData.OrderBy(x => x.Key).ToList();
            for (int i = 0; i < categories.Count; i++)
            {
                var categoryKvp = categories[i];
                string categoryName = categoryKvp.Key;
                var data = categoryKvp.Value;
                string icon = GetCategoryIcon(categoryName);
                bool isCategorySelected = selectedCategory == categoryName;
                bool isMainCategorySelected = isCategorySelected && string.IsNullOrEmpty(selectedSubcategory);
                
                EditorGUILayout.BeginHorizontal();
                if (isMainCategorySelected) GUI.backgroundColor = new Color(0.8f, 1f, 0.8f);
                else if (isCategorySelected) GUI.backgroundColor = new Color(0.7f, 0.9f, 0.7f); // Slightly dimmer when subcategory is selected
                if (GUILayout.Button($"{icon} {categoryName} ({data.props.Count})", EditorStyles.toolbarButton))
                {
                    selectedCategory = categoryName;
                    selectedSubcategory = "";
                    needsFilterRefresh = true;
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                
                // Show subcategories for the selected category (stays visible even when subcategory is selected)
                if (isCategorySelected && data.subcategories.Count > 0)
                {
                    EditorGUILayout.BeginVertical("Box");
                    GUILayout.Label("Subcategories:", EditorStyles.miniLabel);
                    
                    foreach (var subKvp in data.subcategories.OrderBy(x => x.Key))
                    {
                        string subcategoryName = subKvp.Key;
                        var subProps = subKvp.Value;
                        bool isSubSelected = selectedSubcategory == subcategoryName;
                        
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(10); // Indent subcategories
                        if (isSubSelected) GUI.backgroundColor = new Color(0.9f, 0.9f, 1f);
                        if (GUILayout.Button($"▸ {subcategoryName} ({subProps.Count})", EditorStyles.miniButton, GUILayout.Height(18)))
                        {
                            selectedSubcategory = subcategoryName;
                            needsFilterRefresh = true;
                        }
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
                
                // Add small separator between tabs
                if (i < categories.Count - 1)
                {
                    EditorGUILayout.Space(1);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }



        #endregion

        private void DrawHierarchicalCategory(string categoryName, CategoryData data)
        {
            // Get category icon
            string icon = GetCategoryIcon(categoryName);
            
            EditorGUILayout.BeginVertical();
            
            // Main category row
            EditorGUILayout.BeginHorizontal();
            
            // Expand/collapse button (only if has subcategories)
            bool hasSubcategories = data.subcategories.Count > 0;
            bool wasExpanded = expandedCategories.Contains(categoryName);
            
            if (hasSubcategories)
            {
                bool isExpanded = EditorGUILayout.Foldout(wasExpanded, "");
                if (isExpanded != wasExpanded)
                {
                    if (isExpanded)
                        expandedCategories.Add(categoryName);
                    else
                        expandedCategories.Remove(categoryName);
                }
            }
            else
            {
                GUILayout.Space(15);
            }

            // Category button with icon
            bool isSelected = selectedCategory == categoryName && string.IsNullOrEmpty(selectedSubcategory);
            GUIStyle categoryStyle = isSelected ? "MiniButtonMid" : "MiniButton";
            if (isSelected) GUI.backgroundColor = Color.green;
            
            if (GUILayout.Button($"{icon} {categoryName} ({data.props.Count})", categoryStyle, GUILayout.Height(20)))
            {
                selectedCategory = categoryName;
                selectedSubcategory = "";
                needsFilterRefresh = true;
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            // Draw subcategories if expanded
            if (hasSubcategories && wasExpanded)
            {
                EditorGUILayout.BeginVertical();
                foreach (var subKvp in data.subcategories.OrderBy(x => x.Key))
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(20); // Indent
                    
                    bool subSelected = selectedCategory == categoryName && selectedSubcategory == subKvp.Key;
                    GUIStyle subStyle = subSelected ? "MiniButtonRight" : "MiniButton";
                    if (subSelected) GUI.backgroundColor = Color.yellow;
                    
                    if (GUILayout.Button($"├─ {subKvp.Key} ({subKvp.Value.Count})", subStyle, GUILayout.Height(18)))
                    {
                        selectedCategory = categoryName;
                        selectedSubcategory = subKvp.Key;
                        needsFilterRefresh = true;
                    }
                    GUI.backgroundColor = Color.white;
                    
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private string GetCategoryIcon(string categoryName)
        {
            switch (categoryName.ToLower())
            {
                case "buildings": return "🏠";
                case "ships": return "⛵";
                case "weapons": return "⚔️";
                case "characters": return "👤";
                case "caves": return "🕳️";
                case "effects": return "✨";
                case "environment": return "🌿";
                case "props": return "📦";
                default: return "📁";
            }
        }

        private bool IsAnimationAsset(GameObject asset, string path)
        {
            if (asset == null) return false;
            
            string lowerPath = path.ToLower();
            string lowerName = asset.name.ToLower();
            
            // Check path indicators
            if (lowerPath.Contains("_anim") || lowerPath.Contains("-anim") ||
                lowerPath.Contains("/anim") || lowerPath.Contains("\\anim") ||
                lowerPath.Contains("animation") || lowerPath.Contains("/animations/") ||
                lowerPath.Contains("\\animations\\"))
            {
                return true;
            }
            
            // Check name indicators
            if (lowerName.Contains("_anim") || lowerName.Contains("-anim") ||
                lowerName.Contains("animation") || lowerName.EndsWith("anim"))
            {
                return true;
            }
            
            // Check if GameObject has Animation component (likely an animation-only asset)
            var animationComponent = asset.GetComponent<Animation>();
            if (animationComponent != null)
            {
                // If it only has Animation component and no renderers, it's likely animation-only
                var renderers = asset.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                {
                    return true;
                }
            }
            
            return false;
        }

        private bool IsSimplePlane(GameObject asset)
        {
            if (asset == null) return false;
            
            // Check all mesh filters in the asset and its children
            var meshFilters = asset.GetComponentsInChildren<MeshFilter>();
            
            if (meshFilters.Length == 0) return false;
            
            // If asset only has simple plane-like meshes, exclude it
            bool hasComplexMesh = false;
            
            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.sharedMesh == null) continue;
                
                Mesh mesh = meshFilter.sharedMesh;
                int vertexCount = mesh.vertexCount;
                int triangleCount = mesh.triangles.Length / 3;
                
                // Consider it a simple plane if:
                // - Very few vertices (4-6 for a plane)
                // - Very few triangles (2 for a plane)
                // - Mesh name suggests it's a plane
                string meshName = mesh.name.ToLower();
                
                if (vertexCount <= 6 && triangleCount <= 2)
                {
                    // Additional checks for plane-like names
                    if (meshName.Contains("plane") || meshName.Contains("quad") || 
                        meshName.Contains("flat") || meshName.Contains("ground") ||
                        meshName == "default" || meshName == "primitive")
                    {
                        continue; // This is likely a simple plane
                    }
                }
                
                // If we find a mesh with more complexity, it's not just a simple plane
                if (vertexCount > 6 || triangleCount > 2)
                {
                    hasComplexMesh = true;
                    break;
                }
            }
            
            // If no complex meshes found, this is likely just simple planes
            return !hasComplexMesh;
        }

        private bool IsExcludedPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            
            string lowerPath = path.ToLower();
            
            // Exclude specific model subdirectories
            string[] excludedPaths = {
                "/models/effects/",
                "\\models\\effects\\",
                "/models/fonts/",
                "\\models\\fonts\\",
                "/models/gui/",
                "\\models\\gui\\",
                "/models/texturecards/",
                "\\models\\texturecards\\",
                "/effects/",
                "\\effects\\",
                "/fonts/",
                "\\fonts\\",
                "/gui/",
                "\\gui\\",
                "/texturecards/",
                "\\texturecards\\"
            };
            
            foreach (string excludedPath in excludedPaths)
            {
                if (lowerPath.Contains(excludedPath))
                {
                    return true;
                }
            }
            
            // Also exclude files that end with these patterns
            if (lowerPath.Contains("texturecard") || lowerPath.Contains("texture_card") ||
                lowerPath.Contains("effect_") || lowerPath.Contains("font_") ||
                lowerPath.Contains("gui_") || lowerPath.Contains("_gui"))
            {
                return true;
            }
            
            return false;
        }

        private void DrawVirtualizedPropGrid()
        {
            EditorGUILayout.BeginVertical();

            if (filteredProps.Count == 0)
            {
                DrawEmptyState();
            }
            else
            {
                DrawVirtualizedGrid();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawVirtualizedGrid()
        {
            // Calculate layout
            float availableWidth = position.width - 220f; // Account for category panel
            itemsPerRow = Mathf.Max(1, (int)(availableWidth / (thumbnailSize + 10)));
            itemHeight = thumbnailSize + 40f;

            // Simple scrollable grid without virtualization for now to fix GUI errors
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            int currentRow = 0;
            for (int i = 0; i < filteredProps.Count; i += itemsPerRow)
            {
                EditorGUILayout.BeginHorizontal();
                
                for (int j = 0; j < itemsPerRow && (i + j) < filteredProps.Count; j++)
                {
                    DrawPropItem(filteredProps[i + j]);
                }
                
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                currentRow++;
            }

            EditorGUILayout.EndScrollView();
        }

        private void CalculateVisibleRange()
        {
            if (filteredProps.Count == 0) 
            {
                visibleStartIndex = 0;
                visibleEndIndex = 0;
                return;
            }
            
            // For now, just show all items to avoid GUI layout issues
            visibleStartIndex = 0;
            visibleEndIndex = filteredProps.Count - 1;
        }

        private void DrawPropItem(PropAsset prop)
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(thumbnailSize + 10), GUILayout.Height(thumbnailSize + 40));

            // Thumbnail with lazy loading
            Rect thumbnailRect = GUILayoutUtility.GetRect(thumbnailSize, thumbnailSize);
            DrawLazyThumbnail(prop, thumbnailRect);

            // Handle interactions
            HandlePropItemInteraction(prop, thumbnailRect);

            // Name and favorite button
            EditorGUILayout.BeginHorizontal();

            string displayName = prop.name;
            if (displayName.Length > 12)
            {
                displayName = displayName.Substring(0, 9) + "...";
            }

            // Add group indicator
            if (prop.isGroup)
            {
                displayName = "📦 " + displayName;
                GUI.backgroundColor = new Color(0.8f, 0.9f, 1f, 0.8f); // More visible blue tint
            }

            GUILayout.Label(displayName, EditorStyles.miniLabel);
            GUI.backgroundColor = Color.white;
            
            // Favorite toggle
            bool wasFavorite = prop.isFavorite;
            prop.isFavorite = GUILayout.Toggle(prop.isFavorite, wasFavorite ? "⭐" : "☆", EditorStyles.miniButton, GUILayout.Width(20));
            
            if (prop.isFavorite != wasFavorite)
            {
                if (prop.isFavorite)
                    favoriteProps.Add(prop.path);
                else
                    favoriteProps.Remove(prop.path);
                
                SavePreferences();
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawLazyThumbnail(PropAsset prop, Rect thumbnailRect)
        {
            // Check cache first
            if (thumbnailCache.TryGetValue(prop.path, out Texture2D cachedThumbnail) && cachedThumbnail != null)
            {
                GUI.DrawTexture(thumbnailRect, cachedThumbnail, ScaleMode.ScaleToFit);
                return;
            }

            // Check if prop has valid thumbnail
            if (prop.thumbnail != null)
            {
                GUI.DrawTexture(thumbnailRect, prop.thumbnail, ScaleMode.ScaleToFit);
                thumbnailCache[prop.path] = prop.thumbnail;
                return;
            }

            // Show placeholder
            string placeholderText = prop.thumbnailRequested ? "⏳" : "📷";
            GUI.Box(thumbnailRect, placeholderText, EditorStyles.centeredGreyMiniLabel);
        }

        private void HandleLazyThumbnailLoading()
        {
            // Limit processing to avoid spam
            const int maxProcessPerFrame = 3;
            int processed = 0;
            bool needsRepaint = false;

            foreach (var prop in filteredProps)
            {
                if (processed >= maxProcessPerFrame) break;

                // Skip if already has thumbnail or is cached
                if (prop.thumbnail != null || thumbnailCache.ContainsKey(prop.path)) continue;

                if (!prop.thumbnailRequested)
                {
                    // First check disk cache
                    Texture2D cachedTexture = LoadCachedThumbnail(prop.path);
                    if (cachedTexture != null)
                    {
                        prop.thumbnail = cachedTexture;
                        thumbnailCache[prop.path] = cachedTexture;
                        prop.thumbnailRequested = true;
                        needsRepaint = true;
                        continue;
                    }

                    // Request thumbnail generation if not cached
                    if (prop.prefab != null)
                    {
                        // Always use custom thumbnail generation
                        Texture2D customThumbnail = PropBrowserWindow.GenerateCustomThumbnail(prop.prefab);
                        if (customThumbnail != null)
                        {
                            prop.thumbnail = customThumbnail;
                            thumbnailCache[prop.path] = customThumbnail;
                            // Save to disk cache
                            SaveThumbnailToCache(prop.path, customThumbnail);
                        }
                        prop.thumbnailRequested = true;
                        processed++;
                        needsRepaint = true;
                    }
                }
            }
            
            // Only repaint if we actually need to and not too frequently
            if (needsRepaint && Time.realtimeSinceStartup % 0.3f < 0.1f)
            {
                Repaint();
            }
        }

        /// <summary>
        /// Generate custom thumbnail using Unity's exact default settings but with custom camera angle
        /// </summary>
        public static Texture2D GenerateCustomThumbnail(GameObject prefab)
        {
            if (prefab == null) return null;

            // Create temporary scene for thumbnail generation (isolated from main scene)
            var tempScene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            GameObject tempInstance = null;
            
            try
            {
                // Create temporary instance in the isolated scene
                tempInstance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (tempInstance == null) return null;
                
                // Move the instance to the temporary scene
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(tempInstance, tempScene);

                // Use Unity's exact bounds calculation method
                Bounds bounds = new Bounds();
                bool hasBounds = false;
                var renderers = tempInstance.GetComponentsInChildren<Renderer>();
                
                foreach (var renderer in renderers)
                {
                    if (!hasBounds)
                    {
                        bounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }

                if (!hasBounds || bounds.size.magnitude < 0.0001f)
                {
                    DestroyImmediate(tempInstance);
                    return null;
                }

                // Create camera with Unity's exact default settings
                GameObject cameraGO = new GameObject("ThumbnailCamera");
                Camera camera = cameraGO.AddComponent<Camera>();
                
                // Move camera to the temporary scene as well
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGO, tempScene);
                
                // Unity's exact camera configuration
                camera.cameraType = CameraType.Preview;
                camera.scene = tempScene;
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.Color;
                camera.backgroundColor = new Color(0.35f, 0.35f, 0.35f, 1f); // Brighter background for better visibility
                camera.cullingMask = -1;
                camera.orthographic = false;
                camera.fieldOfView = 15f; // Unity's actual FOV for thumbnails (much tighter than 60)
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 2000f; // Moderate draw distance to prevent large props from being cut off
                
                // Add lighting for better visibility
                GameObject keyLight = new GameObject("ThumbnailKeyLight");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(keyLight, tempScene);
                Light keyLightComp = keyLight.AddComponent<Light>();
                keyLightComp.type = LightType.Directional;
                keyLightComp.color = Color.white;
                keyLightComp.intensity = 0.5f; // Bright key light
                keyLight.transform.rotation = Quaternion.Euler(45f, -30f, 0f); // From above and slightly to the side
                
                // Add fill light to reduce harsh shadows
                GameObject fillLight = new GameObject("ThumbnailFillLight");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fillLight, tempScene);
                Light fillLightComp = fillLight.AddComponent<Light>();
                fillLightComp.type = LightType.Directional;
                fillLightComp.color = new Color(0.8f, 0.9f, 1f, 1f); // Slightly blue fill light
                fillLightComp.intensity = 0.2f; // Softer fill light
                fillLight.transform.rotation = Quaternion.Euler(-15f, 60f, 0f); // From below and opposite side
                
                // Perfect framing calculation for any object size
                Vector3 boundsSize = bounds.size;
                
                // Calculate distance needed to perfectly frame the object
                float maxDimension = Mathf.Max(boundsSize.x, boundsSize.y, boundsSize.z);
                float halfFOV = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
                float distance = (maxDimension * 0.6f) / Mathf.Tan(halfFOV); // Perfect framing with slight padding
                
                // Custom camera angle (front-facing with slight side angle looking up)
                Vector3 cameraDirection = new Vector3(0.3f, -0.5f, 1f).normalized; // Front view with side angle from below looking up
                Vector3 cameraPos = bounds.center - cameraDirection * distance;
                
                cameraGO.transform.position = cameraPos;
                cameraGO.transform.LookAt(bounds.center, Vector3.up);

                // Medium resolution render texture for balanced performance/quality
                RenderTexture renderTexture = new RenderTexture(96, 96, 24, RenderTextureFormat.ARGB32);
                renderTexture.antiAliasing = 1;
                
                camera.targetTexture = renderTexture;
                
                // Render exactly like Unity does
                camera.Render();
                
                // Convert to Texture2D with medium resolution
                RenderTexture.active = renderTexture;
                Texture2D thumbnail = new Texture2D(96, 96, TextureFormat.RGB24, false);
                thumbnail.ReadPixels(new Rect(0, 0, 96, 96), 0, 0);
                thumbnail.Apply();
                
                // Cleanup
                RenderTexture.active = null;
                camera.targetTexture = null;
                DestroyImmediate(renderTexture);
                DestroyImmediate(cameraGO);
                
                return thumbnail;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to generate custom thumbnail for {prefab.name}: {ex.Message}");
                return null;
            }
            finally
            {
                // Always clean up the temporary scene
                if (tempInstance != null)
                {
                    DestroyImmediate(tempInstance);
                }
                
                if (tempScene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(tempScene);
                }
            }
        }


        /// <summary>
        /// Calculate the bounds of an object including all child renderers
        /// </summary>
        private Bounds CalculateObjectBounds(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds();

            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        private void HandlePropItemInteraction(PropAsset prop, Rect thumbnailRect)
        {
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && thumbnailRect.Contains(evt.mousePosition))
            {
                selectedProp = prop;
                
                if (evt.clickCount == 2)
                {
                    PlacePropInScene(prop);
                    evt.Use();
                }
                else if (evt.button == 0)
                {
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = new UnityEngine.Object[] { prop.prefab };
                    DragAndDrop.SetGenericData("PropAsset", prop);
                    DragAndDrop.StartDrag(prop.name);
                    evt.Use();
                }
                else if (evt.button == 1)
                {
                    ShowPropContextMenu(prop);
                    evt.Use();
                }
            }
        }

        private void DrawEmptyState()
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (string.IsNullOrEmpty(searchText))
            {
                GUILayout.Label("No props found in the selected category", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                GUILayout.Label($"No props found matching '{searchText}'", EditorStyles.centeredGreyMiniLabel);
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            if (selectedProp != null)
            {
                GUILayout.Label($"Selected: {selectedProp.name} | Category: {selectedProp.category} | Type: {selectedProp.objectType} | Used: {selectedProp.useCount} times");
            }
            else
            {
                GUILayout.Label($"Showing {filteredProps.Count} of {allProps.Count} props | Visible: {visibleEndIndex - visibleStartIndex + 1}");
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Public method to force refresh the prop list (for external calls like GroupEditDialog)
        /// </summary>
        public void ForceRefreshPropList()
        {
            RefreshPropList();
            needsFilterRefresh = true;
        }

        private void RefreshPropList()
        {
            allProps.Clear();
            categoryData.Clear();
            thumbnailCache.Clear();

            // Load saved groups first
            LoadSavedGroups();

            if (showEggFiles)
            {
                // Find all .egg files
                string[] eggGuids = AssetDatabase.FindAssets("t:GameObject");
                List<string> eggPaths = new List<string>();
                
                // Filter for .egg files, excluding animations and unwanted paths
                foreach (string guid in eggGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith(".egg", System.StringComparison.OrdinalIgnoreCase))
                    {
                        string lowerPath = path.ToLower();
                        // Skip animation files
                        if (lowerPath.Contains("_anim") || lowerPath.Contains("/anim") || 
                            lowerPath.Contains("animation") || lowerPath.Contains("/animations/"))
                        {
                            continue;
                        }
                        // Skip excluded paths
                        if (IsExcludedPath(path))
                        {
                            continue;
                        }
                        eggPaths.Add(path);
                    }
                }
                
                DebugLogger.LogAlways($"🔍 Prop Browser: Processing {eggPaths.Count} .egg files (excluding animations, effects, fonts, gui, textureCards)...");
                
                foreach (string path in eggPaths)
                {
                    GameObject eggAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    
                    if (eggAsset != null && !IsAnimationAsset(eggAsset, path) && !IsSimplePlane(eggAsset) && !IsExcludedPath(path))
                    {
                        PropAsset prop = CreatePropAsset(eggAsset, path);
                        allProps.Add(prop);
                        
                        // Organize into hierarchical categories
                        OrganizePropIntoCategories(prop);
                    }
                }
            }
            else
            {
                // Find all prefabs efficiently
                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
                
                DebugLogger.LogAlways($"🔍 Prop Browser: Processing {prefabGuids.Length} prefabs...");
                
                foreach (string guid in prefabGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    
                    // Skip excluded paths early
                    if (IsExcludedPath(path)) continue;
                    
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    
                    if (prefab != null && !IsAnimationAsset(prefab, path) && !IsSimplePlane(prefab))
                    {
                        PropAsset prop = CreatePropAsset(prefab, path);
                        allProps.Add(prop);
                        
                        // Organize into hierarchical categories
                        OrganizePropIntoCategories(prop);
                    }
                }
            }

            needsFilterRefresh = true;
            string assetType = showEggFiles ? ".egg files" : "prefabs";
            DebugLogger.LogAlways($"🔄 Prop Browser: Organized {allProps.Count} {assetType} into {categoryData.Count} categories");
        }

        private PropAsset CreatePropAsset(GameObject prefab, string path)
        {
            var prop = new PropAsset
            {
                name = prefab.name,
                path = path,
                prefab = prefab,
                isFavorite = favoriteProps.Contains(path),
                useCount = propUsageCounts.ContainsKey(path) ? propUsageCounts[path] : 0,
                thumbnailRequested = false,
                thumbnail = null // Explicitly set to null
            };

            // Better categorization using ObjectList data
            DetermineBetterCategory(prop, prefab, path);

            // Create searchable text
            prop.searchableText = (prop.name + " " + prop.category + " " + prop.subcategory + " " + prop.objectType).ToLower();

            return prop;
        }

        private void DetermineBetterCategory(PropAsset prop, GameObject asset, string path)
        {
            if (useObjectListCategories)
            {
                // Use ObjectList.py-based categorization
                DetermineObjectListCategory(prop, asset, path);
            }
            else
            {
                // Use path-based categorization (original method)
                DeterminePathBasedCategory(prop, asset, path);
            }
        }

        private void DetermineObjectListCategory(PropAsset prop, GameObject asset, string path)
        {
            // Try to get category from ObjectListInfo component first
            var objectInfo = asset.GetComponent<ObjectListInfo>();
            if (objectInfo != null && !string.IsNullOrEmpty(objectInfo.objectType))
            {
                prop.objectType = objectInfo.objectType;
                prop.category = GetCategoryFromObjectType(objectInfo.objectType);
                prop.subcategory = GetSubcategoryFromObjectType(objectInfo.objectType);
                return;
            }

            // If no ObjectListInfo, try to find it by name in ObjectList.py
            string modelName = asset.name;
            var objectListCategory = FindInObjectList(modelName);
            
            if (!string.IsNullOrEmpty(objectListCategory))
            {
                prop.objectType = objectListCategory;
                prop.category = GetCategoryFromObjectType(objectListCategory);
                prop.subcategory = GetSubcategoryFromObjectType(objectListCategory);
                return;
            }

            // Fallback to "Unknown" category for ObjectList mode
            prop.category = "Unknown";
            prop.subcategory = GetFolderNameFromPath(path);
            prop.objectType = path.EndsWith(".egg", System.StringComparison.OrdinalIgnoreCase) ? "EGG_MODEL" : "MISC_OBJ";
        }

        /// <summary>
        /// Find object type by model name using ObjectList.py data
        /// </summary>
        private string FindInObjectList(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
                return null;

            try
            {
                // Use the ObjectListParser to lookup the model name
                string objectType = ObjectListParser.GetObjectTypeByModelName(modelName);
                
                // Return null if it's the default fallback type
                return objectType == "MISC_OBJ" ? null : objectType;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to lookup model '{modelName}' in ObjectList: {ex.Message}");
                return null;
            }
        }

        private void DeterminePathBasedCategory(PropAsset prop, GameObject asset, string path)
        {
            // Try to get category from ObjectListInfo component
            var objectInfo = asset.GetComponent<ObjectListInfo>();
            if (objectInfo != null && !string.IsNullOrEmpty(objectInfo.objectType))
            {
                prop.objectType = objectInfo.objectType;
                prop.category = GetCategoryFromObjectType(objectInfo.objectType);
                prop.subcategory = GetSubcategoryFromObjectType(objectInfo.objectType);
                return;
            }

            // Fallback to path-based categorization
            string lowerPath = path.ToLower();
            string lowerName = asset.name.ToLower();

            // Special handling for .egg files - they often have better path structure
            if (path.EndsWith(".egg", System.StringComparison.OrdinalIgnoreCase))
            {
                // .egg files often have more descriptive folder structures
                if (lowerPath.Contains("/models/"))
                {
                    // Extract category from models path structure
                    string[] pathParts = path.Split('/');
                    for (int i = 0; i < pathParts.Length; i++)
                    {
                        if (pathParts[i].ToLower() == "models" && i + 1 < pathParts.Length)
                        {
                            string categoryHint = pathParts[i + 1].ToLower();
                            if (DetermineEggCategory(categoryHint, out string category, out string subcategory))
                            {
                                prop.category = category;
                                prop.subcategory = subcategory;
                                prop.objectType = "EGG_MODEL";
                                return;
                            }
                        }
                    }
                }
            }

            // Standard categorization for both prefabs and .egg files
            if (lowerPath.Contains("/buildings/") || lowerName.Contains("building") || lowerName.Contains("interior"))
            {
                prop.category = "Buildings";
                prop.subcategory = DetermineSubcategory(lowerName, lowerPath, "buildings");
            }
            else if (lowerPath.Contains("/ships/") || lowerName.Contains("ship") || lowerName.Contains("boat"))
            {
                prop.category = "Ships";
                prop.subcategory = DetermineSubcategory(lowerName, lowerPath, "ships");
            }
            else if (lowerPath.Contains("/weapons/") || lowerName.Contains("weapon") || lowerName.Contains("sword") || lowerName.Contains("gun"))
            {
                prop.category = "Weapons";
                prop.subcategory = DetermineSubcategory(lowerName, lowerPath, "weapons");
            }
            else if (lowerPath.Contains("/char/") || lowerName.Contains("char") || lowerName.Contains("avatar"))
            {
                prop.category = "Characters";
                prop.subcategory = DetermineSubcategory(lowerName, lowerPath, "characters");
            }
            else if (lowerPath.Contains("/caves/") || lowerName.Contains("cave"))
            {
                prop.category = "Caves";
                prop.subcategory = DetermineSubcategory(lowerName, lowerPath, "caves");
            }
            else if (lowerPath.Contains("/effects/") || lowerName.Contains("effect") || lowerName.Contains("particle"))
            {
                prop.category = "Effects";
                prop.subcategory = DetermineSubcategory(lowerName, lowerPath, "effects");
            }
            else if (lowerName.Contains("tree") || lowerName.Contains("rock") || lowerName.Contains("terrain") || lowerName.Contains("plant"))
            {
                prop.category = "Environment";
                prop.subcategory = DetermineSubcategory(lowerName, lowerPath, "environment");
            }
            else
            {
                prop.category = "Props";
                prop.subcategory = DetermineSubcategory(lowerName, lowerPath, "props");
            }

            prop.objectType = path.EndsWith(".egg", System.StringComparison.OrdinalIgnoreCase) ? "EGG_MODEL" : "MISC_OBJ";
        }

        private string GetCategoryFromObjectType(string objectType)
        {
            if (string.IsNullOrEmpty(objectType)) return "Unknown";
            
            string upperType = objectType.ToUpper();
            
            // Map ObjectList types to categories with more comprehensive matching
            if (upperType.Contains("BUILDING") || upperType.Contains("INTERIOR") || upperType.Contains("HOUSE") || upperType.Contains("DOOR") || upperType.Contains("WINDOW")) return "Buildings";
            if (upperType.Contains("SHIP") || upperType.Contains("BOAT") || upperType.Contains("VESSEL") || upperType.Contains("MAST") || upperType.Contains("SAIL")) return "Ships";
            if (upperType.Contains("WEAPON") || upperType.Contains("SWORD") || upperType.Contains("PISTOL") || upperType.Contains("CANNON") || upperType.Contains("BLADE")) return "Weapons";
            if (upperType.Contains("CHAR") || upperType.Contains("PIRATE") || upperType.Contains("NPC") || upperType.Contains("AVATAR")) return "Characters";
            if (upperType.Contains("CAVE") || upperType.Contains("TUNNEL") || upperType.Contains("UNDERGROUND")) return "Caves";
            if (upperType.Contains("EFFECT") || upperType.Contains("PARTICLE") || upperType.Contains("VFX") || upperType.Contains("MAGIC")) return "Effects";
            if (upperType.Contains("TREE") || upperType.Contains("ROCK") || upperType.Contains("PLANT") || upperType.Contains("NATURE") || upperType.Contains("TERRAIN")) return "Environment";
            if (upperType.Contains("TREASURE") || upperType.Contains("CHEST") || upperType.Contains("GOLD") || upperType.Contains("COIN")) return "Treasure";
            if (upperType.Contains("FURNITURE") || upperType.Contains("TABLE") || upperType.Contains("CHAIR") || upperType.Contains("BARREL") || upperType.Contains("CRATE")) return "Furniture";
            if (upperType.Contains("MISC") || upperType.Contains("PROP") || upperType.Contains("OBJ")) return "Props";
            
            return "Unknown";
        }

        private string GetSubcategoryFromObjectType(string objectType)
        {
            // Create more specific subcategories
            return objectType.Replace("_", " ").ToTitleCase();
        }

        private bool DetermineEggCategory(string categoryHint, out string category, out string subcategory)
        {
            category = "";
            subcategory = "";
            
            // Map common .egg file path categories
            switch (categoryHint)
            {
                case "buildings":
                case "architecture":
                    category = "Buildings";
                    subcategory = "Architecture";
                    return true;
                    
                case "ships":
                case "boats":
                case "naval":
                    category = "Ships";
                    subcategory = "Naval";
                    return true;
                    
                case "weapons":
                case "arms":
                    category = "Weapons";
                    subcategory = "Arms";
                    return true;
                    
                case "characters":
                case "avatars":
                case "pirates":
                    category = "Characters";
                    subcategory = "Pirates";
                    return true;
                    
                case "caves":
                case "caverns":
                    category = "Caves";
                    subcategory = "Underground";
                    return true;
                    
                case "effects":
                case "particles":
                case "vfx":
                    category = "Effects";
                    subcategory = "Visual";
                    return true;
                    
                case "environment":
                case "terrain":
                case "nature":
                    category = "Environment";
                    subcategory = "Natural";
                    return true;
                    
                case "props":
                case "objects":
                case "items":
                    category = "Props";
                    subcategory = "Items";
                    return true;
                    
                default:
                    return false;
            }
        }

        private string DetermineSubcategory(string name, string path, string mainCategory)
        {
            // Extract subcategory from path structure
            string[] pathParts = path.Split('/');
            for (int i = 0; i < pathParts.Length - 1; i++)
            {
                if (pathParts[i].ToLower().Contains(mainCategory))
                {
                    if (i + 1 < pathParts.Length)
                    {
                        return pathParts[i + 1].ToTitleCase();
                    }
                }
            }

            // Fallback subcategorization based on name patterns
            if (name.Contains("small")) return "Small";
            if (name.Contains("large")) return "Large";
            if (name.Contains("wooden")) return "Wooden";
            if (name.Contains("stone")) return "Stone";
            if (name.Contains("metal")) return "Metal";
            
            // Final fallback: use immediate folder name
            return GetFolderNameFromPath(path);
        }

        /// <summary>
        /// Extract the immediate folder name from a file path for categorization
        /// </summary>
        private string GetFolderNameFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "Uncategorized";

            // Get the directory path and extract the folder name
            string directoryPath = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directoryPath))
                return "Uncategorized";

            // Get the last folder in the path
            string folderName = Path.GetFileName(directoryPath);
            if (string.IsNullOrEmpty(folderName))
                return "Uncategorized";

            // Clean up the folder name for display
            folderName = folderName.Replace("_", " ").Replace("-", " ");
            return folderName.ToTitleCase();
        }

        private void OrganizePropIntoCategories(PropAsset prop)
        {
            if (!categoryData.ContainsKey(prop.category))
            {
                categoryData[prop.category] = new CategoryData { name = prop.category };
            }

            var categoryGroup = categoryData[prop.category];
            categoryGroup.props.Add(prop);

            // Add to subcategory
            if (!string.IsNullOrEmpty(prop.subcategory))
            {
                if (!categoryGroup.subcategories.ContainsKey(prop.subcategory))
                {
                    categoryGroup.subcategories[prop.subcategory] = new List<PropAsset>();
                }
                categoryGroup.subcategories[prop.subcategory].Add(prop);
            }
        }

        private void FilterAndOrganizeProps()
        {
            filteredProps = allProps.Where(prop =>
            {
                // Category filter
                if (selectedCategory != "All")
                {
                    if (prop.category != selectedCategory) return false;
                    if (!string.IsNullOrEmpty(selectedSubcategory) && prop.subcategory != selectedSubcategory) return false;
                }

                // Favorites filter
                if (showFavoritesOnly && !prop.isFavorite) return false;

                // Search filter
                if (!string.IsNullOrEmpty(searchText) && !prop.searchableText.Contains(searchText.ToLower())) return false;

                return true;
            }).OrderByDescending(p => p.isFavorite)
              .ThenByDescending(p => p.useCount)
              .ThenBy(p => p.category)
              .ThenBy(p => p.subcategory)
              .ThenBy(p => p.name)
              .ToList();

            CalculateVisibleRange();
        }

        private void PlacePropInScene(PropAsset prop)
        {
            if (SurfacePlacementTool.IsEnabled)
            {
                // Use surface placement tool for interactive placement (works for both props and groups!)
                SurfacePlacementTool.StartPlacement(prop.prefab);

                prop.useCount++;
                propUsageCounts[prop.path] = prop.useCount;
                SavePreferences();

                DebugLogger.LogAlways($"🎯 Started surface placement for {(prop.isGroup ? "group" : "prop")} '{prop.name}'");
            }
            else
            {
                // Standard placement logic (works for both props and groups!)
                Vector3 position = GetPlacementPosition();

                GameObject instance = PrefabUtility.InstantiatePrefab(prop.prefab) as GameObject;
                instance.transform.position = position;

                // For single props, add ObjectListInfo
                if (!prop.isGroup)
                {
                    SetupObjectListInfo(instance, prop.path, prop.objectType);
                }
                else
                {
                    // For groups, regenerate object IDs for all children
                    RegenerateGroupObjectIds(instance);
                }

                Selection.activeGameObject = instance;

                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.FrameSelected();
                }

                prop.useCount++;
                propUsageCounts[prop.path] = prop.useCount;
                SavePreferences();

                DebugLogger.LogAlways($"🎯 Placed {(prop.isGroup ? "group" : "prop")} '{prop.name}' in scene at {position}");
            }
        }


        private string ExtractModelPath(string assetPath)
        {
            string path = assetPath.Replace("Assets/Resources/", "").Replace(".prefab", "");
            return path;
        }

        /// <summary>
        /// Utility method to add ObjectListInfo component to instantiated objects - REUSE THIS!
        /// </summary>
        public static void SetupObjectListInfo(GameObject instance, string prefabPath, string objectType)
        {
            if (instance.GetComponent<ObjectListInfo>() == null)
            {
                var objectListInfo = instance.AddComponent<ObjectListInfo>();
                objectListInfo.modelPath = prefabPath.Replace("Assets/Resources/", "").Replace(".prefab", "");
                objectListInfo.objectType = objectType;
            }
        }

        /// <summary>
        /// Calculate placement position - either near selected object or in front of scene camera - REUSE THIS!
        /// </summary>
        public static Vector3 GetPlacementPosition()
        {
            if (Selection.activeTransform != null)
            {
                return Selection.activeTransform.position + Vector3.up * 2;
            }
            else if (SceneView.lastActiveSceneView != null)
            {
                return SceneView.lastActiveSceneView.camera.transform.position +
                       SceneView.lastActiveSceneView.camera.transform.forward * 5;
            }
            return Vector3.zero;
        }


        private void ShowPropContextMenu(PropAsset prop)
        {
            GenericMenu menu = new GenericMenu();

            // Check if multiple GameObjects are selected in the scene
            if (Selection.gameObjects != null && Selection.gameObjects.Length > 1)
            {
                menu.AddItem(new GUIContent("📦 Save Selection as Group"), false, () => SaveSelectionAsGroup());
                menu.AddSeparator("");
            }

            if (prop.isGroup)
            {
                menu.AddItem(new GUIContent("📝 Edit Group"), false, () => EditGroup(prop));
            }
            else
            {
                menu.AddItem(new GUIContent("Place in Scene"), false, () => PlacePropInScene(prop));
            }

            menu.AddSeparator("");

            for (int i = 1; i <= 9; i++)
            {
                int slotIndex = i;
                menu.AddItem(new GUIContent($"Send to Quick Slot {slotIndex}"), false, () => SendToQuickSlot(prop, slotIndex));
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent(prop.isFavorite ? "Remove from Favorites" : "Add to Favorites"), false, () => ToggleFavorite(prop));

            if (!prop.isGroup && prop.prefab != null)
            {
                menu.AddItem(new GUIContent("Show in Project"), false, () => EditorGUIUtility.PingObject(prop.prefab));
            }

            if (prop.isGroup)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("🗑️ Delete Group"), false, () => DeleteGroup(prop));
            }

            menu.ShowAsContext();
        }
        
        private void SendToQuickSlot(PropAsset prop, int slotNumber)
        {
            string prefabPath = AssetDatabase.GetAssetPath(prop.prefab);
            string slotKey = $"QuickPlace_Slot{slotNumber - 1}_Prefab";
            string countKey = $"QuickPlace_Slot{slotNumber - 1}_UseCount";
            
            EditorPrefs.SetString(slotKey, prefabPath);
            EditorPrefs.SetInt(countKey, 0);
            
            DebugLogger.LogAlways($"⚡ Sent '{prop.name}' to Quick Slot {slotNumber}");
            DebugLogger.LogAlways($"⚡ Saved to EditorPrefs key: '{slotKey}' = '{prefabPath}'");
            
            // Find open QuickPlaceTool window and reload its slots
            var quickPlaceWindows = UnityEngine.Resources.FindObjectsOfTypeAll<QuickPlaceTool>();
            if (quickPlaceWindows.Length > 0)
            {
                DebugLogger.LogAlways($"⚡ Found {quickPlaceWindows.Length} open QuickPlaceTool windows, reloading slots...");
                foreach (var window in quickPlaceWindows)
                {
                    window.ReloadQuickSlots();
                }
            }
            else
            {
                DebugLogger.LogAlways("⚡ No open QuickPlaceTool windows found - props will appear when window is opened");
            }
        }
        
        private void ToggleFavorite(PropAsset prop)
        {
            prop.isFavorite = !prop.isFavorite;
            if (prop.isFavorite)
                favoriteProps.Add(prop.path);
            else
                favoriteProps.Remove(prop.path);
            
            SavePreferences();
        }

        private void LoadPreferences()
        {
            string favoritesData = EditorPrefs.GetString("PropBrowser_Favorites", "");
            if (!string.IsNullOrEmpty(favoritesData))
            {
                favoriteProps = new HashSet<string>(favoritesData.Split('|'));
            }

            string usageData = EditorPrefs.GetString("PropBrowser_Usage", "");
            if (!string.IsNullOrEmpty(usageData))
            {
                propUsageCounts.Clear();
                foreach (var entry in usageData.Split('|'))
                {
                    var parts = entry.Split('=');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int count))
                    {
                        propUsageCounts[parts[0]] = count;
                    }
                }
            }

            thumbnailSize = EditorPrefs.GetInt("PropBrowser_ThumbnailSize", 64);
            selectedCategory = EditorPrefs.GetString("PropBrowser_SelectedCategory", "All");
            showEggFiles = EditorPrefs.GetBool("PropBrowser_ShowEggFiles", false);
            useObjectListCategories = EditorPrefs.GetBool("PropBrowser_UseObjectListCategories", false);
            // Category style is now always tabbed - no need to load from preferences
            
            // Load expanded categories
            string expandedData = EditorPrefs.GetString("PropBrowser_ExpandedCategories", "");
            if (!string.IsNullOrEmpty(expandedData))
            {
                expandedCategories = new HashSet<string>(expandedData.Split('|'));
            }
        }

        private void SavePreferences()
        {
            EditorPrefs.SetString("PropBrowser_Favorites", string.Join("|", favoriteProps));

            var usageEntries = propUsageCounts.Select(kvp => $"{kvp.Key}={kvp.Value}");
            EditorPrefs.SetString("PropBrowser_Usage", string.Join("|", usageEntries));

            EditorPrefs.SetInt("PropBrowser_ThumbnailSize", thumbnailSize);
            EditorPrefs.SetString("PropBrowser_SelectedCategory", selectedCategory);
            EditorPrefs.SetBool("PropBrowser_ShowEggFiles", showEggFiles);
            EditorPrefs.SetBool("PropBrowser_UseObjectListCategories", useObjectListCategories);
            // Category style is now always tabbed - no need to save to preferences
            EditorPrefs.SetString("PropBrowser_ExpandedCategories", string.Join("|", expandedCategories));
        }

        #region Group Management Methods

        private void SaveSelectionAsGroup()
        {
            if (Selection.gameObjects == null || Selection.gameObjects.Length < 2)
            {
                EditorUtility.DisplayDialog("Invalid Selection", "Please select 2 or more objects in the scene to create a group.", "OK");
                return;
            }

            GroupCreationDialog.ShowDialog(Selection.gameObjects, OnGroupCreated);
        }

        private void OnGroupCreated(GroupCreationDialog.GroupData groupData)
        {
            if (groupData == null || string.IsNullOrEmpty(groupData.name))
            {
                return;
            }

            // Create PropAsset for the group
            PropAsset groupProp = CreateGroupAsset(groupData);

            // Add to prop list
            allProps.Add(groupProp);

            // Organize into categories
            OrganizePropIntoCategories(groupProp);

            // Refresh display
            needsFilterRefresh = true;

            // Save group data to disk
            SaveGroupToDisk(groupProp, groupData);

            DebugLogger.LogAlways($"📦 Created group '{groupData.name}' with {groupData.items.Count} objects");
        }

        private PropAsset CreateGroupAsset(GroupCreationDialog.GroupData groupData)
        {
            var groupProp = new PropAsset
            {
                name = groupData.name,
                path = $"Groups/{groupData.name}",
                category = groupData.category,
                subcategory = "Custom Groups",
                prefab = null, // Will be set after prefab is created in SaveGroupToDisk
                isFavorite = false,
                useCount = 0,
                thumbnailRequested = false,
                thumbnail = null, // Let the existing thumbnail system handle it
                objectType = "GROUP",
                isGroup = true,
                groupItems = groupData.items, // Still needed during creation process
                customThumbnailPath = "",
                pivotType = groupData.pivotType,
                customPivotOffset = groupData.customPivotOffset
            };

            // Create searchable text with null checks
            var itemNames = groupData.items?.Where(item => !string.IsNullOrEmpty(item.prefabPath))
                                         .Select(item => Path.GetFileNameWithoutExtension(item.prefabPath)) ?? new string[0];
            groupProp.searchableText = (groupProp.name + " " + groupProp.category + " " +
                                      string.Join(" ", itemNames) + " group").ToLower();

            return groupProp;
        }

        private void SaveGroupToDisk(PropAsset groupProp, GroupCreationDialog.GroupData groupData)
        {
            string groupsFolder = "Assets/Resources/Groups";

            // Create folder if it doesn't exist
            if (!Directory.Exists(groupsFolder))
            {
                Directory.CreateDirectory(groupsFolder);
                AssetDatabase.Refresh();
            }

            // Create a temporary group GameObject with all the objects as children
            GameObject tempGroupParent = new GameObject(groupData.name);

            try
            {
                // Calculate pivot offset - always use bottom pivot
                Vector3 pivotOffset = Vector3.zero;
                float lowestY = float.MaxValue;
                foreach (var item in groupData.items)
                {
                    if (item.localPosition.y < lowestY)
                    {
                        lowestY = item.localPosition.y;
                    }
                }
                pivotOffset = new Vector3(0, -lowestY, 0);

                // Add all group objects as children
                foreach (var item in groupData.items)
                {
                    GameObject instance = null;

                    // Handle prefab objects
                    if (!string.IsNullOrEmpty(item.prefabPath))
                    {
                        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(item.prefabPath);
                        if (prefab != null)
                        {
                            // Use Instantiate instead of InstantiatePrefab to break prefab connection
                            // This makes the group self-contained and portable across different PCs
                            instance = GameObject.Instantiate(prefab);
                            instance.name = prefab.name; // Remove (Clone) suffix
                        }
                    }
                    // Handle non-prefab objects like lights
                    else if (item.sourceObject != null)
                    {
                        // Check if this is a light
                        Light sourceLight = item.sourceObject.GetComponent<Light>();
                        if (sourceLight != null)
                        {
                            // Create a new light object
                            instance = new GameObject($"Light_{item.objectType}");
                            Light newLight = instance.AddComponent<Light>();

                            // Copy light properties
                            newLight.type = sourceLight.type;
                            newLight.color = sourceLight.color;
                            newLight.intensity = sourceLight.intensity;
                            newLight.range = sourceLight.range;
                            newLight.spotAngle = sourceLight.spotAngle;
                            newLight.innerSpotAngle = sourceLight.innerSpotAngle;
                            newLight.cookieSize = sourceLight.cookieSize;
                            newLight.shadows = sourceLight.shadows;
                            newLight.renderMode = sourceLight.renderMode;
                            newLight.cullingMask = sourceLight.cullingMask;
                        }
                        else
                        {
                            // For other non-prefab objects, try to duplicate them
                            instance = GameObject.Instantiate(item.sourceObject);
                            instance.name = item.sourceObject.name; // Remove (Clone) suffix
                        }
                    }

                    if (instance != null)
                    {
                        instance.transform.SetParent(tempGroupParent.transform);
                        instance.transform.localPosition = item.localPosition + pivotOffset;
                        instance.transform.localRotation = item.localRotation;
                        instance.transform.localScale = item.localScale;

                            // Check if source object had ObjectListInfo and preserve its settings
                            ObjectListInfo sourceInfo = null;
                            if (item.sourceObject != null)
                            {
                                sourceInfo = item.sourceObject.GetComponent<ObjectListInfo>();
                            }

                            // Get or add ObjectListInfo component
                            ObjectListInfo propInfo = instance.GetComponent<ObjectListInfo>();
                            if (propInfo == null)
                            {
                                propInfo = instance.AddComponent<ObjectListInfo>();
                            }

                            if (sourceInfo != null)
                            {
                                // Preserve all settings from source EXCEPT objectId
                                propInfo.objectType = sourceInfo.objectType;
                                propInfo.modelPath = sourceInfo.modelPath;
                                propInfo.hasVisualBlock = sourceInfo.hasVisualBlock;
                                propInfo.visualColor = sourceInfo.visualColor;
                                propInfo.disableCollision = sourceInfo.disableCollision;
                                propInfo.instanced = sourceInfo.instanced;
                                propInfo.holiday = sourceInfo.holiday;
                                propInfo.visSize = sourceInfo.visSize;
                                propInfo.isGroup = sourceInfo.isGroup;
                                propInfo.groupCategory = sourceInfo.groupCategory;
                                propInfo.groupSubcategory = sourceInfo.groupSubcategory;
                                propInfo.autoDetectOnStart = sourceInfo.autoDetectOnStart;
                                propInfo.autoGenerateId = sourceInfo.autoGenerateId;

                                // Also preserve VisualColorHandler if there was one
                                if (sourceInfo.visualColor.HasValue)
                                {
                                    VisualColorHandler sourceHandler = item.sourceObject.GetComponent<VisualColorHandler>();
                                    if (sourceHandler != null)
                                    {
                                        VisualColorHandler newHandler = instance.GetComponent<VisualColorHandler>();
                                        if (newHandler == null)
                                        {
                                            newHandler = instance.AddComponent<VisualColorHandler>();
                                        }
                                        // The handler will automatically apply the color from ObjectListInfo
                                        newHandler.RefreshVisualColor();
                                    }
                                }

                                DebugLogger.LogAlways($"📋 Preserved ObjectListInfo settings from source object for '{instance.name}'");
                            }
                            else
                            {
                                // No source info, use defaults
                                // Clean model path - remove extensions and phase prefix, keep only models/props/modelname
                                string cleanModelPath = item.prefabPath.Replace("Assets/Resources/", "");
                                cleanModelPath = cleanModelPath.Replace(".prefab", "");
                                cleanModelPath = cleanModelPath.Replace(".egg", "");

                                // Remove phase_# prefix if present (e.g., "phase_4/models/props/chest" -> "models/props/chest")
                                if (cleanModelPath.StartsWith("phase_"))
                                {
                                    int slashIndex = cleanModelPath.IndexOf('/');
                                    if (slashIndex > 0)
                                    {
                                        cleanModelPath = cleanModelPath.Substring(slashIndex + 1);
                                    }
                                }

                                propInfo.modelPath = cleanModelPath;
                                propInfo.objectType = item.objectType;
                                propInfo.autoDetectOnStart = false; // Prevent auto-detection from overriding
                                propInfo.autoGenerateId = true;
                            }

                            // Always generate a new unique object ID for each instance
                            propInfo.GenerateObjectId();

                            // Special handling for lights - always set visual color to match light color
                            Light lightComponent = instance.GetComponent<Light>();
                            if (lightComponent != null)
                            {
                                // Always enable visual color for lights
                                propInfo.visualColor = lightComponent.color;
                                propInfo.objectType = "Light - Dynamic"; // Ensure correct type

                                // Add or update VisualColorHandler
                                VisualColorHandler colorHandler = instance.GetComponent<VisualColorHandler>();
                                if (colorHandler == null)
                                {
                                    colorHandler = instance.AddComponent<VisualColorHandler>();
                                }
                                colorHandler.RefreshVisualColor();

                                // Add LightVisualColorSync to keep colors in sync
                                LightVisualColorSync syncComponent = instance.GetComponent<LightVisualColorSync>();
                                if (syncComponent == null)
                                {
                                    syncComponent = instance.AddComponent<LightVisualColorSync>();
                                }
                                syncComponent.SyncColors();

                                DebugLogger.LogAlways($"💡 Set Visual Color for light '{instance.name}' to match light color: {lightComponent.color}");
                            }
                        }
                    }

                // Add group identifier component to parent
                var groupInfo = tempGroupParent.AddComponent<ObjectListInfo>();
                groupInfo.objectType = "GROUP";
                groupInfo.modelPath = $"Groups/{groupData.name}";
                groupInfo.isGroup = true; // Mark as group for special export behavior

                // Save as prefab
                string prefabPath = Path.Combine(groupsFolder, $"{groupData.name}.prefab");
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tempGroupParent, prefabPath);

                // Update the PropAsset to reference the new prefab
                groupProp.prefab = savedPrefab;
                groupProp.path = prefabPath.Replace("Assets/Resources/", "").Replace(".prefab", "");

                Debug.Log($"✅ Saved group '{groupData.name}' as prefab: {prefabPath}");
            }
            finally
            {
                // Clean up temporary object
                if (tempGroupParent != null)
                {
                    DestroyImmediate(tempGroupParent);
                }
            }

            AssetDatabase.Refresh();
        }

        private void LoadSavedGroups()
        {
            string groupsFolder = "Assets/Resources/Groups";

            if (!Directory.Exists(groupsFolder))
            {
                return;
            }

            string[] prefabFiles = Directory.GetFiles(groupsFolder, "*.prefab");

            foreach (string prefabPath in prefabFiles)
            {
                try
                {
                    GameObject groupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                    if (groupPrefab != null)
                    {
                        // Check if it has the ObjectListInfo component that identifies it as a group
                        var groupInfo = groupPrefab.GetComponent<ObjectListInfo>();

                        if (groupInfo != null && (groupInfo.objectType == "GROUP" || groupInfo.isGroup))
                        {
                            // Create PropAsset for this group prefab
                            PropAsset groupProp = new PropAsset
                            {
                                name = groupPrefab.name,
                                path = prefabPath.Replace("Assets/Resources/", "").Replace(".prefab", ""),
                                category = "Groups", // All group prefabs go to Groups category
                                subcategory = "Custom Groups",
                                prefab = groupPrefab, // Now groups have actual prefabs!
                                isFavorite = false,
                                useCount = 0,
                                thumbnailRequested = false,
                                thumbnail = null, // Will be generated by existing system
                                objectType = "GROUP",
                                isGroup = true,
                                groupItems = null, // Not needed anymore since it's a prefab
                                customThumbnailPath = "",
                                pivotType = GroupCreationDialog.PivotType.Bottom, // Default
                                customPivotOffset = Vector3.zero
                            };

                            // Create searchable text
                            groupProp.searchableText = (groupProp.name + " " + groupProp.category + " group").ToLower();

                            allProps.Add(groupProp);
                            OrganizePropIntoCategories(groupProp);

                            Debug.Log($"✅ Successfully loaded group prefab '{groupPrefab.name}' from {prefabPath}");
                        }
                        else
                        {
                            Debug.LogWarning($"Prefab {prefabPath} is not a valid group (missing GROUP ObjectListInfo)");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Could not load prefab at {prefabPath}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to load group prefab from {prefabPath}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Regenerates object IDs for all ObjectListInfo components in a group
        /// </summary>
        private void RegenerateGroupObjectIds(GameObject groupInstance)
        {
            if (groupInstance == null) return;

            // Get all ObjectListInfo components in the group (parent and children)
            ObjectListInfo[] allInfos = groupInstance.GetComponentsInChildren<ObjectListInfo>();

            int regeneratedCount = 0;
            foreach (var info in allInfos)
            {
                if (info != null && info.autoGenerateId)
                {
                    // Generate new unique ID for each instance
                    info.GenerateObjectId();
                    regeneratedCount++;

                    // If there's a visual color, ensure the handler is set up
                    if (info.visualColor.HasValue)
                    {
                        VisualColorHandler handler = info.GetComponent<VisualColorHandler>();
                        if (handler == null)
                        {
                            handler = info.gameObject.AddComponent<VisualColorHandler>();
                        }
                        handler.RefreshVisualColor();
                    }
                }
            }

            if (regeneratedCount > 0)
            {
                DebugLogger.LogAlways($"🔄 Regenerated {regeneratedCount} object IDs for group '{groupInstance.name}'");
            }
        }

        private void PlaceGroupExploded(PropAsset groupProp)
        {
            if (!groupProp.isGroup || groupProp.prefab == null)
            {
                return;
            }

            Vector3 basePosition = GetPlacementPosition();

            List<GameObject> placedObjects = new List<GameObject>();

            // Get all child objects from the group prefab and place them individually
            for (int i = 0; i < groupProp.prefab.transform.childCount; i++)
            {
                Transform child = groupProp.prefab.transform.GetChild(i);

                // Skip the group's own ObjectListInfo component if any
                if (child.GetComponent<ObjectListInfo>() != null)
                {
                    GameObject instance = PrefabUtility.InstantiatePrefab(child.gameObject) as GameObject;
                    instance.transform.position = basePosition + child.localPosition;
                    instance.transform.rotation = child.rotation;
                    instance.transform.localScale = child.localScale;

                    placedObjects.Add(instance);
                }
            }

            if (placedObjects.Count > 0)
            {
                Selection.objects = placedObjects.ToArray();

                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.FrameSelected();
                }

                groupProp.useCount++;
                propUsageCounts[groupProp.path] = groupProp.useCount;
                SavePreferences();

                DebugLogger.LogAlways($"💥 Placed group '{groupProp.name}' exploded with {placedObjects.Count} objects");
            }
        }

        private void EditGroup(PropAsset groupProp)
        {
            // Open the new Group Edit Dialog
            GroupEditDialog.ShowDialog(groupProp.name, groupProp.category, groupProp.subcategory, groupProp.prefab);
        }

        private void DeleteGroup(PropAsset groupProp)
        {
            if (EditorUtility.DisplayDialog("Delete Group",
                $"Are you sure you want to delete the group '{groupProp.name}'?\n\nThis action cannot be undone.",
                "Delete", "Cancel"))
            {
                // Remove from lists
                allProps.Remove(groupProp);
                filteredProps.Remove(groupProp);

                // Remove from categories
                if (categoryData.ContainsKey(groupProp.category))
                {
                    categoryData[groupProp.category].props.Remove(groupProp);

                    if (!string.IsNullOrEmpty(groupProp.subcategory) &&
                        categoryData[groupProp.category].subcategories.ContainsKey(groupProp.subcategory))
                    {
                        categoryData[groupProp.category].subcategories[groupProp.subcategory].Remove(groupProp);
                    }
                }

                // Delete prefab file
                try
                {
                    string prefabPath = AssetDatabase.GetAssetPath(groupProp.prefab);

                    if (!string.IsNullOrEmpty(prefabPath))
                    {
                        AssetDatabase.DeleteAsset(prefabPath);
                        AssetDatabase.Refresh();
                        Debug.Log($"✅ Deleted group prefab: {prefabPath}");
                    }
                    else
                    {
                        Debug.LogWarning($"Could not find prefab path for group '{groupProp.name}'");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to delete group prefab: {ex.Message}");
                }

                needsFilterRefresh = true;
                DebugLogger.LogAlways($"🗑️ Deleted group '{groupProp.name}'");
            }
        }

        #endregion

        #region Thumbnail Cache Management

    /// <summary>
    /// Load all cached thumbnails from disk on startup
    /// </summary>
    private void LoadCachedThumbnails()
    {
        if (thumbnailCacheLoaded) return;

        try
        {
            // Ensure cache directory exists
            if (!Directory.Exists(CACHE_FOLDER))
            {
                Directory.CreateDirectory(CACHE_FOLDER);
                AssetDatabase.Refresh();
                thumbnailCacheLoaded = true;
                return;
            }

            // Check cache version
            if (!IsValidCacheVersion())
            {
                ClearThumbnailCache();
                thumbnailCacheLoaded = true;
                return;
            }

            thumbnailCacheLoaded = true;
            Debug.Log($"📷 Object Browser thumbnail cache ready at: {CACHE_FOLDER}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to load thumbnail cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Load a single thumbnail from disk cache
    /// </summary>
    private Texture2D LoadCachedThumbnail(string propPath)
    {
        try
        {
            string safeName = GetSafeFileName(propPath);
            string cachePath = Path.Combine(CACHE_FOLDER, safeName + ".png");

            if (File.Exists(cachePath))
            {
                byte[] imageData = File.ReadAllBytes(cachePath);
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(imageData))
                {
                    return texture;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to load cached thumbnail for {propPath}: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Save a thumbnail to disk cache
    /// </summary>
    private void SaveThumbnailToCache(string propPath, Texture2D thumbnail)
    {
        if (thumbnail == null) return;

        try
        {
            // Ensure cache directory exists
            if (!Directory.Exists(CACHE_FOLDER))
            {
                Directory.CreateDirectory(CACHE_FOLDER);
                AssetDatabase.Refresh();
            }

            string safeName = GetSafeFileName(propPath);
            string cachePath = Path.Combine(CACHE_FOLDER, safeName + ".png");

            // Convert to PNG and save
            byte[] imageData = thumbnail.EncodeToPNG();
            File.WriteAllBytes(cachePath, imageData);

            // Update version file if needed
            UpdateCacheVersion();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to save thumbnail to cache for {propPath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Clear all cached thumbnails
    /// </summary>
    private void ClearThumbnailCache()
    {
        try
        {
            // Clear memory cache
            thumbnailCache.Clear();
            foreach (var prop in allProps)
            {
                prop.thumbnail = null;
                prop.thumbnailRequested = false;
            }

            // Clear disk cache
            if (Directory.Exists(CACHE_FOLDER))
            {
                // Delete all PNG files
                string[] files = Directory.GetFiles(CACHE_FOLDER, "*.png");
                foreach (string file in files)
                {
                    File.Delete(file);
                }

                // Update version file
                UpdateCacheVersion();

                Debug.Log($"🗑️ Cleared {files.Length} thumbnails from cache");
            }

            thumbnailCacheLoaded = false;
            AssetDatabase.Refresh();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to clear thumbnail cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Get a safe file name from a prop path
    /// </summary>
    private string GetSafeFileName(string path)
    {
        // Replace invalid characters with underscores
        string safeName = path.Replace('/', '_')
                             .Replace('\\', '_')
                             .Replace(':', '_')
                             .Replace('*', '_')
                             .Replace('?', '_')
                             .Replace('"', '_')
                             .Replace('<', '_')
                             .Replace('>', '_')
                             .Replace('|', '_');

        // Add hash for uniqueness if name is too long
        if (safeName.Length > 100)
        {
            int hash = path.GetHashCode();
            safeName = safeName.Substring(0, 90) + "_" + hash.ToString("X8");
        }

        return safeName;
    }

    /// <summary>
    /// Check if cache version is valid
    /// </summary>
    private bool IsValidCacheVersion()
    {
        try
        {
            if (File.Exists(CACHE_VERSION_FILE))
            {
                string version = File.ReadAllText(CACHE_VERSION_FILE).Trim();
                return version == CURRENT_CACHE_VERSION;
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Update cache version file
    /// </summary>
    private void UpdateCacheVersion()
    {
        try
        {
            if (!Directory.Exists(CACHE_FOLDER))
            {
                Directory.CreateDirectory(CACHE_FOLDER);
            }

            File.WriteAllText(CACHE_VERSION_FILE, CURRENT_CACHE_VERSION);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to update cache version: {ex.Message}");
        }
    }

        #endregion
    }

    // Extension method for string formatting
    public static class StringExtensions
    {
        public static string ToTitleCase(this string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var words = input.Split(' ', '_', '-');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }
            return string.Join(" ", words);
        }
    }
}