using UnityEngine;

namespace POTCO.Effects
{
    public class DirtClodEffect : POTCOEffect
    {
        public float cardScale = 64.0f;
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 4.0f; // Start + Wait(3.0)
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("DirtClodParticles");
            
            Material mat = GetMaterialFromParticleMap("particleDirtClod");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleGUI"); // Alpha Blend (Alpha User)
                mat.SetColor("_Color", Color.white);
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 3.0
            main.startLifetime = 3.0f;
            
            // Size: 0.04 -> 0.07 (2.56 -> 4.48)
            main.startSize = 4.48f;
            
            main.maxParticles = 32;
            
            // Force (0,0,-20) Down
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.y = -20.0f;
            
            // Shape: DiscEmitter (Cone)
            // Radius 0.6. Outer Angle 45. Inner 0.
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22.5f; // 45 total
            shape.radius = 0.6f;
            
            // Offset Force (0,0,40) Up -> Initial Impulse
            // Use Start Speed instead? Or Burst velocity?
            // Unity Force Over Lifetime is constant.
            // Velocity Over Lifetime can do curves.
            // Python OffsetForce is usually constant acceleration.
            // Net Force: 40 - 20 = 20 Up?
            // If offset force is applied to velocity...
            // Let's set start speed + gravity.
            main.gravityModifier = 2.0f; // ~20
            main.startSpeed = 40.0f; // Initial Up kick
            
            // Size Over Lifetime: Grow
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 0.57f); // 0.04/0.07
            curve.AddKey(1.0f, 1.0f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
        }
        
        public override void StartEffect()
        {
            base.StartEffect();
            StartCoroutine(RunSequence());
        }
        
        private System.Collections.IEnumerator RunSequence()
        {
            // BirthRate 0.03 -> 33/sec. Litter 1.
            var emission = p0.emission;
            emission.rateOverTime = 33f;
            p0.Play();
            
            yield return new WaitForSeconds(0.1f);
            
            // End
            emission.rateOverTime = 0;
            
            yield return new WaitForSeconds(3.0f);
            
            StopEffect();
        }
    }
}
