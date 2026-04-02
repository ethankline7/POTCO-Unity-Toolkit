using Toolkit.Core;
using UnityEditor;
using UnityEngine;

namespace Toolkit.Editor
{
    public sealed class ToolkitSettingsWindow : EditorWindow
    {
        private const string SettingsDirectory = "Assets/Resources/Toolkit";
        private const string SettingsAssetPath = SettingsDirectory + "/ToolkitProjectSettings.asset";

        private ToolkitProjectSettings settings;

        [MenuItem("Toolkit/Settings")]
        public static void ShowWindow()
        {
            GetWindow<ToolkitSettingsWindow>("Toolkit Settings");
        }

        private void OnEnable()
        {
            settings = LoadOrCreateSettings();
        }

        private void OnGUI()
        {
            if (settings == null)
            {
                EditorGUILayout.HelpBox("Failed to load ToolkitProjectSettings asset.", MessageType.Error);
                if (GUILayout.Button("Retry"))
                {
                    settings = LoadOrCreateSettings();
                }

                return;
            }

            EditorGUILayout.LabelField("Toolkit Project Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            settings.activeGameFlavor = (GameFlavor)EditorGUILayout.EnumPopup("Active Game", settings.activeGameFlavor);
            settings.enableVerboseLogs = EditorGUILayout.Toggle("Enable Verbose Logs", settings.enableVerboseLogs);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Use this setting as the top-level switch for game-specific adapters.", MessageType.Info);
        }

        private static ToolkitProjectSettings LoadOrCreateSettings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ToolkitProjectSettings>(SettingsAssetPath);
            if (asset != null)
            {
                return asset;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            if (!AssetDatabase.IsValidFolder(SettingsDirectory))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Toolkit");
            }

            asset = CreateInstance<ToolkitProjectSettings>();
            AssetDatabase.CreateAsset(asset, SettingsAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return asset;
        }
    }
}