using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Toolkit.Editor.WorldData.Contracts;

namespace Toolkit.Editor.WorldData.Adapters.Toontown
{
    public sealed class ToontownWorldDataDocumentReader : IWorldDataDocumentReader
    {
        private static readonly Regex DictEntryRegex = new Regex(
            @"^\s*'([^']+)':\s*\{",
            RegexOptions.Compiled);

        private static readonly Regex PropertyRegex = new Regex(
            @"^\s*'([^']+)':\s*(.+?)\s*,?\s*$",
            RegexOptions.Compiled);

        private static readonly HashSet<string> ReservedDictKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Objects",
            "Visual",
            "Properties",
            "Attribs"
        };

        private static readonly HashSet<string> LikelyObjectProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Pos",
            "GridPos",
            "Hpr",
            "Scale",
            "Type",
            "Model",
            "Name",
            "Zone",
            "Parent",
            "DNA"
        };

        public string FormatId => "toontown.py.zone";

        public bool CanRead(string sourcePath)
        {
            return !string.IsNullOrWhiteSpace(sourcePath) && sourcePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase);
        }

        public WorldDataDocument ReadFromFile(string sourcePath)
        {
            if (!CanRead(sourcePath))
            {
                throw new NotSupportedException($"Unsupported file type for Toontown reader: {sourcePath}");
            }

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Toontown source file not found.", sourcePath);
            }

            var document = new WorldDataDocument
            {
                Name = Path.GetFileNameWithoutExtension(sourcePath)
            };

            string[] lines = File.ReadAllLines(sourcePath);
            var stack = new Stack<ParseNode>();
            var parsedNodes = new List<ParseNode>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                int indent = GetIndent(line);

                while (stack.Count > 0 && indent <= stack.Peek().Indent)
                {
                    stack.Pop();
                }

                Match entryMatch = DictEntryRegex.Match(line);
                if (entryMatch.Success)
                {
                    string key = entryMatch.Groups[1].Value.Trim();
                    if (IsCandidateObjectKey(key))
                    {
                        var node = new ParseNode
                        {
                            Id = key,
                            ParentId = stack.Count > 0 ? stack.Peek().Id : null,
                            Indent = indent
                        };

                        parsedNodes.Add(node);
                        stack.Push(node);
                    }

                    continue;
                }

                if (stack.Count == 0)
                {
                    continue;
                }

                Match propertyMatch = PropertyRegex.Match(line);
                if (!propertyMatch.Success)
                {
                    continue;
                }

                string propKey = propertyMatch.Groups[1].Value.Trim();
                string propValue = propertyMatch.Groups[2].Value.Trim();
                stack.Peek().Properties[propKey] = propValue;
            }

            var likelyObjects = parsedNodes.Where(IsLikelyWorldObject).ToList();
            foreach (var node in likelyObjects)
            {
                var worldObject = new WorldDataObject
                {
                    Id = node.Id,
                    ParentId = likelyObjects.Any(x => x.Id == node.ParentId) ? node.ParentId : null,
                    Properties = new Dictionary<string, string>(node.Properties, StringComparer.OrdinalIgnoreCase)
                };

                document.Objects.Add(worldObject);
            }

            return document;
        }

        private static int GetIndent(string line)
        {
            int indent = 0;
            while (indent < line.Length && char.IsWhiteSpace(line[indent]))
            {
                indent++;
            }

            return indent;
        }

        private static bool IsCandidateObjectKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (ReservedDictKeys.Contains(key))
            {
                return false;
            }

            return true;
        }

        private static bool IsLikelyWorldObject(ParseNode node)
        {
            if (node.Properties.Count == 0)
            {
                return false;
            }

            foreach (string key in node.Properties.Keys)
            {
                if (LikelyObjectProperties.Contains(key))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class ParseNode
        {
            public string Id;
            public string ParentId;
            public int Indent;
            public Dictionary<string, string> Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
