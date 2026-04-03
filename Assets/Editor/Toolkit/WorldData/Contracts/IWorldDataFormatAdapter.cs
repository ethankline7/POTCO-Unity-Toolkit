namespace Toolkit.Editor.WorldData.Contracts
{
    public interface IWorldDataFormatAdapter
    {
        string FormatId { get; }
        IWorldDataDocumentReader Reader { get; }
        IWorldDataDocumentWriter Writer { get; }
    }
}
