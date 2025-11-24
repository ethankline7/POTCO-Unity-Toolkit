using UnityEngine;

namespace POTCO.Effects
{
    public class CeilingDebrisEffect : POTCOEffect
    {
        public float cardScale = 128.0f;
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 10.0f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("CeilingDebrisParticles");
            
            Material mat = GetMaterialFromParticleMap("particleRockShower");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleGUI"); // Alpha Blend
                mat.SetColor("_Color", new Color(0.3f, 0.2f, 0.0f, 1.0f)); // Brown
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 3.0 +/- 2.5
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 5.5f);
            
            // Size
            // 0.015 * 128 = 1.92
            main.startSize = 1.92f;
            
            main.maxParticles = 64;
            
            // Emission: 0.02s -> 50/sec. Litter 100.
            // 5000/sec? Burst of 100 repeating?
            // Python: BirthRate 0.02.
            // This is continuous. 50 bursts of 100 per second = 5000 particles/sec.
            // Pool size 64. So it will cap immediately.
            // Basically constant rain of rocks.
            var emission = p0.emission;
            emission.rateOverTime = 50f; // Reduced for performance given pool limit
            
            // Shape: DiscEmitter, Radius 100.0
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 100.0f;
            
            // Force (0,0,-5.0) Down
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.y = -5.0f;
            
            // Explicit Launch Vector (0.2, 0, 0) -> Drift X
            // We can use VelocityOverLifetime for constant drift.
            var vel = p0.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = 0.2f * 400.0f; // Launch Vector * Terminal Velocity?
            // Usually explicit launch vector is initial velocity.
            // TerminalVelocity 400 is limit.
            // Let's set startSpeed.
            main.startSpeed = 0.0f;
            // But offset force drives it.
            // And Explicit Launch.
            // Let's add small X drift.
            vel.x = 2.0f; 
        }
    }
}
