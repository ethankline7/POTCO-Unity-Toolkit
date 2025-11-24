using UnityEngine;

namespace POTCO.Effects
{
    public class DesolationChargeSmokeEffect : POTCOEffect
    {
        public float cardScale = 64.0f;
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 4.0f; // Start + Wait(2.0) + End
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("DesolationChargeParticles");
            
            Material mat = GetMaterialFromParticleMap("particleWhiteSmoke");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleGUI"); // Alpha Blend
                mat.SetColor("_Color", Color.white);
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 1.5 +/- 0.25
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.25f, 1.75f);
            
            // Size
            // 0.001 -> 0.01 (*64) = 0.06 -> 0.6
            main.startSize = 0.6f;
            
            main.maxParticles = 256;
            
            // Emission: 0.01s -> 100/sec. Litter 2 -> 200/sec.
            var emission = p0.emission;
            emission.rateOverTime = 200f;
            
            // Shape: Sphere Volume, Radius 0.1 (Small point)
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
            
            // Amplitude 0.25 -> Outward velocity
            main.startSpeed = 0.25f;
            
            // Size Over Lifetime: Grow (10x)
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 0.1f);
            curve.AddKey(1.0f, 1.0f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
            
            // Color: Fade Out Alpha 0.25
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.25f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
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
            var emission = p0.emission;
            emission.rateOverTime = 200f;
            p0.Play();
            
            yield return new WaitForSeconds(2.0f);
            
            emission.rateOverTime = 0;
            
            yield return new WaitForSeconds(2.0f);
            
            StopEffect();
        }
    }
}
