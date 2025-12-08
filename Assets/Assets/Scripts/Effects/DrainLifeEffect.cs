using UnityEngine;

namespace POTCO.Effects
{
    public class DrainLifeEffect : POTCOEffect
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
            p0 = SetupParticleSystem("DrainLifeParticles");
            
            Material mat = GetMaterialFromParticleMap("effectCandleHalo");
            if (mat != null)
            {
                // ColorBlendAttrib.MAdd -> Additive
                mat.shader = Shader.Find("EggImporter/ParticleAdditive");
                mat.SetColor("_Color", Color.white);
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 2.0
            main.startLifetime = 2.0f;
            
            // Size
            // X: 0.01 -> 0.02 (*64) = 0.64 -> 1.28
            // Y: 0.04 -> 0.04 (*64) = 2.56 -> 2.56
            main.startSize3D = true;
            main.startSizeX = 0.64f;
            main.startSizeY = 2.56f;
            main.startSizeZ = 1.0f;
            
            main.maxParticles = 128;
            
            // Emission: 0.02s -> 50/sec. Litter 1.
            var emission = p0.emission;
            emission.rateOverTime = 50f;
            
            // Shape: DiscEmitter, Radius 0.6
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.6f;
            
            // Force (0,0,6) Up
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.y = 6.0f;
            
            // Size Over Lifetime
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = true;
            
            // X: 0.64 -> 1.28 (2x)
            AnimationCurve xCurve = new AnimationCurve();
            xCurve.AddKey(0.0f, 1.0f);
            xCurve.AddKey(1.0f, 2.0f);
            size.x = new ParticleSystem.MinMaxCurve(1.0f, xCurve);
            
            // Y: 2.56 -> 2.56 (1x)
            // Constant
            
            // Color: Red -> Black(Alpha 0.19)
            // (1,0,0,1) -> (0,0,0,0.19)
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.red, 0.0f), new GradientColorKey(Color.black, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.19f, 1.0f) }
            );
            col.color = grad;
            
            // ForceGroup 'Vortex'? Not easy to replicate with standard particle system.
            // Can use Rotation over Lifetime or Orbital Velocity.
            var vel = p0.velocityOverLifetime;
            vel.enabled = true;
            vel.orbitalZ = 5.0f; // Swirl
        }
        
        public override void StartEffect()
        {
            base.StartEffect();
            StartCoroutine(RunSequence());
        }
        
        private System.Collections.IEnumerator RunSequence()
        {
            var emission = p0.emission;
            emission.rateOverTime = 50f;
            p0.Play();
            
            yield return new WaitForSeconds(1.0f);
            
            emission.rateOverTime = 0;
            
            yield return new WaitForSeconds(7.0f);
            
            StopEffect();
        }
    }
}
