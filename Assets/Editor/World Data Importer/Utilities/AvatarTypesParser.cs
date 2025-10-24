using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using WorldDataImporter.Data;
using POTCO.Editor;

namespace WorldDataImporter.Utilities
{
    /// <summary>
    /// Parser for POTCO AvatarTypes.py - extracts enemy type definitions dynamically
    /// </summary>
    public static class AvatarTypesParser
    {
        private static Dictionary<string, AvatarTypeInfo> s_cachedTypes = null;
        private static readonly object s_cacheLock = new object();

        public class AvatarTypeInfo
        {
            public string name;
            public string faction;       // Undead, Navy, Creature, etc.
            public string track;         // LandCreature, AirCreature, Earth, Soldier, etc.
            public bool isCreature;
            public bool isHuman;
            public bool isBoss;

            public AvatarTypeInfo(string name)
            {
                this.name = name;
                this.faction = "";
                this.track = "";
                this.isCreature = false;
                this.isHuman = false;
                this.isBoss = false;
            }
        }

        /// <summary>
        /// Get avatar type info by name
        /// </summary>
        public static AvatarTypeInfo GetAvatarType(string name)
        {
            var types = LoadAllTypes();
            if (types.TryGetValue(name, out AvatarTypeInfo info))
            {
                return info;
            }
            return null;
        }

        /// <summary>
        /// Load all avatar types from AvatarTypes.py
        /// </summary>
        public static Dictionary<string, AvatarTypeInfo> LoadAllTypes()
        {
            lock (s_cacheLock)
            {
                if (s_cachedTypes != null)
                {
                    return s_cachedTypes;
                }

                DebugLogger.LogWorldImporter($"[AvatarTypesParser] Parsing AvatarTypes.py...");
                s_cachedTypes = new Dictionary<string, AvatarTypeInfo>();

                string avatarTypesPath = Path.Combine(Application.dataPath, "Editor", "POTCO_Source", "pirate", "AvatarTypes.py");

                if (!File.Exists(avatarTypesPath))
                {
                    Debug.LogError($"[AvatarTypesParser] AvatarTypes.py not found at: {avatarTypesPath}");
                    return s_cachedTypes;
                }

                string content = File.ReadAllText(avatarTypesPath);
                string[] lines = content.Split('\n');

                // Parse factions (line 14)
                ParseFactions(lines);

                // Parse creature tracks (line 17)
                ParseCreatureTracks(lines);

                // Parse undead tracks (line 78)
                ParseUndeadTracks(lines);

                // Parse navy tracks (line 188)
                ParseNavyTracks(lines);

                DebugLogger.LogWorldImporter($"[AvatarTypesParser] Parsed {s_cachedTypes.Count} avatar types");
                return s_cachedTypes;
            }
        }

        private static void ParseFactions(string[] lines)
        {
            // Line 14: Undead, Navy, Creature, Townfolk, Pirate, TradingCo, Ghost, VoodooZombie, BountyHunter = Factions
            string factionPattern = @"Undead,\s*Navy,\s*Creature,\s*Townfolk,\s*Pirate,\s*TradingCo,\s*Ghost,\s*VoodooZombie,\s*BountyHunter";

            foreach (string line in lines)
            {
                if (Regex.IsMatch(line, factionPattern))
                {
                    // Factions are base types, we'll categorize enemies under them
                    DebugLogger.LogWorldImporter($"[AvatarTypesParser] Found faction definitions");
                    break;
                }
            }
        }

        private static void ParseCreatureTracks(string[] lines)
        {
            // Line 17: LandCreature, SeaCreature, AirCreature, SeaMonster, Animal = CreatureTracks
            // Then parse each creature list:
            // Line 20: Crab, StoneCrab, RockCrab, ... = LandCreatures
            // Line 61: Seagull, Raven, Bat, ... = AirCreatures

            ParseCreatureList(lines, "LandCreatures", "LandCreature");
            ParseCreatureList(lines, "AirCreatures", "AirCreature");
            ParseCreatureList(lines, "SeaCreatures", "SeaCreature");
            ParseCreatureList(lines, "SeaMonsters", "SeaMonster");
            ParseCreatureList(lines, "Animals", "Animal");
        }

        private static void ParseUndeadTracks(string[] lines)
        {
            // Line 79: Earth, Air, Fire, Water, Classic, Boss, French, Spanish, EarthSpecial = UndeadTracks
            // Line 82: Clod, Sludge, Mire, ... = EarthUndead
            // Line 115: Whiff, Reek, Billow, ... = AirUndead
            // Line 118: Glint, Flicker, Smolder, ... = FireUndead
            // Line 121: Drip, Damp, Drizzle, ... = WaterUndead

            ParseUndeadList(lines, "EarthUndead", "Earth");
            ParseUndeadList(lines, "AirUndead", "Air");
            ParseUndeadList(lines, "FireUndead", "Fire");
            ParseUndeadList(lines, "WaterUndead", "Water");
        }

