using UnityEngine;

namespace POTCO.VisZones
{
    /// <summary>
    /// Marker component for VisZone section GameObjects
    /// Each section represents a visibility zone and contains objects assigned to that zone
    /// Named as "Section-<ZoneName>" in the hierarchy
    /// </summary>
    public class VisZoneSection : MonoBehaviour
    {
        [Tooltip("Name of the zone this section represents")]
        public string zoneName;

        [Tooltip("Bounds of the collision zone (calculated from collision_zone_<name>)")]
        public Bounds zoneBounds;

        [Tooltip("Reference to the collision trigger for this zone")]
        public Collider zoneCollider;

        [Tooltip("Is this section currently visible?")]
        [SerializeField]
        private bool isVisible = true;

        // Cache renderers on first hide/show to avoid repeated GetComponentsInChildren calls
        private Renderer[] cachedRenderers;

        // Store original renderer states to preserve character clothing, colliders, etc.
        private System.Collections.Generic.Dictionary<Renderer, bool> originalRendererStates;

        /// <summary>
        /// Show this section (restore renderers to original state, collisions stay active)
        /// Skips renderers marked with PermanentlyHiddenRenderer
        /// </summary>
        public void Show()
        {
            if (!isVisible)
            {
                if (cachedRenderers == null)
                {
                    cachedRenderers = GetComponentsInChildren<Renderer>(true);
                }

                foreach (Renderer renderer in cachedRenderers)
                {
                    if (renderer != null)
                    {
                        // Skip renderers marked as permanently hidden (e.g., pir_m_prp_lev_* objects)
                        if (renderer.GetComponent<PermanentlyHiddenRenderer>() != null)
                        {
                            continue;
                        }

                        // Restore original state if we have it stored, otherwise default to enabled
                        if (originalRendererStates != null && originalRendererStates.TryGetValue(renderer, out bool originalState))
                        {
                            renderer.enabled = originalState;
                        }
                        else
                        {
                            // No stored state - this renderer was probably always visible
                            renderer.enabled = true;
                        }
                    }
                }

                isVisible = true;
            }
        }

        /// <summary>
        /// Hide this section (disable all renderers, collisions stay active)
        /// Stores original renderer states before hiding to preserve character clothing, etc.
        /// Skips renderers marked with PermanentlyHiddenRenderer (already hidden)
        /// </summary>
        public void Hide()
        {
            if (isVisible)
            {
                if (cachedRenderers == null)
                {
                    cachedRenderers = GetComponentsInChildren<Renderer>(true);
                }

                // Initialize state dictionary if first time hiding
                if (originalRendererStates == null)
                {
                    originalRendererStates = new System.Collections.Generic.Dictionary<Renderer, bool>();
                }

                foreach (Renderer renderer in cachedRenderers)
                {
                    if (renderer != null)
                    {
                        // Skip renderers marked as permanently hidden (already disabled)
                        if (renderer.GetComponent<PermanentlyHiddenRenderer>() != null)
                        {
                            continue;
                        }

                        // Store original state before disabling (only if not already stored)
                        if (!originalRendererStates.ContainsKey(renderer))
                        {
                            originalRendererStates[renderer] = renderer.enabled;
                        }

                        renderer.enabled = false;
                    }
                }

                isVisible = false;
            }
        }

        /// <summary>
        /// Check if this section is currently visible
        /// </summary>
        public bool IsVisible => isVisible;
    }
}
