using UnityEngine;
using System.Collections.Generic;

namespace POTCO.Effects
{
    public class BeamEffect : POTCOEffect
    {
        [Header("Beam Settings")]
        public Transform target;
        public int numBeams = 6;
        public float textureScrollSpeed = 20.0f;
        public float jitterAmount = 2.0f;
        public float updateRate = 0.05f; // Update geometry every 0.05s

        private LineRenderer lineRenderer;
        private float timer;
        private Material beamMat;

        protected override void Start()
        {
            duration = 1.0f; // Default duration
            
            // Create LineRenderer
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.positionCount = numBeams + 1;
            lineRenderer.useWorldSpace = true; // Easier to connect points
            lineRenderer.widthMultiplier = 4.0f; // Scale from python (Scale 4.0)
            
            // Load material
            GameObject prefab = Resources.Load<GameObject>("phase_3/models/effects/lightning_beam");
            if (prefab != null)
            {
                Renderer r = prefab.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    beamMat = new Material(r.sharedMaterial);
                    beamMat.shader = Shader.Find("Legacy Shaders/Particles/Additive");
                    lineRenderer.material = beamMat;
                }
            }
            
            // If no target, create a dummy one forward
            if (target == null)
            {
                GameObject t = new GameObject("BeamTarget");
                t.transform.position = transform.position + transform.forward * 20.0f;
                t.transform.SetParent(transform); // Move with us
                target = t.transform;
            }

            base.Start();
        }

        protected override void Update()
        {
            base.Update();
            
            if (isPlaying && lineRenderer != null && target != null)
            {
                // Update Geometry Jitter
                timer += Time.deltaTime;
                if (timer >= updateRate)
                {
                    timer = 0;
                    UpdateBeamGeometry();
                }
                
                // Scroll UVs
                if (beamMat != null)
                {
                    float uOffset = -Time.time * textureScrollSpeed; // Scroll logic
                    beamMat.mainTextureOffset = new Vector2(uOffset, 0);
                }
            }
        }

        private void UpdateBeamGeometry()
        {
            Vector3 startPos = transform.position;
            Vector3 endPos = target.position;
            Vector3 dir = (endPos - startPos);
            float totalDist = dir.magnitude;
            Vector3 dirNorm = dir.normalized;
            
            float step = totalDist / numBeams;
            
            lineRenderer.SetPosition(0, startPos);
            lineRenderer.SetPosition(numBeams, endPos);
            
            for (int i = 1; i < numBeams; i++)
            {
                float distAlong = i * step;
                
                // Base position along the line
                Vector3 basePoint = startPos + dirNorm * distAlong;
                
                // Add random jitter perpendicular to direction?
                // Python used random X/Z in local space where Y is forward.
                // Here we can just add random vector.
                Vector3 jitter = Random.insideUnitSphere * jitterAmount;
                
                lineRenderer.SetPosition(i, basePoint + jitter);
            }
        }
    }
}
