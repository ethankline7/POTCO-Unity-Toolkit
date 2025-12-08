using UnityEngine;
using System.Collections;

namespace POTCO
{
    /// <summary>
    /// Ship health system for AI enemies
    /// Handles damage, sinking animation, and respawn
    /// </summary>
    public class ShipHealth : MonoBehaviour
    {
        [Header("Health Settings")]
        [Tooltip("Maximum health points")]
        public float maxHealth = 1000f;
        [Tooltip("Current health points")]
        public float currentHealth = 1000f;
        [Tooltip("Is this ship currently sinking?")]
        public bool isSinking = false;

        [Header("Sink Animation")]
        [Tooltip("Time in seconds for the sink animation")]
        public float sinkDuration = 5f;
        [Tooltip("How far down the ship sinks (in Unity units)")]
        public float sinkDepth = 20f;
        [Tooltip("Rotation tilt during sinking (degrees)")]
        public float sinkTilt = 15f;

        [Header("Respawn Settings")]
        [Tooltip("Time in seconds before respawning after sinking")]
        public float respawnDelay = 10f;
        [Tooltip("Respawn at original position")]
        public bool respawnAtStart = true;

        // Internal state
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Vector3 respawnPosition;
        private Quaternion respawnRotation;
        private static GUIStyle cachedHealthStyle; // Static to share across all ships

        private void Start()
        {
            currentHealth = maxHealth;
            originalPosition = transform.position;
            originalRotation = transform.rotation;
            respawnPosition = originalPosition;
            respawnRotation = originalRotation;
        }

        /// <summary>
        /// Apply damage to the ship
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (isSinking) return; // Can't damage a sinking ship

            currentHealth -= damage;
            currentHealth = Mathf.Max(0, currentHealth);

            Debug.Log($"{gameObject.name} took {damage} damage! Health: {currentHealth}/{maxHealth}");

            if (currentHealth <= 0)
            {
                StartSinking();
            }
        }

        /// <summary>
        /// Heal the ship
        /// </summary>
        public void Heal(float amount)
        {
            if (isSinking) return;

            currentHealth += amount;
            currentHealth = Mathf.Min(maxHealth, currentHealth);
        }

        /// <summary>
        /// Get current health percentage (0-1)
        /// </summary>
        public float GetHealthPercent()
        {
            return currentHealth / maxHealth;
        }

        /// <summary>
        /// Start the sinking sequence
        /// </summary>
        private void StartSinking()
        {
            if (isSinking) return;

            isSinking = true;
            Debug.Log($"{gameObject.name} is sinking!");

            // Disable AI and movement
            var aiController = GetComponent<ShipAIController>();
            if (aiController != null)
            {
                aiController.enabled = false;
            }

            var shipController = GetComponent<ShipController>();
            if (shipController != null)
            {
                shipController.enabled = false;
            }

            StartCoroutine(SinkSequence());
        }

        /// <summary>
        /// Sinking animation coroutine
        /// </summary>
        private IEnumerator SinkSequence()
        {
            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;

            Vector3 sinkPosition = startPosition - new Vector3(0, sinkDepth, 0);
            Quaternion sinkRotation = startRotation * Quaternion.Euler(Random.Range(-sinkTilt, sinkTilt), 0, Random.Range(-sinkTilt, sinkTilt));

            float elapsed = 0f;

            // Sink animation
            while (elapsed < sinkDuration)
            {
                float t = elapsed / sinkDuration;
                float easedT = EaseInQuad(t); // Accelerate as it sinks

                transform.position = Vector3.Lerp(startPosition, sinkPosition, easedT);
                transform.rotation = Quaternion.Slerp(startRotation, sinkRotation, easedT);

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Make ship invisible
            SetShipVisible(false);

            Debug.Log($"{gameObject.name} has sunk! Respawning in {respawnDelay} seconds...");

            // Wait before respawning
            yield return new WaitForSeconds(respawnDelay);

            // Respawn
            Respawn();
        }

        /// <summary>
        /// Respawn the ship
        /// </summary>
        private void Respawn()
        {
            Debug.Log($"{gameObject.name} is respawning!");

            // Reset health
            currentHealth = maxHealth;
            isSinking = false;

            // Reset position
            if (respawnAtStart)
            {
                transform.position = respawnPosition;
                transform.rotation = respawnRotation;
            }

            // Make ship visible again
            SetShipVisible(true);

            // Re-enable AI
            var aiController = GetComponent<ShipAIController>();
            if (aiController != null)
            {
                aiController.enabled = true;
            }

            var shipController = GetComponent<ShipController>();
            if (shipController != null)
            {
                shipController.enabled = true;
            }

            Debug.Log($"{gameObject.name} has respawned!");
        }

        /// <summary>
        /// Show/hide the ship visually
        /// </summary>
        private void SetShipVisible(bool visible)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = visible;
            }
        }

        /// <summary>
        /// Easing function for smooth acceleration
        /// </summary>
        private float EaseInQuad(float t)
        {
            return t * t;
        }

        /// <summary>
        /// Set custom respawn position
        /// </summary>
        public void SetRespawnPosition(Vector3 position, Quaternion rotation)
        {
            respawnPosition = position;
            respawnRotation = rotation;
        }

        /// <summary>
        /// Debug visualization
        /// </summary>
        private void OnGUI()
        {
            if (isSinking || currentHealth < maxHealth)
            {
                Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 10f);
                if (screenPos.z > 0)
                {
                    // OPTIMIZATION: Initialize style once
                    if (cachedHealthStyle == null)
                    {
                        cachedHealthStyle = new GUIStyle();
                        cachedHealthStyle.alignment = TextAnchor.MiddleCenter;
                        cachedHealthStyle.normal.textColor = Color.red;
                    }

                    // Use cached style
                    GUI.Label(new Rect(screenPos.x - 50, Screen.height - screenPos.y - 10, 100, 20),
                        $"HP: {currentHealth:F0}/{maxHealth:F0}",
                        cachedHealthStyle);
                }
            }
        }
    }
}
