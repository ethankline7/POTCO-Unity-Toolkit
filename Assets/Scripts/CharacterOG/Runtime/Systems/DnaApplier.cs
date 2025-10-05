/// <summary>
/// High-level DNA application system.
/// Ties together BodyShapeApplier, CharacterAssembler, and other systems
/// to apply a complete PirateDNA to a character model.
/// </summary>
using System.Collections.Generic;
using UnityEngine;
using CharacterOG.Models;
using CharacterOG.Runtime.Utils;

namespace CharacterOG.Runtime.Systems
{
    public class DnaApplier
    {
        private BodyShapeApplier bodyShapeApplier;
        private CharacterAssembler assembler;
        private GroupRendererCache rendererCache;
        private MaterialBinder materialBinder;

        private Dictionary<string, BodyShapeDef> bodyShapes;
        private Palettes palettes;
        private JewelryTattooDefs jewelryTattoos;

        private PirateDNA currentDna;

        public DnaApplier(
            GameObject characterRoot,
            Dictionary<string, BodyShapeDef> bodyShapes,
            ClothingCatalog catalog,
            Palettes palettes,
            JewelryTattooDefs jewelryTattoos,
            Transform headRoot = null,
            Transform bodyRoot = null)
        {
            this.bodyShapes = bodyShapes;
            this.palettes = palettes;
            this.jewelryTattoos = jewelryTattoos;

            // Initialize systems
            rendererCache = new GroupRendererCache(characterRoot);
            materialBinder = new MaterialBinder();

            // CRITICAL: Resolve OG patterns to exact mesh group names using the character's actual mesh
            catalog.ResolvePatterns(rendererCache);

            assembler = new CharacterAssembler(rendererCache, materialBinder, catalog, palettes);
            bodyShapeApplier = new BodyShapeApplier(characterRoot.transform, headRoot, bodyRoot);
        }

        /// <summary>Apply complete DNA to character</summary>
        public void ApplyDNA(PirateDNA dna)
        {
            if (dna == null)
            {
                Debug.LogWarning("DnaApplier: Null DNA provided");
                return;
            }

            currentDna = dna;

            // 0. Hide all clothing and show all body by default
            HideAllClothing();

            // 0.5. Always show head (will be handled by RecomputeBodyVisibility)
            // Body parts are shown/hidden via exact names in CharacterAssembler

            // 1. Apply body shape
            if (bodyShapes.TryGetValue(dna.bodyShape, out var shape))
            {
                bodyShapeApplier.ApplyBodyShape(shape);
                bodyShapeApplier.ApplyHeightBias(dna.bodyHeight);
            }
            else
            {
                Debug.LogWarning($"DnaApplier: Body shape '{dna.bodyShape}' not found");
            }

            // 2. Apply underwear defaults first
            assembler.ApplyUnderwear(dna.gender);

            // 3. Apply clothing slots
            ApplyClothingSlot(Slot.Hat, dna.hat, dna.hatTex, dna.hatColorIdx);
            ApplyClothingSlot(Slot.Shirt, dna.shirt, dna.shirtTex, dna.topColorIdx);
            ApplyClothingSlot(Slot.Vest, dna.vest, dna.vestTex, dna.topColorIdx);
            ApplyClothingSlot(Slot.Coat, dna.coat, dna.coatTex, dna.topColorIdx);
            ApplyClothingSlot(Slot.Belt, dna.belt, dna.beltTex, -1);
            ApplyClothingSlot(Slot.Pant, dna.pants, dna.pantsTex, dna.botColorIdx);
            ApplyClothingSlot(Slot.Shoe, dna.shoes, dna.shoesTex, dna.botColorIdx);

            // 4. Apply hair/facial hair
            ApplyClothingSlot(Slot.Hair, dna.hair, 0, dna.hairColorIdx);
            ApplyClothingSlot(Slot.Beard, dna.beard, 0, dna.hairColorIdx);
            ApplyClothingSlot(Slot.Mustache, dna.mustache, 0, dna.hairColorIdx);

            // 5. Apply skin color to body groups
            ApplySkinColor(dna.skinColorIdx);

            // 6. Apply jewelry
            ApplyJewelry(dna.jewelry, dna.gender);

            // 7. Apply tattoos
            ApplyTattoos(dna.tattoos);

            // 8. Apply layer-to-layer hiding (AFTER all clothing is equipped)
            ApplyClothingLayerHiding();

            Debug.Log($"DnaApplier: Applied DNA for '{dna.name}' ({dna.gender}, {dna.bodyShape})");
        }

        /// <summary>Get currently applied DNA</summary>
        public PirateDNA GetCurrentDNA() => currentDna;

        /// <summary>Get renderer cache for advanced manipulation</summary>
        public GroupRendererCache GetRendererCache() => rendererCache;

        /// <summary>Get material binder for advanced manipulation</summary>
        public MaterialBinder GetMaterialBinder() => materialBinder;

        /// <summary>Get character assembler for advanced manipulation</summary>
        public CharacterAssembler GetAssembler() => assembler;

