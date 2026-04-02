using Toolkit.Editor.WorldData.Contracts;

namespace Toolkit.Editor.WorldData.Adapters.Potco
{
    public sealed class PotcoWorldDataFormatAdapter : IWorldDataFormatAdapter
    {
        private static readonly IWorldDataDocumentReader ReaderInstance = new PotcoWorldDataDocumentReader();
        private static readonly IWorldDataDocumentWriter WriterInstance = new PotcoWorldDataDocumentWriter();

        public string FormatId => "potco.py.objectstruct";
        public IWorldDataDocumentReader Reader => ReaderInstance;
        public IWorldDataDocumentWriter Writer => WriterInstance;
    }
}
