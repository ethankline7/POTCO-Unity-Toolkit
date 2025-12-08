using UnityEngine;

namespace POTCO.Effects
{
    public class CannonMuzzleFireEffect : POTCOEffect
    {
        private GameObject splash;

        protected override void Start()
        {
            duration = 1.0f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            // Similar to CannonExplosion but for muzzle flash
            // cannonMuzzleFlash-zero
            // cannonMuzzleFlash-anim
            
            GameObject prefab = Resources.Load<GameObject>("phase_4/models/effects/cannonMuzzleFlash-zero");
            if (prefab == null) prefab = Resources.Load<GameObject>("phase_3/models/effects/cannonMuzzleFlash-zero");
            
            if (prefab != null)
            {
                splash = Instantiate(prefab, transform);
                splash.transform.localPosition = Vector3.zero;
                
                // Animation
                RuntimeAnimatorPlayer player = splash.GetComponentInChildren<RuntimeAnimatorPlayer>();
                if (player == null) player = splash.AddComponent<RuntimeAnimatorPlayer>();
                
                AnimationClip clip = Resources.Load<AnimationClip>("phase_4/models/effects/cannonMuzzleFlash-anim");
                if (clip == null) clip = Resources.Load<AnimationClip>("phase_3/models/effects/cannonMuzzleFlash-anim");
                
                if (clip != null)
                {
                    player.AddClip(clip, "splashdown");
                    player.Play("splashdown");
                    
                    // Anim Duration calculation from python: getDuration * 0.3
                    float animDuration = clip.length * 0.3f;
                    StartCoroutine(FadeOutRoutine(animDuration));
                }
            }
        }
        
        private System.Collections.IEnumerator FadeOutRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            // Fade Out
            float fadeTime = 0.6f;
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
