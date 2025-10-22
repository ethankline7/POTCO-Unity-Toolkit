using UnityEditor;
using UnityEngine;
using POTCO.VisZones;

namespace POTCO.Editor
{
    [CustomEditor(typeof(VisZoneManager))]
    public class VisZoneManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            VisZoneManager manager = (VisZoneManager)target;

            GUILayout.Space(10);
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Runtime Information", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                // Show current zone
                string currentZone = manager.GetCurrentZone();
                if (!string.IsNullOrEmpty(currentZone))
                {
                    EditorGUILayout.LabelField("Current Zone:", currentZone, EditorStyles.boldLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("Current Zone:", "None", EditorStyles.miniLabel);
                }

                GUILayout.Space(5);

                // Show visible zones
                var visibleZones = manager.GetVisibleZones();
                if (visibleZones != null && visibleZones.Count > 0)
                {
                    EditorGUILayout.LabelField($"Visible Zones ({visibleZones.Count}):", EditorStyles.boldLabel);
                    foreach (string zone in visibleZones)
                    {
                        EditorGUI.indentLevel++;
                        bool isCurrent = zone == currentZone;
                        EditorGUILayout.LabelField(isCurrent ? $"• {zone} (current)" : $"  {zone}");
                        EditorGUI.indentLevel--;
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Visible Zones:", "None", EditorStyles.miniLabel);
                }

                GUILayout.Space(5);

                // Show section status
                if (manager.zoneSections != null && manager.zoneSections.Count > 0)
                {
                    EditorGUILayout.LabelField($"Section Status ({manager.zoneSections.Count} total):", EditorStyles.boldLabel);

                    int visibleCount = 0;
                    int hiddenCount = 0;

                    foreach (var section in manager.zoneSections)
                    {
                        if (section != null)
                        {
                            if (section.IsVisible)
                                visibleCount++;
                            else
                                hiddenCount++;
                        }
                    }

                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField($"✓ Visible: {visibleCount}");
                    EditorGUILayout.LabelField($"✗ Hidden: {hiddenCount}");
                    EditorGUI.indentLevel--;
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see runtime information", MessageType.Info);
            }

            EditorGUILayout.EndVertical();

            GUILayout.Space(5);

            // Debug buttons
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Debug Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Refresh Zone Sections"))
            {
                manager.RefreshZoneSections();
                EditorUtility.SetDirty(manager);
            }

            if (Application.isPlaying)
            {
                GUILayout.Space(5);
                EditorGUILayout.LabelField("Quick Zone Tests:", EditorStyles.boldLabel);

                if (manager.visZoneData != null && manager.visZoneData.visTable != null)
                {
                    EditorGUILayout.BeginHorizontal();

                    int buttonCount = 0;
                    foreach (var entry in manager.visZoneData.visTable)
                    {
                        if (GUILayout.Button(entry.zoneName, GUILayout.Height(20)))
                        {
                            manager.SetCurrentZone(entry.zoneName);
                        }

                        buttonCount++;
                        if (buttonCount >= 3)
                        {
                            buttonCount = 0;
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.BeginHorizontal();
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();

            if (Application.isPlaying)
            {
                Repaint();
            }
        }
    }
}
