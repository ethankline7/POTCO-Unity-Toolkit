using UnityEditor;
using UnityEngine;

namespace Toontown.Editor
{
    public sealed class ToontownWorldDataExporter : EditorWindow
    {
        [MenuItem("Toontown/World Data/Exporter")]
        public static void ShowWindow()
        {
            GetWindow<ToontownWorldDataExporter>("Toontown Exporter");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Toontown World Data Exporter", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Scaffold ready. Export pipeline will be added once Toontown object/property mapping is defined.",
                MessageType.Info);

            if (GUILayout.Button("Open Migration Plan"))
            {
                Debug.Log("See docs/TOONTOWN_MIGRATION_PLAN.md for implementation phases.");
            }
        }
    }
}