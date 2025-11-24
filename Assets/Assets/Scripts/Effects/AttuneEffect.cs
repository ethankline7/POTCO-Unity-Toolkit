using UnityEngine;

namespace POTCO.Effects
{
    public class AttuneEffect : POTCOEffect
    {
        [Header("Attune Settings")]
        public Color effectColor = Color.white;
        
        private ParticleSystem p0;
        private GameObject particleDummy;

        protected override void Start()
        {
            duration = 5.0f; // Sequence: Start + Wait(2.0) + End(Wait 3.0)
            base.Start();
        }

        public override void StartEffect()
        {
            if (p0 == null) InitializeSystem();
            base.StartEffect();
            
            // Sequence logic from python:
            // 1. LerpPosInterval 0.75 to (0,0,0.5)
            // 2. BirthRate 0.03
            // 3. Wait 2.0
            // 4. BirthRate 100 (Stop)
            
            StartCoroutine(RunSequence());
        }

        private System.Collections.IEnumerator RunSequence()
        {
            p0.Play();
            
            // Move up (LerpPos)
            float t = 0;
            Vector3 startPos = transform.localPosition;
            Vector3 endPos = startPos + new Vector3(0, 0.5f, 0); // Up in Y (Unity)
            
            while (t < 0.75f)
            {
                t += Time.deltaTime;
                transform.localPosition = Vector3.Lerp(startPos, endPos, t / 0.75f);
                yield return null;
            }
            transform.localPosition = endPos;
            
            yield return new WaitForSeconds(2.0f - 0.75f); // Wait remaining time
            
            // End Effect
            var emission = p0.emission;
            emission.rateOverTime = 0; // Stop emitting
            
            yield return new WaitForSeconds(3.0f); // Wait for particles to die
            
            StopEffect();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("AttuneParticles");
            
            // --- Model & Material ---
            // loader.loadModel('models/effects/voodooRing')
            // GeomParticleRenderer -> Mesh mode
            GameObject ringPrefab = Resources.Load<GameObject>("phase_3/models/effects/voodooRing");
            if (ringPrefab != null)
            {
                MeshFilter mf = ringPrefab.GetComponentInChildren<MeshFilter>();
                Renderer r = ringPrefab.GetComponentInChildren<Renderer>();
                
                if (mf != null && r != null)
                {
                    var psr = p0.GetComponent<ParticleSystemRenderer>();
                    psr.renderMode = ParticleSystemRenderMode.Mesh;
                    psr.mesh = mf.sharedMesh;
                    psr.material = new Material(r.sharedMaterial);
                    
                    // Additive blend? Not specified, usually alpha blend for rings.
                    // Python: setAlphaMode(PRALPHAOUT) -> Fade out alpha.
                    // Unity Particle Standard Shader supports this.
                    // Using default shader from imported model is safest.
                }
            }

            var main = p0.main;
            main.duration = 5.0f;
            main.loop = false;
            // Lifespan 1.75
            main.startLifetime = 1.75f;
            
            // Size: 1.0 -> 4.0
            // Unity StartSize is multiplier.
            main.startSize = 1.0f;
            
            // Emission: 0.03s -> ~33/sec
            var emission = p0.emission;
            emission.rateOverTime = 33f;
            
            // Shape: DiscEmitter
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Circle; // Disc
            shape.radius = 0.75f;
            // Radiate Origin? (0,0,0)
            // Offset Force (0,0,-0.5) -> Down
            
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.y = -0.5f; // Unity Y is Up/Down
            
            // Size Over Lifetime: 1 -> 4
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 1.0f);
            sizeCurve.AddKey(1.0f, 4.0f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);
            
            // Color Over Lifetime
            // 0.0-0.2: Black(0.5) -> Black(1.0)
            // 0.2-1.0: Black(0.75) -> effectColor
            var col = p0.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            
            // Colors
            grad.colorKeys = new GradientColorKey[] {
                new GradientColorKey(Color.black, 0.0f),
                new GradientColorKey(Color.black, 0.2f),
                new GradientColorKey(effectColor, 1.0f)
            };
            
            // Alphas
            grad.alphaKeys = new GradientAlphaKey[] {
                new GradientAlphaKey(0.5f, 0.0f),
                new GradientAlphaKey(1.0f, 0.2f),
                new GradientAlphaKey(0.75f, 0.21f), // Jump down?
                new GradientAlphaKey(1.0f, 1.0f)    // Assuming effectColor is opaque
            };
            
            col.color = grad;
        }
    }
}
