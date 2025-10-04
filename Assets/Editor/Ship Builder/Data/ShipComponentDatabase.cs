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

        // NEW: POTCO ship data parser
        private POTCOShipDataParser potcoParser;
        private bool potcoDataLoaded = false;

        public void Initialize()
        {
            Debug.Log("[ShipComponentDatabase] Initializing...");
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

            // NEW: Initialize POTCO data
            InitializePOTCOData();
        }

        // NEW: Initialize POTCO ship data from Python files
        public void InitializePOTCOData()
        {
            if (potcoDataLoaded) return;

            string potcoSourcePath = "Assets/Editor/POTCO_Source";
            if (!System.IO.Directory.Exists(potcoSourcePath))
            {
                Debug.LogWarning($"POTCO_Source folder not found at {potcoSourcePath}");
                return;
            }

            potcoParser = new POTCOShipDataParser();
            potcoParser.ParseAllPOTCOData(potcoSourcePath);
            potcoDataLoaded = true;
        }

        // NEW: Get list of available ship presets for dropdown
        public string[] GetAvailableShipPresets()
        {
            if (!potcoDataLoaded || potcoParser == null)
            {
                return new string[] { "Custom Ship (No Preset)" };
            }

            List<string> presets = new List<string>();
            presets.Add("Custom Ship (No Preset)"); // Index 0 = custom

            // Get all ship configs sorted by name
            var sortedShips = potcoParser.ShipConfigs
                .OrderBy(kvp => potcoParser.ShipDisplayNames.ContainsKey(kvp.Key) ? potcoParser.ShipDisplayNames[kvp.Key] : kvp.Key.ToString())
                .ToList();

            foreach (var kvp in sortedShips)
            {
                int shipID = kvp.Key;
                string displayName = potcoParser.ShipDisplayNames.ContainsKey(shipID)
                    ? potcoParser.ShipDisplayNames[shipID]
                    : $"Ship #{shipID}";

                presets.Add($"{displayName} (ID: {shipID})");
            }

            return presets.ToArray();
        }

        // NEW: Get ship IDs in same order as GetAvailableShipPresets()
        public int[] GetShipPresetIDs()
        {
            if (!potcoDataLoaded || potcoParser == null)
            {
                return new int[] { -1 };
            }

            List<int> ids = new List<int>();
            ids.Add(-1); // Index 0 = custom ship

            var sortedShips = potcoParser.ShipConfigs
                .OrderBy(kvp => potcoParser.ShipDisplayNames.ContainsKey(kvp.Key) ? potcoParser.ShipDisplayNames[kvp.Key] : kvp.Key.ToString())
                .ToList();

            foreach (var kvp in sortedShips)
            {
                ids.Add(kvp.Key);
            }

            return ids.ToArray();
        }

        // NEW: Get ship configuration data by ID
        public ShipConfigData GetShipConfig(int shipID)
        {
            if (!potcoDataLoaded || potcoParser == null) return null;

            if (potcoParser.ShipConfigs.TryGetValue(shipID, out ShipConfigData config))
            {
                // Fill in display name if available
                if (potcoParser.ShipDisplayNames.TryGetValue(shipID, out string displayName))
                {
                    config.displayName = displayName;
                }
                return config;
            }

            return null;
        }

        // NEW: Get hull model name for a ship ID
        public string GetHullModelName(int shipID)
        {
            if (!potcoDataLoaded || potcoParser == null) return null;

            if (potcoParser.HullModelNames.TryGetValue(shipID, out string modelName))
            {
                // All ship hulls use pir_m_shp_ prefix
                return $"pir_m_shp_{modelName}";
            }

            return null;
        }

        // NEW: Get style texture name (hull textures)
        public string GetStyleTexture(int styleID)
        {
            if (!potcoDataLoaded || potcoParser == null) return null;

            if (potcoParser.StyleTextures.TryGetValue(styleID, out string textureName))
            {
                return textureName;
            }

            return null;
        }

        // NEW: Get sail texture name (sail textures)
        public string GetSailTexture(int styleID)
        {
            if (!potcoDataLoaded || potcoParser == null) return null;

            if (potcoParser.SailTextures.TryGetValue(styleID, out string textureName))
            {
                return textureName;
            }

            return null;
        }

        // NEW: Get logo texture name
        public string GetLogoTexture(int logoID)
        {
            if (!potcoDataLoaded || potcoParser == null) return null;

            if (potcoParser.LogoTextures.TryGetValue(logoID, out string textureName))
            {
                return textureName;
            }

            return null;
        }

        // NEW: Get mast type prefix and max height
        public MastTypeData GetMastTypeData(int mastID)
        {
            if (!potcoDataLoaded || potcoParser == null) return null;

            if (potcoParser.MastTypes.TryGetValue(mastID, out MastTypeData mastData))
            {
                return mastData;
            }

            return null;
        }

        // NEW: Get mast model name from mast config
        public string GetMastModelName(MastConfig config)
        {
            if (!config.IsValid())
            {
                Debug.LogWarning($"[GetMastModelName] Invalid mast config: mastType={config.mastType}, height={config.height}");
                return null;
            }

            MastTypeData mastData = GetMastTypeData(config.mastType);
            if (mastData == null)
            {
                Debug.LogWarning($"[GetMastModelName] No mast type data found for mastType ID: {config.mastType}");
                return null;
            }

            // Build mast name: pir_r_shp_mst_ + prefix
            // Prefix already includes skeleton suffix if needed (e.g., "main_square_skeletonA")
            string modelName = $"pir_r_shp_mst_{mastData.prefix}";
            Debug.Log($"[GetMastModelName] mastType={config.mastType}, prefix={mastData.prefix} → {modelName}");
            return modelName;
        }

        // NEW: Get prow/bowsprit model name from ID
        public string GetProwModelName(int prowID)
        {
            if (!potcoDataLoaded || potcoParser == null) return null;
            if (prowID == 0) return null;

            // Prow models follow pattern in BowSpritDict
            // For now, return common prow patterns
            // TODO: Parse BowSpritDict from ShipBlueprints.py if needed
            if (prowID == 1) return "prow_skeleton_zero"; // Skeleton
            if (prowID == 2) return "prow_female_zero";  // Lady

            return null;
        }

        // NEW: Get deck cannon model name from cannon ID
        public string GetDeckCannonModelName(int cannonID)
        {
            if (!potcoDataLoaded || potcoParser == null) return null;
            if (cannonID == 0) return null;

            if (potcoParser.CannonTypes.TryGetValue(cannonID, out string suffix))
            {
                // Deck cannons: pir_r_shp_can_deck_{suffix}
                return $"pir_r_shp_can_deck_{suffix}";
            }

            return null;
        }

        // NEW: Get broadside cannon model name from cannon ID
        public string GetBroadsideCannonModelName(int cannonID)
        {
            if (!potcoDataLoaded || potcoParser == null) return null;
            if (cannonID == 0) return null;

            if (potcoParser.CannonTypes.TryGetValue(cannonID, out string suffix))
            {
                // Broadside cannons: pir_r_shp_can_broadside_{suffix}
                return $"pir_r_shp_can_broadside_{suffix}";
            }

            return null;
        }

        // NEW: Get available hull styles for customization UI
        public (string[] names, int[] ids) GetAvailableHullStyles()
        {
            if (!potcoDataLoaded || potcoParser == null)
            {
                return (new string[] { "Player (Default)" }, new int[] { 0 });
            }

            List<string> names = new List<string>();
            List<int> ids = new List<int>();

            // Filter hull styles (IDs 0-34, excluding sail-only colors 100+)
            var hullStyles = potcoParser.StyleIDConstants
                .Where(kvp => kvp.Value < 100)
                .OrderBy(kvp => kvp.Value)
                .ToList();

            foreach (var kvp in hullStyles)
            {
                // Only include styles that have hull textures
                if (potcoParser.StyleTextures.ContainsKey(kvp.Value))
                {
                    names.Add(FormatStyleName(kvp.Key));
                    ids.Add(kvp.Value);
                }
            }

            return (names.ToArray(), ids.ToArray());
        }

        // NEW: Get available sail colors for customization UI
        public (string[] names, int[] ids) GetAvailableSailColors()
        {
            if (!potcoDataLoaded || potcoParser == null)
            {
                return (new string[] { "White (Default)" }, new int[] { 100 });
            }

            List<string> names = new List<string>();
            List<int> ids = new List<int>();

            // Get ALL styles that have sail textures (not just IDs 100+)
            // ColorDict includes both low IDs (Navy, EITC, Treasure styles) and high IDs (SailWhite, etc.)
            var sailColors = potcoParser.StyleIDConstants
                .Where(kvp => potcoParser.SailTextures.ContainsKey(kvp.Value))
                .OrderBy(kvp => kvp.Value)
                .ToList();

            foreach (var kvp in sailColors)
            {
                names.Add(FormatStyleName(kvp.Key));
                ids.Add(kvp.Value);
            }

            return (names.ToArray(), ids.ToArray());
        }

        // NEW: Get available logos for customization UI
        public (string[] names, int[] ids) GetAvailableLogos()
        {
            if (!potcoDataLoaded || potcoParser == null)
            {
                return (new string[] { "No Logo" }, new int[] { 0 });
            }

            List<string> names = new List<string>();
            List<int> ids = new List<int>();

            // Add "No Logo" option first
            names.Add("No Logo");
            ids.Add(0);

            // Get all logos sorted by ID
            var logos = potcoParser.LogoIDConstants
                .OrderBy(kvp => kvp.Value)
                .ToList();

            foreach (var kvp in logos)
            {
                if (kvp.Value > 0 && potcoParser.LogoTextures.ContainsKey(kvp.Value))
                {
                    names.Add(FormatLogoName(kvp.Key));
                    ids.Add(kvp.Value);
                }
            }

            return (names.ToArray(), ids.ToArray());
        }

        // NEW: Format style name for display
        private string FormatStyleName(string rawName)
        {
            // Convert "SailWhite" -> "White", "BountyHunter_A" -> "Bounty Hunter A", etc.
            string name = rawName.Replace("Sail", "").Replace("_", " ");

            // Add spaces before capital letters
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                {
                    sb.Append(' ');
                }
                sb.Append(name[i]);
            }

            return sb.ToString();
        }

        // NEW: Format logo name for display
        private string FormatLogoName(string rawName)
        {
            // Convert "Player_Hawk" -> "Player: Hawk", "Contest_Skull" -> "Contest: Skull", etc.
            string name = rawName.Replace("_", ": ");

            // Add spaces before capital letters
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]) && name[i - 1] != ':' && name[i - 1] != ' ')
                {
                    sb.Append(' ');
                }
                sb.Append(name[i]);
            }

            return sb.ToString();
        }

        // NEW: Load texture for preview in editor
        public Texture2D LoadTextureForPreview(string textureName)
        {
            if (string.IsNullOrEmpty(textureName)) return null;

            Debug.Log($"[LoadTextureForPreview] Searching for texture: {textureName}");

            // Search for texture in Resources folders
            string[] searchFolders = new string[]
            {
                "Assets/Resources/phase_2/models/textureCards",
                "Assets/Resources/phase_3/models/textureCards",
                "Assets/Resources/phase_4/models/textureCards",
                "Assets/Resources/phase_5/models/textureCards",
                "Assets/Resources/phase_3/models/shipparts",
                "Assets/Resources/phase_4/models/shipparts",
                "Assets/Resources/phase_5/models/shipparts"
            };

            // Try searching through all .egg files (including .bam converted to .egg)
            foreach (string folder in searchFolders)
            {
                if (!System.IO.Directory.Exists(folder)) continue;

                string[] eggFiles = System.IO.Directory.GetFiles(folder, "*.egg");

                foreach (string file in eggFiles)
                {
                    string resourcePath = GetResourcePath(file);
                    GameObject prefab = Resources.Load<GameObject>(resourcePath);

                    if (prefab != null)
                    {
                        // Search through all children transforms (not just renderers)
                        Transform[] allTransforms = prefab.GetComponentsInChildren<Transform>(true);
                        foreach (Transform t in allTransforms)
                        {
                            // Check if this transform's name matches the texture name
                            if (t.name == textureName)
                            {
                                // Try to get renderer from this specific transform
                                Renderer renderer = t.GetComponent<Renderer>();
                                if (renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.mainTexture != null)
                                {
                                    Debug.Log($"✅ Found texture '{textureName}' in {file} on object '{t.name}'");
                                    return renderer.sharedMaterial.mainTexture as Texture2D;
                                }
                            }
                        }

                        // Also search through all renderers (fallback)
                        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
                        foreach (Renderer renderer in renderers)
                        {
                            if (renderer.sharedMaterial != null && renderer.sharedMaterial.mainTexture != null)
                            {
                                if (renderer.sharedMaterial.mainTexture.name == textureName)
                                {
                                    Debug.Log($"✅ Found texture '{textureName}' in {file} (via material check)");
                                    return renderer.sharedMaterial.mainTexture as Texture2D;
                                }
                            }
                        }
                    }
                }
            }

            Debug.LogWarning($"⚠️ Texture '{textureName}' not found in any Resources folders");
            return null;
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

            // Filter components by prefix or exact match, excluding collision objects
            foreach (string componentName in shipComponents.Keys.OrderBy(x => x))
            {
                // Exclude collision objects
                if (componentName.Contains("_collision") || componentName.EndsWith("_collisions"))
                {
                    continue;
                }

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

        public GameObject GetComponentPrefab(string componentName)
        {
            if (componentName == "<None>" || string.IsNullOrEmpty(componentName))
                return null;

            if (!shipComponents.ContainsKey(componentName))
            {
                Debug.LogWarning($"Component prefab not found: {componentName}");
                return null;
            }

            GameObject prefab = Resources.Load<GameObject>(shipComponents[componentName]);
            if (prefab == null)
            {
                Debug.LogError($"Failed to load component prefab: {componentName} from {shipComponents[componentName]}");
            }

            return prefab;
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
