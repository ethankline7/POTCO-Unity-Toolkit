using UnityEngine;

namespace POTCO.Effects
{
    public class CannonSmokeSimpleEffect : POTCOEffect
    {
        private GameObject whiteSmoke;
        private GameObject darkSmoke;
        
        private Material whiteMat;
        private Material darkMat;

        protected override void Start()
        {
            duration = 1.0f; // 0.1 + 0.75 + cleanup
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            GameObject prefab = Resources.Load<GameObject>("phase_2/models/effects/particleMaps");
            if (prefab != null)
            {
                // White Smoke
                Transform white = FindDeepChild(prefab.transform, "particleWhiteSmoke");
                if (white != null)
                {
                    whiteSmoke = Instantiate(white.gameObject, transform);
                    whiteSmoke.transform.localPosition = new Vector3(0, 1, 2.5f); // From python
                    // Scale start 6,6,12
                    whiteSmoke.transform.localScale = new Vector3(6, 6, 12);
                    
                    Renderer r = whiteSmoke.GetComponent<Renderer>();
                    if (r != null)
                    {
                        whiteMat = new Material(r.sharedMaterial);
                        whiteMat.shader = Shader.Find("EggImporter/ParticleAdditive");
                        whiteMat.SetColor("_Color", Color.white);
                        r.material = whiteMat;
                    }
                }
                
                // Dark Smoke
                Transform dark = FindDeepChild(prefab.transform, "particleSmoke");
                if (dark != null)
                {
                    darkSmoke = Instantiate(dark.gameObject, transform);
                    darkSmoke.transform.localPosition = Vector3.zero;
                    // Scale start 6 (Uniform)
                    darkSmoke.transform.localScale = Vector3.one * 6;
                    
                    Renderer r = darkSmoke.GetComponent<Renderer>();
                    if (r != null)
                    {
                        darkMat = new Material(r.sharedMaterial);
                        darkMat.shader = Shader.Find("EggImporter/ParticleGUI"); // Alpha Blend
                        darkMat.SetColor("_Color", new Color(1,1,1,1));
                        r.material = darkMat;
                    }
                }
            }
            
            if (whiteSmoke) whiteSmoke.SetActive(false);
            if (darkSmoke) darkSmoke.SetActive(false);
        }

        public override void StartEffect()
        {
            base.StartEffect();
            if (whiteSmoke) whiteSmoke.SetActive(true);
            if (darkSmoke) darkSmoke.SetActive(true);
        }

        protected override void Update()
        {
            base.Update();
            
            // Wait 0.1s before starting animation (implied by Sequence(Wait(0.1), Func(show)...))
            // Adjust logic to account for this wait.
            float animTime = age - 0.1f;
            
            if (animTime >= 0 && animTime <= 0.75f)
            {
                // Duration 0.75
                float t = animTime / 0.75f;
                
                // Fade Out (ColorScale 1 -> 0)
                // Assuming fading Alpha. Python LerpColorScaleInterval(1,1,1,0)
                float alpha = 1.0f - t;
                Color c = new Color(1, 1, 1, alpha);
                if (whiteMat) whiteMat.SetColor("_Color", c);
                if (darkMat) darkMat.SetColor("_Color", c);
                
                // Scale Blast (White)
                // Start(6,6,12) -> End(10,10,16)
                Vector3 whiteStart = new Vector3(6, 6, 12);
                Vector3 whiteEnd = new Vector3(10, 10, 16);
                if (whiteSmoke) whiteSmoke.transform.localScale = Vector3.Lerp(whiteStart, whiteEnd, t);
                
                // Scale Blast2 (Dark)
                // Start 6 -> End 10
                if (darkSmoke) darkSmoke.transform.localScale = Vector3.one * Mathf.Lerp(6, 10, t);
                
                // Billboarding
                if (whiteSmoke) whiteSmoke.transform.LookAt(Camera.main.transform);
                if (darkSmoke) darkSmoke.transform.LookAt(Camera.main.transform);
            }
            else if (animTime > 0.75f)
            {
                if (whiteSmoke) whiteSmoke.SetActive(false);
                if (darkSmoke) darkSmoke.SetActive(false);
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
