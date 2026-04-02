using UnityEditor;

namespace Toolkit.Editor.WorldData
{
    public sealed class PotcoWorldDataToolLauncher : IWorldDataToolLauncher
    {
        private static readonly IWorldDataToolRoute Route = new PotcoWorldDataToolRoute();

        public string DisplayName => Route.DisplayName;
        public string ImporterMenuPath => Route.ImporterMenuPath;
        public string ExporterMenuPath => Route.ExporterMenuPath;

        public bool OpenImporter()
        {
            return EditorApplication.ExecuteMenuItem(ImporterMenuPath);
        }

        public bool OpenExporter()
        {
            return EditorApplication.ExecuteMenuItem(ExporterMenuPath);
        }
    }
}
