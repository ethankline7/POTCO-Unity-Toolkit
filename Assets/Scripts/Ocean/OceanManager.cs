using UnityEngine;

namespace POTCO.Ocean
{
    /// <summary>
    /// Manages ocean wave parameters and UV animation, driving the water material.
    /// Mirrors POTCO's SeaPatch UV scale/speed and multi-wave amplitude controls.
    /// </summary>
    public class OceanManager : MonoBehaviour
    {
        [Header("Water Material")]
        [Tooltip("The material used for ocean rendering")]
        public Material waterMaterial;

        [Header("UV Animation")]
        [Tooltip("UV scale for texture tiling")]
        public Vector2 uvScale = new Vector2(0.03f, 0.03f);

        [Tooltip("UV scroll speed for first normal layer")]
        public Vector2 uvSpeedA = new Vector2(0.54f, 0.015f);

        [Tooltip("UV scroll speed for second normal layer")]
        public Vector2 uvSpeedB = new Vector2(-0.02f, 0.008f);

        [Header("Water Color")]
        [Tooltip("Base water color tint (BABABA = 186/186/186 RGB)")]
        public Color waterColor = new Color(0.729f, 0.729f, 0.729f, 1f);

        [Header("Gerstner Waves")]
        [Tooltip("Wave parameters for vertex displacement")]
        public Wave[] waves = new Wave[]
        {
            new Wave { amplitude = 6.22f, wavelength = 8f, speed = 0.5f, directionDegrees = 20f },
            new Wave { amplitude = 0f, wavelength = 5f, speed = 1.8f, directionDegrees = -30f },
            new Wave { amplitude = 3.7f, wavelength = 2.5f, speed = 0.5f, directionDegrees = 75f }
        };

        private Material[] allOceanMaterials;

        void Start()
        {
            CollectAllOceanMaterials();
        }

        void CollectAllOceanMaterials()
        {
            // Find all renderers in children (for OceanGrid patches)
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();

            if (renderers.Length > 0)
            {
                allOceanMaterials = new Material[renderers.Length];
                for (int i = 0; i < renderers.Length; i++)
                {
                    allOceanMaterials[i] = renderers[i].material; // Get material instance
                }
                Debug.Log($"OceanManager: Collected {allOceanMaterials.Length} ocean material instances");
            }
            else if (waterMaterial != null)
            {
                // Fallback: use the single material reference
                allOceanMaterials = new Material[] { waterMaterial };
            }
        }

        /// <summary>
        /// Call this to refresh the material collection (e.g., when OceanGrid adds new patches)
        /// </summary>
        public void RefreshMaterials()
        {
            CollectAllOceanMaterials();
        }

        void Update()
        {
            // Update all ocean materials (for grid-based systems)
            if (allOceanMaterials != null && allOceanMaterials.Length > 0)
            {
                foreach (Material mat in allOceanMaterials)
                {
                    if (mat == null) continue;
                    UpdateMaterial(mat);
                }
            }
            else if (waterMaterial != null)
            {
                // Fallback: update single material
                UpdateMaterial(waterMaterial);
            }
        }

        void UpdateMaterial(Material mat)
        {
            // Set UV animation parameters
            mat.SetVector("_UVScale", uvScale);
            mat.SetVector("_UVSpeedA", uvSpeedA);
            mat.SetVector("_UVSpeedB", uvSpeedB);
            mat.SetFloat("_TimeSec", Time.time);
            mat.SetColor("_WaterColor", waterColor);

            // Set wave parameters
            for (int i = 0; i < waves.Length && i < 4; i++)
            {
                Wave w = waves[i];
                float dirRad = w.directionDegrees * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(dirRad), Mathf.Sin(dirRad));

                // Pack wave data: (amplitude, wavelength, speed, unused)
                mat.SetVector($"_Wave{i}", new Vector4(w.amplitude, w.wavelength, w.speed, 0f));
                mat.SetVector($"_WaveDir{i}", new Vector4(direction.x, direction.y, 0f, 0f));
            }
        }

        [System.Serializable]
        public struct Wave
        {
            [Tooltip("Wave height")]
            public float amplitude;

            [Tooltip("Distance between wave peaks")]
            public float wavelength;

            [Tooltip("Wave movement speed")]
            public float speed;

            [Tooltip("Wave direction in degrees (0 = East, 90 = North)")]
            public float directionDegrees;
        }
    }
}
