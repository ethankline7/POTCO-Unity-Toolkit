using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;
using POTCO.Editor.ItemCreator.Utilities; 

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
        private int _selectedClassFilter = -1; 

        // Preview State
        private Texture2D _iconTexture;
        private GameObject _modelAsset;
        private UnityEditor.Editor _modelPreviewEditor;
        private GUIStyle _cardStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _statStyle;

        [MenuItem("POTCO/Item Editor")]
        public static void ShowWindow()
        {
            GetWindow<ItemEditorWindow>("Item Editor");
        }

        private void OnEnable()
        {
            _itemDatabase = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/Editor/ItemCreator/ItemDatabase.asset");
            if (_itemDatabase == null)
            {
                _itemDatabase = ScriptableObject.CreateInstance<ItemDatabase>();
                AssetDatabase.CreateAsset(_itemDatabase, "Assets/Editor/ItemCreator/ItemDatabase.asset");
                AssetDatabase.SaveAssets();
                _itemDatabase.LoadAllData(); 
            }
            else if (_itemDatabase.AllItems.Count == 0 && _itemDatabase.ColumnHeadings.Count == 0)
            {
                _itemDatabase.LoadAllData(); 
            }
        }

        private void OnDisable()
        {
            if (_modelPreviewEditor != null) DestroyImmediate(_modelPreviewEditor);
        }

        private void InitializeStyles()
        {
            if (_cardStyle == null)
            {
                _cardStyle = new GUIStyle(GUI.skin.box);
                _cardStyle.normal.background = MakeTex(2, 2, new Color(0.1f, 0.1f, 0.15f, 0.9f)); // Dark blue-ish background
                _cardStyle.padding = new RectOffset(10, 10, 10, 10);
            }
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label);
                _titleStyle.fontSize = 18;
                _titleStyle.fontStyle = FontStyle.Bold;
                _titleStyle.alignment = TextAnchor.MiddleCenter;
                _titleStyle.wordWrap = true;
            }
            if (_subtitleStyle == null)
            {
                _subtitleStyle = new GUIStyle(GUI.skin.label);
                _subtitleStyle.fontSize = 12;
                _subtitleStyle.fontStyle = FontStyle.Italic;
                _subtitleStyle.alignment = TextAnchor.MiddleCenter;
                _subtitleStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            }
            if (_statStyle == null)
            {
                _statStyle = new GUIStyle(GUI.skin.label);
                _statStyle.fontSize = 12;
                _statStyle.alignment = TextAnchor.MiddleCenter;
                _statStyle.normal.textColor = Color.white;
            }
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private void OnGUI()
        {
            InitializeStyles();
            DrawToolbar();
            EditorGUILayout.Space();

            if (_itemDatabase == null || _itemDatabase.AllItems == null || _itemDatabase.AllItems.Count == 0)
            {
                if (GUILayout.Button("Load All Data")) _itemDatabase.LoadAllData();
                return;
            }

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

            try 
            {
                DrawItemListPanel();
                DrawItemDetailPanel();
            }
            catch (System.Exception e)
            {
                EditorGUILayout.HelpBox($"Error drawing editor: {e.Message}\n{e.StackTrace}", MessageType.Error);
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

            EditorGUILayout.LabelField("Filter Items", EditorStyles.boldLabel);
            _searchString = EditorGUILayout.TextField("Search:", _searchString);

            List<string> classFilterOptions = new List<string> { "All" };
            classFilterOptions.AddRange(_itemDatabase.InventoryTypeConstantsMap.Keys.OrderBy(k => k));
            
            if (_selectedClassFilter >= classFilterOptions.Count) _selectedClassFilter = 0;
            else if (_selectedClassFilter < 0) _selectedClassFilter = 0;

            _selectedClassFilter = EditorGUILayout.Popup("Item Class:", _selectedClassFilter, classFilterOptions.ToArray());

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            foreach (var entry in _itemDatabase.AllItems.OrderBy(e => e.Key))
            {
                ItemDataRow item = entry.Value;

                bool matchesSearch = string.IsNullOrEmpty(_searchString) ||
                                     item.GetItemName().ToLower().Contains(_searchString.ToLower()) ||
                                     item.GetConstantName().ToLower().Contains(_searchString.ToLower());

                bool matchesClassFilter = false;
                if (_selectedClassFilter >= 0 && _selectedClassFilter < classFilterOptions.Count)
                {
                     matchesClassFilter = _selectedClassFilter == 0 || 
                                          (_itemDatabase.InventoryTypeConstantsMap.TryGetValue(classFilterOptions[_selectedClassFilter], out int classFilterValue) && item.GetItemClass() == classFilterValue);
                }

                if (matchesSearch && matchesClassFilter)
                {
                    bool isSelected = (_selectedItemId == item.ItemId);
                    GUIStyle buttonStyle = isSelected ? (GUIStyle)"WhiteLabel" : GUI.skin.label;

                    if (GUILayout.Button($"ID: {item.ItemId} - {item.GetItemName()}", buttonStyle))
                    {
                        UpdateSelection(item);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void UpdateSelection(ItemDataRow item)
        {
            _selectedItemId = item.ItemId;
            _selectedItem = item;
            
            // Load Icon
            _iconTexture = null;
            string iconName = item.GetItemIcon();
            if (!string.IsNullOrEmpty(iconName))
            {
                // Strip 'u' prefix if parser missed it (safety)
                if (iconName.StartsWith("u'")) iconName = iconName.Substring(2, iconName.Length - 3);
                
                string[] guids = AssetDatabase.FindAssets(iconName + " t:Texture2D");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }

            // Load Model
            if (_modelPreviewEditor != null) DestroyImmediate(_modelPreviewEditor);
            _modelAsset = null;
            string modelName = item.GetItemModel();
            if (!string.IsNullOrEmpty(modelName))
            {
                if (modelName.StartsWith("u'")) modelName = modelName.Substring(2, modelName.Length - 3);

                // Search for the model. It might be an .egg, .fbx, or .prefab
                string[] guids = AssetDatabase.FindAssets(modelName + " t:GameObject");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (_modelAsset != null)
                    {
                        _modelPreviewEditor = UnityEditor.Editor.CreateEditor(_modelAsset);
                    }
                }
            }
        }

        private void DrawPOTCOCard()
        {
            EditorGUILayout.BeginVertical(_cardStyle, GUILayout.ExpandWidth(true));

            // 1. Title (Name) - Colored by Rarity
            Color titleColor = Color.white;
            int rarity = _selectedItem.GetRarity();
            string rarityName = "Unknown";
            
            // Map rarity to colors/names (approximate POTCO values)
            // CRUDE=1, COMMON=2, RARE=3, FAMED=4, LEGENDARY=5
            switch(rarity)
            {
                case 1: 
                    titleColor = new Color(0.52f, 0.31f, 0.09f); 
                    rarityName = "Crude";
                    break;
                case 2: 
                    titleColor = new Color(0.8f, 0.8f, 0.0f); // Yellowish
                    rarityName = "Common";
                    break;
                case 3: 
                    titleColor = new Color(0.0f, 0.6f, 0.0f); // Green
                    rarityName = "Rare";
                    break;
                case 4: 
                    titleColor = new Color(0.24f, 0.36f, 0.6f); // Blue
                    rarityName = "Famed";
                    break;
                case 5: 
                    titleColor = new Color(0.6f, 0.0f, 0.0f); // Red
                    rarityName = "Legendary";
                    break;
                default:
                    rarityName = "Rarity " + rarity;
                    break;
            }

            GUIStyle coloredTitle = new GUIStyle(_titleStyle);
            coloredTitle.normal.textColor = titleColor;
            GUILayout.Label(_selectedItem.GetItemName(), coloredTitle);

            // 2. Subtitle (Rarity + Subtype)
            // Need to reverse lookup subtype name from constant value
            string subtypeName = "";
            int subtypeVal = _selectedItem.GetItemType(); 
            // Note: For weapons, subtype is usually distinct.
            // Let's just display the raw value or try to find it in the map
            var key = _itemDatabase.ItemSubtypeConstantsMap.FirstOrDefault(x => x.Value == subtypeVal).Key;
            subtypeName = key != null ? key.Replace("_", " ").ToTitleCase() : "Type " + subtypeVal;

            GUILayout.Label($"{rarityName} {subtypeName}", _subtitleStyle);

            EditorGUILayout.Space();

            // 3. 3D Model Preview (Center Stage)
            Rect previewRect = GUILayoutUtility.GetRect(200, 200);
            if (_modelPreviewEditor != null)
            {
                _modelPreviewEditor.OnInteractivePreviewGUI(previewRect, GUIStyle.none);
            }
            else if (_iconTexture != null)
            {
                // Fallback to icon if model missing
                GUI.DrawTexture(previewRect, _iconTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.Box(previewRect, "No Model/Icon");
            }

            EditorGUILayout.Space();

            // 4. Stats
            int attack = _selectedItem.GetPower();
            if (attack > 0)
            {
                GUILayout.Label($"Attack: {attack}", _statStyle);
            }
            
            // 5. Flavor Text
            string flavor = _selectedItem.GetFlavorText();
            if (!string.IsNullOrEmpty(flavor))
            {
                EditorGUILayout.Space();
                GUIStyle flavorStyle = new GUIStyle(GUI.skin.label);
                flavorStyle.wordWrap = true;
                flavorStyle.alignment = TextAnchor.UpperLeft;
                flavorStyle.fontStyle = FontStyle.Italic;
                GUILayout.Label(flavor, flavorStyle);
            }

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
                // Header Row
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Editing: {_selectedItem.GetItemName()}", EditorStyles.boldLabel);
                if (GUILayout.Button("Copy to Clipboard", GUILayout.Width(120)))
                {
                    string data = PythonDataParser.WriteSingleItemData(_selectedItem);
                    GUIUtility.systemCopyBuffer = data;
                    Debug.Log("Item data copied: " + data);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                // Draw the POTCO-style Card
                DrawPOTCOCard();

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Raw Data Editor", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                DrawIdentitySection();
                DrawAvailabilityEconomySection();
                DrawGatingProgressionSection();
                DrawPresentationSection();
                DrawModelAppearanceSection();

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
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            _selectedItem.SetItemName(EditorGUILayout.TextField("Item Name:", _selectedItem.GetItemName()));
            EditorGUILayout.LabelField("Constant Name:", _selectedItem.GetConstantName());
            EditorGUILayout.IntField("Item ID:", _selectedItem.ItemId);
            _selectedItem.SetVersion(EditorGUILayout.IntField("Version:", _selectedItem.GetVersion()));

            // Item Class
            if (_itemDatabase.InventoryTypeConstantsMap != null && _itemDatabase.InventoryTypeConstantsMap.Count > 0)
            {
                List<string> classNames = _itemDatabase.InventoryTypeConstantsMap.Keys.OrderBy(k => k).ToList();
                List<string> classDisplayNames = classNames.Select(name => name.Replace("ItemType", "")).ToList();

                int currentItemClassValue = _selectedItem.GetItemClass();
                int selectedClassIndex = classNames.FindIndex(k => _itemDatabase.InventoryTypeConstantsMap[k] == currentItemClassValue);
                
                if (selectedClassIndex == -1) selectedClassIndex = 0;

                int newSelectedClassIndex = EditorGUILayout.Popup("Item Class:", selectedClassIndex, classDisplayNames.ToArray());
                if (newSelectedClassIndex != selectedClassIndex)
                {
                    string newClassName = classNames[newSelectedClassIndex];
                    _selectedItem.SetItemClass(_itemDatabase.InventoryTypeConstantsMap[newClassName]);
                    _selectedItem.SetItemType(0); 
                }

                // Subtype
                List<string> subtypeNames = new List<string>();
                List<string> subtypeDisplayNames = new List<string>();
                
                // Populate based on class (simplified list for brevity, can expand)
                int classVal = _itemDatabase.InventoryTypeConstantsMap[classNames[newSelectedClassIndex]];
                if (classVal == InventoryTypeConstants.GetValue("ItemTypeWeapon"))
                {
                    AddSubtypesToDropdown(subtypeNames, subtypeDisplayNames, 
                        new string[] { "CUTLASS", "SABRE", "BROADSWORD", "PISTOL", "MUSKET", "DAGGER", "GRENADE", "STAFF", "DOLL" });
                }
                else if (classVal == InventoryTypeConstants.GetValue("ItemTypeClothing"))
                {
                    AddSubtypesToDropdown(subtypeNames, subtypeDisplayNames, 
                        new string[] { "SHIRT", "VEST", "COAT", "PANT", "BELT", "BOOTS", "HAT" });
                }

                int currentType = _selectedItem.GetItemType();
                int typeIdx = subtypeNames.FindIndex(k => _itemDatabase.ItemSubtypeConstantsMap[k] == currentType);
                
                if (subtypeNames.Count > 0)
                {
                    if (typeIdx == -1) typeIdx = 0;
                    int newTypeIdx = EditorGUILayout.Popup("Subtype:", typeIdx, subtypeDisplayNames.ToArray());
                    if (newTypeIdx != typeIdx)
                    {
                        _selectedItem.SetItemType(_itemDatabase.ItemSubtypeConstantsMap[subtypeNames[newTypeIdx]]);
                    }
                }
                else
                {
                    _selectedItem.SetItemType(EditorGUILayout.IntField("Subtype (Raw):", currentType));
                }
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
            
            // Only show/set Holiday if the item class supports it
            // This prevents overwriting ITEM_MODEL (which shares the same column index 34) with 0
            if (HasHolidayData(_selectedItem.GetItemClass()))
            {
                _selectedItem.SetHoliday(EditorGUILayout.IntField("Holiday:", _selectedItem.GetHoliday())); 
            }
            
            EditorGUILayout.Space();
        }

        private bool HasHolidayData(int itemClass)
        {
            // Mapping based on POTCO logic
            // Clothing, Tattoo, Jewelry have Holiday data at index 34
            // Weapons have ITEM_MODEL at index 34
            if (_itemDatabase == null || _itemDatabase.InventoryTypeConstantsMap == null) return false;

            // Helper to safely get value
            int GetVal(string key) => InventoryTypeConstants.GetValue(key);

            if (itemClass == GetVal("ItemTypeClothing")) return true;
            if (itemClass == GetVal("ItemTypeTattoo")) return true;
            if (itemClass == GetVal("ItemTypeJewelry")) return true;
            
            return false;
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
            _selectedItem.SetPrimaryColor(EditorGUILayout.IntField("Primary Color:", _selectedItem.GetPrimaryColor()));
            _selectedItem.SetSecondaryColor(EditorGUILayout.IntField("Secondary Color:", _selectedItem.GetSecondaryColor()));
            EditorGUILayout.Space();
        }

        private void DrawWeaponSpecificSection()
        {
            EditorGUILayout.LabelField("Weapon Specific", EditorStyles.boldLabel);
            _selectedItem.SetPower(EditorGUILayout.IntField("Power:", _selectedItem.GetPower()));
            _selectedItem.SetWeaponReq(EditorGUILayout.IntField("Weapon Req:", _selectedItem.GetWeaponReq()));
            _selectedItem.SetSpecialAttack(EditorGUILayout.IntField("Special Attack:", _selectedItem.GetSpecialAttack()));
            _selectedItem.SetBarrels(EditorGUILayout.IntField("Barrels:", _selectedItem.GetBarrels()));
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
            EditorGUILayout.LabelField("Attributes (Raw IDs)", EditorStyles.boldLabel);
            // Placeholder for future complex UI
            EditorGUILayout.Space();
        }

        private void CreateNewItem()
        {
            if (_itemDatabase == null || _itemDatabase.AllItems == null) return;
            int newId = _itemDatabase.AllItems.Any() ? _itemDatabase.AllItems.Keys.Max() + 1 : 1;
            int colCount = ItemColumnMapping.Mapping.Count > 0 ? ItemColumnMapping.Mapping.Count : 50;
            List<object> newRaw = new List<object>(new object[colCount]);
            
            // Fill defaults
            if (ItemColumnMapping.Mapping.TryGetValue("ITEM_ID", out int idIdx)) newRaw[idIdx] = newId;
            if (ItemColumnMapping.Mapping.TryGetValue("ITEM_NAME", out int nameIdx)) newRaw[nameIdx] = "New Item";
            if (ItemColumnMapping.Mapping.TryGetValue("CONSTANT_NAME", out int cNameIdx)) newRaw[cNameIdx] = "NEW_ITEM_" + newId;
            
            ItemDataRow newItem = new ItemDataRow(newId, newRaw);
            _itemDatabase.AllItems.Add(newId, newItem);
            UpdateSelection(newItem);
        }

        private void DuplicateSelectedItem()
        {
            if (_selectedItem == null || _itemDatabase == null) return;
            int newId = _itemDatabase.AllItems.Keys.Max() + 1;
            List<object> copyRaw = new List<object>(_selectedItem.GetRawData());
            
            if (ItemColumnMapping.Mapping.TryGetValue("ITEM_ID", out int idIdx)) copyRaw[idIdx] = newId;
            if (ItemColumnMapping.Mapping.TryGetValue("CONSTANT_NAME", out int cIdx)) copyRaw[cIdx] = _selectedItem.GetConstantName() + "_DUP";
            
            ItemDataRow dupItem = new ItemDataRow(newId, copyRaw);
            _itemDatabase.AllItems.Add(newId, dupItem);
            UpdateSelection(dupItem);
        }

        private void AddSubtypesToDropdown(List<string> names, List<string> displayNames, string[] keys)
        {
            foreach (string k in keys)
            {
                if (_itemDatabase.ItemSubtypeConstantsMap.ContainsKey(k))
                {
                    names.Add(k);
                    displayNames.Add(k.ToTitleCase());
                }
            }
        }
    }
}
