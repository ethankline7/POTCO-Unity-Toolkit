using UnityEngine;

namespace POTCO
{
    /// <summary>
    /// Manages collision mesh generation for world objects.
    /// Can apply colliders to all objects or only to collision-specific objects.
    /// Uses same detection as EGG importer - looks for objects using Collision-Material (invisible shader).
    /// </summary>
    public class WorldCollisionManager : MonoBehaviour
    {
        [Header("Collision Mode")]
        [Tooltip("Apply colliders to all world props")]
        [SerializeField] private bool applyToAllObjects = true;

        [Tooltip("Only apply colliders to objects using Collision-Material (matches EGG importer collision detection)")]
        [SerializeField] private bool onlyCollisionObjects = false;

        [Header("Settings")]
        [Tooltip("Make colliders convex (required for moving colliders, but less accurate)")]
        [SerializeField] private bool useConvexColliders = false;

        [Tooltip("Automatically run on Start()")]
        [SerializeField] private bool autoRunOnStart = true;

        private void Start()
        {
            if (autoRunOnStart)
            {
                ApplyColliders();
            }
        }

        /// <summary>
        /// Apply mesh colliders based on current settings
        /// </summary>
        public void ApplyColliders()
        {
            if (onlyCollisionObjects)
            {
                ApplyCollidersToCollisionObjects();
            }
            else if (applyToAllObjects)
            {
                ApplyCollidersToAllObjects();
            }
        }

        /// <summary>
        /// Apply colliders ONLY to objects that use Collision-Material (invisible collision meshes)
        /// This matches the EGG importer's collision detection system
        /// </summary>
        private void ApplyCollidersToCollisionObjects()
        {
            Debug.Log("🔧 Applying mesh colliders to collision objects only...");

            // Find all MeshRenderers in the scene
            MeshRenderer[] allRenderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            int colliderCount = 0;

            foreach (MeshRenderer renderer in allRenderers)
            {
                // Check if this renderer uses Collision-Material
                bool usesCollisionMaterial = false;
                if (renderer.sharedMaterials != null)
                {
                    foreach (Material mat in renderer.sharedMaterials)
                    {
                        if (mat != null && mat.name.Contains("Collision-Material"))
                        {
                            usesCollisionMaterial = true;
                            break;
                        }
                    }
                }

                if (usesCollisionMaterial)
                {
                    // Skip if already has a collider
                    if (renderer.GetComponent<Collider>() != null)
                        continue;

                    // Get MeshFilter
                    MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        MeshCollider meshCollider = renderer.gameObject.AddComponent<MeshCollider>();
                        meshCollider.sharedMesh = meshFilter.sharedMesh;
                        meshCollider.convex = useConvexColliders;
                        colliderCount++;
                    }
                }
            }

            Debug.Log($"✅ Added {colliderCount} mesh colliders to collision objects");
        }

        /// <summary>
        /// Apply colliders to all ObjectListInfo objects (original behavior)
        /// </summary>
        private void ApplyCollidersToAllObjects()
        {
            Debug.Log("🔧 Applying mesh colliders to all world props...");

            ObjectListInfo[] objectListInfos = FindObjectsByType<ObjectListInfo>(FindObjectsSortMode.None);
            int colliderCount = 0;

            foreach (ObjectListInfo objectInfo in objectListInfos)
            {
                if (objectInfo.GetComponent<Collider>() == null)
                {
                    MeshFilter meshFilter = objectInfo.GetComponent<MeshFilter>();
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        MeshCollider meshCollider = objectInfo.gameObject.AddComponent<MeshCollider>();
                        meshCollider.sharedMesh = meshFilter.sharedMesh;
                        meshCollider.convex = useConvexColliders;
                        colliderCount++;
                    }
                    else
                    {
                        // Try child mesh filters
                        MeshFilter[] childMeshFilters = objectInfo.GetComponentsInChildren<MeshFilter>();
                        foreach (MeshFilter childMeshFilter in childMeshFilters)
                        {
                            if (childMeshFilter.GetComponent<Collider>() == null && childMeshFilter.sharedMesh != null)
                            {
                                MeshCollider meshCollider = childMeshFilter.gameObject.AddComponent<MeshCollider>();
                                meshCollider.sharedMesh = childMeshFilter.sharedMesh;
                                meshCollider.convex = useConvexColliders;
                                colliderCount++;
                            }
                        }
                    }
                }
            }

            Debug.Log($"✅ Added {colliderCount} mesh colliders to world props");
        }

        /// <summary>
        /// Remove all mesh colliders that were added by this system
        /// </summary>
        public void RemoveAllColliders()
        {
            Debug.Log("🧹 Removing all mesh colliders...");

            MeshCollider[] allColliders = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
            int removeCount = 0;

            foreach (MeshCollider collider in allColliders)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
                removeCount++;
            }

            Debug.Log($"✅ Removed {removeCount} mesh colliders");
        }
    }
}
