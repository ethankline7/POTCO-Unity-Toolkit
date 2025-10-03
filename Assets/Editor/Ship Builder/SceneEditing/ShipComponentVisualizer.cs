using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace POTCO.ShipBuilder.SceneEditing
{
    /// <summary>
    /// Visualizes ship components in Scene View and handles user input for selection/cycling
    /// </summary>
    [InitializeOnLoad]
    public static class ShipComponentVisualizer
    {
        private const float COMPONENT_GIZMO_SIZE = 2.0f;
        private const float COMPONENT_CLICK_RADIUS = 3.0f;
        private const string TRACKED_SHIPS_KEY = "ShipBuilder_TrackedShips";

        private static bool isEnabled = false;
        private static HashSet<string> trackedShipNames = new HashSet<string>();

        public static bool IsEnabled
        {
            get => isEnabled;
            set
            {
                isEnabled = value;
                SceneView.RepaintAll();
            }
        }

        static ShipComponentVisualizer()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            LoadTrackedShips();
        }

        public static void RegisterBuiltShip(GameObject ship)
        {
            if (ship == null) return;

            string shipName = ship.name;
            if (!trackedShipNames.Contains(shipName))
            {
                trackedShipNames.Add(shipName);
                SaveTrackedShips();
                Debug.Log($"🚢 Registered ship for automatic scene editing: {shipName}");
            }
        }

        public static void UnregisterShip(string shipName)
        {
            if (trackedShipNames.Remove(shipName))
            {
                SaveTrackedShips();
                Debug.Log($"🗑️ Unregistered ship: {shipName}");
            }
        }

        public static void ClearTrackedShips()
        {
            trackedShipNames.Clear();
            SaveTrackedShips();
            Debug.Log("🧹 Cleared all tracked ships");
        }

        private static void LoadTrackedShips()
        {
            string saved = EditorPrefs.GetString(TRACKED_SHIPS_KEY, "");
            if (!string.IsNullOrEmpty(saved))
            {
                string[] names = saved.Split(';');
                trackedShipNames = new HashSet<string>(names);
                Debug.Log($"📂 Loaded {trackedShipNames.Count} tracked ships");
            }
        }

        private static void SaveTrackedShips()
        {
            string toSave = string.Join(";", trackedShipNames);
            EditorPrefs.SetString(TRACKED_SHIPS_KEY, toSave);
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            Event e = Event.current;

            // Handle keyboard input FIRST for preview mode (always active even if visualizer disabled)
            if (ShipComponentPreview.IsPreviewActive && e.type == EventType.KeyDown)
            {
                bool handled = false;
                switch (e.keyCode)
                {
                    case KeyCode.RightArrow:
                        ShipComponentPreview.NextComponent();
                        handled = true;
                        break;

                    case KeyCode.LeftArrow:
                        ShipComponentPreview.PreviousComponent();
                        handled = true;
                        break;

                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        ShipComponentPreview.ApplyCurrentPreview();
                        handled = true;
                        break;

                    case KeyCode.Escape:
                        ShipComponentPreview.ClearPreview();
                        handled = true;
                        break;
                }

                if (handled)
                {
                    e.Use();
                    sceneView.Repaint();
                    return;
                }
            }

            // Start preview if component selected and arrow key pressed
            if (ShipComponentSelector.HasSelection && !ShipComponentPreview.IsPreviewActive && e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.LeftArrow)
                {
                    ShipComponentPreview.StartPreview();
                    e.Use();
                    sceneView.Repaint();
                    return;
                }
            }

            // Draw preview info overlay
            if (ShipComponentPreview.IsPreviewActive)
            {
                DrawPreviewOverlay(sceneView);
            }

            // Only draw gizmos and handle selection if enabled
            if (!isEnabled) return;

            // Find all ship components to visualize (tracked ships + selected object)
            var shipComponents = FindAllRelevantShipComponents();

            if (shipComponents.Count == 0) return;

            // Draw gizmos for all ship components
            DrawComponentGizmos(shipComponents);

            // Handle component selection
            HandleComponentSelection(shipComponents);

            // Draw selection overlay
            if (ShipComponentSelector.HasSelection)
            {
                DrawSelectionOverlay(sceneView);
            }
        }

        private static List<GameObject> FindAllRelevantShipComponents()
        {
            List<GameObject> allComponents = new List<GameObject>();

            // 1. Find components in all tracked ships
            foreach (string shipName in trackedShipNames)
            {
                GameObject ship = GameObject.Find(shipName);
                if (ship != null)
                {
                    var components = FindShipComponentsInObject(ship);
                    allComponents.AddRange(components);
                }
            }

            // 2. Also include components from currently selected object (if not already included)
            if (Selection.activeGameObject != null)
            {
                var selectedComponents = FindShipComponentsInObject(Selection.activeGameObject);
                foreach (var comp in selectedComponents)
                {
                    if (!allComponents.Contains(comp))
                    {
                        allComponents.Add(comp);
                    }
                }
            }

            return allComponents;
        }

        private static List<GameObject> FindShipComponentsInObject(GameObject obj)
        {
            List<GameObject> components = new List<GameObject>();

            if (obj == null) return components;

            // Get all transforms in the object and its children
            Transform[] allTransforms = obj.GetComponentsInChildren<Transform>(true);

            foreach (Transform t in allTransforms)
            {
                // Check if this transform's gameObject is a ship component
                if (IsShipComponent(t.gameObject))
                {
                    components.Add(t.gameObject);
                }
            }

            return components;
        }

        private static bool IsShipComponent(GameObject obj)
        {
            string name = obj.name;
            string nameLower = name.ToLower();

            // FIRST: Exclude collision objects (most important check)
            if (nameLower.Contains("collision") || name.Contains("_collisions"))
            {
                return false;
            }

            // Check if parent is a ship component category
            if (obj.transform.parent != null)
            {
                string parentName = obj.transform.parent.name;
                if (parentName == "Masts" || parentName.Contains("Cannons") ||
                    parentName == "Bowsprits" || parentName == "Ship Parts")
                {
                    // Double-check it's not a collision object even if parent matches
                    if (nameLower.Contains("collision"))
                        return false;

                    return true;
                }
            }

            // Check by name patterns
            if (nameLower.StartsWith("location_"))
            {
                return true;
            }

            if (nameLower.Contains("pir_r_shp_"))
            {
                return true;
            }

            return false;
        }

        private static void DrawComponentGizmos(List<GameObject> components)
        {
            foreach (GameObject component in components)
            {
                if (component == null) continue;

                // Determine color based on type and selection
                Color gizmoColor = GetGizmoColorForComponent(component);

                // Draw sphere gizmo
                Handles.color = gizmoColor;
                Handles.SphereHandleCap(0, component.transform.position, Quaternion.identity, COMPONENT_GIZMO_SIZE, EventType.Repaint);

                // Draw label
                GUIStyle labelStyle = new GUIStyle(EditorStyles.label);
                labelStyle.normal.textColor = Color.white;
                labelStyle.fontStyle = FontStyle.Bold;

                Handles.Label(component.transform.position + Vector3.up * 2f, component.name, labelStyle);
            }
        }

        private static Color GetGizmoColorForComponent(GameObject component)
        {
            // Highlighted if selected
            if (ShipComponentSelector.SelectedComponent == component)
            {
                return Color.yellow;
            }

            // Determine color by component type
            string name = component.name.ToLower();
            Transform parent = component.transform.parent;

            if (parent != null)
            {
                string parentName = parent.name;
                if (parentName == "Masts") return Color.green;
                if (parentName.Contains("Cannons")) return Color.red;
                if (parentName == "Bowsprits") return Color.cyan;
                if (parentName == "Ship Parts")
                {
                    if (name.Contains("wheel")) return Color.blue;
                    if (name.Contains("ram")) return Color.magenta;
                    return Color.gray;
                }
            }

            // Default
            return Color.white;
        }

        private static void HandleComponentSelection(List<GameObject> components)
        {
            Event e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                GameObject closestComponent = null;
                float closestDistance = float.MaxValue;

                foreach (GameObject component in components)
                {
                    if (component == null) continue;

                    float distance = Vector3.Distance(ray.origin, component.transform.position);
                    Vector3 toComponent = component.transform.position - ray.origin;
                    float projection = Vector3.Dot(toComponent, ray.direction);

                    if (projection > 0)
                    {
                        Vector3 closestPoint = ray.origin + ray.direction * projection;
                        float distanceToRay = Vector3.Distance(closestPoint, component.transform.position);

                        if (distanceToRay < COMPONENT_CLICK_RADIUS && distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestComponent = component;
                        }
                    }
                }

                if (closestComponent != null)
                {
                    ShipComponentSelector.SelectComponent(closestComponent);
                    e.Use();
                    SceneView.RepaintAll();
                }
            }

            // Allow deselection with Escape (if not in preview mode)
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                if (!ShipComponentPreview.IsPreviewActive && ShipComponentSelector.HasSelection)
                {
                    ShipComponentSelector.ClearSelection();
                    e.Use();
                    SceneView.RepaintAll();
                }
            }
        }

        private static void DrawPreviewOverlay(SceneView sceneView)
        {
            Handles.BeginGUI();

            GUILayout.BeginArea(new Rect(10, sceneView.position.height - 120, 400, 110));

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTex(2, 2, new Color(0, 0, 0, 0.8f));

            GUILayout.BeginVertical(boxStyle);

            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.normal.textColor = Color.cyan;
            GUILayout.Label("👻 Component Preview Mode", headerStyle);

            GUIStyle textStyle = new GUIStyle(EditorStyles.label);
            textStyle.normal.textColor = Color.white;

            GameObject currentPreview = ShipComponentPreview.CurrentPreviewComponent;
            if (currentPreview != null)
            {
                GUILayout.Label($"Component: {currentPreview.name} ({ShipComponentPreview.CurrentIndex + 1}/{ShipComponentPreview.TotalComponents})", textStyle);
            }

            GUILayout.Space(5);
            GUILayout.Label("← → Cycle Components | Enter: Apply | Esc: Cancel", textStyle);

            GUILayout.EndVertical();
            GUILayout.EndArea();

            Handles.EndGUI();
        }

        private static void DrawSelectionOverlay(SceneView sceneView)
        {
            Handles.BeginGUI();

            GUILayout.BeginArea(new Rect(10, sceneView.position.height - 100, 400, 90));

            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTex(2, 2, new Color(0, 0, 0, 0.8f));

            GUILayout.BeginVertical(boxStyle);

            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.normal.textColor = Color.yellow;
            GUILayout.Label("⚙️ Component Selected", headerStyle);

            GUIStyle textStyle = new GUIStyle(EditorStyles.label);
            textStyle.normal.textColor = Color.white;

            GUILayout.Label($"Name: {ShipComponentSelector.SelectedComponent.name}", textStyle);
            GUILayout.Label($"Type: {ShipComponentSelector.SelectedType}", textStyle);

            GUILayout.Space(5);
            GUILayout.Label("Press ← or → to start cycling components", textStyle);

            GUILayout.EndVertical();
            GUILayout.EndArea();

            Handles.EndGUI();
        }

        private static Texture2D MakeTex(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();

            return texture;
        }
    }
}
