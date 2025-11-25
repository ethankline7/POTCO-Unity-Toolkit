using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;
using POTCO.Editor.ItemCreator.Utilities; // For StringExtensions

namespace POTCO.Editor.ItemCreator
{
    public class ItemEditorWindow : EditorWindow
    {
        private ItemDatabase _itemDatabase;
        private Vector2 _scrollPosition;
        private Vector2 _detailScrollPosition;
        private int _selectedItemId = -1;
        private ItemDataRow _selectedItem;

        // Editor State
        private string _searchString = "";
        private int _selectedClassFilter = -1; // -1 for "All"
        private int _selectedTypeFilter = -1; // -1 for "All"

        [MenuItem("POTCO/Item Editor")]
        public static void ShowWindow()
        {
            GetWindow<ItemEditorWindow>("Item Editor");
        }

        private void OnEnable()
        {
            // Load the ItemDatabase ScriptableObject
            _itemDatabase = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/Editor/ItemCreator/ItemDatabase.asset");
            if (_itemDatabase == null)
            {
                Debug.LogWarning("ItemDatabase ScriptableObject not found. Creating a new one.");
                _itemDatabase = ScriptableObject.CreateInstance<ItemDatabase>();
                AssetDatabase.CreateAsset(_itemDatabase, "Assets/Editor/ItemCreator/ItemDatabase.asset");
                AssetDatabase.SaveAssets();
                _itemDatabase.LoadAllData(); // Attempt to load data immediately after creation
            }
            else if (_itemDatabase.AllItems.Count == 0 && _itemDatabase.ColumnHeadings.Count == 0)
            {
                _itemDatabase.LoadAllData(); // Load data if the database is empty (e.g., first time opening after project reload)
            }
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space();

            if (_itemDatabase == null)
            {
                EditorGUILayout.HelpBox("ItemDatabase ScriptableObject is missing!", MessageType.Error);
                if (GUILayout.Button("Create ItemDatabase Asset"))
                {
                    OnEnable(); // Attempt to recreate
                }
                return;
            }

            if (_itemDatabase.AllItems == null || _itemDatabase.AllItems.Count == 0)
            {
                EditorGUILayout.HelpBox("No item data loaded. Click 'Load All Data' to parse Python files.", MessageType.Warning);
            }

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

            try 
            {
                DrawItemListPanel();
                DrawItemDetailPanel();
            }
            catch (System.Exception e)
            {
                EditorGUILayout.HelpBox($"Error drawing editor: {e.Message}", MessageType.Error);
                // Debug.LogException(e); // Optional: log to console
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Load All Data", EditorStyles.toolbarButton))
            {
                _itemDatabase.LoadAllData();
                _selectedItemId = -1;
                _selectedItem = null;
            }
            if (GUILayout.Button("Save ItemData.py", EditorStyles.toolbarButton))
            {
                _itemDatabase.SaveItemData();
            }
            if (GUILayout.Button("Create New Item", EditorStyles.toolbarButton))
            {
                CreateNewItem();
            }
            if (GUILayout.Button("Duplicate Selected Item", EditorStyles.toolbarButton))
            {
                DuplicateSelectedItem();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawItemListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.3f), GUILayout.ExpandHeight(true));

            // Search and Filter
            EditorGUILayout.LabelField("Filter Items", EditorStyles.boldLabel);
            _searchString = EditorGUILayout.TextField("Search:", _searchString);

            List<string> classFilterOptions = new List<string> { "All" };
            classFilterOptions.AddRange(_itemDatabase.InventoryTypeConstantsMap.Keys.OrderBy(k => k));
            
            // Validate and clamp selected filter index
            if (_selectedClassFilter >= classFilterOptions.Count)
            {
                _selectedClassFilter = 0;
            }
            else if (_selectedClassFilter < 0)
            {
                _selectedClassFilter = 0;
            }

            _selectedClassFilter = EditorGUILayout.Popup("Item Class:", _selectedClassFilter, classFilterOptions.ToArray());

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            foreach (var entry in _itemDatabase.AllItems.OrderBy(e => e.Key))
            {
                ItemDataRow item = entry.Value;

                // Apply filters
                bool matchesSearch = string.IsNullOrEmpty(_searchString) ||
                                     item.GetItemName().ToLower().Contains(_searchString.ToLower()) ||
                                     item.GetConstantName().ToLower().Contains(_searchString.ToLower());

                // Re-validate index before access, though clamping above should handle it
                bool matchesClassFilter = false;
                if (_selectedClassFilter >= 0 && _selectedClassFilter < classFilterOptions.Count)
                {
                     matchesClassFilter = _selectedClassFilter == 0 || // "All" selected
                                          (_itemDatabase.InventoryTypeConstantsMap.TryGetValue(classFilterOptions[_selectedClassFilter], out int classFilterValue) && item.GetItemClass() == classFilterValue);
                }

                if (matchesSearch && matchesClassFilter)
                {
                    bool isSelected = (_selectedItemId == item.ItemId);
                    GUIStyle buttonStyle = isSelected ? (GUIStyle)"WhiteLabel" : GUI.skin.label; // Highlight selected

                    if (GUILayout.Button($"ID: {item.ItemId} - {item.GetItemName()}", buttonStyle))
                    {
                        _selectedItemId = item.ItemId;
                        _selectedItem = item;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawItemDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            _detailScrollPosition = EditorGUILayout.BeginScrollView(_detailScrollPosition);

            if (_selectedItem == null)
            {
                EditorGUILayout.LabelField("Select an item from the list to view/edit details.", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField($"Editing Item: {_selectedItem.GetItemName()} (ID: {_selectedItem.ItemId})", EditorStyles.boldLabel);
                
                if (GUILayout.Button("Copy to Clipboard"))
                {
                    string data = PythonDataParser.WriteSingleItemData(_selectedItem);
                    GUIUtility.systemCopyBuffer = data;
                    Debug.Log("Item data copied to clipboard: " + data);
                }

                EditorGUILayout.Space();

                DrawIdentitySection();
                DrawAvailabilityEconomySection();
                DrawGatingProgressionSection();
                DrawPresentationSection();
                DrawModelAppearanceSection();

                // Class-specific panels
                int itemClass = _selectedItem.GetItemClass();
                if (itemClass == InventoryTypeConstants.GetValue("ItemTypeWeapon"))
                {
                    DrawWeaponSpecificSection();
                }
                else if (itemClass == InventoryTypeConstants.GetValue("ItemTypeConsumable"))
                {
                    DrawConsumableSpecificSection();
                }

                DrawAttributesBoostsSection();
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawIdentitySection()
        {
            EditorGUILayout.LabelField("Header / Identity", EditorStyles.boldLabel);
            _selectedItem.SetItemName(EditorGUILayout.TextField("Item Name:", _selectedItem.GetItemName()));
            EditorGUILayout.LabelField("Constant Name:", _selectedItem.GetConstantName()); // Readonly or generated
            EditorGUILayout.IntField("Item ID:", _selectedItem.ItemId); // Readonly
            _selectedItem.SetVersion(EditorGUILayout.IntField("Version:", _selectedItem.GetVersion()));

            // --- Item Class Dropdown ---
            if (_itemDatabase.InventoryTypeConstantsMap == null || _itemDatabase.InventoryTypeConstantsMap.Count == 0)
            {
                EditorGUILayout.HelpBox("Inventory Constants missing. Check UberDogGlobals.py parsing.", MessageType.Warning);
                return;
            }

            List<string> classNames = _itemDatabase.InventoryTypeConstantsMap.Keys.OrderBy(k => k).ToList();
            List<string> classDisplayNames = classNames.Select(name => name.Replace("ItemType", "")).ToList();

            int currentItemClassValue = _selectedItem.GetItemClass();
            int selectedClassIndex = -1;
            string currentItemClassName = _itemDatabase.InventoryTypeConstantsMap.FirstOrDefault(x => x.Value == currentItemClassValue).Key;
            if (currentItemClassName != null)
            {
                selectedClassIndex = classNames.IndexOf(currentItemClassName);
            }
            
            if (selectedClassIndex == -1 && classNames.Any())
            {
                 // Default logic or just leave as -1
            }

            int newSelectedClassIndex = EditorGUILayout.Popup("Item Class:", selectedClassIndex, classDisplayNames.ToArray());
            if (newSelectedClassIndex != selectedClassIndex && newSelectedClassIndex >= 0)
            {
                string newClassName = classNames[newSelectedClassIndex];
                int newClassValue = _itemDatabase.InventoryTypeConstantsMap[newClassName];
                _selectedItem.SetItemClass(newClassValue);
                _selectedItem.SetItemType(0); 
            }

            // --- Item Type / Subtype Dropdown (Dynamic based on Item Class) ---
            EditorGUILayout.LabelField("Item Type / Subtype", EditorStyles.boldLabel);
            List<string> subtypeNames = new List<string>();
            List<string> subtypeDisplayNames = new List<string>();

            // Populate subtype options based on the selected Item Class
            switch (currentItemClassValue)
            {
                case int val when val == InventoryTypeConstants.GetValue("ItemTypeWeapon"):
                    AddSubtypesToDropdown(subtypeNames, subtypeDisplayNames, 
                        new string[] { "CUTLASS", "SABRE", "RAPIER", "BAYONET", "DAGGER_SUBTYPE", "GRENADE_SUBTYPE", 
                                       "FLINTLOCK_PISTOL", "BLUNDERBUSS", "MUSKET", "STAFF", "DOLL", "BROADSWORD", "SCIMITAR",
                                       "CURSED_CUTLASS", "PISTOL", "REPEATER", "MUSKET", "BLUNDERBUSS", "BAYONET", "BASIC_DOLL",
                                       "BANE", "MOJO", "SPIRIT", "DIRK", "KRIS", "BASIC_STAFF", "DARK", "NATURE", "WARDING",
                                       "RAM", "BOARDING", "CARRONADE", "DUAL_CUTLASS", "AXE", "FENCING" });
                    break;
                case int val when val == InventoryTypeConstants.GetValue("ItemTypeClothing"):
                    AddSubtypesToDropdown(subtypeNames, subtypeDisplayNames,
                        new string[] { "SHIRT", "VEST", "COAT", "PANT", "BELT", "BOOTS", "HAT", "GLOVES" });
                    break;
                case int val when val == InventoryTypeConstants.GetValue("ItemTypeTattoo"):
                    AddSubtypesToDropdown(subtypeNames, subtypeDisplayNames,
                        new string[] { "TATTOO_HEAD", "TATTOO_ARM_LEFT", "TATTOO_ARM_RIGHT", "TATTOO_TORSO", "TATTOO_LEG_LEFT", "TATTOO_LEG_RIGHT" });
                    break;
                case int val when val == InventoryTypeConstants.GetValue("ItemTypeJewelry"):
                    AddSubtypesToDropdown(subtypeNames, subtypeDisplayNames,
                        new string[] { "RING", "EARRING", "NECKLACE", "BRACELET" });
                    break;
                case int val when val == InventoryTypeConstants.GetValue("ItemTypeCharm"):
                    AddSubtypesToDropdown(subtypeNames, subtypeDisplayNames,
                        new string[] { "TOTEM_FIRE", "TOTEM_ICE", "TOTEM_THUNDER", "TOTEM_VOODOO" });
                    break;
                case int val when val == InventoryTypeConstants.GetValue("ItemTypeConsumable"):
                    AddSubtypesToDropdown(subtypeNames, subtypeDisplayNames,
                        new string[] { "POTION", "GROG", "TONIC", "GRENADES", "AMMO", "ELIXIR" });
                    break;
                case int val when val == InventoryTypeConstants.GetValue("ItemTypeMoney"):
                    AddSubtypesToDropdown(subtypeNames, subtypeDisplayNames,
                        new string[] { "TREASURE", "QUEST_ITEM", "CURRENCY" });
                    break;
                default:
                    break;
            }

            int currentItemTypeValue = _selectedItem.GetItemType();
            int selectedTypeIndex = -1;
            string currentItemTypeName = _itemDatabase.ItemSubtypeConstantsMap.FirstOrDefault(x => x.Value == currentItemTypeValue).Key;
            if (currentItemTypeName != null)
            {
                selectedTypeIndex = subtypeNames.IndexOf(currentItemTypeName);
            }
            
            if (subtypeDisplayNames.Any())
            {
                int newSelectedTypeIndex = EditorGUILayout.Popup("Subtype:", selectedTypeIndex, subtypeDisplayNames.ToArray());
                if (newSelectedTypeIndex != selectedTypeIndex && newSelectedTypeIndex >= 0)
                {
                    string newSubtypeName = subtypeNames[newSelectedTypeIndex];
                    int newSubtypeValue = _itemDatabase.ItemSubtypeConstantsMap[newSubtypeName];
                    _selectedItem.SetItemType(newSubtypeValue);
                }
            } else {
                 _selectedItem.SetItemType(EditorGUILayout.IntField("Subtype (Raw):", _selectedItem.GetItemType()));
            }

            EditorGUILayout.Space();
        }

        private void DrawAvailabilityEconomySection()
        {
            EditorGUILayout.LabelField("Availability & Economy", EditorStyles.boldLabel);
            _selectedItem.SetGoldCost(EditorGUILayout.IntField("Gold Cost:", _selectedItem.GetGoldCost()));
            _selectedItem.SetFromLoot(EditorGUILayout.Toggle("From Loot:", _selectedItem.IsFromLoot()));
            _selectedItem.SetFromShop(EditorGUILayout.Toggle("From Shop:", _selectedItem.IsFromShop()));
            _selectedItem.SetFromQuest(EditorGUILayout.Toggle("From Quest:", _selectedItem.IsFromQuest()));
            _selectedItem.SetFromPromo(EditorGUILayout.Toggle("From Promo:", _selectedItem.IsFromPromo()));
            _selectedItem.SetFromPVP(EditorGUILayout.Toggle("From PVP:", _selectedItem.IsFromPVP()));
            _selectedItem.SetFromNPC(EditorGUILayout.Toggle("From NPC:", _selectedItem.IsFromNPC()));
            EditorGUILayout.Space();
        }

        private void DrawGatingProgressionSection()
        {
            EditorGUILayout.LabelField("Gating / Progression", EditorStyles.boldLabel);
            
            if (ItemColumnMapping.Mapping.ContainsKey("NOTORIETY_REQ"))
            {
                _selectedItem.SetNotorietyReq(EditorGUILayout.IntField("Notoriety Req:", _selectedItem.GetNotorietyReq()));
            }
            if (ItemColumnMapping.Mapping.ContainsKey("ITEM_NOTORIETY_REQ"))
            {
                _selectedItem.SetItemNotorietyReq(EditorGUILayout.IntField("Item Notoriety Req:", _selectedItem.GetItemNotorietyReq()));
            }

            _selectedItem.SetVelvetRope(EditorGUILayout.IntField("Velvet Rope:", _selectedItem.GetVelvetRope()));
            // Holiday only if class supports it - needs HAS_HOLIDAY_DATA mapping from ItemGlobals.py (parse later)
            _selectedItem.SetHoliday(EditorGUILayout.IntField("Holiday:", _selectedItem.GetHoliday())); 
            EditorGUILayout.Space();
        }

        private void DrawPresentationSection()
        {
            EditorGUILayout.LabelField("Presentation", EditorStyles.boldLabel);
            _selectedItem.SetItemIcon(EditorGUILayout.TextField("Item Icon:", _selectedItem.GetItemIcon()));
            
            EditorGUILayout.LabelField("Flavor Text:");
            _selectedItem.SetFlavorText(EditorGUILayout.TextArea(_selectedItem.GetFlavorText(), GUILayout.Height(50)));
            
            EditorGUILayout.Space();
        }

        private void DrawModelAppearanceSection()
        {
            EditorGUILayout.LabelField("Model & Appearance", EditorStyles.boldLabel);
            _selectedItem.SetItemModel(EditorGUILayout.TextField("Item Model:", _selectedItem.GetItemModel()));
            _selectedItem.SetMaleModelId(EditorGUILayout.TextField("Male Model ID:", _selectedItem.GetMaleModelId()));
            _selectedItem.SetFemaleModelId(EditorGUILayout.TextField("Female Model ID:", _selectedItem.GetFemaleModelId()));
            _selectedItem.SetMaleTextureId(EditorGUILayout.IntField("Male Texture ID:", _selectedItem.GetMaleTextureId()));
            _selectedItem.SetFemaleTextureId(EditorGUILayout.IntField("Female Texture ID:", _selectedItem.GetFemaleTextureId()));
            _selectedItem.SetPrimaryColor(EditorGUILayout.IntField("Primary Color:", _selectedItem.GetPrimaryColor()));
            _selectedItem.SetSecondaryColor(EditorGUILayout.IntField("Secondary Color:", _selectedItem.GetSecondaryColor()));
            _selectedItem.SetMaleOrientation(EditorGUILayout.IntField("Male Orientation:", _selectedItem.GetMaleOrientation()));
            _selectedItem.SetMaleOrientation2(EditorGUILayout.IntField("Male Orientation 2:", _selectedItem.GetMaleOrientation2()));
            _selectedItem.SetFemaleOrientation(EditorGUILayout.IntField("Female Orientation:", _selectedItem.GetFemaleOrientation()));
            _selectedItem.SetFemaleOrientation2(EditorGUILayout.IntField("Female Orientation 2:", _selectedItem.GetFemaleOrientation2()));
            _selectedItem.SetCanDyeItem(EditorGUILayout.Toggle("Can Dye Item:", _selectedItem.CanDyeItem()));
            
            // TODO: Add 3D Model Preview
            EditorGUILayout.LabelField("3D Model Preview: (Not Implemented)", EditorStyles.miniLabel);
            EditorGUILayout.Space();
        }

        private void DrawWeaponSpecificSection()
        {
            EditorGUILayout.LabelField("Weapon Specific", EditorStyles.boldLabel);
            _selectedItem.SetPower(EditorGUILayout.IntField("Power:", _selectedItem.GetPower()));
            _selectedItem.SetRating(EditorGUILayout.IntField("Rating:", _selectedItem.GetRating()));
            _selectedItem.SetWeaponReq(EditorGUILayout.IntField("Weapon Req:", _selectedItem.GetWeaponReq()));
            _selectedItem.SetUseSkill(EditorGUILayout.IntField("Use Skill:", _selectedItem.GetUseSkill()));
            _selectedItem.SetSpecialAttackRank(EditorGUILayout.IntField("Special Attack Rank:", _selectedItem.GetSpecialAttackRank()));
            _selectedItem.SetSpecialAttack(EditorGUILayout.IntField("Special Attack:", _selectedItem.GetSpecialAttack()));
            _selectedItem.SetBarrels(EditorGUILayout.IntField("Barrels (Guns/Thrown):", _selectedItem.GetBarrels()));
            _selectedItem.SetVfxType1(EditorGUILayout.IntField("VFX Type 1:", _selectedItem.GetVfxType1()));
            _selectedItem.SetVfxType2(EditorGUILayout.IntField("VFX Type 2:", _selectedItem.GetVfxType2()));
            _selectedItem.SetVfxOffset(EditorGUILayout.IntField("VFX Offset:", _selectedItem.GetVfxOffset()));
            EditorGUILayout.Space();
        }

        private void DrawConsumableSpecificSection()
        {
            EditorGUILayout.LabelField("Consumable Specific", EditorStyles.boldLabel);
            _selectedItem.SetStackLimit(EditorGUILayout.IntField("Stack Limit:", _selectedItem.GetStackLimit()));
            EditorGUILayout.Space();
        }

        private void DrawAttributesBoostsSection()
        {
            EditorGUILayout.LabelField("Attributes / Boosts (Not Implemented)", EditorStyles.boldLabel);
            // TODO: Implement dynamic attribute/skill boost rows
            EditorGUILayout.Space();
        }

        private void CreateNewItem()
        {
            if (_itemDatabase == null || _itemDatabase.AllItems == null || _itemDatabase.ColumnHeadings == null)
            {
                Debug.LogError("ItemDatabase not initialized. Cannot create new item.");
                return;
            }

            int newId = _itemDatabase.AllItems.Any() ? _itemDatabase.AllItems.Keys.Max() + 1 : 1;

            // Initialize raw data list with a default size based on column headings
            int columnCount = ItemColumnMapping.Mapping.Count;
            List<object> newRawData = new List<object>(new object[columnCount]);

            // Set some default values
            if (ItemColumnMapping.Mapping.TryGetValue("ITEM_CLASS", out int classIndex))
            {
                newRawData[classIndex] = InventoryTypeConstants.GetValue("ItemTypeWeapon");
            }
            if (ItemColumnMapping.Mapping.TryGetValue("ITEM_NAME", out int nameIndex))
            {
                newRawData[nameIndex] = "u'New Item'";
            }
            if (ItemColumnMapping.Mapping.TryGetValue("ITEM_ID", out int idIndex))
            {
                newRawData[idIndex] = newId;
            }
            string constantName = $"NEW_ITEM_{newId}";
            if (ItemColumnMapping.Mapping.TryGetValue("CONSTANT_NAME", out int constantNameIndex))
            {
                newRawData[constantNameIndex] = $"u'{constantName}'";
            }
            // Add other sensible defaults here, e.g., Version, Rarity, GoldCost
            if (ItemColumnMapping.Mapping.TryGetValue("VERSION", out int versionIndex)) newRawData[versionIndex] = 1;
            if (ItemColumnMapping.Mapping.TryGetValue("RARITY", out int rarityIndex)) newRawData[rarityIndex] = 1; // COMMON
            if (ItemColumnMapping.Mapping.TryGetValue("GOLD_COST", out int goldCostIndex)) newRawData[goldCostIndex] = 0;


            ItemDataRow newItem = new ItemDataRow(newId, newRawData);
            _itemDatabase.AllItems.Add(newId, newItem);
            _selectedItemId = newId;
            _selectedItem = newItem;

            Debug.Log($"Created new item with ID: {newId}");
            EditorUtility.SetDirty(_itemDatabase); // Mark as dirty to save changes
        }

        private void DuplicateSelectedItem()
        {
            if (_selectedItem == null)
            {
                Debug.LogWarning("No item selected to duplicate.");
                return;
            }

            if (_itemDatabase == null || _itemDatabase.AllItems == null || _itemDatabase.ColumnHeadings == null)
            {
                Debug.LogError("ItemDatabase not initialized. Cannot duplicate item.");
                return;
            }

            int newId = _itemDatabase.AllItems.Any() ? _itemDatabase.AllItems.Keys.Max() + 1 : 1;

            // Deep copy the raw data list
            List<object> duplicatedRawData = _selectedItem.GetRawData().ConvertAll(item =>
            {
                if (item is string s) return s;
                if (item is int i) return i;
                if (item is float f) return f;
                // Add other types if necessary
                return item;
            });


            // Update ITEM_ID for the duplicated item
            if (ItemColumnMapping.Mapping.TryGetValue("ITEM_ID", out int idIndex))
            {
                duplicatedRawData[idIndex] = newId;
            }

            // Generate a new unique CONSTANT_NAME
            string originalConstantName = _selectedItem.GetConstantName();
            string newConstantName = $"DUPLICATE_OF_{originalConstantName.ToUpper().Replace("U'", "").Replace("'", "")}_{newId}";
            if (ItemColumnMapping.Mapping.TryGetValue("CONSTANT_NAME", out int constantNameIndex))
            {
                duplicatedRawData[constantNameIndex] = $"u'{newConstantName}'";
            }
            if (ItemColumnMapping.Mapping.TryGetValue("ITEM_NAME", out int nameIndex))
            {
                duplicatedRawData[nameIndex] = $"u'Duplicate of {_selectedItem.GetItemName().Replace("u'", "").Replace("'", "")}'";
            }


            ItemDataRow duplicatedItem = new ItemDataRow(newId, duplicatedRawData);
            _itemDatabase.AllItems.Add(newId, duplicatedItem);
            _selectedItemId = newId;
            _selectedItem = duplicatedItem;

            Debug.Log($"Duplicated item '{_selectedItem.GetItemName()}' with new ID: {newId}");
            EditorUtility.SetDirty(_itemDatabase); // Mark as dirty to save changes
        }

        private void AddSubtypesToDropdown(List<string> subtypeNames, List<string> subtypeDisplayNames, string[] constants)
        {
            foreach (string constantName in constants)
            {
                if (_itemDatabase.ItemSubtypeConstantsMap.ContainsKey(constantName))
                {
                    subtypeNames.Add(constantName);
                    // Optionally make display names more user-friendly
                    subtypeDisplayNames.Add(constantName.Replace("_", " ").ToLowerInvariant().ToTitleCase()); 
                }
                else
                {
                    Debug.LogWarning($"Subtype constant '{constantName}' not found in ItemSubtypeConstantsMap.");
                }
            }
        }
    }
}