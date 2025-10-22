using UnityEngine;

namespace POTCO
{
    /// <summary>
    /// Runtime component that stores NPC properties from world data
    /// Applied by PropertyProcessor.SpawnNPC() during import
    /// </summary>
    public class NPCData : MonoBehaviour
    {
        [Header("Identity")]
        public string npcId;                    // Object ID from world data
        public string category = "Commoner";     // Category (Commoner, Cast, etc.)
        public string team = "Villager";         // Team affiliation

        [Header("Behavior")]
        public string startState = "LandRoam";   // Start State (Idle/Walk maps to LandRoam)
        public float patrolRadius = 12f;         // Patrol Radius
        public float aggroRadius = 0f;           // Aggro Radius (0 = non-combat NPC)

        [Header("Animations")]
        public string animSet = "default";       // Animation set
        public string greetingAnimation = "";    // Greeting Animation
        public string noticeAnimation1 = "";     // Notice Animation 1
        public string noticeAnimation2 = "";     // Notice Animation 2

        [Header("Runtime Flags (Set by NPCAnimationPlayer)")]
        [Tooltip("If true, this NPC has contextual animations and should stay locked in place")]
        public bool isStationary = false;        // Set to true if NPC has look variations
    }
}
