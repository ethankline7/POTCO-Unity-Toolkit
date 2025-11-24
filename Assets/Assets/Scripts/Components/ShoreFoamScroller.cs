using UnityEngine;

namespace POTCO
{
    public enum FoamMotionType
    {
        ScrollU, // Constant scrolling sideways
        ScrollV, // Constant scrolling inward/outward
        TideV    // Oscillating inward and outward
    }

    public class ShoreFoamScroller : MonoBehaviour
    {
        public FoamMotionType motionType = FoamMotionType.TideV;
        
        [Header("Wave Settings")]
        public float scrollSpeed = 1.0f;   // Frequency for Tide, Speed for Scroll
        public float amplitude = 0.15f;    // Distance to move (Tide only)
        public float phaseOffset = 0f;     // Start offset
        
        private float currentVal;
        private Renderer rend;

        void Awake() { rend = GetComponent<Renderer>(); }

        void Update()
        {
            if (!rend) return;

            if (motionType == FoamMotionType.TideV)
            {
                // Sine wave for tide: washes in and out
                // -1 to 1 oscillation scaled by amplitude
                // We subtract time to make it move "inward" first usually, depending on UVs
                float sine = Mathf.Sin((Time.time * scrollSpeed) + phaseOffset);
                
                // Apply V offset
                SetProp("_FoamV", sine * amplitude);
            }
            else if (motionType == FoamMotionType.ScrollU)
            {
                currentVal = Mathf.Repeat(currentVal + scrollSpeed * Time.deltaTime, 1f);
                SetProp("_FoamU", currentVal);
            }
            else if (motionType == FoamMotionType.ScrollV)
            {
                currentVal = Mathf.Repeat(currentVal + scrollSpeed * Time.deltaTime, 1f);
                SetProp("_FoamV", currentVal);
            }
        }
        
        void SetProp(string name, float val)
        {
            foreach (var m in rend.materials)
            {
                if (m.HasProperty(name)) m.SetFloat(name, val);
            }
        }
    }
}