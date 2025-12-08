using System.Globalization;

namespace POTCO.Editor.ItemCreator.Utilities
{
    public static class StringExtensions
    {
        public static string ToTitleCase(this string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            // Use TextInfo.ToTitleCase for proper title casing
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str.ToLower());
        }
    }
}
