using UnityEngine;

namespace POTCO.Effects
{
    public class BulletEffect : POTCOEffect
    {
        [Header("Bullet Settings")]
        public int numObjects = 1; // random.randint(0,1) usually
        
        private GameObject[] objects;

        protected override void Start()
        {
            duration = 2.0f; // Short life
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            // Load testBoard model
            GameObject prefab = Resources.Load<GameObject>("phase_3/models/props/testBoard");
            if (prefab != null)
            {
                objects = new GameObject[numObjects];
                for(int i=0; i<numObjects; i++)
                {
                    objects[i] = Instantiate(prefab, transform);
                    
                    // Random Scale: 0.6 - 1.0
                    float scale = Random.Range(0.6f, 1.0f);
                    objects[i].transform.localScale = Vector3.one * scale;
                    
                    // Random Velocity
                    Vector3 velocity = new Vector3(
                        Random.Range(-20f, 20f),
                        Random.Range(20f, 80f), // Forward (Z or Y?)
                        Random.Range(-20f, 20f)
                    );
                    
                    // Add Rigidbody for physics
                    Rigidbody rb = objects[i].AddComponent<Rigidbody>();
                    rb.linearVelocity = velocity;
                    rb.useGravity = true; // gravityMult = 4.0 -> Set gravity scale?
                    // Unity Gravity is ~9.8. 4.0x is ~40.
                    // We can add ConstantForce or modify global gravity? No.
                    // Just let it fly.
                    
                    // Random Rotation
                    rb.angularVelocity = Random.insideUnitSphere * 10f;
                }
            }
        }
    }
}
