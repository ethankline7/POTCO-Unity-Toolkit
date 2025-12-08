using UnityEngine;

namespace POTCO.Effects
{
    public class CannonExplosionEffect : POTCOEffect
    {
        private GameObject splash;

        protected override void Start()
        {
            duration = 2.0f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            // Load Animated Model
            // cannonballExplosion-zero (model)
            // cannonballExplosion-anim (anim)
            // EggImporter should combine these if they are in same folder or named correctly.
            // Assuming "phase_4/models/effects/cannonballExplosion.egg" contains anims?
            // Python loads -zero then loadAnims -anim.
            // In Unity, we hopefully imported `cannonballExplosion.egg` with animations.
            
            // Try loading the combined asset
            // The importer might have named it cannonballExplosion-zero if imported directly.
            // Or maybe we have a folder `cannonballExplosion`?
            // Let's try loading from phase_4 effects.
            GameObject prefab = Resources.Load<GameObject>("phase_4/models/effects/cannonballExplosion-zero");
            
            // If not found, check if we have an FBX or other format
            // Or maybe it's in phase 3?
            if (prefab == null) prefab = Resources.Load<GameObject>("phase_3/models/effects/cannonballExplosion-zero");
            
            if (prefab != null)
            {
                splash = Instantiate(prefab, transform);
                splash.transform.localPosition = Vector3.zero;
                
                // Check for Animation component
                // New EggImporter creates AnimationClips.
                // We need to find the clip named "splashdown" or similar.
                // It might be named after the file "cannonballExplosion-anim".
                
                RuntimeAnimatorPlayer player = splash.GetComponentInChildren<RuntimeAnimatorPlayer>();
                if (player == null) player = splash.AddComponent<RuntimeAnimatorPlayer>(); // If missing
                
                // Load animation clip separately if needed?
                // Python loads 'models/effects/cannonballExplosion-anim'.
                AnimationClip clip = Resources.Load<AnimationClip>("phase_4/models/effects/cannonballExplosion-anim");
                if (clip == null) clip = Resources.Load<AnimationClip>("phase_3/models/effects/cannonballExplosion-anim");
                
                if (clip != null)
                {
                    player.AddClip(clip, "splashdown");
                    player.Play("splashdown");
                }
                else
                {
                    // Try finding embedded clip
                    // EggImporter embeds clips in the main asset if found.
                    // Maybe "cannonballExplosion-anim" was imported as a separate asset?
                    // If so, we need to load it.
                    // For now, if clip is missing, it just won't animate.
                }
                
                // Fade Out Logic
                StartCoroutine(FadeOutRoutine());
            }
        }
        
        private System.Collections.IEnumerator FadeOutRoutine()
        {
            yield return new WaitForSeconds(0.35f); // animDuration
            
            // Fade Out
            float fadeTime = 0.8f;
            float t = 0;
            Renderer[] renderers = splash.GetComponentsInChildren<Renderer>();
            
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                float alpha = 1.0f - (t / fadeTime);
                
                foreach (var r in renderers)
                {
                    foreach (var m in r.materials)
                    {
                        if (m.HasProperty("_Color"))
                        {
                            Color c = m.color;
                            c.a = alpha;
                            m.color = c;
                        }
                    }
                }
                yield return null;
            }
            
            splash.SetActive(false);
        }
    }
}
