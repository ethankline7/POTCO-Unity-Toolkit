using UnityEngine;

namespace POTCO.Effects
{
    public class DarkAuraEffect : POTCOEffect
    {
        public float cardScale = 128.0f;
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 15.0f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("DarkAuraParticles");
            
            // models/effects/darkglow
            // effectDarkGlow2
            GameObject prefab = Resources.Load<GameObject>("phase_3/models/effects/darkglow");
            if (prefab != null)
            {
                Transform t = FindDeepChild(prefab.transform, "effectDarkGlow2");
                if (t != null)
                {
                    Renderer r = t.GetComponent<Renderer>();
                    if (r != null)
                    {
                        Material mat = new Material(r.sharedMaterial);
                        mat.shader = Shader.Find("EggImporter/ParticleAdditive"); // MAdd
                        mat.SetColor("_Color", Color.white);
                        p0.GetComponent<ParticleSystemRenderer>().material = mat;
                    }
                }
            }

            var main = p0.main;
            // Lifespan 2.5
            main.startLifetime = 2.5f;
            
            // Size
            // 0.01 * 64 = 0.64
            main.startSize = 0.64f;
            
            main.maxParticles = 32;
            
            // Emission
            var emission = p0.emission;
            emission.rateOverTime = 15f; // 0.2 * 3
            
            // Shape: Sphere Volume, Radius 0.8
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.8f;
            
            // Force (0,0,0.1) Up
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.y = 0.1f;
            
            // Size Over Lifetime: Shrink
            // 0.01 -> 0.001
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 1.0f);
            curve.AddKey(1.0f, 0.1f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
            
            // Color: Alpha 0.3 Fade In/Out
            // Python: PRALPHAINOUT
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(0.3f, 0.5f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            col.color = grad;
            
            // Noise Force (LinearNoiseForce 0.1)
            var noise = p0.noise;
            noise.enabled = true;
            noise.strength = 0.1f;
        }
        
        private Transform FindDeepChild(Transform parent, string name)
        {
            foreach(Transform child in parent)
            {
                if(child.name == name) return child;
                Transform result = FindDeepChild(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
