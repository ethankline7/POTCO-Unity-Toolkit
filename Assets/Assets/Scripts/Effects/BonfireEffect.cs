using UnityEngine;

namespace POTCO.Effects
{
    public class BonfireEffect : POTCOEffect
    {
        protected override void Start()
        {
            // Composite Effect
            // Instantiates Fire and BlackSmoke
            
            GameObject fireGO = new GameObject("Fire");
            fireGO.transform.SetParent(transform, false);
            var fire = fireGO.AddComponent<FireEffect>();
            // fire.effectScale = 1.0f; // Default
            
            GameObject smokeGO = new GameObject("Smoke");
            smokeGO.transform.SetParent(transform, false);
            var smoke = smokeGO.AddComponent<BlackSmokeEffect>();
            
            // Infinite duration for bonfire loop
            duration = Mathf.Infinity;
            loop = true;
            
            base.Start();
        }
    }
}
