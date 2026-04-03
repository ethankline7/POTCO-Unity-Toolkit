using Toolkit.Editor.WorldData.Contracts;

namespace Toolkit.Editor.WorldData.Adapters.Toontown
{
    public sealed class ToontownWorldDataFormatAdapter : IWorldDataFormatAdapter
    {
        private static readonly IWorldDataDocumentReader ReaderInstance = new ToontownWorldDataDocumentReader();
        private static readonly IWorldDataDocumentWriter WriterInstance = new ToontownWorldDataDocumentWriter();

        public string FormatId => "toontown.py.zone";
        public IWorldDataDocumentReader Reader => ReaderInstance;
        public IWorldDataDocumentWriter Writer => WriterInstance;
    }
}
