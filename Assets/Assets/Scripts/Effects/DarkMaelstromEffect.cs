using UnityEngine;
using System.Collections.Generic;

namespace POTCO.Effects
{
    public class DarkMaelstromEffect : POTCOEffect
    {
        // DarkMaelstrom is a complex composite environment effect.
        // It loads "models/effects/GhostShipFX" and animates lightning and swirling clouds.
        // For this request, we'll focus on spawning the model and setting up the animation logic.
        
        private GameObject glow;
        private GameObject[] bolts;
        private Material[] boltMats;
        
        private Transform[] stormTops;
        private Material[] stormTopMats;

        protected override void Start()
        {
            duration = Mathf.Infinity; // Environmental
            loop = true;
            InitializeSystem();
            base.Start();
        }

        private void InitializeSystem()
        {
            // models/effects/GhostShipFX
            GameObject prefab = Resources.Load<GameObject>("phase_4/models/effects/GhostShipFX");
            if (prefab == null) prefab = Resources.Load<GameObject>("phase_3/models/effects/GhostShipFX");
            
            if (prefab != null)
            {
                glow = Instantiate(prefab, transform);
                glow.transform.localPosition = Vector3.zero;
                
                // Find Lightning Bolts
                // lightning_1, lightning_2, lightning_3, lightning_4
                bolts = new GameObject[4];
                boltMats = new Material[4];
                
                for (int i=0; i<4; i++)
                {
                    string name = $"lightning_{i+1}";
                    Transform t = FindDeepChild(glow.transform, name);
                    if (t != null)
                    {
                        bolts[i] = t.gameObject;
                        Renderer r = t.GetComponent<Renderer>();
                        if (r != null)
                        {
                            boltMats[i] = new Material(r.sharedMaterial);
                            boltMats[i].shader = Shader.Find("EggImporter/ParticleAdditive");
                            boltMats[i].SetColor("_Color", new Color(0,0,0,0)); // Start invisible
                            r.material = boltMats[i];
                        }
                    }
                }
                
                // Find Swirls (Swirl_*)
                // Python: findAllMatches('**/Swirl_*')
                // In Unity, we search children.
                List<Transform> swirls = new List<Transform>();
                FindChildrenWithName(glow.transform, "Swirl", swirls);
                
                stormTops = swirls.ToArray();
                stormTopMats = new Material[stormTops.Length];
                
                for(int i=0; i<stormTops.Length; i++)
                {
                    Renderer r = stormTops[i].GetComponent<Renderer>();
                    if (r != null)
                    {
                        stormTopMats[i] = new Material(r.sharedMaterial);
                        // Likely additive or alpha blend? "ColorBlendAttrib.MAdd".
                        stormTopMats[i].shader = Shader.Find("EggImporter/ParticleAdditive");
                        r.material = stormTopMats[i];
                    }
                }
                
                StartCoroutine(LightningRoutine());
            }
        }
        
        private System.Collections.IEnumerator LightningRoutine()
        {
            while (isPlaying)
            {
                yield return new WaitForSeconds(1.0f);
                yield return FlashBolt(0);
                yield return new WaitForSeconds(0.5f);
                yield return FlashBolt(1);
                yield return new WaitForSeconds(2.0f);
                yield return FlashBolt(2);
                yield return new WaitForSeconds(1.5f);
                yield return FlashBolt(3);
            }
        }
        
        private System.Collections.IEnumerator FlashBolt(int index)
        {
            if (index >= bolts.Length || bolts[index] == null) yield break;
            
            // Flash In 0.1s
            float t = 0;
            while (t < 0.1f)
            {
                t += Time.deltaTime;
                float alpha = t / 0.1f;
                if (boltMats[index] != null) boltMats[index].SetColor("_Color", new Color(1,1,1,alpha));
                yield return null;
            }
            
            yield return new WaitForSeconds(0.1f);
            
            // Flash Out 0.1s
            t = 0;
            while (t < 0.1f)
            {
                t += Time.deltaTime;
                float alpha = 1.0f - (t / 0.1f);
                if (boltMats[index] != null) boltMats[index].SetColor("_Color", new Color(1,1,1,alpha));
                yield return null;
            }
            if (boltMats[index] != null) boltMats[index].SetColor("_Color", new Color(0,0,0,0));
        }

        protected override void Update()
        {
            base.Update();
            
            if (isPlaying && stormTops != null)
            {
                // Rotate & Scroll UVs
                // Duration ~20-40s.
                // Rotate 360.
                float rotSpeed = 360.0f / 30.0f; // Approx 12 deg/sec
                
                for (int i=0; i<stormTops.Length; i++)
                {
                    if (stormTops[i] != null)
                    {
                        // Python: Top 0 rotates +360, Top 1 rotates 0 (start 360?) -> -360?
                        // Python: rotate = Parallel(hprInterval(360,0,0), hprInterval(0,0,0, start=360))
                        // So one spins Left, one spins Right (or stops?).
                        // Assuming counter-rotation.
                        float dir = (i % 2 == 0) ? 1.0f : -1.0f;
                        stormTops[i].Rotate(0, 0, dir * rotSpeed * Time.deltaTime);
                        
                        // UV Scroll
                        // 0->1 and 1->0.
                        if (stormTopMats[i] != null)
                        {
                            float offset = (Time.time * 0.05f) % 1.0f; // Slow scroll
                            // UVs offset, offset.
                            stormTopMats[i].mainTextureOffset = new Vector2(offset, offset);
                        }
                    }
                }
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
        
        private void FindChildrenWithName(Transform parent, string nameContains, List<Transform> results)
        {
            foreach(Transform child in parent)
            {
                if(child.name.Contains(nameContains)) results.Add(child);
                FindChildrenWithName(child, nameContains, results);
            }
        }
    }
}
