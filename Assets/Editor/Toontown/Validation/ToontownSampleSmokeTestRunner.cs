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
            if (!ToontownToolkitPaths.BundledSampleExists())
            {
                string missing =
                    $"Bundled sample not found at {ToontownToolkitPaths.BundledSampleRelativePath}.";
                Debug.LogError(missing);
                EditorUtility.DisplayDialog("Toontown Smoke Test", missing, "OK");
                return;
            }

            string source = ToontownToolkitPaths.BundledSampleFullPath;
            ToontownToolkitPaths.EnsureSuggestedExportDirectoryExists();
            string output = ToontownToolkitPaths.SuggestedExportFullPath;

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
                string failure = $"Smoke test failed: {ex.Message}";
                Debug.LogError(failure);
                EditorUtility.DisplayDialog("Toontown Smoke Test", failure, "OK");
                return;
            }

            bool countMatch = inputDoc.Objects.Count == outputDoc.Objects.Count;
            string status = countMatch ? "PASS" : "WARN";

            var report = new StringBuilder();
            report.AppendLine("Toontown Sample Smoke Test");
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
            EditorUtility.DisplayDialog("Toontown Smoke Test", $"Completed with status: {status}", "OK");
        }
    }
}
