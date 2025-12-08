using UnityEngine;

namespace POTCO.Effects
{
    public class DarkWaterFogEffect : POTCOEffect
    {
        public float cardScale = 64.0f;
        public float radius = 700.0f;
        public float lifespan = 4.0f;
        
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 10.0f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("DarkWaterFogParticles");
            
            Material mat = GetMaterialFromParticleMap("particleGunSmoke");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleGUI"); // Alpha Blend (PRALPHAINOUT)
                mat.SetColor("_Color", Color.white);
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 4.0 +/- 2.0
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifespan - 2.0f, lifespan + 2.0f);
            
            // Size: 
            // X: 2.56 * 64 -> 1.92 * 64 (163 -> 122)
            // Y: 1.28 * 64 -> 0.64 * 64 (81 -> 40)
            // Average start ~120
            main.startSize = 120.0f;
            
            main.maxParticles = 512;
            
            // Emission: 0.02s -> 50/sec. Litter 10 -> 500/sec.
            var emission = p0.emission;
            emission.rateOverTime = 500f;
            
            // Shape: TangentRingEmitter
            // Unity Circle with emits from edge?
            // Radius 700, Spread 300
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = 300.0f / radius; // Approximate spread
            
            // Force (0,0,12) Up
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.y = 12.0f;
            
            // Velocity: Amplitude -20 (+/- 10) -> Inward
            main.startSpeed = new ParticleSystem.MinMaxCurve(-30.0f, -10.0f);
            
            // Size Over Lifetime: Shrink
            // 2.56 -> 1.92 (Ratio 0.75)
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 1.0f);
            curve.AddKey(1.0f, 0.75f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
            
            // Color: Blue Tint Fade In/Out
            // PRALPHAINOUT
            // Constant (0.2, 0.3, 0.5, 0.3)
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            Color blue = new Color(0.2f, 0.3f, 0.5f);
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(blue, 0.0f), new GradientColorKey(blue, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(0.3f, 0.2f), new GradientAlphaKey(0.3f, 0.8f), new GradientAlphaKey(0.0f, 1.0f) }
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
            var emission = p0.emission;
            emission.rateOverTime = 500f;
            p0.Play();
            
            yield return new WaitForSeconds(3.8f);
            
            emission.rateOverTime = 0;
            
            yield return new WaitForSeconds(1.0f);
            
            StopEffect();
        }
    }
}
