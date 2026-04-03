using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Toolkit.Core;
using Toolkit.Editor.WorldData;
using Toolkit.Editor.WorldData.Adapters.Toontown;
using Toolkit.Editor.WorldData.Contracts;
using UnityEditor;
using UnityEngine;

namespace Toontown.Editor
{
    public sealed class ToontownDnaSceneImporterWindow : EditorWindow
    {
        private const int MaxPreviewObjects = 30;

        private string sourceDnaPath = string.Empty;
        private string storageRootPath = string.Empty;
        private string statusMessage = "Select a .dna file to begin.";
        private Vector2 scroll;

        private bool autoDiscoverStorageFromSource = true;
        private bool searchStorageRecursively = false;
        private bool includeSuggestedStorageSet = true;
        private bool useEggFiles = true;
        private bool addObjectListInfo = true;
        private bool createPlaceholders = false;
        private bool applyPreviewLighting = true;
        private string customRootName = string.Empty;

        private WorldDataDocument parsedDocument;
        private ToontownSceneImportResult lastImportResult;
        private List<string> resolvedStoragePaths = new List<string>();

        [MenuItem("Toontown/World Data/DNA Scene Importer (MVP)")]
        public static void ShowWindow()
        {
            GetWindow<ToontownDnaSceneImporterWindow>("Toontown DNA Importer");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Toontown DNA -> Scene Importer (MVP)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Imports Toontown .dna hierarchy into a Unity root object and attempts model instantiation from Resources/phase_* paths.",
                MessageType.Info);

