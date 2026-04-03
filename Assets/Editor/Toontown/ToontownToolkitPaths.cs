using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Toontown.Editor
{
    internal static class ToontownToolkitPaths
    {
        public const string BundledSampleRelativePath = "Assets/Editor/Toontown/Samples/toontown_sample_world.py";
        public const string BundledAssignmentSampleRelativePath = "Assets/Editor/Toontown/Samples/toontown_sample_world_assignment_style.py";
        public const string SuggestedExportRelativePath = "Assets/Editor/Toontown/Samples/Generated/toontown_sample_export.py";
        public const string SuggestedAssignmentExportRelativePath = "Assets/Editor/Toontown/Samples/Generated/toontown_sample_assignment_export.py";

        public const string SuggestedDnaSampleRelativePath = "External/open-toontown-resources/phase_4/dna/toontown_central_sz.dna";
        public const string SuggestedDnaStoragePhase35RelativePath = "External/open-toontown-resources/phase_3.5/dna/storage.dna";
        public const string SuggestedDnaStoragePhase4RelativePath = "External/open-toontown-resources/phase_4/dna/storage.dna";
        public const string SuggestedDnaStoragePhase5RelativePath = "External/open-toontown-resources/phase_5/dna/storage_town.dna";

        public static string BundledSampleFullPath => ToFullPath(BundledSampleRelativePath);
        public static string BundledAssignmentSampleFullPath => ToFullPath(BundledAssignmentSampleRelativePath);
        public static string SuggestedExportFullPath => ToFullPath(SuggestedExportRelativePath);
        public static string SuggestedAssignmentExportFullPath => ToFullPath(SuggestedAssignmentExportRelativePath);
        public static string SuggestedDnaSampleFullPath => ToFullPath(SuggestedDnaSampleRelativePath);
        public static string SuggestedDnaStoragePhase35FullPath => ToFullPath(SuggestedDnaStoragePhase35RelativePath);
        public static string SuggestedDnaStoragePhase4FullPath => ToFullPath(SuggestedDnaStoragePhase4RelativePath);
        public static string SuggestedDnaStoragePhase5FullPath => ToFullPath(SuggestedDnaStoragePhase5RelativePath);

        public static bool BundledSampleExists()
        {
            return File.Exists(BundledSampleFullPath);
        }

        public static bool BundledAssignmentSampleExists()
        {
            return File.Exists(BundledAssignmentSampleFullPath);
        }

        public static bool SuggestedDnaSampleExists()
        {
            return File.Exists(SuggestedDnaSampleFullPath);
        }

        public static IReadOnlyList<string> GetSuggestedDnaStorageFullPaths()
        {
            return new[]
            {
                SuggestedDnaStoragePhase35FullPath,
                SuggestedDnaStoragePhase4FullPath,
                SuggestedDnaStoragePhase5FullPath
            }
            .Where(File.Exists)
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .ToList();
        }

        public static void EnsureSuggestedExportDirectoryExists()
        {
            string directory = Path.GetDirectoryName(SuggestedExportFullPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static string ToFullPath(string relativePath)
        {
            return Path.Combine(
                Directory.GetCurrentDirectory(),
                relativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
