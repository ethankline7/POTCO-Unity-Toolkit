using UnityEngine;
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

        /// <summary>
        /// Log message for NPC Controller (runtime AI, pathfinding, states)
        /// </summary>
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
        public static void LogErrorNPCImport(string message)
        {
            #if UNITY_EDITOR
            if (EditorPrefs.GetBool(DEBUG_NPC_IMPORT_KEY, false))
            {
                Debug.LogError(message);
            }
            #endif
        }
    }
}
