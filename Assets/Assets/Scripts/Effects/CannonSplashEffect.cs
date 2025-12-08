using UnityEngine;

namespace POTCO.Effects
{
    public class CannonSplashEffect : POTCOEffect
    {
        public float cardScale = 64.0f;
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 5.0f; // Sequence: Start + Wait(0.3) + End(4.0)
            InitializeSystem();
            base.Start();
        }

        public override void StartEffect()
        {
            if (p0 == null) InitializeSystem();
            base.StartEffect();
            
            // Sound? Random splash sfx.
            
            StartCoroutine(RunSequence());
        }

        private System.Collections.IEnumerator RunSequence()
        {
            // BirthRate 0.05
            // 5 Particles per 0.05s? No, 1 particle every 0.05s.
            // Litter 5. So 100/sec.
            var emission = p0.emission;
            emission.rateOverTime = 100f;
            p0.Play();
            
            yield return new WaitForSeconds(0.3f);
            
            emission.rateOverTime = 0;
            
            yield return new WaitForSeconds(4.0f);
            
            StopEffect();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("CannonSplashParticles");
            
            Material mat = GetMaterialFromParticleMap("particleSplash");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleGUI"); // Alpha Blend
                // PPNOBLEND in python? "No Blend" usually means Overwrite or AlphaTest.
                // But splash is usually transparent. Assuming Alpha Blend for now.
                mat.SetColor("_Color", Color.white);
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 3.0 +/- 1.0
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.0f, 4.0f);
            
            // Size
            // 0.10 * 64 = 6.4
            main.startSize = 6.4f;
            
            main.maxParticles = 32;
            
            // Gravity (0,0,-40)
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.y = -40.0f; // Down
            
            // Emitter: DiscEmitter (Custom?)
            // Python: ETCUSTOM. Disc usually.
            // Radius 1.0
            // Outer Angle 90, Inner 0. (Hemisphere?)
            // OffsetForce(0,0,40) -> Initial Upward kick
            
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45.0f; // 90 deg total?
            shape.radius = 1.0f;
            
            // Initial Velocity
            // Amplitude 2.0. Outer Magnitude 20.0.
            main.startSpeed = 20.0f;
            
            // Offset Force 40 UP (Counteracts gravity initially?)
            // Force over lifetime handles constant force.
            // We already set Gravity -40.
            // Maybe this is Initial Velocity?
            // Let's assume startSpeed 20 Upwards is good.
            
            // Size Over Lifetime
            // 0.10 -> 0.70 (7x)
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 1.0f);
            curve.AddKey(1.0f, 7.0f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
            
            // Color: Alpha 0.2
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.2f, 0.0f), new GradientAlphaKey(0.2f, 1.0f) }
            );
            col.color = grad;
        }
    }
}
