using UnityEngine;

namespace POTCO.Effects
{
    public class DarkSteamEffect : POTCOEffect
    {
        public float cardScale = 64.0f;
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 15.0f; // Start + Wait(10) + End
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("DarkSteamParticles");
            
            Material mat = GetMaterialFromParticleMap("particleWhiteSmoke");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleGUI"); // Alpha Blend
                mat.SetColor("_Color", new Color(0.6f, 0.6f, 0.6f, 0.25f)); // Grey
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            var main = p0.main;
            // Lifespan 10 +/- 2
            main.startLifetime = new ParticleSystem.MinMaxCurve(8.0f, 12.0f);
            
            // Size
            // 0.5 * 64 = 32.0
            main.startSize = 32.0f;
            
            main.maxParticles = 12; // High LOD
            
            // Emission: BirthRate 0.5 -> 2/sec. Litter 1 -> 2/sec.
            var emission = p0.emission;
            emission.rateOverTime = 2f;
            
            // Shape: RectangleEmitter (-10,-1) to (10,1).
            // Unity Box.
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(20.0f, 2.0f, 0.0f); // Width/Depth
            
            // Force (0,-3,12) Up/Back
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.y = 12.0f; // Up
            force.z = -3.0f; // Back
            
            // Size Over Lifetime: Grow
            // 0.5 -> 1.0 (Double)
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 1.0f);
            curve.AddKey(1.0f, 2.0f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
        }
    }
}
