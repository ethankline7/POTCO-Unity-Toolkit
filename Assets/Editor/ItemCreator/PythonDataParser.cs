using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Globalization;

namespace POTCO.Editor.ItemCreator
{
    public static class PythonDataParser
    {
        // --------------------------------------------------------------------------------
        // WRITING (Export)
        // --------------------------------------------------------------------------------

        public static string WritePythonData(Dictionary<int, ItemDataRow> itemData, Dictionary<string, int> columnHeadings)
        {
            StringBuilder sb = new StringBuilder();
            
            char sq = (char)39; 
            char bs = (char)92; 

            sb.AppendLine("from panda3d.core import LVector3f");
            sb.AppendLine("itemInfo = {");

            sb.Append("    'columnHeadings': {");
            bool firstHeading = true;
            foreach (var entry in columnHeadings)
            {
                if (!firstHeading) sb.Append(", ");
                sb.Append("u" + sq + entry.Key + sq + ": " + entry.Value);
                firstHeading = false;
            }
            sb.AppendLine("}, ");

            string escapedQuote = bs.ToString() + sq.ToString();

            foreach (var itemEntry in itemData.OrderBy(x => x.Key))
            {
                int itemId = itemEntry.Key;
                List<object> rawData = itemEntry.Value.GetRawData();

                sb.Append("    " + itemId + ": [");
                for (int i = 0; i < rawData.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    object value = rawData[i];

                    if (value is string s)
                    {
                        string escaped = s.Replace(sq.ToString(), escapedQuote);
                        sb.Append("u" + sq + escaped + sq);
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

        // --------------------------------------------------------------------------------
        // PARSING (Import)
        // --------------------------------------------------------------------------------

        public static Tuple<Dictionary<int, ItemDataRow>, Dictionary<string, int>> ParseItemData(string fileContent)
        {
            Dictionary<int, ItemDataRow> itemData = new Dictionary<int, ItemDataRow>();
            Dictionary<string, int> columnHeadings = new Dictionary<string, int>();

            string cleanContent = RemoveComments(fileContent);

            int startIdx = cleanContent.IndexOf("itemInfo");
            if (startIdx == -1) return new Tuple<Dictionary<int, ItemDataRow>, Dictionary<string, int>>(itemData, columnHeadings);

            int openBrace = cleanContent.IndexOf('{', startIdx);
            if (openBrace == -1) return new Tuple<Dictionary<int, ItemDataRow>, Dictionary<string, int>>(itemData, columnHeadings);

            string dictContent = ExtractBlock(cleanContent, openBrace, '{', '}');
            if (dictContent == null) 
            {
                Debug.LogError("Failed to extract itemInfo dictionary block.");
                return new Tuple<Dictionary<int, ItemDataRow>, Dictionary<string, int>>(itemData, columnHeadings);
            }

            ParseDictionaryContent(dictContent, itemData, columnHeadings);

            return new Tuple<Dictionary<int, ItemDataRow>, Dictionary<string, int>>(itemData, columnHeadings);
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

        public static Dictionary<string, object> ParseLocalizer(string fileContent, Dictionary<string, int> constants = null)
        {
            var result = new Dictionary<string, object>();
            string cleanContent = RemoveComments(fileContent);
            
            // Optimization: Only look for specific dictionaries we care about
            string[] targetKeys = new string[] 
            { 
                "ItemNames", 
                "ClothingFlavorText", 
                "TattooStrings", 
                "TattooFlavorText", 
                "JewelryFlavorText",
                "ItemFlavorText"
            };

            foreach (string key in targetKeys)
            {
                // Find "Key =" or "Key="
                int keyIdx = cleanContent.IndexOf(key);
                while (keyIdx != -1)
                {
                    // Check boundary (ensure it's not SomeItemNames)
                    bool boundaryStart = (keyIdx == 0 || !char.IsLetterOrDigit(cleanContent[keyIdx - 1]));
                    bool boundaryEnd = (keyIdx + key.Length < cleanContent.Length && !char.IsLetterOrDigit(cleanContent[keyIdx + key.Length]));
                    
                    if (boundaryStart && boundaryEnd)
                    {
                        int ptr = keyIdx + key.Length;
                        
                        // Consume whitespace and optional assignment
                        while (ptr < cleanContent.Length && char.IsWhiteSpace(cleanContent[ptr])) ptr++;
                        
                        if (ptr < cleanContent.Length && cleanContent[ptr] == '=')
                        {
                            ptr++; // Consume '='
                            while (ptr < cleanContent.Length && char.IsWhiteSpace(cleanContent[ptr])) ptr++;
                            
                            if (ptr < cleanContent.Length && cleanContent[ptr] == '{')
                            {
                                // Found the start of the dictionary!
                                string dictBlock = ExtractBlock(cleanContent, ptr, '{', '}');
                                if (dictBlock != null)
                                {
                                    try 
                                    {
                                        var parsedDict = ParseDictFromBlock(dictBlock, constants);
                                        result[key] = parsedDict;
                                    }
                                    catch (Exception e)
                                    {
                                        Debug.LogError($"Error parsing block for {key}: {e.Message}");
                                    }
                                }
                                break; // Found it, move to next key
                            }
                        }
                    }
                    
                    // If not found valid assignment, search again later in file
                    keyIdx = cleanContent.IndexOf(key, keyIdx + 1);
                }
            }
            
            return result;
        }

        private static bool IsKeyword(string text, int index)
        {
            string[] keywords = { "import ", "from ", "def ", "class ", "if ", "for ", "elif ", "else:", "try:", "except", "return ", "print ", "del " };
            foreach (var kw in keywords)
            {
                if (index + kw.Length <= text.Length)
                {
                    if (string.Compare(text, index, kw, 0, kw.Length) == 0) return true;
                }
            }
            return false;
        }

        private static object ParseBlock(string text, ref int i, Dictionary<string, int> constants = null)
        {
            while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == ',' || text[i] == ':')) i++;
            if (i >= text.Length) return null;

            char c = text[i];

            if (c == '{') 
            {
                string block = ExtractBlock(text, i, '{', '}');
                if (block == null) return null;
                i += block.Length + 2;
                return ParseDictFromBlock(block, constants);
            }
            if (c == '[') 
            {
                string block = ExtractBlock(text, i, '[', ']');
                if (block == null) return null;
                i += block.Length + 2;
                return ParseListFromBlock(block, constants);
            }
            
            if (c == '\'' || c == '"' || (c == 'u' && i + 1 < text.Length && (text[i+1] == '\'' || text[i+1] == '"')))
            {
                string s = ReadToken(text, ref i);
                return CleanString(s);
            }

            string p = ReadToken(text, ref i);
            
            // Check constants
            if (constants != null && constants.TryGetValue(p, out int constVal))
            {
                return constVal;
            }

            return ConvertValue(p);
        }

        private static Dictionary<object, object> ParseDictFromBlock(string block, Dictionary<string, int> constants)
        {
            var dict = new Dictionary<object, object>();
            int i = 0;
            while(i < block.Length)
            {
                while(i < block.Length && (char.IsWhiteSpace(block[i]) || block[i] == ',')) i++;
                if (i >= block.Length) break;

                object key = ParseBlock(block, ref i, constants);
                while(i < block.Length && (char.IsWhiteSpace(block[i]) || block[i] == ':')) i++;
                object val = ParseBlock(block, ref i, constants);

                if (key != null) dict[key] = val;
            }
            return dict;
        }

        private static List<object> ParseListFromBlock(string block, Dictionary<string, int> constants)
        {
            var list = new List<object>();
            int i = 0;
            while(i < block.Length)
            {
                while(i < block.Length && (char.IsWhiteSpace(block[i]) || block[i] == ',')) i++;
                if (i >= block.Length) break;

                object val = ParseBlock(block, ref i, constants);
                list.Add(val);
            }
            return list;
        }

        private static string ReadToken(string text, ref int i)
        {
            if (i >= text.Length) return null;

            char c = text[i];
            char bs = (char)92; 
            char sq = (char)39; 
            char dq = (char)34; 

            if (c == sq || c == dq || (c == 'u' && i+1 < text.Length && (text[i+1] == sq || text[i+1] == dq)))
            {
                int start = i;
                
                if (c == 'u') i++;
                
                char quote = text[i];
                i++; 

                while (i < text.Length)
                {
                    if (text[i] == bs) 
                    {
                        i += 2; 
                        continue;
                    }
                    if (text[i] == quote)
                    {
                        i++; 
                        break;
                    }
                    i++;
                }
                return text.Substring(start, i - start);
            }
            else
            {
                int start = i;
                while (i < text.Length)
                {
                    char curr = text[i];
                    if (curr == ',' || curr == ':' || curr == '}' || curr == ']' || curr == '{' || curr == '[' || curr == '=' || curr == '(' || curr == ')' || curr == '\n' || curr == '\r') break;
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
                char c = text[i];
                
                // Handle Strings to ignore braces inside them
                if (c == '\'' || c == '"')
                {
                    char quote = c;
                    i++;
                    while (i < text.Length)
                    {
                        if (text[i] == '\\') 
                        {
                            i += 2; 
                            continue;
                        }
                        if (text[i] == quote)
                        {
                            // Check if it's a triple quote (common in Python for docstrings)
                            // Simple check: if we are inside a triple quote, we need 3 matches.
                            // For robustness/simplicity here, let's just assume simple strings for dictionary values usually.
                            // BUT Python often uses """ for big blocks. 
                            // Let's stick to simple quote skipping for now to avoid over-complexity, 
                            // as ReadToken also does simple skipping.
                            break;
                        }
                        i++;
                    }
                }
                else if (c == openChar) 
                {
                    depth++;
                }
                else if (c == closeChar)
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
            int len = text.Length;
            int i = 0;
            
            while (i < len)
            {
                char c = text[i];
                
                // Handle strings
                if (c == '\'' || c == '"')
                {
                    char quote = c;
                    sb.Append(c);
                    i++;
                    
                    while (i < len)
                    {
                        char sc = text[i];
                        sb.Append(sc);
                        
                        if (sc == '\\')
                        {
                            i++;
                            if (i < len) sb.Append(text[i]); // Append escaped char
                        }
                        else if (sc == quote)
                        {
                            // Check for triple quote start (simple heuristic, mainly for skipping)
                            // If we want to support triple quotes fully we need more lookahead, 
                            // but for "remove comments" simply consuming to next matching quote is usually safe enough 
                            // unless it's a triple quote string containing the same quote char.
                            // Given ItemData.py structure, simple quote matching is safer than line splitting.
                            break;
                        }
                        i++;
                    }
                }
                else if (c == '#')
                {
                    // Found comment, skip until newline
                    while (i < len && text[i] != '\n' && text[i] != '\r') i++;
                    
                    // If we hit a newline, let the main loop handle it in the next iteration 
                    // so it gets appended. We just want to skip the comment text.
                    // But currently 'i' is pointing at the newline (or end of string).
                    // If we continue, the loop does i++, skipping the newline.
                    
                    // Fix: If we are at a newline, append it here or decrement i so main loop catches it?
                    // Easier: Just append the newline if we found one.
                    if (i < len && (text[i] == '\n' || text[i] == '\r'))
                    {
                        sb.Append(text[i]);
                    }
                }
                else
                {
                    sb.Append(c);
                }
                i++;
            }
            return sb.ToString();
        }

        private static object ConvertValue(string token)
        {
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