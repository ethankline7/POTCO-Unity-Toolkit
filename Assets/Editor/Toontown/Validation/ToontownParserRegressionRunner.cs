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

        public static bool Run(out string report)
        {
            var checks = new List<RegressionCheckResult>();
            var reader = new ToontownWorldDataDocumentReader();

            checks.Add(RunDictionaryFixtureCheck(reader));
            checks.Add(RunAssignmentFixtureCheck(reader));

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

        private static RegressionCheckResult RunAssignmentFixtureCheck(ToontownWorldDataDocumentReader reader)
        {
            if (!ToontownToolkitPaths.BundledAssignmentSampleExists())
            {
                return RegressionCheckResult.Fail(
                    "Bundled assignment fixture",
                    $"Missing fixture: {ToontownToolkitPaths.BundledAssignmentSampleRelativePath}");
            }

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
