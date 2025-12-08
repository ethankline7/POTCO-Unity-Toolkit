using UnityEngine;

namespace POTCO.Effects
{
    public class DefenseCannonballEffect : POTCOEffect
    {
        // This effect spawns OTHER effects based on ammo type.
        // We'll implement a generic "Explosion" version for preview.
        
        protected override void Start()
        {
            // Spawn ExplosionFlip (Generic Explosion)
            GameObject go = new GameObject("ExplosionFlip");
            go.transform.SetParent(transform, false);
            // We haven't implemented ExplosionFlip yet, use standard Explosion for now.
            go.AddComponent<ExplosionEffect>();
            
            duration = 2.0f;
            base.Start();
        }
    }
}
