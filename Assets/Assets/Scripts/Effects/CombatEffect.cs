using UnityEngine;

namespace POTCO.Effects
{
    public class CombatEffect : POTCOEffect
    {
        // CombatEffect is a manager that spawns OTHER effects based on ID.
        // Implementing full logic requires all sub-effects.
        // For now, I'll implement a generic preview that spawns a "HitFlashA" and "SparkBurst"
        // since those are common to most weapon impacts.
        
        // We need HitFlashAEffect and SparkBurstEffect.
        // I'll create stub classes for them if they don't exist, or implement simple versions here.
        
        protected override void Start()
        {
            duration = 2.0f;
            
            // Spawn HitFlashA (Generic Hit)
            GameObject flash = new GameObject("HitFlash");
            flash.transform.SetParent(transform, false);
            var flashEffect = flash.AddComponent<HitFlashAEffect>();
            
            // Spawn SparkBurst
            GameObject sparks = new GameObject("Sparks");
            sparks.transform.SetParent(transform, false);
            var sparkEffect = sparks.AddComponent<SparkBurstEffect>();
            
            base.Start();
        }
    }
    
    // Minimal implementations for preview
    public class HitFlashAEffect : POTCOEffect
    {
        private ParticleSystem p0;
        protected override void Start() 
        { 
            duration = 0.5f;
            InitializeSystem();
            base.Start();
        }
        
        void InitializeSystem()
        {
            p0 = SetupParticleSystem("HitFlashParticles");
            Material mat = GetMaterialFromParticleMap("particleFlash"); // Guessing name
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleAdditive");
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }
            
            var main = p0.main;
            main.startLifetime = 0.2f;
            main.startSize = 5.0f;
            main.startColor = new Color(1, 0.8f, 0.5f);
            
            var emission = p0.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 1) });
        }
    }

    public class SparkBurstEffect : POTCOEffect
    {
        private ParticleSystem p0;
        protected override void Start() 
        { 
            duration = 1.0f;
            InitializeSystem();
            base.Start();
        }
        
        void InitializeSystem()
        {
            p0 = SetupParticleSystem("SparkParticles");
            Material mat = GetMaterialFromParticleMap("particleSpark"); // Guessing
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleAdditive");
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }
            
            var main = p0.main;
            main.startLifetime = 0.5f;
            main.startSize = 0.5f;
            main.startSpeed = 10.0f;
            main.gravityModifier = 1.0f;
            
            var emission = p0.emission;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 20) });
            
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
        }
    }
}
