using System;
using System.Collections.Generic;
using System.Linq;

namespace Toolkit.Editor.WorldData.Adapters.Toontown
{
    public static class ToontownPropertyOrdering
    {
        private static readonly string[] PreferredOrder =
        {
            "Type",
            "Name",
            "Model",
            "Pos",
            "GridPos",
            "Hpr",
            "Scale",
            "DNA",
            "Zone",
            "Parent"
        };

        public static List<KeyValuePair<string, string>> Sort(IDictionary<string, string> properties)
        {
            var list = properties.ToList();
            list.Sort(CompareProperties);
            return list;
        }

        private static int CompareProperties(KeyValuePair<string, string> a, KeyValuePair<string, string> b)
        {
            int ai = IndexOfPreferred(a.Key);
            int bi = IndexOfPreferred(b.Key);

            if (ai != bi)
            {
                return ai.CompareTo(bi);
            }

            return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
        }

        private static int IndexOfPreferred(string key)
        {
            for (int i = 0; i < PreferredOrder.Length; i++)
            {
                if (string.Equals(PreferredOrder[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return int.MaxValue;
        }
    }
}
