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
            checks.Add(RunResolvedNodeStrictFixtureCheck());
            checks.Add(RunResolvedNodeFuzzyFixtureCheck());
            checks.Add(RunResolvedNodeModuleAliasFixtureCheck());
            checks.Add(RunResolvedNodeParentAnchorAliasFixtureCheck());

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
