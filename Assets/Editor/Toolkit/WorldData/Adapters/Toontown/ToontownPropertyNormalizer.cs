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

            return trimmed;
        }
    }
}
