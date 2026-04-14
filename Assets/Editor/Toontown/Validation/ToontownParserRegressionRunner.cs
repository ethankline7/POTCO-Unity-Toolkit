using System.Collections.Generic;
using System.Linq;
using System.Text;
using Toolkit.Editor.WorldData.Adapters.Toontown;
using Toolkit.Editor.WorldData.Contracts;
using UnityEditor;
using UnityEngine;

namespace Toontown.Editor.Validation
{
    public static class ToontownParserRegressionRunner
    {
        [MenuItem("Toontown/Validation/Run Parser Regression Tests")]
        public static void RunFromMenu()
        {
            bool passed = Run(out string report);
            if (passed)
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogError(report);
            }

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Toontown Parser Regression",
                    passed ? "All parser regression checks passed." : "Parser regression checks failed. See Console.",
                    "OK");
            }
        }

        // Used by batch mode: -executeMethod Toontown.Editor.Validation.ToontownParserRegressionRunner.RunBatch
        public static void RunBatch()
        {
            bool passed = Run(out string report);
            if (passed)
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogError(report);
            }

            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            var checks = new List<RegressionCheckResult>();
            var reader = new ToontownWorldDataDocumentReader();

            checks.Add(RunDictionaryFixtureCheck(reader));
            checks.Add(RunAssignmentFixtureCheck(reader));
            checks.Add(RunDnaStorageFixtureCheck());
            checks.Add(RunDnaStyleCodeFixtureCheck());
            checks.Add(RunResolvedNodeStrictFixtureCheck());
            checks.Add(RunResolvedNodeFuzzyFixtureCheck());
            checks.Add(RunResolvedNodeSpellingAliasFixtureCheck());
            checks.Add(RunResolvedNodeModuleAliasFixtureCheck());
            checks.Add(RunResolvedNodeParentAnchorAliasFixtureCheck());
            checks.Add(RunZeroCountWindowGroupFixtureCheck());
            checks.Add(RunSingleCountWindowGroupFixtureCheck());
            checks.Add(RunSingleCountWindowOffsetFixtureCheck());
            checks.Add(RunNarrowWallWindowCountFixtureCheck());
            checks.Add(RunMultiCountWindowOffsetFixtureCheck());
            checks.Add(RunHighCountWindowOffsetFixtureCheck());
            checks.Add(RunLandmarkSignLabelFixtureCheck());
            checks.Add(RunEggMaterialScopeFixtureCheck());

            bool passed = checks.All(c => c.Passed);
            var sb = new StringBuilder();
            sb.AppendLine("Toontown Parser Regression");
            sb.AppendLine($"Status: {(passed ? "PASS" : "FAIL")}");

            foreach (var check in checks)
            {
                sb.AppendLine($"- [{(check.Passed ? "PASS" : "FAIL")}] {check.Name}: {check.Message}");
            }

            report = sb.ToString();
            return passed;
        }

        private static RegressionCheckResult RunDictionaryFixtureCheck(ToontownWorldDataDocumentReader reader)
        {
            if (!ToontownToolkitPaths.BundledSampleExists())
            {
                return RegressionCheckResult.Fail(
                    "Bundled dictionary fixture",
                    $"Missing fixture: {ToontownToolkitPaths.BundledSampleRelativePath}");
            }

            try
            {
                WorldDataDocument doc = reader.ReadFromFile(ToontownToolkitPaths.BundledSampleFullPath);
                if (doc.Objects.Count != 3)
                {
                    return RegressionCheckResult.Fail(
                        "Bundled dictionary fixture",
                        $"Expected 3 objects, got {doc.Objects.Count}.");
                }

                bool hasRoot = doc.Objects.Any(o => o.Id == "sample-zone-root");
                bool hasMailbox = doc.Objects.Any(o => o.Id == "sample-prop-01");
                bool hasBench = doc.Objects.Any(o => o.Id == "sample-prop-02");

                if (!hasRoot || !hasMailbox || !hasBench)
                {
                    return RegressionCheckResult.Fail(
                        "Bundled dictionary fixture",
                        "Missing one or more expected object ids (sample-zone-root, sample-prop-01, sample-prop-02).");
                }

                return RegressionCheckResult.Pass(
                    "Bundled dictionary fixture",
                    $"Parsed expected 3 objects (warnings={doc.Warnings.Count}).");
            }
            catch (System.Exception ex)
            {
                return RegressionCheckResult.Fail(
                    "Bundled dictionary fixture",
                    $"Exception while parsing fixture: {ex.Message}");
            }
        }

        private static RegressionCheckResult RunAssignmentFixtureCheck(ToontownWorldDataDocumentReader reader)
        {
            if (!ToontownToolkitPaths.BundledAssignmentSampleExists())
            {
                return RegressionCheckResult.Fail(
                    "Bundled assignment fixture",
                    $"Missing fixture: {ToontownToolkitPaths.BundledAssignmentSampleRelativePath}");
            }

            try
            {
                WorldDataDocument doc = reader.ReadFromFile(ToontownToolkitPaths.BundledAssignmentSampleFullPath);
                if (doc.Objects.Count != 2)
                {
                    return RegressionCheckResult.Fail(
                        "Bundled assignment fixture",
                        $"Expected 2 objects, got {doc.Objects.Count}.");
                }

                WorldDataObject prop = doc.Objects.FirstOrDefault(o => o.Id == "assign-prop");
                if (prop == null)
                {
                    return RegressionCheckResult.Fail(
                        "Bundled assignment fixture",
                        "Expected object id 'assign-prop' was not parsed.");
                }

                if (!prop.Properties.TryGetValue("Zone", out string zone) || zone != "TutorialPlayground")
                {
                    return RegressionCheckResult.Fail(
                        "Bundled assignment fixture",
                        "Expected 'Zone' property value 'TutorialPlayground' on assign-prop.");
                }

                if (!prop.Properties.TryGetValue("DNA", out string dna) || dna != "tt_mbox_default")
                {
                    return RegressionCheckResult.Fail(
                        "Bundled assignment fixture",
                        "Expected 'DNA' property value 'tt_mbox_default' on assign-prop.");
                }

                return RegressionCheckResult.Pass(
                    "Bundled assignment fixture",
                    $"Parsed expected 2 objects with direct assignment properties (warnings={doc.Warnings.Count}).");
            }
            catch (System.Exception ex)
            {
                return RegressionCheckResult.Fail(
                    "Bundled assignment fixture",
                    $"Exception while parsing fixture: {ex.Message}");
            }
        }

        private static RegressionCheckResult RunDnaStorageFixtureCheck()
        {
            if (!ToontownToolkitPaths.BundledDnaRegressionSamplesExist())
            {
                return RegressionCheckResult.Fail(
                    "Bundled DNA storage fixture",
                    $"Missing fixture pair: {ToontownToolkitPaths.BundledDnaZoneRegressionRelativePath}, " +
                    $"{ToontownToolkitPaths.BundledDnaStorageRegressionRelativePath}");
            }

            try
            {
                var reader = new ToontownDnaDocumentReader();
                WorldDataDocument doc = reader.ReadFromFileWithStorage(
                    ToontownToolkitPaths.BundledDnaZoneRegressionFullPath,
                    new[] { ToontownToolkitPaths.BundledDnaStorageRegressionFullPath });

                WorldDataObject door = doc.Objects.FirstOrDefault(o => o.Id == "prop:front-door");
                if (door == null)
                {
                    return RegressionCheckResult.Fail(
                        "Bundled DNA storage fixture",
                        "Expected DNA object id 'prop:front-door' was not parsed.");
                }

                if (!door.Properties.TryGetValue("ResolvedModel", out string resolvedModel) ||
                    resolvedModel != "phase_4/models/modules/tt_m_ara_int_regression_model")
                {
                    return RegressionCheckResult.Fail(
                        "Bundled DNA storage fixture",
                        $"Expected ResolvedModel from storage mapping, got '{resolvedModel}'.");
                }

                if (!door.Properties.TryGetValue("ResolvedNode", out string resolvedNode) ||
                    resolvedNode != "door_origin_ul")
                {
                    return RegressionCheckResult.Fail(
                        "Bundled DNA storage fixture",
                        $"Expected ResolvedNode 'door_origin_ul', got '{resolvedNode}'.");
                }

                return RegressionCheckResult.Pass(
                    "Bundled DNA storage fixture",
                    $"Resolved storage mapping for prop:front-door (warnings={doc.Warnings.Count}).");
            }
            catch (System.Exception ex)
            {
                return RegressionCheckResult.Fail(
                    "Bundled DNA storage fixture",
                    $"Exception while parsing fixture: {ex.Message}");
            }
        }

        private static RegressionCheckResult RunDnaStyleCodeFixtureCheck()
        {
            if (!ToontownToolkitPaths.BundledDnaStyleRegressionSamplesExist())
            {
                return RegressionCheckResult.Fail(
                    "Bundled DNA style-code fixture",
                    $"Missing fixture pair: {ToontownToolkitPaths.BundledDnaStyleZoneRegressionRelativePath}, " +
                    $"{ToontownToolkitPaths.BundledDnaStyleStorageRegressionRelativePath}");
            }

            try
            {
                var reader = new ToontownDnaDocumentReader();
                WorldDataDocument doc = reader.ReadFromFileWithStorage(
                    ToontownToolkitPaths.BundledDnaStyleZoneRegressionFullPath,
                    new[] { ToontownToolkitPaths.BundledDnaStyleStorageRegressionFullPath });

                var expectedMappings = new Dictionary<string, (string Model, string Node)>
                {
                    ["prop:round-window"] = ("phase_3.5/models/modules/windows", "window_sm_round_ur"),
                    ["prop:curved-window"] = ("phase_3.5/models/modules/windows", "window_md_curved_ur"),
                    ["prop:porthole-window"] = ("phase_3.5/models/modules/windows", "window_porthole_ur"),
                    ["prop:curved-cornice"] = ("phase_3.5/models/modules/cornices", "cornice_curved_ur"),
                    ["prop:round-door"] = ("phase_4/models/modules/doors", "door_double_round_ul"),
                    ["prop:clothshop-door"] = ("phase_4/models/modules/doors", "door_double_clothesshop_ur"),
                    ["landmark_building:clothshop-landmark"] = ("phase_4/models/modules/clothshopTT", "toon_landmark_TT_clothes_shop")
                };

                foreach (KeyValuePair<string, (string Model, string Node)> expected in expectedMappings)
                {
                    WorldDataObject obj = doc.Objects.FirstOrDefault(o => o.Id == expected.Key);
                    if (obj == null)
                    {
                        return RegressionCheckResult.Fail(
                            "Bundled DNA style-code fixture",
                            $"Expected DNA object id '{expected.Key}' was not parsed.");
                    }

                    if (!obj.Properties.TryGetValue("ResolvedModel", out string resolvedModel) ||
                        resolvedModel != expected.Value.Model)
                    {
                        return RegressionCheckResult.Fail(
                            "Bundled DNA style-code fixture",
                            $"Expected ResolvedModel '{expected.Value.Model}' for '{expected.Key}', got '{resolvedModel}'.");
                    }

                    if (!obj.Properties.TryGetValue("ResolvedNode", out string resolvedNode) ||
                        resolvedNode != expected.Value.Node)
                    {
                        return RegressionCheckResult.Fail(
                            "Bundled DNA style-code fixture",
                            $"Expected ResolvedNode '{expected.Value.Node}' for '{expected.Key}', got '{resolvedNode}'.");
                    }
                }

                return RegressionCheckResult.Pass(
                    "Bundled DNA style-code fixture",
                    $"Resolved curated style and landmark codes from bundled storage mapping (warnings={doc.Warnings.Count}).");
            }
            catch (System.Exception ex)
            {
                return RegressionCheckResult.Fail(
                    "Bundled DNA style-code fixture",
                    $"Exception while parsing fixture: {ex.Message}");
            }
        }

        private static RegressionCheckResult RunResolvedNodeStrictFixtureCheck()
        {
            GameObject root = new GameObject("__ToontownResolvedNodeStrictRegression");
            try
            {
                var exact = new GameObject("door_origin_ul");
                exact.transform.SetParent(root.transform, false);

                var fuzzyOnly = new GameObject("door_origin_detail_panel");
                fuzzyOnly.transform.SetParent(root.transform, false);

                if (!ToontownSceneDocumentImporter.TryFindResolvedNodeForRegression(
                        root.transform,
                        "door_origin_ul",
                        allowFuzzyMatch: false,
                        out string matchedName,
                        out string strategy,
                        out string diagnostics))
                {
                    return RegressionCheckResult.Fail(
                        "Resolved-node strict lookup",
                        $"Expected exact strict lookup to pass, but failed: {diagnostics}");
                }

                if (matchedName != "door_origin_ul" || strategy != "exact-name")
                {
                    return RegressionCheckResult.Fail(
                        "Resolved-node strict lookup",
                        $"Expected exact-name match on door_origin_ul, got '{matchedName}' via '{strategy}'.");
                }

                if (ToontownSceneDocumentImporter.TryFindResolvedNodeForRegression(
                        root.transform,
                        "door_origin",
                        allowFuzzyMatch: false,
                        out matchedName,
                        out strategy,
                        out diagnostics))
                {
                    return RegressionCheckResult.Fail(
                        "Resolved-node strict lookup",
                        $"Strict lookup unexpectedly matched '{matchedName}' via '{strategy}'.");
                }

                if (string.IsNullOrWhiteSpace(diagnostics))
                {
                    return RegressionCheckResult.Fail(
                        "Resolved-node strict lookup",
                        "Expected strict lookup miss to include diagnostics.");
                }

                return RegressionCheckResult.Pass(
                    "Resolved-node strict lookup",
                    "Exact strict matching passes and fuzzy-only candidates remain misses.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static RegressionCheckResult RunResolvedNodeFuzzyFixtureCheck()
        {
            GameObject root = new GameObject("__ToontownResolvedNodeFuzzyRegression");
            try
            {
                var fuzzyOnly = new GameObject("door_origin_detail_panel");
                fuzzyOnly.transform.SetParent(root.transform, false);

                if (!ToontownSceneDocumentImporter.TryFindResolvedNodeForRegression(
                        root.transform,
                        "door_origin",
                        allowFuzzyMatch: true,
                        out string matchedName,
                        out string strategy,
                        out string diagnostics))
                {
                    return RegressionCheckResult.Fail(
                        "Resolved-node fuzzy lookup",
                        $"Expected fuzzy lookup to match door_origin_detail_panel, but failed: {diagnostics}");
                }

                bool usedFuzzyStrategy =
                    strategy == "token-overlap" ||
                    strategy.StartsWith("fuzzy-", System.StringComparison.OrdinalIgnoreCase);

                if (matchedName != "door_origin_detail_panel" || !usedFuzzyStrategy)
                {
                    return RegressionCheckResult.Fail(
                        "Resolved-node fuzzy lookup",
                        $"Expected fuzzy match on door_origin_detail_panel, got '{matchedName}' via '{strategy}'.");
                }

                return RegressionCheckResult.Pass(
                    "Resolved-node fuzzy lookup",
                    $"Fuzzy fallback matched '{matchedName}' via '{strategy}'.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static RegressionCheckResult RunResolvedNodeSpellingAliasFixtureCheck()
        {
            GameObject root = new GameObject("__ToontownResolvedNodeSpellingAliasRegression");
            try
            {
                var aliasRoot = new GameObject("door_double_clothshop_ur");
                aliasRoot.transform.SetParent(root.transform, false);

                if (!ToontownSceneDocumentImporter.TryFindResolvedNodeForRegression(
                        root.transform,
                        "door_double_clothesshop_ur",
                        allowFuzzyMatch: false,
                        out string matchedName,
                        out string strategy,
                        out string diagnostics))
                {
                    return RegressionCheckResult.Fail(
                        "Resolved-node spelling alias lookup",
                        $"Expected spelling-alias lookup to match clothshop node, but failed: {diagnostics}");
                }

                if (matchedName != "door_double_clothshop_ur" || strategy != "normalized-name")
                {
                    return RegressionCheckResult.Fail(
                        "Resolved-node spelling alias lookup",
                        $"Expected normalized-name match on door_double_clothshop_ur, got '{matchedName}' via '{strategy}'.");
                }

                return RegressionCheckResult.Pass(
                    "Resolved-node spelling alias lookup",
                    "Normalized resolved-node lookup keeps clothesshop storage aliases aligned with clothshop model nodes.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static RegressionCheckResult RunResolvedNodeModuleAliasFixtureCheck()
        {
            GameObject root = new GameObject("__ToontownResolvedNodeModuleAliasRegression");
            try
            {
                var aliasRoot = new GameObject("partyGate_TT");
                aliasRoot.transform.SetParent(root.transform, false);

                if (ToontownSceneDocumentImporter.TryFindResolvedNodeForRegression(
                        root.transform,
                        "prop_party_gate",
                        allowFuzzyMatch: false,
                        out string matchedName,
                        out string strategy,
                        out string diagnostics))
                {
                    return RegressionCheckResult.Fail(
                        "Resolved-node module alias lookup",
                        $"Strict lookup unexpectedly matched '{matchedName}' via '{strategy}'.");
                }

                if (!ToontownSceneDocumentImporter.TryFindResolvedNodeForRegression(
                        root.transform,
                        "prop_party_gate",
                        allowFuzzyMatch: true,
                        out matchedName,
                        out strategy,
                        out diagnostics))
                {
                    return RegressionCheckResult.Fail(
                        "Resolved-node module alias lookup",
                        $"Expected fuzzy module alias lookup to match partyGate_TT, but failed: {diagnostics}");
                }

                if (matchedName != "partyGate_TT" || strategy != "token-overlap")
                {
                    return RegressionCheckResult.Fail(
                        "Resolved-node module alias lookup",
                        $"Expected token-overlap match on partyGate_TT, got '{matchedName}' via '{strategy}'.");
                }

                return RegressionCheckResult.Pass(
                    "Resolved-node module alias lookup",
                    "CamelCase module aliases match only when fuzzy lookup is enabled.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static RegressionCheckResult RunResolvedNodeParentAnchorAliasFixtureCheck()
        {
            GameObject root = new GameObject("__ToontownResolvedNodeParentAnchorAliasRegression");
            try
            {
                var parentAnchor = new GameObject("library_door_origin");
                parentAnchor.transform.SetParent(root.transform, false);

                if (ToontownSceneDocumentImporter.TryFindResolvedNodeForRegression(
                        root.transform,
                        "door_origin",
                        allowFuzzyMatch: false,
                        out string matchedName,
                        out string strategy,
                        out string diagnostics))
                {
                    return RegressionCheckResult.Fail(
                        "Resolved-node parent-anchor alias lookup",
                        $"Strict lookup unexpectedly matched '{matchedName}' via '{strategy}'.");
                }

                if (!ToontownSceneDocumentImporter.TryFindResolvedNodeForRegression(
                        root.transform,
                        "door_origin",
                        allowFuzzyMatch: true,
                        out matchedName,
                        out strategy,
                        out diagnostics))
                {
                    return RegressionCheckResult.Fail(
                        "Resolved-node parent-anchor alias lookup",
                        $"Expected fuzzy parent-anchor lookup to match library_door_origin, but failed: {diagnostics}");
                }

                if (matchedName != "library_door_origin" || strategy != "token-overlap")
                {
                    return RegressionCheckResult.Fail(
                        "Resolved-node parent-anchor alias lookup",
                        $"Expected token-overlap match on library_door_origin, got '{matchedName}' via '{strategy}'.");
                }

                return RegressionCheckResult.Pass(
                    "Resolved-node parent-anchor alias lookup",
                    "Generic parent-anchor fallbacks can match prefixed building anchors when fuzzy lookup is enabled.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static RegressionCheckResult RunZeroCountWindowGroupFixtureCheck()
        {
            const string rootName = "__ToontownZeroCountWindowRegression";
            var document = new WorldDataDocument
            {
                Name = "zero_count_window_regression"
            };

            document.Objects.Add(new WorldDataObject
            {
                Id = "wall:fixture",
                Properties = new Dictionary<string, string>
                {
                    { "Keyword", "wall" },
                    { "Name", "wall_fixture" }
                }
            });

            document.Objects.Add(new WorldDataObject
            {
                Id = "windows:fixture",
                ParentId = "wall:fixture",
                Properties = new Dictionary<string, string>
                {
                    { "Keyword", "windows" },
                    { "Count", "0" },
                    { "ResolvedModel", "models/modules/windows" },
                    { "ResolvedNode", "window_sm_round_ur" }
                }
            });

            try
            {
                var result = ToontownSceneDocumentImporter.ImportDocument(
                    document,
                    new ToontownSceneImportSettings
                    {
                        UseEggFiles = false,
                        AddObjectListInfo = false,
                        CreatePlaceholderForMissingModel = true,
                        ApplyPreviewLighting = false,
                        RemoveFakeShadowsByDefault = false,
                        RootObjectName = rootName
                    });

                if (result.ZeroCountWindowGroupsSkipped != 1)
                {
                    return RegressionCheckResult.Fail(
                        "Zero-count window group import",
                        $"Expected one skipped zero-count window group, got {result.ZeroCountWindowGroupsSkipped}.");
                }

                if (result.MissingModels != 0 || result.PlaceholdersCreated != 0)
                {
                    return RegressionCheckResult.Fail(
                        "Zero-count window group import",
                        $"Expected no missing model or placeholder for skipped window group, got missing={result.MissingModels}, placeholders={result.PlaceholdersCreated}.");
                }

                if (result.DoorWindowParentAnchorsAttempted != 0 ||
                    result.WindowCountLayoutGroupsPending != 0)
                {
                    return RegressionCheckResult.Fail(
                        "Zero-count window group import",
                        $"Expected no anchor/layout attempt for skipped window group, got anchors={result.DoorWindowParentAnchorsAttempted}, pendingLayouts={result.WindowCountLayoutGroupsPending}.");
                }

                return RegressionCheckResult.Pass(
                    "Zero-count window group import",
                    "Skipped zero-count wall window groups without model or placement work.");
            }
            finally
            {
                GameObject root = GameObject.Find(rootName);
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static RegressionCheckResult RunSingleCountWindowGroupFixtureCheck()
        {
            const string rootName = "__ToontownSingleCountWindowRegression";
            var document = new WorldDataDocument
            {
                Name = "single_count_window_regression"
            };

            document.Objects.Add(new WorldDataObject
            {
                Id = "wall:fixture",
                Properties = new Dictionary<string, string>
                {
                    { "Keyword", "wall" },
                    { "Name", "wall_fixture" },
                    { "ResolvedModel", "models/modules/walls" }
                }
            });

            document.Objects.Add(new WorldDataObject
            {
                Id = "windows:fixture",
                ParentId = "wall:fixture",
                Properties = new Dictionary<string, string>
                {
                    { "Keyword", "windows" },
                    { "Count", "1" },
                    { "ResolvedModel", "models/modules/windows" },
                    { "ResolvedNode", "window_sm_round_ur" }
                }
            });

            try
            {
                var result = ToontownSceneDocumentImporter.ImportDocument(
                    document,
                    new ToontownSceneImportSettings
                    {
                        UseEggFiles = false,
                        AddObjectListInfo = false,
                        CreatePlaceholderForMissingModel = false,
                        ApplyPreviewLighting = false,
                        RemoveFakeShadowsByDefault = false,
                        RootObjectName = rootName
                    });

                if (result.WindowCountLayoutGroupsPending != 0 ||
                    result.WindowCountLayoutRequestedInstances != 0)
                {
                    return RegressionCheckResult.Fail(
                        "Single-count window group import",
                        $"Expected single-count window group to avoid pending layout bucket, got pending={result.WindowCountLayoutGroupsPending}, requested={result.WindowCountLayoutRequestedInstances}.");
                }

                if (result.DoorWindowParentAnchorsAttempted != 1 ||
                    result.DoorWindowParentAnchorsMissed != 1)
                {
                    return RegressionCheckResult.Fail(
                        "Single-count window group import",
                        $"Expected single-count window group to use normal parent-anchor flow, got attempted={result.DoorWindowParentAnchorsAttempted}, missed={result.DoorWindowParentAnchorsMissed}.");
                }

                if (result.GetWarningCategoryCount(ToontownSceneImportResult.WindowCountLayoutCategory) != 0)
                {
                    return RegressionCheckResult.Fail(
                        "Single-count window group import",
                        "Expected no window-count-layout warning category entry for single-count window group.");
                }

                return RegressionCheckResult.Pass(
                    "Single-count window group import",
                    "Single-count wall window groups stay on the normal parent-anchor placement path.");
            }
            finally
            {
                GameObject root = GameObject.Find(rootName);
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static RegressionCheckResult RunMultiCountWindowOffsetFixtureCheck()
        {
            float[] offsets = ToontownSceneDocumentImporter.BuildEvenlySpacedWallWindowOffsetsForRegression(2, 15f);
            if (offsets == null || offsets.Length != 2)
            {
                return RegressionCheckResult.Fail(
                    "Multi-count window layout offsets",
                    $"Expected 2 offsets, got {(offsets == null ? 0 : offsets.Length)}.");
            }

            if (!Mathf.Approximately(offsets[0], -2.5f) || !Mathf.Approximately(offsets[1], 2.5f))
            {
                return RegressionCheckResult.Fail(
                    "Multi-count window layout offsets",
                    $"Expected offsets [-2.5, 2.5], got [{offsets[0]}, {offsets[1]}].");
            }

            return RegressionCheckResult.Pass(
                "Multi-count window layout offsets",
                "Two-window wall layout offsets stay evenly spaced across the parent width.");
        }

        private static RegressionCheckResult RunNarrowWallWindowCountFixtureCheck()
        {
            int effectiveCount = ToontownSceneDocumentImporter.GetEffectiveWallWindowCountForRegression(2, 14.9f);
            if (effectiveCount != 1)
            {
                return RegressionCheckResult.Fail(
                    "Narrow wall window-count clamp",
                    $"Expected narrow walls to clamp two requested windows down to one, got {effectiveCount}.");
            }

            float[] offsets = ToontownSceneDocumentImporter.BuildEvenlySpacedWallWindowOffsetsForRegression(2, 14.9f);
            if (offsets == null || offsets.Length != 1)
            {
                return RegressionCheckResult.Fail(
                    "Narrow wall window-count clamp",
                    $"Expected one centered offset after clamping, got {(offsets == null ? 0 : offsets.Length)}.");
            }

            if (!Mathf.Approximately(offsets[0], 0f))
            {
                return RegressionCheckResult.Fail(
                    "Narrow wall window-count clamp",
                    $"Expected centered offset [0] after clamping, got [{offsets[0]}].");
            }

            return RegressionCheckResult.Pass(
                "Narrow wall window-count clamp",
                "Narrow wall spans clamp multi-window requests to a single centered window to match style-editor parity.");
        }

        private static RegressionCheckResult RunHighCountWindowOffsetFixtureCheck()
        {
            float[] offsets = ToontownSceneDocumentImporter.BuildEvenlySpacedWallWindowOffsetsForRegression(4, 20f);
            if (offsets == null || offsets.Length != 4)
            {
                return RegressionCheckResult.Fail(
                    "High-count window layout offsets",
                    $"Expected 4 offsets, got {(offsets == null ? 0 : offsets.Length)}.");
            }

            if (!Mathf.Approximately(offsets[0], -6f) ||
                !Mathf.Approximately(offsets[1], -2f) ||
                !Mathf.Approximately(offsets[2], 2f) ||
                !Mathf.Approximately(offsets[3], 6f))
            {
                return RegressionCheckResult.Fail(
                    "High-count window layout offsets",
                    $"Expected offsets [-6, -2, 2, 6], got [{offsets[0]}, {offsets[1]}, {offsets[2]}, {offsets[3]}].");
            }

            return RegressionCheckResult.Pass(
                "High-count window layout offsets",
                "Wide wall layout offsets stay evenly spaced for four-window style cases authored in OpenLevelEditor style files.");
        }

        private static RegressionCheckResult RunLandmarkSignLabelFixtureCheck()
        {
            var document = new WorldDataDocument
            {
                Name = "landmark-sign-regression",
                Objects = new List<WorldDataObject>
                {
                    new WorldDataObject
                    {
                        Id = "landmark:clothingshop",
                        Properties = new Dictionary<string, string>
                        {
                            { "Keyword", "landmark_building" },
                            { "Title", "Clothing Shop" }
                        }
                    },
                    new WorldDataObject
                    {
                        Id = "sign:clothingshop",
                        ParentId = "landmark:clothingshop",
                        Properties = new Dictionary<string, string>
                        {
                            { "Keyword", "sign" }
                        }
                    },
                    new WorldDataObject
                    {
                        Id = "baseline:clothingshop",
                        ParentId = "sign:clothingshop",
                        Properties = new Dictionary<string, string>
                        {
                            { "Keyword", "baseline" },
                            { "Color", "1 0.611765 0.423529 1" }
                        }
                    }
                }
            };

            ToontownSceneImportResult result = ToontownSceneDocumentImporter.ImportDocument(
                document,
                new ToontownSceneImportSettings
                {
                    RootObjectName = "__ToontownLandmarkSignLabelRegression",
                    ApplyPreviewLighting = false,
                    AddObjectListInfo = false,
                    CreatePlaceholderForMissingModel = false,
                    RemoveFakeShadowsByDefault = false
                });

            try
            {
                GameObject baseline = GameObject.Find("baseline:clothingshop");
                if (baseline == null)
                {
                    return RegressionCheckResult.Fail(
                        "Landmark sign label import",
                        "Expected baseline fixture object was not created.");
                }

                TextMesh label = baseline.GetComponentInChildren<TextMesh>(true);
                if (label == null)
                {
                    return RegressionCheckResult.Fail(
                        "Landmark sign label import",
                        "Expected landmark baseline to create a TextMesh label.");
                }

                if (label.text != "Clothing Shop")
                {
                    return RegressionCheckResult.Fail(
                        "Landmark sign label import",
                        $"Expected landmark sign label text 'Clothing Shop', got '{label.text}'.");
                }

                return RegressionCheckResult.Pass(
                    "Landmark sign label import",
                    "Landmark baseline nodes emit visible TextMesh labels using the building title.");
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(result.RootObjectName))
                {
                    GameObject root = GameObject.Find(result.RootObjectName);
                    if (root != null)
                    {
                        UnityEngine.Object.DestroyImmediate(root);
                    }
                }
            }
        }

        private static RegressionCheckResult RunSingleCountWindowOffsetFixtureCheck()
        {
            float[] offsets = ToontownSceneDocumentImporter.BuildEvenlySpacedWallWindowOffsetsForRegression(1, 15f);
            if (offsets == null || offsets.Length != 1)
            {
                return RegressionCheckResult.Fail(
                    "Single-count window layout offsets",
                    $"Expected 1 offset, got {(offsets == null ? 0 : offsets.Length)}.");
            }

            if (!Mathf.Approximately(offsets[0], 0f))
            {
                return RegressionCheckResult.Fail(
                    "Single-count window layout offsets",
                    $"Expected centered offset [0], got [{offsets[0]}].");
            }

            return RegressionCheckResult.Pass(
                "Single-count window layout offsets",
                "Single-window wall layout fallback stays centered on the parent width.");
        }

        private static RegressionCheckResult RunEggMaterialScopeFixtureCheck()
        {
            const string rootName = "__ToontownEggMaterialScopeRegression";
            var root = new GameObject(rootName);

            try
            {
                string[] lines =
                {
                    "<Group> parent {",
                    "  <TRef> { parent_tex }",
                    "  <Group> alpha_child {",
                    "    <Scalar> alpha { blend }",
                    "    <Polygon> {",
                    "    }",
                    "  }",
                    "  <Group> child_textured {",
                    "    <TRef> { child_tex }",
                    "    <Polygon> {",
                    "    }",
                    "  }",
                    "  <Group> sibling {",
                    "    <Polygon> {",
                    "    }",
                    "  }",
                    "}",
                    "<Group> isolated {",
                    "  <Texture> unused_tex {",
                    "    \"phase_4/maps/unused.png\"",
                    "  }",
                    "  <Material> unused_mat {",
                    "  }",
                    "  <Polygon> {",
                    "  }",
                    "}"
                };

                var hierarchyMap = new Dictionary<string, Transform>
                {
                    { string.Empty, root.transform }
                };
                var geometryMap = new Dictionary<string, GeometryData>();
                var processor = new GeometryProcessor();

                processor.BuildHierarchyAndMapGeometry(
                    lines,
                    0,
                    lines.Length,
                    string.Empty,
                    hierarchyMap,
                    geometryMap);

                string alphaChildMaterials = GetMaterialSummary(geometryMap, "parent/alpha_child");
                if (alphaChildMaterials != "parent_tex_ALPHABLEND")
                {
                    return RegressionCheckResult.Fail(
                        "EGG material scope",
                        $"Expected alpha_child to inherit parent_tex with alpha blend, got '{alphaChildMaterials}'.");
                }

                string childTexturedMaterials = GetMaterialSummary(geometryMap, "parent/child_textured");
                if (childTexturedMaterials != "parent_tex||child_tex")
                {
                    return RegressionCheckResult.Fail(
                        "EGG material scope",
                        $"Expected child_textured to combine scoped parent and child texture refs, got '{childTexturedMaterials}'.");
                }

                string siblingMaterials = GetMaterialSummary(geometryMap, "parent/sibling");
                if (siblingMaterials != "parent_tex")
                {
                    return RegressionCheckResult.Fail(
                        "EGG material scope",
                        $"Expected sibling to inherit only parent_tex, got '{siblingMaterials}'.");
                }

                string isolatedMaterials = GetMaterialSummary(geometryMap, "isolated");
                if (isolatedMaterials != "Default-Material")
                {
                    return RegressionCheckResult.Fail(
                        "EGG material scope",
                        $"Expected isolated group to ignore texture/material definitions without TRef, got '{isolatedMaterials}'.");
                }

                return RegressionCheckResult.Pass(
                    "EGG material scope",
                    "Scoped texture refs and alpha blend inheritance stay within their EGG group boundaries.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static string GetMaterialSummary(
            Dictionary<string, GeometryData> geometryMap,
            string path)
        {
            if (!geometryMap.TryGetValue(path, out GeometryData geometry))
            {
                return "<missing geometry>";
            }

            if (geometry.materialNames.Count == 0)
            {
                return "<none>";
            }

            return string.Join(",", geometry.materialNames);
        }

        private sealed class RegressionCheckResult
        {
            public string Name;
            public bool Passed;
            public string Message;

            public static RegressionCheckResult Pass(string name, string message)
            {
                return new RegressionCheckResult
                {
                    Name = name,
                    Passed = true,
                    Message = message
                };
            }

            public static RegressionCheckResult Fail(string name, string message)
            {
                return new RegressionCheckResult
                {
                    Name = name,
                    Passed = false,
                    Message = message
                };
            }
        }
    }
}
