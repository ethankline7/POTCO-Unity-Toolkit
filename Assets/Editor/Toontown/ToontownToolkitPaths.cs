using System.IO;

namespace Toontown.Editor
{
    internal static class ToontownToolkitPaths
    {
        public const string BundledSampleRelativePath = "Assets/Editor/Toontown/Samples/toontown_sample_world.py";
        public const string BundledAssignmentSampleRelativePath = "Assets/Editor/Toontown/Samples/toontown_sample_world_assignment_style.py";
        public const string SuggestedExportRelativePath = "Assets/Editor/Toontown/Samples/Generated/toontown_sample_export.py";
        public const string SuggestedAssignmentExportRelativePath = "Assets/Editor/Toontown/Samples/Generated/toontown_sample_assignment_export.py";

        public static string BundledSampleFullPath => ToFullPath(BundledSampleRelativePath);
        public static string BundledAssignmentSampleFullPath => ToFullPath(BundledAssignmentSampleRelativePath);
        public static string SuggestedExportFullPath => ToFullPath(SuggestedExportRelativePath);
        public static string SuggestedAssignmentExportFullPath => ToFullPath(SuggestedAssignmentExportRelativePath);

        public static bool BundledSampleExists()
        {
            return File.Exists(BundledSampleFullPath);
        }

        public static bool BundledAssignmentSampleExists()
        {
            return File.Exists(BundledAssignmentSampleFullPath);
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
