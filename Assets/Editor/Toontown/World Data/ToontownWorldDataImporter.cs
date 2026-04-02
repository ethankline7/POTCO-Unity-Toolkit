using System.Linq;
using UnityEditor;
using UnityEngine;
using Toolkit.Core;
using Toolkit.Editor.WorldData;
using Toolkit.Editor.WorldData.Adapters.Toontown;
using Toolkit.Editor.WorldData.Contracts;

namespace Toontown.Editor
{
    public sealed class ToontownWorldDataImporter : EditorWindow
    {
        private string sourcePath;
        private string statusMessage = "No file selected.";
        private Vector2 scroll;
        private WorldDataDocument parsedDocument;

        [MenuItem("Toontown/World Data/Importer")]
        public static void ShowWindow()
        {
            GetWindow<ToontownWorldDataImporter>("Toontown Importer");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Toontown World Data Importer", EditorStyles.boldLabel);
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
                    statusMessage = $"Selected: {System.IO.Path.GetFileName(sourcePath)}";
                }
            }

            EditorGUILayout.LabelField("File", string.IsNullOrEmpty(sourcePath) ? "<none>" : sourcePath);
            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(sourcePath));
            if (GUILayout.Button("Parse Preview"))
            {
                ParseSelectedFile();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);

            if (parsedDocument != null)
            {
                DrawParsedSummary(parsedDocument);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Open Migration Plan"))
            {
                Debug.Log("See docs/TOONTOWN_MIGRATION_PLAN.md for implementation phases.");
            }
        }

        private void ParseSelectedFile()
        {
            try
            {
                // Prefer the active adapter when set to Toontown.
                // Fallback to explicit Toontown reader so this window is still usable if flavor is not switched yet.
                var reader = WorldDataToolRouteResolver.GetActiveGameFlavor() == GameFlavor.Toontown
                    ? WorldDataFormatAdapterRegistry.GetActiveAdapter().Reader
                    : new ToontownWorldDataDocumentReader();

                if (!reader.CanRead(sourcePath))
                {
                    statusMessage = $"Reader '{reader.FormatId}' cannot parse this file type.";
                    parsedDocument = null;
                    return;
                }

                parsedDocument = reader.ReadFromFile(sourcePath);
                statusMessage = $"Parsed {parsedDocument.Objects.Count} likely world objects via {reader.FormatId}.";
            }
            catch (System.Exception ex)
            {
                parsedDocument = null;
                statusMessage = $"Parse failed: {ex.Message}";
            }
        }

        private void DrawParsedSummary(WorldDataDocument document)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Parsed Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Document", document.Name);
            EditorGUILayout.LabelField("Likely Objects", document.Objects.Count.ToString());
            EditorGUILayout.LabelField("Warnings", document.Warnings.Count.ToString());

            int withModel = 0;
            int withType = 0;
            foreach (var obj in document.Objects)
            {
                if (obj.Properties.ContainsKey("Model")) withModel++;
                if (obj.Properties.ContainsKey("Type")) withType++;
            }

            EditorGUILayout.LabelField("Objects with Model", withModel.ToString());
            EditorGUILayout.LabelField("Objects with Type", withType.ToString());
            EditorGUILayout.Space();

            var typeCounts = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var obj in document.Objects)
            {
                string t = obj.Properties.ContainsKey("Type") ? obj.Properties["Type"] : "<none>";
                if (!typeCounts.ContainsKey(t)) typeCounts[t] = 0;
                typeCounts[t]++;
            }

            EditorGUILayout.LabelField("Top Types", EditorStyles.boldLabel);
            int shown = 0;
            foreach (var kvp in typeCounts.OrderByDescending(x => x.Value))
            {
                EditorGUILayout.LabelField($"- {kvp.Key}: {kvp.Value}");
                shown++;
                if (shown >= 8) break;
            }
            EditorGUILayout.Space();

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(180));
            int previewCount = Mathf.Min(document.Objects.Count, 25);
            for (int i = 0; i < previewCount; i++)
            {
                WorldDataObject obj = document.Objects[i];
                string type = obj.Properties.ContainsKey("Type") ? obj.Properties["Type"] : "<none>";
                EditorGUILayout.LabelField($"[{i + 1}] {obj.Id} (Type: {type})");
            }
            EditorGUILayout.EndScrollView();

            if (document.Warnings.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Warnings", EditorStyles.boldLabel);
                for (int i = 0; i < document.Warnings.Count; i++)
                {
                    EditorGUILayout.HelpBox(document.Warnings[i], MessageType.Warning);
                }
            }

            EditorGUILayout.EndVertical();
        }
    }
}
