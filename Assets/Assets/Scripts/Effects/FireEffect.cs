using UnityEngine;

namespace POTCO.Effects
{
    /// <summary>
    /// Port of Fire.py
    /// </summary>
    public class FireEffect : POTCOEffect
    {
        [Header("Fire Settings")]
        public float cardScale = 64.0f;
        public int poolSize = 96;
        
        private ParticleSystem p0;

        protected override void Start()
        {
            base.Start(); // Auto-start
        }

        public override void StartEffect()
        {
            if (p0 == null) InitializeSystem();
            base.StartEffect();
            p0.Play();
        }

        public override void StopEffect()
        {
            base.StopEffect();
            if (p0 != null) p0.Stop();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("FireParticles");
            
            // --- 1. Material ---
            // self.card = model.find('**/particleFire2')
            Material mat = GetMaterialFromParticleMap("particleFire2");
            if (mat != null)
            {
                // Fire.py uses "ColorBlendAttrib.MAdd" which is Additive blending
                // Use custom shader that supports separate AlphaTex
                mat.shader = Shader.Find("EggImporter/ParticleAdditive");
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            // --- 2. Main Module ---
            var main = p0.main;
            main.duration = 10.0f; // self.duration
            main.loop = true;
            // Lifespan 0.75 +/- 0.25 (0.5 to 1.0)
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            // Start Size
            // Python: InitialXScale 0.05 * cardScale(64) * effectScale(1) = 3.2
            // FinalXScale 0.03 * 64 = 1.92
            // Unity particles scale uniformly usually, let's avg X/Y
            // InitialYScale is same as X.
            main.startSize = 3.2f; // Base size
            
            // Python: MassBase 1.0. We use GravityModifier for physics.
            // Python OffsetForce(0,0,15) is basically antigravity/lift.
            main.gravityModifier = -0.5f; // Rough approximation of lift
            main.simulationSpace = ParticleSystemSimulationSpace.World; // self.p0.setLocalVelocityFlag(1) ??? Actually 1 usually means Local.
            // Fire.py says: setLocalVelocityFlag(1). So Local space.
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            main.maxParticles = poolSize;

            // --- 3. Emission ---
            // BirthRate 0.01, LitterSize 4 -> 400/sec
            var emission = p0.emission;
            emission.rateOverTime = 400f;

            // --- 4. Shape (Emitter) ---
            // DiscEmitter, Radius 6.0
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 6.0f * effectScale;
            // Python: setExplicitLaunchVector(Vec3(1, 0, 0)) -> Radiate outwards?
            // DiscEmitter usually radiates from center.
            
            // --- 5. Velocity ---
            // Python: TerminalVelocityBase 400. This acts as a drag/limit.
            var limitVel = p0.limitVelocityOverLifetime;
            limitVel.enabled = true;
            limitVel.limit = 400f;
            
            // Add the OffsetForce (0, 0, 15)
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.z = 15.0f; // Z is up in Panda local space usually? Or Y?
            // In Unity, for a ParticleSystem rotated -90 X (standard up-facing):
            // Local Y is Up (screen Up), Local Z is Forward (screen Depth).
            // Fire usually goes UP.
            // If we didn't rotate the GO, Y is Up.
            force.y = 15.0f; 

            // --- 6. Color Over Lifetime ---
            // addLinear(0.0, 1.0, Vec4(1.0, 0.6, 0.2, 1.0), Vec4(0.5, 0.2, 0.2, 0.5), 1)
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(new Color(1.0f, 0.6f, 0.2f), 0.0f),
                    new GradientColorKey(new Color(0.5f, 0.2f, 0.2f), 1.0f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1.0f, 0.0f), 
                    new GradientAlphaKey(0.5f, 1.0f) 
                }
            );
            col.color = grad;

            // --- 7. Size Over Lifetime ---
            // Initial 3.2 -> Final 1.92. Ratio approx 0.6.
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 1.0f);
            curve.AddKey(1.0f, 0.6f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
            
            // --- 8. Rotation ---
            // setInitialAngle 0, Spread 360
            // enableAngularVelocity(1) -> 350 spread?
            var rot = p0.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-180f * Mathf.Deg2Rad, 180f * Mathf.Deg2Rad); // Random spin
        }
    }
}
