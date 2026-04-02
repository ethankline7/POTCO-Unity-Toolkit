using Toolkit.Core;
using UnityEditor;

namespace Toolkit.Editor.WorldData
{
    public static class WorldDataToolRouteResolver
    {
        private const string SettingsAssetPath = "Assets/Resources/Toolkit/ToolkitProjectSettings.asset";
        private static readonly IWorldDataToolRoute PotcoRoute = new PotcoWorldDataToolRoute();
        private static readonly IWorldDataToolRoute ToontownRoute = new ToontownWorldDataToolRoute();

        public static IWorldDataToolRoute ResolveActiveRoute()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ToolkitProjectSettings>(SettingsAssetPath);
            if (settings == null)
            {
                return PotcoRoute;
            }

            return settings.activeGameFlavor == GameFlavor.Toontown ? ToontownRoute : PotcoRoute;
        }
    }
}
