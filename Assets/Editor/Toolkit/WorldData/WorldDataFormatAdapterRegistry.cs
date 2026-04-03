using Toolkit.Core;
using Toolkit.Editor.WorldData.Adapters.Potco;
using Toolkit.Editor.WorldData.Adapters.Toontown;
using Toolkit.Editor.WorldData.Contracts;

namespace Toolkit.Editor.WorldData
{
    public static class WorldDataFormatAdapterRegistry
    {
        private static readonly IWorldDataFormatAdapter PotcoAdapter = new PotcoWorldDataFormatAdapter();
        private static readonly IWorldDataFormatAdapter ToontownAdapter = new ToontownWorldDataFormatAdapter();

        public static IWorldDataFormatAdapter GetActiveAdapter()
        {
            return WorldDataToolRouteResolver.GetActiveGameFlavor() == GameFlavor.Toontown
                ? ToontownAdapter
                : PotcoAdapter;
        }
    }
}
