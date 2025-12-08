using System.Collections.Generic;
using UnityEngine;
using WorldDataImporter.Data;
using POTCO.Editor;

namespace WorldDataImporter.Utilities
{
    /// <summary>
    /// Parser for POTCO enemy/spawnable definitions - FULLY DYNAMIC
    /// Combines data from AvatarTypesParser and EnemyStatsParser
    /// Maps spawn node "Spawnables" strings to enemy data
    /// </summary>
    public static class EnemyDataParser
    {
        private static Dictionary<string, EnemyData> s_cachedEnemies = null;
        private static readonly object s_cacheLock = new object();

        /// <summary>
        /// Get enemy data by spawnable name (e.g., "Alligator", "Crab T1", "Navy")
        /// </summary>
        public static EnemyData GetEnemyData(string spawnableName)
        {
            if (string.IsNullOrEmpty(spawnableName))
                return null;

            var enemies = LoadAllEnemies();

            // Try exact match first
            if (enemies.TryGetValue(spawnableName, out EnemyData enemy))
            {
                return enemy;
            }

            // Try case-insensitive match
            foreach (var kvp in enemies)
            {
                if (kvp.Key.Equals(spawnableName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            // Try partial match (e.g., "Crab T1" might be stored as "Crab")
            string baseName = spawnableName.Split(' ')[0]; // Get first word
            if (enemies.TryGetValue(baseName, out EnemyData baseEnemy))
            {
                return baseEnemy;
            }

            DebugLogger.LogWorldImporter($"[EnemyDataParser] Unknown spawnable: {spawnableName}");
            return null;
        }

        /// <summary>
        /// Load all enemy definitions DYNAMICALLY from Python source files
        /// </summary>
        public static Dictionary<string, EnemyData> LoadAllEnemies()
        {
            lock (s_cacheLock)
            {
                if (s_cachedEnemies != null)
                {
                    return s_cachedEnemies;
                }

                DebugLogger.LogWorldImporter($"[EnemyDataParser] Loading enemy definitions DYNAMICALLY...");
                s_cachedEnemies = new Dictionary<string, EnemyData>();

                // Step 1: Load avatar type information (faction, track, isCreature, isHuman)
                var avatarTypes = AvatarTypesParser.LoadAllTypes();

                // Step 2: Load enemy stats (levels, aggro, damage, etc.)
                var enemyStats = EnemyStatsParser.LoadAllStats();

                // Step 3: Combine both sources into EnemyData objects
                foreach (var kvp in avatarTypes)
                {
                    string enemyName = kvp.Key;
                    var avatarInfo = kvp.Value;

                    // Create EnemyData from avatar type info
                    var enemyData = new EnemyData(enemyName)
                    {
                        faction = avatarInfo.faction,
                        track = avatarInfo.track,
                        isCreature = avatarInfo.isCreature,
                        isHuman = avatarInfo.isHuman
                    };

                    // Try to find stats for this enemy
                    if (enemyStats.TryGetValue(enemyName, out var stats))
                    {
                        enemyData.minLevel = stats.minLevel;
                        enemyData.maxLevel = stats.maxLevel;
                        enemyData.damageMultiplier = stats.damageMultiplier;
                        enemyData.aggroRadius = stats.aggroRadius;
                        enemyData.searchRadius = stats.searchRadius;
                        enemyData.enemyType = stats.enemyType;
                        enemyData.modelId = stats.modelId;
                    }
                    else
                    {
                        DebugLogger.LogWorldImporter($"[EnemyDataParser] No stats found for {enemyName}, using defaults");
                    }

                    s_cachedEnemies[enemyName] = enemyData;
                }

                DebugLogger.LogWorldImporter($"[EnemyDataParser] Dynamically loaded {s_cachedEnemies.Count} enemy definitions");
                return s_cachedEnemies;
            }
        }

        /// <summary>
        /// Clear the cache (useful for editor refresh)
        /// </summary>
        public static void ClearCache()
        {
            lock (s_cacheLock)
            {
                s_cachedEnemies = null;

                // Also clear the sub-parsers
                AvatarTypesParser.ClearCache();
                EnemyStatsParser.ClearCache();

                DebugLogger.LogWorldImporter("[EnemyDataParser] Cache cleared");
            }
        }

        /// <summary>
        /// Get list of all available enemy types
        /// </summary>
        public static List<string> GetAllEnemyTypes()
        {
            var enemies = LoadAllEnemies();
            return new List<string>(enemies.Keys);
        }

        /// <summary>
        /// Check if an enemy is a creature type (uses Animal AI)
        /// </summary>
        public static bool IsCreatureType(string enemyName)
        {
            var enemy = GetEnemyData(enemyName);
            return enemy != null && enemy.isCreature;
        }

        /// <summary>
        /// Get all creature type names
        /// </summary>
        public static List<string> GetAllCreatureTypes()
        {
            var enemies = LoadAllEnemies();
            var creatures = new List<string>();

            foreach (var kvp in enemies)
            {
                if (kvp.Value.isCreature)
                {
                    creatures.Add(kvp.Key);
                }
            }

            return creatures;
        }
    }
}
