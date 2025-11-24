using UnityEngine;

namespace POTCO.Effects
{
    public class BlockShieldEffect : POTCOEffect
    {
        [Header("Block Shield Settings")]
        public Color effectColor = new Color(0.4f, 0.6f, 1.0f, 1.0f);
        
        private GameObject shieldModel;
        private Material mat;

        protected override void Start()
        {
            duration = 0.4f; // uvScroll duration is 0.4
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            // models/effects/pir_m_efx_chr_blockShield
            // Unity: Assets/Resources/phase_4/models/effects/pir_m_efx_chr_blockShield.egg (Guessing phase 4/character effects)
            // Or phase 3?
            // Let's try searching common phases
            
            GameObject prefab = null;
            string[] phases = { "phase_2", "phase_3", "phase_4" };
            foreach(string p in phases)
            {
                prefab = Resources.Load<GameObject>($"{p}/models/effects/pir_m_efx_chr_blockShield");
                if (prefab != null) break;
            }

            if (prefab != null)
            {
                shieldModel = Instantiate(prefab, transform);
                shieldModel.transform.localPosition = Vector3.zero;
                
                Renderer r = shieldModel.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    mat = new Material(r.sharedMaterial);
                    mat.shader = Shader.Find("EggImporter/ParticleAdditive"); // Additive
                    mat.SetColor("_Color", effectColor);
                    // Double sided? ParticleAdditive usually culls off by default or we set it.
                    mat.SetFloat("_Cull", 0); // Off
                    r.material = mat;
                }
            }
            else
            {
                Debug.LogWarning("Could not find 'pir_m_efx_chr_blockShield' model.");
            }
        }

        protected override void Update()
        {
            base.Update();

            if (isPlaying && shieldModel != null && mat != null)
            {
                // 1. Fade Out over 0.2s (Wait 0.2s first)
                // Sequence(Wait(0.2), FadeOut(0.2))
                if (age > 0.2f)
                {
                    float fadeT = (age - 0.2f) / 0.2f;
                    fadeT = Mathf.Clamp01(fadeT);
                    // EaseIn fade out?
                    Color c = Color.Lerp(effectColor, new Color(0,0,0,0), fadeT * fadeT);
                    mat.SetColor("_Color", c);
                }
                else
                {
                    mat.SetColor("_Color", effectColor);
                }
                
                // 2. Scale over 0.2s
                // Start (1,1,0.5) -> End (1,1,1.2)
                if (age <= 0.2f)
                {
                    float scaleT = age / 0.2f;
                    Vector3 startS = new Vector3(1, 1, 0.5f);
                    Vector3 endS = new Vector3(1, 1, 1.2f);
                    shieldModel.transform.localScale = Vector3.Lerp(startS, endS, scaleT);
                }
                
                // 3. UV Scroll over 0.4s
                // 0.0 -> 0.5
                float uvT = age / 0.4f;
                float vOffset = Mathf.Lerp(0.0f, 0.5f, uvT);
                mat.mainTextureOffset = new Vector2(0, vOffset);
                
                // Billboard
                shieldModel.transform.LookAt(Camera.main.transform);
            }
        }
    }
}
