using Toolkit.Core;

namespace Toolkit.Editor.WorldData
{
    public sealed class ToontownWorldDataToolRoute : IWorldDataToolRoute
    {
        public string DisplayName => GameFlavor.Toontown.ToString();
        public string ImporterMenuPath => "Toontown/World Data/Importer";
        public string ExporterMenuPath => "Toontown/World Data/Exporter";
    }
}
