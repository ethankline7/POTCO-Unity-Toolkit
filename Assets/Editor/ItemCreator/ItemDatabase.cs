using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using System.Linq;

namespace POTCO.Editor.ItemCreator
{
    /// <summary>
    /// ScriptableObject that acts as the central data store for item information.
    /// It loads and manages data parsed from Python files.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "POTCO/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        // Store raw parsed data dictionaries
        [NonSerialized] public Dictionary<int, ItemDataRow> AllItems = new Dictionary<int, ItemDataRow>();
        [NonSerialized] public Dictionary<string, int> ColumnHeadings = new Dictionary<string, int>();
        [NonSerialized] public Dictionary<string, int> InventoryTypeConstantsMap = new Dictionary<string, int>();
        [NonSerialized] public Dictionary<string, int> ItemSubtypeConstantsMap = new Dictionary<string, int>();
        [NonSerialized] public Dictionary<string, int> ItemRarityConstantsMap = new Dictionary<string, int>(); // Assuming rarities are defined similarly
        [NonSerialized] public Dictionary<string, int> ItemGlobalsConstantsMap = new Dictionary<string, int>();
        [NonSerialized] public Dictionary<string, object> LocalizationData = new Dictionary<string, object>();

        // Paths to the Python data files (relative to Assets/)n        [Header("Python Data File Paths (Relative to Assets/)")]
        public string itemDataPath = "Editor/POTCO_Source/inventory/ItemData.py";
        public string uberDogGlobalsPath = "Editor/POTCO_Source/uberdog/UberDogGlobals.py";
        public string itemConstantsPath = "Editor/POTCO_Source/inventory/ItemConstants.py";
        public string localizerPath = "Editor/POTCO_Source/PLocalizerEnglish.py";

