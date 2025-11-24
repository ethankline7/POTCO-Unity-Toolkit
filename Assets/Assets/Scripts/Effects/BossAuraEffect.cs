using UnityEngine;

namespace POTCO.Effects
{
    public class BossAuraEffect : POTCOEffect
    {
        [Header("Boss Aura Settings")]
        public Color effectColor = Color.white;
        
        private GameObject auraModel;
        private Renderer innerRenderer;
        private Renderer outerRenderer;
        private Material innerMat;
        private Material outerMat;

        protected override void Start()
        {
            duration = 10.0f;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            GameObject prefab = Resources.Load<GameObject>("phase_3/models/effects/bossAura");
            if (prefab != null)
            {
                auraModel = Instantiate(prefab, transform);
                auraModel.transform.localPosition = Vector3.zero;
                
                // Inner and Outer meshes
                Transform inner = FindDeepChild(auraModel.transform, "inner");
                Transform outer = FindDeepChild(auraModel.transform, "outer");
                
                if (inner != null)
                {
                    innerRenderer = inner.GetComponent<Renderer>();
                    if (innerRenderer != null)
                    {
                        innerMat = new Material(innerRenderer.sharedMaterial);
                        innerMat.shader = Shader.Find("EggImporter/ParticleAdditive");
                        innerMat.SetColor("_Color", new Color(0,0,0,0)); // Start invisible
                        innerRenderer.material = innerMat;
                    }
                }
                
                if (outer != null)
                {
                    outerRenderer = outer.GetComponent<Renderer>();
                    if (outerRenderer != null)
                    {
                        outerMat = new Material(outerRenderer.sharedMaterial);
                        outerMat.shader = Shader.Find("EggImporter/ParticleAdditive");
                        outerMat.SetColor("_Color", new Color(0,0,0,0)); // Start invisible
                        outerRenderer.material = outerMat;
                    }
                }
            }
        }

        protected override void Update()
        {
            base.Update();
            
            if (isPlaying && auraModel != null)
            {
                // Billboard to camera
                auraModel.transform.LookAt(Camera.main.transform);
                
                // UV Scroll
                // LerpFunctionInterval 4.0, toData=-1.0, fromData=1.0
                // Speed = -2.0 / 4.0 = -0.5 units/sec
                float t = Mathf.Repeat(age * 0.5f, 1.0f);
                // Python: toData=-1, fromData=1. Wait, is it looping? "uvScroll.loop"
                // So it goes 1 -> -1 continuously? Or resets?
                // Usually loop means repeating the interval.
                // 1 -> -1 is a distance of 2. Duration 4s.
                
                float offset = 1.0f - (age % 4.0f) / 4.0f * 2.0f; // 1 to -1
                
                // Inner: 2 * offset
                // Outer: offset
                if (innerMat != null) innerMat.mainTextureOffset = new Vector2(0, 2 * offset);
                if (outerMat != null) outerMat.mainTextureOffset = new Vector2(0, offset);
                
                // Fade In/Out Logic
                // FadeIn: 2.0s -> (1,1,1,0.25)
                // FadeOut: 2.0s -> (0,0,0,0)
                // Sequence(FadeIn, Wait(10), FadeOut)
                
                Color targetColor = effectColor;
                targetColor.a = 0.25f; // Max alpha
                
                Color currentColor = Color.clear;
                
                if (age < 2.0f)
                {
                    float fadeInT = age / 2.0f;
                    currentColor = Color.Lerp(Color.clear, targetColor, fadeInT);
                }
                else if (age < duration - 2.0f) // Wait(10) is middle
                {
                    currentColor = targetColor;
                }
                else
                {
                    float fadeOutT = (age - (duration - 2.0f)) / 2.0f;
                    currentColor = Color.Lerp(targetColor, Color.clear, fadeOutT);
                }
                
                if (innerMat != null) innerMat.SetColor("_Color", currentColor);
                if (outerMat != null) outerMat.SetColor("_Color", currentColor);
            }
        }
        
        private Transform FindDeepChild(Transform parent, string name)
        {
            foreach(Transform child in parent)
            {
                if(child.name == name) return child;
                Transform result = FindDeepChild(child, name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
