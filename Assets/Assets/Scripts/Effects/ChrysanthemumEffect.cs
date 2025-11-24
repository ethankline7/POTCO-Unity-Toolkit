using UnityEngine;

namespace POTCO.Effects
{
    public class ChrysanthemumEffect : POTCOEffect
    {
        [Header("Chrysanthemum Settings")]
        public Color effectColor = Color.white;
        public float effectScale = 1.0f;

        private GameObject burst1;
        private GameObject burst2;
        private GameObject stars;
        private Material mat1, mat2, starsMat;

        protected override void Start()
        {
            duration = 2.5f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            // Models
            GameObject burstPrefab = Resources.Load<GameObject>("phase_4/models/effects/fireworkBurst_tflip");
            if (burstPrefab == null) burstPrefab = Resources.Load<GameObject>("phase_3/models/effects/fireworkBurst_tflip");
            
            if (burstPrefab != null)
            {
                burst1 = Instantiate(burstPrefab, transform);
                burst2 = Instantiate(burstPrefab, transform);
                SetupMaterial(burst1, out mat1);
                SetupMaterial(burst2, out mat2);
            }

            GameObject starsPrefab = Resources.Load<GameObject>("phase_2/models/effects/fireworkCards");
            if (starsPrefab != null)
            {
                Transform t = FindDeepChild(starsPrefab.transform, "pir_t_efx_msc_fireworkStars_02");
                if (t != null)
                {
                    stars = Instantiate(t.gameObject, transform);
                    SetupMaterial(stars, out starsMat);
                }
            }
        }

        private void SetupMaterial(GameObject go, out Material mat)
        {
            mat = null;
            Renderer r = go.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                mat = new Material(r.sharedMaterial);
                mat.shader = Shader.Find("EggImporter/ParticleAdditive");
                // Start invisible
                mat.SetColor("_Color", new Color(0,0,0,0));
                r.material = mat;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (isPlaying)
            {
                // Burst 1: Scale 200 -> 700 (0.5s), Fade In/Out
                if (burst1 != null)
                {
                    float t1 = Mathf.Clamp01(age / 0.5f);
                    float s1 = Mathf.Lerp(200f, 700f, 1f - Mathf.Pow(1f - t1, 2f)); // EaseOut
                    burst1.transform.localScale = Vector3.one * s1 * effectScale * 0.01f; // Scale down for Unity
                    
                    // Fade Logic: 1.25s. Fade to (Color-1) -> Darker?
                    // Python: fadeColor = effectColor - (0,0,0,1).
                    // Fade from (1,1,0.8,1) -> fadeColor
                    float fadeT = Mathf.Clamp01(age / 1.25f);
                    Color startC = new Color(1f, 1f, 0.8f, 1f);
                    Color endC = new Color(effectColor.r, effectColor.g, effectColor.b, 0f);
                    // EaseIn fade
                    float fadeEase = fadeT * fadeT;
                    if (mat1 != null) mat1.SetColor("_Color", Color.Lerp(startC, endC, fadeEase));
                    
                    burst1.transform.LookAt(Camera.main.transform);
                }

                // Burst 2: Scale 250 -> 720 (1.0s)
                if (burst2 != null)
                {
                    float t2 = Mathf.Clamp01(age / 1.0f);
                    float s2 = Mathf.Lerp(250f, 720f, 1f - Mathf.Pow(1f - t2, 2f));
                    burst2.transform.localScale = Vector3.one * s2 * effectScale * 0.01f;
                    
                    float fadeT2 = Mathf.Clamp01(age / 1.0f);
                    Color startC = new Color(1f, 1f, 0.8f, 1f);
                    Color endC = new Color(effectColor.r, effectColor.g, effectColor.b, 0f);
                    if (mat2 != null) mat2.SetColor("_Color", Color.Lerp(startC, endC, fadeT2 * fadeT2));
                    
                    burst2.transform.LookAt(Camera.main.transform);
                }

                // Stars: Wait 0.4s, Fade In 0.25s, Fade Out 1.0s
                if (stars != null)
                {
                    float starsAge = age - 0.4f;
                    if (starsAge >= 0)
                    {
                        stars.SetActive(true);
                        // Scale: 660 -> 720 (1.5s)
                        float sT = Mathf.Clamp01(starsAge / 1.5f);
                        float s = Mathf.Lerp(660f, 720f, 1f - Mathf.Pow(1f - sT, 2f));
                        stars.transform.localScale = Vector3.one * s * effectScale * 0.01f;
                        
                        Color c = Color.clear;
                        if (starsAge < 0.25f)
                        {
                            // Fade In
                            c = Color.Lerp(new Color(1,1,1,0), effectColor, starsAge / 0.25f);
                        }
                        else if (starsAge < 1.25f)
                        {
                            // Fade Out
                            float fT = (starsAge - 0.25f) / 1.0f;
                            c = Color.Lerp(effectColor, new Color(0,0,0,0), fT * fT);
                        }
                        
                        if (starsMat != null) starsMat.SetColor("_Color", c);
                        stars.transform.LookAt(Camera.main.transform);
                    }
                    else
                    {
                        stars.SetActive(false);
                    }
                }
            }
        }
        
        private Transform FindDeepChild(Transform parent, string name)
        {
            foreach(Transform child in parent)
            {
                if(child.name == name) return child;
                Transform result = FindDeepChild(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
