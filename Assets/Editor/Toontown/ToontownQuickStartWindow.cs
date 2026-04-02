using Toolkit.Core;
using UnityEditor;
using UnityEngine;
using System.IO;

namespace Toontown.Editor
{
    public sealed class ToontownQuickStartWindow : EditorWindow
    {
        private const string SettingsDirectory = "Assets/Resources/Toolkit";
        private const string SettingsAssetPath = SettingsDirectory + "/ToolkitProjectSettings.asset";
        private string statusMessage = "Use this window to launch the first Toontown workflow.";

        [MenuItem("Toontown/Quick Start")]
        public static void ShowWindow()
        {
            GetWindow<ToontownQuickStartWindow>("Toontown Quick Start");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Toontown Quick Start", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Goal: get to a working parse/export cycle with bundled sample data in a few clicks.",
                MessageType.Info);

            EditorGUILayout.Space();
            if (GUILayout.Button("1) Switch Active Game Flavor to Toontown"))
            {
                SwitchActiveFlavorToToontown();
            }

            if (GUILayout.Button("2) Open Toontown Importer"))
            {
                ToontownWorldDataImporter.ShowWindow();
                statusMessage = "Opened Toontown importer.";
            }

            if (GUILayout.Button("3) Open Toontown Exporter"))
            {
                ToontownWorldDataExporter.ShowWindow();
                statusMessage = "Opened Toontown exporter.";
            }

            if (GUILayout.Button("4) Open Sample Validator"))
            {
                Validation.ToontownSampleValidationWindow.ShowWindow();
                statusMessage = "Opened Toontown sample validator.";
            }

            if (GUILayout.Button("5) Reveal Bundled Sample File"))
            {
                if (!ToontownToolkitPaths.BundledSampleExists())
                {
                    statusMessage =
                        $"Bundled sample not found at {ToontownToolkitPaths.BundledSampleRelativePath}.";
                }
                else
                {
                    EditorUtility.RevealInFinder(ToontownToolkitPaths.BundledSampleFullPath);
                    statusMessage = "Opened file explorer at bundled sample location.";
                }
            }

            if (GUILayout.Button("Open Quick Start Doc"))
            {
                EditorUtility.OpenWithDefaultApp(Path.Combine(Directory.GetCurrentDirectory(), "docs/TOONTOWN_QUICKSTART.md"));
                statusMessage = "Opened docs/TOONTOWN_QUICKSTART.md";
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        }

        private void SwitchActiveFlavorToToontown()
        {
            ToolkitProjectSettings settings = LoadOrCreateSettings();
            settings.activeGameFlavor = GameFlavor.Toontown;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            statusMessage = "Active game flavor set to Toontown.";
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
