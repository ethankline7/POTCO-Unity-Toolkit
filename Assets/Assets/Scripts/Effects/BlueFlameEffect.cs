using UnityEngine;

namespace POTCO.Effects
{
    public class BlueFlameEffect : POTCOEffect
    {
        public float cardScale = 64.0f;
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 5.0f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("BlueFlameParticles");
            
            Material mat = GetMaterialFromParticleMap("particleFire2");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleAdditive");
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 1.2 +/- 0.5
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.7f);
            
            // Size: 0.0018 * 64 = 0.1152
            main.startSize = 0.1152f;
            
            main.maxParticles = 128;
            
            // Emission: 0.2s -> 5/sec
            var emission = p0.emission;
            emission.rateOverTime = 5f;
            
            // Shape: Sphere Surface, Radius 0.5
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            
            // Force (0,0,4) Up
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.y = 4.0f;
            
            // Size Over Lifetime
            // 0.115 -> 0.0064 (Shrink)
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 1.0f);
            curve.AddKey(1.0f, 0.05f); // Approx ratio
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
            
            // Color: Blue Gradient
            // Start: (0.2, 0.6, 1.0, 1.0) -> End: (0.2, 0.2, 0.6, 0.5)
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.2f, 0.6f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.2f, 0.2f, 0.6f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.5f, 1.0f)
                }
            );
            col.color = grad;
            
            // Track logic: growSize 3.0s
            // Sets Amplitude -2.0*t
            // Sets Scale 0.001*t (Grow)
            // Effectively makes the fire grow/intensify.
            // For simplicity, we'll just let it run at full intensity or add a start delay.
            // The logic seems to fade it IN.
            // We'll use main.startSize multiplier over time?
            // Actually, let's just play it.
        }
    }
}
