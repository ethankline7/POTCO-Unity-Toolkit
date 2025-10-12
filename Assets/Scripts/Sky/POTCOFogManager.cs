using UnityEngine;

namespace POTCO.Sky
{
    /// <summary>
    /// Manages fog settings that sync with POTCO skybox time-of-day system.
    /// Attach to the same GameObject as SkyboxManager for automatic integration.
    /// </summary>
    [RequireComponent(typeof(SkyboxManager))]
    public class POTCOFogManager : MonoBehaviour
    {
        [Header("Fog System")]
        [Tooltip("Enable fog rendering")]
        public bool enableFog = true;

        [Tooltip("Fog calculation mode")]
        public FogMode fogMode = FogMode.ExponentialSquared;

        [Header("Fog Preset Library")]
        [Tooltip("Fog settings for day time")]
        public FogSettings dayFog = new FogSettings
        {
            enabled = true,
            color = new Color(0.7f, 0.8f, 0.95f),
            density = 0.0008f,
            linearStart = 50f,
            linearEnd = 800f
        };

        [Tooltip("Fog settings for sunset")]
        public FogSettings sunsetFog = new FogSettings
        {
            enabled = true,
            color = new Color(0.85f, 0.6f, 0.5f),
            density = 0.0012f,
            linearStart = 40f,
            linearEnd = 600f
        };

        [Tooltip("Fog settings for night")]
        public FogSettings nightFog = new FogSettings
        {
            enabled = true,
            color = new Color(0.12f, 0.15f, 0.25f),
            density = 0.0015f,
            linearStart = 30f,
            linearEnd = 500f
        };

        [Tooltip("Fog settings for stars preset")]
        public FogSettings starsFog = new FogSettings
        {
            enabled = true,
            color = new Color(0.08f, 0.1f, 0.18f),
            density = 0.002f,
            linearStart = 20f,
            linearEnd = 400f
        };

        [Tooltip("Fog settings for overcast/stormy weather")]
        public FogSettings overcastFog = new FogSettings
        {
            enabled = true,
            color = new Color(0.5f, 0.55f, 0.6f),
            density = 0.003f,
            linearStart = 20f,
            linearEnd = 300f
        };

        [Header("Manual Overrides")]
        [Tooltip("Override fog color (alpha = 0 means use preset)")]
        public Color colorOverride = new Color(0, 0, 0, 0);

        [Tooltip("Override fog density (0 = use preset)")]
        [Range(0, 0.01f)]
        public float densityOverride = 0f;

        [Tooltip("Override linear fog start distance (0 = use preset)")]
        public float linearStartOverride = 0f;

        [Tooltip("Override linear fog end distance (0 = use preset)")]
        public float linearEndOverride = 0f;

        [Header("Fog Animation")]
        [Tooltip("Enable fog density pulsing for atmospheric effect")]
        public bool enableFogPulse = false;

        [Tooltip("Fog pulse speed")]
        [Range(0, 2)]
        public float pulseSpeed = 0.5f;

        [Tooltip("Fog pulse amplitude (how much density varies)")]
        [Range(0, 0.002f)]
        public float pulseAmplitude = 0.0002f;

        private SkyboxManager skyboxManager;
        private FogSettings currentFog;
        private float pulseTime = 0f;

        void Start()
        {
            // Get reference to SkyboxManager on same GameObject
            skyboxManager = GetComponent<SkyboxManager>();
            if (skyboxManager == null)
            {
                Debug.LogError("POTCOFogManager: SkyboxManager component not found! Fog will not sync with TOD.");
            }

            // Initialize fog
            RenderSettings.fog = enableFog;
            RenderSettings.fogMode = fogMode;

            Debug.Log("POTCOFogManager: Initialized");
        }

        void OnValidate()
        {
            // Apply fog settings in editor for immediate preview
            if (Application.isPlaying)
            {
                RenderSettings.fog = enableFog;
                RenderSettings.fogMode = fogMode;
            }
        }

        void Update()
        {
            if (!enableFog)
            {
                if (RenderSettings.fog)
                {
                    RenderSettings.fog = false;
                }
                return;
            }

            // Ensure fog is enabled
            if (!RenderSettings.fog)
            {
                RenderSettings.fog = true;
            }

            // Ensure fog mode matches
            if (RenderSettings.fogMode != fogMode)
            {
                RenderSettings.fogMode = fogMode;
            }

            // Get current fog settings based on skybox preset/time
            currentFog = GetFogSettingsForCurrentSky();

            // Apply fog settings with optional overrides
            ApplyFogSettings(currentFog);
        }

        /// <summary>
        /// Get fog settings based on current SkyboxManager state
        /// </summary>
        FogSettings GetFogSettingsForCurrentSky()
        {
            if (skyboxManager == null)
            {
                return dayFog; // Fallback
            }

            // Check if skybox is in manual preset mode or automatic time mode
            if (skyboxManager.useManualPreset)
            {
                // Manual preset mode - get fog for current preset
                return GetFogForPreset(skyboxManager.currentPreset);
            }
            else
            {
                // Automatic time mode - interpolate fog based on time of day
                return GetFogForTime(skyboxManager.timeOfDay);
            }
        }

