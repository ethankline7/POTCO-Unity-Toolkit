using UnityEngine;

namespace POTCO.Effects
{
    public class DustCloudEffect : POTCOEffect
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
            p0 = SetupParticleSystem("DustCloudParticles");
            
            Material mat = GetMaterialFromParticleMap("particleDust");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleGUI");
                mat.SetColor("_Color", Color.white);
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 6.0 +/- 2.0
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.0f, 8.0f);
            
            // Size: 0.02 -> 0.7 (1.28 -> 44.8)
            main.startSize = 44.8f;
            
            main.maxParticles = 64;
            
            // Emission: 0.3s -> 3.3/sec. Litter 8 -> 26/sec.
            var emission = p0.emission;
            emission.rateOverTime = 26f;
            
            // Shape: DiscEmitter, Radius 1.0.
            // OuterAngle 90, Inner 0. Hemisphere?
            // OffsetForce(0,0,30) Up
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45.0f;
            shape.radius = 1.0f;
            
            // Force: Gravity -10 (Down). Offset 30 Up. Net 20 Up?
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.y = 20.0f;
            
            // Amplitude 1.0 (Initial Velocity)
            main.startSpeed = 1.0f;
            
            // Size Over Lifetime: Grow
            // 0.02 -> 0.7 (35x)
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 0.03f); // 1/35
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
            var emission = p0.emission;
            emission.rateOverTime = 26f;
            p0.Play();
            
            yield return new WaitForSeconds(0.3f);
            
            emission.rateOverTime = 0;
            
            yield return new WaitForSeconds(8.0f);
            
            StopEffect();
        }
    }
}
