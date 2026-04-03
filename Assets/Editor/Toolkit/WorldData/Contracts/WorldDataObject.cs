using System.Collections.Generic;

namespace Toolkit.Editor.WorldData.Contracts
{
    public sealed class WorldDataObject
    {
        public string Id;
        public string ParentId;
        public Dictionary<string, string> Properties = new Dictionary<string, string>();
    }
}
