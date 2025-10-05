/// <summary>
/// Binds textures and dye colors to character materials using MaterialPropertyBlock.
/// Avoids runtime material instantiation for performance.
/// Caches property blocks per renderer.
/// </summary>
using System.Collections.Generic;
using UnityEngine;

namespace CharacterOG.Runtime.Systems
{
    public class MaterialBinder
    {
        private Dictionary<Renderer, MaterialPropertyBlock> propertyBlocks = new();
        private Dictionary<string, Texture2D> textureCache = new();

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
            // Default texture loader (tries Resources)
            TextureLoader = (texName) => Resources.Load<Texture2D>(texName);
        }

        /// <summary>Apply texture to renderer</summary>
        public void ApplyTexture(Renderer renderer, string textureId)
        {
            if (renderer == null || string.IsNullOrEmpty(textureId))
                return;

            var tex = GetTexture(textureId);

            if (tex == null)
            {
                Debug.LogWarning($"MaterialBinder: Texture '{textureId}' not found");
                return;
            }

            var block = GetOrCreatePropertyBlock(renderer);
            block.SetTexture(s_mainTexProp, tex);
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
            textureCache[textureId] = texture;
        }

        /// <summary>Clear texture cache</summary>
        public void ClearTextureCache()
        {
            textureCache.Clear();
        }

        private Texture2D GetTexture(string textureId)
        {
            // Check cache first
            if (textureCache.TryGetValue(textureId, out var cached))
                return cached;

            // Try to load
            var tex = TextureLoader?.Invoke(textureId);

            if (tex != null)
            {
                textureCache[textureId] = tex;
            }

            return tex;
        }

        private MaterialPropertyBlock GetOrCreatePropertyBlock(Renderer renderer)
        {
            if (!propertyBlocks.TryGetValue(renderer, out var block))
            {
                block = new MaterialPropertyBlock();
                propertyBlocks[renderer] = block;
            }
            else
            {
                // Preserve existing properties
                renderer.GetPropertyBlock(block);
            }

            return block;
        }

        /// <summary>Get diagnostic info</summary>
        public string GetDiagnosticInfo()
        {
            return $"MaterialBinder: {propertyBlocks.Count} active property blocks, {textureCache.Count} cached textures";
        }
    }
}
