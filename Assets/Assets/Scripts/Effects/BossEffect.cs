using UnityEngine;

namespace POTCO.Effects
{
    public class BossEffect : POTCOEffect
    {
        public float cardScale = 64.0f;
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 5.0f; // Start(3.0) + End(2.0)
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("BossParticles");
            
            // Material: particleSparkles
            Material mat = GetMaterialFromParticleMap("particleSparkles");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleAdditive");
                mat.SetColor("_Color", Color.white);
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 1.0 +/- 0.25
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.25f);
            
            // Size
            // 0.04 * 64 = 2.56
            main.startSize = 2.56f;
            
            main.maxParticles = 16;
            
            // Emission: 0.5s -> 2/sec? No, BirthRate 0.5 means 1 particle every 0.5s.
            // LitterSize 4.
            // Rate = 4 / 0.5 = 8 particles/sec.
            var emission = p0.emission;
            emission.rateOverTime = 8f;
            
            // Shape: Sphere Volume
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.0f;
            
            // Force (0,0,0.2) Up
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.y = 0.2f;
            
            // Size Over Lifetime: Grow
            // 0.04 -> 0.08 (Double)
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 1.0f);
            curve.AddKey(1.0f, 2.0f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
            
            // Color: Alpha 1.0 -> 1.0 (No fade?)
            // Python: PRALPHAINOUT? Alpha In/Out usually means Fade In -> Hold -> Fade Out.
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(0.0f, 0.0f), 
                    new GradientAlphaKey(1.0f, 0.1f),
                    new GradientAlphaKey(1.0f, 0.9f),
                    new GradientAlphaKey(0.0f, 1.0f) 
                }
            );
            col.color = grad;
        }
    }
}
