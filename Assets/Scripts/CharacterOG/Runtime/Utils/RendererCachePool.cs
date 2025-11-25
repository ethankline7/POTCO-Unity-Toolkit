/// <summary>
/// PHASE 2 OPTIMIZATION: Renderer cache pool for character models.
/// Maintains gender-specific template caches built from prefabs once, then cloned for new NPCs.
/// Eliminates repeated GetComponentsInChildren calls during NPC spawning.
/// </summary>
using System.Collections.Generic;
using UnityEngine;

namespace CharacterOG.Runtime.Utils
{
    public class RendererCachePool
    {
        // Static singleton instance
        private static RendererCachePool s_instance;
        public static RendererCachePool Instance
        {
            get
            {
                if (s_instance == null)
                    s_instance = new RendererCachePool();
                return s_instance;
            }
        }

        // Template caches built from prefabs (one per gender)
        private Dictionary<string, GroupRendererCache> templateCaches = new Dictionary<string, GroupRendererCache>();

        // Lock for thread safety
        private readonly object cacheLock = new object();

        /// <summary>Get or create a renderer cache for a character instance</summary>
        /// <param name="gender">Character gender ("m" or "f")</param>
        /// <param name="characterRoot">The character GameObject instance</param>
        /// <returns>A GroupRendererCache for this character</returns>
        public GroupRendererCache GetOrCreateCache(string gender, GameObject characterRoot)
        {
            if (characterRoot == null)
            {
                Debug.LogError("[RendererCachePool] characterRoot is null");
                return null;
            }

            // OPTIMIZATION REMOVED: Template caching caused severe bugs because GroupRendererCache
            // stores direct references to Renderer components. Reusing a template meant controlling
            // the renderers of the *first* spawned character, not the current one.
            // We must build a fresh cache for every new character instance.
            // scanning ~1000 transforms is fast enough (~1ms).
            
            return new GroupRendererCache(characterRoot);
        }

        /// <summary>Clear all template caches (useful for editor refresh)</summary>
        public static void ClearCaches()
        {
            if (s_instance != null)
            {
                lock (s_instance.cacheLock)
                {
                    s_instance.templateCaches.Clear();
                    Debug.Log("[RendererCachePool] All template caches cleared");
                }
            }
        }

        /// <summary>Get cache statistics for debugging</summary>
        public static string GetCacheStats()
        {
            if (s_instance == null)
                return "RendererCachePool: Not initialized";

            lock (s_instance.cacheLock)
            {
                var stats = $"RendererCachePool: {s_instance.templateCaches.Count} gender templates";
                foreach (var kvp in s_instance.templateCaches)
                {
                    stats += $"\n  - {kvp.Key}: {kvp.Value.TotalRendererCount} renderers";
                }
                return stats;
            }
        }
    }
}
