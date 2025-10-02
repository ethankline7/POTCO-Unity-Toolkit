using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace POTCO.ShipBuilder
{
    public class ShipComponentDatabase
    {
        private Dictionary<string, string> shipHulls = new Dictionary<string, string>(); // name -> path
        private Dictionary<string, string> shipComponents = new Dictionary<string, string>(); // name -> path

        public void Initialize()
        {
            shipHulls.Clear();
            shipComponents.Clear();

            // Find all ship models in Resources folders
            string[] searchFolders = new string[]
            {
                "Assets/Resources/phase_3/models/shipparts",
                "Assets/Resources/phase_4/models/shipparts",
                "Assets/Resources/phase_5/models/shipparts",
                "Assets/Resources/phase_6/models/shipparts"
            };

            foreach (string folder in searchFolders)
            {
                if (!System.IO.Directory.Exists(folder)) continue;

                string[] files = System.IO.Directory.GetFiles(folder, "*.egg");

                foreach (string file in files)
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(file);

                    // Skip logic files for now
                    if (fileName.EndsWith("_logic")) continue;

                    // Get the Resources-relative path
                    string resourcePath = GetResourcePath(file);

                    // Categorize by prefix
                    // Check for ship parts first (pir_m_shp_prt_, pir_m_shp_ram_, etc.)
                    if (fileName.StartsWith("pir_m_shp_prt_") || fileName.StartsWith("pir_m_shp_ram_") ||
                        fileName.StartsWith("pir_r_shp_") || fileName.StartsWith("prow_") || fileName.StartsWith("repair_spot_"))
                    {
                        // Ship component
                        shipComponents[fileName] = resourcePath;
                    }
                    else if (fileName.StartsWith("pir_m_shp_"))
                    {
                        // Base ship hull (only if not a part)
                        shipHulls[fileName] = resourcePath;
                    }
                }
            }

            // Also search char folder for masts
            string[] charFolders = new string[]
            {
                "Assets/Resources/phase_3/models/char"
            };

            foreach (string folder in charFolders)
            {
                if (!System.IO.Directory.Exists(folder)) continue;

                string[] files = System.IO.Directory.GetFiles(folder, "pir_r_shp_*.egg");

                foreach (string file in files)
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                    if (fileName.EndsWith("_logic")) continue;

                    string resourcePath = GetResourcePath(file);

                    if (fileName.StartsWith("pir_r_shp_"))
                    {
                        shipComponents[fileName] = resourcePath;
                    }
                }
            }

            // Search props folder for repair spots
            string[] propsFolders = new string[]
            {
                "Assets/Resources/phase_4/models/props"
            };

            foreach (string folder in propsFolders)
            {
                if (!System.IO.Directory.Exists(folder)) continue;

                string[] files = System.IO.Directory.GetFiles(folder, "repair_spot_*.egg");

                foreach (string file in files)
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                    string resourcePath = GetResourcePath(file);
                    shipComponents[fileName] = resourcePath;
                }
            }

            Debug.Log($"Ship Builder Database initialized: {shipHulls.Count} hulls, {shipComponents.Count} components");
        }

        public string[] GetAvailableHulls()
        {
            return shipHulls.Keys.OrderBy(x => x).ToArray();
        }

        public string[] GetComponentsByPrefix(string prefix)
        {
            List<string> components = new List<string>();

            // Add "None" option
            components.Add("<None>");

            // Filter components by prefix or exact match
            foreach (string componentName in shipComponents.Keys.OrderBy(x => x))
            {
                if (componentName.StartsWith(prefix) || componentName.Contains(prefix))
                {
                    components.Add(componentName);
                }
            }

            return components.ToArray();
        }

        public GameObject LoadShipHull(string hullName)
        {
            if (!shipHulls.ContainsKey(hullName))
            {
                Debug.LogError($"Hull not found: {hullName}");
                return null;
            }

            GameObject prefab = Resources.Load<GameObject>(shipHulls[hullName]);
            if (prefab != null)
            {
                return GameObject.Instantiate(prefab);
            }

            Debug.LogError($"Failed to load hull: {hullName} from {shipHulls[hullName]}");
            return null;
        }

        public GameObject LoadShipLogic(string logicName)
        {
            // Try to find the logic file
            string[] searchFolders = new string[]
            {
                "Assets/Resources/phase_3/models/shipparts",
                "Assets/Resources/phase_4/models/shipparts",
                "Assets/Resources/phase_5/models/shipparts",
                "Assets/Resources/phase_6/models/shipparts"
            };

            foreach (string folder in searchFolders)
            {
                if (!System.IO.Directory.Exists(folder)) continue;

                string filePath = System.IO.Path.Combine(folder, logicName + ".egg");
                if (System.IO.File.Exists(filePath))
                {
                    string resourcePath = GetResourcePath(filePath);
                    GameObject prefab = Resources.Load<GameObject>(resourcePath);
                    if (prefab != null)
                    {
                        return GameObject.Instantiate(prefab);
                    }
                }
            }

            Debug.LogError($"Logic file not found: {logicName}");
            return null;
        }

        public GameObject LoadComponent(string componentName)
        {
            if (componentName == "<None>" || string.IsNullOrEmpty(componentName))
                return null;

            if (!shipComponents.ContainsKey(componentName))
            {
                Debug.LogWarning($"Component not found: {componentName}");
                return null;
            }

            GameObject prefab = Resources.Load<GameObject>(shipComponents[componentName]);
            if (prefab != null)
            {
                return GameObject.Instantiate(prefab);
            }

            Debug.LogError($"Failed to load component: {componentName} from {shipComponents[componentName]}");
            return null;
        }

        private string GetResourcePath(string fullPath)
        {
            // Convert full file path to Resources-relative path
            // Example: "Assets/Resources/phase_3/models/shipparts/pir_m_shp_brig_light.egg"
            //       -> "phase_3/models/shipparts/pir_m_shp_brig_light"

            string normalizedPath = fullPath.Replace("\\", "/");
            int resourcesIndex = normalizedPath.IndexOf("Resources/");

            if (resourcesIndex >= 0)
            {
                string relativePath = normalizedPath.Substring(resourcesIndex + "Resources/".Length);
                // Remove file extension
                int lastDot = relativePath.LastIndexOf('.');
                if (lastDot > 0)
                {
                    relativePath = relativePath.Substring(0, lastDot);
                }
                return relativePath;
            }

            return fullPath;
        }
    }
}
