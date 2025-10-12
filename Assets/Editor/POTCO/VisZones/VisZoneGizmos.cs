using UnityEngine;
using UnityEditor;
using POTCO.VisZones;
using System.Collections.Generic;
using System.Linq;

namespace POTCO.Editor
{
    /// <summary>
    /// Custom scene view gizmos for VisZones
    /// Draws zone volumes, neighbor connections, and member highlights
    /// </summary>
    [InitializeOnLoad]
    public static class VisZoneGizmos
    {
        private static bool showZoneVolumes = true;
        private static bool showNeighborConnections = true;
        private static bool showMemberHighlights = true;
        private static bool showZoneLabels = true;

        static VisZoneGizmos()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            // Get preferences
            showZoneVolumes = EditorPrefs.GetBool("VisZone_ShowVolumes", true);
            showNeighborConnections = EditorPrefs.GetBool("VisZone_ShowConnections", true);
            showMemberHighlights = EditorPrefs.GetBool("VisZone_ShowMembers", true);
            showZoneLabels = EditorPrefs.GetBool("VisZone_ShowLabels", true);

            // Find all zone volumes
            VisZoneVolume[] allZones = Object.FindObjectsByType<VisZoneVolume>(FindObjectsSortMode.None);
            if (allZones.Length == 0)
                return;

            // Find selected zone
            VisZoneVolume selectedZone = null;
            if (Selection.activeGameObject != null)
            {
                selectedZone = Selection.activeGameObject.GetComponent<VisZoneVolume>();
                if (selectedZone == null)
                {
                    // Check if selected object is inside a zone section
                    VisZoneSection parentSection = Selection.activeGameObject.GetComponentInParent<VisZoneSection>();
                    if (parentSection != null)
                    {
                        selectedZone = allZones.FirstOrDefault(z => z.zoneName == parentSection.zoneName);
                    }
                }
            }

            // Draw all zones
            foreach (VisZoneVolume zone in allZones)
            {
                if (zone == null || zone.zoneCollider == null)
                    continue;

                bool isSelected = zone == selectedZone;

                // Draw zone volume
                if (showZoneVolumes)
                {
                    DrawZoneVolume(zone, isSelected);
                }

                // Draw zone label
                if (showZoneLabels)
                {
                    DrawZoneLabel(zone, isSelected);
                }

                // Draw neighbor connections for selected zone
                if (showNeighborConnections && isSelected)
                {
                    DrawNeighborConnections(zone, allZones);
                }

                // Draw member highlights for selected zone
                if (showMemberHighlights && isSelected)
                {
                    DrawMemberHighlights(zone);
                }
            }

            // Draw gizmo controls
            DrawGizmoControls(sceneView);
        }

        /// <summary>
        /// Draw zone volume wireframe with tinted color
        /// </summary>
        private static void DrawZoneVolume(VisZoneVolume zone, bool isSelected)
        {
            Bounds bounds = zone.GetBounds();
            Color color = zone.displayColor;

            if (isSelected)
            {
                color.a = 0.5f;
                Handles.color = color;

                // Draw filled cube for selected zone
                Vector3 center = bounds.center;
                Vector3 size = bounds.size;

                // Draw filled sides
                Handles.DrawSolidRectangleWithOutline(
                    new Vector3[] {
                        center + new Vector3(-size.x/2, -size.y/2, -size.z/2),
                        center + new Vector3(size.x/2, -size.y/2, -size.z/2),
                        center + new Vector3(size.x/2, size.y/2, -size.z/2),
                        center + new Vector3(-size.x/2, size.y/2, -size.z/2)
                    },
                    new Color(color.r, color.g, color.b, 0.1f),
                    color
                );
            }
            else
            {
                color.a = 0.3f;
            }

            Handles.color = color;

            // Draw wireframe cube
            DrawWireCube(bounds.center, bounds.size);

            // Draw pivot sphere at bounds center
            if (isSelected)
            {
                Handles.color = Color.white;
                Handles.SphereHandleCap(0, bounds.center, Quaternion.identity, 2f, EventType.Repaint);
            }
        }

