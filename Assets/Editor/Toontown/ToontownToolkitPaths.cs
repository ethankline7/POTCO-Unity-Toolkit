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
        public const string SuggestedDnaStoragePhase35InteriorRelativePath = "External/open-toontown-resources/phase_3.5/dna/storage_interior.dna";
        public const string SuggestedDnaStoragePhase35TutorialRelativePath = "External/open-toontown-resources/phase_3.5/dna/storage_tutorial.dna";
        public const string SuggestedDnaStoragePhase4RelativePath = "External/open-toontown-resources/phase_4/dna/storage.dna";
        public const string SuggestedDnaStoragePhase4TtRelativePath = "External/open-toontown-resources/phase_4/dna/storage_TT.dna";
        public const string SuggestedDnaStoragePhase4TtSafeZoneRelativePath = "External/open-toontown-resources/phase_4/dna/storage_TT_sz.dna";
        public const string SuggestedDnaStoragePhase5TownRelativePath = "External/open-toontown-resources/phase_5/dna/storage_town.dna";
        public const string SuggestedDnaStoragePhase5TtTownRelativePath = "External/open-toontown-resources/phase_5/dna/storage_TT_town.dna";

        public static string BundledSampleFullPath => ToFullPath(BundledSampleRelativePath);
        public static string BundledAssignmentSampleFullPath => ToFullPath(BundledAssignmentSampleRelativePath);
        public static string SuggestedExportFullPath => ToFullPath(SuggestedExportRelativePath);
        public static string SuggestedAssignmentExportFullPath => ToFullPath(SuggestedAssignmentExportRelativePath);
        public static string SuggestedDnaSampleFullPath => ToFullPath(SuggestedDnaSampleRelativePath);
        public static string SuggestedDnaStoragePhase35InteriorFullPath => ToFullPath(SuggestedDnaStoragePhase35InteriorRelativePath);
        public static string SuggestedDnaStoragePhase35TutorialFullPath => ToFullPath(SuggestedDnaStoragePhase35TutorialRelativePath);
        public static string SuggestedDnaStoragePhase4FullPath => ToFullPath(SuggestedDnaStoragePhase4RelativePath);
        public static string SuggestedDnaStoragePhase4TtFullPath => ToFullPath(SuggestedDnaStoragePhase4TtRelativePath);
        public static string SuggestedDnaStoragePhase4TtSafeZoneFullPath => ToFullPath(SuggestedDnaStoragePhase4TtSafeZoneRelativePath);
        public static string SuggestedDnaStoragePhase5TownFullPath => ToFullPath(SuggestedDnaStoragePhase5TownRelativePath);
        public static string SuggestedDnaStoragePhase5TtTownFullPath => ToFullPath(SuggestedDnaStoragePhase5TtTownRelativePath);

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
                SuggestedDnaStoragePhase35InteriorFullPath,
                SuggestedDnaStoragePhase35TutorialFullPath,
                SuggestedDnaStoragePhase4FullPath,
                SuggestedDnaStoragePhase4TtFullPath,
                SuggestedDnaStoragePhase4TtSafeZoneFullPath,
                SuggestedDnaStoragePhase5TownFullPath,
                SuggestedDnaStoragePhase5TtTownFullPath
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
