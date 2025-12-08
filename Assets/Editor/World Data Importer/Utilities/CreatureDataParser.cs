using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldDataImporter.Data;
using CharacterOG.Data.PureCSharpBackend;
using POTCO.Editor;

namespace WorldDataImporter.Utilities
{
    /// <summary>
    /// Parser for POTCO creature source files (e.g., Dog.py, Chicken.py).
    /// Extracts ModelInfo and AnimList from Python class definitions.
    /// </summary>
    public static class CreatureDataParser
    {
        private static Dictionary<string, CreatureData> s_cachedCreatures = null;
        private static readonly object s_cacheLock = new object();

        /// <summary>
        /// Get the POTCO_Source/creature directory path
        /// </summary>
        private static string GetCreatureSourcePath()
        {
            string assetsPath = Application.dataPath;
            return Path.Combine(assetsPath, "Editor", "POTCO_Source", "creature");
        }

        /// <summary>
        /// Load all creature data from POTCO_Source/creature folder
        /// </summary>
        public static Dictionary<string, CreatureData> LoadAllCreatures()
        {
            lock (s_cacheLock)
            {
                // Return cached data if available
                if (s_cachedCreatures != null)
                {
                    DebugLogger.LogWorldImporter($"[CreatureDataParser] Using cached creature data ({s_cachedCreatures.Count} species)");
                    return s_cachedCreatures;
                }

                DebugLogger.LogWorldImporter($"[CreatureDataParser] Loading creature data from disk...");
                s_cachedCreatures = new Dictionary<string, CreatureData>();

                string creaturePath = GetCreatureSourcePath();
                if (!Directory.Exists(creaturePath))
                {
                    Debug.LogError($"[CreatureDataParser] Creature source path not found: {creaturePath}");
                    return s_cachedCreatures;
                }

                // Find all .py files in creature folder
                string[] creatureFiles = Directory.GetFiles(creaturePath, "*.py");
                DebugLogger.LogWorldImporter($"[CreatureDataParser] Found {creatureFiles.Length} Python files in {creaturePath}");

                foreach (string filePath in creatureFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);

                    // Skip special files
                    if (fileName.StartsWith("__") || fileName.StartsWith("Distributed") ||
                        fileName == "Animal" || fileName == "Creature" || fileName == "Monstrous")
                    {
                        continue;
                    }

                    try
                    {
                        CreatureData creature = ParseCreatureFile(filePath, fileName);
                        if (creature != null)
                        {
                            s_cachedCreatures[fileName] = creature;
                            DebugLogger.LogWorldImporter($"✅ Parsed creature: {fileName} → {creature.GetBestModelPath()}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[CreatureDataParser] Failed to parse {fileName}: {ex.Message}");
                    }
                }

                DebugLogger.LogWorldImporter($"[CreatureDataParser] Successfully loaded {s_cachedCreatures.Count} creatures");
                return s_cachedCreatures;
            }
        }

        /// <summary>
        /// Parse a single creature Python file
        /// </summary>
        private static CreatureData ParseCreatureFile(string filePath, string species)
        {
            var reader = new OgPyReader("", filePath);
            var data = reader.ParseFile(filePath);

            CreatureData creature = new CreatureData(species);

            // Extract ModelInfo tuple: ('models/char/chicken_hi', 'models/char/chicken_')
            if (data.TryGetValue("ModelInfo", out var modelInfoNode) && modelInfoNode is PyTuple modelInfoTuple)
            {
                if (modelInfoTuple.items.Count >= 1 && modelInfoTuple.items[0] is PyString hiModel)
                {
                    creature.modelPathHi = hiModel.value;
                }
                if (modelInfoTuple.items.Count >= 2 && modelInfoTuple.items[1] is PyString loModel)
                {
                    creature.modelPathLo = loModel.value;
                }
            }

            // Extract AnimList: (('idle', 'idle'), ('walk', 'walk'), ...)
            if (data.TryGetValue("AnimList", out var animListNode) && animListNode is PyTuple animListTuple)
            {
                foreach (var item in animListTuple.items)
                {
                    if (item is PyTuple animPair && animPair.items.Count >= 2)
                    {
                        if (animPair.items[0] is PyString animName && animPair.items[1] is PyString animFile)
                        {
                            creature.animations[animName.value] = animFile.value;
                        }
                    }
                }
            }

            // Parse animation state sequences from setupAnimInfoState calls
            ParseAnimationStates(filePath, creature);

            // Only return creature if we found model info
            if (string.IsNullOrEmpty(creature.modelPathHi) && string.IsNullOrEmpty(creature.modelPathLo))
            {
                return null;
            }

            return creature;
        }

        /// <summary>
        /// Parse setupAnimInfoState calls to extract animation sequences for each state
        /// Example: cls.setupAnimInfoState('LandRoam', (('idle', 1.0), ('walk', 1.0), ...))
        /// </summary>
        private static void ParseAnimationStates(string filePath, CreatureData creature)
        {
            try
            {
                string content = File.ReadAllText(filePath);

                // Regex to match setupAnimInfoState calls
                // Pattern: setupAnimInfoState('StateName', (('anim1', rate1), ('anim2', rate2), ...))
                var regex = new System.Text.RegularExpressions.Regex(
                    @"setupAnimInfoState\s*\(\s*'([^']+)'\s*,\s*\(((?:\s*\([^)]+\)\s*,?\s*)+)\)",
                    System.Text.RegularExpressions.RegexOptions.Singleline
                );

                var matches = regex.Matches(content);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    string stateName = match.Groups[1].Value; // e.g., "LandRoam"
                    string animSequence = match.Groups[2].Value; // e.g., "('idle', 1.0), ('walk', 1.0), ..."

                    // Parse individual animation tuples
                    var animTupleRegex = new System.Text.RegularExpressions.Regex(@"\('([^']+)'\s*,\s*([-\d.]+)\)");
                    var animMatches = animTupleRegex.Matches(animSequence);

                    List<(string animName, float playRate)> animList = new List<(string, float)>();
                    foreach (System.Text.RegularExpressions.Match animMatch in animMatches)
                    {
                        string animName = animMatch.Groups[1].Value;
                        float playRate = float.Parse(animMatch.Groups[2].Value);
                        animList.Add((animName, playRate));
                    }

                    if (animList.Count > 0)
                    {
                        creature.animStates[stateName] = animList;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CreatureDataParser] Failed to parse animation states for {creature.species}: {ex.Message}");
            }
        }

        /// <summary>
        /// Get creature data by species name
        /// </summary>
        public static CreatureData GetCreatureData(string species)
        {
            var creatures = LoadAllCreatures();
            if (creatures.TryGetValue(species, out CreatureData creature))
            {
                return creature;
            }
            return null;
        }

        /// <summary>
        /// Clear the cache (useful for editor refresh)
        /// </summary>
        public static void ClearCache()
        {
            lock (s_cacheLock)
            {
                s_cachedCreatures = null;
                DebugLogger.LogWorldImporter("[CreatureDataParser] Cache cleared");
            }
        }

        /// <summary>
        /// Get list of all available species
        /// </summary>
        public static List<string> GetAllSpecies()
        {
            var creatures = LoadAllCreatures();
            return creatures.Keys.ToList();
        }
    }
}
