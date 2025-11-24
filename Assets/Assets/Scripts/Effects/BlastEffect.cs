using UnityEngine;

namespace POTCO.Effects
{
    public class BlastEffect : POTCOEffect
    {
        [Header("Blast Settings")]
        public float fadeTime = 0.15f;
        public Color effectColor = Color.white;
        public float startScale = 1.0f;
        public float endScale = 4.0f;

        private GameObject blastCard;
        private Material mat;

        protected override void Start()
        {
            duration = fadeTime + 0.1f; // Slightly longer to ensure finish
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            // Load particleCards
            GameObject prefab = Resources.Load<GameObject>("phase_2/models/effects/particleCards");
            if (prefab != null)
            {
                Transform t = FindDeepChild(prefab.transform, "particleBlast");
                if (t != null)
                {
                    blastCard = Instantiate(t.gameObject, transform);
                    blastCard.transform.localPosition = Vector3.zero;
                    blastCard.transform.localRotation = Quaternion.identity;
                    blastCard.transform.localScale = Vector3.one * startScale;
                    
                    Renderer r = blastCard.GetComponent<Renderer>();
                    if (r != null)
                    {
                        mat = new Material(r.sharedMaterial);
                        mat.shader = Shader.Find("EggImporter/ParticleAdditive");
                        mat.SetColor("_Color", effectColor);
                        r.material = mat;
                    }
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            if (isPlaying && blastCard != null && mat != null)
            {
                float t = age / fadeTime;
                
                if (t <= 1.0f)
                {
                    // Scale: EaseIn (Quadratic)
                    float scaleT = t * t; 
                    float currentScale = Mathf.Lerp(startScale, endScale, scaleT);
                    blastCard.transform.localScale = Vector3.one * currentScale;
                    
                    // Color/Fade: EaseOut (Inverse Quadratic)
                    // 1 - (1-t)^2
                    float fadeT = 1.0f - (1.0f - t) * (1.0f - t);
                    // Fade from effectColor to Transparent
                    Color c = Color.Lerp(effectColor, new Color(0,0,0,0), fadeT);
                    mat.SetColor("_Color", c);
                    
                    // Billboard
                    blastCard.transform.LookAt(Camera.main.transform);
                }
                else
                {
                    blastCard.SetActive(false);
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
