using UnityEngine;

namespace POTCO.Effects
{
    public class DarkPortalEffect : POTCOEffect
    {
        private GameObject portal;
        private Material mat;

        protected override void Start()
        {
            duration = 5.0f; // ScaleUp(0.75) + Hold(2.5) + ScaleDown(0.75) + padding
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            // models/effects/darkPortal
            GameObject prefab = Resources.Load<GameObject>("phase_4/models/effects/darkPortal");
            if (prefab == null) prefab = Resources.Load<GameObject>("phase_3/models/effects/darkPortal");
            
            if (prefab != null)
            {
                portal = Instantiate(prefab, transform);
                portal.transform.localPosition = Vector3.zero;
                portal.transform.localRotation = Quaternion.Euler(0, -90, 0); // Python H=-90
                portal.transform.localScale = Vector3.zero; // Start Scale 0
                
                Renderer r = portal.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    mat = new Material(r.sharedMaterial);
                    // Transparency MAlpha -> Alpha Blend
                    mat.shader = Shader.Find("EggImporter/ParticleGUI");
                    mat.SetColor("_Color", new Color(1,1,1,0.75f)); // Alpha 0.75
                    r.material = mat;
                }
            }
        }

        protected override void Update()
        {
            base.Update();
            
            if (isPlaying && portal != null)
            {
                // Sequence: ScaleUp (0.75s) -> Hold (2.5s) -> ScaleDown (0.75s)
                // Target Size 40.
                
                float speed = 0.75f;
                float hold = 2.5f;
                float size = 40.0f;
                
                float s = 0f;
                
                if (age < speed)
                {
                    // EaseIn Scale Up
                    float t = age / speed;
                    s = Mathf.Lerp(0, size, t * t);
                }
                else if (age < speed + hold)
                {
                    s = size;
                }
                else if (age < speed + hold + speed)
                {
                    // EaseIn Scale Down
                    float t = (age - (speed + hold)) / speed;
                    s = Mathf.Lerp(size, 0, t * t);
                }
                
                portal.transform.localScale = Vector3.one * s;
            }
        }
    }
}
