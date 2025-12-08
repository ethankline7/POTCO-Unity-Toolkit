using UnityEngine;

namespace POTCO.Effects
{
    public class DustRingEffect : POTCOEffect
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
            p0 = SetupParticleSystem("DustRingParticles");
            
            Material mat = GetMaterialFromParticleMap("particleSmoke");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleGUI");
                mat.SetColor("_Color", Color.white);
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 1.8 +/- 0.5
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 2.3f);
            
            // Size: 0.05 -> 0.2 (3.2 -> 12.8)
            main.startSize = 12.8f;
            
            main.maxParticles = 64;
            
            // Force (0,0,-5) Down. Offset (0,0,2) Up. Net -3.
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.y = -3.0f;
            
            // Shape: RingEmitter (Custom)
            // Explicit Launch (5,0,0) -> Tangent?
            // Radius 2.0. Amplitude 20.
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 2.0f;
            shape.radiusThickness = 0; // Edge only
            
            main.startSpeed = 20.0f; // Amplitude
            
            // Size Over Lifetime: Grow 4x
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 0.25f);
            curve.AddKey(1.0f, 1.0f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
            
            // Color: Alpha 0.2 Fade Out
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.2f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
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
            // BirthRate 0.01 (100/sec). Litter 4 -> 400/sec.
            var emission = p0.emission;
            emission.rateOverTime = 400f;
            p0.Play();
            
            yield return new WaitForSeconds(0.1f);
            
            emission.rateOverTime = 0;
            
            yield return new WaitForSeconds(4.0f);
            
            StopEffect();
        }
    }
}
