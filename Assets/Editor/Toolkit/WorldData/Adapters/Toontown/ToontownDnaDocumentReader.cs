using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Toolkit.Editor.WorldData.Contracts;

namespace Toolkit.Editor.WorldData.Adapters.Toontown
{
    public sealed class ToontownDnaDocumentReader : IWorldDataDocumentReader
    {
        private static readonly Regex ModelStartRegex = new Regex(
            @"^\s*(?<keyword>[A-Za-z_][A-Za-z0-9_]*)\s+""(?<path>[^""]+)""\s*\[\s*$",
            RegexOptions.Compiled);

        private static readonly Regex NamedBlockQuotedRegex = new Regex(
            @"^\s*(?<keyword>[A-Za-z_][A-Za-z0-9_]*)\s+""(?<name>[^""]+)""\s*\[\s*$",
            RegexOptions.Compiled);

        private static readonly Regex NamedBlockBareRegex = new Regex(
            @"^\s*(?<keyword>[A-Za-z_][A-Za-z0-9_]*)\s+(?<name>[A-Za-z0-9_:\.-]+)\s*\[\s*$",
            RegexOptions.Compiled);

        private static readonly Regex ListPropertyRegex = new Regex(
            @"^\s*(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*\[\s*(?<value>.*)\s*\]\s*$",
            RegexOptions.Compiled);

        private static readonly Regex NumberRegex = new Regex(
            @"[-+]?\d*\.?\d+(?:[eE][-+]?\d+)?",
            RegexOptions.Compiled);

        public string FormatId => "toontown.dna.zone";

        public bool CanRead(string sourcePath)
        {
            return !string.IsNullOrWhiteSpace(sourcePath) &&
                   sourcePath.EndsWith(".dna", StringComparison.OrdinalIgnoreCase);
        }

        public WorldDataDocument ReadFromFile(string sourcePath)
        {
            return ReadFromFileWithStorage(sourcePath, null);
        }

        public WorldDataDocument ReadFromFileWithStorage(string sourcePath, IEnumerable<string> storagePaths)
        {
            if (!CanRead(sourcePath))
            {
                throw new NotSupportedException($"Unsupported file type for Toontown DNA reader: {sourcePath}");
            }

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Toontown DNA source file not found.", sourcePath);
            }

            var document = new WorldDataDocument
            {
                Name = Path.GetFileNameWithoutExtension(sourcePath)
            };

            var storeNodeLookup = BuildStoreNodeLookup(storagePaths, document);
            ParseDnaFile(sourcePath, storeNodeLookup, document);
            FinalizeObjects(document, storeNodeLookup);

            return document;
        }

        private static Dictionary<string, StoreNodeRecord> BuildStoreNodeLookup(
            IEnumerable<string> storagePaths,
            WorldDataDocument document)
        {
            var lookup = new Dictionary<string, StoreNodeRecord>(StringComparer.OrdinalIgnoreCase);
            if (storagePaths == null)
            {
                return lookup;
            }

            foreach (string storagePath in storagePaths)
            {
                if (string.IsNullOrWhiteSpace(storagePath))
                {
                    continue;
                }

                if (!File.Exists(storagePath))
                {
                    document.Warnings.Add($"Storage file not found: {storagePath}");
                    continue;
                }

                try
                {
                    ParseDnaFile(storagePath, lookup, null);
                }
                catch (Exception ex)
                {
                    document.Warnings.Add($"Failed to parse storage file '{storagePath}': {ex.Message}");
                }
            }

            return lookup;
        }

