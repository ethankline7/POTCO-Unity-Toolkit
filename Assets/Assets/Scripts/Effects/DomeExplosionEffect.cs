using UnityEngine;

namespace POTCO.Effects
{
    public class DomeExplosionEffect : POTCOEffect
    {
        public float speed = 0.75f;
        public float size = 40.0f;
        
        private GameObject explosion;
        private Material mat;

        protected override void Start()
        {
            duration = 3.0f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            // models/effects/explosion_sphere
            // Assuming phase_3 or phase_4
            GameObject prefab = Resources.Load<GameObject>("phase_4/models/effects/explosion_sphere");
            if (prefab == null) prefab = Resources.Load<GameObject>("phase_3/models/effects/explosion_sphere");
            
            if (prefab != null)
            {
                explosion = Instantiate(prefab, transform);
                explosion.transform.localPosition = Vector3.zero;
                explosion.transform.localScale = Vector3.zero;
                
                Renderer r = explosion.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    mat = new Material(r.sharedMaterial);
                    // ColorBlendAttrib.MAdd -> Additive
                    mat.shader = Shader.Find("EggImporter/ParticleAdditive");
                    mat.SetColor("_Color", new Color(0,0,0,0.65f)); // Alpha 0.65
                    r.material = mat;
                }
            }
        }

        protected override void Update()
        {
            base.Update();
            
            if (isPlaying && explosion != null && mat != null)
            {
                // Sequence: Parallel(ScaleUp(speed), WaitFade(speed*0.5 -> FadeOut(speed*0.5)))
                // ScaleUp: 0 -> size (easeIn)
                // Fade: Hold 0.65 for half speed, then fade to 0 over half speed.
                
                // Scale
                if (age < speed)
                {
                    float t = age / speed;
                    // easeIn
                    float s = Mathf.Lerp(0, size, t * t);
                    explosion.transform.localScale = Vector3.one * s;
                }
                else
                {
                    explosion.transform.localScale = Vector3.one * size;
                }
                
                // Color
                float fadeStart = speed * 0.5f;
                float fadeDuration = speed * 0.5f; // Should match speed?
                // Python: colorScaleInterval(self.speed * 0.5)
                
                if (age < fadeStart)
                {
                    mat.SetColor("_Color", new Color(0,0,0,0.65f));
                }
                else if (age < fadeStart + fadeDuration)
                {
                    float t = (age - fadeStart) / fadeDuration;
                    float alpha = Mathf.Lerp(0.65f, 0.0f, t);
                    mat.SetColor("_Color", new Color(0,0,0,alpha));
                }
                else
                {
                    mat.SetColor("_Color", new Color(0,0,0,0));
                }
            }
        }
    }
}
