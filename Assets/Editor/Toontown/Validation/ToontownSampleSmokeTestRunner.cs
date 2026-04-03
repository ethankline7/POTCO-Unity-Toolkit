using System.IO;
using System.Text;
using Toolkit.Editor.WorldData.Adapters.Toontown;
using Toolkit.Editor.WorldData.Contracts;
using UnityEditor;
using UnityEngine;

namespace Toontown.Editor.Validation
{
    public static class ToontownSampleSmokeTestRunner
    {
        [MenuItem("Toontown/Validation/Run Sample Smoke Test")]
        public static void Run()
        {
            if (!ToontownToolkitPaths.BundledSampleExists() || !ToontownToolkitPaths.BundledAssignmentSampleExists())
            {
                var missingBuilder = new StringBuilder();
                if (!ToontownToolkitPaths.BundledSampleExists())
                {
                    missingBuilder.AppendLine($"Bundled sample not found at {ToontownToolkitPaths.BundledSampleRelativePath}.");
                }

                if (!ToontownToolkitPaths.BundledAssignmentSampleExists())
                {
                    missingBuilder.AppendLine($"Assignment sample not found at {ToontownToolkitPaths.BundledAssignmentSampleRelativePath}.");
                }

                string missing = missingBuilder.ToString().Trim();
                Debug.LogError(missing);
                ShowDialogIfInteractive("Toontown Smoke Test", missing);
                return;
            }

            ToontownToolkitPaths.EnsureSuggestedExportDirectoryExists();

            bool firstPass = RunSingleSample(
                source: ToontownToolkitPaths.BundledSampleFullPath,
                output: ToontownToolkitPaths.SuggestedExportFullPath,
                sampleLabel: "Bundled Dictionary Sample");

            bool secondPass = RunSingleSample(
                source: ToontownToolkitPaths.BundledAssignmentSampleFullPath,
                output: ToontownToolkitPaths.SuggestedAssignmentExportFullPath,
                sampleLabel: "Bundled Assignment Sample");

            string finalStatus = (firstPass && secondPass) ? "PASS" : "WARN";
            ShowDialogIfInteractive("Toontown Smoke Test", $"Completed with status: {finalStatus}");
        }

        private static bool RunSingleSample(string source, string output, string sampleLabel)
        {
            var reader = new ToontownWorldDataDocumentReader();
            var writer = new ToontownWorldDataDocumentWriter();

            WorldDataDocument inputDoc;
            WorldDataDocument outputDoc;

            try
            {
                inputDoc = reader.ReadFromFile(source);
                writer.WriteToFile(inputDoc, output);
                outputDoc = reader.ReadFromFile(output);
            }
            catch (System.Exception ex)
            {
                string failure = $"{sampleLabel} smoke test failed: {ex.Message}";
                Debug.LogError(failure);
                return false;
            }

            bool countMatch = inputDoc.Objects.Count == outputDoc.Objects.Count;
            string status = countMatch ? "PASS" : "WARN";

            var report = new StringBuilder();
            report.AppendLine("Toontown Sample Smoke Test");
            report.AppendLine($"Sample: {sampleLabel}");
            report.AppendLine($"Status: {status}");
            report.AppendLine($"Source: {source}");
            report.AppendLine($"Output: {output}");
            report.AppendLine($"Input objects: {inputDoc.Objects.Count}");
            report.AppendLine($"Output objects: {outputDoc.Objects.Count}");
            report.AppendLine($"Input warnings: {inputDoc.Warnings.Count}");
            report.AppendLine($"Output warnings: {outputDoc.Warnings.Count}");

            if (!countMatch)
            {
                report.AppendLine("Round-trip object count mismatch detected.");
            }

            if (File.Exists(output))
            {
                report.AppendLine($"Output file size: {new FileInfo(output).Length} bytes");
            }

            Debug.Log(report.ToString());
            return countMatch;
        }

        private static void ShowDialogIfInteractive(string title, string message)
        {
            if (Application.isBatchMode)
            {
                return;
            }

            EditorUtility.DisplayDialog(title, message, "OK");
        }
    }
}
