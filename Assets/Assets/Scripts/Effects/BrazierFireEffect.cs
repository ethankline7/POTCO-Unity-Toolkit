using UnityEngine;

namespace POTCO.Effects
{
    public class BrazierFireEffect : POTCOEffect
    {
        protected override void Start()
        {
            // Composite: LightFire + GentleSmoke + LightSparks
            
            // 1. LightFire (BlueFlame but usually orange?)
            // LightFire.py is separate. But wait, BlueFlame is blue. LightFire is standard.
            // Assuming FireEffect can be reused or modified for LightFire.
            // LightFire typically has smaller scale.
            GameObject fireGO = new GameObject("Fire");
            fireGO.transform.SetParent(transform, false);
            var fire = fireGO.AddComponent<FireEffect>();
            fire.effectScale = 0.5f; // Smaller for brazier
            
            // 2. GentleSmoke
            // We haven't implemented GentleSmoke yet, but BlackSmoke is similar.
            // Let's use BlackSmoke with lighter color for now or create GentleSmoke stub.
            // Actually, I'll create GentleSmokeEffect next.
            // For now, skip smoke or use BlackSmoke.
            GameObject smokeGO = new GameObject("Smoke");
            smokeGO.transform.SetParent(transform, false);
            var smoke = smokeGO.AddComponent<BlackSmokeEffect>();
            // GentleSmoke is usually white/gray.
            smokeGO.GetComponent<ParticleSystemRenderer>().material.SetColor("_Color", new Color(0.8f, 0.8f, 0.8f, 0.5f));
            
            // 3. LightSparks
            // Need to implement LightSparks.
            
            duration = 10.0f;
            loop = true; // Braziers usually loop
            
            base.Start();
        }
    }
}