        private static void ParseNavyTracks(string[] lines)
        {
            // Line 189: Soldier, Marksman, Leader = NavyTracks
            // Line 192: Axeman, Swordsman, RoyalGuard, ... = Soldiers
            // Line 195: Cadet, Guard, Marine, ... = Marksmen

            ParseNavyList(lines, "Soldiers", "Soldier");
            ParseNavyList(lines, "Marksmen", "Marksman");
        }

        private static void ParseCreatureList(string[] lines, string listName, string track)
        {
            // Find pattern: "Name1, Name2, Name3, ... = ListName"
            string pattern = $@"^([A-Z][a-zA-Z,\s]+)\s*=\s*{listName}";

            foreach (string line in lines)
            {
                Match match = Regex.Match(line, pattern);
                if (match.Success)
                {
                    string namesStr = match.Groups[1].Value;
                    string[] names = namesStr.Split(',');

                    foreach (string name in names)
                    {
                        string trimmedName = name.Trim();
                        if (!string.IsNullOrEmpty(trimmedName))
                        {
                            var info = new AvatarTypeInfo(trimmedName)
                            {
                                faction = "Creature",
                                track = track,
                                isCreature = true,
                                isHuman = false
                            };
                            s_cachedTypes[trimmedName] = info;
                        }
                    }

                    DebugLogger.LogWorldImporter($"[AvatarTypesParser] Parsed {names.Length} creatures from {listName} ({track})");
                    break;
                }
            }
        }

        private static void ParseUndeadList(string[] lines, string listName, string track)
        {
            // Find pattern: "Name1, Name2, Name3, ... = ListName"
            string pattern = $@"^([A-Z][a-zA-Z,\s]+)\s*=\s*{listName}";

            foreach (string line in lines)
            {
                Match match = Regex.Match(line, pattern);
                if (match.Success)
                {
                    string namesStr = match.Groups[1].Value;
                    string[] names = namesStr.Split(',');

                    foreach (string name in names)
                    {
                        string trimmedName = name.Trim();
                        if (!string.IsNullOrEmpty(trimmedName))
                        {
                            var info = new AvatarTypeInfo(trimmedName)
                            {
                                faction = "Undead",
                                track = track,
                                isCreature = false,
                                isHuman = false
                            };
                            s_cachedTypes[trimmedName] = info;
                        }
                    }

                    DebugLogger.LogWorldImporter($"[AvatarTypesParser] Parsed {names.Length} undead from {listName} ({track})");
                    break;
                }
            }
        }

        private static void ParseNavyList(string[] lines, string listName, string track)
        {
            // Find pattern: "Name1, Name2, Name3, ... = ListName"
            string pattern = $@"^([A-Z][a-zA-Z,\s]+)\s*=\s*{listName}";

            foreach (string line in lines)
            {
                Match match = Regex.Match(line, pattern);
                if (match.Success)
                {
                    string namesStr = match.Groups[1].Value;
                    string[] names = namesStr.Split(',');

                    foreach (string name in names)
                    {
                        string trimmedName = name.Trim();
                        if (!string.IsNullOrEmpty(trimmedName))
                        {
                            var info = new AvatarTypeInfo(trimmedName)
                            {
                                faction = "Navy",
                                track = track,
                                isCreature = false,
                                isHuman = true
                            };
                            s_cachedTypes[trimmedName] = info;
                        }
                    }

                    DebugLogger.LogWorldImporter($"[AvatarTypesParser] Parsed {names.Length} navy types from {listName} ({track})");
                    break;
                }
            }
        }

        /// <summary>
        /// Check if a type is a creature (uses Animal AI)
        /// </summary>
        public static bool IsCreatureType(string typeName)
        {
            var info = GetAvatarType(typeName);
            return info != null && info.isCreature;
        }

        /// <summary>
        /// Get all creature type names
        /// </summary>
        public static List<string> GetAllCreatureTypes()
        {
            var types = LoadAllTypes();
            var creatures = new List<string>();

            foreach (var kvp in types)
            {
                if (kvp.Value.isCreature)
                {
                    creatures.Add(kvp.Key);
                }
            }

            return creatures;
        }

        /// <summary>
        /// Clear the cache
        /// </summary>
        public static void ClearCache()
        {
            lock (s_cacheLock)
            {
                s_cachedTypes = null;
                DebugLogger.LogWorldImporter("[AvatarTypesParser] Cache cleared");
            }
        }
    }
}
