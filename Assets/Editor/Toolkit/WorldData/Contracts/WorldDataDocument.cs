using System.Collections.Generic;

namespace Toolkit.Editor.WorldData.Contracts
{
    public sealed class WorldDataDocument
    {
        public string Name;
        public List<WorldDataObject> Objects = new List<WorldDataObject>();
        public List<string> Warnings = new List<string>();
    }
}