        /// <summary>
        /// Get fog settings for a specific preset
        /// </summary>
        FogSettings GetFogForPreset(SkyboxManager.TODPreset preset)
        {
            switch (preset)
            {
                case SkyboxManager.TODPreset.Day:
                    return dayFog;
                case SkyboxManager.TODPreset.Sunset:
                    return sunsetFog;
                case SkyboxManager.TODPreset.Night:
                    return nightFog;
                case SkyboxManager.TODPreset.Stars:
                    return starsFog;
                case SkyboxManager.TODPreset.Overcast:
                    return overcastFog;
                default:
                    return dayFog;
            }
        }

        /// <summary>
        /// Get fog settings for current time of day with smooth transitions
        /// Mirrors SkyboxManager's time-based transition system
        /// </summary>
        FogSettings GetFogForTime(float time)
        {
            FogSettings from, to;
            float t;

            // Dawn/Sunrise (5-7): Night to Day transition
            if (time >= 5f && time < 7f)
            {
                from = nightFog;
                to = dayFog;
                t = (time - 5f) / 2f;
            }
            // Day (7-16): Use day settings
            else if (time >= 7f && time < 16f)
            {
                return dayFog;
            }
            // Sunset (16-19): Day to Sunset transition
            else if (time >= 16f && time < 19f)
            {
                from = dayFog;
                to = sunsetFog;
                t = (time - 16f) / 3f;
            }
            // Dusk (19-21): Sunset to Night transition
            else if (time >= 19f && time < 21f)
            {
                from = sunsetFog;
                to = nightFog;
                t = (time - 19f) / 2f;
            }
            // Night (21-5): Use night settings
            else
            {
                return nightFog;
            }

            // Lerp between settings
            return LerpFogSettings(from, to, t);
        }

        /// <summary>
        /// Interpolate between two fog settings
        /// </summary>
        FogSettings LerpFogSettings(FogSettings from, FogSettings to, float t)
        {
            return new FogSettings
            {
                enabled = to.enabled,
                color = Color.Lerp(from.color, to.color, t),
                density = Mathf.Lerp(from.density, to.density, t),
                linearStart = Mathf.Lerp(from.linearStart, to.linearStart, t),
                linearEnd = Mathf.Lerp(from.linearEnd, to.linearEnd, t)
            };
        }

        /// <summary>
        /// Apply fog settings to Unity's render settings
        /// </summary>
        void ApplyFogSettings(FogSettings settings)
        {
            if (!settings.enabled)
            {
                RenderSettings.fog = false;
                return;
            }

            // Apply color (with override)
            Color fogColor = colorOverride.a > 0f ? colorOverride : settings.color;
            RenderSettings.fogColor = fogColor;

            // Apply density/distance based on fog mode
            float baseDensity = densityOverride > 0f ? densityOverride : settings.density;

            // Apply fog pulse if enabled
            if (enableFogPulse)
            {
                pulseTime += Time.deltaTime * pulseSpeed;
                float pulse = Mathf.Sin(pulseTime) * pulseAmplitude;
                baseDensity += pulse;
            }

            // Set fog parameters based on mode
            switch (fogMode)
            {
                case FogMode.Linear:
                    float startDist = linearStartOverride > 0f ? linearStartOverride : settings.linearStart;
                    float endDist = linearEndOverride > 0f ? linearEndOverride : settings.linearEnd;
                    RenderSettings.fogStartDistance = startDist;
                    RenderSettings.fogEndDistance = endDist;
                    break;

                case FogMode.Exponential:
                case FogMode.ExponentialSquared:
                    RenderSettings.fogDensity = baseDensity;
                    break;
            }
        }

        /// <summary>
        /// Manually set fog preset (useful for scripted weather events)
        /// </summary>
        public void SetFogPreset(SkyboxManager.TODPreset preset, float transitionDuration = 2f)
        {
            FogSettings targetFog = GetFogForPreset(preset);
            StartCoroutine(TransitionToFog(targetFog, transitionDuration));
        }

        /// <summary>
        /// Transition smoothly to new fog settings
        /// </summary>
        System.Collections.IEnumerator TransitionToFog(FogSettings targetFog, float duration)
        {
            FogSettings startFog = currentFog;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                FogSettings interpolated = LerpFogSettings(startFog, targetFog, t);
                ApplyFogSettings(interpolated);

                yield return null;
            }

            currentFog = targetFog;
        }

        /// <summary>
        /// Quick fog intensity adjustment (for dramatic weather changes)
        /// </summary>
        public void SetFogIntensityMultiplier(float multiplier)
        {
            densityOverride = currentFog.density * multiplier;
        }

        /// <summary>
        /// Reset all overrides to use preset values
        /// </summary>
        [ContextMenu("Reset Overrides")]
        public void ResetOverrides()
        {
            colorOverride = new Color(0, 0, 0, 0);
            densityOverride = 0f;
            linearStartOverride = 0f;
            linearEndOverride = 0f;
            Debug.Log("POTCOFogManager: Reset all overrides");
        }

        [System.Serializable]
        public struct FogSettings
        {
            public bool enabled;
            public Color color;
            public float density;          // For Exponential/ExponentialSquared modes
            public float linearStart;      // For Linear mode
            public float linearEnd;        // For Linear mode
        }
    }
}
