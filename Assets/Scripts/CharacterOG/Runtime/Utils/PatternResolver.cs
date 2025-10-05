/// <summary>
/// Resolves POTCO patterns (e.g., "**/clothing_layer1_shirt_*") to exact mesh group names.
/// Compiles anchored regex from OG patterns and matches against actual mesh names.
/// </summary>
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace CharacterOG.Runtime.Utils
{
    public static class PatternResolver
    {
        /// <summary>
        /// Compile POTCO pattern to anchored regex.
        /// Strips **/ prefix, escapes regex, turns * into .*, anchors with ^...$
        /// </summary>
        public static Regex CompileAnchored(string ogPattern)
        {
            // Drop panda path glob prefix
            var p = ogPattern.Replace("**/", "");

            // Escape regex special chars, then replace \* with .*
            p = Regex.Escape(p).Replace(@"\*", ".*");

            // Drop suffix directives like ';+s'
            var semi = p.IndexOf(@"\;");
            if (semi >= 0)
                p = p.Substring(0, semi);

            // Anchor with ^...$
            return new Regex("^" + p + "$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// Resolve POTCO patterns to exact mesh group names.
        /// Returns list of exact names that match any of the input patterns.
        /// </summary>
        public static List<string> ResolveToExact(GroupRendererCache cache, IEnumerable<string> patterns)
        {
            var names = cache.AllNames().ToList();
            var outSet = new HashSet<string>();

            foreach (var pat in patterns)
            {
                var rx = CompileAnchored(pat);
                foreach (var n in names)
                {
                    if (rx.IsMatch(n))
                        outSet.Add(n);
                }
            }

            return outSet.ToList();
        }

        /// <summary>
        /// Resolve single pattern to exact mesh group names.
        /// </summary>
        public static List<string> ResolveToExact(GroupRendererCache cache, string pattern)
        {
            return ResolveToExact(cache, new[] { pattern });
        }
    }
}
