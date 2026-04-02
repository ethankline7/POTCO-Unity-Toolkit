using UnityEditor;
using UnityEngine;

namespace Toolkit.Editor.WorldData
{
    public static class ToolkitWorldDataMenu
    {
        [MenuItem("Toolkit/World Data/Open Importer")]
        public static void OpenImporter()
        {
            var launcher = WorldDataToolLauncherRegistry.GetActiveLauncher();
            if (!launcher.OpenImporter())
            {
                Debug.LogError($"Failed to open importer: {launcher.ImporterMenuPath}");
            }
        }

        [MenuItem("Toolkit/World Data/Open Exporter")]
        public static void OpenExporter()
        {
            var launcher = WorldDataToolLauncherRegistry.GetActiveLauncher();
            if (!launcher.OpenExporter())
            {
                Debug.LogError($"Failed to open exporter: {launcher.ExporterMenuPath}");
            }
        }
    }
}
