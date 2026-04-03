using Toolkit.Core;

namespace Toolkit.Editor.WorldData
{
    public static class WorldDataToolLauncherRegistry
    {
        private static readonly IWorldDataToolLauncher PotcoLauncher = new PotcoWorldDataToolLauncher();
        private static readonly IWorldDataToolLauncher ToontownLauncher = new ToontownWorldDataToolLauncher();

        public static IWorldDataToolLauncher GetActiveLauncher()
        {
            return WorldDataToolRouteResolver.GetActiveGameFlavor() == GameFlavor.Toontown
                ? ToontownLauncher
                : PotcoLauncher;
        }
    }
}
