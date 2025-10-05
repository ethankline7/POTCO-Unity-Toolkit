/// <summary>
/// Clothing catalog loaded from ClothingGlobals.py and PirateMale/Female.py.
/// Maps slots to available variants with group patterns and texture options.
/// </summary>
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterOG.Models
{
    public enum Slot
    {
        Hat = 0,
        Shirt = 1,
        Vest = 2,
        Coat = 3,
        Pant = 4,
        Belt = 5,
        Sock = 6,
        Shoe = 7,
        Hair = 8,
        Beard = 9,
        Mustache = 10
    }

    [Serializable]
    public class SlotVariant
    {
        /// <summary>Index in OG clothing arrays</summary>
        public int ogIndex = -1;

        /// <summary>Variant identifier (e.g., "tricorn")</summary>
        public string id;

        /// <summary>Display name from OG data</summary>
        public string displayName;

        /// <summary>Group pattern (e.g., "**/clothing_layer1_hat_tricorn*")</summary>
        public string pattern;

        /// <summary>Explicit group names to enable</summary>
        public List<string> showGroups = new();

        /// <summary>Explicit group names to disable</summary>
        public List<string> hideGroups = new();

        /// <summary>NEW: indices from PirateMale/Female clothingsX lists (negative ints become positive here)</summary>
        public List<int> bodyHideIndices = new();

        /// <summary>Texture IDs available for this variant</summary>
        public List<string> textureIds = new();

        /// <summary>Dye channel names (e.g., ["base", "trim"])</summary>
        public List<string> dyeChannels = new();
    }

    [Serializable]
    public class ClothingCatalog
    {
        /// <summary>All variants organized by slot</summary>
        public Dictionary<Slot, List<SlotVariant>> variantsBySlot = new();

        /// <summary>Optional fine-grained masks: slot → variantId → (show, hide)</summary>
        public Dictionary<Slot, Dictionary<string, (List<string> show, List<string> hide)>> masks = new();

        /// <summary>Slot-level dye permission indices from OG data</summary>
        public Dictionary<Slot, List<int>> slotDyePermissionIndices = new();

        /// <summary>Gender-specific underwear defaults</summary>
        public Dictionary<string, Dictionary<Slot, (int idx, int texIdx, int colorIdx)>> underwear = new();

        /// <summary>NEW: map OG body index -> the actual body_* group name (order mirrors PirateMale.py bodys list)</summary>
        public static readonly string[] BodyIndexToGroup =
        {
            "body_neck",         // 0  (note: Pirate uses body_neck* in findAllMatches)
            "body_torso_base",   // 1
            "body_torso_back",   // 2
            "body_torso_front",  // 3
            "body_collar_sharp", // 4
            "body_collar_round", // 5
            "body_belt",         // 6
            "body_waist",        // 7
            "body_armpit_right", // 8
            "body_armpit_left",  // 9
            "body_shoulder_right",//10
            "body_shoulder_left", //11
            "body_forearm_right", //12
            "body_forearm_left",  //13
            "body_hand_right",    //14
            "body_hand_left",     //15
            "body_knee_right",    //16
            "body_knee_left",     //17
            "body_shin_right",    //18
            "body_shin_left",     //19
            "body_foot_right",    //20
            "body_foot_left"      //21
        };

        public ClothingCatalog()
        {
            foreach (Slot slot in Enum.GetValues(typeof(Slot)))
            {
                variantsBySlot[slot] = new List<SlotVariant>();
            }
        }

        /// <summary>Get all variants for a slot</summary>
        public List<SlotVariant> GetVariants(Slot slot)
            => variantsBySlot.TryGetValue(slot, out var v) ? v : new List<SlotVariant>();

        /// <summary>Get variant by slot and OG index</summary>
        public SlotVariant GetVariant(Slot slot, int ogIndex)
        {
            if (!variantsBySlot.TryGetValue(slot, out var variants))
                return null;

            return variants.Find(v => v.ogIndex == ogIndex);
        }

        /// <summary>Get variant by slot and OG index (alternative name)</summary>
        public SlotVariant GetVariantByOgIndex(Slot slot, int ogIndex)
            => GetVariant(slot, ogIndex);

        /// <summary>Get variant by slot and ID</summary>
        public SlotVariant GetVariantById(Slot slot, string id)
        {
            if (!variantsBySlot.TryGetValue(slot, out var variants))
                return null;

            return variants.Find(v => v.id == id);
        }
    }
}
