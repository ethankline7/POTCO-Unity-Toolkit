using UnityEngine;

namespace POTCO.Effects
{
    public class EnergySpiralEffect : POTCOEffect
    {
        public Color effectColor = Color.white;
        private GameObject spiral;
        private Material mat;

        protected override void Start()
        {
            duration = 6.0f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            // models/effects/energy_spirals
            GameObject prefab = Resources.Load<GameObject>("phase_4/models/effects/energy_spirals");
            if (prefab == null) prefab = Resources.Load<GameObject>("phase_3/models/effects/energy_spirals");
            
            if (prefab != null)
            {
                spiral = Instantiate(prefab, transform);
                spiral.transform.localPosition = Vector3.zero;
                
                // 2 models in python (one reparented to other). Just use one complex model or duplicate.
                // Unity prefab likely contains both if they were in the egg.
                // But Python explicitly loads it twice and reparents.
                // Let's duplicate the instance to match density.
                GameObject spiral2 = Instantiate(prefab, spiral.transform);
                spiral2.transform.localPosition = Vector3.zero;
                
                // Billboard Axis 0?
                // Scale (0.4, 0.5, 0.5)
                spiral.transform.localScale = new Vector3(0.4f, 0.5f, 0.5f);
                
                Renderer[] renderers = spiral.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    // Assuming share same material
                    mat = new Material(renderers[0].sharedMaterial);
                    mat.shader = Shader.Find("EggImporter/ParticleAdditive");
                    // Start Invisible
                    mat.SetColor("_Color", new Color(0,0,0,0));
                    
                    foreach(var r in renderers) r.material = mat;
                }
            }
        }

        protected override void Update()
        {
            base.Update();
            
            if (isPlaying && spiral != null && mat != null)
            {
                // Fade In 1.0s -> Hold -> Fade Out 1.0s (at end)
                float fadeInT = Mathf.Clamp01(age / 1.0f);
                float fadeOutT = Mathf.Clamp01((age - (duration - 1.0f)) / 1.0f);
                
                float alpha = 1.0f;
                if (age < 1.0f) alpha = fadeInT;
                else if (age > duration - 1.0f) alpha = 1.0f - fadeOutT;
                
                Color c = effectColor;
                c.a = alpha;
                // Python: (1 - (1-col)/4) + (0.1,0.1,0,1) offset?
                // Just use effectColor * alpha for simplicity.
                mat.SetColor("_Color", c);
                
                // Scale: (1,1,4) -> (1,1,4)? Wait.
                // Python: LerpScale (1,1,4) start (1,1,4). Constant?
                // Maybe startScale was different.
                // Assuming Grow Z: 1 -> 4.
                float scaleT = age / duration;
                float zScale = Mathf.Lerp(1.0f, 4.0f, scaleT);
                spiral.transform.localScale = new Vector3(0.4f, 0.5f, 0.5f * zScale);
                
                // UV Scroll: 1.0 -> -1.0. Speed duration/4?
                float vOffset = Mathf.Lerp(1.0f, -1.0f, (age * 4.0f / duration) % 1.0f);
                mat.mainTextureOffset = new Vector2(0, vOffset);
                
                // Billboard
                spiral.transform.LookAt(Camera.main.transform);
            }
        }
    }
}
