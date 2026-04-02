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
            var route = WorldDataToolRouteResolver.ResolveActiveRoute();
            var adapter = WorldDataFormatAdapterRegistry.GetActiveAdapter();
            EditorGUILayout.LabelField("World Data Router", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Active Route", route.DisplayName);
            EditorGUILayout.LabelField("Importer Menu", route.ImporterMenuPath);
            EditorGUILayout.LabelField("Exporter Menu", route.ExporterMenuPath);
            EditorGUILayout.LabelField("Format Adapter", adapter.FormatId);
            EditorGUILayout.Space();

            if (GUILayout.Button("Open Active Importer"))
            {
                if (!EditorApplication.ExecuteMenuItem(route.ImporterMenuPath))
                {
                    Debug.LogError($"Could not open importer menu path: {route.ImporterMenuPath}");
                }
            }

            if (GUILayout.Button("Open Active Exporter"))
            {
                if (!EditorApplication.ExecuteMenuItem(route.ExporterMenuPath))
                {
                    Debug.LogError($"Could not open exporter menu path: {route.ExporterMenuPath}");
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
