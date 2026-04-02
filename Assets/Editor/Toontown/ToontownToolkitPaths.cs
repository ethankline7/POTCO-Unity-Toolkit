using System.IO;

namespace Toontown.Editor
{
    internal static class ToontownToolkitPaths
    {
        public const string BundledSampleRelativePath = "Assets/Editor/Toontown/Samples/toontown_sample_world.py";
        public const string SuggestedExportRelativePath = "Assets/Editor/Toontown/Samples/Generated/toontown_sample_export.py";

        public static string BundledSampleFullPath => ToFullPath(BundledSampleRelativePath);
        public static string SuggestedExportFullPath => ToFullPath(SuggestedExportRelativePath);

        public static bool BundledSampleExists()
        {
            return File.Exists(BundledSampleFullPath);
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
