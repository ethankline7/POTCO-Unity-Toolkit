using Toolkit.Core;

namespace Toolkit.Editor.WorldData
{
    public sealed class PotcoWorldDataToolRoute : IWorldDataToolRoute
    {
        public string DisplayName => GameFlavor.POTCO.ToString();
        public string ImporterMenuPath => "POTCO/World Data/Importer";
        public string ExporterMenuPath => "POTCO/World Data/Exporter";
    }
}
