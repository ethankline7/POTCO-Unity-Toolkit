using UnityEngine;
using System.Collections.Generic;

namespace POTCO.VisZones
{
    /// <summary>
    /// Detects which VisZone the player is in via collision triggers
    /// Attach to the player GameObject
    /// </summary>
    public class VisZoneSensor : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("VisZoneManager to notify when zone changes (auto-found if not set)")]
        public VisZoneManager zoneManager;

        [Header("Detection Settings")]
        [Tooltip("Layer mask for zone collision detection")]
        public LayerMask zoneLayer = -1; // All layers by default

        [Header("Current State")]
        [Tooltip("Currently detected zone")]
        [SerializeField]
        private string currentZone = "";

        // Track which zones we're currently overlapping to prevent flipping
        private HashSet<string> overlappingZones = new HashSet<string>();
        private float lastZoneChangeTime = 0f;
        private const float ZONE_CHANGE_COOLDOWN = 0.1f; // Prevent rapid zone switching

        private void Start()
        {
            // Auto-find VisZoneManager if not set
            if (zoneManager == null)
            {
                zoneManager = FindFirstObjectByType<VisZoneManager>();
                if (zoneManager == null)
                {
                    Debug.LogWarning("[VisZoneSensor] No VisZoneManager found in scene!");
                }
            }

            // Detect which zone we're spawning in
            DetectInitialZone();
        }

        /// <summary>
        /// Detect which zone the player is spawning in
        /// </summary>
        private void DetectInitialZone()
        {
            // Check all collision zones to see which one we're inside
            Collider[] overlappingColliders = Physics.OverlapSphere(transform.position, 1f);

            foreach (Collider col in overlappingColliders)
            {
                if (col.gameObject.name.StartsWith("collision_zone_"))
                {
                    string zoneName = ExtractZoneName(col.gameObject.name);
                    overlappingZones.Add(zoneName);

                    // Set the first zone we find as our starting zone
                    if (string.IsNullOrEmpty(currentZone))
                    {
                        EnterZone(zoneName);
                        return;
                    }
                }
            }

            // If we didn't find any zone, force a check with a larger radius
            if (string.IsNullOrEmpty(currentZone))
            {
                overlappingColliders = Physics.OverlapSphere(transform.position, 50f);

                foreach (Collider col in overlappingColliders)
                {
                    if (col.gameObject.name.StartsWith("collision_zone_"))
                    {
                        string zoneName = ExtractZoneName(col.gameObject.name);
                        overlappingZones.Add(zoneName);
                        EnterZone(zoneName);
                        Debug.LogWarning($"[VisZoneSensor] Player spawned outside zone triggers, using nearest zone: {zoneName}");
                        return;
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Check if this is a zone collision trigger
            if (other.gameObject.name.StartsWith("collision_zone_"))
            {
                string zoneName = ExtractZoneName(other.gameObject.name);
                overlappingZones.Add(zoneName);

                // Only switch zones if this is a new zone and we're past the cooldown
                if (currentZone != zoneName && Time.time - lastZoneChangeTime > ZONE_CHANGE_COOLDOWN)
                {
                    EnterZone(zoneName);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // Remove from overlapping zones when we leave
            if (other.gameObject.name.StartsWith("collision_zone_"))
            {
                string zoneName = ExtractZoneName(other.gameObject.name);
                overlappingZones.Remove(zoneName);

                // If we just exited our current zone, check if we should switch to another
                if (zoneName == currentZone)
                {
                    // If we're still overlapping other zones, switch to one of them
                    if (overlappingZones.Count > 0)
                    {
                        // Pick the first overlapping zone
                        foreach (string newZone in overlappingZones)
                        {
                            EnterZone(newZone);
                            break; // Only need the first one
                        }
                    }
                    // If we exited all zones, keep current visibility (props stay until entering new zone)
                    // This is intentional - POTCO zones are large and usually overlap at boundaries
                }
            }
        }

        /// <summary>
        /// Enter a new zone
        /// </summary>
        private void EnterZone(string zoneName)
        {
            if (currentZone == zoneName)
                return;

            string previousZone = currentZone;
            currentZone = zoneName;
            lastZoneChangeTime = Time.time;

            // Notify the zone manager
            if (zoneManager != null)
            {
                zoneManager.SetCurrentZone(zoneName);
            }

            // Log zone transition
            if (string.IsNullOrEmpty(previousZone))
            {
                Debug.Log($"[VisZoneSensor] Initial zone: {zoneName}");
            }
            else
            {
                Debug.Log($"[VisZoneSensor] Zone changed: {previousZone} → {zoneName}");
            }
        }

        /// <summary>
        /// Extract zone name from collision_zone_<name> GameObject
        /// </summary>
        private string ExtractZoneName(string gameObjectName)
        {
            if (gameObjectName.StartsWith("collision_zone_"))
            {
                return gameObjectName.Substring("collision_zone_".Length);
            }
            return gameObjectName;
        }

        /// <summary>
        /// Get current zone name
        /// </summary>
        public string GetCurrentZone() => currentZone;
    }
}
