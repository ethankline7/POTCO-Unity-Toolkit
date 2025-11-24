using UnityEngine;

namespace POTCO.Effects
{
    public class AttuneSmokeEffect : POTCOEffect
    {
        public float cardScale = 64.0f;
        private ParticleSystem p0;

        protected override void Start()
        {
            duration = 10.0f; 
            base.Start();
        }

        public override void StartEffect()
        {
            if (p0 == null) InitializeSystem();
            base.StartEffect();
            StartCoroutine(RunSequence());
        }

        private System.Collections.IEnumerator RunSequence()
        {
            // Start
            var emission = p0.emission;
            // BirthRate 0.04
            emission.rateOverTime = 25f;
            p0.Play();
            
            yield return new WaitForSeconds(3.0f);
            
            // End
            emission.rateOverTime = 0;
            
            yield return new WaitForSeconds(7.0f); // Wait for cleanup
            
            StopEffect();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("AttuneSmokeParticles");
            
            // Material: candleHalo / effectCandleHalo
            GameObject prefab = Resources.Load<GameObject>("phase_2/models/effects/candleHalo");
            if (prefab != null)
            {
                Transform t = prefab.transform.Find("effectCandleHalo"); // Need exact path?
                if (t == null) t = prefab.transform.GetChild(0); // Fallback
                
                if (t != null)
                {
                    Renderer r = t.GetComponent<Renderer>();
                    if (r != null)
                    {
                        var psr = p0.GetComponent<ParticleSystemRenderer>();
                        psr.material = new Material(r.sharedMaterial);
                        // Python sets color to Black (0,0,0,1).
                        // If material uses VertexColor, we control it via StartColor.
                    }
                }
            }

            var main = p0.main;
            main.loop = false;
            // Lifespan 0.6 +/- 0.5
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 1.1f);
            
            // Size (Non-Uniform)
            // X: 0.02*64 (1.28) -> 0.01*64 (0.64)
            // Y: 0.001*64 (0.064) -> 0.06*64 (3.84)
            // Unity 3D Start Size
            main.startSize3D = true;
            main.startSizeX = 1.28f;
            main.startSizeY = 0.064f;
            main.startSizeZ = 1.0f;
            
            // Color: Black
            main.startColor = Color.black;
            
            // Emission
            var emission = p0.emission;
            emission.rateOverTime = 25f; 
            
            // Shape
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.5f;
            
            // Force (0,0,4) Up
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.y = 4.0f;
            
            // Size Over Lifetime (Separate Axes)
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = true;
            
            // X: 1.28 -> 0.64 (0.5 ratio)
            AnimationCurve xCurve = new AnimationCurve();
            xCurve.AddKey(0.0f, 1.0f);
            xCurve.AddKey(1.0f, 0.5f);
            size.x = new ParticleSystem.MinMaxCurve(1.0f, xCurve);
            
            // Y: 0.064 -> 3.84 (60 ratio!)
            AnimationCurve yCurve = new AnimationCurve();
            yCurve.AddKey(0.0f, 1.0f);
            yCurve.AddKey(1.0f, 60.0f);
            size.y = new ParticleSystem.MinMaxCurve(1.0f, yCurve);
        }
    }
}
