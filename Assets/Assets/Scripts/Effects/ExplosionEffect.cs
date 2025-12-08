using UnityEngine;

namespace POTCO.Effects
{
    /// <summary>
    /// Port of Explosion.py
    /// </summary>
    public class ExplosionEffect : POTCOEffect
    {
        [Header("Explosion Settings")]
        public float cardScale = 128.0f;
        public float radius = 8.0f;
        
        private ParticleSystem p0;

        protected override void Start()
        {
            // Explosion is a one-shot sequence
            duration = 2.0f; // Sequence takes about 1.85s
            base.Start();
        }

        public override void StartEffect()
        {
            if (p0 == null) InitializeSystem();
            base.StartEffect();
            
            // Sequence logic:
            // 1. Burst with rate 0.02 (50/sec) for short time
            // 2. Stop emission
            p0.Play();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("ExplosionParticles");
            
            // --- 1. Material ---
            // self.card = model.find('**/pir_t_efx_msc_lavaSplash')
            Material mat = GetMaterialFromParticleMap("pir_t_efx_msc_lavaSplash");
            if (mat != null)
            {
                // Explosion.py uses "ColorBlendAttrib.MAdd" -> Additive
                mat.shader = Shader.Find("EggImporter/ParticleAdditive");
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            // --- 2. Main ---
            var main = p0.main;
            main.loop = false;
            main.duration = 1.0f;
            // Lifespan 1.0 +/- 0.25
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.25f);
            
            // Size
            // Initial 0.05 * 128 = 6.4
            // Final 0.12 * 128 = 15.36
            main.startSize = 6.4f;
            
            main.maxParticles = 16; // PoolSize 16
            
            // --- 3. Emission ---
            // BirthRate 0.01, LitterSize 2 -> 200/sec.
            // BUT track says: setBirthRate 0.02 (50/sec) then wait 0.6s.
            // 50 * 0.6 = 30 particles? But max is 16.
            // Unity Burst is better for explosions.
            var emission = p0.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 16) });

            // --- 4. Shape ---
            // SphereSurfaceEmitter
            // setEmissionType(ETRADIATE) -> Radiate from surface normal?
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            
            // --- 5. Velocity ---
            // OffsetForce (0, 0, 5.0)
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.y = 5.0f; // Up
            
            // Amplitude 2.0 -> Initial velocity outwards
            main.startSpeed = 2.0f;

            // --- 6. Color ---
            // No explicit color interpolation in constructor?
            // renderer.setColor(Vec4(1,1,1,1))
            main.startColor = Color.white;
            
            // --- 7. Size Over Lifetime ---
            // Grow from 6.4 to 15.36 (Ratio ~2.4)
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 1.0f);
            curve.AddKey(1.0f, 2.4f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
            
            // --- 8. Rotation ---
            // AngularVelocity 20.0 +/- 5.0
            var rot = p0.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(15f * Mathf.Deg2Rad, 25f * Mathf.Deg2Rad);
        }
    }
}
