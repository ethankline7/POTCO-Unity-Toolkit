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

        // Preset Management
        private string presetSavePath = "";
        private string[] availablePresets;
        private int selectedPresetIndex = 0;

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
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawPresetManagement();
            EditorGUILayout.Space(10);

            DrawShipSelection();
            EditorGUILayout.Space(10);

            DrawBuildOptions();
            EditorGUILayout.Space(10);

            DrawComponentCustomization();
            EditorGUILayout.Space(10);

            DrawBuildButtons();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("POTCO Ship Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Build and customize ships using POTCO ship parts. Select a base ship hull and customize components like masts, cannons, and wheels.", MessageType.Info);
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

            EditorGUILayout.Space(5);

            currentShip.addShipController = EditorGUILayout.Toggle("Add Ship Controller", currentShip.addShipController);
            EditorGUILayout.HelpBox("Adds a ShipController component that allows you to pilot the ship. Press Shift near the wheel to enter control mode.", MessageType.Info);
        }

        private void DrawComponentCustomization()
        {
            if (string.IsNullOrEmpty(currentShip.baseHullName))
            {
                EditorGUILayout.HelpBox("Select a base hull to customize components.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Ship Components", EditorStyles.boldLabel);

            // Masts
            DrawComponentSection("Masts", currentShip.masts, "pir_r_shp_mst_");

            // Cannons
            DrawComponentSection("Broadside Cannons (Left)", currentShip.cannonsBroadsideLeft, "pir_r_shp_can_broadside_");
            DrawComponentSection("Broadside Cannons (Right)", currentShip.cannonsBroadsideRight, "pir_r_shp_can_broadside_");
            DrawComponentSection("Deck Cannons", currentShip.cannonsDeck, "pir_r_shp_can_deck_");

            // Bowsprits (customizable)
            DrawComponentSection("Bowsprits", currentShip.bowsprits, "prow_");

            // Note: Wheel, Repair Spots, and Ram are auto-assigned and not shown in UI
        }

        private void DrawComponentSection(string label, Dictionary<string, string> components, string componentPrefix)
        {
            if (components == null || components.Count == 0) return;

            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;

            string[] availableComponents = componentDatabase.GetComponentsByPrefix(componentPrefix);

            List<string> keys = new List<string>(components.Keys);
            foreach (string locator in keys)
            {
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

        private void DrawSingleComponentSection(string label, string locatorKey, string currentComponent, string componentPrefix)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;

            string[] availableComponents = componentDatabase.GetComponentsByPrefix(componentPrefix);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Type:", GUILayout.Width(150));

            int currentIndex = System.Array.IndexOf(availableComponents, currentComponent);
            if (currentIndex < 0) currentIndex = 0;

            int newIndex = EditorGUILayout.Popup(currentIndex, availableComponents);
            if (newIndex >= 0 && newIndex < availableComponents.Length)
            {
                currentShip.wheel = availableComponents[newIndex];
            }

            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
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
    }
}
