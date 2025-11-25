using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Globalization;

namespace POTCO.Editor.ItemCreator
{
    public static class PythonDataParser
    {
        public static string WritePythonData(Dictionary<int, ItemDataRow> itemData, Dictionary<string, int> columnHeadings)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("from panda3d.core import LVector3f");
            sb.AppendLine("itemInfo = {");

            sb.Append("    'columnHeadings': {");
            bool firstHeading = true;
            foreach (var entry in columnHeadings)
            {
                if (!firstHeading) sb.Append(", ");
                sb.Append($"u'{entry.Key}': {entry.Value}");
                firstHeading = false;
            }
            sb.AppendLine("}, ");

            foreach (var itemEntry in itemData.OrderBy(x => x.Key))
            {
                int itemId = itemEntry.Key;
                List<object> rawData = itemEntry.Value.GetRawData();

                sb.Append($"    {itemId}: [");
                for (int i = 0; i < rawData.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    object value = rawData[i];

                    if (value is string s)
                    {
                        string escaped = s.Replace(((char)39).ToString(), "' ");
                        sb.Append($"u'{(escaped)}'");
                    }
                    else if (value is float f)
                    {
                        sb.Append(f.ToString(CultureInfo.InvariantCulture));
                    }
                    else if (value is bool b)
                    {
                        sb.Append(b ? "1" : "0");
                    }
                    else
                    {
                        sb.Append(value.ToString());
                    }
                }
                sb.AppendLine("],");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        public static Tuple<Dictionary<int, ItemDataRow>, Dictionary<string, int>> ParseItemData(string fileContent)
        {
            Dictionary<int, ItemDataRow> itemData = new Dictionary<int, ItemDataRow>();
            Dictionary<string, int> columnHeadings = new Dictionary<string, int>();

            Match itemInfoMatch = Regex.Match(fileContent, @"itemInfo\s*=\s*(\{[\s\S]*?\})\s*(?=\n\w|\Z)", RegexOptions.Multiline);
            if (!itemInfoMatch.Success)
            {
                Debug.LogError("Could not find 'itemInfo' dictionary in ItemData.py content.");
                return new Tuple<Dictionary<int, ItemDataRow>, Dictionary<string, int>>(itemData, columnHeadings);
            }

            string itemInfoString = itemInfoMatch.Groups[1].Value;

            Match columnHeadingsMatch = Regex.Match(itemInfoString, @"'columnHeadings':\s*(\{[\s\S]*?\}),?");
            if (columnHeadingsMatch.Success)
            {
                string columnHeadingsDictString = columnHeadingsMatch.Groups[1].Value;
                columnHeadings = ParsePythonDictString<string, int>(columnHeadingsDictString);
                ItemColumnMapping.SetMapping(columnHeadings);
            }
            else
            {
                Debug.LogError("Could not find 'columnHeadings' in ItemData.py content.");
            }

            itemInfoString = Regex.Replace(itemInfoString, @"'columnHeadings':\s*(\{[\s\S]*?\}),?", "");

            MatchCollection itemMatches = Regex.Matches(itemInfoString, @"(\d+):\s*(\[.*?\]),?", RegexOptions.Multiline);

            foreach (Match match in itemMatches)
            {
                int itemId = int.Parse(match.Groups[1].Value);
                string listString = match.Groups[2].Value;

                List<object> values = ParsePythonListString(listString);
                itemData[itemId] = new ItemDataRow(itemId, values);
            }

            return new Tuple<Dictionary<int, ItemDataRow>, Dictionary<string, int>>(itemData, columnHeadings);
        }

        private static Dictionary<K, V> ParsePythonDictString<K, V>(string dictString)
        {
            Dictionary<K, V> result = new Dictionary<K, V>();
            dictString = dictString.Trim().Trim('{', '}');

            char dq = (char)34;
            char sq = (char)39;
            
            string pattern = "(?:u" + sq + ")?([^" + sq + ":" + dq + "]+)(?:" + sq + "|" + dq + ")?\\s*:\\s*([^,]+)";

            MatchCollection pairs = Regex.Matches(dictString, pattern);

            foreach (Match pair in pairs)
            {
                string keyString = pair.Groups[1].Value.Trim().Trim(sq, dq);
                string valueString = pair.Groups[2].Value.Trim();

                if (keyString.StartsWith("u'")) keyString = keyString.Substring(2, keyString.Length - 2);

                try 
                {
                    K key = (K)Convert.ChangeType(keyString, typeof(K));
                    V value = (V)ConvertLiteral(valueString, typeof(V));
                    result[key] = value;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to parse key-value pair: {keyString} : {valueString}. Error: {e.Message}");
                }
            }
            return result;
        }

        /// <summary>
        /// Parses a Python list string into a C# List<object>.
        /// Handles commas inside string literals correctly.
        /// </summary>
        private static List<object> ParsePythonListString(string listString)
        {
            List<object> result = new List<object>();
            listString = listString.Trim().Trim('[', ']');
            
            if (string.IsNullOrEmpty(listString)) return result;

            List<string> tokens = new List<string>();
            bool inString = false;
            char quoteChar = '\0';
            int startIndex = 0;

            for (int i = 0; i < listString.Length; i++)
            {
                char c = listString[i];
                
                // Check for quote start/end
                // Check if it's a quote and not escaped (checking i>0 for safety)
                if ((c == '\'' || c == '"') && (i == 0 || listString[i - 1] != '\\'))
                {
                    if (inString)
                    {
                        if (c == quoteChar)
                        {
                            inString = false;
                        }
                    }
                    else
                    {
                        inString = true;
                        quoteChar = c;
                    }
                }

                // Check for delimiter
                if (c == ',' && !inString)
                {
                    tokens.Add(listString.Substring(startIndex, i - startIndex));
                    startIndex = i + 1;
                }
            }
            
            // Add the last token
            if (startIndex <= listString.Length)
            {
                tokens.Add(listString.Substring(startIndex));
            }

            foreach (string rawValue in tokens)
            {
                if (string.IsNullOrWhiteSpace(rawValue)) continue;
                result.Add(ConvertLiteral(rawValue.Trim()));
            }
            return result;
        }

        private static object ConvertLiteral(string literal, Type targetType = null)
        {
            literal = literal.Trim();
            char dq = (char)34;
            char sq = (char)39;

            bool isString = targetType == typeof(string) || literal.StartsWith("u'") || literal.StartsWith(sq.ToString()) || literal.StartsWith(dq.ToString());

            if (isString)
            {
                if (literal.StartsWith("u'")) return literal.Substring(2, literal.Length - 3);
                if (literal.StartsWith(sq.ToString()) || literal.StartsWith(dq.ToString())) return literal.Trim(sq, dq);
                return literal;
            }

            if (int.TryParse(literal, out int intVal)) return intVal;
            if (float.TryParse(literal, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatVal)) return floatVal;
            if (bool.TryParse(literal, out bool boolVal)) return boolVal;

            return literal;
        }

        public static Dictionary<string, int> ParseConstants(string fileContent, string className = null)
        {
            Dictionary<string, int> constants = new Dictionary<string, int>();
            string searchArea = fileContent;

            if (className != null)
            {
                // Find the class block
                Match classMatch = Regex.Match(fileContent, $@"(class\s+{className}:[\s\S]*?)(?=\nclass|\Z)");
                if (classMatch.Success)
                {
                    searchArea = classMatch.Groups[1].Value;
                }
                else
                {
                    Debug.LogWarning($"Class '{className}' not found in file content for parsing constants.");
                    return constants;
                }
            }

            // Regex to find lines like CONSTANT = VALUE
            // More permissive pattern to handle comments and whitespace
            MatchCollection matches = Regex.Matches(searchArea, @"^\s*(\w+)\s*=\s*([0-9]+)", RegexOptions.Multiline);

            foreach (Match match in matches)
            {
                string constantName = match.Groups[1].Value.Trim();
                string valueString = match.Groups[2].Value.Trim();
                
                if (int.TryParse(valueString, out int value))
                {
                    constants[constantName] = value;
                }
            }
            return constants;
        }
    }
}