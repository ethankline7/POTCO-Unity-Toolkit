using UnityEngine;

namespace POTCO.Effects
{
    public class ConeRaysEffect : POTCOEffect
    {
        [Header("Cone Rays Settings")]
        public Color effectColor = Color.white;
        
        private GameObject coneRays;
        private Material mat;

        protected override void Start()
        {
            duration = 1.7f; // Wait 0.2 + Duration 1.5
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            // models/effects/pir_m_efx_chr_coneRays
            GameObject prefab = Resources.Load<GameObject>("phase_4/models/effects/pir_m_efx_chr_coneRays");
            if (prefab == null) prefab = Resources.Load<GameObject>("phase_3/models/effects/pir_m_efx_chr_coneRays");
            
            if (prefab != null)
            {
                coneRays = Instantiate(prefab, transform);
                coneRays.transform.localPosition = Vector3.zero;
                coneRays.transform.localScale = new Vector3(1, 1, 2.25f); // Start Scale
                
                Renderer r = coneRays.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    mat = new Material(r.sharedMaterial);
                    mat.shader = Shader.Find("EggImporter/ParticleAdditive");
                    mat.SetColor("_Color", new Color(0,0,0,0)); // Start invisible
                    // Billboard axis 0? Assuming Z-up billboard. 
                    // Usually rays face camera but stay upright.
                    // ParticleAdditive handles standard rendering. Billboarding done in Update.
                    r.material = mat;
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            if (isPlaying && coneRays != null && mat != null)
            {
                // Wait 0.2s
                if (age > 0.2f)
                {
                    float t = (age - 0.2f) / 1.5f;
                    t = Mathf.Clamp01(t);
                    
                    // Color: Set to effectColor instantly after wait?
                    // Python: Sequence(Wait(0.2), Func(setColorScale, effectColor), Parallel(...))
                    // So yes, it pops in.
                    mat.SetColor("_Color", effectColor);
                    
                    // UV Scroll: -1.0 -> 1.0
                    float vOffset = Mathf.Lerp(-1.0f, 1.0f, t);
                    mat.mainTextureOffset = new Vector2(0, vOffset);
                    
                    // Scale: (1,1,2.25) -> (2.25, 2.25, 0.25)
                    Vector3 startS = new Vector3(1, 1, 2.25f);
                    Vector3 endS = new Vector3(2.25f, 2.25f, 0.25f);
                    coneRays.transform.localScale = Vector3.Lerp(startS, endS, t);
                    
                    // Billboard
                    Vector3 camPos = Camera.main.transform.position;
                    camPos.y = coneRays.transform.position.y; // Lock Y axis
                    coneRays.transform.LookAt(camPos);
                }
            }
        }
    }
}
