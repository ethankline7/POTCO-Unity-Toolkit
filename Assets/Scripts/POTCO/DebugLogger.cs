using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug; // Disambiguate Debug
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace POTCO
{
    /// <summary>
    /// Runtime debug logger that checks EditorPrefs for debug flags
    /// </summary>
    public static class DebugLogger
    {
        // EditorPrefs keys (must match DebugSettings)
        private const string DEBUG_NPC_CONTROLLER_KEY = "POTCO_Debug_NPCController";
        private const string DEBUG_NPC_ANIMATION_KEY = "POTCO_Debug_NPCAnimation";
        private const string DEBUG_NPC_IMPORT_KEY = "POTCO_Debug_NPCImport";
        private const string DEBUG_ANIMAL_ANIMATION_KEY = "POTCO_Debug_AnimalAnimation";
        private const string DEBUG_RUNTIME_ANIMATOR_KEY = "POTCO_Debug_RuntimeAnimator";
        private const string DEBUG_PLAYER_ANIMATION_KEY = "POTCO_Debug_PlayerAnimation";
        private const string DEBUG_SHIP_CONTROLLER_KEY = "POTCO_Debug_ShipController";
        private const string DEBUG_OCEAN_MANAGER_KEY = "POTCO_Debug_OceanManager";
        private const string DEBUG_LEVEL_GEOMETRY_KEY = "POTCO_Debug_LevelGeometry";

        /// <summary>
        /// Log message for NPC Controller (runtime AI, pathfinding, states)
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void LogNPCController(string message)
        {
            #if UNITY_EDITOR
            if (EditorPrefs.GetBool(DEBUG_NPC_CONTROLLER_KEY, false))
            {
                Debug.Log(message);
            }
            #endif
        }

        /// <summary>
        /// Log warning for NPC Controller
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void LogWarningNPCController(string message)
        {
            #if UNITY_EDITOR
            if (EditorPrefs.GetBool(DEBUG_NPC_CONTROLLER_KEY, false))
            {
                Debug.LogWarning(message);
            }
            #endif
        }

        /// <summary>
        /// Log error for NPC Controller
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void LogErrorNPCController(string message)
        {
            #if UNITY_EDITOR
            if (EditorPrefs.GetBool(DEBUG_NPC_CONTROLLER_KEY, false))
            {
                Debug.LogError(message);
            }
            #endif
        }

        /// <summary>
        /// Log message for NPC Animation (runtime animation playback, gender detection)
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void LogNPCAnimation(string message)
        {
            #if UNITY_EDITOR
            if (EditorPrefs.GetBool(DEBUG_NPC_ANIMATION_KEY, false))
            {
                Debug.Log(message);
            }
            #endif
        }

        /// <summary>
        /// Log warning for NPC Animation
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void LogWarningNPCAnimation(string message)
        {
            #if UNITY_EDITOR
            if (EditorPrefs.GetBool(DEBUG_NPC_ANIMATION_KEY, false))
            {
                Debug.LogWarning(message);
            }
            #endif
        }

        /// <summary>
        /// Log error for NPC Animation
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void LogErrorNPCAnimation(string message)
        {
            #if UNITY_EDITOR
            if (EditorPrefs.GetBool(DEBUG_NPC_ANIMATION_KEY, false))
            {
                Debug.LogError(message);
            }
            #endif
        }

        /// <summary>
        /// Log message for NPC Import
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void LogNPCImport(string message)
        {
            #if UNITY_EDITOR
            if (EditorPrefs.GetBool(DEBUG_NPC_IMPORT_KEY, false))
            {
                Debug.Log(message);
            }
            #endif
        }

        /// <summary>
        /// Log warning for NPC Import
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void LogWarningNPCImport(string message)
        {
            #if UNITY_EDITOR
            if (EditorPrefs.GetBool(DEBUG_NPC_IMPORT_KEY, false))
            {
                Debug.LogWarning(message);
            }
            #endif
        }

        /// <summary>
        /// Log error for NPC Import
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void LogErrorNPCImport(string message)
        {
            #if UNITY_EDITOR
            if (EditorPrefs.GetBool(DEBUG_NPC_IMPORT_KEY, false))
            {
                Debug.LogError(message);
            }
            #endif
        }

        /// <summary>
        /// Log message for Animal Animation (runtime animal animation playback)
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void LogAnimalAnimation(string message)
        {
            #if UNITY_EDITOR
            if (EditorPrefs.GetBool(DEBUG_ANIMAL_ANIMATION_KEY, false))
            {
                Debug.Log(message);
            }
            #endif
        }

        /// <summary>
        /// Log warning for Animal Animation
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void LogWarningAnimalAnimation(string message)
        {
            #if UNITY_EDITOR
            if (EditorPrefs.GetBool(DEBUG_ANIMAL_ANIMATION_KEY, false))
            {
                Debug.LogWarning(message);
            }
            #endif
        }

        /// <summary>
        /// Log message for RuntimeAnimatorPlayer (Playables API animation system)
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void LogRuntimeAnimator(string message)
        {
            #if UNITY_EDITOR
            if (EditorPrefs.GetBool(DEBUG_RUNTIME_ANIMATOR_KEY, false))
            {
                Debug.Log(message);
            }
            #endif
        }

        /// <summary>
        /// Log message for Player Animation (SimpleAnimationPlayer)
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void LogPlayerAnimation(string message)
        {
            #if UNITY_EDITOR
            if (EditorPrefs.GetBool(DEBUG_PLAYER_ANIMATION_KEY, false))
            {
                Debug.Log(message);
            }
            #endif
        }

        /// <summary>
        /// Log message for Ship Controller (wheel interaction, sailing)
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void LogShipController(string message)
        {
            #if UNITY_EDITOR
            if (EditorPrefs.GetBool(DEBUG_SHIP_CONTROLLER_KEY, false))
            {
                Debug.Log(message);
            }
            #endif
        }

        /// <summary>
        /// Log message for Ocean Manager (water color, time of day)
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void LogOceanManager(string message)
        {
            #if UNITY_EDITOR
            if (EditorPrefs.GetBool(DEBUG_OCEAN_MANAGER_KEY, false))
            {
                Debug.Log(message);
            }
            #endif
        }

        /// <summary>
        /// Log message for Level Geometry (hiding collision meshes)
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void LogLevelGeometry(string message)
        {
            #if UNITY_EDITOR
            if (EditorPrefs.GetBool(DEBUG_LEVEL_GEOMETRY_KEY, false))
            {
                Debug.Log(message);
            }
            #endif
        }
    }
}
