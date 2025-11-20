using UnityEngine;

namespace POTCO
{
    /// <summary>
    /// Cannonball projectile component
    /// Handles collision, destruction, and visual effects
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CannonProjectile : MonoBehaviour
    {
        [Header("Projectile Settings")]
        [Tooltip("Lifetime before auto-destruction (seconds)")]
        public float lifetime = 10f;
        [Tooltip("Damage dealt on impact")]
        public float damage = 50f;
        [Tooltip("Explosion radius for area damage")]
        public float explosionRadius = 2f;

        [Header("Effects")]
        [Tooltip("Explosion effect prefab (optional)")]
        public GameObject explosionPrefab;
        [Tooltip("Trail effect (optional)")]
        public TrailRenderer trail;

        private float spawnTime;
        private Rigidbody rb;

        private void Start()
        {
            spawnTime = Time.time;
            rb = GetComponent<Rigidbody>();

            // Add sphere collider if not present
            if (GetComponent<Collider>() == null)
            {
                SphereCollider collider = gameObject.AddComponent<SphereCollider>();
                collider.radius = 0.15f; // Standard cannonball size
            }

            // Ensure Rigidbody is configured correctly
            if (rb != null)
            {
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.useGravity = true;
            }

            // Try to load explosion effect if not assigned
            if (explosionPrefab == null)
            {
                GameObject loadedEffect = Resources.Load<GameObject>("phase_3/models/effects/cannonballExplosion-zero");
                if (loadedEffect != null)
                {
                    explosionPrefab = loadedEffect;
                }
            }
        }

        private void Update()
        {
            // Auto-destroy after lifetime
            if (Time.time >= spawnTime + lifetime)
            {
                DestroySelf(false);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Impact position
            Vector3 impactPoint = collision.contacts[0].point;

            Debug.Log($"💥 Cannonball hit: {collision.gameObject.name} at {impactPoint}");

            // Spawn explosion effect
            if (explosionPrefab != null)
            {
                GameObject explosion = Instantiate(explosionPrefab, impactPoint, Quaternion.identity);
                Destroy(explosion, 3f); // Clean up after 3 seconds
            }

            // Area damage - apply damage to ships
            Collider[] hitColliders = Physics.OverlapSphere(impactPoint, explosionRadius);
            foreach (Collider hitCollider in hitColliders)
            {
                if (hitCollider.gameObject == gameObject) continue;

                // Check for ShipHealth component (either on this object or parent)
                ShipHealth shipHealth = hitCollider.GetComponentInParent<ShipHealth>();
                if (shipHealth != null)
                {
                    shipHealth.TakeDamage(damage);
                    Debug.Log($"   - Explosion hit ship: {shipHealth.gameObject.name} - dealt {damage} damage");
                }
                else
                {
                    Debug.Log($"   - Explosion hit: {hitCollider.gameObject.name} (no ShipHealth component)");
                }
            }

            // Destroy cannonball
            DestroySelf(true);
        }

        private void DestroySelf(bool wasImpact)
        {
            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            // Draw explosion radius
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
