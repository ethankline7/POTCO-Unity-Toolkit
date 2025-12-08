using UnityEngine;

namespace POTCO.Effects
{
    public class CurseHitEffect : POTCOEffect
    {
        public float cardScale = 128.0f;
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 8.0f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("CurseHitParticles");
            
            Material mat = GetMaterialFromParticleMap("effectYellowGlowRing");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleAdditive");
                mat.SetColor("_Color", Color.white);
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            main.startLifetime = 4.0f;
            main.startSize = 0.01f * 64.0f; // 0.64
            main.maxParticles = 256;
            
            // Emission: 0.01s -> 100/sec. Litter 5 -> 500/sec.
            var emission = p0.emission;
            emission.rateOverTime = 500f;
            
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 4.0f;
            
            // Explicit Launch Vector (1,0,0.5) -> Radiate?
            main.startSpeed = 1.0f;
            
            // Color: Green/Yellow -> Transparent
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(new Color(0.8f, 1.0f, 0.0f), 0.0f), 
                    new GradientColorKey(Color.black, 1.0f) // Additive black = invisible
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            col.color = grad;
        }
        
        public override void StartEffect()
        {
            base.StartEffect();
            StartCoroutine(RunSequence());
        }
        
        private System.Collections.IEnumerator RunSequence()
        {
            // Start: BirthRate 0.001 (1000/sec)
            var emission = p0.emission;
            emission.rateOverTime = 1000f;
            p0.Play();
            
            yield return new WaitForSeconds(1.0f);
            
            // End
            emission.rateOverTime = 0;
            
            yield return new WaitForSeconds(7.0f);
            
            StopEffect();
        }
    }
}
