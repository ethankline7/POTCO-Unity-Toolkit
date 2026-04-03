using System;
using Toolkit.Editor.WorldData.Contracts;

namespace Toolkit.Editor.WorldData.Adapters.Potco
{
    public sealed class PotcoWorldDataDocumentReader : IWorldDataDocumentReader
    {
        public string FormatId => "potco.py.objectstruct";

        public bool CanRead(string sourcePath)
        {
            return !string.IsNullOrWhiteSpace(sourcePath) && sourcePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase);
        }

        public WorldDataDocument ReadFromFile(string sourcePath)
        {
            throw new NotSupportedException(
                "Shared POTCO document reader not wired yet. Use POTCO/World Data/Importer for behavior.");
        }
    }
}
