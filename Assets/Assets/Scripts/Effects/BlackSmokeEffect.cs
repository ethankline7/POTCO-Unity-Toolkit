using UnityEngine;

namespace POTCO.Effects
{
    public class BlackSmokeEffect : POTCOEffect
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
            p0 = SetupParticleSystem("BlackSmokeParticles");
            
            // Material
            Material mat = GetMaterialFromParticleMap("particleBlackSmoke");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleGUI"); // Alpha Blended
                mat.SetColor("_Color", Color.white); // Keep texture color? Python says (1,1,1,1)
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 2.5 +/- 1.5
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 4.0f);
            
            // Size
            // 0.13 * 64 = 8.32
            main.startSize = 8.32f;
            
            main.maxParticles = 32;
            
            // Emission
            var emission = p0.emission;
            emission.rateOverTime = 8f; // 2 / 0.25
            
            // Shape: Disc, Radius 4
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 4.0f;
            
            // Force (2, 2, 25)
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.x = 2.0f;
            force.z = 2.0f;
            force.y = 25.0f; // Up
            
            // Size Over Lifetime
            // 0.13 -> 0.40 (Ratio ~3.0)
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 1.0f);
            curve.AddKey(1.0f, 3.0f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
            
            // Color: Alpha 0.8 fade out?
            // PRALPHAOUT -> Fade Alpha
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            col.color = grad;
        }
    }
}
