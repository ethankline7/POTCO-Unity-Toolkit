/// <summary>
/// Caches all Renderer components on a character model indexed by exact group name.
/// Provides fast Enable/Disable operations for clothing/body part visibility.
/// Build once on initialization, reuse throughout character lifetime.
/// </summary>
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CharacterOG.Runtime.Utils
{
    public class GroupRendererCache
    {
        private Dictionary<string, List<Renderer>> renderersByGroup = new();
        private Dictionary<string, List<Renderer>> renderersByPattern = new();
        private List<Renderer> allRenderers = new();
        private GameObject rootObject;

        /// <summary>All group names in the cache</summary>
        public IEnumerable<string> GroupNames => renderersByGroup.Keys;

        /// <summary>Total renderer count</summary>
        public int TotalRendererCount => allRenderers.Count;

        /// <summary>Build cache from root GameObject</summary>
        public GroupRendererCache(GameObject root)
        {
            rootObject = root;
            BuildCache();
        }

        /// <summary>Rebuild cache (call if hierarchy changes)</summary>
        public void Rebuild()
        {
            renderersByGroup.Clear();
            renderersByPattern.Clear();
            allRenderers.Clear();
            BuildCache();
        }

        /// <summary>Enable/disable all renderers matching exact group name</summary>
        public void EnableGroup(string groupName, bool enabled)
        {
            if (renderersByGroup.TryGetValue(groupName, out var renderers))
            {
                foreach (var renderer in renderers)
                {
                    if (renderer != null)
                        renderer.enabled = enabled;
                }
            }
        }

        /// <summary>Enable/disable all renderers matching pattern (supports wildcards * and **)</summary>
        public void EnablePattern(string pattern, bool enabled)
        {
            var matches = GetRenderersMatchingPattern(pattern);

            foreach (var renderer in matches)
            {
                if (renderer != null)
                    renderer.enabled = enabled;
            }
        }

        /// <summary>Get all renderers with exact group name</summary>
        public List<Renderer> GetRenderers(string groupName)
        {
            return renderersByGroup.TryGetValue(groupName, out var renderers) ? renderers : new List<Renderer>();
        }

        /// <summary>Get all renderers matching pattern</summary>
        public List<Renderer> GetRenderersMatchingPattern(string pattern)
        {
            // Check cache first
            if (renderersByPattern.TryGetValue(pattern, out var cached))
                return cached;

            var matches = new List<Renderer>();

            // Convert pattern to regex-like matching
            // ** = match any path segment
            // * = match within current segment
            string regexPattern = pattern
                .Replace("**", "___DOUBLESTAR___")
                .Replace("*", "[^/]*")
                .Replace("___DOUBLESTAR___", ".*");

            var regex = new System.Text.RegularExpressions.Regex($"^{regexPattern}$");

            foreach (var kvp in renderersByGroup)
            {
                if (regex.IsMatch(kvp.Key))
                {
                    matches.AddRange(kvp.Value);
                }
            }

            // Cache pattern result
            renderersByPattern[pattern] = matches;
            return matches;
        }

        /// <summary>Check if group exists</summary>
        public bool HasGroup(string groupName)
        {
            return renderersByGroup.ContainsKey(groupName);
        }

        /// <summary>Get hierarchical path for a transform</summary>
        private string GetHierarchyPath(Transform t, Transform root)
        {
            if (t == root)
                return t.name;

            var parts = new List<string>();

            while (t != null && t != root)
            {
                parts.Insert(0, t.name);
                t = t.parent;
            }

            return string.Join("/", parts);
        }

        private void BuildCache()
        {
            if (rootObject == null)
            {
                Debug.LogError("GroupRendererCache: Root object is null");
                return;
            }

            // Get all renderers in hierarchy
            allRenderers.AddRange(rootObject.GetComponentsInChildren<Renderer>(includeInactive: true));

            foreach (var renderer in allRenderers)
            {
                // Use exact GameObject name as group name
                string groupName = renderer.gameObject.name;

                if (!renderersByGroup.ContainsKey(groupName))
                {
                    renderersByGroup[groupName] = new List<Renderer>();
                }

                renderersByGroup[groupName].Add(renderer);

                // Also index by full hierarchical path for pattern matching
                string fullPath = GetHierarchyPath(renderer.transform, rootObject.transform);

                if (!renderersByGroup.ContainsKey(fullPath))
                {
                    renderersByGroup[fullPath] = new List<Renderer>();
                }

                if (fullPath != groupName)
                {
                    renderersByGroup[fullPath].Add(renderer);
                }
            }

            Debug.Log($"GroupRendererCache: Indexed {allRenderers.Count} renderers across {renderersByGroup.Count} unique group names");
        }

        /// <summary>Get diagnostic info for debugging</summary>
        public string GetDiagnosticInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"GroupRendererCache for {rootObject.name}");
            sb.AppendLine($"Total Renderers: {allRenderers.Count}");
            sb.AppendLine($"Unique Groups: {renderersByGroup.Count}");
            sb.AppendLine();
            sb.AppendLine("Groups:");

            foreach (var kvp in renderersByGroup.OrderBy(x => x.Key))
            {
                sb.AppendLine($"  {kvp.Key} ({kvp.Value.Count} renderers)");
            }

            return sb.ToString();
        }
    }
}
