using UnityEngine;

namespace POTCO.Effects
{
    public class CraterSmokeEffect : POTCOEffect
    {
        public float cardScale = 64.0f;
        private ParticleSystem p0;
        private ParticleSystem p1;

        protected override void Start()
        {
            duration = 15.0f; // Start + Wait(10) + End
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            // P0: Main Smoke
            p0 = SetupParticleSystem("CraterSmokeP0");
            
            // P1: Secondary Smoke
            p1 = SetupParticleSystem("CraterSmokeP1");
            
            Material mat = GetMaterialFromParticleMap("particleWhiteSmoke");
            if (mat != null)
            {
                // Alpha In/Out -> ParticleGUI
                mat.shader = Shader.Find("EggImporter/ParticleGUI");
                mat.SetColor("_Color", new Color(0.8f, 0.8f, 1.0f, 0.5f));
                
                var psr0 = p0.GetComponent<ParticleSystemRenderer>();
                var psr1 = p1.GetComponent<ParticleSystemRenderer>();
                psr0.material = mat;
                psr1.material = mat;
            }

            // --- P0 Setup ---
            var main0 = p0.main;
            main0.startLifetime = new ParticleSystem.MinMaxCurve(7.0f, 13.0f); // 10 +/- 3
            main0.startSize = 0.15f * 64.0f; // 9.6
            main0.maxParticles = 8;
            
            var emission0 = p0.emission;
            emission0.rateOverTime = 0.75f; // Very slow? Wait, BirthRate 0.75 means 1 every 0.75s.
            // 1.33/sec.
            
            var shape0 = p0.shape;
            shape0.shapeType = ParticleSystemShapeType.Box; // RectangleEmitter
            shape0.scale = new Vector3(10, 10, 0); // Min -5,-5 Max 5,5
            
            var force0 = p0.forceOverLifetime;
            force0.enabled = true;
            force0.space = ParticleSystemSimulationSpace.Local;
            force0.z = 17.0f; // Up
            
            var size0 = p0.sizeOverLifetime;
            size0.enabled = true;
            AnimationCurve curve0 = new AnimationCurve();
            curve0.AddKey(0.0f, 1.0f);
            curve0.AddKey(1.0f, 6.6f); // 0.15 -> 1.0 ratio
            size0.size = new ParticleSystem.MinMaxCurve(1.0f, curve0);
            
            // Color P0: Fade Out
            var col0 = p0.colorOverLifetime;
            col0.enabled = true;
            Gradient grad0 = new Gradient();
            grad0.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(0.5f, 0.25f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            col0.color = grad0;

            // --- P1 Setup ---
            var main1 = p1.main;
            main1.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 4.5f);
            main1.startSize = 0.2f * 64.0f; // 12.8
            main1.maxParticles = 16;
            
            var emission1 = p1.emission;
            emission1.rateOverTime = 1.0f; // BirthRate 1.0
            
            var shape1 = p1.shape;
            shape1.shapeType = ParticleSystemShapeType.Box;
            shape1.scale = new Vector3(10, 10, 0);
            
            var force1 = p1.forceOverLifetime;
            force1.enabled = true;
            force1.z = 5.0f; // Up
            
            var size1 = p1.sizeOverLifetime;
            size1.enabled = true;
            AnimationCurve curve1 = new AnimationCurve();
            curve1.AddKey(0.0f, 1.0f);
            curve1.AddKey(1.0f, 2.5f); // 0.2 -> 0.5
            size1.size = new ParticleSystem.MinMaxCurve(1.0f, curve1);
            
            var col1 = p1.colorOverLifetime;
            col1.enabled = true;
            Gradient grad1 = new Gradient();
            grad1.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(0.5f, 0.1f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            col1.color = grad1;
        }
    }
}