            if (WorldDataToolRouteResolver.GetActiveGameFlavor() != GameFlavor.Toontown)
            {
                EditorGUILayout.HelpBox(
                    "Active game flavor is not set to Toontown. Switch in Toolkit/Settings for consistent routing.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();
            DrawSourceSelection();
            EditorGUILayout.Space();
            DrawStorageSelection();
            EditorGUILayout.Space();
            DrawImportSettings();
            EditorGUILayout.Space();
            DrawActionButtons();
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(statusMessage, MessageType.None);
            DrawResultSummaries();
        }

        private void DrawSourceSelection()
        {
            EditorGUILayout.LabelField("Source DNA", EditorStyles.boldLabel);

            if (GUILayout.Button("Select Source .dna File"))
            {
                string selected = EditorUtility.OpenFilePanel("Select Toontown DNA File", Application.dataPath, "dna");
                if (!string.IsNullOrWhiteSpace(selected))
                {
                    sourceDnaPath = selected;
                    parsedDocument = null;
                    lastImportResult = null;
                    statusMessage = $"Selected source: {Path.GetFileName(sourceDnaPath)}";
                }
            }

            if (GUILayout.Button("Use Suggested OpenToontown Sample"))
            {
                if (!ToontownToolkitPaths.SuggestedDnaSampleExists())
                {
                    statusMessage =
                        $"Suggested DNA sample not found at {ToontownToolkitPaths.SuggestedDnaSampleRelativePath}.";
                }
                else
                {
                    sourceDnaPath = ToontownToolkitPaths.SuggestedDnaSampleFullPath;
                    parsedDocument = null;
                    lastImportResult = null;
                    statusMessage = $"Selected suggested source: {Path.GetFileName(sourceDnaPath)}";
                }
            }

            EditorGUILayout.LabelField("File", string.IsNullOrWhiteSpace(sourceDnaPath) ? "<none>" : sourceDnaPath);
        }

        private void DrawStorageSelection()
        {
            EditorGUILayout.LabelField("Storage Mapping", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Storage files (storage*.dna) map code keys to model paths. Include them for better model resolution.",
                MessageType.None);

            autoDiscoverStorageFromSource = EditorGUILayout.ToggleLeft(
                "Auto-discover storage files near source",
                autoDiscoverStorageFromSource);

            searchStorageRecursively = EditorGUILayout.ToggleLeft(
                "Recursive search when storage root is set",
                searchStorageRecursively);

            includeSuggestedStorageSet = EditorGUILayout.ToggleLeft(
                "Include suggested OpenToontown storage files",
                includeSuggestedStorageSet);

            if (GUILayout.Button("Select Storage Root Folder (Optional)"))
            {
                string selected = EditorUtility.OpenFolderPanel("Select Storage Root", Application.dataPath, string.Empty);
                if (!string.IsNullOrWhiteSpace(selected))
                {
                    storageRootPath = selected;
                    parsedDocument = null;
                    lastImportResult = null;
                    statusMessage = $"Selected storage root: {storageRootPath}";
                }
            }

            if (GUILayout.Button("Clear Storage Root"))
            {
                storageRootPath = string.Empty;
                parsedDocument = null;
                lastImportResult = null;
                statusMessage = "Cleared storage root.";
            }

            EditorGUILayout.LabelField("Storage Root", string.IsNullOrWhiteSpace(storageRootPath) ? "<none>" : storageRootPath);
        }

        private void DrawImportSettings()
        {
            EditorGUILayout.LabelField("Import Settings", EditorStyles.boldLabel);
            useEggFiles = EditorGUILayout.ToggleLeft("Use .egg assets (if available)", useEggFiles);
            addObjectListInfo = EditorGUILayout.ToggleLeft("Attach ObjectListInfo components", addObjectListInfo);
            createPlaceholders = EditorGUILayout.ToggleLeft("Create placeholders for missing models", createPlaceholders);
            applyPreviewLighting = EditorGUILayout.ToggleLeft("Apply Toontown preview lighting", applyPreviewLighting);
            customRootName = EditorGUILayout.TextField("Root Object Name (Optional)", customRootName);
        }

        private void DrawActionButtons()
        {
            EditorGUI.BeginDisabledGroup(string.IsNullOrWhiteSpace(sourceDnaPath));
            if (GUILayout.Button("Parse DNA Preview"))
            {
                ParseSelectedDna();
            }

            if (GUILayout.Button("Import Parsed DNA Into Scene"))
            {
                ImportIntoScene();
            }
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Open Toontown Quick Start"))
            {
                ToontownQuickStartWindow.ShowWindow();
            }
        }

        private void ParseSelectedDna()
        {
            try
            {
                var reader = new ToontownDnaDocumentReader();
                resolvedStoragePaths = ResolveStoragePaths();
                parsedDocument = reader.ReadFromFileWithStorage(sourceDnaPath, resolvedStoragePaths);
                lastImportResult = null;

                statusMessage =
                    $"Parsed {parsedDocument.Objects.Count} objects from {Path.GetFileName(sourceDnaPath)} " +
                    $"with {resolvedStoragePaths.Count} storage mapping file(s).";
            }
            catch (Exception ex)
            {
                parsedDocument = null;
                lastImportResult = null;
                statusMessage = $"Parse failed: {ex.Message}";
            }
        }

        private void ImportIntoScene()
        {
            if (parsedDocument == null)
            {
                ParseSelectedDna();
                if (parsedDocument == null)
                {
                    return;
                }
            }

            var settings = new ToontownSceneImportSettings
            {
                UseEggFiles = useEggFiles,
                AddObjectListInfo = addObjectListInfo,
                CreatePlaceholderForMissingModel = createPlaceholders,
                ApplyPreviewLighting = applyPreviewLighting,
                RootObjectName = customRootName
            };

            try
            {
                lastImportResult = ToontownSceneDocumentImporter.ImportDocument(parsedDocument, settings);
                statusMessage =
                    $"Imported '{lastImportResult.RootObjectName}' with {lastImportResult.CreatedSceneObjects} objects. " +
                    $"Models: {lastImportResult.InstantiatedModels} resolved, {lastImportResult.MissingModels} missing.";
            }
            catch (Exception ex)
            {
                lastImportResult = null;
                statusMessage = $"Scene import failed: {ex.Message}";
            }
        }

        private List<string> ResolveStoragePaths()
        {
            var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (autoDiscoverStorageFromSource && !string.IsNullOrWhiteSpace(sourceDnaPath))
            {
                AddStorageNearSource(sourceDnaPath, resolved);
            }

            if (!string.IsNullOrWhiteSpace(storageRootPath))
            {
                AddStorageFromRoot(storageRootPath, searchStorageRecursively, resolved);
            }

            if (includeSuggestedStorageSet)
            {
                foreach (string suggested in ToontownToolkitPaths.GetSuggestedDnaStorageFullPaths())
                {
                    if (File.Exists(suggested))
                    {
                        resolved.Add(NormalizePath(suggested));
                    }
                }
            }

            return resolved.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void AddStorageNearSource(string sourcePath, HashSet<string> output)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return;
            }

            string startDirectory = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(startDirectory) || !Directory.Exists(startDirectory))
            {
                return;
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cursor = new DirectoryInfo(startDirectory);
            int depth = 0;

            while (cursor != null && depth < 4)
            {
                string cursorPath = NormalizePath(cursor.FullName);
                if (visited.Add(cursorPath))
                {
                    AddStorageFromRoot(cursorPath, false, output);
                    string dnaSubfolder = Path.Combine(cursorPath, "dna");
                    AddStorageFromRoot(dnaSubfolder, false, output);
                }

                cursor = cursor.Parent;
                depth++;
            }
        }

