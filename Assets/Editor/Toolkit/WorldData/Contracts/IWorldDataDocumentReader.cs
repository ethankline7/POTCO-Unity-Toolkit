namespace Toolkit.Editor.WorldData.Contracts
{
    public interface IWorldDataDocumentReader
    {
        string FormatId { get; }
        bool CanRead(string sourcePath);
        WorldDataDocument ReadFromFile(string sourcePath);
    }
}
