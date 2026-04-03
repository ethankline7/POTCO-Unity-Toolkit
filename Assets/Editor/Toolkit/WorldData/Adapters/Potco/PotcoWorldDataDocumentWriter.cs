using System;
using Toolkit.Editor.WorldData.Contracts;

namespace Toolkit.Editor.WorldData.Adapters.Potco
{
    public sealed class PotcoWorldDataDocumentWriter : IWorldDataDocumentWriter
    {
        public string FormatId => "potco.py.objectstruct";

        public bool CanWrite(string outputPath)
        {
            return !string.IsNullOrWhiteSpace(outputPath) && outputPath.EndsWith(".py", StringComparison.OrdinalIgnoreCase);
        }

        public void WriteToFile(WorldDataDocument document, string outputPath)
        {
            throw new NotSupportedException(
                "Shared POTCO document writer not wired yet. Use POTCO/World Data/Exporter for behavior.");
        }
    }
}
