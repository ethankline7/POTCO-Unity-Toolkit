using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Toolkit.Editor.WorldData.Contracts;

namespace Toolkit.Editor.WorldData.Adapters.Toontown
{
    public sealed class ToontownWorldDataDocumentWriter : IWorldDataDocumentWriter
    {
        private static readonly Regex NumericRegex = new Regex(
            @"^[-+]?(?:\d+\.?\d*|\.\d+)$",
            RegexOptions.Compiled);

        public string FormatId => "toontown.py.zone";

        public bool CanWrite(string outputPath)
        {
            return !string.IsNullOrWhiteSpace(outputPath) && outputPath.EndsWith(".py", StringComparison.OrdinalIgnoreCase);
        }

        public void WriteToFile(WorldDataDocument document, string outputPath)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (!CanWrite(outputPath))
            {
                throw new NotSupportedException($"Unsupported output file type: {outputPath}");
            }

            var byParent = BuildChildrenLookup(document.Objects);
            var allIds = new HashSet<string>(document.Objects.Select(o => o.Id), StringComparer.OrdinalIgnoreCase);
            var roots = document.Objects
                .Where(o => string.IsNullOrWhiteSpace(o.ParentId) || !allIds.Contains(o.ParentId))
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("objectStruct = {");
            sb.AppendLine("    'Objects': {");

            for (int i = 0; i < roots.Count; i++)
            {
                WriteObjectRecursive(sb, roots[i], byParent, 2, i == roots.Count - 1);
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, sb.ToString());
        }

        private static Dictionary<string, List<WorldDataObject>> BuildChildrenLookup(List<WorldDataObject> objects)
        {
            var lookup = new Dictionary<string, List<WorldDataObject>>(StringComparer.OrdinalIgnoreCase);
            foreach (var obj in objects)
            {
                if (string.IsNullOrWhiteSpace(obj.ParentId))
                {
                    continue;
                }

                if (!lookup.TryGetValue(obj.ParentId, out var children))
                {
                    children = new List<WorldDataObject>();
                    lookup[obj.ParentId] = children;
                }

                children.Add(obj);
            }

            return lookup;
        }

        private static void WriteObjectRecursive(
            StringBuilder sb,
            WorldDataObject obj,
            Dictionary<string, List<WorldDataObject>> byParent,
            int indentLevel,
            bool isLast)
        {
            string indent = new string(' ', indentLevel * 4);
            string inner = new string(' ', (indentLevel + 1) * 4);

            sb.AppendLine($"{indent}'{EscapeSingleQuote(obj.Id)}': {{");

            var orderedProperties = ToontownPropertyOrdering.Sort(obj.Properties);

            for (int i = 0; i < orderedProperties.Count; i++)
            {
                bool needsComma = i < orderedProperties.Count - 1 || byParent.ContainsKey(obj.Id);
                string key = EscapeSingleQuote(orderedProperties[i].Key);
                string value = FormatPythonValue(orderedProperties[i].Value);
                sb.AppendLine($"{inner}'{key}': {value}{(needsComma ? "," : string.Empty)}");
            }

            if (byParent.TryGetValue(obj.Id, out var children) && children.Count > 0)
            {
                sb.AppendLine($"{inner}'Objects': {{");
                for (int i = 0; i < children.Count; i++)
                {
                    WriteObjectRecursive(sb, children[i], byParent, indentLevel + 2, i == children.Count - 1);
                }
                sb.AppendLine($"{inner}}}");
            }

            sb.AppendLine($"{indent}}}{(isLast ? string.Empty : ",")}");
        }

        private static string FormatPythonValue(string value)
        {
            if (value == null)
            {
                return "None";
            }

            string trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return "''";
            }

            if (trimmed.StartsWith("'") && trimmed.EndsWith("'"))
            {
                return trimmed;
            }

            if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
            {
                return trimmed;
            }

            if (trimmed == "True" || trimmed == "False" || trimmed == "None")
            {
                return trimmed;
            }

            if (NumericRegex.IsMatch(trimmed))
            {
                return double.Parse(trimmed, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            }

            if (trimmed.EndsWith(")") || trimmed.StartsWith("(") || trimmed.StartsWith("["))
            {
                return trimmed;
            }

            return $"'{EscapeSingleQuote(trimmed)}'";
        }

        private static string EscapeSingleQuote(string input)
        {
            return input.Replace("\\", "\\\\").Replace("'", "\\'");
        }
    }
}