        /// <summary>
        /// Loads all item data and constants from the specified Python files asynchronously.
        /// </summary>
        public async void LoadAllDataAsync(Action<string, float> onProgress, Action onPartialComplete, Action onComplete)
        {
            // Cache paths on main thread
            string fullItemDataPath = Path.Combine(Application.dataPath, itemDataPath);
            string fullUberDogGlobalsPath = Path.Combine(Application.dataPath, uberDogGlobalsPath);
            string fullItemConstantsPath = Path.Combine(Application.dataPath, itemConstantsPath);
            string fullLocalizerPath = Path.Combine(Application.dataPath, localizerPath);

            await Task.Run(() =>
            {
                try
                {
                    // --- Load ItemData.py ---
                    ReportProgress(onProgress, "Loading ItemData.py...", 0.1f);
                    Dictionary<int, ItemDataRow> loadedItems = new Dictionary<int, ItemDataRow>();
                    Dictionary<string, int> loadedHeadings = new Dictionary<string, int>();

                    if (File.Exists(fullItemDataPath))
                    {
                        string itemDataContent = File.ReadAllText(fullItemDataPath);
                        var result = PythonDataParser.ParseItemData(itemDataContent);
                        loadedItems = result.Item1;
                        loadedHeadings = result.Item2;
                    }

                    // --- Load UberDogGlobals.py ---
                    ReportProgress(onProgress, "Loading UberDogGlobals.py...", 0.3f);
                    Dictionary<string, int> loadedInventoryTypes = new Dictionary<string, int>();
                    if (File.Exists(fullUberDogGlobalsPath))
                    {
                        string content = File.ReadAllText(fullUberDogGlobalsPath);
                        loadedInventoryTypes = PythonDataParser.ParseConstants(content, "InventoryType");
                    }

                    // --- Load ItemConstants.py ---
                    ReportProgress(onProgress, "Loading ItemConstants.py...", 0.4f);
                    Dictionary<string, int> loadedSubtypes = new Dictionary<string, int>();
                    Dictionary<string, int> loadedRarities = new Dictionary<string, int>();
                    if (File.Exists(fullItemConstantsPath))
                    {
                        string content = File.ReadAllText(fullItemConstantsPath);
                        loadedSubtypes = PythonDataParser.ParseConstants(content);
                        loadedRarities = PythonDataParser.ParseConstants(content, "RARITY_");
                    }

                    // --- Build Constants Map ---
                    ReportProgress(onProgress, "Mapping Constants...", 0.5f);
                    Dictionary<string, int> loadedGlobalsMap = new Dictionary<string, int>();
                    
                    foreach (var kvp in loadedInventoryTypes)
                        loadedGlobalsMap["ItemGlobals.InventoryType." + kvp.Key] = kvp.Value;
                    
                    foreach (var kvp in loadedSubtypes)
                        loadedGlobalsMap["ItemGlobals." + kvp.Key] = kvp.Value;
                    
                    foreach (var kvp in loadedRarities)
                        loadedGlobalsMap["ItemGlobals." + kvp.Key] = kvp.Value;

                    if (loadedHeadings.TryGetValue("CONSTANT_NAME", out int constNameIdx))
                    {
                        foreach (var kvp in loadedItems)
                        {
                            ItemDataRow row = kvp.Value;
                            var rawData = row.GetRawData();
                            if (rawData != null && rawData.Count > constNameIdx)
                            {
                                string constName = rawData[constNameIdx] as string;
                                if (!string.IsNullOrEmpty(constName))
                                {
                                    loadedGlobalsMap["ItemGlobals." + constName] = kvp.Key;
                                }
                            }
                        }
                    }

                    // --- STAGE 1 COMPLETE: Update UI with Basic Data ---
                    EditorApplication.delayCall += () =>
                    {
                        AllItems = loadedItems;
                        ColumnHeadings = loadedHeadings;
                        InventoryTypeConstantsMap = loadedInventoryTypes;
                        ItemSubtypeConstantsMap = loadedSubtypes;
                        ItemRarityConstantsMap = loadedRarities;
                        ItemGlobalsConstantsMap = loadedGlobalsMap;
                        
                        // Clear old localization so we don't show stale data mixed with new items
                        LocalizationData = new Dictionary<string, object>();

                        ItemColumnMapping.SetMapping(ColumnHeadings);
                        InventoryTypeConstants.SetMapping(InventoryTypeConstantsMap);
                        ItemSubtypeConstants.SetMapping(ItemSubtypeConstantsMap);
                        ItemRarityConstants.SetMapping(ItemRarityConstantsMap);
                        
                        Debug.Log($"Basic Data Loaded: {AllItems.Count} Items. Starting Localization...");
                        onPartialComplete?.Invoke();
                    };

                    // --- Load PLocalizerEnglish.py ---
                    ReportProgress(onProgress, "Parsing PLocalizerEnglish.py (This may take a while)...", 0.6f);
                    Dictionary<string, object> loadedLocalization = new Dictionary<string, object>();
                    if (File.Exists(fullLocalizerPath))
                    {
                        string content = File.ReadAllText(fullLocalizerPath);
                        // Check if file is huge
                        if (content.Length > 5000000) // 5MB check just in case
                        {
                             Debug.LogWarning("PLocalizerEnglish.py is very large, parsing might be slow.");
                        }
                        loadedLocalization = PythonDataParser.ParseLocalizer(content, loadedGlobalsMap);
                    }

                    ReportProgress(onProgress, "Finalizing...", 0.9f);

                    // --- STAGE 2 COMPLETE: Update UI with Localization ---
                    EditorApplication.delayCall += () =>
                    {
                        LocalizationData = loadedLocalization;
                        Debug.Log($"Async Load Complete: {LocalizationData.Count} Loc Entries.");
                        onComplete?.Invoke();
                    };
                }
                catch (Exception e)
                {
                    Debug.LogError($"Async Load Failed: {e.Message}\n{e.StackTrace}");
                    EditorApplication.delayCall += () => onComplete?.Invoke();
                }
            });
        }

        private void ReportProgress(Action<string, float> onProgress, string message, float progress)
        {
            // Ensure progress is reported on main thread if it touches UI
            EditorApplication.delayCall += () => onProgress?.Invoke(message, progress);
        }

