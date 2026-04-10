using System;
using System.Collections.Generic;
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
                    CreatePlaceholderForMissingModel = false,
                    ApplyPreviewLighting = true,
                    RemoveFakeShadowsByDefault = true,
                    RootObjectName = "ToontownDNA_MVP_Demo"
                };

                int forcedEggImports = settings.UseEggFiles
                    ? ForceImportRequiredEggAssets(document)
                    : 0;

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                ToontownSceneImportResult result = ToontownSceneDocumentImporter.ImportDocument(document, settings);

                string outputDirectory = Path.GetDirectoryName(SuggestedOutputScenePath);
                if (!string.IsNullOrWhiteSpace(outputDirectory) && !Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                bool saved = EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), SuggestedOutputScenePath);
                long outputBytes = File.Exists(SuggestedOutputScenePath) ? new FileInfo(SuggestedOutputScenePath).Length : 0L;
                bool hasMissingModels = result.MissingModels > 0;
                List<KeyValuePair<string, int>> warningCategoryMetrics =
                    BuildWarningCategoryMetrics(document, result);

                var report = new StringBuilder();
                report.AppendLine("Toontown DNA MVP Demo Import");
                report.AppendLine(hasMissingModels ? "Status: FAIL" : "Status: PASS");
                report.AppendLine($"Source DNA: {sourcePath}");
                report.AppendLine($"Storage files: {storagePaths.Count}");
                report.AppendLine($"Parsed objects: {document.Objects.Count}");
                report.AppendLine($"Document warnings: {document.Warnings.Count}");
                report.AppendLine($"Created scene objects: {result.CreatedSceneObjects}");
                report.AppendLine($"Instantiated models: {result.InstantiatedModels}");
                report.AppendLine($"Missing models: {result.MissingModels}");
                report.AppendLine($"Placeholders created: {result.PlaceholdersCreated}");
                report.AppendLine($"Fake shadow renderers disabled: {result.FakeShadowRenderersDisabled}");
                report.AppendLine($"Resolved-node isolate success: {result.ResolvedNodeIsolationsSucceeded}");
                report.AppendLine($"Resolved-node isolate failed: {result.ResolvedNodeIsolationsFailed}");
                report.AppendLine(
                    $"Door/window parent anchors: {result.DoorWindowParentAnchorsApplied}/{result.DoorWindowParentAnchorsAttempted}");
                report.AppendLine($"Door/window parent anchor misses: {result.DoorWindowParentAnchorsMissed}");
                report.AppendLine($"Forced EGG imports: {forcedEggImports}");
                report.AppendLine($"Scene saved: {saved}");
                report.AppendLine($"Output scene: {SuggestedOutputScenePath}");
                report.AppendLine($"Output scene size: {outputBytes} bytes");
                report.AppendLine("Warning categories:");
                foreach (KeyValuePair<string, int> metric in warningCategoryMetrics)
                {
                    report.AppendLine($"- {metric.Key}: {metric.Value}");
                }
                if (result.DoorWindowParentAnchorWarnings.Count > 0)
                {
                    report.AppendLine("Door/window anchor warnings:");
                    foreach (string warning in result.DoorWindowParentAnchorWarnings.Take(25))
                    {
                        report.AppendLine($"- {warning}");
                    }
                }
                if (result.FailedResolvedNodeEntries.Count > 0)
                {
                    report.AppendLine("Resolved-node failures:");
                    foreach (string failure in result.FailedResolvedNodeEntries.Take(25))
                    {
                        report.AppendLine($"- {failure}");
                    }
                }
                if (result.MissingModelPaths.Count > 0)
                {
                    report.AppendLine("Missing model keys:");
                    foreach (string missingModel in result.MissingModelPaths.Take(25))
                    {
                        report.AppendLine($"- {missingModel}");
                    }
                }
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
                ExitBatch(hasMissingModels ? 1 : 0, exitOnFinish);
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

        private static int ForceImportRequiredEggAssets(WorldDataDocument document)
        {
            if (document == null)
            {
                return 0;
            }

            var modelPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WorldDataObject obj in document.Objects)
            {
                string modelPath = ToontownSceneDocumentImporter.ResolveModelPathFromProperties(obj.Properties);
                if (!string.IsNullOrWhiteSpace(modelPath))
                {
                    modelPaths.Add(modelPath);
                }
            }

            if (modelPaths.Count == 0)
            {
                return 0;
            }

            string[] phaseRoots = Directory.GetDirectories("Assets/Resources", "phase_*", SearchOption.AllDirectories);
            if (phaseRoots.Length == 0)
            {
                return 0;
            }

            EggImporterSettings eggSettings = EggImporterSettings.Instance;
            bool originalAutoImport = eggSettings.autoImportEnabled;
            EggImporterSettings.PivotMode originalPivotMode = eggSettings.pivotMode;
            eggSettings.autoImportEnabled = true;
            eggSettings.pivotMode = EggImporterSettings.PivotMode.Original;
            EditorUtility.SetDirty(eggSettings);
            AssetDatabase.SaveAssets();

            int importCount = 0;
            try
            {
                foreach (string modelPath in modelPaths)
                {
                    foreach (string phaseRoot in phaseRoots)
                    {
                        string candidatePath = Path.Combine(phaseRoot, modelPath + ".egg").Replace('\\', '/');
                        if (!File.Exists(candidatePath))
                        {
                            continue;
                        }

                        AssetDatabase.ImportAsset(
                            candidatePath,
                            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                        importCount++;
                        break;
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                eggSettings.autoImportEnabled = originalAutoImport;
                eggSettings.pivotMode = originalPivotMode;
                EditorUtility.SetDirty(eggSettings);
                AssetDatabase.SaveAssets();
            }

            return importCount;
        }

        private static List<KeyValuePair<string, int>> BuildWarningCategoryMetrics(
            WorldDataDocument document,
            ToontownSceneImportResult result)
        {
            int missingModel = result?.MissingModels ?? 0;
            int missingResolvedNode = result?.ResolvedNodeIsolationsFailed ?? 0;
            int fallbackPlacement = result?.DoorWindowParentAnchorsMissed ?? 0;
            int materialFallback = 0;
            int fakeShadowRemoval = result?.FakeShadowRenderersDisabled ?? 0;
            int uncategorizedDocumentWarnings = 0;

            if (document?.Warnings != null)
            {
                foreach (string warning in document.Warnings)
                {
                    if (WarningContainsAny(warning, "missing model", "model not found", "could not resolve model"))
                    {
                        missingModel++;
                    }
                    else if (WarningContainsAny(warning, "missing resolved node", "resolved-node", "resolved node"))
                    {
                        missingResolvedNode++;
                    }
                    else if (WarningContainsAny(warning, "fallback placement", "parent anchor", "anchor miss"))
                    {
                        fallbackPlacement++;
                    }
                    else if (WarningContainsAny(warning, "material fallback", "fallback material", "texture fallback"))
                    {
                        materialFallback++;
                    }
                    else if (WarningContainsAny(warning, "fake shadow", "shadow renderer"))
                    {
                        fakeShadowRemoval++;
                    }
                    else
                    {
                        uncategorizedDocumentWarnings++;
                    }
                }
            }

            return new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("missing model", missingModel),
                new KeyValuePair<string, int>("missing resolved node", missingResolvedNode),
                new KeyValuePair<string, int>("fallback placement", fallbackPlacement),
                new KeyValuePair<string, int>("material fallback", materialFallback),
                new KeyValuePair<string, int>("fake shadow removal", fakeShadowRemoval),
                new KeyValuePair<string, int>("uncategorized document warning", uncategorizedDocumentWarnings)
            };
        }

        private static bool WarningContainsAny(string warning, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(warning) || tokens == null)
            {
                return false;
            }

            foreach (string token in tokens)
            {
                if (!string.IsNullOrWhiteSpace(token) &&
                    warning.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