        private static void ParseDnaFile(
            string sourcePath,
            Dictionary<string, StoreNodeRecord> storeNodeLookup,
            WorldDataDocument targetDocument)
        {
            string[] lines = File.ReadAllLines(sourcePath);
            var scopeStack = new Stack<ScopeFrame>();
            var idCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int generatedIndex = 1;

            for (int i = 0; i < lines.Length; i++)
            {
                string noComment = StripInlineComment(lines[i]);
                if (string.IsNullOrWhiteSpace(noComment))
                {
                    continue;
                }

                string line = noComment.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                while (line.StartsWith("]", StringComparison.Ordinal))
                {
                    if (scopeStack.Count > 0)
                    {
                        scopeStack.Pop();
                    }

                    line = line.Substring(1).TrimStart();
                    if (line.Length == 0)
                    {
                        break;
                    }
                }

                if (line.Length == 0)
                {
                    continue;
                }

                Match modelStart = ModelStartRegex.Match(line);
                if (modelStart.Success && IsModelKeyword(modelStart.Groups["keyword"].Value))
                {
                    scopeStack.Push(new ScopeFrame
                    {
                        Kind = ScopeKind.Model,
                        ModelPath = modelStart.Groups["path"].Value.Trim()
                    });
                    continue;
                }

                Match namedQuoted = NamedBlockQuotedRegex.Match(line);
                if (namedQuoted.Success)
                {
                    PushNamedObjectScope(namedQuoted, scopeStack, targetDocument, idCounts, ref generatedIndex);
                    continue;
                }

                Match namedBare = NamedBlockBareRegex.Match(line);
                if (namedBare.Success)
                {
                    PushNamedObjectScope(namedBare, scopeStack, targetDocument, idCounts, ref generatedIndex);
                    continue;
                }

                if (line.EndsWith("[", StringComparison.Ordinal))
                {
                    scopeStack.Push(new ScopeFrame { Kind = ScopeKind.Other });
                    continue;
                }

                Match listPropertyMatch = ListPropertyRegex.Match(line);
                if (!listPropertyMatch.Success)
                {
                    continue;
                }

                string key = listPropertyMatch.Groups["key"].Value.Trim();
                string value = listPropertyMatch.Groups["value"].Value.Trim();

                ScopeFrame currentScope = scopeStack.Count > 0 ? scopeStack.Peek() : null;
                if (currentScope == null)
                {
                    continue;
                }

                if (currentScope.Kind == ScopeKind.Model && string.Equals(key, "store_node", StringComparison.OrdinalIgnoreCase))
                {
                    ParseStoreNode(value, currentScope.ModelPath, storeNodeLookup);
                    continue;
                }

                if (currentScope.Kind != ScopeKind.Object || currentScope.Node == null)
                {
                    continue;
                }

                ApplyProperty(currentScope.Node, key, value);
            }
        }

        private static void PushNamedObjectScope(
            Match namedMatch,
            Stack<ScopeFrame> scopeStack,
            WorldDataDocument targetDocument,
            Dictionary<string, int> idCounts,
            ref int generatedIndex)
        {
            string keyword = namedMatch.Groups["keyword"].Value.Trim();
            string rawName = namedMatch.Groups["name"].Value.Trim();
            string displayName = string.IsNullOrWhiteSpace(rawName) ? $"{keyword}_{generatedIndex}" : rawName;
            string baseId = BuildBaseId(keyword, displayName);
            string id = MakeUniqueId(baseId, idCounts);

            string parentId = null;
            foreach (ScopeFrame frame in scopeStack)
            {
                if (frame.Kind == ScopeKind.Object && frame.Node != null)
                {
                    parentId = frame.Node.Id;
                    break;
                }
            }

            var node = new DnaParseNode
            {
                Id = id,
                ParentId = parentId,
                Keyword = keyword,
                DisplayName = displayName
            };

            node.Properties["Keyword"] = keyword;
            node.Properties["Name"] = displayName;

            if (targetDocument != null)
            {
                targetDocument.Objects.Add(new WorldDataObject
                {
                    Id = node.Id,
                    ParentId = node.ParentId,
                    Properties = node.Properties
                });
            }

            scopeStack.Push(new ScopeFrame
            {
                Kind = ScopeKind.Object,
                Node = node
            });

            generatedIndex++;
        }

        private static string BuildBaseId(string keyword, string displayName)
        {
            string cleanedName = Regex.Replace(displayName, @"[^A-Za-z0-9_:\.-]", "_");
            cleanedName = cleanedName.Trim('_');
            if (string.IsNullOrWhiteSpace(cleanedName))
            {
                cleanedName = keyword;
            }

            return $"{keyword}:{cleanedName}";
        }

