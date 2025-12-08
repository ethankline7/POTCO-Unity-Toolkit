using UnityEngine;

namespace POTCO.Effects
{
    public class CleanseBlastEffect : POTCOEffect
    {
        public float cardScale = 128.0f;
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 5.0f; // Start(1.0) + End(4.0)
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("CleanseBlastParticles");
            
            Material mat = GetMaterialFromParticleMap("particleWhiteSmoke");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleGUI"); // Alpha Blend (PRALPHAOUT)
                mat.SetColor("_Color", new Color(1, 1, 1, 0.8f));
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 2.75 +/- 0.5
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.25f, 3.25f);
            
            // Size
            // 0.016 * 128 = 2.048
            main.startSize = 2.048f;
            
            main.maxParticles = 24;
            
            // Emission: 0.03s -> 33/sec. Litter 3.
            // 100/sec
            var emission = p0.emission;
            emission.rateOverTime = 100f;
            
            // Shape: Sphere Surface, Radius 3.0
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 3.0f;
            
            // Force (0,0,7.0) Gravity -> Up
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.y = 7.0f;
            
            // OffsetForce (0,0,-1.0) -> Down?
            // Combined with gravity 7.0 UP?
            // Gravity usually pulls down (-Z). If Force is +Z, it's Up.
            // Python: LinearVectorForce(0,0,7).
            // Python OffsetForce(0,0,-1).
            // Net: +6 Up.
            // Wait, offsetForce is local. Gravity is world.
            // Unity force local Y is Up.
            force.y = 6.0f; 
            
            // Size Over Lifetime
            // 0.016 -> 0.012 (Shrink slightly) -> No wait, Final is 0.012
            // Y size differs? 0.012 -> 0.02.
            // Assume Uniform average. 0.014 -> 0.016.
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 1.0f);
            curve.AddKey(1.0f, 1.1f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
            
            // Color: Additive blend?
            // ColorBlendAttrib.MAdd.
            // Change material shader.
            p0.GetComponent<ParticleSystemRenderer>().material.shader = Shader.Find("EggImporter/ParticleAdditive");
            
            // Color Interp: White -> Yellowish
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(Color.white, 0.0f), 
                    new GradientColorKey(Color.white, 0.5f),
                    new GradientColorKey(new Color(1f, 0.9f, 0.5f), 1.0f) 
                },
                new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            col.color = grad;
        }
    }
}
