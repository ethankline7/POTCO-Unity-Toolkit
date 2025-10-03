using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace POTCO.ShipBuilder
{
    public class ShipBuilderEditor : EditorWindow
    {
        private ShipConfiguration currentShip;
        private ShipComponentDatabase componentDatabase;
        private Vector2 scrollPosition;

        // UI State
        private string shipName = "New Ship";
        private GameObject previewShip;

        // Preset Management (JSON-based custom presets)
        private string[] availablePresets;
        private int selectedPresetIndex = 0;

        // POTCO Ship Preset System
        private string[] availablePOTCOPresets;
        private int[] potcoPresetIDs;
        private int selectedPOTCOPresetIndex = 0;

        // Tab System
        private enum Tab { ShipDesign, Customization, BuildSettings, Presets }
        private Tab currentTab = Tab.ShipDesign;

        // Foldout States
        private bool foldoutMasts = true;
        private bool foldoutBroadsideLeft = false;
        private bool foldoutBroadsideRight = false;
        private bool foldoutDeckCannons = false;
        private bool foldoutBowsprits = false;
        private bool foldoutShipParts = true;
        private bool foldoutRams = false;
        private bool foldoutRepairSpots = false;

        // Search/Filter
        private string searchFilter = "";

        [MenuItem("POTCO/Ship Builder")]
        public static void ShowWindow()
        {
            ShipBuilderEditor window = GetWindow<ShipBuilderEditor>("Ship Builder");
            window.minSize = new Vector2(600, 700);
            window.Show();
        }

        private void OnEnable()
        {
            componentDatabase = new ShipComponentDatabase();
            componentDatabase.Initialize();

            currentShip = new ShipConfiguration();
            LoadAvailablePresets();
            LoadPOTCOPresets();
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(5);

            DrawTabBar();
            EditorGUILayout.Space(5);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            switch (currentTab)
            {
                case Tab.ShipDesign:
                    DrawShipDesignTab();
                    break;
                case Tab.Customization:
                    DrawCustomizationTab();
                    break;
                case Tab.BuildSettings:
                    DrawBuildSettingsTab();
                    break;
                case Tab.Presets:
                    DrawPresetsTab();
                    break;
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);
            DrawBuildButtons();
        }

        private void DrawTabBar()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Toggle(currentTab == Tab.ShipDesign, "⚓ Ship Design", EditorStyles.toolbarButton))
                currentTab = Tab.ShipDesign;

            if (GUILayout.Toggle(currentTab == Tab.Customization, "🎨 Customization", EditorStyles.toolbarButton))
                currentTab = Tab.Customization;

            if (GUILayout.Toggle(currentTab == Tab.BuildSettings, "⚙️ Build Settings", EditorStyles.toolbarButton))
                currentTab = Tab.BuildSettings;

            if (GUILayout.Toggle(currentTab == Tab.Presets, "📦 Presets", EditorStyles.toolbarButton))
                currentTab = Tab.Presets;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader()
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft
            };

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("🚢", GUILayout.Width(30), GUILayout.Height(30));
            EditorGUILayout.LabelField("POTCO Ship Builder", headerStyle);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawShipDesignTab()
        {
            DrawPOTCOShipPresetSelection();
            EditorGUILayout.Space(10);

            DrawShipSelection();
            EditorGUILayout.Space(10);

            DrawValidationPanel();
            EditorGUILayout.Space(10);

            DrawComponentCustomization();
            EditorGUILayout.Space(10);

            DrawStatisticsPanel();
        }

        private void DrawCustomizationTab()
        {
            EditorGUILayout.HelpBox("🎨 Material and color customization coming soon!", MessageType.Info);
            EditorGUILayout.Space(10);

            // Placeholder for future material/color customization
            EditorGUILayout.LabelField("Future Features:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• Color picker for hull");
            EditorGUILayout.LabelField("• Material presets (weathered, new, damaged)");
            EditorGUILayout.LabelField("• Custom texture application");
        }

        private void DrawBuildSettingsTab()
        {
            DrawBuildOptions();
        }

        private void DrawPresetsTab()
        {
            DrawPresetManagement();
        }

        private void DrawValidationPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("✅ Validation Status", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            bool hasHull = !string.IsNullOrEmpty(currentShip.baseHullName);
            bool hasMasts = currentShip.masts != null && currentShip.masts.Count > 0;
            bool hasWheel = !string.IsNullOrEmpty(currentShip.wheel);

            DrawValidationItem(hasHull, "Hull Selected", "No hull selected");
            DrawValidationItem(hasMasts, "Masts Assigned", "No masts configured");
            DrawValidationItem(hasWheel, "Wheel Assigned", "No wheel configured");

            EditorGUILayout.Space(5);

            if (hasHull && hasMasts && hasWheel)
            {
                EditorGUILayout.HelpBox("✅ Ship configuration is valid! Ready to build.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("⚠️ Configuration incomplete. Address issues above.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawValidationItem(bool isValid, string validText, string invalidText)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(isValid ? "✅" : "❌", GUILayout.Width(20));
            EditorGUILayout.LabelField(isValid ? validText : invalidText);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatisticsPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📊 Ship Statistics", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            int totalCannons = 0;
            if (currentShip.cannonsBroadsideLeft != null) totalCannons += currentShip.cannonsBroadsideLeft.Count;
            if (currentShip.cannonsBroadsideRight != null) totalCannons += currentShip.cannonsBroadsideRight.Count;
            if (currentShip.cannonsDeck != null) totalCannons += currentShip.cannonsDeck.Count;

            int totalMasts = currentShip.masts != null ? currentShip.masts.Count : 0;

            int totalComponents = 0;
            if (currentShip.masts != null) totalComponents += currentShip.masts.Count;
            if (currentShip.cannonsBroadsideLeft != null) totalComponents += currentShip.cannonsBroadsideLeft.Count;
            if (currentShip.cannonsBroadsideRight != null) totalComponents += currentShip.cannonsBroadsideRight.Count;
            if (currentShip.cannonsDeck != null) totalComponents += currentShip.cannonsDeck.Count;
            if (currentShip.bowsprits != null) totalComponents += currentShip.bowsprits.Count;
            if (!string.IsNullOrEmpty(currentShip.wheel)) totalComponents++;
            if (currentShip.rams != null) totalComponents += currentShip.rams.Count;
            if (currentShip.repairSpots != null) totalComponents += currentShip.repairSpots.Count;

            EditorGUILayout.LabelField($"📊 Total Cannons: {totalCannons}");
            EditorGUILayout.LabelField($"🎯 Masts: {totalMasts}");
            EditorGUILayout.LabelField($"⚙️ Total Components: {totalComponents}");
            EditorGUILayout.LabelField($"🏴‍☠️ Hull: {currentShip.baseHullName ?? "None"}");

            EditorGUILayout.EndVertical();
        }

        private void DrawPresetManagement()
        {
            EditorGUILayout.LabelField("Preset Management", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Load Preset:", GUILayout.Width(100));

            if (availablePresets != null && availablePresets.Length > 0)
            {
                selectedPresetIndex = EditorGUILayout.Popup(selectedPresetIndex, availablePresets);

                if (GUILayout.Button("Load", GUILayout.Width(60)))
                {
                    LoadPreset(availablePresets[selectedPresetIndex]);
                }
            }
            else
            {
                EditorGUILayout.LabelField("No presets found");
            }

            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
            {
                LoadAvailablePresets();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            shipName = EditorGUILayout.TextField("Ship Name:", shipName);

            if (GUILayout.Button("Save Preset", GUILayout.Width(100)))
            {
                SavePreset();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawShipSelection()
        {
            EditorGUILayout.LabelField("Base Ship Hull", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Hull Type:", GUILayout.Width(100));

            string[] hullOptions = componentDatabase.GetAvailableHulls();
            int currentIndex = System.Array.IndexOf(hullOptions, currentShip.baseHullName);
            if (currentIndex < 0) currentIndex = 0;

            int newIndex = EditorGUILayout.Popup(currentIndex, hullOptions);
            if (newIndex >= 0 && newIndex < hullOptions.Length)
            {
                string newHull = hullOptions[newIndex];
                if (currentShip.baseHullName != newHull)
                {
                    currentShip.baseHullName = newHull;
                    currentShip.InitializeFromHull(componentDatabase);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawBuildOptions()
        {
            EditorGUILayout.LabelField("Build Options", EditorStyles.boldLabel);

            currentShip.generateCollisions = EditorGUILayout.Toggle("Generate Mesh Collisions", currentShip.generateCollisions);
            EditorGUILayout.HelpBox("Adds MeshCollider components to all ship parts for physics interactions.", MessageType.Info);

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Scene Editing", EditorStyles.boldLabel);

            bool sceneEditingEnabled = SceneEditing.ShipComponentVisualizer.IsEnabled;
            bool newSceneEditingEnabled = EditorGUILayout.Toggle("Enable Scene Component Editing", sceneEditingEnabled);
            if (newSceneEditingEnabled != sceneEditingEnabled)
            {
                SceneEditing.ShipComponentVisualizer.IsEnabled = newSceneEditingEnabled;
            }
            EditorGUILayout.HelpBox("Shows gizmos in Scene View for tracked ships and selected objects. Click components and use arrow keys to cycle.", MessageType.Info);

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Clear Tracked Ships"))
            {
                SceneEditing.ShipComponentVisualizer.ClearTrackedShips();
            }
            EditorGUILayout.HelpBox("Built ships are automatically tracked for scene editing. Click to clear the list.", MessageType.Info);

            EditorGUILayout.Space(10);

            currentShip.addShipController = EditorGUILayout.Toggle("Add Ship Controller", currentShip.addShipController);
            EditorGUILayout.HelpBox("Adds a ShipController component that allows you to pilot the ship. Press Shift near the wheel to enter control mode.", MessageType.Info);
        }

        private void DrawComponentCustomization()
        {
            if (string.IsNullOrEmpty(currentShip.baseHullName))
            {
                EditorGUILayout.HelpBox("⚠️ Select a base hull to customize components.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("⚙️ Ship Components", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Search/Filter Bar
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("🔍", GUILayout.Width(20));
            searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                searchFilter = "";
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Expand/Collapse All buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Expand All", EditorStyles.miniButton))
            {
                foldoutMasts = foldoutBroadsideLeft = foldoutBroadsideRight =
                foldoutDeckCannons = foldoutBowsprits = foldoutShipParts =
                foldoutRams = foldoutRepairSpots = true;
            }
            if (GUILayout.Button("Collapse All", EditorStyles.miniButton))
            {
                foldoutMasts = foldoutBroadsideLeft = foldoutBroadsideRight =
                foldoutDeckCannons = foldoutBowsprits = foldoutShipParts =
                foldoutRams = foldoutRepairSpots = false;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Masts
            DrawComponentSectionWithFoldout("🎯 Masts", ref foldoutMasts, currentShip.masts, "pir_r_shp_mst_");

            // Cannons
            DrawComponentSectionWithFoldout("💣 Broadside Cannons (Left)", ref foldoutBroadsideLeft,
                currentShip.cannonsBroadsideLeft, "pir_r_shp_can_broadside_");
            DrawComponentSectionWithFoldout("💣 Broadside Cannons (Right)", ref foldoutBroadsideRight,
                currentShip.cannonsBroadsideRight, "pir_r_shp_can_broadside_");
            DrawComponentSectionWithFoldout("💣 Deck Cannons", ref foldoutDeckCannons,
                currentShip.cannonsDeck, "pir_r_shp_can_deck_");

            // Bowsprits
            DrawComponentSectionWithFoldout("⛵ Bowsprits", ref foldoutBowsprits,
                currentShip.bowsprits, "prow_");

            // Ship Parts (Wheel, Ram, etc.)
            DrawShipPartsSectionWithFoldout();

            EditorGUILayout.EndVertical();
        }

        private void DrawComponentSectionWithFoldout(string label, ref bool foldout,
            Dictionary<string, string> components, string componentPrefix)
        {
            if (components == null || components.Count == 0) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Foldout header with count
            EditorGUILayout.BeginHorizontal();
            foldout = EditorGUILayout.Foldout(foldout, $"{label} ({components.Count})", true, EditorStyles.foldoutHeader);

            if (GUILayout.Button("Set All", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                ShowSetAllMenu(components, componentPrefix);
            }
            EditorGUILayout.EndHorizontal();

            if (foldout)
            {
                EditorGUI.indentLevel++;

                string[] availableComponents = componentDatabase.GetComponentsByPrefix(componentPrefix);
                List<string> keys = new List<string>(components.Keys);

                foreach (string locator in keys)
                {
                    // Apply search filter
                    if (!string.IsNullOrEmpty(searchFilter) &&
                        !locator.ToLower().Contains(searchFilter.ToLower()) &&
                        !components[locator].ToLower().Contains(searchFilter.ToLower()))
                    {
                        continue;
                    }

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(locator, GUILayout.Width(150));

                    int currentIndex = System.Array.IndexOf(availableComponents, components[locator]);
                    if (currentIndex < 0) currentIndex = 0;

                    int newIndex = EditorGUILayout.Popup(currentIndex, availableComponents);
                    if (newIndex >= 0 && newIndex < availableComponents.Length)
                    {
                        components[locator] = availableComponents[newIndex];
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        private void ShowSetAllMenu(Dictionary<string, string> components, string componentPrefix)
        {
            GenericMenu menu = new GenericMenu();
            string[] availableComponents = componentDatabase.GetComponentsByPrefix(componentPrefix);

            foreach (string component in availableComponents)
            {
                menu.AddItem(new GUIContent(component), false, () =>
                {
                    List<string> keys = new List<string>(components.Keys);
                    foreach (string key in keys)
                    {
                        components[key] = component;
                    }
                });
            }

            menu.ShowAsContext();
        }

        private void DrawShipPartsSectionWithFoldout()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            foldoutShipParts = EditorGUILayout.Foldout(foldoutShipParts, "🔧 Ship Parts (Wheel)", true, EditorStyles.foldoutHeader);
            EditorGUILayout.EndHorizontal();

            if (foldoutShipParts)
            {
                EditorGUI.indentLevel++;

                // Wheel
                DrawSingleComponentField("Wheel:", ref currentShip.wheel, "pir_m_shp_prt_wheel");

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);

            // Rams (as dictionary)
            DrawComponentSectionWithFoldout("🗡️ Rams", ref foldoutRams,
                currentShip.rams, "pir_m_shp_ram_");

            // Repair Spots (as dictionary)
            DrawComponentSectionWithFoldout("🔧 Repair Spots", ref foldoutRepairSpots,
                currentShip.repairSpots, "repair_spot_");
        }

        private void DrawSingleComponentField(string label, ref string component, string componentPrefix)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(100));

            string[] availableComponents = componentDatabase.GetComponentsByPrefix(componentPrefix);

            if (availableComponents.Length > 0)
            {
                int currentIndex = System.Array.IndexOf(availableComponents, component);
                if (currentIndex < 0) currentIndex = 0;

                int newIndex = EditorGUILayout.Popup(currentIndex, availableComponents);
                if (newIndex >= 0 && newIndex < availableComponents.Length)
                {
                    component = availableComponents[newIndex];
                }
            }
            else
            {
                EditorGUILayout.LabelField("No components found", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();
        }


        private void DrawBuildButtons()
        {
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Build Ship in Scene", GUILayout.Height(40)))
            {
                BuildShipInScene();
            }

            if (GUILayout.Button("Create Prefab", GUILayout.Height(40)))
            {
                CreateShipPrefab();
            }

            EditorGUILayout.EndHorizontal();

            if (previewShip != null)
            {
                if (GUILayout.Button("Clear Preview", GUILayout.Height(30)))
                {
                    DestroyImmediate(previewShip);
                    previewShip = null;
                }
            }
        }

        private void BuildShipInScene()
        {
            if (previewShip != null)
            {
                DestroyImmediate(previewShip);
            }

            ShipAssembler assembler = new ShipAssembler(componentDatabase);
            previewShip = assembler.BuildShip(currentShip, shipName);

            if (previewShip != null)
            {
                Selection.activeGameObject = previewShip;
                SceneView.lastActiveSceneView.FrameSelected();
                Debug.Log($"Ship '{shipName}' built successfully!");
            }
        }

        private void CreateShipPrefab()
        {
            ShipAssembler assembler = new ShipAssembler(componentDatabase);
            GameObject shipObject = assembler.BuildShip(currentShip, shipName);

            if (shipObject != null)
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    "Save Ship Prefab",
                    shipName + ".prefab",
                    "prefab",
                    "Choose where to save the ship prefab"
                );

                if (!string.IsNullOrEmpty(path))
                {
                    PrefabUtility.SaveAsPrefabAsset(shipObject, path);
                    Debug.Log($"Ship prefab saved to: {path}");
                    DestroyImmediate(shipObject);
                }
            }
        }

        private void SavePreset()
        {
            string presetsFolder = "Assets/Editor/Ship Builder/Presets";
            if (!Directory.Exists(presetsFolder))
            {
                Directory.CreateDirectory(presetsFolder);
            }

            string path = Path.Combine(presetsFolder, shipName + ".json");
            string json = JsonUtility.ToJson(currentShip, true);
            File.WriteAllText(path, json);

            AssetDatabase.Refresh();
            LoadAvailablePresets();

            Debug.Log($"Preset saved: {path}");
        }

        private void LoadPreset(string presetName)
        {
            string presetsFolder = "Assets/Editor/Ship Builder/Presets";
            string path = Path.Combine(presetsFolder, presetName + ".json");

            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                currentShip = JsonUtility.FromJson<ShipConfiguration>(json);
                shipName = presetName;
                Debug.Log($"Preset loaded: {presetName}");
            }
        }

        private void LoadAvailablePresets()
        {
            string presetsFolder = "Assets/Editor/Ship Builder/Presets";
            if (!Directory.Exists(presetsFolder))
            {
                availablePresets = new string[0];
                return;
            }

            string[] files = Directory.GetFiles(presetsFolder, "*.json");
            availablePresets = new string[files.Length];

            for (int i = 0; i < files.Length; i++)
            {
                availablePresets[i] = Path.GetFileNameWithoutExtension(files[i]);
            }
        }

        private void LoadPOTCOPresets()
        {
            availablePOTCOPresets = componentDatabase.GetAvailableShipPresets();
            potcoPresetIDs = componentDatabase.GetShipPresetIDs();

            if (availablePOTCOPresets != null && availablePOTCOPresets.Length > 0)
            {
                Debug.Log($"Loaded {availablePOTCOPresets.Length} POTCO ship presets");
            }
        }

        private void DrawPOTCOShipPresetSelection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🚢 POTCO Ship Presets", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            if (availablePOTCOPresets != null && availablePOTCOPresets.Length > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Ship Type:", GUILayout.Width(100));

                int newPresetIndex = EditorGUILayout.Popup(selectedPOTCOPresetIndex, availablePOTCOPresets);
                if (newPresetIndex != selectedPOTCOPresetIndex)
                {
                    selectedPOTCOPresetIndex = newPresetIndex;
                    if (selectedPOTCOPresetIndex > 0 && selectedPOTCOPresetIndex < potcoPresetIDs.Length)
                    {
                        int shipID = potcoPresetIDs[selectedPOTCOPresetIndex];
                        ApplyPOTCOShipPreset(shipID);
                    }
                }

                EditorGUILayout.EndHorizontal();

                if (selectedPOTCOPresetIndex > 0)
                {
                    EditorGUILayout.HelpBox("✅ Preset applied! All fields below are fully customizable.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("Select a POTCO ship to auto-fill configuration, or build a custom ship manually.", MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("⚠️ No POTCO ship presets found. Check Assets/Editor/POTCO_Source folder.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void ApplyPOTCOShipPreset(int shipID)
        {
            ShipConfigData config = componentDatabase.GetShipConfig(shipID);
            if (config == null)
            {
                Debug.LogError($"Failed to load ship config for ID {shipID}");
                return;
            }

            Debug.Log($"Applying POTCO ship preset: {config.displayName} (ID: {shipID})");

            // Set preset ID and style/logo
            currentShip.selectedPresetID = shipID;
            currentShip.styleID = config.defaultStyle;
            currentShip.logoID = config.sailLogo;

            // Auto-fill hull (use modelClass, not shipID, since ships can share hulls)
            string hullModelName = componentDatabase.GetHullModelName(config.modelClass);
            if (!string.IsNullOrEmpty(hullModelName))
            {
                currentShip.baseHullName = hullModelName;
                Debug.Log($"  Hull: {hullModelName} (Model Class: {config.modelClass})");

                // Initialize from hull to populate locators
                currentShip.InitializeFromHull(componentDatabase);
            }
            else
            {
                Debug.LogWarning($"  No hull model found for model class {config.modelClass} (Ship ID: {shipID})");
            }

            // Auto-configure masts (NOTE: model name mapping needs to be fixed when we know the pattern)
            ApplyMastConfig("location_mainmast1", config.mastConfig1);
            ApplyMastConfig("location_mainmast2", config.mastConfig2);
            ApplyMastConfig("location_mainmast3", config.mastConfig3);
            ApplyMastConfig("location_foremast", config.foremastConfig);
            ApplyMastConfig("location_aftmast", config.aftmastConfig);

            // Auto-limit cannons
            LimitCannons(currentShip.cannonsDeck, config.maxDeckCannons);
            LimitCannons(currentShip.cannonsBroadsideLeft, config.maxBroadsideLeft);
            LimitCannons(currentShip.cannonsBroadsideRight, config.maxBroadsideRight);

            // Set prow/bowsprit
            string prowModelName = componentDatabase.GetProwModelName(config.prowType);
            if (!string.IsNullOrEmpty(prowModelName) && currentShip.bowsprits != null)
            {
                foreach (var key in new List<string>(currentShip.bowsprits.Keys))
                {
                    currentShip.bowsprits[key] = prowModelName;
                }
                Debug.Log($"  Prow: {prowModelName}");
            }

            Debug.Log($"  Style ID: {config.defaultStyle}, Logo ID: {config.sailLogo}");
            Debug.Log($"  Cannons - Deck: {config.maxDeckCannons}, Left: {config.maxBroadsideLeft}, Right: {config.maxBroadsideRight}");
            Debug.Log($"✅ POTCO ship preset applied successfully!");
        }

        private void ApplyMastConfig(string locatorName, MastConfig mastConfig)
        {
            if (!mastConfig.IsValid())
            {
                // Remove mast from this locator if config is invalid (mastType = 0 or height = 0)
                if (currentShip.masts != null && currentShip.masts.ContainsKey(locatorName))
                {
                    currentShip.masts[locatorName] = "<None>";
                }
                return;
            }

            // Get mast model name from database
            string mastModelName = componentDatabase.GetMastModelName(mastConfig);
            if (!string.IsNullOrEmpty(mastModelName))
            {
                if (currentShip.masts != null && currentShip.masts.ContainsKey(locatorName))
                {
                    currentShip.masts[locatorName] = mastModelName;
                    Debug.Log($"  Mast {locatorName}: {mastModelName}");
                }
            }
        }

        private void LimitCannons(Dictionary<string, string> cannons, int maxCount)
        {
            if (cannons == null || maxCount <= 0) return;
            if (cannons.Count <= maxCount) return;

            // Keep only first 'maxCount' entries, remove the rest
            List<string> keys = new List<string>(cannons.Keys);
            for (int i = maxCount; i < keys.Count; i++)
            {
                cannons[keys[i]] = "<None>";
            }
        }
    }
}
