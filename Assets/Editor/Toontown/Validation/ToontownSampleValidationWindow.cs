using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Toolkit.Editor.WorldData.Adapters.Toontown;
using Toolkit.Editor.WorldData.Contracts;
using UnityEditor;
using UnityEngine;

namespace Toontown.Editor.Validation
{
    public sealed class ToontownSampleValidationWindow : EditorWindow
    {
        private const int MaxFolderFiles = 200;

        private string selectedFilePath;
        private string selectedFolderPath;
        private string statusMessage = "Select a file or folder to validate.";
        private Vector2 scroll;
        private readonly List<ValidationResult> results = new List<ValidationResult>();

        [MenuItem("Toontown/Validation/Sample Validator")]
        public static void ShowWindow()
        {
            GetWindow<ToontownSampleValidationWindow>("Toontown Validator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Toontown Sample Validator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawSelection();
            EditorGUILayout.Space();
            DrawActions();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);

            if (results.Count > 0)
            {
                EditorGUILayout.Space();
                DrawSummary();
                DrawResults();
            }
        }

        private void DrawSelection()
        {
            if (GUILayout.Button("Select Sample .py File"))
            {
                string selected = EditorUtility.OpenFilePanel("Select Toontown Sample", Application.dataPath, "py");
                if (!string.IsNullOrWhiteSpace(selected))
                {
                    selectedFilePath = selected;
                    statusMessage = $"Selected sample file: {Path.GetFileName(selectedFilePath)}";
                }
            }

            EditorGUILayout.LabelField("Sample File", string.IsNullOrWhiteSpace(selectedFilePath) ? "<none>" : selectedFilePath);

            if (GUILayout.Button("Select Folder (.py Batch)"))
            {
                string selected = EditorUtility.OpenFolderPanel("Select Sample Folder", Application.dataPath, string.Empty);
                if (!string.IsNullOrWhiteSpace(selected))
                {
                    selectedFolderPath = selected;
                    statusMessage = $"Selected folder: {selectedFolderPath}";
                }
            }

            EditorGUILayout.LabelField("Sample Folder", string.IsNullOrWhiteSpace(selectedFolderPath) ? "<none>" : selectedFolderPath);
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(selectedFilePath));
            if (GUILayout.Button("Validate Selected File"))
            {
                ValidateSingleFile(selectedFilePath);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(selectedFolderPath));
            if (GUILayout.Button("Validate Folder"))
            {
                ValidateFolder(selectedFolderPath);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Open Type Map Config"))
            {
                EditorUtility.OpenWithDefaultApp(ToontownObjectTypeMapper.ConfigFullPath);
            }

            EditorGUI.BeginDisabledGroup(results.Count == 0);
            if (GUILayout.Button("Export CSV Report"))
            {
                ExportCsvReport();
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Clear Results"))
            {
                results.Clear();
                statusMessage = "Validation results cleared.";
            }
        }

        private void ValidateSingleFile(string path)
        {
            results.Clear();
            var result = ValidateFile(path);
            results.Add(result);
            statusMessage = $"Validated 1 file: {result.FileName} ({result.Quality}).";
        }

        private void ValidateFolder(string folderPath)
        {
            results.Clear();
            if (!Directory.Exists(folderPath))
            {
                statusMessage = "Selected folder does not exist.";
                return;
            }

            string[] files = Directory.GetFiles(folderPath, "*.py", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            if (files.Length == 0)
            {
                statusMessage = "No .py files found in selected folder.";
                return;
            }

            int takeCount = Mathf.Min(files.Length, MaxFolderFiles);
            for (int i = 0; i < takeCount; i++)
            {
                results.Add(ValidateFile(files[i]));
            }

            if (files.Length > MaxFolderFiles)
            {
                statusMessage =
                    $"Validated {takeCount}/{files.Length} files (cap {MaxFolderFiles}). Refine folder scope for full coverage.";
            }
            else
            {
                statusMessage = $"Validated {results.Count} files.";
            }
        }

        private static ValidationResult ValidateFile(string path)
        {
            var reader = new ToontownWorldDataDocumentReader();
            var result = new ValidationResult
            {
                FilePath = path,
                FileName = Path.GetFileName(path)
            };

            try
            {
                WorldDataDocument doc = reader.ReadFromFile(path);
                result.ObjectCount = doc.Objects.Count;
                result.WarningCount = doc.Warnings.Count;

                int withModel = 0;
                int withType = 0;
                int unknownType = 0;

                foreach (var obj in doc.Objects)
                {
                    if (obj.Properties.ContainsKey("Model")) withModel++;
                    if (obj.Properties.ContainsKey("Type"))
                    {
                        withType++;
                        string t = obj.Properties["Type"];
                        if (string.Equals(t, "Unknown", StringComparison.OrdinalIgnoreCase))
                        {
                            unknownType++;
                        }
                    }
                }

                result.ObjectsWithModel = withModel;
                result.ObjectsWithType = withType;
                result.UnknownTypeCount = unknownType;

                int duplicateGroups = doc.Objects
                    .GroupBy(o => o.Id, StringComparer.OrdinalIgnoreCase)
                    .Count(g => g.Count() > 1);
                result.DuplicateIdGroups = duplicateGroups;

                result.UnknownTypeRatio = result.ObjectCount > 0
                    ? (float)result.UnknownTypeCount / result.ObjectCount
                    : 1f;

                result.Quality = ComputeQuality(result);
            }
            catch (Exception ex)
            {
                result.Quality = "FAIL";
                result.ParseError = ex.Message;
            }

            return result;
        }

        private static string ComputeQuality(ValidationResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.ParseError))
            {
                return "FAIL";
            }

            if (result.ObjectCount == 0)
            {
                return "FAIL";
            }

            if (result.DuplicateIdGroups > 0)
            {
                return "FAIL";
            }

            if (result.UnknownTypeRatio > 0.50f)
            {
                return "FAIL";
            }

            if (result.UnknownTypeRatio > 0.20f || result.WarningCount > 10)
            {
                return "WARN";
            }

            return "PASS";
        }

