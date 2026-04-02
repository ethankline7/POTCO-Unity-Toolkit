using System;
using Toolkit.Editor.WorldData.Contracts;

namespace Toolkit.Editor.WorldData.Adapters.Toontown
{
    public sealed class ToontownWorldDataDocumentWriter : IWorldDataDocumentWriter
    {
        public string FormatId => "toontown.py.zone";

        public bool CanWrite(string outputPath)
        {
            return !string.IsNullOrWhiteSpace(outputPath) && outputPath.EndsWith(".py", StringComparison.OrdinalIgnoreCase);
        }

        public void WriteToFile(WorldDataDocument document, string outputPath)
        {
            throw new NotSupportedException(
                "Toontown document writer is a scaffold and has not been implemented yet.");
        }
    }
}
