/// <summary>
/// Clothing catalog loaded from ClothingGlobals.py and PirateMale/Female.py.
/// Maps slots to available variants with group patterns and texture options.
/// </summary>
using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <summary>Index in OG clothing arrays (from append() order)</summary>
        public int ogIndex = -1;

        /// <summary>Variant identifier (e.g., "tricorn")</summary>
        public string id;

        /// <summary>Display name from OG data</summary>
        public string displayName;

        /// <summary>OG patterns from POTCO source (before resolution)</summary>
        public List<string> ogPatterns = new();

        /// <summary>EXACT mesh group names to enable (resolved from ogPatterns at runtime)</summary>
        public List<string> showGroups = new();

        /// <summary>Body part indices to hide (from negative ints in POTCO append)</summary>
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

        /// <summary>Parsed hair cuts per hat index (Gender -> List of cut strings)</summary>
        public Dictionary<string, List<string>> hairHatCuts = new();

        /// <summary>PHASE 4 OPTIMIZATION: Flag to track if patterns have been resolved</summary>
        [System.NonSerialized]
        private bool isPatternsResolved = false;

        /// <summary>Male body index -> body_* group name (order mirrors PirateMale.py bodys list)</summary>
        public static readonly string[] MaleBodyIndexToGroup =
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

        /// <summary>Female body index -> body_* group name (order mirrors PirateFemale.py bodys list, lines 1998-2030)</summary>
        public static readonly string[] FemaleBodyIndexToGroup =
        {
            "body_neck1",          // 0
            "body_neck2",          // 1
            "body_neck_back",      // 2
            "body_collar1",        // 3
            "body_chest_center",   // 4
            "body_collar_round",   // 5
            "body_chest_remain",   // 6
            "body_ribs1",          // 7
            "body_ribs2",          // 8
            "body_torso_upper",    // 9
            "body_clavicles",      // 10
            "body_belly_button",   // 11
            "body_waist_line",     // 12
            "body_bicep_left",     // 13
            "body_bicep_right",    // 14
            "body_forearm_left",   // 15
            "body_forearm_right",  // 16
            "body_hand_left",      // 17
            "body_hand_right",     // 18
            "body_upper_hips",     // 19
            "body_hips",           // 20
            "body_thigh_left",     // 21
            "body_thigh_right",    // 22
            "body_knee_left",      // 23
            "body_knee_right",     // 24
            "body_uppercalf_left", // 25
            "body_uppercalf_right",// 26
            "body_lowercalf_left", // 27
            "body_lowercalf_right",// 28
            "body_foot_left",      // 29
            "body_foot_right",     // 30
            "body_armpit_left",    // 31
            "body_armpit_right"    // 32
        };

        /// <summary>Get body parts array for gender</summary>
        public static string[] GetBodyIndexToGroup(string gender)
        {
            return gender.ToLower() == "f" ? FemaleBodyIndexToGroup : MaleBodyIndexToGroup;
        }

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

        /// <summary>Resolve all OG patterns to exact mesh group names using the character model's renderer cache</summary>
        public void ResolvePatterns(CharacterOG.Runtime.Utils.GroupRendererCache cache)
        {
            // PHASE 4 OPTIMIZATION: Skip resolution if already done
            if (isPatternsResolved)
            {
                UnityEngine.Debug.Log("[ResolvePatterns] Patterns already resolved, skipping");
                return;
            }

            int totalResolved = 0;
            int debugCount = 0;
            foreach (var slotKvp in variantsBySlot)
            {
                foreach (var variant in slotKvp.Value)
                {
                    if (variant.ogPatterns != null && variant.ogPatterns.Count > 0)
                    {
                        variant.showGroups = CharacterOG.Runtime.Utils.PatternResolver.ResolveToExact(cache, variant.ogPatterns);
                        totalResolved += variant.showGroups.Count;

                        // Debug first few resolutions
                        if (debugCount < 10 && variant.showGroups.Count > 0)
                        {
                            string exactNames = string.Join(", ", variant.showGroups.Take(3));
                            if (variant.showGroups.Count > 3) exactNames += "...";
                            UnityEngine.Debug.Log($"[ResolvePatterns] {slotKvp.Key}[{variant.ogIndex}] '{variant.displayName}': {variant.ogPatterns.Count} patterns → {variant.showGroups.Count} exact ({exactNames})");
                            debugCount++;
                        }
                    }
                }
            }

            // Mark as resolved
            isPatternsResolved = true;
            UnityEngine.Debug.Log($"[ResolvePatterns] Resolved {totalResolved} exact mesh group names from OG patterns (cached for reuse)");
        }

        /// <summary>PHASE 4: Reset pattern resolution flag (useful if catalog is modified)</summary>
        public void ResetPatternResolution()
        {
            isPatternsResolved = false;
            UnityEngine.Debug.Log("[ClothingCatalog] Pattern resolution reset");
        }
    }
}