        /// <summary>
        /// Loads all item data and constants from the specified Python files.
        /// This method should be called to initialize the database.
        /// </summary>
        [ContextMenu("Load All Data From Python Files")]
        public void LoadAllData()
        {
            Debug.Log("Loading all item data from Python files...");

            // Clear previous data
            AllItems.Clear();
            ColumnHeadings.Clear();
            InventoryTypeConstantsMap.Clear();
            ItemSubtypeConstantsMap.Clear();
            ItemRarityConstantsMap.Clear();
            LocalizationData.Clear();

            // --- Load ItemData.py ---
            string fullItemDataPath = Path.Combine(Application.dataPath, itemDataPath);
            if (File.Exists(fullItemDataPath))
            {
                string itemDataContent = File.ReadAllText(fullItemDataPath);
                Tuple<Dictionary<int, ItemDataRow>, Dictionary<string, int>> parsedItemData = PythonDataParser.ParseItemData(itemDataContent);
                AllItems = parsedItemData.Item1;
                ColumnHeadings = parsedItemData.Item2;
                Debug.Log($"Loaded {AllItems.Count} items from ItemData.py.");
            }
            else
            {
                Debug.LogError($"ItemData.py not found at: {fullItemDataPath}");
            }

            // --- Load UberDogGlobals.py ---
            string fullUberDogGlobalsPath = Path.Combine(Application.dataPath, uberDogGlobalsPath);
            if (File.Exists(fullUberDogGlobalsPath))
            {
                string uberDogGlobalsContent = File.ReadAllText(fullUberDogGlobalsPath);
                InventoryTypeConstantsMap = PythonDataParser.ParseConstants(uberDogGlobalsContent, "InventoryType");
                InventoryTypeConstants.SetMapping(InventoryTypeConstantsMap);
                Debug.Log($"Loaded {InventoryTypeConstantsMap.Count} InventoryType constants from UberDogGlobals.py.");
            }
            else
            {
                Debug.LogError($"UberDogGlobals.py not found at: {fullUberDogGlobalsPath}");
            }

            // --- Load ItemConstants.py ---
            string fullItemConstantsPath = Path.Combine(Application.dataPath, itemConstantsPath);
            if (File.Exists(fullItemConstantsPath))
            {
                string itemConstantsContent = File.ReadAllText(fullItemConstantsPath);
                
                // Parse general item subtypes and other constants
                ItemSubtypeConstantsMap = PythonDataParser.ParseConstants(itemConstantsContent);
                ItemSubtypeConstants.SetMapping(ItemSubtypeConstantsMap);
                Debug.Log($"Loaded {ItemSubtypeConstantsMap.Count} Item Subtype constants from ItemConstants.py.");

                // Attempt to parse rarity constants if they are in similar format in ItemConstants.py
                ItemRarityConstantsMap = PythonDataParser.ParseConstants(itemConstantsContent, "RARITY_"); // Assuming a prefix like RARITY_
                ItemRarityConstants.SetMapping(ItemRarityConstantsMap);
                Debug.Log($"Loaded {ItemRarityConstantsMap.Count} Item Rarity constants from ItemConstants.py (prefix RARITY_).");
            }
            else
            {
                Debug.LogError($"ItemConstants.py not found at: {fullItemConstantsPath}");
            }

            // --- Build ItemGlobals Constants Map for Localizer ---
            ItemGlobalsConstantsMap.Clear();

            // 1. InventoryType constants (ItemGlobals.InventoryType.CONST)
            foreach (var kvp in InventoryTypeConstantsMap)
            {
                ItemGlobalsConstantsMap["ItemGlobals.InventoryType." + kvp.Key] = kvp.Value;
            }

            // 2. ItemConstants (ItemGlobals.CONST)
            foreach (var kvp in ItemSubtypeConstantsMap)
            {
                ItemGlobalsConstantsMap["ItemGlobals." + kvp.Key] = kvp.Value;
            }
            foreach (var kvp in ItemRarityConstantsMap)
            {
                ItemGlobalsConstantsMap["ItemGlobals." + kvp.Key] = kvp.Value;
            }

            // 3. Item IDs from ItemData (ItemGlobals.CONST_NAME)
            if (ColumnHeadings.TryGetValue("CONSTANT_NAME", out int constNameIdx))
            {
                foreach (var kvp in AllItems)
                {
                    int id = kvp.Key;
                    ItemDataRow row = kvp.Value;
                    var rawData = row.GetRawData();
                    if (rawData != null && rawData.Count > constNameIdx)
                    {
                        string constName = rawData[constNameIdx] as string;
                        if (!string.IsNullOrEmpty(constName))
                        {
                            ItemGlobalsConstantsMap["ItemGlobals." + constName] = id;
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning("Could not find 'CONSTANT_NAME' column in ItemData.py. PLocalizer item constants will not be resolved.");
            }
            Debug.Log($"Mapped {ItemGlobalsConstantsMap.Count} constants for PLocalizer resolution.");

            // DEBUG: Check for specific constant
            if (ItemGlobalsConstantsMap.TryGetValue("ItemGlobals.TAILORED_CAPRIS", out int tailoredId))
            {
                Debug.Log($"DEBUG: Found ItemGlobals.TAILORED_CAPRIS -> {tailoredId}");
            }
            else
            {
                Debug.LogWarning("DEBUG: ItemGlobals.TAILORED_CAPRIS NOT FOUND in Constants Map!");
            }

            // --- Load PLocalizerEnglish.py ---
            string fullLocalizerPath = Path.Combine(Application.dataPath, localizerPath);
            if (File.Exists(fullLocalizerPath))
            {
                string localizerContent = File.ReadAllText(fullLocalizerPath);
                LocalizationData = PythonDataParser.ParseLocalizer(localizerContent, ItemGlobalsConstantsMap);
                Debug.Log($"Loaded {LocalizationData.Count} localization entries from PLocalizerEnglish.py.");

                // DEBUG: Check Localization Results
                if (LocalizationData.TryGetValue("ItemNames", out object itemNamesObj) && itemNamesObj is Dictionary<object, object> itemNames)
                {
                    Debug.Log($"DEBUG: ItemNames found with {itemNames.Count} entries.");
                    if (tailoredId != 0)
                    {
                        if (itemNames.TryGetValue(tailoredId, out object nameVal))
                            Debug.Log($"DEBUG: ItemNames[{tailoredId}] = '{nameVal}'");
                        else
                            Debug.LogWarning($"DEBUG: ItemNames[{tailoredId}] NOT FOUND.");
                    }
                }
                else
                {
                    Debug.LogWarning("DEBUG: ItemNames dictionary NOT FOUND in LocalizationData.");
                }

                if (LocalizationData.TryGetValue("ClothingFlavorText", out object clothingFlavorObj) && clothingFlavorObj is Dictionary<object, object> clothingFlavor)
                {
                    Debug.Log($"DEBUG: ClothingFlavorText found with {clothingFlavor.Count} entries.");
                    if (tailoredId != 0)
                    {
                        if (clothingFlavor.TryGetValue(tailoredId, out object flavorVal))
                            Debug.Log($"DEBUG: ClothingFlavorText[{tailoredId}] = '{flavorVal}'");
                        else
                            Debug.LogWarning($"DEBUG: ClothingFlavorText[{tailoredId}] NOT FOUND.");
                    }
                }
            }
            else
            {
                Debug.LogError($"PLocalizerEnglish.py not found at: {fullLocalizerPath}");
            }

            Debug.Log("Item data loading complete.");
        }

        // Add methods for saving data back to Python files later
        public void SaveItemData()
        {
            Debug.Log("Saving item data to Python file...");
            string fullItemDataPath = Path.Combine(Application.dataPath, itemDataPath);

            if (AllItems == null || ColumnHeadings == null || AllItems.Count == 0 || ColumnHeadings.Count == 0)
            {
                Debug.LogWarning("No item data or column headings to save. Load data first.");
                return;
            }

            try
            {
                // Prepare raw data for writing
                Dictionary<int, ItemDataRow> itemsToWrite = new Dictionary<int, ItemDataRow>();
                foreach (var entry in AllItems)
                {
                    itemsToWrite.Add(entry.Key, entry.Value);
                }

                string pythonContent = PythonDataParser.WritePythonData(itemsToWrite, ColumnHeadings);
                File.WriteAllText(fullItemDataPath, pythonContent);
                AssetDatabase.Refresh();
                Debug.Log($"Successfully saved item data to {fullItemDataPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save item data to {fullItemDataPath}. Error: {e.Message}");
            }
        }


        public ItemDataRow GetItem(int itemId)
        {
            AllItems.TryGetValue(itemId, out ItemDataRow item);
            return item;
        }
    }
}
