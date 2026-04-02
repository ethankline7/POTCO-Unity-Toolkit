using UnityEditor;
using UnityEngine;

namespace Toontown.Editor
{
    public sealed class ToontownWorldDataImporter : EditorWindow
    {
        [MenuItem("Toontown/World Data/Importer")]
        public static void ShowWindow()
        {
            GetWindow<ToontownWorldDataImporter>("Toontown Importer");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Toontown World Data Importer", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Scaffold ready. This window is intentionally minimal until Toontown format adapters are implemented.",
                MessageType.Info);

            if (GUILayout.Button("Open Migration Plan"))
            {
                Debug.Log("See docs/TOONTOWN_MIGRATION_PLAN.md for implementation phases.");
            }
        }
    }
}