/// <summary>
/// Color palettes from HumanDNA.py.
/// Includes skin colors, dye colors, and slot-specific dye mappings.
/// </summary>
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterOG.Models
{
    [Serializable]
    public class Palettes
    {
        /// <summary>Skin color palette from skinColors array</summary>
        public List<Color> skin = new();

        /// <summary>Hair color palette from hairColors array</summary>
        public List<Color> hair = new();

        /// <summary>Dye color palette from DYE_COLORS / hatColorsOld</summary>
        public List<Color> dye = new();

        /// <summary>Hat colors from hatColorsOld array (all elements)</summary>
        public List<List<Color>> hatColors = new();

        /// <summary>Crazy skin colors from crazySkinColors array</summary>
        public List<Color> crazySkin = new();

        /// <summary>Jewelry colors from jewelryColors array</summary>
        public List<Color> jewelry = new();

        /// <summary>Top clothing colors from clothesTopColorsOld array</summary>
        public List<List<Color>> clothesTopColors = new();

        /// <summary>Bottom clothing colors from clothesBotColorsOld array</summary>
        public List<List<Color>> clothesBotColors = new();

        /// <summary>Slot → available dye indices mapping</summary>
        public Dictionary<Slot, List<int>> slotToDyeIndices = new();

        /// <summary>Level-based dye color unlocks from DYE_COLOR_LEVEL</summary>
        public Dictionary<int, List<int>> dyeColorLevels = new();

        public Palettes()
        {
            foreach (Slot slot in Enum.GetValues(typeof(Slot)))
            {
                slotToDyeIndices[slot] = new List<int>();
            }
        }

        /// <summary>Get skin color by index (safe, returns white if out of range)</summary>
        public Color GetSkinColor(int index)
        {
            if (index < 0 || index >= skin.Count)
                return Color.white;
            return skin[index];
        }

        /// <summary>Get hair color by index (safe, returns white if out of range)</summary>
        public Color GetHairColor(int index)
        {
            if (index < 0 || index >= hair.Count)
                return Color.white;
            return hair[index];
        }

        /// <summary>Get dye color by index (safe, returns white if out of range)</summary>
        public Color GetDyeColor(int index)
        {
            if (index < 0 || index >= dye.Count)
                return Color.white;
            return dye[index];
        }

        /// <summary>Get hat color by set and index (safe, returns white if out of range)</summary>
        public Color GetHatColor(int setIndex, int colorIndex)
        {
            if (setIndex < 0 || setIndex >= hatColors.Count)
                return Color.white;
            var colorSet = hatColors[setIndex];
            if (colorIndex < 0 || colorIndex >= colorSet.Count)
                return Color.white;
            return colorSet[colorIndex];
        }

        /// <summary>Get crazy skin color by index (safe, returns white if out of range)</summary>
        public Color GetCrazySkinColor(int index)
        {
            if (index < 0 || index >= crazySkin.Count)
                return Color.white;
            return crazySkin[index];
        }

        /// <summary>Get jewelry color by index (safe, returns white if out of range)</summary>
        public Color GetJewelryColor(int index)
        {
            if (index < 0 || index >= jewelry.Count)
                return Color.white;
            return jewelry[index];
        }

        /// <summary>Get top clothing color by set and index (safe, returns white if out of range)</summary>
        public Color GetTopClothesColor(int setIndex, int colorIndex)
        {
            if (setIndex < 0 || setIndex >= clothesTopColors.Count)
                return Color.white;
            var colorSet = clothesTopColors[setIndex];
            if (colorIndex < 0 || colorIndex >= colorSet.Count)
                return Color.white;
            return colorSet[colorIndex];
        }

        /// <summary>Get bottom clothing color by set and index (safe, returns white if out of range)</summary>
        public Color GetBotClothesColor(int setIndex, int colorIndex)
        {
            if (setIndex < 0 || setIndex >= clothesBotColors.Count)
                return Color.white;
            var colorSet = clothesBotColors[setIndex];
            if (colorIndex < 0 || colorIndex >= colorSet.Count)
                return Color.white;
            return colorSet[colorIndex];
        }

        /// <summary>Check if a dye index is allowed for a given slot</summary>
        public bool IsDyeAllowedForSlot(Slot slot, int dyeIndex)
        {
            if (!slotToDyeIndices.TryGetValue(slot, out var allowedIndices))
                return false;

            return allowedIndices.Count == 0 || allowedIndices.Contains(dyeIndex);
        }
    }
}
