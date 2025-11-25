/// <summary>
/// Assembles character clothing and accessories.
/// Manages slot visibility using GroupRendererCache and applies textures/dyes via MaterialBinder.
/// </summary>
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CharacterOG.Models;
using CharacterOG.Runtime.Utils;

namespace CharacterOG.Runtime.Systems
{
    public class CharacterAssembler
    {
        private GroupRendererCache rendererCache;
        private MaterialBinder materialBinder;
        private ClothingCatalog catalog;
        private Palettes palettes;
        private string gender;

        private Dictionary<Slot, (SlotVariant variant, int texIdx, Color? dye)> currentSlots = new();

        public CharacterAssembler(GroupRendererCache rendererCache, MaterialBinder materialBinder, ClothingCatalog catalog, Palettes palettes, string gender)
        {
            this.rendererCache = rendererCache;
            this.materialBinder = materialBinder;
            this.catalog = catalog;
            this.palettes = palettes;
            this.gender = gender;

            // Initialize slot tracking
            foreach (Slot slot in System.Enum.GetValues(typeof(Slot)))
            {
                currentSlots[slot] = (null, 0, null);
            }
        }

        /// <summary>Set clothing for a slot by variant ID</summary>
        public void SetSlot(Slot slot, string variantId, int textureIdx = 0, Color? dye = null)
        {
            var variant = catalog.GetVariantById(slot, variantId);

            if (variant == null)
            {
                Debug.LogWarning($"CharacterAssembler: Variant '{variantId}' not found for slot {slot}");
                return;
            }

            SetSlot(slot, variant, textureIdx, dye);
        }

        /// <summary>Set clothing for a slot by OG index</summary>
        public void SetSlotByIndex(Slot slot, int ogIndex, int textureIdx = 0, Color? dye = null)
        {
            var variant = catalog.GetVariant(slot, ogIndex);

            if (variant == null)
            {
                Debug.LogWarning($"CharacterAssembler: No variant at index {ogIndex} for slot {slot}");
                return;
            }

            SetSlot(slot, variant, textureIdx, dye);
        }

        /// <summary>Set clothing for a slot by variant object (EXCLUSIVE TOGGLING)</summary>
        public void SetSlot(Slot slot, SlotVariant variant, int textureIdx = 0, Color? dye = null)
        {
            if (variant == null)
            {
                ClearSlot(slot);
                return;
            }

            // STEP 1: Disable all groups owned by this slot (slot exclusive behavior)
            DisableActiveVariant(slot);

            // STEP 2: Enable only the chosen variant's exact groups
            rendererCache.EnableExactMany(variant.showGroups, true);
            Debug.Log($"[SetSlot] {slot}: Enabled {variant.showGroups.Count} groups for '{variant.displayName}' (ogIndex {variant.ogIndex})");

            // STEP 3: Apply textures and dyes
            string textureId = null;
            if (textureIdx > 0 && textureIdx < variant.textureIds.Count)
            {
                // Note: textureIdx > 0 (not >= 0) because index 0 often means "use default material texture"
                // Only apply custom textures for index 1+
                textureId = variant.textureIds[textureIdx];
                Debug.Log($"[SetSlot] {slot}: Using texture index {textureIdx} = '{textureId}'");
            }
            else if (textureIdx == 0)
            {
                Debug.Log($"[SetSlot] {slot}: Texture index 0 - using default material texture (not applying custom texture)");
            }

            // Get renderers for exact groups only
            foreach (var groupName in variant.showGroups)
            {
                var renderers = GetRenderersForExactGroup(groupName);
                foreach (var renderer in renderers)
                {
                    if (!string.IsNullOrEmpty(textureId))
                    {
                        materialBinder.ApplyTexture(renderer, textureId);
                    }

                    if (dye.HasValue)
                    {
                        materialBinder.ApplyDye(renderer, dye.Value);
                    }
                }
            }

            // STEP 4: Track current slot state
            currentSlots[slot] = (variant, textureIdx, dye);

            // STEP 5: Recompute body visibility (union all active variants' bodyHideIndices)
            RecomputeBodyVisibilityInternal();

            Debug.Log($"CharacterAssembler: Set {slot} to '{variant.displayName}' (tex:{textureIdx})");
        }

        /// <summary>Clear a slot (disable all variants for that slot)</summary>
        public void ClearSlot(Slot slot)
        {
            DisableActiveVariant(slot);
            currentSlots[slot] = (null, 0, null);
            RecomputeBodyVisibilityInternal();
        }

        /// <summary>Disable the currently active variant for a slot</summary>
        private void DisableActiveVariant(Slot slot)
        {
            var (variant, _, _) = currentSlots[slot];
            if (variant != null)
            {
                rendererCache.EnableExactMany(variant.showGroups, false);
                Debug.Log($"[DisableActiveVariant] {slot}: Disabled {variant.showGroups.Count} groups for '{variant.displayName}'");
            }
        }

        /// <summary>Get renderers for an exact group name</summary>
        private List<Renderer> GetRenderersForExactGroup(string groupName)
        {
            return rendererCache.GetExact(groupName);
        }

