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
        public Vector2 uvSpeedA = new Vector2(0.2f, 0.2f);

        [Tooltip("UV scroll speed for second normal layer")]
        public Vector2 uvSpeedB = new Vector2(-0.02f, 0.008f);

        [Header("Water Color (Time-Based)")]
        [Tooltip("Enable automatic water color changes based on time of day from SkyboxManager")]
        public bool enableTimeBasedColor = true;

        [Tooltip("Reference to SkyboxManager for time-of-day synchronization")]
        public POTCO.Sky.SkyboxManager skyboxManager;

        [Tooltip("Water color transition speed")]
        [Range(0.1f, 5f)]
        public float colorTransitionSpeed = 1.0f;

        [Header("Water Color Presets")]
        [Tooltip("Water color at dawn (5:00-7:00)")]
        public Color dawnWaterColor = new Color(0.4f, 0.5f, 0.6f, 1f);

        [Tooltip("Water color during day (7:00-16:00)")]
        public Color dayWaterColor = new Color(0.3f, 0.5f, 0.7f, 1f);

        [Tooltip("Water color at sunset (16:00-19:00)")]
        public Color sunsetWaterColor = new Color(0.6f, 0.4f, 0.5f, 1f);

        [Tooltip("Water color at dusk (19:00-21:00)")]
        public Color duskWaterColor = new Color(0.3f, 0.3f, 0.5f, 1f);

        [Tooltip("Water color at night (21:00-5:00)")]
        public Color nightWaterColor = new Color(0.15f, 0.2f, 0.3f, 1f);

        [Header("Manual Water Color")]
        [Tooltip("Manual water color (used when time-based color is disabled)")]
        public Color waterColor = new Color(0.729f, 0.729f, 0.729f, 1f);

        [Header("Gerstner Waves")]
        [Tooltip("Wave parameters for vertex displacement")]
        public Wave[] waves = new Wave[]
        {
            new Wave { amplitude = 0.1f, wavelength = 8f, speed = 0.5f, directionDegrees = 20f },
            new Wave { amplitude = 0.1f, wavelength = 5f, speed = 1.8f, directionDegrees = -30f },
            new Wave { amplitude = 0.1f, wavelength = 2.5f, speed = 0.5f, directionDegrees = 75f }
        };

        private Material[] allOceanMaterials;
        private Color currentWaterColor;
        private Color targetWaterColor;

        void Start()
        {
            CollectAllOceanMaterials();

            // Find SkyboxManager if not assigned
            if (enableTimeBasedColor && skyboxManager == null)
            {
                skyboxManager = FindObjectOfType<POTCO.Sky.SkyboxManager>();
                if (skyboxManager == null)
                {
                    Debug.LogWarning("OceanManager: Time-based color enabled but no SkyboxManager found. Disabling time-based color.");
                    enableTimeBasedColor = false;
                }
            }

            // Initialize current color based on mode
            if (enableTimeBasedColor && skyboxManager != null)
            {
                currentWaterColor = CalculateWaterColorForTime(skyboxManager.timeOfDay);
                Debug.Log($"OceanManager: Time-based color enabled. Initial time: {skyboxManager.timeOfDay:F1}, Color: {currentWaterColor}");
            }
            else
            {
                currentWaterColor = waterColor;
                Debug.Log($"OceanManager: Using manual water color: {waterColor}");
            }
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
            // Calculate water color based on time of day
            if (enableTimeBasedColor && skyboxManager != null)
            {
                targetWaterColor = CalculateWaterColorForTime(skyboxManager.timeOfDay);

                // Smoothly transition to target color
                currentWaterColor = Color.Lerp(currentWaterColor, targetWaterColor, Time.deltaTime * colorTransitionSpeed);

                // Debug log every few seconds
                if (Time.frameCount % 120 == 0)
                {
                    Debug.Log($"OceanManager: Time={skyboxManager.timeOfDay:F1}, Target Color={targetWaterColor}, Current Color={currentWaterColor}");
                }
            }
            else
            {
                // Use manual water color
                currentWaterColor = waterColor;

                // Debug log if time-based is disabled
                if (Time.frameCount % 300 == 0)
                {
                    Debug.Log($"OceanManager: Time-based disabled. enableTimeBasedColor={enableTimeBasedColor}, skyboxManager={(skyboxManager != null ? "found" : "null")}");
                }
            }

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

            // Use time-based color if enabled, otherwise use manual color
            mat.SetColor("_WaterColor", currentWaterColor);

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

        /// <summary>
        /// Calculate water color based on time of day (0-24 hours)
        /// </summary>
        Color CalculateWaterColorForTime(float time)
        {
            // Dawn (5-7): Night to Day transition
            if (time >= 5f && time < 7f)
            {
                float t = (time - 5f) / 2f;
                return Color.Lerp(nightWaterColor, dawnWaterColor, t);
            }
            // Day (7-16): Full daylight water
            else if (time >= 7f && time < 16f)
            {
                float t = (time - 7f) / 9f; // Progress through day
                return Color.Lerp(dawnWaterColor, dayWaterColor, Mathf.Clamp01(t * 2f)); // Transition to bright day color
            }
            // Sunset (16-19): Day to Sunset transition
            else if (time >= 16f && time < 19f)
            {
                float t = (time - 16f) / 3f;
                return Color.Lerp(dayWaterColor, sunsetWaterColor, t);
            }
            // Dusk (19-21): Sunset to Night transition
            else if (time >= 19f && time < 21f)
            {
                float t = (time - 19f) / 2f;
                return Color.Lerp(sunsetWaterColor, duskWaterColor, t);
            }
            // Night (21-5): Dark water
            else
            {
                // Handle wrap-around midnight
                if (time >= 21f)
                {
                    float t = (time - 21f) / 3f; // 21:00 to 00:00
                    return Color.Lerp(duskWaterColor, nightWaterColor, Mathf.Clamp01(t));
                }
                else // time < 5
                {
                    return nightWaterColor;
                }
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