        private static void AddStorageFromRoot(string rootPath, bool recursive, HashSet<string> output)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return;
            }

            SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string[] files;
            try
            {
                files = Directory.GetFiles(rootPath, "storage*.dna", searchOption);
            }
            catch
            {
                return;
            }

            foreach (string file in files)
            {
                output.Add(NormalizePath(file));
            }
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private void DrawResultSummaries()
        {
            if (resolvedStoragePaths.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Resolved Storage Files ({resolvedStoragePaths.Count})", EditorStyles.boldLabel);
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(110));
                foreach (string path in resolvedStoragePaths.Take(20))
                {
                    EditorGUILayout.LabelField($"- {path}");
                }

                if (resolvedStoragePaths.Count > 20)
                {
                    EditorGUILayout.LabelField($"...and {resolvedStoragePaths.Count - 20} more");
                }
                EditorGUILayout.EndScrollView();
            }

            if (parsedDocument == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Parsed Document Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Document", parsedDocument.Name);
            EditorGUILayout.LabelField("Objects", parsedDocument.Objects.Count.ToString());
            EditorGUILayout.LabelField("Warnings", parsedDocument.Warnings.Count.ToString());

            int withResolvedModel = parsedDocument.Objects.Count(
                obj => obj.Properties.ContainsKey("ResolvedModel") || obj.Properties.ContainsKey("Model"));
            EditorGUILayout.LabelField("Objects with model hint", withResolvedModel.ToString());

            var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (WorldDataObject obj in parsedDocument.Objects)
            {
                string type = obj.Properties.ContainsKey("Type") ? obj.Properties["Type"] : "<none>";
                if (!typeCounts.ContainsKey(type))
                {
                    typeCounts[type] = 0;
                }

                typeCounts[type]++;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Top Types", EditorStyles.boldLabel);
            foreach (var kvp in typeCounts.OrderByDescending(x => x.Value).Take(8))
            {
                EditorGUILayout.LabelField($"- {kvp.Key}: {kvp.Value}");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview Objects", EditorStyles.boldLabel);
            int previewCount = Mathf.Min(parsedDocument.Objects.Count, MaxPreviewObjects);
            for (int i = 0; i < previewCount; i++)
            {
                WorldDataObject obj = parsedDocument.Objects[i];
                string type = obj.Properties.ContainsKey("Type") ? obj.Properties["Type"] : "<none>";
                EditorGUILayout.LabelField($"[{i + 1}] {obj.Id} ({type})");
            }

            if (parsedDocument.Warnings.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Warnings", EditorStyles.boldLabel);
                foreach (string warning in parsedDocument.Warnings.Take(8))
                {
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }

                if (parsedDocument.Warnings.Count > 8)
                {
                    EditorGUILayout.HelpBox($"...and {parsedDocument.Warnings.Count - 8} more warning(s).", MessageType.Warning);
                }
            }
            EditorGUILayout.EndVertical();

            if (lastImportResult != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Last Scene Import", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Root", lastImportResult.RootObjectName);
                EditorGUILayout.LabelField("Created Objects", lastImportResult.CreatedSceneObjects.ToString());
                EditorGUILayout.LabelField("Instantiated Models", lastImportResult.InstantiatedModels.ToString());
                EditorGUILayout.LabelField("Missing Models", lastImportResult.MissingModels.ToString());
                EditorGUILayout.LabelField("Placeholders", lastImportResult.PlaceholdersCreated.ToString());
                EditorGUILayout.EndVertical();
            }
        }
    }
}
