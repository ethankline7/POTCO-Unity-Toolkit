using UnityEngine;

namespace POTCO.Effects
{
    public class DesolationSmokeEffect : POTCOEffect
    {
        public float cardScale = 64.0f;
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 8.0f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("DesolationSmokeParticles");
            
            Material mat = GetMaterialFromParticleMap("particleWhiteSmoke");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleGUI"); // Alpha Blend
                mat.SetColor("_Color", Color.white);
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 1.5 +/- 0.5
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 2.0f);
            
            // Size: 0.05 -> 0.20 (3.2 -> 12.8)
            main.startSize = 12.8f;
            
            main.maxParticles = 128;
            
            // Force (0,0,-2) Down
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.y = -2.0f;
            
            // Emitter: RingEmitter (Custom)
            // Explicit Launch (5,0,0) -> Tangent?
            // Radius 1.5 -> 22.0 (Reconfigured)
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Circle; // Ring
            shape.radius = 1.5f;
            shape.radiusThickness = 0; // Edge only
            
            // Size Over Lifetime: Grow (4x)
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 0.25f);
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
            // Start: BirthRate 0.01 (100/sec). Litter 8 -> 800/sec.
            // Amplitude 20. Radius 1.5.
            var emission = p0.emission;
            emission.rateOverTime = 800f;
            var main = p0.main;
            main.startSpeed = 20.0f;
            var shape = p0.shape;
            shape.radius = 1.5f;
            
            p0.Play();
            
            yield return new WaitForSeconds(0.1f);
            
            // Reconfigure: BirthRate 0.2 (5/sec). Litter 16 -> 80/sec.
            // Amplitude 3. Radius 22.
            emission.rateOverTime = 80f;
            main.startSpeed = 3.0f;
            shape.radius = 22.0f;
            
            yield return new WaitForSeconds(4.0f);
            
            // End
            emission.rateOverTime = 0;
            
            yield return new WaitForSeconds(2.0f);
            
            StopEffect();
        }
    }
}
