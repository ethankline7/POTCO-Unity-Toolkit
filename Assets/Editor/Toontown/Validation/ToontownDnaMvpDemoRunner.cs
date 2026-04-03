using System;
using System.IO;
using System.Linq;
using System.Text;
using Toolkit.Editor.WorldData.Adapters.Toontown;
using Toolkit.Editor.WorldData.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Toontown.Editor.Validation
{
    public static class ToontownDnaMvpDemoRunner
    {
        private const string SuggestedOutputScenePath = "Assets/Editor/Toontown/Samples/Generated/toontown_dna_mvp_demo.unity";

        [MenuItem("Toontown/Validation/Run DNA MVP Demo Import")]
        public static void Run()
        {
            RunInternal(exitOnFinish: false);
        }

        // Used by batch mode: -executeMethod Toontown.Editor.Validation.ToontownDnaMvpDemoRunner.RunBatch
        public static void RunBatch()
        {
            RunInternal(exitOnFinish: true);
        }

        private static void RunInternal(bool exitOnFinish)
        {
            try
            {
                string sourcePath = ToontownToolkitPaths.SuggestedDnaSampleFullPath;
                if (!File.Exists(sourcePath))
                {
                    string missing = $"Suggested DNA sample not found at {ToontownToolkitPaths.SuggestedDnaSampleRelativePath}";
                    Debug.LogError(missing);
                    ShowDialogIfInteractive("DNA MVP Demo", missing);
                    ExitBatch(1, exitOnFinish);
                    return;
                }

                var storagePaths = ToontownToolkitPaths.GetSuggestedDnaStorageFullPaths().ToList();
                if (storagePaths.Count == 0)
                {
                    string missingStorage = "No suggested storage*.dna files found. DNA model resolution will be limited.";
                    Debug.LogWarning(missingStorage);
                }

                var reader = new ToontownDnaDocumentReader();
                WorldDataDocument document = reader.ReadFromFileWithStorage(sourcePath, storagePaths);

                var settings = new ToontownSceneImportSettings
                {
                    UseEggFiles = true,
                    AddObjectListInfo = true,
                    CreatePlaceholderForMissingModel = true,
                    RootObjectName = "ToontownDNA_MVP_Demo"
                };

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                ToontownSceneImportResult result = ToontownSceneDocumentImporter.ImportDocument(document, settings);

                string outputDirectory = Path.GetDirectoryName(SuggestedOutputScenePath);
                if (!string.IsNullOrWhiteSpace(outputDirectory) && !Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                bool saved = EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), SuggestedOutputScenePath);
                long outputBytes = File.Exists(SuggestedOutputScenePath) ? new FileInfo(SuggestedOutputScenePath).Length : 0L;

                var report = new StringBuilder();
                report.AppendLine("Toontown DNA MVP Demo Import");
                report.AppendLine("Status: PASS");
                report.AppendLine($"Source DNA: {sourcePath}");
                report.AppendLine($"Storage files: {storagePaths.Count}");
                report.AppendLine($"Parsed objects: {document.Objects.Count}");
                report.AppendLine($"Document warnings: {document.Warnings.Count}");
                report.AppendLine($"Created scene objects: {result.CreatedSceneObjects}");
                report.AppendLine($"Instantiated models: {result.InstantiatedModels}");
                report.AppendLine($"Missing models: {result.MissingModels}");
                report.AppendLine($"Placeholders created: {result.PlaceholdersCreated}");
                report.AppendLine($"Scene saved: {saved}");
                report.AppendLine($"Output scene: {SuggestedOutputScenePath}");
                report.AppendLine($"Output scene size: {outputBytes} bytes");
                if (document.Warnings.Count > 0)
                {
                    report.AppendLine("Top warnings:");
                    foreach (string warning in document.Warnings.Take(5))
                    {
                        report.AppendLine($"- {warning}");
                    }
                }

                Debug.Log(report.ToString());
                ShowDialogIfInteractive(
                    "DNA MVP Demo",
                    $"Import finished. Parsed {document.Objects.Count} objects, created {result.CreatedSceneObjects} scene objects.");
                ExitBatch(0, exitOnFinish);
            }
            catch (Exception ex)
            {
                Debug.LogError($"DNA MVP demo import failed: {ex}");
                ShowDialogIfInteractive("DNA MVP Demo", $"Import failed: {ex.Message}");
                ExitBatch(1, exitOnFinish);
            }
        }

        private static void ShowDialogIfInteractive(string title, string message)
        {
            if (Application.isBatchMode)
            {
                return;
            }

            EditorUtility.DisplayDialog(title, message, "OK");
        }

        private static void ExitBatch(int exitCode, bool exitOnFinish)
        {
            if (!Application.isBatchMode || !exitOnFinish)
            {
                return;
            }

            EditorApplication.Exit(exitCode);
        }
    }
}
