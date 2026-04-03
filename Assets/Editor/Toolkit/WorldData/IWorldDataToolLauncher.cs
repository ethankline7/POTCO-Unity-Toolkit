namespace Toolkit.Editor.WorldData
{
    public interface IWorldDataToolLauncher
    {
        string DisplayName { get; }
        string ImporterMenuPath { get; }
        string ExporterMenuPath { get; }

        bool OpenImporter();
        bool OpenExporter();
    }
}