        /// <summary>Clear all slots</summary>
        public void ClearAllSlots()
        {
            foreach (Slot slot in System.Enum.GetValues(typeof(Slot)))
            {
                ClearSlot(slot);
            }
        }


        /// <summary>Get current variant for slot</summary>
        public SlotVariant GetCurrentVariant(Slot slot)
        {
            return currentSlots[slot].variant;
        }

        /// <summary>Apply underwear defaults from catalog</summary>
        public void ApplyUnderwear(string gender)
        {
            if (!catalog.underwear.TryGetValue(gender, out var underwearSet))
            {
                Debug.LogWarning($"CharacterAssembler: No underwear defined for gender '{gender}'");
                return;
            }

            foreach (var kvp in underwearSet)
            {
                Slot slot = kvp.Key;
                var (idx, texIdx, colorIdx) = kvp.Value;

                var variant = catalog.GetVariant(slot, idx);
                string bodyHideStr = variant != null && variant.bodyHideIndices.Count > 0
                    ? string.Join(",", variant.bodyHideIndices)
                    : "NONE";

                Debug.Log($"[ApplyUnderwear] {gender}: {slot} index={idx}, tex={texIdx}, color={colorIdx}, bodyHides=[{bodyHideStr}]");

                Color? dye = null;
                if (colorIdx >= 0)
                {
                    dye = palettes.GetDyeColor(colorIdx);
                }

                SetSlotByIndex(slot, idx, texIdx, dye);
            }

            Debug.Log($"CharacterAssembler: Applied underwear for {gender}");
        }

        /// <summary>
        /// Public method to recompute body visibility, optionally excluding certain slots.
        /// Used when clothing layer hiding rules override normal body hide behavior.
        /// </summary>
        public void RecomputeBodyVisibility(HashSet<Slot> excludeSlots = null)
        {
            RecomputeBodyVisibilityInternal(excludeSlots);
        }

        /// <summary>
        /// Recompute body visibility based on current slots.
        /// Mirrors POTCO's handleLayer*Hiding: union all active variants' bodyHideIndices and disable those body parts.
        /// </summary>
        private void RecomputeBodyVisibilityInternal(HashSet<Slot> excludeSlots = null)
        {
            // Get gender-specific body parts
            var bodyParts = ClothingCatalog.GetBodyIndexToGroup(gender);

            // STEP 1: Enable all body parts first
            foreach (var name in bodyParts)
            {
                rendererCache.EnableExact(name, true);
            }

            // STEP 2: Union all hides from currently equipped clothing
            var toHide = new HashSet<int>();
            foreach (var kvp in currentSlots)
            {
                // Skip excluded slots (e.g., shirt when vest >= 3 hides it completely)
                if (excludeSlots != null && excludeSlots.Contains(kvp.Key))
                {
                    Debug.Log($"[RecomputeBodyVisibility] Skipping {kvp.Key} (excluded from body hide calculation)");
                    continue;
                }

                var (variant, _, _) = kvp.Value;
                if (variant != null && variant.bodyHideIndices.Count > 0)
                {
                    Debug.Log($"[RecomputeBodyVisibility] {kvp.Key} variant '{variant.displayName}' (ogIndex {variant.ogIndex}) has {variant.bodyHideIndices.Count} body hides: {string.Join(",", variant.bodyHideIndices)}");
                    foreach (var h in variant.bodyHideIndices)
                    {
                        toHide.Add(h);
                    }
                }
            }

            // STEP 3: Disable those exact body groups
            var hiddenParts = new List<string>();
            foreach (var i in toHide)
            {
                if (i >= 0 && i < bodyParts.Length)
                {
                    string partName = bodyParts[i];
                    rendererCache.EnableExact(partName, false);
                    hiddenParts.Add(partName);
                    Debug.Log($"[RecomputeBodyVisibility] Hiding body part index {i}: {partName}");
                }
                else
                {
                    Debug.LogWarning($"[RecomputeBodyVisibility] Invalid body hide index {i} (bodyParts.Length={bodyParts.Length})");
                }
            }

            if (hiddenParts.Count > 0)
            {
                Debug.Log($"[RecomputeBodyVisibility] Total hidden: {hiddenParts.Count} body parts: {string.Join(", ", hiddenParts)}");
            }
            else
            {
                Debug.Log($"[RecomputeBodyVisibility] No body parts hidden (toHide was empty)");
            }
        }

        /// <summary>
        /// Permanently remove unused meshes to save memory/CPU.
        /// Only use for static NPCs!
        /// </summary>
        public void OptimizeForStatic()
        {
            rendererCache.StripHidden();
        }

        /// <summary>Get diagnostic info</summary>
        public string GetDiagnosticInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("CharacterAssembler:");

            foreach (var kvp in currentSlots)
            {
                var (variant, texIdx, dye) = kvp.Value;
                if (variant != null)
                {
                    sb.AppendLine($"  {kvp.Key}: {variant.displayName} (tex:{texIdx}, dye:{dye?.ToString() ?? "none"})");
                }
                else
                {
                    sb.AppendLine($"  {kvp.Key}: <empty>");
                }
            }

            return sb.ToString();
        }
    }
}
