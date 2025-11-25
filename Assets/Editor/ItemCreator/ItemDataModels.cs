using System;
using System.Collections.Generic;
using UnityEngine; // For Vector3, if LVector3f was actually used and needed conversion

namespace POTCO.Editor.ItemCreator
{
    /// <summary>
    /// Stores the mapping from column name to its index in the item data list.
    /// This is populated by parsing ItemData.py's 'columnHeadings' entry.
    /// </summary>
    public static class ItemColumnMapping
    {
        public static Dictionary<string, int> Mapping { get; private set; } = new Dictionary<string, int>();

        public static void SetMapping(Dictionary<string, int> mapping)
        {
            if (mapping == null)
            {
                Debug.LogError("Attempted to set null mapping for ItemColumnMapping.");
                return;
            }
            Mapping = mapping;
        }

        public static int GetColumnIndex(string columnName)
        {
            if (Mapping.TryGetValue(columnName, out int index))
            {
                return index;
            }
            Debug.LogError($"Column '{columnName}' not found in ItemColumnMapping. Please ensure ItemData.py is correctly parsed.");
            return -1; // Or throw an exception
        }
    }

    /// <summary>
    /// Represents a single row of item data, providing type-safe access to item properties
    /// using the dynamically loaded column mapping.
    /// </summary>
    public class ItemDataRow
    {
        public int ItemId { get; private set; }
        private readonly List<object> _data;

