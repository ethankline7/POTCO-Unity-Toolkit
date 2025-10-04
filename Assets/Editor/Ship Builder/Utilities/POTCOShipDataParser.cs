using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace POTCO.ShipBuilder
{
    /// <summary>
    /// Parses POTCO Python ship data files (ShipGlobals.py, ShipBlueprints.py, PLocalizerEnglish.py)
    /// Uses line-by-line parsing similar to EggParser pattern
    /// </summary>
    public class POTCOShipDataParser
    {
        private static readonly char[] ColonSeparator = { ':' };
        private static readonly char[] CommaSeparator = { ',' };
        private static readonly char[] SpaceSeparator = { ' ' };
        private static readonly char[] ParensSeparator = { '(', ')', ',' };

        // Ship configuration data indexed by ship ID
        public Dictionary<int, ShipConfigData> ShipConfigs { get; private set; }

        // Model name mappings (ship ID -> hull model name)
        public Dictionary<int, string> HullModelNames { get; private set; }

        // Style texture mappings (style ID -> texture name) - for hull textures
        public Dictionary<int, string> StyleTextures { get; private set; }

        // Sail texture mappings (style ID -> texture name) - for sail textures
        public Dictionary<int, string> SailTextures { get; private set; }

        // Logo texture mappings (logo ID -> texture name)
        public Dictionary<int, string> LogoTextures { get; private set; }

        // Ship display names (ship ID -> display name)
        public Dictionary<int, string> ShipDisplayNames { get; private set; }

        // Mast configurations (mast ID -> prefix and max height)
        public Dictionary<int, MastTypeData> MastTypes { get; private set; }

        // Ship ID constants (name -> ID)
        public Dictionary<string, int> ShipIDConstants { get; private set; }

        // Style ID constants (name -> ID)
        public Dictionary<string, int> StyleIDConstants { get; private set; }

        // Logo ID constants (name -> ID)
        public Dictionary<string, int> LogoIDConstants { get; private set; }

        // Mast ID constants (name -> ID)
        public Dictionary<string, int> MastIDConstants { get; private set; }

        // Prow ID constants (name -> ID)
        public Dictionary<string, int> ProwIDConstants { get; private set; }

        // Cannon ID constants (name -> ID)
        public Dictionary<string, int> CannonIDConstants { get; private set; }

        // Cannon type data (cannon ID -> model name suffix)
        public Dictionary<int, string> CannonTypes { get; private set; }

        public POTCOShipDataParser()
        {
            ShipConfigs = new Dictionary<int, ShipConfigData>();
            HullModelNames = new Dictionary<int, string>();
            StyleTextures = new Dictionary<int, string>();
            SailTextures = new Dictionary<int, string>();
            LogoTextures = new Dictionary<int, string>();
            ShipDisplayNames = new Dictionary<int, string>();
            MastTypes = new Dictionary<int, MastTypeData>();
            CannonTypes = new Dictionary<int, string>();

            ShipIDConstants = new Dictionary<string, int>();
            StyleIDConstants = new Dictionary<string, int>();
            LogoIDConstants = new Dictionary<string, int>();
            MastIDConstants = new Dictionary<string, int>();
            ProwIDConstants = new Dictionary<string, int>();
            CannonIDConstants = new Dictionary<string, int>();
        }

        public void ParseAllPOTCOData(string potcoSourceFolder)
        {
            string shipGlobalsPath = Path.Combine(potcoSourceFolder, "ShipGlobals.py");
            string shipBlueprintsPath = Path.Combine(potcoSourceFolder, "ShipBlueprints.py");
            string localizerPath = Path.Combine(potcoSourceFolder, "PLocalizerEnglish.py");
            string cannonPath = Path.Combine(potcoSourceFolder, "Cannon.py");

            if (File.Exists(shipGlobalsPath))
            {
                string content = File.ReadAllText(shipGlobalsPath);
                ParseShipGlobals(content);
            }

            if (File.Exists(cannonPath))
            {
                string content = File.ReadAllText(cannonPath);
                ParseCannon(content);
            }
            else
            {
                Debug.LogWarning($"[POTCOShipDataParser] Cannon.py not found at: {cannonPath}");
            }

            if (File.Exists(shipBlueprintsPath))
            {
                string content = File.ReadAllText(shipBlueprintsPath);
                ParseShipBlueprints(content);
            }

            if (File.Exists(localizerPath))
            {
                string content = File.ReadAllText(localizerPath);
                ParseLocalizer(content);
            }

            Debug.Log($"POTCO Ship Data Parsed: {ShipConfigs.Count} ships, {StyleTextures.Count} hull textures, {SailTextures.Count} sail textures, {LogoTextures.Count} logos");
        }

        private void ParseShipGlobals(string content)
        {
            string[] lines = content.Split('\n');

            // Parse ship ID constants (e.g., "QUEEN_ANNES_REVENGE = 55")
            ParseIDConstants(lines, ShipIDConstants, "INTERCEPTORL1", "STUMPY_SHIP");

            // Parse style enum (class Styles:)
            ParseStyleConstants(lines);

            // Parse logo enum (class Logos:)
            ParseLogoConstants(lines);

            // Parse mast enum and configurations
            ParseMastConstants(lines);

            // Parse prow enum
            ParseProwConstants(lines);

            // Parse cannon enum
            ParseCannonConstants(lines);

            // Parse ship configurations (__shipConfigs = {)
            ParseShipConfigurations(lines);
        }

        private void ParseShipBlueprints(string content)
        {
            string[] lines = content.Split('\n');

            // Parse HullDict (ship ID -> model name)
            ParseHullDict(lines);

            // Debug: Check if SKEL_INTERCEPTORL3 (ID 61) was parsed
            if (HullModelNames.TryGetValue(61, out string skelInterceptorModel))
            {
                Debug.Log($"[ParseShipBlueprints] SKEL_INTERCEPTORL3 (61) hull model: {skelInterceptorModel}");
            }
            else
            {
                Debug.LogWarning($"[ParseShipBlueprints] SKEL_INTERCEPTORL3 (61) NOT found in HullModelNames!");
            }

            // Parse shipStyles (style ID -> hull texture name)
            ParseShipStyles(lines);

            // Parse ColorDict (style ID -> sail texture name)
            ParseColorDict(lines);

            // Parse LogoDict (logo ID -> texture name)
            ParseLogoDict(lines);

            // Parse mastDict (mast ID -> prefix and maxHeight)
            ParseMastDict(lines);
        }

        private void ParseCannon(string content)
        {
            string[] lines = content.Split('\n');

            // Parse cannon dictionary (cannon ID -> model suffix)
            ParseCannonDict(lines);

            Debug.Log($"[ParseCannon] Cannon enum has {CannonIDConstants.Count} types, parsed {CannonTypes.Count} cannon models from Cannon.py");
        }

        private void ParseLocalizer(string content)
        {
            string[] lines = content.Split('\n');

            // Parse ShipClassNames dictionary
            ParseShipClassNames(lines);
        }

        private void ParseIDConstants(string[] lines, Dictionary<string, int> target, string startMarker, string endMarker)
        {
            bool inRange = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.Contains(startMarker) && line.Contains("="))
                {
                    inRange = true;
                }

                if (inRange)
                {
                    // Parse lines like "QUEEN_ANNES_REVENGE = 55"
                    if (line.Contains("=") && !line.StartsWith("#") && !line.StartsWith("class"))
                    {
                        var parts = line.Split('=');
                        if (parts.Length >= 2)
                        {
                            string name = parts[0].Trim();
                            string valueStr = parts[1].Trim().Split(new[] { ' ', '#', '\r' })[0];

                            if (int.TryParse(valueStr, out int value))
                            {
                                target[name] = value;
                            }
                        }
                    }

                    if (line.Contains(endMarker))
                    {
                        break;
                    }
                }
            }
        }

        private void ParseStyleConstants(string[] lines)
        {
            bool inStyleClass = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.Contains("class Styles:"))
                {
                    inStyleClass = true;
                    continue;
                }

                if (inStyleClass)
                {
                    // Stop at next class or empty line pattern
                    if (line.StartsWith("class ") || (string.IsNullOrWhiteSpace(line) && i > 0 && string.IsNullOrWhiteSpace(lines[i - 1])))
                    {
                        break;
                    }

                    // Parse lines like "QueenAnnesRevenge = 13"
                    if (line.Contains("=") && !line.StartsWith("#"))
                    {
                        var parts = line.Split('=');
                        if (parts.Length >= 2)
                        {
                            string name = parts[0].Trim();
                            string valueStr = parts[1].Trim().Split(new[] { ' ', '#', '\r' })[0];

                            if (int.TryParse(valueStr, out int value))
                            {
                                StyleIDConstants[name] = value;
                            }
                        }
                    }
                }
            }
        }

        private void ParseLogoConstants(string[] lines)
        {
            bool inLogoClass = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.Contains("class Logos:"))
                {
                    inLogoClass = true;
                    continue;
                }

                if (inLogoClass)
                {
                    if (line.StartsWith("class ") || (string.IsNullOrWhiteSpace(line) && i > 0 && string.IsNullOrWhiteSpace(lines[i - 1])))
                    {
                        break;
                    }

                    if (line.Contains("=") && !line.StartsWith("#"))
                    {
                        var parts = line.Split('=');
                        if (parts.Length >= 2)
                        {
                            string name = parts[0].Trim();
                            string valueStr = parts[1].Trim().Split(new[] { ' ', '#', '\r' })[0];

                            if (int.TryParse(valueStr, out int value))
                            {
                                LogoIDConstants[name] = value;
                            }
                        }
                    }
                }
            }
        }

        private void ParseMastConstants(string[] lines)
        {
            bool inMastClass = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.Contains("class Masts:"))
                {
                    inMastClass = true;
                    continue;
                }

                if (inMastClass)
                {
                    if (line.StartsWith("class ") || (string.IsNullOrWhiteSpace(line) && i > 0 && string.IsNullOrWhiteSpace(lines[i - 1])))
                    {
                        break;
                    }

                    if (line.Contains("=") && !line.StartsWith("#"))
                    {
                        var parts = line.Split('=');
                        if (parts.Length >= 2)
                        {
                            string name = parts[0].Trim();
                            string valueStr = parts[1].Trim().Split(new[] { ' ', '#', '\r' })[0];

                            if (int.TryParse(valueStr, out int value))
                            {
                                MastIDConstants[name] = value;
                            }
                        }
                    }
                }
            }
        }

        private void ParseProwConstants(string[] lines)
        {
            bool inProwClass = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.Contains("class Prows:"))
                {
                    inProwClass = true;
                    continue;
                }

                if (inProwClass)
                {
                    if (line.StartsWith("class ") || (string.IsNullOrWhiteSpace(line) && i > 0 && string.IsNullOrWhiteSpace(lines[i - 1])))
                    {
                        break;
                    }

                    if (line.Contains("=") && !line.StartsWith("#"))
                    {
                        var parts = line.Split('=');
                        if (parts.Length >= 2)
                        {
                            string name = parts[0].Trim();
                            string valueStr = parts[1].Trim().Split(new[] { ' ', '#', '\r' })[0];

                            if (int.TryParse(valueStr, out int value))
                            {
                                ProwIDConstants[name] = value;
                            }
                        }
                    }
                }
            }
        }

        private void ParseCannonConstants(string[] lines)
        {
            bool inCannonClass = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.Contains("class Cannons:"))
                {
                    inCannonClass = true;
                    Debug.Log($"[ParseCannonConstants] Found Cannons class at line {i}");
                    continue;
                }

                if (inCannonClass)
                {
                    if (line.StartsWith("class ") || (string.IsNullOrWhiteSpace(line) && i > 0 && string.IsNullOrWhiteSpace(lines[i - 1])))
                    {
                        Debug.Log($"[ParseCannonConstants] Finished parsing. Total cannons: {CannonIDConstants.Count}");
                        break;
                    }

                    if (line.Contains("=") && !line.StartsWith("#"))
                    {
                        var parts = line.Split('=');
                        if (parts.Length >= 2)
                        {
                            string name = parts[0].Trim();
                            string valueStr = parts[1].Trim().Split(new[] { ' ', '#', '\r' })[0];

                            if (int.TryParse(valueStr, out int value))
                            {
                                CannonIDConstants[name] = value;
                                Debug.Log($"[ParseCannonConstants] {name} = {value}");
                            }
                        }
                    }
                }
            }

            if (!inCannonClass)
            {
                Debug.LogWarning("[ParseCannonConstants] 'class Cannons:' not found in ShipGlobals.py");
            }
        }

        private void ParseShipConfigurations(string[] lines)
        {
            // Find __shipConfigs = { line
            int configStart = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("__shipConfigs") && lines[i].Contains("=") && lines[i].Contains("{"))
                {
                    configStart = i;
                    break;
                }
            }

            if (configStart == -1) return;

            // Parse each ship configuration block
            int currentShipID = -1;
            ShipConfigData currentConfig = null;
            int braceDepth = 0;
            bool inShipConfig = false;

            for (int i = configStart; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                // Track brace depth
                braceDepth += line.Count(c => c == '{');
                braceDepth -= line.Count(c => c == '}');

                // End of __shipConfigs
                if (braceDepth == 0 && i > configStart)
                {
                    if (currentConfig != null)
                    {
                        ShipConfigs[currentShipID] = currentConfig;
                    }
                    break;
                }

                // Start of ship entry (e.g., "QUEEN_ANNES_REVENGE: {")
                if (line.Contains(":") && line.Contains("{") && !line.Contains("'"))
                {
                    // Save previous config
                    if (currentConfig != null)
                    {
                        ShipConfigs[currentShipID] = currentConfig;
                    }

                    // Extract ship name
                    string shipName = line.Split(ColonSeparator)[0].Trim();
                    if (ShipIDConstants.TryGetValue(shipName, out int shipID))
                    {
                        currentShipID = shipID;
                        currentConfig = new ShipConfigData { shipId = shipID };
                        inShipConfig = true;
                    }
                }

                // Parse config properties
                if (inShipConfig && currentConfig != null && line.Contains("'") && line.Contains(":"))
                {
                    // Check if value is on multiple lines (ends with [)
                    string trimmedLine = line.Trim();
                    if (trimmedLine.EndsWith("["))
                    {
                        // Multi-line array - combine with next line
                        string key = trimmedLine.Split(ColonSeparator)[0].Trim();

                        // Read next line for the actual value
                        if (i + 1 < lines.Length)
                        {
                            string nextLine = lines[i + 1].Trim();
                            // Reconstruct the full line with opening bracket
                            line = key + ": [" + nextLine;
                            i++; // Skip next line since we already processed it
                        }
                    }

                    ParseConfigProperty(line, currentConfig);
                }
            }
        }

        private void ParseConfigProperty(string line, ShipConfigData config)
        {
            // Remove comments
            int commentIndex = line.IndexOf('#');
            if (commentIndex >= 0)
            {
                line = line.Substring(0, commentIndex);
            }

            line = line.Trim().TrimEnd(',');

            // Extract key and value
            int colonIndex = line.IndexOf(':');
            if (colonIndex < 0) return;

            string key = line.Substring(0, colonIndex).Trim().Trim('\'');
            string value = line.Substring(colonIndex + 1).Trim();

            switch (key)
            {
                case "modelClass":
                    if (TryParseShipID(value, out int modelClass))
                    {
                        config.modelClass = modelClass;
                        Debug.Log($"  Parsed modelClass: {value} -> {modelClass}");
                    }
                    else
                    {
                        Debug.LogWarning($"  Failed to parse modelClass: {value}");
                    }
                    break;

                case "defaultStyle":
                    if (TryParseStyleID(value, out int styleID))
                        config.defaultStyle = styleID;
                    break;

                case "mastConfig1":
                    config.mastConfig1 = ParseMastConfig(value);
                    break;

                case "mastConfig2":
                    config.mastConfig2 = ParseMastConfig(value);
                    break;

                case "mastConfig3":
                    config.mastConfig3 = ParseMastConfig(value);
                    break;

                case "foremastConfig":
                    config.foremastConfig = ParseMastConfig(value);
                    break;

                case "aftmastConfig":
                    config.aftmastConfig = ParseMastConfig(value);
                    break;

                case "sailLogo":
                    if (TryParseLogoID(value, out int logoID))
                        config.sailLogo = logoID;
                    break;

                case "cannons":
                    config.maxDeckCannons = ParseCannonArrayLength(value);
                    config.cannonType = ParseCannonType(value);
                    break;

                case "leftBroadsides":
                    config.maxBroadsideLeft = ParseCannonArrayLength(value);
                    config.broadsideType = ParseCannonType(value);
                    break;

                case "rightBroadsides":
                    config.maxBroadsideRight = ParseCannonArrayLength(value);
                    // Use same broadside type as left
                    if (config.broadsideType == 0)
                        config.broadsideType = ParseCannonType(value);
                    break;

                case "prow":
                    if (TryParseProwID(value, out int prowID))
                        config.prowType = prowID;
                    break;
            }
        }

        private bool TryParseShipID(string value, out int id)
        {
            value = value.Trim().TrimEnd(',');
            if (int.TryParse(value, out id)) return true;

            // Extract constant name (handle both "INTERCEPTORL3" and "ShipGlobals.INTERCEPTORL3")
            string constantName = value;
            if (value.Contains("."))
            {
                constantName = value.Split('.').Last().Trim();
            }

            // Exact match lookup
            if (ShipIDConstants.TryGetValue(constantName, out id))
            {
                return true;
            }

            id = 0;
            return false;
        }

        private bool TryParseStyleID(string value, out int id)
        {
            value = value.Trim().TrimEnd(',');

            // Extract style name (e.g., "Styles.QueenAnnesRevenge" -> "QueenAnnesRevenge")
            if (value.Contains("Styles."))
            {
                string styleName = value.Split('.').Last().Trim();
                if (StyleIDConstants.TryGetValue(styleName, out id))
                {
                    return true;
                }
            }

            id = 0;
            return false;
        }

        private bool TryParseLogoID(string value, out int id)
        {
            value = value.Trim().TrimEnd(',');

            if (value.Contains("Logos."))
            {
                string logoName = value.Split('.').Last().Trim();
                if (LogoIDConstants.TryGetValue(logoName, out id))
                {
                    return true;
                }
            }

            id = 0;
            return false;
        }

        private bool TryParseProwID(string value, out int id)
        {
            value = value.Trim().TrimEnd(',');

            if (value.Contains("Prows."))
            {
                string prowName = value.Split('.').Last().Trim();
                if (ProwIDConstants.TryGetValue(prowName, out id))
                {
                    return true;
                }
            }

            if (int.TryParse(value, out id)) return true;

            id = 0;
            return false;
        }

        private MastConfig ParseMastConfig(string value)
        {
            // Parse tuples like "(Masts.Main_Square, 3)" or "0"
            value = value.Trim().TrimEnd(',');

            if (value == "0" || value == "None")
            {
                return new MastConfig { mastType = 0, height = 0 };
            }

            // Extract values from parentheses
            int openParen = value.IndexOf('(');
            int closeParen = value.IndexOf(')');
            if (openParen < 0 || closeParen < 0) return new MastConfig();

            string tupleContent = value.Substring(openParen + 1, closeParen - openParen - 1);
            var parts = tupleContent.Split(CommaSeparator, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2) return new MastConfig();

            // Parse mast type
            int mastType = 0;
            string mastTypeStr = parts[0].Trim();
            if (mastTypeStr.Contains("Masts."))
            {
                string mastName = mastTypeStr.Split('.').Last().Trim();
                if (MastIDConstants.TryGetValue(mastName, out int id))
                {
                    mastType = id;
                }
            }

            // Parse height
            int height = 0;
            if (parts.Length >= 2)
            {
                int.TryParse(parts[1].Trim(), out height);
            }

            return new MastConfig { mastType = mastType, height = height };
        }

        private int ParseCannonArrayLength(string value)
        {
            // Parse arrays like "[Cannons.L1] * 10" -> 10
            value = value.Trim().TrimEnd(',');

            int multiplyIndex = value.IndexOf('*');
            if (multiplyIndex >= 0)
            {
                string countStr = value.Substring(multiplyIndex + 1).Trim();
                if (int.TryParse(countStr, out int count))
                {
                    return count;
                }
            }

            return 0;
        }

        private int ParseCannonType(string value)
        {
            // Parse arrays like "[Cannons.L1] * 10" -> extract Cannons.L1 -> L1 ID
            value = value.Trim().TrimEnd(',');

            // Extract content between [ and ]
            int openBracket = value.IndexOf('[');
            int closeBracket = value.IndexOf(']');
            if (openBracket < 0 || closeBracket < 0)
            {
                Debug.LogWarning($"[ParseCannonType] No brackets found in: {value}");
                return 0;
            }

            string arrayContent = value.Substring(openBracket + 1, closeBracket - openBracket - 1).Trim();

            // Extract cannon name (e.g., "Cannons.L1" -> "L1")
            if (arrayContent.Contains("Cannons."))
            {
                string cannonName = arrayContent.Split('.').Last().Trim();
                if (CannonIDConstants.TryGetValue(cannonName, out int cannonID))
                {
                    Debug.Log($"[ParseCannonType] Found cannon '{cannonName}' -> ID {cannonID}");
                    return cannonID;
                }
                else
                {
                    Debug.LogWarning($"[ParseCannonType] Cannon name '{cannonName}' not found in CannonIDConstants (count: {CannonIDConstants.Count})");
                }
            }

            return 0;
        }

        private void ParseHullDict(string[] lines)
        {
            bool inDict = false;

            // Debug: Check if SKEL_INTERCEPTORL3 is in ShipIDConstants
            if (ShipIDConstants.TryGetValue("SKEL_INTERCEPTORL3", out int skelID))
            {
                Debug.Log($"[ParseHullDict] SKEL_INTERCEPTORL3 found in ShipIDConstants with ID {skelID}");
            }
            else
            {
                Debug.LogWarning("[ParseHullDict] SKEL_INTERCEPTORL3 NOT in ShipIDConstants!");
            }

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.Contains("HullDict") && line.Contains("=") && line.Contains("{"))
                {
                    inDict = true;
                    Debug.Log("[ParseHullDict] Found HullDict");
                    continue;
                }

                if (inDict)
                {
                    // Parse lines like "ShipGlobals.QUEEN_ANNES_REVENGE: 'cas_queenAnnesRevenge',"
                    // or "ShipGlobals.SKEL_INTERCEPTORL3: 'skl_sloop' }" (with closing brace)
                    if (line.Contains(":") && line.Contains("'"))
                    {
                        var parts = line.Split(ColonSeparator);
                        if (parts.Length >= 2)
                        {
                            string shipIDStr = parts[0].Trim();
                            string modelName = parts[1].Trim().Trim(',', '\'', ' ', '}');

                            // Extract ship name
                            if (shipIDStr.Contains("ShipGlobals."))
                            {
                                string shipName = shipIDStr.Split('.').Last().Trim();
                                if (ShipIDConstants.TryGetValue(shipName, out int shipID))
                                {
                                    HullModelNames[shipID] = modelName;
                                    if (shipName == "SKEL_INTERCEPTORL3")
                                    {
                                        Debug.Log($"[ParseHullDict] *** FOUND SKEL_INTERCEPTORL3 (ID {shipID}) -> {modelName}");
                                    }
                                }
                                else
                                {
                                    if (shipName == "SKEL_INTERCEPTORL3")
                                    {
                                        Debug.LogWarning($"[ParseHullDict] *** SKEL_INTERCEPTORL3 not found in ShipIDConstants (has {ShipIDConstants.Count} entries)");
                                    }
                                }
                            }
                        }
                    }

                    // Check for end of dictionary AFTER parsing the line
                    if (line.Contains("}"))
                    {
                        Debug.Log($"[ParseHullDict] Parsed {HullModelNames.Count} hull models");
                        break;
                    }
                }
            }
        }

        private void ParseShipStyles(string[] lines)
        {
            bool inDict = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.Contains("shipStyles") && line.Contains("=") && line.Contains("{"))
                {
                    inDict = true;
                    continue;
                }

                if (inDict)
                {
                    if (line.Contains("}") && !line.Contains("{"))
                    {
                        break;
                    }

                    // Parse lines like "ShipGlobals.Styles.QueenAnnesRevenge: 'ships_static_qar_palette_3cmla_1',"
                    if (line.Contains(":") && line.Contains("'"))
                    {
                        var parts = line.Split(ColonSeparator);
                        if (parts.Length >= 2)
                        {
                            string styleIDStr = parts[0].Trim();
                            string textureName = parts[1].Trim().Trim(',', '\'', ' ');

                            if (styleIDStr.Contains("Styles."))
                            {
                                string styleName = styleIDStr.Split('.').Last().Trim();
                                if (StyleIDConstants.TryGetValue(styleName, out int styleID))
                                {
                                    StyleTextures[styleID] = textureName;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ParseColorDict(string[] lines)
        {
            bool inDict = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.Contains("ColorDict") && line.Contains("=") && line.Contains("{"))
                {
                    inDict = true;
                    continue;
                }

                if (inDict)
                {
                    if (line.Contains("}") && !line.Contains("{"))
                    {
                        break;
                    }

                    // Parse lines like "ShipGlobals.Styles.Navy: 'pir_t_shp_clr_navy',"
                    if (line.Contains(":") && line.Contains("'"))
                    {
                        var parts = line.Split(ColonSeparator);
                        if (parts.Length >= 2)
                        {
                            string styleIDStr = parts[0].Trim();
                            string textureName = parts[1].Trim().Trim(',', '\'', ' ');

                            if (styleIDStr.Contains("Styles."))
                            {
                                string styleName = styleIDStr.Split('.').Last().Trim();
                                if (StyleIDConstants.TryGetValue(styleName, out int styleID))
                                {
                                    SailTextures[styleID] = textureName;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ParseLogoDict(string[] lines)
        {
            bool inDict = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.Contains("LogoDict") && line.Contains("=") && line.Contains("{"))
                {
                    inDict = true;
                    continue;
                }

                if (inDict)
                {
                    if (line.Contains("}") && !line.Contains("{"))
                    {
                        break;
                    }

                    // Parse lines like "ShipGlobals.Logos.BlackPearl: 'ship_sailBP_patches',"
                    if (line.Contains(":") && line.Contains("'"))
                    {
                        var parts = line.Split(ColonSeparator);
                        if (parts.Length >= 2)
                        {
                            string logoIDStr = parts[0].Trim();
                            string textureName = parts[1].Trim().Trim(',', '\'', ' ');

                            if (logoIDStr.Contains("Logos."))
                            {
                                string logoName = logoIDStr.Split('.').Last().Trim();
                                if (LogoIDConstants.TryGetValue(logoName, out int logoID))
                                {
                                    LogoTextures[logoID] = textureName;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ParseMastDict(string[] lines)
        {
            bool inDict = false;
            int currentMastID = -1;
            MastTypeData currentMastData = new MastTypeData();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                // Look for both "mastDict" and "MastData" to support different file formats
                if ((line.Contains("mastDict") || line.Contains("MastData")) && line.Contains("=") && line.Contains("{"))
                {
                    inDict = true;
                    Debug.Log($"[ParseMastDict] Found mast data dictionary at line {i}: {line}");
                    continue;
                }

                if (inDict)
                {
                    // New mast entry (e.g., "ShipGlobals.Masts.Main_Square: {")
                    if (line.Contains("Masts.") && line.Contains(":") && line.Contains("{"))
                    {
                        // Save previous mast
                        if (currentMastID >= 0)
                        {
                            MastTypes[currentMastID] = currentMastData;
                            Debug.Log($"[ParseMastDict] Saved mast ID {currentMastID}: prefix='{currentMastData.prefix}', maxHeight={currentMastData.maxHeight}");
                        }

                        string mastName = line.Split(new[] { "Masts." }, StringSplitOptions.None)[1].Split(ColonSeparator)[0].Trim();
                        if (MastIDConstants.TryGetValue(mastName, out int mastID))
                        {
                            currentMastID = mastID;
                            currentMastData = new MastTypeData();
                            Debug.Log($"[ParseMastDict] Started parsing mast '{mastName}' (ID: {mastID})");
                        }
                        else
                        {
                            Debug.LogWarning($"[ParseMastDict] Unknown mast name: {mastName}");
                        }
                    }

                    // Parse properties
                    if (currentMastID >= 0 && line.Contains("'") && line.Contains(":"))
                    {
                        if (line.Contains("'maxHeight':"))
                        {
                            string valueStr = line.Split(ColonSeparator)[1].Trim().Trim(',', ' ');
                            if (int.TryParse(valueStr, out int maxHeight))
                            {
                                currentMastData.maxHeight = maxHeight;
                            }
                        }
                        else if (line.Contains("'prefix':"))
                        {
                            string prefix = line.Split(ColonSeparator)[1].Trim().Trim(',', '\'', ' ', '}');
                            currentMastData.prefix = prefix;
                            Debug.Log($"[ParseMastDict] Mast ID {currentMastID}: prefix = '{prefix}', maxHeight = {currentMastData.maxHeight}");

                            // Check if this is the last entry (has closing braces like "} }")
                            if (line.TrimEnd().EndsWith("} }") || line.TrimEnd().EndsWith("}}"))
                            {
                                // Save the current mast since it's the last one
                                MastTypes[currentMastID] = currentMastData;
                                Debug.Log($"[ParseMastDict] Saved final mast ID {currentMastID}: prefix='{currentMastData.prefix}', maxHeight={currentMastData.maxHeight}");
                                Debug.Log($"[ParseMastDict] Finished parsing. Total masts: {MastTypes.Count}");
                                break;
                            }
                        }
                    }

                    // Check for standalone closing brace
                    if (line.Trim() == "}")
                    {
                        if (currentMastID >= 0)
                        {
                            MastTypes[currentMastID] = currentMastData;
                            Debug.Log($"[ParseMastDict] Saved final mast ID {currentMastID}: prefix='{currentMastData.prefix}', maxHeight={currentMastData.maxHeight}");
                        }
                        Debug.Log($"[ParseMastDict] Finished parsing. Total masts: {MastTypes.Count}");
                        break;
                    }
                }
            }
        }

        private void ParseCannonDict(string[] lines)
        {
            bool inDict = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                // Look for "cannonTypes" dictionary in Cannon.py
                if (line.Contains("cannonTypes") && line.Contains("=") && line.Contains("{"))
                {
                    inDict = true;
                    Debug.Log($"[ParseCannonDict] Found cannonTypes dictionary at line {i}: {line}");
                    continue;
                }

                if (inDict)
                {
                    if (line == "}")
                    {
                        break;
                    }

                    // Parse lines like "ShipGlobals.Cannons.L1: 'plain',"
                    if (line.Contains("Cannons.") && line.Contains(":") && line.Contains("'"))
                    {
                        var parts = line.Split(ColonSeparator);
                        if (parts.Length >= 2)
                        {
                            string cannonIDStr = parts[0].Trim();
                            string modelSuffix = parts[1].Trim().Trim(',', '\'', ' ', '}');

                            // Extract cannon name (e.g., "ShipGlobals.Cannons.L1" -> "L1")
                            if (cannonIDStr.Contains("Cannons."))
                            {
                                string cannonName = cannonIDStr.Split(new[] { "Cannons." }, StringSplitOptions.None)[1].Trim();
                                if (CannonIDConstants.TryGetValue(cannonName, out int cannonID))
                                {
                                    CannonTypes[cannonID] = modelSuffix;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ParseShipClassNames(string[] lines)
        {
            bool inDict = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (line.Contains("ShipClassNames") && line.Contains("=") && line.Contains("{"))
                {
                    inDict = true;
                    continue;
                }

                if (inDict)
                {
                    // This dictionary is often on one long line, so we need to handle it differently
                    // Look for ship names like "ShipGlobals.QUEEN_ANNES_REVENGE: 'Legendary Ship',"
                    if (line.Contains("ShipGlobals.") && line.Contains(":") && line.Contains("'"))
                    {
                        // Split by comma to handle multiple entries on one line
                        var entries = line.Split(CommaSeparator);

                        foreach (var entry in entries)
                        {
                            if (entry.Contains("ShipGlobals.") && entry.Contains(":"))
                            {
                                var parts = entry.Split(ColonSeparator);
                                if (parts.Length >= 2)
                                {
                                    string shipIDStr = parts[0].Trim();
                                    string displayName = parts[1].Trim().Trim(',', '\'', ' ');

                                    string shipName = shipIDStr.Split('.').Last().Trim();
                                    if (ShipIDConstants.TryGetValue(shipName, out int shipID))
                                    {
                                        ShipDisplayNames[shipID] = displayName;
                                    }
                                }
                            }
                        }
                    }

                    if (line.Contains("}"))
                    {
                        break;
                    }
                }
            }
        }
    }

    // Data structures
    [Serializable]
    public class ShipConfigData
    {
        public int shipId;
        public string displayName;
        public int modelClass;
        public int defaultStyle;
        public MastConfig mastConfig1;
        public MastConfig mastConfig2;
        public MastConfig mastConfig3;
        public MastConfig foremastConfig;
        public MastConfig aftmastConfig;
        public int sailLogo;
        public int maxDeckCannons;
        public int maxBroadsideLeft;
        public int maxBroadsideRight;
        public int prowType;

        // Cannon types (can be different per ship)
        public int cannonType; // Cannons enum value
        public int broadsideType; // Cannons enum value for broadsides
    }

    [Serializable]
    public struct MastConfig
    {
        public int mastType;
        public int height;

        public bool IsValid() => mastType > 0 && height > 0;
    }

    [Serializable]
    public class MastTypeData
    {
        public string prefix; // Full model suffix (e.g., "main_square", "main_square_skeletonA", "fore_skeleton")
        public int maxHeight;
    }
}