        /// <summary>Get body shape applier for advanced manipulation</summary>
        public BodyShapeApplier GetBodyShapeApplier() => bodyShapeApplier;

        private void HideAllClothing()
        {
            // STEP 1: Explicitly disable ALL clothing meshes (including orphaned ones not in any variant)
            int clothingDisabled = 0;
            foreach (var name in rendererCache.AllNames())
            {
                if (name.StartsWith("clothing_layer"))
                {
                    rendererCache.EnableExact(name, false);
                    clothingDisabled++;
                }
            }
            Debug.Log($"DnaApplier: Disabled {clothingDisabled} clothing meshes");

            // STEP 2: Clear all slots (this will disable all clothing groups via CharacterAssembler)
            assembler.ClearAllSlots();

            // STEP 3: Show all body parts (will be hidden later based on equipped clothing)
            foreach (var name in ClothingCatalog.BodyIndexToGroup)
            {
                rendererCache.EnableExact(name, true);
            }

            Debug.Log("DnaApplier: Cleared all clothing slots, showed all body parts");
        }

        private void ApplyClothingSlot(Slot slot, int ogIndex, int textureIdx, int colorIdx)
        {
            if (ogIndex < 0)
                return;

            Color? dye = null;

            if (colorIdx >= 0)
            {
                dye = palettes.GetDyeColor(colorIdx);
            }

            assembler.SetSlotByIndex(slot, ogIndex, textureIdx, dye);

            // Hide body parts covered by clothing (or show if no covering)
            // Note: Body is shown by default in HideAllClothing()
            // TODO: Replace with proper hide/show groups from clothing variants
        }

        private void ApplyClothingLayerHiding()
        {
            var vestVariant = assembler.GetCurrentVariant(Slot.Vest);
            var coatVariant = assembler.GetCurrentVariant(Slot.Coat);
            var beltVariant = assembler.GetCurrentVariant(Slot.Belt);
            var shirtVariant = assembler.GetCurrentVariant(Slot.Shirt);
            var pantVariant = assembler.GetCurrentVariant(Slot.Pant);

            // When vest is equipped: hide shirt parts based on vest type
            if (vestVariant != null && vestVariant.ogIndex > 0 && shirtVariant != null && shirtVariant.showGroups.Count > 0)
            {
                int hiddenCount = 0;
                foreach (var groupName in shirtVariant.showGroups)
                {
                    var renderers = rendererCache.GetExact(groupName);
                    foreach (var r in renderers)
                    {
                        if (r == null) continue;
                        var name = r.gameObject.name;

                        // If vest ogIndex > 1: hide shirt parts with 'base', 'front', 'collar_v_low'
                        // Else: hide shirt parts with 'base'
                        if (vestVariant.ogIndex > 1)
                        {
                            if (name.Contains("base") || name.Contains("front") || name.Contains("collar_v_low"))
                            {
                                r.enabled = false;
                                hiddenCount++;
                            }
                        }
                        else
                        {
                            if (name.Contains("base"))
                            {
                                r.enabled = false;
                                hiddenCount++;
                            }
                        }
                    }
                }
                if (hiddenCount > 0)
                {
                    Debug.Log($"DnaApplier: Vest hid {hiddenCount} shirt submeshes");
                }
            }

            // When belt is equipped: hide pant parts with 'belt'
            if (beltVariant != null && beltVariant.ogIndex > 0 && pantVariant != null && pantVariant.showGroups.Count > 0)
            {
                int hiddenCount = 0;
                foreach (var groupName in pantVariant.showGroups)
                {
                    var renderers = rendererCache.GetExact(groupName);
                    foreach (var r in renderers)
                    {
                        if (r == null) continue;
                        var name = r.gameObject.name;

                        if (name.Contains("belt"))
                        {
                            r.enabled = false;
                            hiddenCount++;
                        }
                    }
                }
                if (hiddenCount > 0)
                {
                    Debug.Log($"DnaApplier: Belt hid {hiddenCount} pant submeshes");
                }
            }

            // When belt AND long vest (ogIndex == 3): hide vest parts with 'belt'
            if (beltVariant != null && beltVariant.ogIndex > 0 && vestVariant != null && vestVariant.ogIndex == 3 && vestVariant.showGroups.Count > 0)
            {
                int hiddenCount = 0;
                foreach (var groupName in vestVariant.showGroups)
                {
                    var renderers = rendererCache.GetExact(groupName);
                    foreach (var r in renderers)
                    {
                        if (r == null) continue;
                        var name = r.gameObject.name;

                        if (name.Contains("belt"))
                        {
                            r.enabled = false;
                            hiddenCount++;
                        }
                    }
                }
                if (hiddenCount > 0)
                {
                    Debug.Log($"DnaApplier: Belt hid {hiddenCount} vest submeshes");
                }
            }

            // When coat is equipped: hide shirt/vest parts that don't contain 'front' or 'collar'
            if (coatVariant != null && coatVariant.ogIndex > 0)
            {
                // Hide shirt parts that don't contain 'front' or 'collar'
                if (shirtVariant != null && shirtVariant.showGroups.Count > 0)
                {
                    int hiddenCount = 0;
                    foreach (var groupName in shirtVariant.showGroups)
                    {
                        var renderers = rendererCache.GetExact(groupName);
                        foreach (var r in renderers)
                        {
                            if (r == null) continue;
                            var name = r.gameObject.name;

                            // Hide if doesn't contain 'front' AND doesn't contain 'collar'
                            if (!name.Contains("front") && !name.Contains("collar"))
                            {
                                r.enabled = false;
                                hiddenCount++;
                            }
                        }
                    }
                    if (hiddenCount > 0)
                    {
                        Debug.Log($"DnaApplier: Coat hid {hiddenCount} shirt submeshes");
                    }
                }

                // Hide vest parts that don't contain 'front'
                if (vestVariant != null && vestVariant.showGroups.Count > 0)
                {
                    int hiddenCount = 0;
                    foreach (var groupName in vestVariant.showGroups)
                    {
                        var renderers = rendererCache.GetExact(groupName);
                        foreach (var r in renderers)
                        {
                            if (r == null) continue;
                            var name = r.gameObject.name;

                            if (!name.Contains("front"))
                            {
                                r.enabled = false;
                                hiddenCount++;
                            }
                        }
                    }
                    if (hiddenCount > 0)
                    {
                        Debug.Log($"DnaApplier: Coat hid {hiddenCount} vest submeshes");
                    }
                }
            }
        }