        public ItemDataRow(int itemId, List<object> data)
        {
            ItemId = itemId;
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        private T GetValue<T>(string columnName, T defaultValue = default(T))
        {
            int index = ItemColumnMapping.GetColumnIndex(columnName);
            if (index >= 0 && index < _data.Count)
            {
                object rawValue = _data[index];
                
                // Handle nulls
                if (rawValue == null) return defaultValue;

                // Handle direct assignment if types match
                if (rawValue is T tValue) return tValue;

                try
                {
                    // Handle Python 'u'' prefix for strings if the raw value didn't match T directly
                    if (typeof(T) == typeof(string) && rawValue is string s)
                    {
                        if (s.StartsWith("u'") && s.EndsWith("'"))
                        {
                            return (T)(object)s.Substring(2, s.Length - 3);
                        }
                    }

                    // Handle converting Int to String (e.g. model ID -1 to "-1")
                    if (typeof(T) == typeof(string))
                    {
                        return (T)(object)rawValue.ToString();
                    }

                    // Handle generic conversion (e.g. string "0" to int 0, or float to int)
                    // Use invariant culture to be safe with numbers
                    return (T)Convert.ChangeType(rawValue, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception)
                {
                    // Polymorphic column collision (e.g. reading String column as Int)
                    // Just return default value, do not spam errors
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        // Setters - important for editing. Need to ensure index is valid and type is correct.
        private void SetValue(string columnName, object value)
        {
            int index = ItemColumnMapping.GetColumnIndex(columnName);
            if (index >= 0 && index < _data.Count)
            {
                object currentValue = _data[index];
                
                // Try to set directly if types are compatible
                if (currentValue != null && value.GetType() == currentValue.GetType())
                {
                    _data[index] = value;
                    return;
                }

                // If types differ, attempt conversion
                try
                {
                    // Special handling for setting strings back to 'u' format if needed?
                    // For now, just store the raw value. The Writer handles adding u''.
                    
                    Type targetType = currentValue?.GetType() ?? value.GetType();
                    
                    // If target is int but value is string, try parsing
                    object convertedValue = Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture);
                    _data[index] = convertedValue;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to set item ID {ItemId}, column '{columnName}'. Could not convert {value.GetType()} to {currentValue?.GetType()}. Error: {e.Message}");
                }
            }
            else
            {
                // Debug.LogWarning($"Cannot set value for column '{columnName}'...");
            }
        }

        public int GetItemClass() => GetValue<int>("ITEM_CLASS");
        public int GetVersion() => GetValue<int>("VERSION");
        public int GetGoldCost() => GetValue<int>("GOLD_COST");
        public string GetItemName() => GetValue<string>("ITEM_NAME");
        public string GetConstantName() => GetValue<string>("CONSTANT_NAME");
        public int GetRarity() => GetValue<int>("RARITY");
        public int GetItemType() => GetValue<int>("ITEM_TYPE");
        public string GetItemIcon() => GetValue<string>("ITEM_ICON");
        public string GetFlavorText() => GetValue<string>("FLAVOR_TEXT");
        public string GetItemModel() => GetValue<string>("ITEM_MODEL");
        public string GetMaleModelId() => GetValue<string>("MALE_MODEL_ID");
        public string GetFemaleModelId() => GetValue<string>("FEMALE_MODEL_ID");
        public int GetPower() => GetValue<int>("POWER");
        public int GetWeaponReq() => GetValue<int>("WEAPON_REQ");
        public int GetStackLimit() => GetValue<int>("STACK_LIMIT");
        public bool IsFromLoot() => GetValue<int>("FROM_LOOT") == 1;
        public bool IsFromShop() => GetValue<int>("FROM_SHOP") == 1;
        public bool IsFromQuest() => GetValue<int>("FROM_QUEST") == 1;
        public bool IsFromPromo() => GetValue<int>("FROM_PROMO") == 1;
        public bool IsFromPVP() => GetValue<int>("FROM_PVP") == 1;
        public bool IsFromNPC() => GetValue<int>("FROM_NPC") == 1;
        public int GetHoliday() => GetValue<int>("HOLIDAY");
        public int GetNotorietyReq() => GetValue<int>("NOTORIETY_REQ");
        public int GetItemNotorietyReq() => GetValue<int>("ITEM_NOTORIETY_REQ");
        public int GetVelvetRope() => GetValue<int>("VELVET_ROPE");
        public int GetUseSkill() => GetValue<int>("USE_SKILL");
        public int GetSpecialAttackRank() => GetValue<int>("SPECIAL_ATTACK_RANK");
        public int GetSpecialAttack() => GetValue<int>("SPECIAL_ATTACK");
        public int GetBarrels() => GetValue<int>("BARRELS");
        public int GetVfxType1() => GetValue<int>("VFX_TYPE_1");
        public int GetVfxType2() => GetValue<int>("VFX_TYPE_2");
        public int GetVfxOffset() => GetValue<int>("VFX_OFFSET");
        public int GetPrimaryColor() => GetValue<int>("PRIMARY_COLOR");
        public int GetSecondaryColor() => GetValue<int>("SECONDARY_COLOR");
        public int GetMaleTextureId() => GetValue<int>("MALE_TEXTURE_ID");
        public int GetFemaleTextureId() => GetValue<int>("FEMALE_TEXTURE_ID");
        public int GetMaleOrientation() => GetValue<int>("MALE_ORIENTATION");
        public int GetMaleOrientation2() => GetValue<int>("MALE_ORIENTATION_2");
        public int GetFemaleOrientation() => GetValue<int>("FEMALE_ORIENTATION");
        public int GetFemaleOrientation2() => GetValue<int>("FEMALE_ORIENTATION_2");
        public bool CanDyeItem() => GetValue<int>("CAN_DYE_ITEM") == 1;
        public int GetRating() => GetValue<int>("RATING");

        public void SetItemClass(int value) => SetValue("ITEM_CLASS", value);
        public void SetVersion(int value) => SetValue("VERSION", value);
        public void SetGoldCost(int value) => SetValue("GOLD_COST", value);
        public void SetItemName(string value) => SetValue("ITEM_NAME", value);
        public void SetConstantName(string value) => SetValue("CONSTANT_NAME", value);
        public void SetRarity(int value) => SetValue("RARITY", value);
        public void SetItemType(int value) => SetValue("ITEM_TYPE", value);
        public void SetItemIcon(string value) => SetValue("ITEM_ICON", value);
        public void SetFlavorText(string value) => SetValue("FLAVOR_TEXT", value);
        public void SetItemModel(string value) => SetValue("ITEM_MODEL", value);
        public void SetMaleModelId(string value) => SetValue("MALE_MODEL_ID", value);
        public void SetFemaleModelId(string value) => SetValue("FEMALE_MODEL_ID", value);
        public void SetPower(int value) => SetValue("POWER", value);
        public void SetWeaponReq(int value) => SetValue("WEAPON_REQ", value);
        public void SetStackLimit(int value) => SetValue("STACK_LIMIT", value);
        public void SetFromLoot(bool value) => SetValue("FROM_LOOT", value ? 1 : 0);
        public void SetFromShop(bool value) => SetValue("FROM_SHOP", value ? 1 : 0);
        public void SetFromQuest(bool value) => SetValue("FROM_QUEST", value ? 1 : 0);
        public void SetFromPromo(bool value) => SetValue("FROM_PROMO", value ? 1 : 0);
        public void SetFromPVP(bool value) => SetValue("FROM_PVP", value ? 1 : 0);
        public void SetFromNPC(bool value) => SetValue("FROM_NPC", value ? 1 : 0);
        public void SetHoliday(int value) => SetValue("HOLIDAY", value);
        public void SetNotorietyReq(int value) => SetValue("NOTORIETY_REQ", value);
        public void SetItemNotorietyReq(int value) => SetValue("ITEM_NOTORIETY_REQ", value);
        public void SetVelvetRope(int value) => SetValue("VELVET_ROPE", value);
        public void SetUseSkill(int value) => SetValue("USE_SKILL", value);
        public void SetSpecialAttackRank(int value) => SetValue("SPECIAL_ATTACK_RANK", value);
        public void SetSpecialAttack(int value) => SetValue("SPECIAL_ATTACK", value);
        public void SetBarrels(int value) => SetValue("BARRELS", value);
        public void SetVfxType1(int value) => SetValue("VFX_TYPE_1", value);
        public void SetVfxType2(int value) => SetValue("VFX_TYPE_2", value);
        public void SetVfxOffset(int value) => SetValue("VFX_OFFSET", value);
        public void SetPrimaryColor(int value) => SetValue("PRIMARY_COLOR", value);
        public void SetSecondaryColor(int value) => SetValue("SECONDARY_COLOR", value);
        public void SetMaleTextureId(int value) => SetValue("MALE_TEXTURE_ID", value);
        public void SetFemaleTextureId(int value) => SetValue("FEMALE_TEXTURE_ID", value);
        public void SetMaleOrientation(int value) => SetValue("MALE_ORIENTATION", value);
        public void SetMaleOrientation2(int value) => SetValue("MALE_ORIENTATION_2", value);
        public void SetFemaleOrientation(int value) => SetValue("FEMALE_ORIENTATION", value);
        public void SetFemaleOrientation2(int value) => SetValue("FEMALE_ORIENTATION_2", value);
        public void SetCanDyeItem(bool value) => SetValue("CAN_DYE_ITEM", value ? 1 : 0);
        public void SetRating(int value) => SetValue("RATING", value);


        // Method to get the raw data list (useful for saving back to Python format)
        public List<object> GetRawData()
        {
            return _data;
        }
    }


    /// <summary>
    /// Stores the dynamically parsed constants for InventoryType from UberDogGlobals.py.
    /// </summary>
    public static class InventoryTypeConstants
    {
        public static Dictionary<string, int> Mapping { get; private set; } = new Dictionary<string, int>();

        public static void SetMapping(Dictionary<string, int> mapping)
        {
            if (mapping == null)
            {
                Debug.LogError("Attempted to set null mapping for InventoryTypeConstants.");
                return;
            }
            Mapping = mapping;
        }

        public static int GetValue(string constantName)
        {
            if (Mapping.TryGetValue(constantName, out int value))
            {
                return value;
            }
            Debug.LogError($"InventoryType constant '{constantName}' not found. Please ensure UberDogGlobals.py is correctly parsed.");
            return -1; // Or throw an exception
        }
    }

    /// <summary>
    /// Stores the dynamically parsed constants for item subtypes (e.g., Weapon Subtypes) from ItemConstants.py.
    /// </summary>
    public static class ItemSubtypeConstants
    {
        public static Dictionary<string, int> Mapping { get; private set; } = new Dictionary<string, int>();

        public static void SetMapping(Dictionary<string, int> mapping)
        {
            if (mapping == null)
            {
                Debug.LogError("Attempted to set null mapping for ItemSubtypeConstants.");
                return;
            }
            Mapping = mapping;
        }

        public static int GetValue(string constantName)
        {
            if (Mapping.TryGetValue(constantName, out int value))
            {
                return value;
            }
            Debug.LogError($"Item Subtype constant '{constantName}' not found. Please ensure ItemConstants.py is correctly parsed.");
            return -1; // Or throw an exception
        }
    }

    /// <summary>
    /// Stores dynamically parsed constants for item rarities from ItemConstants.py.
    /// (Assuming ItemConstants.py defines RARITY_COMMON, RARITY_UNCOMMON etc.)
    /// </summary>
    public static class ItemRarityConstants
    {
        public static Dictionary<string, int> Mapping { get; private set; } = new Dictionary<string, int>();

        public static void SetMapping(Dictionary<string, int> mapping)
        {
            if (mapping == null)
            {
                Debug.LogError("Attempted to set null mapping for ItemRarityConstants.");
                return;
            }
            Mapping = mapping;
        }

        public static int GetValue(string constantName)
        {
            if (Mapping.TryGetValue(constantName, out int value))
            {
                return value;
            }
            Debug.LogError($"Item Rarity constant '{constantName}' not found. Please ensure ItemConstants.py is correctly parsed.");
            return -1; // Or throw an exception
        }
    }
}