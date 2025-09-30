using UnityEngine;
using UnityEditor;
using System.Linq;

namespace CaveGenerator
{
    [InitializeOnLoad]
    public static class CaveConnectorVisualizer
    {
        private const float CONNECTOR_SIZE = 8.0f;
        private const float CONNECTOR_CLICK_RADIUS = 10.0f;

        static CaveConnectorVisualizer()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            Event e = Event.current;

            // Handle keyboard input FIRST for preview mode to intercept before camera controls
            if (CaveConnectorPreview.IsPreviewActive && e.type == EventType.KeyDown)
            {
                bool handled = false;
                switch (e.keyCode)
                {
                    case KeyCode.RightArrow:
                        CaveConnectorPreview.NextPiece();
                        handled = true;
                        break;

                    case KeyCode.LeftArrow:
                        CaveConnectorPreview.PreviousPiece();
                        handled = true;
                        break;

                    case KeyCode.UpArrow:
                        Debug.Log("⬆️ Up Arrow pressed - switching connector");
                        CaveConnectorPreview.NextConnector();
                        handled = true;
                        break;

                    case KeyCode.DownArrow:
                        Debug.Log("⬇️ Down Arrow pressed - switching connector");
                        CaveConnectorPreview.PreviousConnector();
                        handled = true;
                        break;

                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        var placed = CaveConnectorPreview.PlaceCurrentPreview();
                        if (placed != null)
                        {
                            Selection.activeGameObject = placed;
                        }
                        handled = true;
                        break;

                    case KeyCode.Escape:
                        CaveConnectorPreview.ClearPreview();
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

            // Start preview if connector selected and arrow key pressed
            if (CaveConnectorSelector.HasSelection && !CaveConnectorPreview.IsPreviewActive && e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.LeftArrow)
                {
                    CaveConnectorPreview.StartPreview();
                    e.Use();
                    sceneView.Repaint();
                    return;
                }
            }

            // Draw UI overlay if preview is active
            if (CaveConnectorPreview.IsPreviewActive)
            {
                DrawPreviewUI(sceneView);
            }

            if (Selection.activeGameObject == null) return;

            // Find all connectors in the selected object and its children
            var connectors = Selection.activeGameObject.GetComponentsInChildren<Transform>()
                .Where(t => t.name.StartsWith("cave_connector_"))
                .ToList();

            if (connectors.Count == 0) return;

            // Draw all connectors
            foreach (var connector in connectors)
            {
                if (connector == null) continue;

                bool isSelected = CaveConnectorSelector.SelectedConnector == connector;
                DrawConnector(connector, isSelected);
            }

            // Handle mouse clicks
            HandleConnectorSelection(connectors);
        }

        private static void DrawConnector(Transform connector, bool isSelected)
        {
            Vector3 position = connector.position;
            Vector3 direction = connector.forward;

            // Choose color based on selection
            Color connectorColor = isSelected ? Color.cyan : Color.green;
            Color directionColor = isSelected ? Color.yellow : Color.blue;

            Handles.color = connectorColor;

            // Draw sphere at connector position
            Handles.SphereHandleCap(0, position, Quaternion.identity, CONNECTOR_SIZE, EventType.Repaint);

            // Draw direction arrow
            Handles.color = directionColor;
            Handles.ArrowHandleCap(0, position, Quaternion.LookRotation(direction), 2f, EventType.Repaint);

            // Draw label
            Handles.Label(position + Vector3.up * 0.5f,
                isSelected ? $"✓ {connector.name}" : connector.name,
                new GUIStyle(EditorStyles.label)
                {
                    normal = new GUIStyleState { textColor = isSelected ? Color.cyan : Color.white },
                    fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal
                });
        }

        private static void HandleConnectorSelection(System.Collections.Generic.List<Transform> connectors)
        {
            Event e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

                // Find closest connector to click
                Transform closestConnector = null;
                float closestDistance = float.MaxValue;

                foreach (var connector in connectors)
                {
                    if (connector == null) continue;

                    float distance = Vector3.Cross(ray.direction, connector.position - ray.origin).magnitude;

                    if (distance < CONNECTOR_CLICK_RADIUS && distance < closestDistance)
                    {
                        closestConnector = connector;
                        closestDistance = distance;
                    }
                }

                if (closestConnector != null)
                {
                    // Toggle selection
                    if (CaveConnectorSelector.SelectedConnector == closestConnector)
                    {
                        CaveConnectorSelector.ClearSelection();
                    }
                    else
                    {
                        CaveConnectorSelector.SelectConnector(closestConnector);
                    }

                    e.Use();
                }
            }

            // Allow deselection with Escape (if not in preview mode)
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                if (!CaveConnectorPreview.IsPreviewActive && CaveConnectorSelector.HasSelection)
                {
                    CaveConnectorSelector.ClearSelection();
                    e.Use();
                }
            }
        }

        private static void DrawPreviewUI(SceneView sceneView)
        {
            Handles.BeginGUI();

            // Draw semi-transparent background panel
            GUILayout.BeginArea(new Rect(10, 10, 400, 140));
            GUI.Box(new Rect(0, 0, 400, 140), "", EditorStyles.helpBox);

            GUILayout.BeginVertical();
            GUILayout.Space(10);

            // Title
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 14;
            titleStyle.normal.textColor = Color.cyan;
            GUILayout.Label("🔗 Cave Piece Preview", titleStyle);

            GUILayout.Space(5);

            // Current piece info
            var currentPiece = CaveConnectorPreview.CurrentPreviewPiece;
            if (currentPiece != null)
            {
                GUIStyle nameStyle = new GUIStyle(EditorStyles.label);
                nameStyle.fontSize = 12;
                nameStyle.normal.textColor = Color.white;
                GUILayout.Label($"Piece: {currentPiece.name}", nameStyle);

                // Progress indicator
                GUILayout.Label($"({CaveConnectorPreview.CurrentIndex + 1} / {CaveConnectorPreview.TotalPieces})", nameStyle);
            }

            GUILayout.Space(5);

            // Connector info
            if (CaveConnectorPreview.TotalConnectors > 0)
            {
                GUIStyle connectorStyle = new GUIStyle(EditorStyles.label);
                connectorStyle.fontSize = 11;
                connectorStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                GUILayout.Label($"Connector: {CaveConnectorPreview.CurrentConnectorIndex + 1} / {CaveConnectorPreview.TotalConnectors}", connectorStyle);
            }

            GUILayout.Space(10);

            // Controls hint
            GUIStyle hintStyle = new GUIStyle(EditorStyles.miniLabel);
            hintStyle.normal.textColor = Color.yellow;
            GUILayout.Label("← → Arrow Keys: Cycle pieces", hintStyle);
            GUILayout.Label("↑ ↓ Arrow Keys: Cycle connectors", hintStyle);
            GUILayout.Label("Enter: Place piece  |  Escape: Cancel", hintStyle);

            GUILayout.EndVertical();
            GUILayout.EndArea();

            Handles.EndGUI();
        }
    }
}
