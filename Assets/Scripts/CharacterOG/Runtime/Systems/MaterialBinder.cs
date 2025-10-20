/// <summary>
/// Binds textures and dye colors to character materials using MaterialPropertyBlock.
/// Avoids runtime material instantiation for performance.
/// Caches property blocks per renderer.
/// PHASE 3 OPTIMIZATION: Uses PropertyBlocks exclusively, no material instances created.
/// Shares texture cache across all instances for maximum performance.
/// </summary>
using System.Collections.Generic;
using UnityEngine;

namespace CharacterOG.Runtime.Systems
{
    public class MaterialBinder
    {
        private Dictionary<Renderer, MaterialPropertyBlock> propertyBlocks = new();

        // PHASE 3 OPTIMIZATION: Static texture cache shared across ALL MaterialBinder instances
        private static Dictionary<string, Texture2D> s_sharedTextureCache = new Dictionary<string, Texture2D>();
        private static readonly object s_textureCacheLock = new object();

        // PHASE 3 OPTIMIZATION: Property block pool to reuse blocks
        private static Queue<MaterialPropertyBlock> s_propertyBlockPool = new Queue<MaterialPropertyBlock>();
        private static readonly object s_poolLock = new object();

        // Shader property names (customize based on your shaders)
        private static readonly int s_mainTexProp = Shader.PropertyToID("_MainTex");
        private static readonly int s_baseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int s_colorProp = Shader.PropertyToID("_Color"); // Built-in RP
        private static readonly int s_dyeColorProp = Shader.PropertyToID("_DyeColor");
        private static readonly int s_trimColorProp = Shader.PropertyToID("_TrimColor");

        /// <summary>Texture loader delegate - set this to load textures from your asset pipeline</summary>
        public System.Func<string, Texture2D> TextureLoader { get; set; }

        public MaterialBinder()
        {
            // Default texture loader - searches POTCO phase_2 through phase_7 maps folders
            TextureLoader = (texName) =>
            {
                // POTCO textures are located in Resources/phase_#/maps/ (phase_2 through phase_7)
                string[] searchPaths = new[]
                {
                    $"phase_2/maps/{texName}",
                    $"phase_3/maps/{texName}",
                    $"phase_4/maps/{texName}",
                    $"phase_5/maps/{texName}",
                    $"phase_6/maps/{texName}",
                    $"phase_7/maps/{texName}"
                };

                foreach (var path in searchPaths)
                {
                    var tex = Resources.Load<Texture2D>(path);
                    if (tex != null)
                    {
                        return tex;
                    }
                }

                return null;
            };
        }

        // Additional shader properties for dual textures
        private static readonly int s_overlayTexProp = Shader.PropertyToID("_OverlayTex");
        private static readonly int s_detailTexProp = Shader.PropertyToID("_DetailTex");

        /// <summary>Apply texture to renderer (supports dual textures with '+' separator)</summary>
        public void ApplyTexture(Renderer renderer, string textureId)
        {
            if (renderer == null || string.IsNullOrEmpty(textureId))
                return;

            // Check for dual textures (e.g., "hat_barbossa+hat_barbossa_feather")
            if (textureId.Contains("+"))
            {
                var texNames = textureId.Split('+');
                if (texNames.Length == 2)
                {
                    ApplyDualTexture(renderer, texNames[0].Trim(), texNames[1].Trim());
                    return;
                }
            }

            // Single texture
            var tex = GetTexture(textureId);

            if (tex == null)
            {
                Debug.LogWarning($"MaterialBinder: Texture '{textureId}' not found");
                return;
            }

            // PHASE 3 OPTIMIZATION: Use PropertyBlock exclusively - NO material instance creation
            var block = GetOrCreatePropertyBlock(renderer);
            block.SetTexture(s_mainTexProp, tex);
            renderer.SetPropertyBlock(block);
        }

        /// <summary>Apply two textures (base + overlay/detail) - used for hats with feathers, etc.</summary>
        private void ApplyDualTexture(Renderer renderer, string baseTexId, string overlayTexId)
        {
            var baseTex = GetTexture(baseTexId);
            var overlayTex = GetTexture(overlayTexId);

            if (baseTex == null && overlayTex == null)
            {
                Debug.LogWarning($"MaterialBinder: Neither texture found: '{baseTexId}' or '{overlayTexId}'");
                return;
            }

            if (renderer.sharedMaterial == null)
                return;

            // PHASE 3 OPTIMIZATION: Use PropertyBlock exclusively for dual textures
            var block = GetOrCreatePropertyBlock(renderer);

            // Apply base texture to _MainTex
            if (baseTex != null)
            {
                block.SetTexture(s_mainTexProp, baseTex);
            }

            // Apply overlay texture to _OverlayTex or _DetailTex
            // Try _OverlayTex first (custom shader), then _DetailTex (standard Unity)
            if (overlayTex != null)
            {
                if (renderer.sharedMaterial.HasProperty(s_overlayTexProp))
                {
                    block.SetTexture(s_overlayTexProp, overlayTex);
                }
                else if (renderer.sharedMaterial.HasProperty(s_detailTexProp))
                {
                    block.SetTexture(s_detailTexProp, overlayTex);
                }
                else
                {
                    // Fallback: overlay shader property not available
                    Debug.LogWarning($"MaterialBinder: Material doesn't have _OverlayTex or _DetailTex property, overlay '{overlayTexId}' may not appear correctly");
                }
            }

            renderer.SetPropertyBlock(block);
        }

        /// <summary>Apply dye color to renderer (base channel)</summary>
        public void ApplyDye(Renderer renderer, Color color)
        {
            ApplyDye(renderer, "base", color);
        }

