using UnityEngine;

namespace POTCO.Effects
{
    public class DarkStarEffect : POTCOEffect
    {
        public float cardScale = 128.0f;
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 2.0f; // Start(0.3) + End(1.5)
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("DarkStarParticles");
            
            Material mat = GetMaterialFromParticleMap("particleDarkSmoke");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleAdditive"); // MAdd
                mat.SetColor("_Color", Color.white);
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 0.5
            main.startLifetime = 0.5f;
            
            // Size: Small
            // 0.0001 -> 0.015 (*128) = 0.01 -> 1.9
            // Y: 0.0005 -> 0.035 (*128) = 0.06 -> 4.4
            // Non-uniform size. Unity supports 3D Start Size.
            main.startSize3D = true;
            main.startSizeX = 0.0128f;
            main.startSizeY = 0.064f;
            main.startSizeZ = 1.0f;
            
            main.maxParticles = 64;
            
            // Emission: 0.02s -> 50/sec. Litter 4 -> 200/sec.
            var emission = p0.emission;
            emission.rateOverTime = 200f;
            
            // Shape: Sphere Surface, Radius 0.01
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.01f;
            
            // Force (0,0,0.1) Up
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.y = 0.1f;
            
            // Size Over Lifetime
            // Grow massively.
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = true;
            
            // X: 0.01 -> 1.9 (190x)
            AnimationCurve xCurve = new AnimationCurve();
            xCurve.AddKey(0.0f, 1.0f);
            xCurve.AddKey(1.0f, 190.0f);
            size.x = new ParticleSystem.MinMaxCurve(1.0f, xCurve);
            
            // Y: 0.06 -> 4.4 (73x)
            AnimationCurve yCurve = new AnimationCurve();
            yCurve.AddKey(0.0f, 1.0f);
            yCurve.AddKey(1.0f, 73.0f);
            size.y = new ParticleSystem.MinMaxCurve(1.0f, yCurve);
            
            // Color: White -> Purple/Dark
            // Linear 0->1: White -> (0.5, 0.2, 1.0, 0.4)
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(new Color(0.5f, 0.2f, 1.0f), 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.4f, 1.0f) }
            );
            col.color = grad;
        }
    }
}
