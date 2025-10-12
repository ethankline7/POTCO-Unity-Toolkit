using UnityEditor;
using UnityEngine;
using POTCO.VisZones;

namespace POTCO.Editor
{
    [CustomEditor(typeof(VisZoneSensor))]
    public class VisZoneSensorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            VisZoneSensor sensor = (VisZoneSensor)target;

            GUILayout.Space(10);
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Runtime Information", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                // Show detected zone
                string currentZone = sensor.GetCurrentZone();
                if (!string.IsNullOrEmpty(currentZone))
                {
                    EditorGUILayout.LabelField("Detected Zone:", currentZone, EditorStyles.boldLabel);
                }
                else
                {
                    EditorGUILayout.HelpBox("Not in any zone - move player into a collision_zone_* trigger", MessageType.Warning);
                }

                GUILayout.Space(5);

                // Show zone manager status
                if (sensor.zoneManager != null)
                {
                    EditorGUILayout.LabelField("Zone Manager:", "Connected ✓", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField("Manager Zone:", sensor.zoneManager.GetCurrentZone());
                }
                else
                {
                    EditorGUILayout.HelpBox("No VisZoneManager found! Make sure the scene has a VisZoneManager component.", MessageType.Error);
                }

                GUILayout.Space(5);

                // Show collider info
                Collider playerCollider = sensor.GetComponent<Collider>();
                if (playerCollider != null)
                {
                    if (playerCollider.isTrigger)
                    {
                        EditorGUILayout.HelpBox("Player collider is set to 'Is Trigger'. This may prevent zone detection. Set 'Is Trigger' to false on player collider.", MessageType.Warning);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Player Collider:", "Configured ✓", EditorStyles.miniLabel);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No Collider found on player! Add a Collider component for zone detection to work.", MessageType.Error);
                }

                // Show rigidbody info
                Rigidbody playerRigidbody = sensor.GetComponent<Rigidbody>();
                if (playerRigidbody != null)
                {
                    EditorGUILayout.LabelField("Player Rigidbody:", "Found ✓", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.HelpBox("No Rigidbody found on player! Add a Rigidbody component for trigger detection to work.", MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see runtime information", MessageType.Info);

                // Pre-flight checks even when not playing
                GUILayout.Space(5);
                EditorGUILayout.LabelField("Pre-Flight Checks:", EditorStyles.boldLabel);

                Collider playerCollider = sensor.GetComponent<Collider>();
                if (playerCollider == null)
                {
                    EditorGUILayout.HelpBox("⚠ No Collider found - add a Collider component", MessageType.Warning);
                }
                else if (playerCollider.isTrigger)
                {
                    EditorGUILayout.HelpBox("⚠ Player collider should not be a trigger", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.LabelField("✓ Collider configured correctly");
                }

                Rigidbody playerRigidbody = sensor.GetComponent<Rigidbody>();
                if (playerRigidbody == null)
                {
                    EditorGUILayout.HelpBox("⚠ No Rigidbody found - add a Rigidbody component", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.LabelField("✓ Rigidbody found");
                }

                if (sensor.zoneManager == null)
                {
                    VisZoneManager manager = FindFirstObjectByType<VisZoneManager>();
                    if (manager == null)
                    {
                        EditorGUILayout.HelpBox("⚠ No VisZoneManager in scene - import with 'Enable VisZones'", MessageType.Warning);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("✓ VisZoneManager found in scene (will auto-connect)");
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("✓ VisZoneManager reference set");
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
