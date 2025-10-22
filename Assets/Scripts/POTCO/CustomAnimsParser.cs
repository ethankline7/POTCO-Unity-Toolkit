using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;

namespace POTCO
{
    /// <summary>
    /// Parses CustomAnims.py and provides AnimSet lookup
    /// </summary>
    public static class CustomAnimsParser
    {
        private static Dictionary<string, CustomAnimData> animSetDatabase;
        private static bool isInitialized = false;

        // Regex patterns for parsing
        private static readonly Regex animSetKeyRegex = new Regex(@"^\s*'([^']+)':\s*{");
        private static readonly Regex propertyRegex = new Regex(@"^\s*'(\w+)':\s*\[");
        private static readonly Regex stringValueRegex = new Regex(@"'([^']+)'");
        private static readonly Regex closeBracketRegex = new Regex(@"^\s*\]");
        private static readonly Regex closeBraceRegex = new Regex(@"^\s*}");

        /// <summary>
        /// Parse CustomAnims.py and build the database
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized) return;

            animSetDatabase = new Dictionary<string, CustomAnimData>();

            string filePath = Path.Combine(Application.dataPath, "Editor/POTCO_Source/leveleditor/CustomAnims.py");
            if (!File.Exists(filePath))
            {
                Debug.LogError($"❌ CustomAnims.py not found at: {filePath}");
                return;
            }

            try
            {
                ParseCustomAnimsFile(filePath);
                isInitialized = true;
                Debug.Log($"✅ CustomAnims parsed successfully! Loaded {animSetDatabase.Count} AnimSets");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Failed to parse CustomAnims.py: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void ParseCustomAnimsFile(string filePath)
        {
            string[] lines = File.ReadAllLines(filePath);

            CustomAnimData currentAnimSet = null;
            string currentProperty = null;
            bool inInteractAnims = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                // Check for INTERACT_ANIMS start
                if (line.Contains("INTERACT_ANIMS = {"))
                {
                    inInteractAnims = true;
                    continue;
                }

                // Check for INTERACT_ANIMS end
                if (inInteractAnims && line.StartsWith("}"))
                {
                    // Save last animset
                    if (currentAnimSet != null)
                    {
                        animSetDatabase[currentAnimSet.animSetName] = currentAnimSet;
                    }
                    break;
                }

                if (!inInteractAnims) continue;

                // Check for AnimSet key (e.g., 'bar_wipe': {)
                Match animSetMatch = animSetKeyRegex.Match(line);
                if (animSetMatch.Success)
                {
                    // Save previous animset
                    if (currentAnimSet != null)
                    {
                        animSetDatabase[currentAnimSet.animSetName] = currentAnimSet;
                    }

                    currentAnimSet = new CustomAnimData();
                    currentAnimSet.animSetName = animSetMatch.Groups[1].Value;
                    currentProperty = null;
                    continue;
                }

                // Check for property start (e.g., 'idles': [)
                Match propertyMatch = propertyRegex.Match(line);
                if (propertyMatch.Success)
                {
                    currentProperty = propertyMatch.Groups[1].Value;

                    // Check if entire array is on one line
                    if (line.Contains("]"))
                    {
                        ParsePropertyLine(currentAnimSet, currentProperty, line);
                        currentProperty = null;
                    }
                    continue;
                }

                // Parse property values (animation names)
                if (currentProperty != null && currentAnimSet != null)
                {
                    // Check for closing bracket
                    if (closeBracketRegex.IsMatch(line))
                    {
                        currentProperty = null;
                        continue;
                    }

                    // Extract string values
                    MatchCollection matches = stringValueRegex.Matches(line);
                    foreach (Match match in matches)
                    {
                        string value = match.Groups[1].Value;
                        AddValueToProperty(currentAnimSet, currentProperty, value);
                    }
                }
            }
        }

        private static void ParsePropertyLine(CustomAnimData animSet, string property, string line)
        {
            // Extract all string values from the line
            MatchCollection matches = stringValueRegex.Matches(line);
            foreach (Match match in matches)
            {
                string value = match.Groups[1].Value;
                AddValueToProperty(animSet, property, value);
            }
        }

        private static void AddValueToProperty(CustomAnimData animSet, string property, string value)
        {
            switch (property)
            {
                case "idles":
                    animSet.idles.Add(value);
                    break;
                case "interactInto":
                    animSet.interactInto.Add(value);
                    break;
                case "interact":
                    animSet.interact.Add(value);
                    break;
                case "interactOutof":
                    animSet.interactOutof.Add(value);
                    break;
                case "props":
                    // Props are model paths
                    if (value.StartsWith("models/"))
                    {
                        animSet.props.Add(new PropData(value, 0)); // Default to DYNAMIC
                    }
                    break;
            }
        }

        /// <summary>
        /// Get AnimSet data by name (e.g., "bar_wipe")
        /// </summary>
        public static CustomAnimData GetAnimSet(string animSetName)
        {
            if (!isInitialized)
            {
                Initialize();
            }

            if (animSetDatabase != null && animSetDatabase.ContainsKey(animSetName))
            {
                return animSetDatabase[animSetName];
            }

            return null;
        }

        /// <summary>
        /// Check if an AnimSet exists
        /// </summary>
        public static bool HasAnimSet(string animSetName)
        {
            if (!isInitialized)
            {
                Initialize();
            }

            return animSetDatabase != null && animSetDatabase.ContainsKey(animSetName);
        }

        /// <summary>
        /// Get all AnimSet names
        /// </summary>
        public static List<string> GetAllAnimSetNames()
        {
            if (!isInitialized)
            {
                Initialize();
            }

            if (animSetDatabase == null) return new List<string>();
            return new List<string>(animSetDatabase.Keys);
        }

        /// <summary>
        /// Debug: Print all loaded AnimSets
        /// </summary>
        public static void DebugPrintAllAnimSets()
        {
            if (!isInitialized)
            {
                Initialize();
            }

            Debug.Log($"========== CustomAnims Database ({animSetDatabase.Count} entries) ==========");
            foreach (var kvp in animSetDatabase)
            {
                Debug.Log($"AnimSet: {kvp.Key}");
                Debug.Log($"  Idles: {string.Join(", ", kvp.Value.idles)}");
                Debug.Log($"  InteractInto: {string.Join(", ", kvp.Value.interactInto)}");
                Debug.Log($"  Interact: {string.Join(", ", kvp.Value.interact)}");
                Debug.Log($"  InteractOutof: {string.Join(", ", kvp.Value.interactOutof)}");
                Debug.Log($"  Props: {kvp.Value.props.Count} prop(s)");
            }
        }
    }
}
