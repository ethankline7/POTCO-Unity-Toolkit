using UnityEngine;

namespace POTCO
{
    /// <summary>
    /// Hides level geometry and map objects on play
    /// - pir_m_prp_lev_* objects: Hide mesh, keep collisions
    /// - *_barrier* objects: Hide mesh, keep collisions
    /// - minimap/smiley/water_alpha/water_color objects: Hide completely (disable GameObject)
    /// - Any mesh with no material: Hide mesh, keep collisions
    /// </summary>
    public class HideLevelGeometry : MonoBehaviour
    {
        [Header("Statistics")]
        [SerializeField] private int levelGeometryHidden = 0;
        [SerializeField] private int mapObjectsHidden = 0;
        [SerializeField] private int renderersDisabled = 0;
        [SerializeField] private int noMaterialMeshesHidden = 0;
        [SerializeField] private int barrierMeshesHidden = 0;

        public void HideObjects()
        {
            levelGeometryHidden = 0;
            mapObjectsHidden = 0;
            renderersDisabled = 0;
            noMaterialMeshesHidden = 0;
            barrierMeshesHidden = 0;

            Debug.Log("[HideLevelGeometry] Starting hide process...");

            // Get ALL GameObjects in scene (including inactive ones)
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            int scannedCount = 0;
            int levelGeometryFound = 0;
            int mapObjectsFound = 0;
            int barrierObjectsFound = 0;

            foreach (GameObject obj in allObjects)
            {
                // Skip prefabs and assets (only process scene objects)
                if (obj.scene.name == null) continue;

                scannedCount++;
                string objName = obj.name.ToLower();

                // Category 1: pir_m_prp_lev_* objects (hide mesh, keep collisions)
                if (objName.Contains("pir_m_prp_lev_"))
                {
                    levelGeometryFound++;
                    Debug.Log($"[HideLevelGeometry] Found level geometry: {obj.name} at path: {GetGameObjectPath(obj)}");
                    HideMeshOnly(obj);
                    levelGeometryHidden++;
                }
                // Category 2: _barrier objects (hide mesh, keep collisions)
                else if (objName.Contains("_barrier"))
                {
                    barrierObjectsFound++;
                    Debug.Log($"[HideLevelGeometry] Found barrier object: {obj.name} at path: {GetGameObjectPath(obj)}");
                    HideMeshOnly(obj);
                    barrierMeshesHidden++;
                }
                // Category 3: minimap/smiley/water objects (hide completely)
                else if (objName.Contains("minimap") || objName.Contains("smiley") ||
                         objName.Contains("water_alpha") || objName.Contains("water_color"))
                {
                    mapObjectsFound++;
                    Debug.Log($"[HideLevelGeometry] Found map/water object: {obj.name}");
                    obj.SetActive(false);
                    mapObjectsHidden++;
                }
                // Category 4: Any mesh with no material (hide mesh only)
                else
                {
                    Renderer renderer = obj.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        bool hasNoMaterial = renderer.sharedMaterial == null ||
                                           (renderer.sharedMaterials != null && renderer.sharedMaterials.Length == 0);

                        if (hasNoMaterial)
                        {
                            Debug.Log($"[HideLevelGeometry] Found mesh with no material: {obj.name} at path: {GetGameObjectPath(obj)}");
                            renderer.enabled = false;
                            // Add marker component so VisZones know not to re-enable this
                            if (renderer.GetComponent<VisZones.PermanentlyHiddenRenderer>() == null)
                            {
                                renderer.gameObject.AddComponent<VisZones.PermanentlyHiddenRenderer>();
                            }
                            noMaterialMeshesHidden++;
                        }
                    }
                }
            }

            Debug.Log($"[HideLevelGeometry] Search results:");
            Debug.Log($"  - Level geometry found: {levelGeometryFound}");
            Debug.Log($"  - Barrier objects found: {barrierObjectsFound}");
            Debug.Log($"  - Map/water objects found: {mapObjectsFound}");

            Debug.Log($"[HideLevelGeometry] COMPLETE:");
            Debug.Log($"  - Scanned: {scannedCount} scene objects");
            Debug.Log($"  - Level geometry hidden: {levelGeometryHidden} objects ({renderersDisabled} renderers)");
            Debug.Log($"  - Barrier meshes hidden: {barrierMeshesHidden} objects (collisions kept)");
            Debug.Log($"  - Map/water objects disabled: {mapObjectsHidden} objects (completely hidden)");
            Debug.Log($"  - Meshes with no material hidden: {noMaterialMeshesHidden} objects");
        }

        /// <summary>
        /// Get the full hierarchy path of a GameObject for debugging
        /// </summary>
        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform current = obj.transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        /// <summary>
        /// Hide all renderers on this object and its children, but keep colliders active
        /// Adds PermanentlyHiddenRenderer marker so VisZone system won't re-enable them
        /// Also hides any renderers that have no material assigned
        /// </summary>
        private void HideMeshOnly(GameObject obj)
        {
            int renderersOnThisObject = 0;

            // Disable renderer on the object itself
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                Debug.Log($"[HideLevelGeometry]   - Disabling renderer on: {obj.name}");
                renderer.enabled = false;
                // Add marker component so VisZones know not to re-enable this
                if (renderer.GetComponent<VisZones.PermanentlyHiddenRenderer>() == null)
                {
                    renderer.gameObject.AddComponent<VisZones.PermanentlyHiddenRenderer>();
                }
                renderersDisabled++;
                renderersOnThisObject++;
            }

            // Disable all child renderers
            Renderer[] childRenderers = obj.GetComponentsInChildren<Renderer>(true);
            Debug.Log($"[HideLevelGeometry]   - Found {childRenderers.Length} total renderers in children");

            foreach (Renderer childRenderer in childRenderers)
            {
                if (childRenderer != null)
                {
                    // Check if renderer has no material
                    bool hasNoMaterial = childRenderer.sharedMaterial == null ||
                                        (childRenderer.sharedMaterials != null && childRenderer.sharedMaterials.Length == 0);

                    if (hasNoMaterial)
                    {
                        Debug.Log($"[HideLevelGeometry]   - Child renderer on: {childRenderer.gameObject.name} has no material, hiding it");
                    }
                    else
                    {
                        Debug.Log($"[HideLevelGeometry]   - Child renderer on: {childRenderer.gameObject.name}, enabled: {childRenderer.enabled}");
                    }

                    childRenderer.enabled = false;
                    // Add marker component so VisZones know not to re-enable this
                    if (childRenderer.GetComponent<VisZones.PermanentlyHiddenRenderer>() == null)
                    {
                        childRenderer.gameObject.AddComponent<VisZones.PermanentlyHiddenRenderer>();
                    }
                    renderersDisabled++;
                    renderersOnThisObject++;
                }
            }

            Debug.Log($"[HideLevelGeometry]   - TOTAL disabled on {obj.name}: {renderersOnThisObject} renderers");
        }
    }
}
