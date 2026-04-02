using UnityEditor;
using UnityEngine;

namespace Toolkit.Editor.WorldData
{
    public sealed class WorldDataRouterWindow : EditorWindow
    {
        [MenuItem("Toolkit/World Data/Router")]
        public static void ShowWindow()
        {
            GetWindow<WorldDataRouterWindow>("World Data Router");
        }

        private void OnGUI()
        {
            var launcher = WorldDataToolLauncherRegistry.GetActiveLauncher();
            var adapter = WorldDataFormatAdapterRegistry.GetActiveAdapter();
            EditorGUILayout.LabelField("World Data Router", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Active Route", launcher.DisplayName);
            EditorGUILayout.LabelField("Importer Menu", launcher.ImporterMenuPath);
            EditorGUILayout.LabelField("Exporter Menu", launcher.ExporterMenuPath);
            EditorGUILayout.LabelField("Format Adapter", adapter.FormatId);
            EditorGUILayout.Space();

            if (GUILayout.Button("Open Active Importer"))
            {
                if (!launcher.OpenImporter())
                {
                    Debug.LogError($"Could not open importer menu path: {launcher.ImporterMenuPath}");
                }
            }

            if (GUILayout.Button("Open Active Exporter"))
            {
                if (!launcher.OpenExporter())
                {
                    Debug.LogError($"Could not open exporter menu path: {launcher.ExporterMenuPath}");
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Open Toolkit Settings"))
            {
                if (!EditorApplication.ExecuteMenuItem("Toolkit/Settings"))
                {
                    Debug.LogError("Could not open Toolkit/Settings.");
                }
            }

            EditorGUILayout.HelpBox(
                "Set the active game in Toolkit/Settings. This router keeps POTCO tools unchanged while enabling Toontown-specific entry points.",
                MessageType.Info);
        }
    }
}
