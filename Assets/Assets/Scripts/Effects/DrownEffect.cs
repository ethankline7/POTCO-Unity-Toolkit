using UnityEngine;

namespace POTCO.Effects
{
    public class DrownEffect : POTCOEffect
    {
        public float cardScale = 64.0f;
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 4.0f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("DrownParticles");
            
            Material mat = GetMaterialFromParticleMap("particleGunSmoke");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleGUI"); // Alpha Blend
                mat.SetColor("_Color", Color.white);
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 1.25 +/- 0.5
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.75f);
            
            // Size
            // X: 0.085 -> 0.01 (*64) = 5.44 -> 0.64
            // Y: 0.08 -> 0.01 (*64) = 5.12 -> 0.64
            main.startSize3D = true;
            main.startSizeX = 5.44f;
            main.startSizeY = 5.12f;
            main.startSizeZ = 1.0f;
            
            main.maxParticles = 8;
            
            // Emission: 0.1s -> 10/sec. Litter 1.
            var emission = p0.emission;
            emission.rateOverTime = 10f;
            
            // Shape: Sphere Volume, Radius 2.0
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 2.0f;
            
            // Amplitude 3.0 (Outward)
            main.startSpeed = 3.0f;
            
            // Offset Force (0,0,-0.6) Down
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.y = -0.6f;
            
            // Size Over Lifetime: Shrink (Ratio ~0.12)
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 1.0f);
            curve.AddKey(1.0f, 0.12f);
            size.x = new ParticleSystem.MinMaxCurve(1.0f, curve);
            size.y = new ParticleSystem.MinMaxCurve(1.0f, curve);
            
            // Color: Blue Tint
            // (0.35, 0.5, 0.8, 0.6)
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            Color c = new Color(0.35f, 0.5f, 0.8f);
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(c, 0.0f), new GradientColorKey(c, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0.0f), new GradientAlphaKey(0.6f, 1.0f) }
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
            emission.rateOverTime = 10f;
            p0.Play();
            
            yield return new WaitForSeconds(0.5f);
            
            emission.rateOverTime = 0;
            
            yield return new WaitForSeconds(3.0f);
            
            StopEffect();
        }
    }
}
