using UnityEngine;

namespace POTCO.Effects
{
    public class CannonBlastSmokeEffect : POTCOEffect
    {
        public float cardScale = 64.0f;
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 3.0f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("CannonBlastParticles");
            
            // Material: particleWhiteSmoke
            Material mat = GetMaterialFromParticleMap("particleWhiteSmoke");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleGUI"); // Alpha Blended (PRALPHAOUT)
                // PPNOBLEND in Python means it overwrites?
                // Usually smoke is alpha blended.
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 1.25 +/- 0.25
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.5f);
            
            // Size
            // Increased size for better visibility (was 0.64)
            main.startSize = 1.5f;
            
            main.maxParticles = 8;
            
            // Emission: 0.02s -> 50/sec. Litter 6.
            // 300/sec? Max 8. So basically instant burst.
            var emission = p0.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 8) });
            
            // Shape: LineEmitter (0,0,0) to (0,6,0)
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Cone; // Cone is best approximation for directional spread
            shape.angle = 0; // Straight line?
            shape.radius = 0.1f; // Thin
            // Python: Endpoint1(0,0,0) Endpoint2(0,6,0).
            // We can use a Box shape scaled? Or just emit from volume.
            // Or Cone with height.
            // Let's use shape position offset logic if needed, but Cone is fine.
            
            // Force (0,4,1)
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.y = 1.0f;
            force.z = 4.0f; // Forward/Up?
            
            // Size Over Lifetime: Grow
            // 0.64 -> 6.4 (10x)
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 1.0f);
            curve.AddKey(1.0f, 10.0f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
            
            // Color: Fade Out
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            col.color = grad;
        }
    }
}
