using UnityEngine;

namespace POTCO.Effects
{
    public class DarkShipFogEffect : POTCOEffect
    {
        public float cardScale = 64.0f;
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 10.0f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("DarkShipFogParticles");
            
            Material mat = GetMaterialFromParticleMap("particleFlameSmoke");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleGUI"); // Alpha Blend
                // Color Interpolation: Constant Blue-ish (0.45, 0.7, 1.0, 0.75)
                mat.SetColor("_Color", new Color(0.45f, 0.7f, 1.0f, 0.75f));
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 3.0 +/- 2.0
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 5.0f);
            
            // Size
            // X: 3.0 -> 2.0 (* 64) = 192 -> 128
            // Y: 2.5 -> 1.5 (* 64) = 160 -> 96
            // Average Size ~160
            main.startSize = 160.0f;
            
            main.maxParticles = 128;
            
            // Emission: 0.02s -> 50/sec. Litter 10 -> 500/sec.
            var emission = p0.emission;
            emission.rateOverTime = 500f;
            
            // Shape: Sphere Volume, Radius 250
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 250.0f;
            
            // Velocity: Explicit (0.2, 0, 0) -> Drift
            var vel = p0.velocityOverLifetime;
            vel.enabled = true;
            vel.x = 0.2f * 60.0f; // Scale?
            // Python ExplicitLaunchVector is initial velocity direction.
            // Terminal Velocity 400 is limit.
            // Let's set start speed.
            main.startSpeed = 10.0f;
            
            // Size Over Lifetime: Shrink
            // 3.0 -> 2.0 (Ratio 0.66)
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 1.0f);
            curve.AddKey(1.0f, 0.66f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
        }
    }
}
