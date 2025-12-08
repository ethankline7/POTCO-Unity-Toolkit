using UnityEngine;

namespace POTCO.Effects
{
    public class EruptionSmokeEffect : POTCOEffect
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
            p0 = SetupParticleSystem("EruptionSmokeParticles");
            
            Material mat = GetMaterialFromParticleMap("particleWhiteSmoke");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleGUI");
                mat.SetColor("_Color", Color.white);
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 8 +/- 2
            main.startLifetime = new ParticleSystem.MinMaxCurve(6.0f, 10.0f);
            
            // Size: 1.5 -> 4.0 (*64) = 96 -> 256
            main.startSize = 256.0f;
            
            main.maxParticles = 16;
            
            // Emission: BirthRate 1.0. Litter 1. -> 1/sec.
            var emission = p0.emission;
            emission.rateOverTime = 1f;
            
            // Shape: Sphere Surface, Radius 60.0
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 60.0f;
            
            // Amplitude 60 (Outward)
            main.startSpeed = 60.0f;
            
            // Force (0,0,60) Up
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.y = 60.0f;
            
            // Size Over Lifetime: Grow
            // 1.5 -> 4.0 (2.6x)
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 0.375f); // 1.5/4.0
            curve.AddKey(1.0f, 1.0f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
            
            // Color: Complex Gradient
            // 0-0.1: Orange/Red Fade In
            // 0.1-0.4: Yellow Hold
            // 0.4-1.0: Yellow -> Black Fade Out
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(new Color(1f, 0.25f, 0f), 0.0f), 
                    new GradientColorKey(new Color(1f, 0.4f, 0.25f), 0.1f),
                    new GradientColorKey(new Color(1f, 0.4f, 0.25f), 0.4f),
                    new GradientColorKey(Color.black, 1.0f)
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(0.0f, 0.0f), 
                    new GradientAlphaKey(1.0f, 0.1f),
                    new GradientAlphaKey(1.0f, 0.4f),
                    new GradientAlphaKey(1.0f, 1.0f)
                }
            );
            col.color = grad;
        }
    }
}
