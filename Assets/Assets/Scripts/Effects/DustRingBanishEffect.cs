using UnityEngine;

namespace POTCO.Effects
{
    public class DustRingBanishEffect : POTCOEffect
    {
        public float cardScale = 64.0f;
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 5.0f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("DustRingBanishParticles");
            
            Material mat = GetMaterialFromParticleMap("particleSmoke");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleGUI");
                // Color (0.5, 0.5, 1.0, 1.0) -> Blue tint
                mat.SetColor("_Color", new Color(0.5f, 0.5f, 1.0f, 1.0f));
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 1.8 +/- 0.5
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.3f);
            
            // Size: 0.035 -> 0.15 (*64) = 2.24 -> 9.6
            main.startSize = 9.6f;
            
            main.maxParticles = 64;
            
            // Force (0,0,-10) Down. Offset (0,0,2) Up. Net -8.
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.y = -8.0f;
            
            // Shape: RingEmitter. Radius 1.0. Amplitude 25.
            // Explicit Launch (6,0,0) -> Tangent/Outward?
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.0f;
            shape.radiusThickness = 0; // Edge
            
            main.startSpeed = 25.0f; // Amplitude
            
            // Size Over Lifetime: Grow 4x
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 0.23f); // 2.24/9.6
            curve.AddKey(1.0f, 1.0f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
            
            // Color: Alpha Fade Out
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            col.color = grad;
        }
        
        public override void StartEffect()
        {
            base.StartEffect();
            StartCoroutine(RunSequence());
        }
        
        private System.Collections.IEnumerator RunSequence()
        {
            // BirthRate 0.01 (100/sec). Litter 16 -> 1600/sec.
            var emission = p0.emission;
            emission.rateOverTime = 1600f;
            p0.Play();
            
            yield return new WaitForSeconds(0.1f);
            
            emission.rateOverTime = 0;
            
            yield return new WaitForSeconds(4.0f);
            
            StopEffect();
        }
    }
}
