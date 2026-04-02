using Toolkit.Core;
using UnityEditor;

namespace Toolkit.Editor.WorldData
{
    public static class WorldDataToolRouteResolver
    {
        private const string SettingsAssetPath = "Assets/Resources/Toolkit/ToolkitProjectSettings.asset";
        private static readonly IWorldDataToolRoute PotcoRoute = new PotcoWorldDataToolRoute();
        private static readonly IWorldDataToolRoute ToontownRoute = new ToontownWorldDataToolRoute();

        public static GameFlavor GetActiveGameFlavor()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ToolkitProjectSettings>(SettingsAssetPath);
            if (settings == null)
            {
                return GameFlavor.POTCO;
            }

            return settings.activeGameFlavor;
        }

        public static IWorldDataToolRoute ResolveActiveRoute()
        {
            return GetActiveGameFlavor() == GameFlavor.Toontown ? ToontownRoute : PotcoRoute;
        }
    }
}
