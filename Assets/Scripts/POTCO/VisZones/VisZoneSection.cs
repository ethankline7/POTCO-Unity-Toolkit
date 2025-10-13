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
            // Draw the actual collision shape if available
            if (zoneCollider != null)
            {
                Gizmos.color = Color.cyan;

                if (zoneCollider is MeshCollider meshCol && meshCol.sharedMesh != null)
                {
                    // Draw mesh collider wireframe
                    DrawMeshColliderGizmo(meshCol);
                }
                else if (zoneCollider is BoxCollider boxCol)
                {
                    // Draw box collider
                    DrawBoxColliderGizmo(boxCol);
                }
                else
                {
                    // Fallback: draw collider bounds
                    Bounds bounds = zoneCollider.bounds;
                    Gizmos.DrawWireCube(bounds.center, bounds.size);
                }
            }
            else
            {
                // Fallback: draw zone bounds
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(zoneBounds.center, zoneBounds.size);
            }
        }

        private void DrawMeshColliderGizmo(MeshCollider meshCol)
        {
            Mesh mesh = meshCol.sharedMesh;
            Transform trans = meshCol.transform;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;

            // The extruded mesh has 3 layers, only draw the original (first third of vertices)
            int originalVertCount = vertices.Length / 3;

            // Only draw triangles from the original middle layer
            for (int i = 0; i < triangles.Length / 3; i += 3)
            {
                int idx0 = triangles[i];
                int idx1 = triangles[i + 1];
                int idx2 = triangles[i + 2];

                // Only draw triangles where all vertices are from the original layer
                if (idx0 < originalVertCount && idx1 < originalVertCount && idx2 < originalVertCount)
                {
                    Vector3 v0 = trans.TransformPoint(vertices[idx0]);
                    Vector3 v1 = trans.TransformPoint(vertices[idx1]);
                    Vector3 v2 = trans.TransformPoint(vertices[idx2]);

                    Gizmos.DrawLine(v0, v1);
                    Gizmos.DrawLine(v1, v2);
                    Gizmos.DrawLine(v2, v0);
                }
            }
        }

        private void DrawBoxColliderGizmo(BoxCollider boxCol)
        {
            Transform trans = boxCol.transform;
            Vector3 center = trans.TransformPoint(boxCol.center);
            Vector3 size = Vector3.Scale(boxCol.size, trans.lossyScale);

            // Store original matrix
            Matrix4x4 originalMatrix = Gizmos.matrix;

            // Set gizmo matrix to match box rotation
            Gizmos.matrix = Matrix4x4.TRS(center, trans.rotation, Vector3.one);

            // Draw wire cube
            Gizmos.DrawWireCube(Vector3.zero, size);

            // Restore original matrix
            Gizmos.matrix = originalMatrix;
        }
    }
}