        /// <summary>
        /// Draw zone name label
        /// </summary>
        private static void DrawZoneLabel(VisZoneVolume zone, bool isSelected)
        {
            Bounds bounds = zone.GetBounds();
            Vector3 labelPos = bounds.center + Vector3.up * (bounds.extents.y + 2f);

            // Create style with background for better visibility
            GUIStyle style = new GUIStyle(EditorStyles.whiteLargeLabel);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
            style.fontSize = isSelected ? 16 : 13;

            // Set text color with better contrast
            if (isSelected)
            {
                style.normal.textColor = Color.yellow;
            }
            else
            {
                // Ensure label is always visible with high contrast
                Color labelColor = zone.displayColor;
                if (labelColor.grayscale < 0.5f)
                {
                    labelColor = Color.Lerp(labelColor, Color.white, 0.5f);
                }
                style.normal.textColor = labelColor;
            }

            // Add shadow/background for better visibility
            GUIContent content = new GUIContent(zone.zoneName);

            // Save original color
            Color originalColor = GUI.color;

            // Draw background (shadow)
            Color shadowColor = new Color(0, 0, 0, 0.7f);
            GUI.color = shadowColor;
            Handles.Label(labelPos + Vector3.one * 0.1f, content, style);

            // Draw foreground text
            GUI.color = Color.white;
            Handles.Label(labelPos, content, style);

            // Restore original color
            GUI.color = originalColor;
        }

        /// <summary>
        /// Draw neighbor connection lines
        /// </summary>
        private static void DrawNeighborConnections(VisZoneVolume selectedZone, VisZoneVolume[] allZones)
        {
            VisZoneManager manager = Object.FindFirstObjectByType<VisZoneManager>();
            if (manager == null || manager.visZoneData == null)
                return;

            VisZoneEntry entry = manager.visZoneData.visTable.Find(e => e.zoneName == selectedZone.zoneName);
            if (entry == null)
                return;

            Bounds selectedBounds = selectedZone.GetBounds();

            foreach (string neighborName in entry.visibleZones)
            {
                VisZoneVolume neighborZone = allZones.FirstOrDefault(z => z.zoneName == neighborName);
                if (neighborZone == null || neighborZone.zoneCollider == null)
                    continue;

                Bounds neighborBounds = neighborZone.GetBounds();

                // Check symmetry
                int symmetryStatus = manager.visZoneData.GetNeighborSymmetryStatus(selectedZone.zoneName, neighborName);

                Color lineColor;
                if (symmetryStatus == 2)
                {
                    lineColor = Color.green; // Symmetric
                }
                else if (symmetryStatus == 1)
                {
                    lineColor = new Color(1f, 0.5f, 0f); // Orange for one-way
                }
                else
                {
                    lineColor = Color.red; // Error
                }

                lineColor.a = 0.6f;
                Handles.color = lineColor;

                // Draw dashed line
                Vector3 start = selectedBounds.center;
                Vector3 end = neighborBounds.center;

                DrawDashedLine(start, end, 5f);

                // Draw direction arrow for one-way connections
                if (symmetryStatus == 1)
                {
                    Vector3 direction = (end - start).normalized;
                    Vector3 arrowPos = Vector3.Lerp(start, end, 0.7f);
                    DrawArrow(arrowPos, direction, lineColor);
                }
            }
        }

