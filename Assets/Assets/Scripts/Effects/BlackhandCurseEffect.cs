using UnityEngine;

namespace POTCO.Effects
{
    public class BlackhandCurseEffect : POTCOEffect
    {
        private GameObject glow1;
        private GameObject glow2;
        private Material mat;

        protected override void Start()
        {
            duration = 3.0f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            GameObject prefab = Resources.Load<GameObject>("phase_2/models/effects/particleCards");
            if (prefab != null)
            {
                // Find particleWhiteGlow
                Transform child = prefab.transform.Find("particleWhiteGlow");
                // Recursively find if not direct child (prefab structure might vary)
                if (child == null)
                {
                    foreach(Transform t in prefab.GetComponentsInChildren<Transform>())
                    {
                        if (t.name == "particleWhiteGlow") { child = t; break; }
                    }
                }

                if (child != null)
                {
                    // Create duplicates
                    glow1 = Instantiate(child.gameObject, transform);
                    glow2 = Instantiate(child.gameObject, transform);
                    
                    glow1.transform.localPosition = Vector3.zero;
                    glow2.transform.localPosition = Vector3.zero;
                    
                    // Material Setup
                    Renderer r = glow1.GetComponent<Renderer>();
                    if (r != null)
                    {
                        mat = new Material(r.sharedMaterial);
                        // Python: setColorScale(0,0,0,1). This tints the texture black.
                        // For standard shaders, _Color black makes it black.
                        // For additive, black makes it invisible.
                        // Assuming this is a dark aura, it likely uses Alpha Blending (not additive).
                        // We'll use ParticleGUI or VertexColorTexture with black tint.
                        mat.shader = Shader.Find("EggImporter/ParticleGUI"); 
                        mat.SetColor("_Color", Color.black);
                        
                        r.material = mat;
                        glow2.GetComponent<Renderer>().material = mat;
                    }
                }
            }
        }

        protected override void Update()
        {
            base.Update();
            
            if (isPlaying && glow1 != null)
            {
                // Pulse Logic
                // 0.15s easeIn to 1.2
                // 0.15s easeOut to 0.6
                float pulseDuration = 0.3f;
                float t = Mathf.Repeat(Time.time, pulseDuration) / pulseDuration;
                
                float scale;
                if (t < 0.5f)
                {
                    // 0.0 -> 0.5: 0.6 -> 1.2
                    float st = t * 2.0f;
                    // EaseIn (Quadratic)
                    st = st * st; 
                    scale = Mathf.Lerp(0.6f, 1.2f, st);
                }
                else
                {
                    // 0.5 -> 1.0: 1.2 -> 0.6
                    float st = (t - 0.5f) * 2.0f;
                    // EaseOut (Inverse Quadratic)
                    st = st * (2 - st);
                    scale = Mathf.Lerp(1.2f, 0.6f, st);
                }
                
                glow1.transform.localScale = Vector3.one * scale;
                glow2.transform.localScale = Vector3.one * scale;
                
                // Billboard
                glow1.transform.LookAt(Camera.main.transform);
                glow2.transform.LookAt(Camera.main.transform);
            }
        }
    }
}
