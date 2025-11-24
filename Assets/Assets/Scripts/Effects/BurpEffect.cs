using UnityEngine;

namespace POTCO.Effects
{
    public class BurpEffect : POTCOEffect
    {
        public float cardScale = 128.0f;
        private ParticleSystem p0;
        private GameObject skull;

        protected override void Start()
        {
            duration = 8.0f;
            InitializeSystem();
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
            // 1. Start Particles
            p0.Play();
            
            // 2. Skull Animation
            if (skull != null)
            {
                skull.SetActive(true);
                skull.transform.localScale = Vector3.one * 0.1f;
                
                // Fade In (2.0s) & Scale Up (4.0s)
                float t = 0;
                Color startCol = new Color(0, 0, 0, 0);
                Color midCol = new Color(0.1f, 0.1f, 0, 0.35f); // Dark yellowish
                
                Material skullMat = skull.GetComponentInChildren<Renderer>().material;
                
                while (t < 4.0f)
                {
                    t += Time.deltaTime;
                    
                    // Color (0-2s)
                    if (t < 2.0f)
                    {
                        Color c = Color.Lerp(startCol, midCol, t / 2.0f);
                        skullMat.SetColor("_Color", c);
                    }
                    
                    // Scale (0-4s)
                    float scale = Mathf.Lerp(0.1f, 3.0f, t / 4.0f); // easeOut?
                    skull.transform.localScale = Vector3.one * scale;
                    
                    yield return null;
                }
                
                // Fade Out (1.0s)
                t = 0;
                while (t < 1.0f)
                {
                    t += Time.deltaTime;
                    Color c = Color.Lerp(midCol, new Color(0,0,0,0), t);
                    skullMat.SetColor("_Color", c);
                    yield return null;
                }
                
                skull.SetActive(false);
            }
            
            // Stop Particles
            var emission = p0.emission;
            emission.rateOverTime = 0;
            
            yield return new WaitForSeconds(6.5f); // Cleanup wait
            
            StopEffect();
        }

        private void InitializeSystem()
        {
            p0 = SetupParticleSystem("BurpParticles");
            
            // Material: particleGroundFog
            Material mat = GetMaterialFromParticleMap("particleGroundFog");
            if (mat != null)
            {
                mat.shader = Shader.Find("EggImporter/ParticleGUI"); // Alpha Blend (No Blend in python?)
                // PPNOBLEND means Overwrite? Or Opaque?
                // Assuming Fog is Alpha Blended.
                mat.SetColor("_Color", Color.yellow);
                p0.GetComponent<ParticleSystemRenderer>().material = mat;
            }

            // Skull Model
            GameObject skullPrefab = Resources.Load<GameObject>("phase_3/models/effects/skull");
            if (skullPrefab != null)
            {
                skull = Instantiate(skullPrefab, transform);
                skull.SetActive(false);
                // Billboard
                // We'll handle lookAt in Update
                
                Renderer r = skull.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    r.material = new Material(r.sharedMaterial);
                    r.material.shader = Shader.Find("EggImporter/ParticleGUI");
                }
            }

            var main = p0.main;
            // Lifespan 6.0 +/- 0.1
            main.startLifetime = new ParticleSystem.MinMaxCurve(5.9f, 6.1f);
            
            // Size
            // 0.001 * 128 = 0.128
            main.startSize = 0.128f;
            
            // Emission: 0.1s -> 10/sec. Litter 9.
            // 90/sec?
            var emission = p0.emission;
            emission.rateOverTime = 90f;
            
            // Shape: Sphere Surface, Radius 0.25
            var shape = p0.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            
            // Force (0,0,-1.0) Down
            var force = p0.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.Local;
            force.y = -1.0f;
            
            // Size Over Lifetime: Grow
            // 0.128 -> 2.56 (20x)
            var size = p0.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 1.0f);
            curve.AddKey(1.0f, 20.0f);
            size.size = new ParticleSystem.MinMaxCurve(1.0f, curve);
            
            // Color: Yellow
            main.startColor = Color.yellow;
        }

        protected override void Update()
        {
            base.Update();
            if (skull != null && skull.activeSelf)
            {
                skull.transform.LookAt(Camera.main.transform);
            }
        }
    }
}
