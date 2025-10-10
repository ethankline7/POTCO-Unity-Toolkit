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
        public Vector2 uvScale = new Vector2(0.15f, 0.12f);

        [Tooltip("UV scroll speed for first normal layer")]
        public Vector2 uvSpeedA = new Vector2(0.03f, 0.015f);

        [Tooltip("UV scroll speed for second normal layer")]
        public Vector2 uvSpeedB = new Vector2(-0.02f, 0.008f);

        [Header("Water Color")]
        [Tooltip("Base water color tint (POTCO default: roughly 77/128/179 RGB)")]
        public Color waterColor = new Color(77f/255f, 128f/255f, 179f/255f, 1f);

        [Header("Gerstner Waves")]
        [Tooltip("Wave parameters for vertex displacement")]
        public Wave[] waves = new Wave[]
        {
            new Wave { amplitude = 0.25f, wavelength = 8f, speed = 1.2f, directionDegrees = 20f },
            new Wave { amplitude = 0.15f, wavelength = 5f, speed = 1.8f, directionDegrees = -30f },
            new Wave { amplitude = 0.08f, wavelength = 2.5f, speed = 2.2f, directionDegrees = 75f }
        };

        void Update()
        {
            if (waterMaterial == null) return;

            // Set UV animation parameters
            waterMaterial.SetVector("_UVScale", uvScale);
            waterMaterial.SetVector("_UVSpeedA", uvSpeedA);
            waterMaterial.SetVector("_UVSpeedB", uvSpeedB);
            waterMaterial.SetFloat("_TimeSec", Time.time);
            waterMaterial.SetColor("_WaterColor", waterColor);

            // Set wave parameters
            for (int i = 0; i < waves.Length && i < 4; i++)
            {
                Wave w = waves[i];
                float dirRad = w.directionDegrees * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(dirRad), Mathf.Sin(dirRad));

                // Pack wave data: (amplitude, wavelength, speed, unused)
                waterMaterial.SetVector($"_Wave{i}", new Vector4(w.amplitude, w.wavelength, w.speed, 0f));
                waterMaterial.SetVector($"_WaveDir{i}", new Vector4(direction.x, direction.y, 0f, 0f));
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
