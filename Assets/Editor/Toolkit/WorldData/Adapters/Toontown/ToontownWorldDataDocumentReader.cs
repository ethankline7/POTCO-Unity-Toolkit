using System;
using Toolkit.Editor.WorldData.Contracts;

namespace Toolkit.Editor.WorldData.Adapters.Toontown
{
    public sealed class ToontownWorldDataDocumentReader : IWorldDataDocumentReader
    {
        public string FormatId => "toontown.py.zone";

        public bool CanRead(string sourcePath)
        {
            return !string.IsNullOrWhiteSpace(sourcePath) && sourcePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase);
        }

        public WorldDataDocument ReadFromFile(string sourcePath)
        {
            throw new NotSupportedException(
                "Toontown document reader is a scaffold and has not been implemented yet.");
        }
    }
}