        private void ApplySkinColor(int skinColorIdx)
        {
            Color skinColor = palettes.GetSkinColor(skinColorIdx);

            // Apply to all body part groups using exact names
            foreach (var bodyPartName in ClothingCatalog.BodyIndexToGroup)
            {
                var renderers = rendererCache.GetExact(bodyPartName);
                foreach (var renderer in renderers)
                {
                    materialBinder.ApplyDye(renderer, "base", skinColor);
                }
            }
        }

        private void ApplyJewelry(Dictionary<string, int> jewelry, string gender)
        {
            foreach (var kvp in jewelry)
            {
                string zone = kvp.Key;
                int index = kvp.Value;

                var jewelryGroups = jewelryTattoos.GetJewelryGroups(zone, gender);

                if (index >= 0 && index < jewelryGroups.Count)
                {
                    string groupName = jewelryGroups[index];

                    // Enable the jewelry group (exact name)
                    rendererCache.EnableExact(groupName, true);

                    Debug.Log($"DnaApplier: Enabled jewelry '{groupName}' in zone '{zone}'");
                }
            }
        }

        private void ApplyTattoos(List<TattooSpec> tattoos)
        {
            foreach (var tattoo in tattoos)
            {
                string zone = $"zone{tattoo.zone}";
                var bodyGroups = jewelryTattoos.GetTattooBodyGroups(zone);

                foreach (var groupName in bodyGroups)
                {
                    var renderers = rendererCache.GetExact(groupName);

                    foreach (var renderer in renderers)
                    {
                        // Apply tattoo as texture overlay with UV transform
                        // This is simplified - full implementation would use shader properties
                        // for UV offset (tattoo.u, tattoo.v), scale (tattoo.scale), rotation (tattoo.rotation)

                        Color tattooColor = palettes.GetDyeColor(tattoo.colorIdx);
                        materialBinder.ApplyDye(renderer, "tattoo", tattooColor);

                        // TODO: Apply UV transform via MaterialPropertyBlock
                        // block.SetFloat("_TattooU", tattoo.u);
                        // block.SetFloat("_TattooV", tattoo.v);
                        // block.SetFloat("_TattooScale", tattoo.scale);
                        // block.SetFloat("_TattooRotation", tattoo.rotation);
                    }
                }

                Debug.Log($"DnaApplier: Applied tattoo {tattoo.idx} to zone{tattoo.zone}");
            }
        }

        /// <summary>Get diagnostic info</summary>
        public string GetDiagnosticInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== DnaApplier Diagnostics ===");
            sb.AppendLine();
            sb.AppendLine(rendererCache.GetDiagnosticInfo());
            sb.AppendLine();
            sb.AppendLine(materialBinder.GetDiagnosticInfo());
            sb.AppendLine();
            sb.AppendLine(assembler.GetDiagnosticInfo());
            sb.AppendLine();
            sb.AppendLine(bodyShapeApplier.GetDiagnosticInfo());

            if (currentDna != null)
            {
                sb.AppendLine();
                sb.AppendLine($"Current DNA: {currentDna.name} ({currentDna.gender})");
                sb.AppendLine($"  Body: {currentDna.bodyShape} (height: {currentDna.bodyHeight})");
                sb.AppendLine($"  Clothing: Hat={currentDna.hat}, Shirt={currentDna.shirt}, Pants={currentDna.pants}");
                sb.AppendLine($"  Colors: Top={currentDna.topColorIdx}, Bot={currentDna.botColorIdx}, Hat={currentDna.hatColorIdx}");
                sb.AppendLine($"  Jewelry: {currentDna.jewelry.Count} zones");
                sb.AppendLine($"  Tattoos: {currentDna.tattoos.Count}");
            }

            return sb.ToString();
        }
    }
}
