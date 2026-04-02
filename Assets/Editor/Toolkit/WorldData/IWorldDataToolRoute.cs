namespace Toolkit.Editor.WorldData
{
    public interface IWorldDataToolRoute
    {
        string DisplayName { get; }
        string ImporterMenuPath { get; }
        string ExporterMenuPath { get; }
    }
}