        /// <summary>Apply dye color to specific channel (base, trim, etc.)</summary>
        public void ApplyDye(Renderer renderer, string channel, Color color)
        {
            if (renderer == null)
                return;

            var block = GetOrCreatePropertyBlock(renderer);

            // For skin/body colors, use the material's main color property
            // Try both URP (_BaseColor) and Built-in (_Color) properties
            if (channel.ToLower() == "base")
            {
                // Check which property the material has
                if (renderer.sharedMaterial != null)
                {
                    if (renderer.sharedMaterial.HasProperty(s_baseColorProp))
                    {
                        block.SetColor(s_baseColorProp, color);
                    }
                    else if (renderer.sharedMaterial.HasProperty(s_colorProp))
                    {
                        block.SetColor(s_colorProp, color);
                    }
                    else if (renderer.sharedMaterial.HasProperty(s_dyeColorProp))
                    {
                        block.SetColor(s_dyeColorProp, color);
                    }
                    else
                    {
                        // Fallback: try _BaseColor anyway (URP default)
                        block.SetColor(s_baseColorProp, color);
                    }
                }
            }
            else
            {
                // For other channels (trim, etc.), use channel-specific properties
                int propertyId = channel.ToLower() switch
                {
                    "trim" => s_trimColorProp,
                    _ => s_dyeColorProp
                };
                block.SetColor(propertyId, color);
            }

            renderer.SetPropertyBlock(block);
        }

        /// <summary>Apply both texture and dye color</summary>
        public void ApplyTextureAndDye(Renderer renderer, string textureId, Color dyeColor)
        {
            if (renderer == null)
                return;

            var tex = GetTexture(textureId);
            var block = GetOrCreatePropertyBlock(renderer);

            if (tex != null)
                block.SetTexture(s_mainTexProp, tex);

            block.SetColor(s_dyeColorProp, dyeColor);
            renderer.SetPropertyBlock(block);
        }

        /// <summary>Clear all property blocks (reset to material defaults)</summary>
        public void ClearPropertyBlocks()
        {
            foreach (var kvp in propertyBlocks)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.SetPropertyBlock(null);
                }
            }

            propertyBlocks.Clear();
        }

        /// <summary>Clear property block for specific renderer</summary>
        public void ClearPropertyBlock(Renderer renderer)
        {
            if (renderer != null && propertyBlocks.ContainsKey(renderer))
            {
                renderer.SetPropertyBlock(null);
                propertyBlocks.Remove(renderer);
            }
        }

        /// <summary>Register a texture in cache</summary>
        public void RegisterTexture(string textureId, Texture2D texture)
        {
            lock (s_textureCacheLock)
            {
                s_sharedTextureCache[textureId] = texture;
            }
        }

        /// <summary>Clear texture cache (PHASE 3: now clears shared cache)</summary>
        public static void ClearTextureCache()
        {
            lock (s_textureCacheLock)
            {
                s_sharedTextureCache.Clear();
                Debug.Log("[MaterialBinder] Shared texture cache cleared");
            }
        }

        /// <summary>PHASE 3: Preload common textures to warm up the cache</summary>
        public static void PreloadCommonTextures(System.Func<string, Texture2D> textureLoader, string[] textureIds)
        {
            if (textureLoader == null || textureIds == null || textureIds.Length == 0)
                return;

            int loadedCount = 0;
            foreach (var texId in textureIds)
            {
                lock (s_textureCacheLock)
                {
                    if (!s_sharedTextureCache.ContainsKey(texId))
                    {
                        var tex = textureLoader(texId);
                        if (tex != null)
                        {
                            s_sharedTextureCache[texId] = tex;
                            loadedCount++;
                        }
                    }
                }
            }

            Debug.Log($"[MaterialBinder] Preloaded {loadedCount} textures into shared cache");
        }

        /// <summary>Get cache statistics for debugging</summary>
        public static string GetCacheStats()
        {
            lock (s_textureCacheLock)
            {
                lock (s_poolLock)
                {
                    return $"MaterialBinder Cache: {s_sharedTextureCache.Count} textures cached, {s_propertyBlockPool.Count} blocks pooled";
                }
            }
        }

        private Texture2D GetTexture(string textureId)
        {
            // PHASE 3 OPTIMIZATION: Check shared cache first
            lock (s_textureCacheLock)
            {
                if (s_sharedTextureCache.TryGetValue(textureId, out var cached))
                    return cached;
            }

            // Cache miss - try to load
            var tex = TextureLoader?.Invoke(textureId);

            if (tex != null)
            {
                // Store in shared cache
                lock (s_textureCacheLock)
                {
                    s_sharedTextureCache[textureId] = tex;
                }
            }

            return tex;
        }

        private MaterialPropertyBlock GetOrCreatePropertyBlock(Renderer renderer)
        {
            if (!propertyBlocks.TryGetValue(renderer, out var block))
            {
                // PHASE 3 OPTIMIZATION: Try to get a block from the pool first
                lock (s_poolLock)
                {
                    if (s_propertyBlockPool.Count > 0)
                    {
                        block = s_propertyBlockPool.Dequeue();
                        block.Clear(); // Reset the block
                    }
                    else
                    {
                        block = new MaterialPropertyBlock();
                    }
                }
                propertyBlocks[renderer] = block;
            }
            else
            {
                // Preserve existing properties
                renderer.GetPropertyBlock(block);
            }

            return block;
        }

        /// <summary>Get diagnostic info (PHASE 3: updated for shared cache)</summary>
        public string GetDiagnosticInfo()
        {
            lock (s_textureCacheLock)
            {
                return $"MaterialBinder: {propertyBlocks.Count} active property blocks, {s_sharedTextureCache.Count} shared cached textures";
            }
        }
    }
}
