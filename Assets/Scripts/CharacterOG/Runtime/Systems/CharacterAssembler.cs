/// <summary>
/// Assembles character clothing and accessories.
/// Manages slot visibility using GroupRendererCache and applies textures/dyes via MaterialBinder.
/// </summary>
using System.Collections.Generic;
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

        private Dictionary<Slot, (SlotVariant variant, int texIdx, Color? dye)> currentSlots = new();

        public CharacterAssembler(GroupRendererCache rendererCache, MaterialBinder materialBinder, ClothingCatalog catalog, Palettes palettes)
        {
            this.rendererCache = rendererCache;
            this.materialBinder = materialBinder;
            this.catalog = catalog;
            this.palettes = palettes;

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

        /// <summary>Set clothing for a slot by variant object</summary>
        public void SetSlot(Slot slot, SlotVariant variant, int textureIdx = 0, Color? dye = null)
        {
            if (variant == null)
            {
                ClearSlot(slot);
                return;
            }

            // Clear current slot first
            ClearSlot(slot);

            // Apply show/hide masks
            ApplyMasksFor(slot, variant);

            // Apply textures and dyes to visible renderers
            var renderers = GetRenderersForVariant(variant);

            string textureId = null;
            if (textureIdx >= 0 && textureIdx < variant.textureIds.Count)
            {
                textureId = variant.textureIds[textureIdx];
            }

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

            // Track current slot state
            currentSlots[slot] = (variant, textureIdx, dye);

            // Recompute body visibility
            RecomputeBodyVisibility();

            Debug.Log($"CharacterAssembler: Set {slot} to '{variant.displayName}' (tex:{textureIdx})");
        }

        /// <summary>Clear a slot (hide all variants)</summary>
        public void ClearSlot(Slot slot)
        {
            var (currentVariant, _, _) = currentSlots[slot];

            if (currentVariant != null)
            {
                // Hide current variant groups
                foreach (var groupName in currentVariant.showGroups)
                {
                    rendererCache.EnableGroup(groupName, false);
                }

                if (!string.IsNullOrEmpty(currentVariant.pattern))
                {
                    rendererCache.EnablePattern(currentVariant.pattern, false);
                }

                currentSlots[slot] = (null, 0, null);
            }
        }

        /// <summary>Clear all slots</summary>
        public void ClearAllSlots()
        {
            foreach (Slot slot in System.Enum.GetValues(typeof(Slot)))
            {
                ClearSlot(slot);
            }
        }

        /// <summary>Apply show/hide masks for a variant</summary>
        public void ApplyMasksFor(Slot slot, SlotVariant variant)
        {
            // First, use pattern if available
            if (!string.IsNullOrEmpty(variant.pattern))
            {
                rendererCache.EnablePattern(variant.pattern, true);
            }

            // Then apply explicit show groups
            foreach (var groupName in variant.showGroups)
            {
                rendererCache.EnableGroup(groupName, true);
            }

            // Then apply explicit hide groups
            foreach (var groupName in variant.hideGroups)
            {
                rendererCache.EnableGroup(groupName, false);
            }

            // Apply catalog-level masks if available
            if (catalog.masks.TryGetValue(slot, out var slotMasks))
            {
                if (slotMasks.TryGetValue(variant.id, out var mask))
                {
                    foreach (var groupName in mask.show)
                    {
                        rendererCache.EnableGroup(groupName, true);
                    }

                    foreach (var groupName in mask.hide)
                    {
                        rendererCache.EnableGroup(groupName, false);
                    }
                }
            }
        }

        /// <summary>Get renderers affected by a variant</summary>
        private List<Renderer> GetRenderersForVariant(SlotVariant variant)
        {
            var renderers = new List<Renderer>();

            // Get from pattern
            if (!string.IsNullOrEmpty(variant.pattern))
            {
                renderers.AddRange(rendererCache.GetRenderersMatchingPattern(variant.pattern));
            }

            // Get from explicit show groups
            foreach (var groupName in variant.showGroups)
            {
                renderers.AddRange(rendererCache.GetRenderers(groupName));
            }

            return renderers;
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

                Color? dye = null;
                if (colorIdx >= 0)
                {
                    dye = palettes.GetDyeColor(colorIdx);
                }

                SetSlotByIndex(slot, idx, texIdx, dye);
            }

            Debug.Log($"CharacterAssembler: Applied underwear for {gender}");
        }

        /// <summary>Recompute body visibility based on current slots</summary>
        private void RecomputeBodyVisibility()
        {
            // Start by showing all body groups
            for (int i = 0; i < ClothingCatalog.BodyIndexToGroup.Length; i++)
            {
                string grp = ClothingCatalog.BodyIndexToGroup[i];
                rendererCache.EnableGroup(grp, true);
            }

            // Then hide any body indices covered by active slots
            foreach (var kvp in currentSlots)
            {
                var (variant, _, _) = kvp.Value;
                if (variant != null)
                {
                    foreach (var bodyIdx in variant.bodyHideIndices)
                    {
                        if (bodyIdx >= 0 && bodyIdx < ClothingCatalog.BodyIndexToGroup.Length)
                        {
                            string grp = ClothingCatalog.BodyIndexToGroup[bodyIdx];
                            rendererCache.EnableGroup(grp, false);
                        }
                    }
                }
            }
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
