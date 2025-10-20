/// <summary>
/// Pure C# Python literal parser.
/// Tokenizes and parses Python data literals only (no code execution).
/// Supports: dicts, lists, tuples, strings, numbers, booleans, None, VBase3/VBase4 calls.
/// </summary>
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace CharacterOG.Data.PureCSharpBackend
{
    public class OgPyReader
    {
        private string source;
        private int pos;
        private int line = 1;
        private int column = 1;
        private string filePath;

        public OgPyReader(string source, string filePath = "<string>")
        {
            this.source = source;
            this.filePath = filePath;
            this.pos = 0;
        }

        /// <summary>Parse file and extract top-level variable assignments</summary>
        public Dictionary<string, PyNode> ParseFile(string path)
        {
            filePath = path;
            source = File.ReadAllText(path);
            pos = 0;
            line = 1;
            column = 1;

            var result = new Dictionary<string, PyNode>();

            while (!IsAtEnd())
            {
                SkipWhitespaceAndComments();
                if (IsAtEnd()) break;

                // Try to parse variable assignment: NAME = VALUE
                if (TryParseAssignment(out string varName, out PyNode value))
                {
                    result[varName] = value;
                }
                else
                {
                    // Skip line if not an assignment
                    SkipLine();
                }
            }

            return result;
        }

        /// <summary>Parse a single Python expression</summary>
        public PyNode ParseExpression()
        {
            SkipWhitespaceAndComments();
            return ParseValue();
        }

        private bool TryParseAssignment(out string varName, out PyNode value)
        {
            varName = null;
            value = null;

            int startPos = pos;
            int startLine = line;
            int startCol = column;

            // Try to read identifier
            if (!TryReadIdentifier(out varName))
            {
                pos = startPos;
                line = startLine;
                column = startCol;
                return false;
            }

            SkipWhitespaceAndComments();

            // Expect '=' but not '=='
            if (Peek() != '=')
            {
                pos = startPos;
                line = startLine;
                column = startCol;
                return false;
            }

            Advance(); // consume '='

            // Check if this is '==' (comparison) instead of '=' (assignment)
            if (Peek() == '=')
            {
                // This is a comparison operator, not an assignment
                pos = startPos;
                line = startLine;
                column = startCol;
                return false;
            }

            SkipWhitespaceAndComments();

            // Parse value
            try
            {
                value = ParseValue();
                return true;
            }
            catch (System.Exception ex)
            {
                // Log parse failures for debugging large structures
                if (varName == "ControlShapes")
                {
                    UnityEngine.Debug.LogError($"[OgPyReader] CRITICAL: Failed to parse {varName} at line {startLine}: {ex.Message}");
                    UnityEngine.Debug.LogError($"[OgPyReader] Exception type: {ex.GetType().Name}");
                    UnityEngine.Debug.LogError($"[OgPyReader] Stack trace: {ex.StackTrace}");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[OgPyReader] Failed to parse {varName} at line {startLine}: {ex.Message}");
                }

                pos = startPos;
                line = startLine;
                column = startCol;
                return false;
            }
        }

        private PyNode ParseValue()
        {
            SkipWhitespaceAndComments();

            char c = Peek();

            if (c == '{') return ParseDict();
            if (c == '[') return ParseList();
            if (c == '(') return ParseTupleOrParen();
            if (c == '\'' || c == '"') return ParseString();

            // Handle unary minus
            if (c == '-')
            {
                int savedPos = pos;
                int savedLine = line;
                int savedCol = column;

                Advance(); // consume '-'
                SkipWhitespaceAndComments();

                char next = Peek();

                if (next == '(')
                {
                    // This is -(expr), parse the expression and negate it
                    var expr = ParseTupleOrParen();

                    // If it's a single number in parens, negate it
                    if (expr is PyNumber num)
                    {
                        return new PyNumber(-num.value);
                    }
                    // If it's a tuple with arithmetic, try to evaluate it
                    else if (expr is PyTuple tuple && tuple.items.Count == 1 && tuple.items[0] is PyNumber tupleNum)
                    {
                        return new PyNumber(-tupleNum.value);
                    }
                    else
                    {
                        // Can't evaluate, return null
                        return PyNull.Instance;
                    }
                }
                else if (char.IsDigit(next))
                {
                    // Negative number, restore and parse as number
                    pos = savedPos;
                    line = savedLine;
                    column = savedCol;
                    return ParseNumber();
                }
                else if (char.IsLetter(next) || next == '_')
                {
                    // This is -variable, which we can't evaluate
                    // Skip the identifier and return null
                    TryReadIdentifier(out _);
                    return PyNull.Instance;
                }
                else
                {
                    // Restore and try as number
                    pos = savedPos;
                    line = savedLine;
                    column = savedCol;
                    return ParseNumber();
                }
            }

            if (char.IsDigit(c)) return ParseNumber();
            if (char.IsLetter(c) || c == '_') return ParseIdentifierOrKeyword();

            throw Error($"Unexpected character: '{c}'");
        }

        private PyDict ParseDict()
        {
            int startLine = line;
            Expect('{');
            var dict = new PyDict();

            SkipWhitespaceAndComments();
            int itemCount = 0;

            while (Peek() != '}')
            {
                // Parse key (string, identifier, or number)
                SkipWhitespaceAndComments();

                // Check for closing brace after trailing comma
                if (Peek() == '}')
                    break;

                string key;

                if (Peek() == '\'' || Peek() == '"')
                {
                    // String key
                    key = (ParseString() as PyString).value;
                }
                else if (char.IsDigit(Peek()) || Peek() == '-')
                {
                    // Numeric key - parse as number and convert to string
                    var numNode = ParseNumber();
                    key = ((int)numNode.value).ToString();
                }
                else
                {
                    // Identifier key
                    if (!TryReadIdentifier(out key))
                        throw Error("Expected dictionary key");
                }

                SkipWhitespaceAndComments();
                Expect(':');
                SkipWhitespaceAndComments();

                var value = ParseValue();
                dict.items[key] = value;
                itemCount++;

                // Log progress for large dictionaries every 100 items
                if (itemCount % 100 == 0)
                {
                    UnityEngine.Debug.Log($"Parsing large dict from line {startLine}: {itemCount} items parsed...");
                }

                SkipWhitespaceAndComments();

                if (Peek() == ',')
                {
                    Advance();
                    SkipWhitespaceAndComments();
                }
            }

            Expect('}');

            if (itemCount > 50)
            {
                UnityEngine.Debug.Log($"Completed parsing large dict from line {startLine}: {itemCount} total items");
            }

            return dict;
        }

        private PyList ParseList()
        {
            Expect('[');
            var list = new PyList();

            SkipWhitespaceAndComments();

            while (Peek() != ']')
            {
                list.items.Add(ParseValue());
                SkipWhitespaceAndComments();

                if (Peek() == ',')
                {
                    Advance();
                    SkipWhitespaceAndComments();
                }
            }

            Expect(']');
            return list;
        }

        private PyNode ParseTupleOrParen()
        {
            Expect('(');
            var items = new List<PyNode>();

            SkipWhitespaceAndComments();

            // Empty tuple
            if (Peek() == ')')
            {
                Advance();
                return new PyTuple();
            }

            items.Add(ParseValue());
            SkipWhitespaceAndComments();

            // Check for arithmetic operators (simple expression evaluation)
            char op = Peek();
            if (op == '+' || op == '-' || op == '*' || op == '/')
            {
                Advance(); // consume operator
                SkipWhitespaceAndComments();

                var right = ParseValue();
                SkipWhitespaceAndComments();

                // Evaluate simple binary arithmetic
                if (items[0] is PyNumber left && right is PyNumber rightNum)
                {
                    double result = op switch
                    {
                        '+' => left.value + rightNum.value,
                        '-' => left.value - rightNum.value,
                        '*' => left.value * rightNum.value,
                        '/' => left.value / rightNum.value,
                        _ => throw Error($"Unknown operator: {op}")
                    };

                    Expect(')');
                    return new PyNumber(result);
                }
                else
                {
                    throw Error($"Cannot evaluate arithmetic expression with non-numeric operands");
                }
            }

            bool isTuple = false;

            while (Peek() == ',')
            {
                isTuple = true;
                Advance();
                SkipWhitespaceAndComments();

                // Trailing comma
                if (Peek() == ')')
                    break;

                items.Add(ParseValue());
                SkipWhitespaceAndComments();
            }

            Expect(')');

            // Single item without comma = parenthesized expression
            if (!isTuple && items.Count == 1)
                return items[0];

            var tuple = new PyTuple();
            tuple.items = items;
            return tuple;
        }

        private PyString ParseString()
        {
            char quote = Peek();
            if (quote != '\'' && quote != '"')
                throw Error("Expected string quote");

            Advance(); // consume opening quote

            StringBuilder sb = new StringBuilder();

            while (Peek() != quote && !IsAtEnd())
            {
                char c = Peek();

                if (c == '\\')
                {
                    Advance();
                    char escaped = Peek();
                    switch (escaped)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case '\\': sb.Append('\\'); break;
                        case '\'': sb.Append('\''); break;
                        case '"': sb.Append('"'); break;
                        default: sb.Append(escaped); break;
                    }
                    Advance();
                }
                else
                {
                    sb.Append(c);
                    Advance();
                }
            }

            Expect(quote);
            return new PyString(sb.ToString());
        }

        private PyNumber ParseNumber()
        {
            StringBuilder sb = new StringBuilder();

            if (Peek() == '-')
            {
                sb.Append(Advance());
            }

            bool hasDigits = false;
            while (char.IsDigit(Peek()) || Peek() == '.')
            {
                hasDigits = true;
                sb.Append(Advance());
            }

            // If we didn't capture any digits, this isn't a valid number
            if (!hasDigits)
            {
                throw Error($"Invalid number: {sb}");
            }

            // Handle scientific notation
            if (Peek() == 'e' || Peek() == 'E')
            {
                sb.Append(Advance());
                if (Peek() == '+' || Peek() == '-')
                    sb.Append(Advance());

                while (char.IsDigit(Peek()))
                    sb.Append(Advance());
            }

            double value;
            if (!double.TryParse(sb.ToString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                // Number is too large or malformed - use max value or 0
                if (sb.ToString().Contains("-"))
                    value = double.MinValue;
                else
                    value = double.MaxValue;

                UnityEngine.Debug.LogWarning($"[OgPyReader] Number overflow at {filePath}:{line}:{column}, using {value}: {sb}");
            }

            // Check for arithmetic operators (division, multiplication, etc.)
            SkipWhitespace();
            char op = Peek();

            if (op == '/' || op == '*' || op == '+')
            {
                Advance(); // consume operator
                SkipWhitespace();

                // Try to parse right side - if it fails, just return the left value
                try
                {
                    PyNumber rightSide = ParseNumber();

                    // Compute result
                    double result = op switch
                    {
                        '/' => value / rightSide.AsFloat(),
                        '*' => value * rightSide.AsFloat(),
                        '+' => value + rightSide.AsFloat(),
                        _ => value
                    };

                    return new PyNumber(result);
                }
                catch
                {
                    // If right side parsing fails, return left value
                    return new PyNumber(value);
                }
            }
            else if (op == '-')
            {
                // Only treat as binary minus if followed by a digit
                int savedPos = pos;
                Advance(); // consume '-'
                SkipWhitespace();

                if (char.IsDigit(Peek()))
                {
                    // This is binary subtraction
                    try
                    {
                        PyNumber rightSide = ParseNumber();
                        return new PyNumber(value - rightSide.AsFloat());
                    }
                    catch
                    {
                        return new PyNumber(value);
                    }
                }
                else
                {
                    // Not binary subtraction, restore position
                    pos = savedPos;
                }
            }

            return new PyNumber(value);
        }

        private PyNode ParseIdentifierOrKeyword()
        {
            if (!TryReadIdentifier(out string ident))
                throw Error("Expected identifier");

            SkipWhitespace();

            PyNode result = null;

            // Check for function call (VBase3, VBase4, etc.)
            if (Peek() == '(')
            {
                result = ParseFunctionCall(ident);
            }
            // Check for array/dict indexing (e.g., PLocalizer.NPCNames['id'])
            else if (Peek() == '[')
            {
                SkipIndexing();
                result = PyNull.Instance;
            }
            // Keywords
            else if (ident == "True")
            {
                result = new PyBool(true);
            }
            else if (ident == "False")
            {
                result = new PyBool(false);
            }
            else if (ident == "None")
            {
                result = PyNull.Instance;
            }
            else
            {
                // Variable reference
                result = new PyVariable(ident);
            }

            // Check for method chaining, operators, or other expression continuations
            SkipWhitespace();
            while (true)
            {
                char c = Peek();

                // Method chaining: obj.method() or obj.property
                if (c == '.')
                {
                    Advance(); // consume '.'
                    SkipWhitespace();

                    // Read the method/property name
                    if (!TryReadIdentifier(out string methodName))
                        throw Error("Expected method or property name after '.'");

                    SkipWhitespace();

                    // Check if it's a method call
                    if (Peek() == '(')
                    {
                        // Parse the method call but return null since we can't evaluate it
                        ParseFunctionCall(methodName);
                        result = PyNull.Instance;
                    }
                    else if (Peek() == '[')
                    {
                        // Handle indexing after method/property
                        SkipIndexing();
                        result = PyNull.Instance;
                    }
                    else
                    {
                        // Just a property access
                        result = PyNull.Instance;
                    }

                    SkipWhitespace();
                }
                // Comparison operators: >=, <=, ==, !=, <, >
                else if (c == '>' || c == '<' || c == '=' || c == '!')
                {
                    // Skip comparison and the rest of the expression
                    return HandleComparisonExpression();
                }
                // Binary operators: +, - (for concatenation or arithmetic)
                // Only handle these when we already have a complex expression (result is PyNull)
                else if ((c == '+' || c == '-') && result == PyNull.Instance)
                {
                    // Skip the rest of this complex expression
                    SkipComplexExpression();
                    return PyNull.Instance;
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        private void SkipComplexExpression()
        {
            // Skip operators and operands until we reach a delimiter
            int depth = 0;
            while (!IsAtEnd())
            {
                char c = Peek();

                // Track nested structures
                if (c == '(' || c == '[' || c == '{')
                    depth++;
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (depth == 0)
                        break; // End of expression
                    depth--;
                }
                else if (depth == 0)
                {
                    // Check for expression delimiters at top level
                    if (c == ',' || c == '\n' || c == ':')
                        break;

                    // Check for 'and', 'or' keywords
                    if (char.IsLetter(c))
                    {
                        int savedPos = pos;
                        int savedLine = line;
                        int savedCol = column;

                        if (TryReadIdentifier(out string keyword))
                        {
                            if (keyword == "and" || keyword == "or")
                            {
                                // Restore position to before the keyword
                                pos = savedPos;
                                line = savedLine;
                                column = savedCol;
                                break;
                            }
                            // Not a keyword, continue from current position
                        }
                        // Continue parsing
                        continue;
                    }
                }

                Advance();
            }
        }

        private PyNode HandleComparisonExpression()
        {
            // Skip comparison operator
            char c = Peek();
            if (c == '>' || c == '<' || c == '=' || c == '!')
            {
                Advance();
                if (Peek() == '=')
                    Advance(); // consume second char of >=, <=, ==, !=
            }

            SkipWhitespace();

            // Skip the right side of the comparison
            SkipComplexExpression();

            return PyNull.Instance;
        }

        private void SkipIndexing()
        {
            // Skip [key] expressions - we can't resolve them without executing Python
            while (Peek() == '[')
            {
                Advance(); // consume '['
                int depth = 1;
                while (depth > 0 && !IsAtEnd())
                {
                    char c = Peek();
                    if (c == '[') depth++;
                    if (c == ']') depth--;
                    Advance();
                }
            }
        }

        private PyFunctionCall ParseFunctionCall(string functionName)
        {
            var call = new PyFunctionCall(functionName);

            Expect('(');
            SkipWhitespaceAndComments();

            while (Peek() != ')')
            {
                // Check for keyword argument (name = value)
                int savedPos = pos;
                int savedLine = line;
                int savedCol = column;

                if (TryReadIdentifier(out string potentialKeyword))
                {
                    SkipWhitespace();
                    if (Peek() == '=')
                    {
                        // This is a keyword argument, skip it
                        Advance(); // consume '='
                        SkipWhitespaceAndComments();

                        // Try to parse the value, but if it fails, skip to next comma or )
                        try
                        {
                            call.args.Add(ParseValue());
                        }
                        catch
                        {
                            SkipToNextArgumentOrEnd();
                        }

                        SkipWhitespaceAndComments();
                        if (Peek() == ',')
                        {
                            Advance();
                            SkipWhitespaceAndComments();
                        }
                        continue;
                    }
                    else
                    {
                        // Not a keyword argument, restore and parse normally
                        pos = savedPos;
                        line = savedLine;
                        column = savedCol;
                    }
                }

                // Normal argument - try to parse value
                try
                {
                    call.args.Add(ParseValue());
                }
                catch
                {
                    // If we can't parse this argument, skip to next comma or end
                    SkipToNextArgumentOrEnd();
                    call.args.Add(PyNull.Instance);
                }

                SkipWhitespaceAndComments();

                if (Peek() == ',')
                {
                    Advance();
                    SkipWhitespaceAndComments();
                }
            }

            Expect(')');
            return call;
        }

        private void SkipToNextArgumentOrEnd()
        {
            int depth = 0;
            while (!IsAtEnd())
            {
                char c = Peek();
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}')
                {
                    if (depth == 0) return;
                    depth--;
                }
                else if (c == ',' && depth == 0) return;

                Advance();
            }
        }

        private bool TryReadIdentifier(out string ident)
        {
            ident = null;
            SkipWhitespace();

            if (!char.IsLetter(Peek()) && Peek() != '_')
                return false;

            StringBuilder sb = new StringBuilder();

            while (char.IsLetterOrDigit(Peek()) || Peek() == '_' || Peek() == '.')
            {
                sb.Append(Advance());
            }

            ident = sb.ToString();
            return true;
        }

        private void SkipWhitespaceAndComments()
        {
            while (!IsAtEnd())
            {
                SkipWhitespace();

                if (Peek() == '#')
                {
                    SkipLine();
                }
                else
                {
                    break;
                }
            }
        }

        private void SkipWhitespace()
        {
            while (char.IsWhiteSpace(Peek()))
                Advance();
        }

        private void SkipLine()
        {
            while (!IsAtEnd() && Peek() != '\n')
                Advance();

            if (Peek() == '\n')
                Advance();
        }

        private char Peek()
        {
            if (IsAtEnd()) return '\0';
            return source[pos];
        }

        private char Advance()
        {
            if (IsAtEnd()) return '\0';

            char c = source[pos++];

            if (c == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }

            return c;
        }

        private void Expect(char expected)
        {
            if (Peek() != expected)
                throw Error($"Expected '{expected}', got '{Peek()}'");
            Advance();
        }

        private bool IsAtEnd()
        {
            return pos >= source.Length;
        }

        private Exception Error(string message)
        {
            return new Exception($"{filePath}:{line}:{column}: {message}\nNear: {GetContextSnippet()}");
        }

        private string GetContextSnippet()
        {
            int start = Math.Max(0, pos - 20);
            int end = Math.Min(source.Length, pos + 20);
            string snippet = source.Substring(start, end - start);
            return snippet.Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
