namespace Toolkit.Editor.WorldData.Contracts
{
    public interface IWorldDataDocumentWriter
    {
        string FormatId { get; }
        bool CanWrite(string outputPath);
        void WriteToFile(WorldDataDocument document, string outputPath);
    }
}
