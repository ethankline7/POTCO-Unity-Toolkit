using UnityEditor;
using UnityEngine;
using Toolkit.Core;
using Toolkit.Editor.WorldData;
using Toolkit.Editor.WorldData.Adapters.Toontown;
using Toolkit.Editor.WorldData.Contracts;

namespace Toontown.Editor
{
    public sealed class ToontownWorldDataExporter : EditorWindow
    {
        private string sourcePath;
        private string outputPath;
        private string statusMessage = "Select source and output files.";
        private WorldDataDocument parsedDocument;

        [MenuItem("Toontown/World Data/Exporter")]
        public static void ShowWindow()
        {
            GetWindow<ToontownWorldDataExporter>("Toontown Exporter");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Toontown World Data Exporter", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (WorldDataToolRouteResolver.GetActiveGameFlavor() != GameFlavor.Toontown)
            {
                EditorGUILayout.HelpBox(
                    "Active game flavor is not set to Toontown. Switch in Toolkit/Settings for consistent routing.",
                    MessageType.Warning);
            }

            if (GUILayout.Button("Select Source .py File"))
            {
                string selected = EditorUtility.OpenFilePanel("Select Toontown Source", Application.dataPath, "py");
                if (!string.IsNullOrEmpty(selected))
                {
                    sourcePath = selected;
                    statusMessage = $"Selected source: {System.IO.Path.GetFileName(sourcePath)}";
                }
            }

            if (GUILayout.Button("Select Output .py File"))
            {
                string selected = EditorUtility.SaveFilePanel(
                    "Select Toontown Output",
                    Application.dataPath,
                    "toontown_export.py",
                    "py");

                if (!string.IsNullOrEmpty(selected))
                {
                    outputPath = selected;
                    statusMessage = $"Selected output: {System.IO.Path.GetFileName(outputPath)}";
                }
            }

            EditorGUILayout.LabelField("Source", string.IsNullOrWhiteSpace(sourcePath) ? "<none>" : sourcePath);
            EditorGUILayout.LabelField("Output", string.IsNullOrWhiteSpace(outputPath) ? "<none>" : outputPath);
            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(outputPath));
            if (GUILayout.Button("Parse And Write"))
            {
                ParseAndWrite();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);

            if (parsedDocument != null)
            {
                EditorGUILayout.LabelField("Last Parsed Document", parsedDocument.Name);
                EditorGUILayout.LabelField("Likely Objects", parsedDocument.Objects.Count.ToString());
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Open Migration Plan"))
            {
                Debug.Log("See docs/TOONTOWN_MIGRATION_PLAN.md for implementation phases.");
            }
        }

        private void ParseAndWrite()
        {
            try
            {
                IWorldDataDocumentReader reader = new ToontownWorldDataDocumentReader();
                IWorldDataDocumentWriter writer = new ToontownWorldDataDocumentWriter();

                if (!reader.CanRead(sourcePath))
                {
                    statusMessage = $"Reader '{reader.FormatId}' cannot parse source file.";
                    parsedDocument = null;
                    return;
                }

                if (!writer.CanWrite(outputPath))
                {
                    statusMessage = $"Writer '{writer.FormatId}' cannot write output file.";
                    return;
                }

                parsedDocument = reader.ReadFromFile(sourcePath);
                writer.WriteToFile(parsedDocument, outputPath);

                statusMessage =
                    $"Wrote {parsedDocument.Objects.Count} likely objects to {System.IO.Path.GetFileName(outputPath)}.";
            }
            catch (System.Exception ex)
            {
                statusMessage = $"Parse/write failed: {ex.Message}";
            }
        }
    }
}
