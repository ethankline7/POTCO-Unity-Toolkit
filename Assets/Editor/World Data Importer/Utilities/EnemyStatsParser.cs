using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using POTCO.Editor;

namespace WorldDataImporter.Utilities
{
    /// <summary>
    /// Parser for POTCO EnemyGlobals.py - extracts enemy stats from __baseAvatarStats
    /// </summary>
    public static class EnemyStatsParser
    {
        private static Dictionary<string, EnemyStats> s_cachedStats = null;
        private static readonly object s_cacheLock = new object();

        public class EnemyStats
        {
            public string enemyName;
            public int minLevel;
            public int maxLevel;
            public float damageMultiplier;
            public float aggroRadius;
            public float searchRadius;
            public int enemyType;         // 1=SKELETON, 2=MONSTER, 3=HUMAN
            public int modelId;

            public EnemyStats(string name)
            {
                this.enemyName = name;
                this.minLevel = 1;
                this.maxLevel = 1;
                this.damageMultiplier = 1.0f;
                this.aggroRadius = 5.0f;
                this.searchRadius = 2.0f;
                this.enemyType = 2; // Default to MONSTER
                this.modelId = 0;
            }

            public string GetEnemyTypeName()
            {
                switch (enemyType)
                {
                    case 1: return "SKELETON";
                    case 2: return "MONSTER";
                    case 3: return "HUMAN";
                    default: return "UNKNOWN";
                }
            }
        }

        /// <summary>
        /// Get enemy stats by name
        /// </summary>
        public static EnemyStats GetEnemyStats(string enemyName)
        {
            var stats = LoadAllStats();
            if (stats.TryGetValue(enemyName, out EnemyStats enemyStats))
            {
                return enemyStats;
            }
            return null;
        }

        /// <summary>
        /// Load all enemy stats from EnemyGlobals.py __baseAvatarStats
        /// </summary>
        public static Dictionary<string, EnemyStats> LoadAllStats()
        {
            lock (s_cacheLock)
            {
                if (s_cachedStats != null)
                {
                    return s_cachedStats;
                }

                DebugLogger.LogWorldImporter($"[EnemyStatsParser] Parsing EnemyGlobals.py...");
                s_cachedStats = new Dictionary<string, EnemyStats>();

                string enemyGlobalsPath = Path.Combine(Application.dataPath, "Editor", "POTCO_Source", "battle", "EnemyGlobals.py");

                if (!File.Exists(enemyGlobalsPath))
                {
                    Debug.LogError($"[EnemyStatsParser] EnemyGlobals.py not found at: {enemyGlobalsPath}");
                    return s_cachedStats;
                }

                string content = File.ReadAllText(enemyGlobalsPath);
                ParseBaseAvatarStats(content);

                DebugLogger.LogWorldImporter($"[EnemyStatsParser] Parsed stats for {s_cachedStats.Count} enemies");
                return s_cachedStats;
            }
        }

        private static void ParseBaseAvatarStats(string content)
        {
            // Find the __baseAvatarStats dictionary (starts around line 300)
            // Pattern: AvatarTypes.SomeName: [minLevel, maxLevel, damageMultiplier, aggroRadius, searchRadius, enemyType, modelId],

            string pattern = @"AvatarTypes\.(\w+):\s*\[\s*(\d+),\s*(\d+),\s*([\d.]+),\s*([\d.]+),\s*([\d.]+),\s*(\w+),\s*(\d+)\s*\]";

            MatchCollection matches = Regex.Matches(content, pattern);

            foreach (Match match in matches)
            {
                string enemyName = match.Groups[1].Value;
                int minLevel = int.Parse(match.Groups[2].Value);
                int maxLevel = int.Parse(match.Groups[3].Value);
                float damageMultiplier = float.Parse(match.Groups[4].Value);
                float aggroRadius = float.Parse(match.Groups[5].Value);
                float searchRadius = float.Parse(match.Groups[6].Value);
                string enemyTypeStr = match.Groups[7].Value;
                int modelId = int.Parse(match.Groups[8].Value);

                // Convert enemy type string to int
                int enemyType = ConvertEnemyType(enemyTypeStr);

                var stats = new EnemyStats(enemyName)
                {
                    minLevel = minLevel,
                    maxLevel = maxLevel,
                    damageMultiplier = damageMultiplier,
                    aggroRadius = aggroRadius,
                    searchRadius = searchRadius,
                    enemyType = enemyType,
                    modelId = modelId
                };

                s_cachedStats[enemyName] = stats;
            }
        }

        private static int ConvertEnemyType(string typeStr)
        {
            switch (typeStr)
            {
                case "SKELETON":
                    return 1;
                case "MONSTER":
                    return 2;
                case "HUMAN":
                    return 3;
                default:
                    Debug.LogWarning($"[EnemyStatsParser] Unknown enemy type: {typeStr}, defaulting to MONSTER");
                    return 2;
            }
        }

        /// <summary>
        /// Clear the cache
        /// </summary>
        public static void ClearCache()
        {
            lock (s_cacheLock)
            {
                s_cachedStats = null;
                DebugLogger.LogWorldImporter("[EnemyStatsParser] Cache cleared");
            }
        }

        /// <summary>
        /// Get all enemy names that have stats defined
        /// </summary>
        public static List<string> GetAllEnemyNames()
        {
            var stats = LoadAllStats();
            return new List<string>(stats.Keys);
        }
    }
}
