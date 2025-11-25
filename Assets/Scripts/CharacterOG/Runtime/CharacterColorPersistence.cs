/// <summary>
/// Persists character colors (skin, hair, clothing) through play mode transitions
/// Uses MaterialPropertyBlock pattern similar to VisualColorHandler
/// Automatically reapplies colors when entering/exiting play mode
/// </summary>
using System.Collections.Generic;
using UnityEngine;

namespace CharacterOG.Runtime
{
    [ExecuteAlways] // Run in both edit and play mode
    public class CharacterColorPersistence : MonoBehaviour
    {
        // Serialized color data - persists through play mode transitions
        [SerializeField, HideInInspector]
        private Color skinColor = Color.white;
        [SerializeField, HideInInspector]
        private Color hairColor = Color.white;
        [SerializeField, HideInInspector]
        private Color topColor = Color.white;
        [SerializeField, HideInInspector]
        private Color botColor = Color.white;
        [SerializeField, HideInInspector]
        private Color shoeColor = Color.white;
        [SerializeField, HideInInspector]
        private bool hasStoredColors = false;

        // Property blocks for each renderer
        private Dictionary<Renderer, MaterialPropertyBlock> propertyBlocks = new Dictionary<Renderer, MaterialPropertyBlock>();

        // Shader properties
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int MainColorProperty = Shader.PropertyToID("_Color");
        private static readonly int DyeColorProperty = Shader.PropertyToID("_DyeColor");

        private void Awake()
        {
            ForceRefresh();
        }

        private void OnEnable()
        {
            ForceRefresh();
        }

        private void Start()
        {
            // Double-check on start to ensure colors are applied
            ForceRefresh();
        }

        // Removed Update() - colors should persist once applied via MaterialPropertyBlock
        // If colors are lost, the issue is elsewhere (material instantiation, shader changes, etc.)

        private void OnDestroy()
        {
            ClearPropertyBlocks();
        }

        /// <summary>
        /// Store color data from DNA application
        /// Call this after applying DNA to a character
        /// </summary>
        public void StoreColors(Color skinColor, Color hairColor, Color topColor, Color botColor, Color shoeColor)
        {
            this.skinColor = skinColor;
            this.hairColor = hairColor;
            this.topColor = topColor;
            this.botColor = botColor;
            this.shoeColor = shoeColor;
            this.hasStoredColors = true;

#if UNITY_EDITOR
            // Mark as dirty to ensure serialization
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.EditorUtility.SetDirty(gameObject);
            }
#endif

            // Apply immediately
            ReapplyColors();
        }

        /// <summary>
        /// Force refresh colors - reapply from stored data
        /// </summary>
        private void ForceRefresh()
        {
            if (hasStoredColors)
            {
                ReapplyColors();
            }
        }

        /// <summary>
        /// Reapply all stored colors to character renderers
        /// </summary>
        private void ReapplyColors()
        {
            if (!hasStoredColors)
                return;

            Renderer[] renderers = GetComponentsInChildren<Renderer>();

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                string name = renderer.gameObject.name.ToLower();

                // Determine which color to apply based on mesh name
                Color? colorToApply = null;

                // Skin color - body parts and head/face
                if (name.Contains("body_") || name.Contains("head") || name.Contains("face") || name.Contains("_arm") || name.Contains("_leg") || name.Contains("_torso"))
                {
                    colorToApply = skinColor;
                }
                // Hair color - hair, beard, mustache, eyebrows
                else if (name.Contains("hair_") || name.Contains("beard_") || name.Contains("mustache_") || name.Contains("eyebrow"))
                {
                    colorToApply = hairColor;
                }
                // Top clothing color - shirts, vests, coats, hats
                else if (name.Contains("clothing_layer") || name.Contains("shirt") || name.Contains("vest") || name.Contains("coat") || name.Contains("hat"))
                {
                    // Check if it's top or bottom clothing
                    if (name.Contains("shoe") || name.Contains("boot"))
                    {
                        colorToApply = shoeColor;
                    }
                    else if (name.Contains("pant"))
                    {
                        colorToApply = botColor;
                    }
                    else
                    {
                        colorToApply = topColor;
                    }
                }
                // Bottom clothing color - pants, shoes
                else if (name.Contains("pant") || name.Contains("_abs"))
                {
                    colorToApply = botColor;
                }
                else if (name.Contains("shoe") || name.Contains("boot"))
                {
                    colorToApply = shoeColor;
                }

                if (colorToApply.HasValue)
                {
                    ApplyColorToRenderer(renderer, colorToApply.Value);
                }
            }
        }

        /// <summary>
        /// Apply color to a specific renderer using MaterialPropertyBlock
        /// </summary>
        private void ApplyColorToRenderer(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;

            // Get or create property block
            if (!propertyBlocks.ContainsKey(renderer) || propertyBlocks[renderer] == null)
            {
                propertyBlocks[renderer] = new MaterialPropertyBlock();
            }

            MaterialPropertyBlock block = propertyBlocks[renderer];
            renderer.GetPropertyBlock(block);

            // Set color properties - try all common shader property names
            // Don't check if material has property - MaterialPropertyBlock will handle it
            block.SetColor(BaseColorProperty, color);
            block.SetColor(MainColorProperty, color);
            block.SetColor(DyeColorProperty, color);

            renderer.SetPropertyBlock(block);
        }

        /// <summary>
        /// Clear all property blocks
        /// </summary>
        private void ClearPropertyBlocks()
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

        /// <summary>
        /// Public API - Get stored colors for debugging
        /// </summary>
        public (Color skin, Color hair, Color top, Color bot, Color shoe) GetStoredColors()
        {
            return (skinColor, hairColor, topColor, botColor, shoeColor);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Apply color changes immediately in editor
            if (!Application.isPlaying && hasStoredColors)
            {
                ReapplyColors();
            }
        }
#endif
    }
}
