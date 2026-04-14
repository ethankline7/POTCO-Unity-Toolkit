using System.Globalization;
using System.Text.RegularExpressions;

namespace Toolkit.Editor.WorldData.Adapters.Toontown
{
    public static class ToontownPropertyNormalizer
    {
        private static readonly Regex NumericRegex = new Regex(
            @"^[-+]?(?:\d+\.?\d*|\.\d+)$",
            RegexOptions.Compiled);

        public static string NormalizeForDocument(string raw)
        {
            if (raw == null)
            {
                return string.Empty;
            }

            string trimmed = raw.Trim();
            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            // Keep tuple/list expressions as-is.
            if (trimmed.StartsWith("(") || trimmed.StartsWith("["))
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

            if (IsQuotedStringLiteral(trimmed))
            {
                return UnwrapQuotedStringLiteral(trimmed);
            }

            return trimmed;
        }

        private static bool IsQuotedStringLiteral(string value)
        {
            if (value.Length < 2)
            {
                return false;
            }

            char quote = value[0];
            return (quote == '\'' || quote == '"') && value[value.Length - 1] == quote;
        }

        private static string UnwrapQuotedStringLiteral(string value)
        {
            char quote = value[0];
            string inner = value.Substring(1, value.Length - 2);
            var result = new System.Text.StringBuilder(inner.Length);
            bool escaped = false;

            foreach (char c in inner)
            {
                if (escaped)
                {
                    if (c != quote && c != '\\')
                    {
                        result.Append('\\');
                    }

                    result.Append(c);
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                result.Append(c);
            }

            if (escaped)
            {
                result.Append('\\');
            }

            return result.ToString();
        }
    }
}