        /// <summary>
        /// Draw member highlights
        /// </summary>
        private static void DrawMemberHighlights(VisZoneVolume zone)
        {
            if (zone == null || zone.sectionRoot == null)
                return;

            // Use bright contrasting color for member highlights
            Color highlightColor = zone.displayColor;
            highlightColor = Color.Lerp(highlightColor, Color.white, 0.3f); // Brighten
            highlightColor.a = 0.6f; // More opaque for visibility

            int memberCount = 0;

            foreach (Transform child in zone.sectionRoot.transform)
            {
                if (child == null)
                    continue;

                // Get all renderers in this member
                Renderer[] renderers = child.GetComponentsInChildren<Renderer>();

                if (renderers.Length == 0)
                    continue;

                memberCount++;

                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null)
                        continue;

                    Bounds bounds = renderer.bounds;

                    // Draw thicker wireframe by drawing multiple lines
                    Handles.color = highlightColor;
                    DrawWireCube(bounds.center, bounds.size * 1.05f);

                    // Draw second layer for thickness
                    Color thickerColor = highlightColor;
                    thickerColor.a *= 0.5f;
                    Handles.color = thickerColor;
                    DrawWireCube(bounds.center, bounds.size * 1.08f);

                    // Draw label at object center
                    if (memberCount <= 10) // Limit labels to first 10 to avoid clutter
                    {
                        GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel);
                        labelStyle.normal.textColor = Color.white;
                        labelStyle.alignment = TextAnchor.MiddleCenter;

                        Handles.Label(bounds.center, child.name, labelStyle);
                    }
                }
            }

            // If no members found, show debug message
            if (memberCount == 0)
            {
                Bounds zoneBounds = zone.GetBounds();
                Vector3 labelPos = zoneBounds.center;

                GUIStyle style = new GUIStyle(EditorStyles.helpBox);
                style.normal.textColor = Color.yellow;
                style.alignment = TextAnchor.MiddleCenter;

                Handles.Label(labelPos, $"Zone '{zone.zoneName}' has no members", style);
            }
        }

        /// <summary>
        /// Draw wireframe cube
        /// </summary>
        private static void DrawWireCube(Vector3 center, Vector3 size)
        {
            Vector3 halfSize = size * 0.5f;

            // Bottom face
            Vector3 p0 = center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
            Vector3 p1 = center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
            Vector3 p2 = center + new Vector3(halfSize.x, -halfSize.y, halfSize.z);
            Vector3 p3 = center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z);

            // Top face
            Vector3 p4 = center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
            Vector3 p5 = center + new Vector3(halfSize.x, halfSize.y, -halfSize.z);
            Vector3 p6 = center + new Vector3(halfSize.x, halfSize.y, halfSize.z);
            Vector3 p7 = center + new Vector3(-halfSize.x, halfSize.y, halfSize.z);

            // Bottom face
            Handles.DrawLine(p0, p1);
            Handles.DrawLine(p1, p2);
            Handles.DrawLine(p2, p3);
            Handles.DrawLine(p3, p0);

            // Top face
            Handles.DrawLine(p4, p5);
            Handles.DrawLine(p5, p6);
            Handles.DrawLine(p6, p7);
            Handles.DrawLine(p7, p4);

            // Vertical edges
            Handles.DrawLine(p0, p4);
            Handles.DrawLine(p1, p5);
            Handles.DrawLine(p2, p6);
            Handles.DrawLine(p3, p7);
        }

        /// <summary>
        /// Draw dashed line
        /// </summary>
        private static void DrawDashedLine(Vector3 start, Vector3 end, float dashSize)
        {
            float distance = Vector3.Distance(start, end);
            Vector3 direction = (end - start).normalized;

            float currentDist = 0f;
            bool drawDash = true;

            while (currentDist < distance)
            {
                float segmentLength = Mathf.Min(dashSize, distance - currentDist);
                Vector3 segmentStart = start + direction * currentDist;
                Vector3 segmentEnd = segmentStart + direction * segmentLength;

                if (drawDash)
                {
                    Handles.DrawLine(segmentStart, segmentEnd);
                }

                currentDist += segmentLength;
                drawDash = !drawDash;
            }
        }

        /// <summary>
        /// Draw direction arrow
        /// </summary>
        private static void DrawArrow(Vector3 position, Vector3 direction, Color color)
        {
            Handles.color = color;

            float arrowSize = 3f;
            Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
            Vector3 left = -right;

            Vector3 arrowTip = position + direction * arrowSize;
            Vector3 arrowLeft = position + left * arrowSize * 0.5f;
            Vector3 arrowRight = position + right * arrowSize * 0.5f;

            Handles.DrawLine(arrowTip, arrowLeft);
            Handles.DrawLine(arrowTip, arrowRight);
        }

        /// <summary>
        /// Draw gizmo controls overlay
        /// </summary>
        private static void DrawGizmoControls(SceneView sceneView)
        {
            Handles.BeginGUI();

            GUILayout.BeginArea(new Rect(10, sceneView.position.height - 150, 200, 140));
            GUILayout.BeginVertical("box");

            GUILayout.Label("VisZone Gizmos", EditorStyles.boldLabel);

            bool newShowVolumes = GUILayout.Toggle(showZoneVolumes, "Show Zone Volumes");
            if (newShowVolumes != showZoneVolumes)
            {
                EditorPrefs.SetBool("VisZone_ShowVolumes", newShowVolumes);
                showZoneVolumes = newShowVolumes;
                sceneView.Repaint();
            }

            bool newShowConnections = GUILayout.Toggle(showNeighborConnections, "Show Neighbor Connections");
            if (newShowConnections != showNeighborConnections)
            {
                EditorPrefs.SetBool("VisZone_ShowConnections", newShowConnections);
                showNeighborConnections = newShowConnections;
                sceneView.Repaint();
            }

            bool newShowMembers = GUILayout.Toggle(showMemberHighlights, "Show Member Highlights");
            if (newShowMembers != showMemberHighlights)
            {
                EditorPrefs.SetBool("VisZone_ShowMembers", newShowMembers);
                showMemberHighlights = newShowMembers;
                sceneView.Repaint();
            }

            bool newShowLabels = GUILayout.Toggle(showZoneLabels, "Show Zone Labels");
            if (newShowLabels != showZoneLabels)
            {
                EditorPrefs.SetBool("VisZone_ShowLabels", newShowLabels);
                showZoneLabels = newShowLabels;
                sceneView.Repaint();
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();

            Handles.EndGUI();
        }
    }
}
