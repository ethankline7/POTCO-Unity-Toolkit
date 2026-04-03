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
        private const int MaxInferenceWarnings = 20;
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
            int warningCountFromInference = 0;
            int suppressedInferenceWarnings = 0;
            var typeMapper = ToontownObjectTypeMapper.LoadOrCreateDefault(out string mapperLoadWarning);
            if (!string.IsNullOrWhiteSpace(mapperLoadWarning))
            {
                document.Warnings.Add(mapperLoadWarning);
            }

            string[] lines = File.ReadAllLines(sourcePath);
            var stack = new Stack<ParseNode>();
            var dictScopeStack = new Stack<DictScope>();
            int objectsScopeDepth = 0;
            bool sawObjectsScope = false;
            var parsedNodes = new List<ParseNode>();
            ParseNode pendingPropertyNode = null;
            string pendingPropertyKey = null;
            string pendingPropertyRawValue = null;
            int pendingContinuationBalance = 0;

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

                while (dictScopeStack.Count > 0 && indent <= dictScopeStack.Peek().Indent)
                {
                    var popped = dictScopeStack.Pop();
                    if (popped.IsObjectsScope)
                    {
                        objectsScopeDepth--;
                    }
                }

                if (pendingPropertyNode != null)
                {
                    string continuationSegment = line.Trim();
                    pendingPropertyRawValue = $"{pendingPropertyRawValue} {continuationSegment}";
                    pendingContinuationBalance += GetContinuationBalanceDelta(continuationSegment);

                    if (pendingContinuationBalance <= 0)
                    {
                        string completedValue = ToontownPropertyNormalizer.NormalizeForDocument(pendingPropertyRawValue);
                        pendingPropertyNode.Properties[pendingPropertyKey] = completedValue;
                        pendingPropertyNode = null;
                        pendingPropertyKey = null;
                        pendingPropertyRawValue = null;
                        pendingContinuationBalance = 0;
                    }

                    continue;
                }

                Match entryMatch = DictEntryRegex.Match(line);
                if (entryMatch.Success)
                {
                    string key = entryMatch.Groups[1].Value.Trim();
                    bool isObjectsScope = string.Equals(key, "Objects", StringComparison.OrdinalIgnoreCase);
                    bool insideObjectsScope = objectsScopeDepth > 0;

                    if (insideObjectsScope && IsCandidateObjectKey(key))
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

                    dictScopeStack.Push(new DictScope
                    {
                        Indent = indent,
                        IsObjectsScope = isObjectsScope
                    });

                    if (isObjectsScope)
                    {
                        objectsScopeDepth++;
                        sawObjectsScope = true;
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
                string propRawValue = propertyMatch.Groups[2].Value;

                if (ShouldStartContinuation(propRawValue, out int continuationBalance))
                {
                    pendingPropertyNode = stack.Peek();
                    pendingPropertyKey = propKey;
                    pendingPropertyRawValue = propRawValue;
                    pendingContinuationBalance = continuationBalance;
                    continue;
                }

                string propValue = ToontownPropertyNormalizer.NormalizeForDocument(propRawValue);
                stack.Peek().Properties[propKey] = propValue;
            }

            if (pendingPropertyNode != null)
            {
                string bestEffortValue = ToontownPropertyNormalizer.NormalizeForDocument(pendingPropertyRawValue);
                pendingPropertyNode.Properties[pendingPropertyKey] = bestEffortValue;
                document.Warnings.Add(
                    $"Property '{pendingPropertyKey}' on object '{pendingPropertyNode.Id}' ended with unbalanced continuation; captured best-effort value.");
            }

            if (!sawObjectsScope)
            {
                document.Warnings.Add(
                    "No 'Objects' dictionary scope was detected. Verify source format assumptions for this file.");
            }

            var likelyObjects = parsedNodes.Where(IsLikelyWorldObject).ToList();
            var duplicateIdCounts = likelyObjects
                .GroupBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var node in likelyObjects)
            {
                var worldObject = new WorldDataObject
                {
                    Id = node.Id,
                    ParentId = likelyObjects.Any(x => x.Id == node.ParentId) ? node.ParentId : null,
                    Properties = new Dictionary<string, string>(node.Properties, StringComparer.OrdinalIgnoreCase)
                };

                ApplyTypeInference(worldObject, typeMapper, document, ref warningCountFromInference, ref suppressedInferenceWarnings);

                document.Objects.Add(worldObject);
            }

            if (document.Objects.Count == 0)
            {
                document.Warnings.Add(
                    "No likely world objects were detected. Verify source format assumptions for this file.");
            }

            if (duplicateIdCounts.Count > 0)
            {
                foreach (var group in duplicateIdCounts)
                {
                    document.Warnings.Add($"Duplicate object id detected: {group.Key} ({group.Count()} entries).");
                }
            }

            if (suppressedInferenceWarnings > 0)
            {
                document.Warnings.Add(
                    $"Suppressed {suppressedInferenceWarnings} additional type-inference warnings (limit {MaxInferenceWarnings}).");
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

        private static void ApplyTypeInference(
            WorldDataObject worldObject,
            ToontownObjectTypeMapper typeMapper,
            WorldDataDocument document,
            ref int warningCountFromInference,
            ref int suppressedInferenceWarnings)
        {
            if (worldObject.Properties.ContainsKey("Type"))
            {
                return;
            }

            if (!worldObject.Properties.TryGetValue("Model", out string modelRaw))
            {
                AddInferenceWarning(
                    document,
                    $"Object '{worldObject.Id}' has no 'Type' or 'Model' property; type inference skipped.",
                    ref warningCountFromInference,
                    ref suppressedInferenceWarnings);
                return;
            }

            string modelPath = NormalizeModelValue(modelRaw);
            string inferredType = typeMapper.InferTypeFromModel(modelPath, out bool usedDefault);
            worldObject.Properties["Type"] = inferredType;

            if (usedDefault)
            {
                AddInferenceWarning(
                    document,
                    $"Object '{worldObject.Id}' model '{modelPath}' did not match mapping rules; default type '{inferredType}' applied.",
                    ref warningCountFromInference,
                    ref suppressedInferenceWarnings);
            }
        }

        private static string NormalizeModelValue(string modelRaw)
        {
            if (string.IsNullOrWhiteSpace(modelRaw))
            {
                return string.Empty;
            }

            string trimmed = modelRaw.Trim();
            if ((trimmed.StartsWith("'") && trimmed.EndsWith("'")) ||
                (trimmed.StartsWith("\"") && trimmed.EndsWith("\"")))
            {
                return trimmed.Substring(1, trimmed.Length - 2);
            }

            return trimmed;
        }

        private static void AddInferenceWarning(
            WorldDataDocument document,
            string message,
            ref int warningCountFromInference,
            ref int suppressedInferenceWarnings)
        {
            if (warningCountFromInference < MaxInferenceWarnings)
            {
                document.Warnings.Add(message);
                warningCountFromInference++;
            }
            else
            {
                suppressedInferenceWarnings++;
            }
        }

        private static bool ShouldStartContinuation(string rawValue, out int continuationBalance)
        {
            continuationBalance = 0;
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            string trimmed = rawValue.TrimStart();
            if (trimmed.StartsWith("{"))
            {
                return false;
            }

            continuationBalance = GetContinuationBalanceDelta(trimmed);
            if (continuationBalance <= 0)
            {
                return false;
            }

            if (trimmed.StartsWith("[") || trimmed.StartsWith("("))
            {
                return true;
            }

            return trimmed.IndexOf('(') >= 0;
        }

        private static int GetContinuationBalanceDelta(string text)
        {
            bool inSingleQuote = false;
            bool inDoubleQuote = false;
            bool escaped = false;
            int delta = 0;

            foreach (char c in text)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (!inDoubleQuote && c == '\'')
                {
                    inSingleQuote = !inSingleQuote;
                    continue;
                }

                if (!inSingleQuote && c == '"')
                {
                    inDoubleQuote = !inDoubleQuote;
                    continue;
                }

                if (inSingleQuote || inDoubleQuote)
                {
                    continue;
                }

                if (c == '[' || c == '(')
                {
                    delta++;
                    continue;
                }

                if (c == ']' || c == ')')
                {
                    delta--;
                }
            }

            return delta;
        }

        private sealed class ParseNode
        {
            public string Id;
            public string ParentId;
            public int Indent;
            public Dictionary<string, string> Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class DictScope
        {
            public int Indent;
            public bool IsObjectsScope;
        }
    }
}
