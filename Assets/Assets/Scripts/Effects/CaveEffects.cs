using UnityEngine;

namespace POTCO.Effects
{
    public class CaveEffects : POTCOEffect
    {
        // Cave Effects are mostly environmental (Water, Lava).
        // Logic: If water cave -> Spawn water plane. If lava cave -> Spawn lava.
        // We'll just spawn both for preview, or togglable.
        
        [Header("Cave Settings")]
        public bool isLava = false;
        
        private GameObject waterPlane;
        private Material waterMat;

        protected override void Start()
        {
            duration = Mathf.Infinity;
            loop = true;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            if (isLava)
            {
                // Lava Logic
                // models/caves/lava
                // Unity: phase_2/models/caves/lava.egg? Or phase_4?
                // Search common paths.
                GameObject prefab = Resources.Load<GameObject>("phase_2/models/caves/lava");
                if (prefab == null) prefab = Resources.Load<GameObject>("phase_4/models/caves/lava");
                
                if (prefab != null)
                {
                    waterPlane = Instantiate(prefab, transform);
                    // Lava animation: LerpScale 1.0 -> 1.006. (Breathing)
                }
            }
            else
            {
                // Water Logic
                // models/caves/cave_a_water
                GameObject prefab = Resources.Load<GameObject>("phase_2/models/caves/cave_a_water");
                if (prefab != null)
                {
                    waterPlane = Instantiate(prefab, transform);
                    // Color: (0, 1/255, 4/255, 1) -> Very dark teal/black?
                    // Shader water color: (0, 1, 4, 255) -> HDR?
                    
                    Renderer r = waterPlane.GetComponentInChildren<Renderer>();
                    if (r != null)
                    {
                        waterMat = new Material(r.sharedMaterial);
                        // Use water shader if available, or standard.
                        waterMat.color = new Color(0, 1f/255f, 4f/255f, 1f);
                        r.material = waterMat;
                    }
                }
            }
        }

        protected override void Update()
        {
            base.Update();
            
            if (isPlaying && isLava && waterPlane != null)
            {
                // Lava Breathing
                float t = Mathf.PingPong(Time.time * 0.5f, 1.0f); // 2 sec loop
                // Scale 1.0 -> 1.006
                float s = Mathf.Lerp(1.0f, 1.006f, t);
                // Only scale X/Y?
                waterPlane.transform.localScale = new Vector3(s, s, 1.0f);
            }
        }
    }
}
