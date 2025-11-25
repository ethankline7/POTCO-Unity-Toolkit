using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace POTCO.Editor.ItemCreator
{
    public static class PythonDataParser
    {
        // --------------------------------------------------------------------------------
        // WRITING (Export)
        // --------------------------------------------------------------------------------

        public static string WriteSingleItemData(ItemDataRow item)
        {
            StringBuilder sb = new StringBuilder();
            char sq = (char)39; 
            char bs = (char)92; 
            string escapedQuote = bs.ToString() + sq.ToString();

            List<object> listData = item.GetRawData();

            sb.Append("[");
            for (int i = 0; i < listData.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                object val = listData[i];

                if (val is string s)
                {
                    string escaped = s.Replace(bs.ToString(), bs.ToString() + bs.ToString())
                                      .Replace(sq.ToString(), bs.ToString() + sq.ToString());
                    sb.Append("u" + sq + escaped + sq);
                }
                else if (val is bool b)
                {
                    sb.Append(b ? "1" : "0");
                }
                else if (val is float f)
                {
                    sb.Append(f.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    sb.Append(val != null ? val.ToString() : "0");
                }
            }
            sb.Append("]");
            return sb.ToString();
        }

        public static string WritePythonData(Dictionary<int, ItemDataRow> itemData, Dictionary<string, int> columnHeadings)
        {
            StringBuilder sb = new StringBuilder();
            
            char sq = (char)39; 
            char bs = (char)92; 

            sb.AppendLine("from panda3d.core import LVector3f");
            sb.AppendLine("itemInfo = {");

            sb.Append("    'columnHeadings': {");
            bool first = true;
            foreach (var kvp in columnHeadings)
            {
                if (!first) sb.Append(", ");
                sb.Append("u" + sq + kvp.Key + sq + ": " + kvp.Value);
                first = false;
            }
            sb.AppendLine("}, ");

            var sortedKeys = itemData.Keys.ToList();
            sortedKeys.Sort();

            foreach (var id in sortedKeys)
            {
                ItemDataRow row = itemData[id];
                List<object> listData = row.GetRawData();

                sb.Append("    " + id + ": [");
                
                for (int i = 0; i < listData.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    object val = listData[i];

                    if (val is string s)
                    {
                        string escaped = s.Replace(bs.ToString(), bs.ToString() + bs.ToString())
                                          .Replace(sq.ToString(), bs.ToString() + sq.ToString());
                        
                        sb.Append("u" + sq + escaped + sq);
                    }
                    else if (val is bool b)
                    {
                        sb.Append(b ? "1" : "0");
                    }
                    else if (val is float f)
                    {
                        sb.Append(f.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(val != null ? val.ToString() : "0");
                    }
                }
                sb.AppendLine("],");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        // --------------------------------------------------------------------------------
        // PARSING (Import)
        // --------------------------------------------------------------------------------

        public static Tuple<Dictionary<int, ItemDataRow>, Dictionary<string, int>> ParseItemData(string fileContent)
        {
            var items = new Dictionary<int, ItemDataRow>();
            var headings = new Dictionary<string, int>();

            string cleanContent = RemoveComments(fileContent);

            int startIdx = cleanContent.IndexOf("itemInfo");
            if (startIdx == -1) return new Tuple<Dictionary<int, ItemDataRow>, Dictionary<string, int>>(items, headings);

            int openBrace = cleanContent.IndexOf('{', startIdx);
            if (openBrace == -1) return new Tuple<Dictionary<int, ItemDataRow>, Dictionary<string, int>>(items, headings);

            string dictContent = ExtractBlock(cleanContent, openBrace, '{', '}');
            if (dictContent == null) 
            {
                Debug.LogError("Failed to extract itemInfo dictionary block.");
                return new Tuple<Dictionary<int, ItemDataRow>, Dictionary<string, int>>(items, headings);
            }

            ParseDictionaryContent(dictContent, items, headings);

            return new Tuple<Dictionary<int, ItemDataRow>, Dictionary<string, int>>(items, headings);
        }

        private static void ParseDictionaryContent(string content, Dictionary<int, ItemDataRow> items, Dictionary<string, int> headings)
        {
            int i = 0;
            int len = content.Length;

            while (i < len)
            {
                char c = content[i];
                if (char.IsWhiteSpace(c) || c == ',') 
                {
                    i++;
                    continue;
                }

                string key = ReadToken(content, ref i);
                if (key == null) break;

                while (i < len && (char.IsWhiteSpace(content[i]) || content[i] == ':')) i++;

                if (i >= len) break;

                if (content[i] == '{')
                {
                    string dictBlock = ExtractBlock(content, i, '{', '}');
                    if (dictBlock == null) 
                    {
                        i++; 
                        continue;
                    }
                    
                    i += dictBlock.Length + 2;

                    if (key == "columnHeadings" || key == "'columnHeadings'" || key == "u'columnHeadings'")
                    {
                        ParseColumnHeadings(dictBlock, headings);
                    }
                }
                else if (content[i] == '[')
                {
                    string listBlock = ExtractBlock(content, i, '[', ']');
                    if (listBlock == null)
                    {
                        i++;
                        continue;
                    }

                    i += listBlock.Length + 2;

                    if (int.TryParse(key, out int id))
                    {
                        List<object> rowData = ParseDataList(listBlock);
                        items[id] = new ItemDataRow(id, rowData);
                    }
                }
                else
                {
                    while (i < len && content[i] != ',') i++;
                }
            }
        }

        private static void ParseColumnHeadings(string content, Dictionary<string, int> headings)
        {
            int i = 0;
            int len = content.Length;
            while (i < len)
            {
                if (char.IsWhiteSpace(content[i]) || content[i] == ',') { i++; continue; }

                string key = ReadToken(content, ref i);
                if (key == null) break;

                key = CleanString(key);

                while (i < len && (char.IsWhiteSpace(content[i]) || content[i] == ':')) i++;

                string valStr = ReadToken(content, ref i);
                if (int.TryParse(valStr, out int val))
                {
                    headings[key] = val;
                }
            }
            ItemColumnMapping.SetMapping(headings);
        }

        private static List<object> ParseDataList(string content)
        {
            List<object> list = new List<object>();
            int i = 0;
            int len = content.Length;

            while (i < len)
            {
                if (char.IsWhiteSpace(content[i]) || content[i] == ',') { i++; continue; }

                string token = ReadToken(content, ref i);
                if (string.IsNullOrEmpty(token)) break;

                list.Add(ConvertValue(token));
            }
            return list;
        }

        private static string ReadToken(string text, ref int i)
        {
            if (i >= text.Length) return null;

            char c = text[i];
            char bs = (char)92; // Backslash 
            char sq = (char)39; // Single Quote '
            char dq = (char)34; // Double Quote "

            // String start
            if (c == sq || c == dq || (c == 'u' && i+1 < text.Length && (text[i+1] == sq || text[i+1] == dq)))
            {
                int start = i;
                
                if (c == 'u') i++;
                
                char quote = text[i];
                i++; // skip open quote

                while (i < text.Length)
                {
                    if (text[i] == bs) 
                    {
                        i += 2; // skip escape sequence
                        continue;
                    }
                    if (text[i] == quote)
                    {
                        i++; // skip close quote
                        break;
                    }
                    i++;
                }
                return text.Substring(start, i - start);
            }
            else
            {
                // Number or simple literal
                int start = i;
                while (i < text.Length)
                {
                    char curr = text[i];
                    if (curr == ',' || curr == ':' || curr == '}' || curr == ']') break;
                    i++;
                }
                return text.Substring(start, i - start).Trim();
            }
        }

        private static string ExtractBlock(string text, int startIdx, char openChar, char closeChar)
        {
            int depth = 0;
            int i = startIdx;
            if (i >= text.Length || text[i] != openChar) return null;

            depth++;
            i++;
            int contentStart = i;

            while (i < text.Length)
            {
                if (text[i] == openChar) depth++;
                else if (text[i] == closeChar)
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text.Substring(contentStart, i - contentStart);
                    }
                }
                i++;
            }
            return null;
        }

        private static string RemoveComments(string text)
        {
            StringBuilder sb = new StringBuilder();
            string[] lines = text.Split(new char[] { '\n', '\r' }, StringSplitOptions.None);
            
            foreach(var line in lines)
            {
                int commentIdx = line.IndexOf('#');
                if (commentIdx == -1)
                {
                    sb.AppendLine(line);
                }
                else
                {
                    sb.AppendLine(line.Substring(0, commentIdx));
                }
            }
            return sb.ToString();
        }

        private static object ConvertValue(string token)
        {
            // Is it a string?
            if (token.StartsWith("u'" ) || token.StartsWith("u\"") || token.StartsWith("'" ) || token.StartsWith("\""))
            {
                return CleanString(token);
            }

            if (int.TryParse(token, out int iVal)) return iVal;
            if (float.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out float fVal)) return fVal;

            if (token == "True") return true;
            if (token == "False") return false;

            return token; 
        }

        private static string CleanString(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            int start = 0;
            int len = raw.Length;

            char sq = (char)39; 
            char dq = (char)34; 

            if (raw.StartsWith("u")) start = 1;
            
            if (start < len && (raw[start] == sq || raw[start] == dq))
            {
                char quote = raw[start];
                start++;
                if (raw.EndsWith(quote.ToString())) len--;
            }

            if (len <= start) return "";

            string content = raw.Substring(start, len - start);
            
            char bs = (char)92;
            string escapedBs = bs.ToString() + bs.ToString(); 
            string escapedSq = bs.ToString() + "'" ;
            string escapedDq = bs.ToString() + "\""; 

            content = content.Replace(escapedSq, "'");
            content = content.Replace(escapedDq, "\"");
            content = content.Replace(escapedBs, bs.ToString());
            
            return content;
        }

        public static Dictionary<string, int> ParseConstants(string fileContent, string className = null)
        {
            var result = new Dictionary<string, int>();
            string[] lines = fileContent.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach(var line in lines)
            {
                string t = line.Trim();
                if (t.StartsWith("#") || string.IsNullOrEmpty(t)) continue;

                int eq = t.IndexOf('=');
                if (eq > 0)
                {
                    string key = t.Substring(0, eq).Trim();
                    string valPart = t.Substring(eq + 1).Trim();
                    
                    int c = valPart.IndexOf('#');
                    if (c != -1) valPart = valPart.Substring(0, c).Trim();

                    if (int.TryParse(valPart, out int v))
                    {
                        result[key] = v;
                    }
                }
            }
            return result;
        }
    }
}
