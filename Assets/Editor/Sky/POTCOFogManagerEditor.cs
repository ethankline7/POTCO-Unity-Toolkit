using UnityEngine;
using UnityEditor;

namespace POTCO.Sky.Editor
{
    /// <summary>
    /// Custom inspector for POTCOFogManager with helpful buttons
    /// </summary>
    [CustomEditor(typeof(POTCOFogManager))]
    public class POTCOFogManagerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            POTCOFogManager fogManager = (POTCOFogManager)target;

            // Info box
            EditorGUILayout.HelpBox(
                "Fog system automatically syncs with SkyboxManager's time-of-day.\n\n" +
                "• Manual Preset Mode: Fog matches current skybox preset\n" +
                "• Automatic Mode: Fog transitions smoothly with time\n" +
                "• Use 'Enable Fog' toggle above to turn fog on/off",
                MessageType.Info);

            EditorGUILayout.Space();

            // Quick actions
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Overrides"))
            {
                Undo.RecordObject(fogManager, "Reset Fog Overrides");
                fogManager.ResetOverrides();
            }

            if (GUILayout.Button("Enable Fog"))
            {
                Undo.RecordObject(fogManager, "Enable Fog");
                fogManager.enableFog = true;
                RenderSettings.fog = true;
            }

            if (GUILayout.Button("Disable Fog"))
            {
                Undo.RecordObject(fogManager, "Disable Fog");
                fogManager.enableFog = false;
                RenderSettings.fog = false;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Current state info
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Runtime Info", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Fog Enabled:", RenderSettings.fog.ToString());
                EditorGUILayout.LabelField("Fog Mode:", RenderSettings.fogMode.ToString());
                EditorGUILayout.LabelField("Fog Color:", RenderSettings.fogColor.ToString());
                EditorGUILayout.LabelField("Fog Density:", RenderSettings.fogDensity.ToString("F6"));

                EditorGUILayout.Space();
            }

            // Draw default inspector
            DrawDefaultInspector();

            // Preset testing buttons (only in play mode)
            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Test Fog Presets", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Day"))
                    fogManager.SetFogPreset(SkyboxManager.TODPreset.Day, 2f);
                if (GUILayout.Button("Sunset"))
                    fogManager.SetFogPreset(SkyboxManager.TODPreset.Sunset, 2f);
                if (GUILayout.Button("Night"))
                    fogManager.SetFogPreset(SkyboxManager.TODPreset.Night, 2f);

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Stars"))
                    fogManager.SetFogPreset(SkyboxManager.TODPreset.Stars, 2f);
                if (GUILayout.Button("Overcast"))
                    fogManager.SetFogPreset(SkyboxManager.TODPreset.Overcast, 2f);
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
