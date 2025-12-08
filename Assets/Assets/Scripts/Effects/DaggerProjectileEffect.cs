using UnityEngine;

namespace POTCO.Effects
{
    public class DaggerProjectileEffect : POTCOEffect
    {
        public float speed = 20.0f;
        private GameObject dagger;
        private TrailRenderer trail1;
        private TrailRenderer trail2;

        protected override void Start()
        {
            duration = 2.0f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            // models/handheld/dagger_high
            GameObject prefab = Resources.Load<GameObject>("phase_2/models/handheld/dagger_high");
            if (prefab != null)
            {
                // Rotation dummy
                GameObject rotDummy = new GameObject("RotDummy");
                rotDummy.transform.SetParent(transform, false);
                
                dagger = Instantiate(prefab, rotDummy.transform);
                dagger.transform.localPosition = new Vector3(0, -0.6f, 0);
                dagger.transform.localRotation = Quaternion.Euler(180, 0, 0); // R=180
                
                // Trails (PolyTrail)
                // Vertex list [(0,1,0), (0,-1,0)] -> Center length
                // Vertex list [(0.3,0,0), (-0.3,0,0)] -> Cross width
                // We'll add two TrailRenderers to empty gameobjects at these points.
                
                CreateTrail(rotDummy.transform, new Vector3(0, 1, 0), out trail1);
                CreateTrail(rotDummy.transform, new Vector3(0.3f, 0, 0), out trail2);
            }
        }
        
        private void CreateTrail(Transform parent, Vector3 pos, out TrailRenderer tr)
        {
            GameObject t = new GameObject("Trail");
            t.transform.SetParent(parent, false);
            t.transform.localPosition = pos;
            tr = t.AddComponent<TrailRenderer>();
            tr.time = 0.2f;
            tr.startWidth = 0.1f;
            tr.endWidth = 0.0f;
            tr.material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            tr.startColor = new Color(0.5f, 0.6f, 0.8f, 1.0f);
            tr.endColor = new Color(0.5f, 0.6f, 0.8f, 0.0f);
        }

        protected override void Update()
        {
            base.Update();
            
            if (isPlaying && dagger != null)
            {
                // Move forward
                transform.Translate(Vector3.forward * speed * Time.deltaTime);
                
                // Spin (RotDummy)
                // H -3080*t, P 90
                Transform rotDummy = dagger.transform.parent;
                rotDummy.localRotation = Quaternion.Euler(0, -3080f * age, 90f);
            }
        }
    }
}
