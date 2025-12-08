using UnityEngine;
using System.Collections.Generic;

namespace POTCO.Effects
{
    public class CausticsProjectorEffect : POTCOEffect
    {
        // Caustics Projector Logic
        // Panda3D Projector applies texture to geometry.
        // Unity Projector is legacy. URP uses Decal Projector.
        // Since I can't guarantee URP Decals are set up or performant,
        // I'll use a Light Cookie approach which is standard and fast.
        
        public Light lightSource;
        public Texture2D[] causticsTextures;
        public float fps = 10.0f;
        
        private float timer;
        private int index;

        protected override void Start()
        {
            duration = Mathf.Infinity;
            loop = true;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            // Load Textures
            // models/effects/causticsCards
            // Need to find textures named *caustics*
            // This might be tricky without scanning all textures.
            // Assuming they are in phase_3/maps/ or similar.
            // Let's try to load them by name pattern if possible, or just one.
            
            // For now, let's try to load one specific texture if known, or skip.
            // Actually, let's look for "maps/water/caustic_*.jpg" or similar.
            // Or just create a dummy light for now.
            
            GameObject go = new GameObject("CausticsLight");
            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(90, 0, 0); // Point Down
            
            lightSource = go.AddComponent<Light>();
            lightSource.type = LightType.Spot;
            lightSource.intensity = 2.0f;
            lightSource.range = 20.0f;
            lightSource.spotAngle = 60.0f;
            lightSource.color = new Color(0.5f, 0.8f, 1.0f);
            
            // Cookies?
            // If we can find the textures, we can animate the cookie.
            // "models/effects/causticsCards"
            // Let's try to load the model and extract textures?
            GameObject prefab = Resources.Load<GameObject>("phase_3/models/effects/causticsCards");
            if (prefab != null)
            {
                Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
                List<Texture2D> texList = new List<Texture2D>();
                foreach (Renderer r in renderers)
                {
                    if (r.sharedMaterial && r.sharedMaterial.mainTexture is Texture2D)
                    {
                        texList.Add((Texture2D)r.sharedMaterial.mainTexture);
                    }
                }
                causticsTextures = texList.ToArray();
            }
        }

        protected override void Update()
        {
            base.Update();
            
            if (isPlaying && lightSource != null && causticsTextures != null && causticsTextures.Length > 0)
            {
                timer += Time.deltaTime;
                if (timer >= (1.0f / fps))
                {
                    timer = 0;
                    index = (index + 1) % causticsTextures.Length;
                    lightSource.cookie = causticsTextures[index];
                }
            }
        }
    }
}
