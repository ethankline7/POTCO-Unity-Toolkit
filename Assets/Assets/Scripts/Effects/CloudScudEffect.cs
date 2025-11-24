using UnityEngine;

namespace POTCO.Effects
{
    public class CloudScudEffect : POTCOEffect
    {
        public float cardScale = 64.0f;
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 15.0f; // Start + Wait(10) + End
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("CloudScudParticles");
            
            Material mat = GetMaterialFromParticleMap("particleSmoke");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleAdditive"); // MAdd
                mat.SetColor("_Color", new Color(1,1,1,0.25f));
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 2.5 +/- 1.5
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 4.0f);
            
            // Size: 2.0 * 64 = 128.0
            main.startSize = 128.0f;
            
            main.maxParticles = 32;
            
            // Emission
            var emission = p0.emission;
            emission.rateOverTime = 250f; // 5 / 0.02
            
            // Shape: Sphere Volume, Radius 1400
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1400.0f;
            
            // Force (1,0,0)
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.x = 1.0f; // Drift
            
            // Size Over Lifetime
            // 2.0 -> 2.2
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 1.0f);
            curve.AddKey(1.0f, 1.1f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
            
            // Color: Fade In/Out
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(0.25f, 0.5f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            col.color = grad;
        }
    }
}
