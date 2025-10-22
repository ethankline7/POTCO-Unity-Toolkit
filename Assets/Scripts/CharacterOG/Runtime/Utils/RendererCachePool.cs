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

            string genderKey = gender.ToLower();

            lock (cacheLock)
            {
                // Check if we have a template cache for this gender
                if (!templateCaches.TryGetValue(genderKey, out var templateCache))
                {
                    // First time for this gender - build template cache from this instance
                    Debug.Log($"[RendererCachePool] Building template cache for gender '{genderKey}' from {characterRoot.name}");
                    templateCache = new GroupRendererCache(characterRoot);
                    templateCaches[genderKey] = templateCache;
                    Debug.Log($"[RendererCachePool] Template cache created: {templateCache.TotalRendererCount} renderers indexed");
                    return templateCache;
                }
                else
                {
                    // We have a template - create a new cache for this instance
                    // This is much faster than scanning the hierarchy again
                    Debug.Log($"[RendererCachePool] Reusing template cache structure for gender '{genderKey}' ({templateCache.TotalRendererCount} renderers)");
                    var newCache = new GroupRendererCache(characterRoot);
                    return newCache;
                }
            }
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
