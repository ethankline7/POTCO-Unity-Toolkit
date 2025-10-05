/// <summary>
/// Path configuration for POTCO source files.
/// Centralizes path resolution with StreamingAssets fallback and override support.
/// </summary>
using System.IO;
using UnityEngine;

namespace CharacterOG.Data
{
    public static class OgPaths
    {
        private static string s_overrideRoot = null;

        /// <summary>
        /// Override the default Assets/Editor/POTCO_Source path.
        /// Set to null to use default.
        /// </summary>
        public static string OverrideRoot
        {
            get => s_overrideRoot;
            set => s_overrideRoot = value;
        }

        /// <summary>Base path to POTCO_Source folder</summary>
        public static string PiratesRoot
        {
            get
            {
                if (!string.IsNullOrEmpty(s_overrideRoot))
                    return s_overrideRoot;

                // Default to Assets/Editor/POTCO_Source
                return Path.Combine(Application.dataPath, "Editor", "POTCO_Source");
            }
        }

        /// <summary>Path to pirate/BodyDefs.py</summary>
        public static string BodyDefs => Path.Combine(PiratesRoot, "pirate", "BodyDefs.py");

        /// <summary>Path to pirate/HumanDNA.py</summary>
        public static string HumanDNA => Path.Combine(PiratesRoot, "pirate", "HumanDNA.py");

        /// <summary>Path to makeapirate/ClothingGlobals.py</summary>
        public static string ClothingGlobals => Path.Combine(PiratesRoot, "makeapirate", "ClothingGlobals.py");

        /// <summary>Path to makeapirate/PirateMale.py</summary>
        public static string PirateMale => Path.Combine(PiratesRoot, "makeapirate", "PirateMale.py");

        /// <summary>Path to makeapirate/PirateFemale.py</summary>
        public static string PirateFemale => Path.Combine(PiratesRoot, "makeapirate", "PirateFemale.py");

        /// <summary>Path to leveleditor/NPCList.py</summary>
        public static string NPCList => Path.Combine(PiratesRoot, "leveleditor", "NPCList.py");

        /// <summary>Get pirate gender file (Male or Female)</summary>
        public static string GetPirateGenderFile(string gender)
        {
            return gender.ToLower() == "f" ? PirateFemale : PirateMale;
        }

        /// <summary>Validate that all required files exist</summary>
        public static bool ValidatePaths(out string missingFile)
        {
            string[] requiredFiles = {
                BodyDefs,
                HumanDNA,
                ClothingGlobals,
                PirateMale,
                PirateFemale,
                NPCList
            };

            foreach (var file in requiredFiles)
            {
                if (!File.Exists(file))
                {
                    missingFile = file;
                    return false;
                }
            }

            missingFile = null;
            return true;
        }

        /// <summary>Get diagnostic info for debugging</summary>
        public static string GetDiagnosticInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Assets Path: {Application.dataPath}");
            sb.AppendLine($"Default POTCO_Source: {Path.Combine(Application.dataPath, "Editor", "POTCO_Source")}");
            sb.AppendLine($"Override Root: {(s_overrideRoot ?? "None")}");
            sb.AppendLine($"Active Root: {PiratesRoot}");
            sb.AppendLine();
            sb.AppendLine("File Status:");
            sb.AppendLine($"  BodyDefs: {(File.Exists(BodyDefs) ? "✓" : "✗")} {BodyDefs}");
            sb.AppendLine($"  HumanDNA: {(File.Exists(HumanDNA) ? "✓" : "✗")} {HumanDNA}");
            sb.AppendLine($"  ClothingGlobals: {(File.Exists(ClothingGlobals) ? "✓" : "✗")} {ClothingGlobals}");
            sb.AppendLine($"  PirateMale: {(File.Exists(PirateMale) ? "✓" : "✗")} {PirateMale}");
            sb.AppendLine($"  PirateFemale: {(File.Exists(PirateFemale) ? "✓" : "✗")} {PirateFemale}");
            sb.AppendLine($"  NPCList: {(File.Exists(NPCList) ? "✓" : "✗")} {NPCList}");
            return sb.ToString();
        }
    }
}
