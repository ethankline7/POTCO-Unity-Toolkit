using System.Collections.Generic;

namespace WorldDataImporter.Data
{
    /// <summary>
    /// Data class holding enemy/spawnable information parsed from POTCO source files
    /// (AvatarTypes.py and EnemyGlobals.py)
    /// </summary>
    public class EnemyData
    {
        public string enemyName;            // Enemy type name (e.g., "Alligator", "Navy", "Crab T1")
        public string faction;              // Faction: "Creature", "Navy", "Undead", etc.
        public string track;                // Sub-faction: "LandCreature", "Soldier", "Earth", etc.
        public bool isCreature;             // True if this is a creature (use Animal AI)
        public bool isHuman;                // True if this is a human enemy (use NPC AI)
        public string modelPath;            // Model path if available

        // Stats from EnemyGlobals.py
        public int minLevel;                // Minimum level
        public int maxLevel;                // Maximum level
        public float damageMultiplier;      // Damage multiplier
        public float aggroRadius;           // Aggression radius
        public float searchRadius;          // Search radius
        public int enemyType;               // 1=SKELETON, 2=MONSTER, 3=HUMAN
        public int modelId;                 // Model ID

        public EnemyData(string enemyName)
        {
            this.enemyName = enemyName;
            this.faction = "";
            this.track = "";
            this.isCreature = false;
            this.isHuman = false;
            this.modelPath = "";
            this.minLevel = 1;
            this.maxLevel = 1;
            this.damageMultiplier = 1.0f;
            this.aggroRadius = 5.0f;
            this.searchRadius = 2.0f;
            this.enemyType = 2; // Default to MONSTER
            this.modelId = 0;
        }

        /// <summary>
        /// Check if this enemy should use creature AI (same as Type="Animal")
        /// </summary>
        public bool ShouldUseCreatureAI()
        {
            return isCreature;
        }

        /// <summary>
        /// Check if this enemy should use NPC AI (same as Type="Townsperson")
        /// </summary>
        public bool ShouldUseNPCAI()
        {
            return isHuman || faction == "Undead" || faction == "Navy";
        }

        /// <summary>
        /// Get enemy type name (SKELETON, MONSTER, HUMAN)
        /// </summary>
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
}
