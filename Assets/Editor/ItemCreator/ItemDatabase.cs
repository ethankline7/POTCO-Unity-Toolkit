using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

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

        // Paths to the Python data files (relative to Assets folder)
        [Header("Python Data File Paths (Relative to Assets/)")]
        public string itemDataPath = "Editor/POTCO_Source/inventory/ItemData.py";
        public string uberDogGlobalsPath = "Editor/POTCO_Source/uberdog/UberDogGlobals.py";
        public string itemConstantsPath = "Editor/POTCO_Source/inventory/ItemConstants.py";

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
