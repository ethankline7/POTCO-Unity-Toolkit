using UnityEngine;

namespace POTCO.Ocean
{
    /// <summary>
    /// Creates an infinite ocean by managing a grid of water patches that follow the camera.
    /// Mirrors POTCO's OceanGrid system that spawns/controls SeaPatch around the player.
    /// </summary>
    public class OceanGrid : MonoBehaviour
    {
        [Header("Grid Setup")]
        [Tooltip("Prefab for individual water patch (should have the ocean material)")]
        public GameObject waterPatchPrefab;

        [Tooltip("Camera to follow (usually main camera)")]
        public Transform followTarget;

        [Tooltip("Size of each water patch in world units")]
        public float patchSize = 100f;

        [Tooltip("Number of patches in each direction from center (3 = 7x7 grid)")]
        [Range(1, 5)]
        public int gridRadius = 3;

        [Header("Optimization")]
        [Tooltip("Update grid position every N frames")]
        [Range(1, 10)]
        public int updateInterval = 3;

        private GameObject[,] patches;
        private Vector2Int currentGridCenter = Vector2Int.zero;
        private int frameCounter = 0;

        void Start()
        {
            // Find main camera if not assigned
            if (followTarget == null)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                    followTarget = mainCam.transform;
            }

            // Create initial grid
            CreateGrid();
        }

        void Update()
        {
            if (followTarget == null) return;

            // Update interval optimization
            frameCounter++;
            if (frameCounter < updateInterval)
                return;
            frameCounter = 0;

            UpdateGridPosition();
        }

        void CreateGrid()
        {
            int gridSize = gridRadius * 2 + 1;
            patches = new GameObject[gridSize, gridSize];

            for (int x = -gridRadius; x <= gridRadius; x++)
            {
                for (int z = -gridRadius; z <= gridRadius; z++)
                {
                    CreatePatch(x, z);
                }
            }
        }

        void CreatePatch(int gridX, int gridZ)
        {
            if (waterPatchPrefab == null)
            {
                Debug.LogError("OceanGrid: waterPatchPrefab is not assigned!");
                return;
            }

            int arrayX = gridX + gridRadius;
            int arrayZ = gridZ + gridRadius;

            // Create patch
            GameObject patch = Instantiate(waterPatchPrefab, transform);
            patch.name = $"WaterPatch_{gridX}_{gridZ}";

            // Position patch
            Vector3 position = new Vector3(
                gridX * patchSize,
                transform.position.y,
                gridZ * patchSize
            );
            patch.transform.position = position;

            // Scale to patch size (assuming prefab is 1x1 unit)
            patch.transform.localScale = new Vector3(patchSize, 1f, patchSize);

            patches[arrayX, arrayZ] = patch;
        }

        void UpdateGridPosition()
        {
            // Calculate which grid cell the camera is in
            Vector3 camPos = followTarget.position;
            Vector2Int newGridCenter = new Vector2Int(
                Mathf.RoundToInt(camPos.x / patchSize),
                Mathf.RoundToInt(camPos.z / patchSize)
            );

            // If grid center hasn't changed, no update needed
            if (newGridCenter == currentGridCenter)
                return;

            // Calculate offset
            Vector2Int offset = newGridCenter - currentGridCenter;

            // Shift patches
            ShiftGrid(offset);

            currentGridCenter = newGridCenter;
        }

        void ShiftGrid(Vector2Int offset)
        {
            int gridSize = gridRadius * 2 + 1;
            GameObject[,] newPatches = new GameObject[gridSize, gridSize];
            bool createdNewPatches = false;

            // Move existing patches to new positions
            for (int x = 0; x < gridSize; x++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    int newX = x - offset.x;
                    int newZ = z - offset.y;

                    // Check if patch stays within grid
                    if (newX >= 0 && newX < gridSize && newZ >= 0 && newZ < gridSize)
                    {
                        newPatches[newX, newZ] = patches[x, z];
                    }
                    else
                    {
                        // Destroy patches that moved out of grid
                        if (patches[x, z] != null)
                            Destroy(patches[x, z]);
                    }
                }
            }

            // Create new patches for empty slots
            for (int x = 0; x < gridSize; x++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    if (newPatches[x, z] == null)
                    {
                        int worldX = (x - gridRadius) + currentGridCenter.x + offset.x;
                        int worldZ = (z - gridRadius) + currentGridCenter.y + offset.y;

                        CreatePatchAt(x, z, worldX, worldZ, ref newPatches);
                        createdNewPatches = true;
                    }
                }
            }

            patches = newPatches;

            // Notify OceanManager to refresh materials if new patches were created
            if (createdNewPatches)
            {
                OceanManager oceanManager = GetComponent<OceanManager>();
                if (oceanManager != null)
                {
                    oceanManager.RefreshMaterials();
                }
            }
        }

        void CreatePatchAt(int arrayX, int arrayZ, int worldX, int worldZ, ref GameObject[,] patchArray)
        {
            if (waterPatchPrefab == null) return;

            GameObject patch = Instantiate(waterPatchPrefab, transform);
            patch.name = $"WaterPatch_{worldX}_{worldZ}";

            Vector3 position = new Vector3(
                worldX * patchSize,
                transform.position.y,
                worldZ * patchSize
            );
            patch.transform.position = position;
            patch.transform.localScale = new Vector3(patchSize, 1f, patchSize);

            patchArray[arrayX, arrayZ] = patch;
        }

        void OnDrawGizmosSelected()
        {
            // Visualize grid in editor
            Gizmos.color = Color.cyan;
            float size = (gridRadius * 2 + 1) * patchSize;
            Vector3 center = new Vector3(
                currentGridCenter.x * patchSize,
                transform.position.y,
                currentGridCenter.y * patchSize
            );
            Gizmos.DrawWireCube(center, new Vector3(size, 0.1f, size));
        }
    }
}
