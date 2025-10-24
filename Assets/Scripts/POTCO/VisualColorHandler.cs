using UnityEngine;
using System.Collections.Generic;

namespace POTCO
{
    /// <summary>
    /// Handles Visual color application to materials with proper runtime persistence
    /// Uses MaterialPropertyBlock to avoid material instantiation and serialization issues
    /// </summary>
    [RequireComponent(typeof(ObjectListInfo))]
    [ExecuteAlways] // Run in both edit and play mode
    public class VisualColorHandler : MonoBehaviour
    {
        private ObjectListInfo objectListInfo;
        private Dictionary<Renderer, MaterialPropertyBlock> propertyBlocks = new Dictionary<Renderer, MaterialPropertyBlock>();

        [SerializeField, HideInInspector]
        private Color lastAppliedColor = Color.white;
        [SerializeField, HideInInspector]
        private bool hasLastAppliedColor = false;

        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int MainColorProperty = Shader.PropertyToID("_Color");
        private static readonly int TintColorProperty = Shader.PropertyToID("_TintColor");
        private static readonly int AlbedoProperty = Shader.PropertyToID("_Albedo");

        private void Awake()
        {
            // Skip for preview objects
            if (gameObject.name.Contains("[SURFACE_PREVIEW]") ||
                gameObject.name.Contains("[PREVIEW]"))
            {
                return;
            }

            objectListInfo = GetComponent<ObjectListInfo>();
            // Force apply on awake to ensure colors are set
            ForceRefresh();
        }

        private void OnEnable()
        {
            // Skip for preview objects
            if (gameObject.name.Contains("[SURFACE_PREVIEW]") ||
                gameObject.name.Contains("[PREVIEW]"))
            {
                return;
            }

            objectListInfo = GetComponent<ObjectListInfo>();
            ForceRefresh();
        }

        private void OnDisable()
        {
            // Don't clear property blocks when disabled - keep them for play mode transitions
            // Only clear if the component is being destroyed
        }

        private void OnDestroy()
        {
            // Clear property blocks only when actually destroyed
            ClearPropertyBlocks();
        }

        private void Start()
        {
            // Double-check on start to ensure colors are applied
            ForceRefresh();
        }

        private void Update()
        {
            // Skip update for preview objects to prevent lag
            if (gameObject.name.Contains("[SURFACE_PREVIEW]") ||
                gameObject.name.Contains("[PREVIEW]"))
            {
                return;
            }

            if (objectListInfo == null)
            {
                objectListInfo = GetComponent<ObjectListInfo>();
            }

            // Check if color changed
            bool currentHasColor = objectListInfo?.visualColor.HasValue ?? false;
            Color currentColor = objectListInfo?.visualColor ?? Color.white;

            bool colorChanged = (currentHasColor != hasLastAppliedColor) ||
                               (currentHasColor && currentColor != lastAppliedColor);

            if (colorChanged)
            {
                if (currentHasColor)
                {
                    ApplyVisualColor(currentColor);
                }
                else
                {
                    ClearPropertyBlocks();
                }
                lastAppliedColor = currentColor;
                hasLastAppliedColor = currentHasColor;
            }
        }

        private void ForceRefresh()
        {
            if (objectListInfo != null && objectListInfo.visualColor.HasValue)
            {
                ApplyVisualColor(objectListInfo.visualColor.Value);
                lastAppliedColor = objectListInfo.visualColor.Value;
                hasLastAppliedColor = true;
            }
            else if (hasLastAppliedColor && objectListInfo != null)
            {
                // Reapply the last color if it was lost (play mode transition)
                objectListInfo.visualColor = lastAppliedColor;
                ApplyVisualColor(lastAppliedColor);
            }
        }

        /// <summary>
        /// Apply visual color to all renderers using MaterialPropertyBlock
        /// This avoids material instantiation and preserves the color across play mode
        /// </summary>
        public void ApplyVisualColor(Color color)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();

            foreach (Renderer renderer in renderers)
            {
                // Skip if renderer is a child of a SpawnNode (spawned creatures)
                SpawnNode spawnNode = renderer.GetComponentInParent<SpawnNode>();
                if (spawnNode != null) continue;

                // Skip if renderer is on a child with its own ObjectListInfo (separate object)
                ObjectListInfo childInfo = renderer.GetComponent<ObjectListInfo>();
                if (childInfo != null && childInfo != objectListInfo) continue;

                // Skip if renderer is on a sibling or child that has a VisualColorHandler
                // This prevents parent colors from leaking to independently colored children
                VisualColorHandler childHandler = renderer.GetComponent<VisualColorHandler>();
                if (childHandler != null && childHandler != this) continue;

                // Skip if renderer is descended from another VisualColorHandler
                // Walk up the hierarchy to check if there's a closer color handler
                Transform parent = renderer.transform.parent;
                bool hasCloserHandler = false;
                while (parent != null && parent != transform)
                {
                    if (parent.GetComponent<VisualColorHandler>() != null)
                    {
                        hasCloserHandler = true;
                        break;
                    }
                    parent = parent.parent;
                }
                if (hasCloserHandler) continue;

                // Get or create MaterialPropertyBlock for this renderer
                if (!propertyBlocks.ContainsKey(renderer) || propertyBlocks[renderer] == null)
                {
                    propertyBlocks[renderer] = new MaterialPropertyBlock();
                }

                MaterialPropertyBlock block = propertyBlocks[renderer];
                renderer.GetPropertyBlock(block);

                // Set color properties - try all common shader property names
                block.SetColor(BaseColorProperty, color);
                block.SetColor(MainColorProperty, color);
                block.SetColor(TintColorProperty, color);
                block.SetColor(AlbedoProperty, color);

                renderer.SetPropertyBlock(block);
            }

            lastAppliedColor = color;
            hasLastAppliedColor = true;

#if UNITY_EDITOR
            // Mark the object as dirty in editor to ensure serialization
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.EditorUtility.SetDirty(gameObject);
            }
#endif
        }

        /// <summary>
        /// Clear all property blocks to restore original materials
        /// </summary>
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
            hasLastAppliedColor = false;
        }

        /// <summary>
        /// Force refresh the visual color application
        /// </summary>
        public void RefreshVisualColor()
        {
            if (objectListInfo != null && objectListInfo.visualColor.HasValue)
            {
                ApplyVisualColor(objectListInfo.visualColor.Value);
            }
        }

#if UNITY_EDITOR
        // Editor-specific validation
        private void OnValidate()
        {
            if (objectListInfo == null)
            {
                objectListInfo = GetComponent<ObjectListInfo>();
            }

            // Apply color changes immediately in editor
            if (!Application.isPlaying && objectListInfo != null)
            {
                bool currentHasColor = objectListInfo.visualColor.HasValue;
                Color currentColor = objectListInfo.visualColor ?? Color.white;

                bool colorChanged = (currentHasColor != hasLastAppliedColor) ||
                                   (currentHasColor && currentColor != lastAppliedColor);

                if (colorChanged)
                {
                    if (currentHasColor)
                    {
                        ApplyVisualColor(currentColor);
                    }
                    else
                    {
                        ClearPropertyBlocks();
                    }
                    lastAppliedColor = currentColor;
                    hasLastAppliedColor = currentHasColor;
                }
            }
        }
#endif
    }
}