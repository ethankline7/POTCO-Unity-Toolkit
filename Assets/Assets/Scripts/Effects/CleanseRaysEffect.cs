using UnityEngine;

namespace POTCO.Effects
{
    public class CleanseRaysEffect : POTCOEffect
    {
        private GameObject tubeRays;
        private Material mat;

        protected override void Start()
        {
            duration = 5.5f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            GameObject prefab = Resources.Load<GameObject>("phase_4/models/effects/pir_m_efx_chr_tubeRays");
            if (prefab == null) prefab = Resources.Load<GameObject>("phase_3/models/effects/pir_m_efx_chr_tubeRays");
            
            if (prefab != null)
            {
                tubeRays = Instantiate(prefab, transform);
                tubeRays.transform.localPosition = Vector3.zero;
                // Start Scale (0.8, 0.8, 0.2)
                tubeRays.transform.localScale = new Vector3(0.8f, 0.8f, 0.2f);
                
                Renderer r = tubeRays.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    mat = new Material(r.sharedMaterial);
                    mat.shader = Shader.Find("EggImporter/ParticleAdditive"); // MAdd
                    mat.SetFloat("_Cull", 0); // Double sided
                    r.material = mat;
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            if (isPlaying && tubeRays != null && mat != null)
            {
                float t = Mathf.Clamp01(age / duration);
                
                // Scale: (0.8,0.8,0.2) -> (1.2,1.2,1.6)
                Vector3 startS = new Vector3(0.8f, 0.8f, 0.2f);
                Vector3 endS = new Vector3(1.2f, 1.2f, 1.6f);
                tubeRays.transform.localScale = Vector3.Lerp(startS, endS, t);
                
                // UV Scroll: 1.0 -> -2.0
                float vOffset = Mathf.Lerp(1.0f, -2.0f, t);
                mat.mainTextureOffset = new Vector2(0, vOffset);
                
                // Billboard Axis 0? Usually means Z-up billboard.
                // tubeRays might need to face camera but stay vertical.
                // Assuming standard billboard behavior for now.
                Vector3 target = Camera.main.transform.position;
                target.y = tubeRays.transform.position.y; // Lock Y
                tubeRays.transform.LookAt(target);
            }
        }
    }
}
