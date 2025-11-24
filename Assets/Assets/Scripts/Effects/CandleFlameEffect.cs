using UnityEngine;

namespace POTCO.Effects
{
    public class CandleFlameEffect : POTCOEffect
    {
        private GameObject glow;
        private GameObject halo;

        protected override void Start()
        {
            duration = Mathf.Infinity; // Loops indefinitely
            loop = true;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            // Load candleFlame
            GameObject glowPrefab = Resources.Load<GameObject>("phase_3/models/effects/candleFlame");
            if (glowPrefab != null)
            {
                glow = Instantiate(glowPrefab, transform);
                glow.transform.localPosition = new Vector3(0, 0, 0.15f); // Python z=0.15
                SetupMaterial(glow);
            }

            // Load candleHalo
            GameObject haloPrefab = Resources.Load<GameObject>("phase_2/models/effects/candleHalo");
            if (haloPrefab != null)
            {
                halo = Instantiate(haloPrefab, transform);
                halo.transform.localPosition = new Vector3(0, 0, 0.15f);
                SetupMaterial(halo);
            }
        }

        private void SetupMaterial(GameObject go)
        {
            Renderer r = go.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                Material mat = new Material(r.sharedMaterial);
                mat.shader = Shader.Find("EggImporter/ParticleAdditive");
                mat.SetColor("_Color", Color.white);
                r.material = mat;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (isPlaying)
            {
                float t = Time.time;

                // Glow Animation (Randomized Scale)
                // Python: 0.1s + random/10.
                // To replicate exactly is hard without coroutines for randomness.
                // We'll use Sine/Perlin noise for continuous flickering.
                
                // Base: 1.1 -> 1.0.
                float noise = Mathf.PerlinNoise(t * 10.0f, 0);
                float scale = Mathf.Lerp(1.0f, 1.3f, noise); // 1.0 to 1.3 (roughly)
                
                if (glow != null)
                {
                    glow.transform.localScale = new Vector3(scale, scale, scale * 1.2f); // Stretch Z slightly
                    glow.transform.LookAt(Camera.main.transform);
                }

                // Halo Animation
                // 2.0 -> 2.2
                float haloNoise = Mathf.PerlinNoise(t * 5.0f, 100);
                float haloScale = Mathf.Lerp(2.0f, 2.2f, haloNoise);
                
                if (halo != null)
                {
                    halo.transform.localScale = Vector3.one * haloScale;
                    halo.transform.LookAt(Camera.main.transform);
                }
            }
        }
    }
}