        private static string MakeUniqueId(string baseId, Dictionary<string, int> idCounts)
        {
            if (!idCounts.TryGetValue(baseId, out int count))
            {
                idCounts[baseId] = 1;
                return baseId;
            }

            count++;
            idCounts[baseId] = count;
            return $"{baseId}#{count}";
        }

        private static void ParseStoreNode(
            string rawValue,
            string modelPath,
            Dictionary<string, StoreNodeRecord> storeNodeLookup)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                return;
            }

            List<string> tokens = ParseListTokens(rawValue);
            if (tokens.Count < 2)
            {
                return;
            }

            string nodeName = SanitizeStoreNodeToken(tokens[1]);
            string preferredCode = tokens.Count > 2 ? SanitizeStoreNodeToken(tokens[2]) : nodeName;
            if (string.IsNullOrWhiteSpace(nodeName) || string.IsNullOrWhiteSpace(preferredCode))
            {
                return;
            }

            var record = new StoreNodeRecord
            {
                ModelPath = modelPath,
                NodeName = nodeName
            };

            storeNodeLookup[preferredCode] = record;
            storeNodeLookup[nodeName] = record;
        }

        private static string SanitizeStoreNodeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            if (trimmed.StartsWith("(", StringComparison.Ordinal))
            {
                trimmed = trimmed.TrimStart('(');
            }

            if (trimmed.EndsWith(")", StringComparison.Ordinal))
            {
                trimmed = trimmed.TrimEnd(')');
            }

            return trimmed.Trim();
        }

        private static void ApplyProperty(DnaParseNode node, string key, string value)
        {
            string normalizedKey = key.Trim();
            string normalizedValue = NormalizeListValue(value);

            if (string.Equals(normalizedKey, "code", StringComparison.OrdinalIgnoreCase))
            {
                List<string> tokens = ParseListTokens(value);
                node.Properties["Code"] = tokens.Count > 0 ? tokens[0] : normalizedValue;
                return;
            }

            if (string.Equals(normalizedKey, "model", StringComparison.OrdinalIgnoreCase))
            {
                List<string> tokens = ParseListTokens(value);
                node.Properties["Model"] = tokens.Count > 0 ? tokens[0] : normalizedValue;
                return;
            }

            if (string.Equals(normalizedKey, "pos", StringComparison.OrdinalIgnoreCase))
            {
                if (TryExtractVector(value, out float x, out float y, out float z))
                {
                    node.Properties["Pos"] = FormatTriple(x, y, z);
                }
                return;
            }

            if (string.Equals(normalizedKey, "hpr", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedKey, "nhpr", StringComparison.OrdinalIgnoreCase))
            {
                if (TryExtractVector(value, out float h, out float p, out float r))
                {
                    node.Properties["Hpr"] = FormatTriple(h, p, r);
                }
                return;
            }

            if (string.Equals(normalizedKey, "scale", StringComparison.OrdinalIgnoreCase))
            {
                List<float> numeric = ExtractNumbers(value);
                if (numeric.Count == 1)
                {
                    node.Properties["Scale"] = FormatTriple(numeric[0], numeric[0], numeric[0]);
                }
                else if (numeric.Count >= 3)
                {
                    node.Properties["Scale"] = FormatTriple(numeric[0], numeric[1], numeric[2]);
                }
                return;
            }

            if (!node.Properties.ContainsKey(normalizedKey))
            {
                node.Properties[normalizedKey] = normalizedValue;
            }
        }

        private static void FinalizeObjects(
            WorldDataDocument document,
            Dictionary<string, StoreNodeRecord> storeNodeLookup)
        {
            var mapper = ToontownObjectTypeMapper.LoadOrCreateDefault(out string mapperWarning);
            if (!string.IsNullOrWhiteSpace(mapperWarning))
            {
                document.Warnings.Add(mapperWarning);
            }

            foreach (WorldDataObject obj in document.Objects)
            {
                if (!obj.Properties.ContainsKey("Model") &&
                    obj.Properties.TryGetValue("Code", out string code) &&
                    !string.IsNullOrWhiteSpace(code) &&
                    storeNodeLookup.TryGetValue(code, out StoreNodeRecord record))
                {
                    obj.Properties["ResolvedModel"] = record.ModelPath;
                    obj.Properties["ResolvedNode"] = record.NodeName;
                }

                string modelForType = null;
                if (obj.Properties.TryGetValue("ResolvedModel", out string resolvedModel))
                {
                    modelForType = resolvedModel;
                }
                else if (obj.Properties.TryGetValue("Model", out string directModel))
                {
                    modelForType = directModel;
                }

                if (!obj.Properties.ContainsKey("Type"))
                {
                    if (!string.IsNullOrWhiteSpace(modelForType))
                    {
                        obj.Properties["Type"] = mapper.InferTypeFromModel(modelForType, out _);
                    }
                    else if (obj.Properties.TryGetValue("Keyword", out string keyword) && !string.IsNullOrWhiteSpace(keyword))
                    {
                        obj.Properties["Type"] = keyword;
                    }
                    else
                    {
                        obj.Properties["Type"] = "Unknown";
                    }
                }
            }

            if (document.Objects.Count == 0)
            {
                document.Warnings.Add("No named DNA blocks were parsed. Verify file format and parser assumptions.");
            }
        }

        private static string StripInlineComment(string line)
        {
            bool inSingle = false;
            bool inDouble = false;
            bool escaped = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

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

                if (!inDouble && c == '\'')
                {
                    inSingle = !inSingle;
                    continue;
                }

                if (!inSingle && c == '"')
                {
                    inDouble = !inDouble;
                    continue;
                }

                if (c == '#' && !inSingle && !inDouble)
                {
                    return line.Substring(0, i);
                }
            }

            return line;
        }

        private static string NormalizeListValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(value.Length);
            bool previousWhitespace = false;
            foreach (char c in value.Trim())
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!previousWhitespace)
                    {
                        sb.Append(' ');
                    }

                    previousWhitespace = true;
                }
                else
                {
                    sb.Append(c);
                    previousWhitespace = false;
                }
            }

            return sb.ToString();
        }

        private static List<string> ParseListTokens(string value)
        {
            var tokens = new List<string>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return tokens;
            }

            var sb = new StringBuilder();
            bool inSingle = false;
            bool inDouble = false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '"' && !inSingle)
                {
                    inDouble = !inDouble;
                    continue;
                }

                if (c == '\'' && !inDouble)
                {
                    inSingle = !inSingle;
                    continue;
                }

                if (!inSingle && !inDouble && (char.IsWhiteSpace(c) || c == ','))
                {
                    if (sb.Length > 0)
                    {
                        tokens.Add(sb.ToString());
                        sb.Clear();
                    }

                    continue;
                }

                sb.Append(c);
            }

            if (sb.Length > 0)
            {
                tokens.Add(sb.ToString());
            }

            return tokens;
        }

        private static bool TryExtractVector(string value, out float x, out float y, out float z)
        {
            x = 0f;
            y = 0f;
            z = 0f;
            List<float> numbers = ExtractNumbers(value);
            if (numbers.Count < 3)
            {
                return false;
            }

            x = numbers[0];
            y = numbers[1];
            z = numbers[2];
            return true;
        }

        private static List<float> ExtractNumbers(string value)
        {
            var list = new List<float>();
            MatchCollection matches = NumberRegex.Matches(value ?? string.Empty);
            foreach (Match match in matches)
            {
                if (float.TryParse(match.Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float number))
                {
                    list.Add(number);
                }
            }

            return list;
        }

        private static string FormatTriple(float x, float y, float z)
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0} {1} {2}",
                x,
                y,
                z);
        }

        private static bool IsModelKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return false;
            }

            string normalized = keyword.Trim();
            return string.Equals(normalized, "model", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("_model", StringComparison.OrdinalIgnoreCase);
        }

        private enum ScopeKind
        {
            Other,
            Object,
            Model
        }

        private sealed class ScopeFrame
        {
            public ScopeKind Kind;
            public DnaParseNode Node;
            public string ModelPath;
        }

        private sealed class DnaParseNode
        {
            public string Id;
            public string ParentId;
            public string Keyword;
            public string DisplayName;
            public Dictionary<string, string> Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class StoreNodeRecord
        {
            public string ModelPath;
            public string NodeName;
        }
    }
}