        private void DrawSummary()
        {
            int totalFiles = results.Count;
            int pass = results.Count(r => r.Quality == "PASS");
            int warn = results.Count(r => r.Quality == "WARN");
            int fail = results.Count(r => r.Quality == "FAIL");

            float avgUnknownRatio = totalFiles > 0 ? results.Average(r => r.UnknownTypeRatio) : 0f;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Validation Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Files", totalFiles.ToString());
            EditorGUILayout.LabelField("PASS", pass.ToString());
            EditorGUILayout.LabelField("WARN", warn.ToString());
            EditorGUILayout.LabelField("FAIL", fail.ToString());
            EditorGUILayout.LabelField("Average Unknown Type Ratio", avgUnknownRatio.ToString("P1"));
            EditorGUILayout.EndVertical();
        }

        private void DrawResults()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Per-File Results", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(300));

            foreach (var r in results)
            {
                string headline =
                    $"{r.Quality} | {r.FileName} | objs={r.ObjectCount}, unknown={r.UnknownTypeRatio:P1}, dupGroups={r.DuplicateIdGroups}, warnings={r.WarningCount}";
                EditorGUILayout.LabelField(headline);

                if (!string.IsNullOrWhiteSpace(r.ParseError))
                {
                    EditorGUILayout.HelpBox($"Parse error: {r.ParseError}", MessageType.Error);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void ExportCsvReport()
        {
            string path = EditorUtility.SaveFilePanel(
                "Export Validation Report",
                Application.dataPath,
                "toontown_validation_report.csv",
                "csv");

            if (string.IsNullOrWhiteSpace(path))
            {
                statusMessage = "CSV export cancelled.";
                return;
            }

            try
            {
                using (var writer = new StreamWriter(path, false))
                {
                    writer.WriteLine(
                        "FileName,Quality,ObjectCount,UnknownTypeRatio,UnknownTypeCount,DuplicateIdGroups,WarningCount,ObjectsWithModel,ObjectsWithType,ParseError");

                    foreach (var r in results)
                    {
                        writer.WriteLine(
                            $"{Csv(r.FileName)}," +
                            $"{Csv(r.Quality)}," +
                            $"{r.ObjectCount}," +
                            $"{r.UnknownTypeRatio:0.####}," +
                            $"{r.UnknownTypeCount}," +
                            $"{r.DuplicateIdGroups}," +
                            $"{r.WarningCount}," +
                            $"{r.ObjectsWithModel}," +
                            $"{r.ObjectsWithType}," +
                            $"{Csv(r.ParseError)}");
                    }
                }

                statusMessage = $"Exported CSV report: {path}";
            }
            catch (Exception ex)
            {
                statusMessage = $"CSV export failed: {ex.Message}";
            }
        }

        private static string Csv(string input)
        {
            if (input == null)
            {
                return "\"\"";
            }

            string escaped = input.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        private sealed class ValidationResult
        {
            public string FilePath;
            public string FileName;
            public int ObjectCount;
            public int ObjectsWithModel;
            public int ObjectsWithType;
            public int UnknownTypeCount;
            public float UnknownTypeRatio;
            public int DuplicateIdGroups;
            public int WarningCount;
            public string Quality;
            public string ParseError;
        }
    }
}
