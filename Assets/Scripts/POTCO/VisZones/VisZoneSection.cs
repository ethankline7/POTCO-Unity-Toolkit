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

        /// <summary>
        /// Show this section (activate all children)
        /// </summary>
        public void Show()
        {
            if (!isVisible)
            {
                gameObject.SetActive(true);
                isVisible = true;
            }
        }

        /// <summary>
        /// Hide this section (deactivate all children)
        /// </summary>
        public void Hide()
        {
            if (isVisible)
            {
                gameObject.SetActive(false);
                isVisible = false;
            }
        }

        /// <summary>
        /// Check if this section is currently visible
        /// </summary>
        public bool IsVisible => isVisible;

        private void OnDrawGizmosSelected()
        {
            // Draw zone bounds in editor for visualization
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(zoneBounds.center, zoneBounds.size);
        }
    }
}
