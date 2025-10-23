/// <summary>
/// Persists character textures (face, eyes, clothing) through play mode transitions
/// Stores texture names and renderer references, then reapplies them when entering play mode
/// Automatically reapplies textures when entering/exiting play mode
/// </summary>
using System.Collections.Generic;
using UnityEngine;

namespace CharacterOG.Runtime
{
    [ExecuteAlways] // Run in both edit and play mode
    public class CharacterTexturePersistence : MonoBehaviour
    {
        [System.Serializable]
        private class TextureMapping
        {
            public string rendererPath;  // Path to renderer from character root
            public string textureName;   // Texture name to load from Resources
        }

        // Serialized texture mappings - persists through play mode transitions
        [SerializeField, HideInInspector]
        private List<TextureMapping> textureMappings = new List<TextureMapping>();

        [SerializeField, HideInInspector]
        private bool hasStoredTextures = false;

        // Shader property
        private static readonly int MainTexProperty = Shader.PropertyToID("_MainTex");

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
            // Double-check on start to ensure textures are applied
            ForceRefresh();
        }

        /// <summary>
        /// Store texture data from DNA application
        /// Call this after applying DNA to a character
        /// </summary>
        public void StoreTexture(Renderer renderer, string textureName)
        {
            if (renderer == null || string.IsNullOrEmpty(textureName))
                return;

            // Get path from character root to renderer
            string rendererPath = GetRelativePath(renderer.transform);

            // Check if already stored
            foreach (var mapping in textureMappings)
            {
                if (mapping.rendererPath == rendererPath)
                {
                    mapping.textureName = textureName;
                    hasStoredTextures = true;
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        UnityEditor.EditorUtility.SetDirty(this);
                    }
#endif
                    return;
                }
            }

            // Add new mapping
            textureMappings.Add(new TextureMapping
            {
                rendererPath = rendererPath,
                textureName = textureName
            });

            hasStoredTextures = true;

#if UNITY_EDITOR
            // Mark as dirty to ensure serialization
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.EditorUtility.SetDirty(gameObject);
            }
#endif
        }

        /// <summary>
        /// Force refresh textures - reapply from stored data
        /// </summary>
        private void ForceRefresh()
        {
            if (hasStoredTextures && textureMappings.Count > 0)
            {
                ReapplyTextures();
            }
        }

        /// <summary>
        /// Reapply all stored textures to character renderers
        /// </summary>
        private void ReapplyTextures()
        {
            if (!hasStoredTextures || textureMappings.Count == 0)
                return;

            int appliedCount = 0;

            foreach (var mapping in textureMappings)
            {
                // Find renderer by path
                Transform rendererTransform = transform.Find(mapping.rendererPath);
                if (rendererTransform == null)
                {
                    Debug.LogWarning($"[CharacterTexturePersistence] Renderer not found at path: {mapping.rendererPath}");
                    continue;
                }

                Renderer renderer = rendererTransform.GetComponent<Renderer>();
                if (renderer == null)
                {
                    Debug.LogWarning($"[CharacterTexturePersistence] No Renderer component on: {mapping.rendererPath}");
                    continue;
                }

                // Load texture from Resources (search phase_2 through phase_7)
                Texture2D texture = LoadTexture(mapping.textureName);
                if (texture == null)
                {
                    Debug.LogWarning($"[CharacterTexturePersistence] Texture '{mapping.textureName}' not found in Resources");
                    continue;
                }

                // Apply texture using MaterialPropertyBlock (matches MaterialBinder approach)
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetTexture(MainTexProperty, texture);
                renderer.SetPropertyBlock(block);

                appliedCount++;
            }

            if (appliedCount > 0)
            {
                Debug.Log($"[CharacterTexturePersistence] Reapplied {appliedCount} textures to {gameObject.name}");
            }
        }

        /// <summary>
        /// Load texture from Resources (searches phase_2 through phase_7 maps folders)
        /// </summary>
        private Texture2D LoadTexture(string textureName)
        {
            string[] searchPaths = new[]
            {
                $"phase_2/maps/{textureName}",
                $"phase_3/maps/{textureName}",
                $"phase_4/maps/{textureName}",
                $"phase_5/maps/{textureName}",
                $"phase_6/maps/{textureName}",
                $"phase_7/maps/{textureName}"
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
        }

        /// <summary>
        /// Get relative path from this transform to target transform
        /// </summary>
        private string GetRelativePath(Transform target)
        {
            if (target == transform)
                return "";

            List<string> pathParts = new List<string>();
            Transform current = target;

            while (current != null && current != transform)
            {
                pathParts.Insert(0, current.name);
                current = current.parent;
            }

            return string.Join("/", pathParts);
        }

        /// <summary>
        /// Clear all stored textures
        /// </summary>
        public void ClearStoredTextures()
        {
            textureMappings.Clear();
            hasStoredTextures = false;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Apply texture changes immediately in editor
            if (!Application.isPlaying && hasStoredTextures)
            {
                ReapplyTextures();
            }
        }
#endif
    }
}
