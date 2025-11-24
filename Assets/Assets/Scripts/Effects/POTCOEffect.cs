using UnityEngine;
using System.Collections;

namespace POTCO.Effects
{
    /// <summary>
    /// Base class for all ported POTCO effects.
    /// Handles common lifecycle management and resource loading.
    /// </summary>
    public abstract class POTCOEffect : MonoBehaviour
    {
        [Header("Effect Settings")]
        public float duration = 10.0f;
        public float effectScale = 1.0f;
        public bool loop = false;
        
        protected float age = 0f;
        protected bool isPlaying = false;

        protected virtual void Start()
        {
            StartEffect();
        }

        protected virtual void Update()
        {
            if (isPlaying && !loop)
            {
                age += Time.deltaTime;
                if (age >= duration)
                {
                    StopEffect();
                }
            }
        }

        public virtual void StartEffect()
        {
            isPlaying = true;
            age = 0f;
        }

        public virtual void StopEffect()
        {
            isPlaying = false;
            // Default behavior: destroy self
            Destroy(gameObject);
        }

        /// <summary>
        /// Helper to find a specific texture/material from the particleMaps egg/prefab.
        /// </summary>
        protected Material GetMaterialFromParticleMap(string nodeName)
        {
            // In POTCO, particleMaps is a single model file containing many billboard cards.
            // We need to find the specific child node (e.g. 'particleFire2') and extract its material.
            
            // Try to load the particleMaps prefab (assumed to be imported at this path)
            GameObject particleMaps = Resources.Load<GameObject>("phase_2/models/effects/particleMaps");
            
            if (particleMaps == null)
            {
                Debug.LogWarning("[POTCOEffect] Could not find 'phase_2/models/effects/particleMaps' in Resources.");
                return null;
            }

            Transform child = FindDeepChild(particleMaps.transform, nodeName);
            if (child != null)
            {
                Renderer r = child.GetComponent<Renderer>();
                if (r != null)
                {
                    // Clone the material so we can modify it without affecting the source
                    return new Material(r.sharedMaterial);
                }
            }
            else
            {
                Debug.LogWarning($"[POTCOEffect] Could not find node '{nodeName}' inside particleMaps.");
            }

            return null;
        }

        private Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform result = FindDeepChild(child, name);
                if (result != null) return result;
            }
            return null;
        }
        
        /// <summary>
        /// Sets up a standard Particle System with common POTCO settings
        /// </summary>
        protected ParticleSystem SetupParticleSystem(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            
            // Rotate -90 on X because Unity particles emit +Z (up/forward depending on shape) 
            // while Panda emitters often emit in +Z (up).
            // We'll adjust this per effect, but starting identity is safer.
            go.transform.localRotation = Quaternion.identity;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false; // We control start
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            
            return ps;
        }
    }
}
