using UnityEngine;

namespace POTCO.Effects
{
    public class WindEffect : POTCOEffect
    {
        [Header("Wind Settings")]
        public float fadeTime = 0.7f;
        public Vector3 targetScale = new Vector3(2.0f, 2.0f, 2.0f); 
        public Color fadeColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
        public float scrollSpeed = 3.0f; 

        private GameObject windTunnel;
        private System.Collections.Generic.List<Material> materials = new System.Collections.Generic.List<Material>();
        
        private float fadeInDuration;
        private float mainWaitDuration;
        private float fadeOutDuration;
        private float totalDuration;

        protected override void Start()
        {
            // Setup timing
            fadeInDuration = fadeTime / 3.0f;
            mainWaitDuration = fadeTime;
            fadeOutDuration = fadeTime / 3.0f;
            totalDuration = mainWaitDuration + fadeOutDuration;
            
            duration = totalDuration;
            
            base.Start();
        }

        public override void StartEffect()
        {
            if (windTunnel == null) InitializeSystem();
            base.StartEffect();
            
            // Reset state
            if (windTunnel != null)
            {
                windTunnel.SetActive(true);
                windTunnel.transform.localScale = targetScale;
                UpdateColor(new Color(0, 0, 0, 0)); // Start invisible
            }
        }

        public override void StopEffect()
        {
            if (windTunnel != null) windTunnel.SetActive(false);
            base.StopEffect();
        }

        private void InitializeSystem()
        {
            // Load Model
            GameObject prefab = Resources.Load<GameObject>("phase_3/models/effects/wind_tunnel");
            if (prefab != null)
            {
                windTunnel = Instantiate(prefab, transform);
                windTunnel.name = "WindTunnelMesh";
                windTunnel.transform.localScale = new Vector3(2.0f, 2.0f, 1.0f);
                
                // Setup Materials for ALL renderers
                materials.Clear();
                Renderer[] renderers = windTunnel.GetComponentsInChildren<Renderer>();

                foreach (Renderer r in renderers)
                {
                    // Accessing .materials creates instances automatically
                    Material[] mats = r.materials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        mats[i].shader = Shader.Find("EggImporter/ParticleAdditive");
                        materials.Add(mats[i]);
                    }
                    // Assign back to ensure the renderer uses the modified array
                    r.materials = mats;
                }
            }
            else
            {
                Debug.LogWarning("Could not find 'wind_tunnel' model in Resources/phase_3/models/effects/");
            }
        }

        protected override void Update()
        {
            base.Update();

            if (isPlaying && windTunnel != null && materials.Count > 0)
            {
                // 1. UV Scrolling
                float uOffset = (age / fadeTime) * 3.0f;
                Vector2 offset = new Vector2(uOffset, 0);

                // 2. Fading Logic
                Color currentColor = Color.clear;

                if (age < fadeInDuration)
                {
                    float t = age / fadeInDuration;
                    currentColor = Color.Lerp(Color.clear, fadeColor, t);
                }
                else if (age < mainWaitDuration)
                {
                    currentColor = fadeColor;
                }
                else if (age < totalDuration)
                {
                    float t = (age - mainWaitDuration) / fadeOutDuration;
                    currentColor = Color.Lerp(fadeColor, Color.clear, t);
                }
                else
                {
                    currentColor = Color.clear;
                }

                // Apply to all materials
                foreach (var mat in materials)
                {
                    if (mat != null)
                    {
                        mat.mainTextureOffset = offset;
                        if (mat.HasProperty("_TintColor"))
                            mat.SetColor("_TintColor", currentColor);
                        else if (mat.HasProperty("_Color"))
                            mat.SetColor("_Color", currentColor);
                    }
                }
            }
        }

        private void UpdateColor(Color c)
        {
            foreach (var mat in materials)
            {
                if (mat != null)
                {
                    if (mat.HasProperty("_TintColor"))
                        mat.SetColor("_TintColor", c);
                    else if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", c);
                }
            }
        }
    }
}
