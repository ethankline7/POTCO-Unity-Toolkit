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
            string gender,
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

            assembler = new CharacterAssembler(rendererCache, materialBinder, catalog, palettes, gender);
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
            // STEP 1: Explicitly disable ALL clothing, hair, beard, mustache, and jewelry meshes (except eyebrows)
            int clothingDisabled = 0;
            int hairDisabled = 0;
            int beardDisabled = 0;
            int mustacheDisabled = 0;
            int jewelryDisabled = 0;

            foreach (var name in rendererCache.AllNames())
            {
                if (name.StartsWith("clothing_layer"))
                {
                    rendererCache.EnableExact(name, false);
                    clothingDisabled++;
                }
                else if (name.StartsWith("hair_") && !name.StartsWith("hair_eyebrow_"))
                {
                    rendererCache.EnableExact(name, false);
                    hairDisabled++;
                }
                else if (name.StartsWith("beard_"))
                {
                    rendererCache.EnableExact(name, false);
                    beardDisabled++;
                }
                else if (name.StartsWith("mustache_"))
                {
                    rendererCache.EnableExact(name, false);
                    mustacheDisabled++;
                }
                else if (name.StartsWith("acc_"))
                {
                    rendererCache.EnableExact(name, false);
                    jewelryDisabled++;
                }
            }

            Debug.Log($"DnaApplier: Disabled {clothingDisabled} clothing, {hairDisabled} hair, {beardDisabled} beard, {mustacheDisabled} mustache, {jewelryDisabled} jewelry meshes");

            // STEP 2: Clear all slots (this will disable all clothing groups via CharacterAssembler)
            assembler.ClearAllSlots();

            // STEP 3: Show all body parts (will be hidden later based on equipped clothing)
            var bodyParts = ClothingCatalog.GetBodyIndexToGroup(currentDna.gender);
            foreach (var name in bodyParts)
            {
                rendererCache.EnableExact(name, true);
            }

            // STEP 4: Enable eyebrows (PirateMale.py line 1852: self.eyeBrows.unstash())
            rendererCache.EnableExact("hair_eyebrow_left", true);
            rendererCache.EnableExact("hair_eyebrow_right", true);

            // STEP 5: Hide gh_master_face (zombie PVP face)
            rendererCache.EnableExact("gh_master_face", false);

            Debug.Log("DnaApplier: Cleared all clothing slots, showed all body parts, enabled eyebrows, hid zombie face");
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
            if (currentDna.gender.ToLower() == "f")
            {
                ApplyFemaleClothingHiding();
            }
            else
            {
                ApplyMaleClothingHiding();
            }
        }

        private void ApplyMaleClothingHiding()
        {
            var vestVariant = assembler.GetCurrentVariant(Slot.Vest);
            var coatVariant = assembler.GetCurrentVariant(Slot.Coat);
            var beltVariant = assembler.GetCurrentVariant(Slot.Belt);
            var shirtVariant = assembler.GetCurrentVariant(Slot.Shirt);
            var pantVariant = assembler.GetCurrentVariant(Slot.Pant);

            // Vest: hide shirt parts (PirateMale.py lines 3692-3695)
            if (vestVariant != null && vestVariant.ogIndex > 0 && shirtVariant != null)
            {
                if (vestVariant.ogIndex > 1)
                {
                    HideRenderersByTokens(shirtVariant, new[] { "base", "front", "collar_v_low" });
                }
                else
                {
                    HideRenderersByTokens(shirtVariant, new[] { "base" });
                }
            }

            // Belt: hide pant parts (PirateMale.py line 3711)
            if (beltVariant != null && beltVariant.ogIndex > 0 && pantVariant != null)
            {
                HideRenderersByTokens(pantVariant, new[] { "belt" });
            }

            // Belt + long vest: hide vest parts (PirateMale.py line 3714)
            if (beltVariant != null && beltVariant.ogIndex > 0 && vestVariant != null && vestVariant.ogIndex == 3)
            {
                HideRenderersByTokens(vestVariant, new[] { "belt" });
            }

            // Coat: hide shirt/vest parts (PirateMale.py lines 3730-3732)
            if (coatVariant != null && coatVariant.ogIndex > 0)
            {
                // Hide shirt parts that DON'T contain 'front' or 'collar'
                if (shirtVariant != null)
                {
                    HideRenderersNotContaining(shirtVariant, new[] { "front", "collar" });
                }

                // Hide vest parts that DON'T contain 'front'
                if (vestVariant != null)
                {
                    HideRenderersNotContaining(vestVariant, new[] { "front" });
                }
            }
        }

        private void ApplyFemaleClothingHiding()
        {
            var vestVariant = assembler.GetCurrentVariant(Slot.Vest);
            var coatVariant = assembler.GetCurrentVariant(Slot.Coat);
            var beltVariant = assembler.GetCurrentVariant(Slot.Belt);
            var shirtVariant = assembler.GetCurrentVariant(Slot.Shirt);
            var pantVariant = assembler.GetCurrentVariant(Slot.Pant);

            // Vest hiding (PirateFemale.py lines 3562-3576)
            if (vestVariant != null && vestVariant.ogIndex > 0)
            {
                if (vestVariant.ogIndex == 1)
                {
                    // handleLayer2Hiding(clothingsLayer1, layerShirt, 'base', 'low_vcut', 'front')
                    if (shirtVariant != null)
                    {
                        HideRenderersByTokens(shirtVariant, new[] { "base", "low_vcut", "front" });
                        // handleLayer2Hiding(clothingsLayer1, layerShirt, 'breast', 'belt', 'waist')
                        HideRenderersByTokens(shirtVariant, new[] { "breast", "belt", "waist" });
                    }
                    // handleLayer2Hiding(clothingsLayer1, layerPant, 'belt')
                    if (pantVariant != null)
                    {
                        HideRenderersByTokens(pantVariant, new[] { "belt" });
                    }
                }
                else if (vestVariant.ogIndex == 2)
                {
                    // handleLayer2Hiding(clothingsLayer1, layerShirt, 'base', 'front', 'waist')
                    if (shirtVariant != null)
                    {
                        HideRenderersByTokens(shirtVariant, new[] { "base", "front", "waist" });
                        // handleLayer2Hiding(clothingsLayer1, layerShirt, 'belt')
                        HideRenderersByTokens(shirtVariant, new[] { "belt" });
                    }
                    if (pantVariant != null)
                    {
                        // handleLayer2Hiding(clothingsLayer1, layerPant, 'belt', '_abs')
                        HideRenderersByTokens(pantVariant, new[] { "belt", "_abs" });
                        // Hide vest bottom based on pant type
                        if (pantVariant.ogIndex == 2)
                        {
                            // handleLayer2Hiding(clothingsLayer2, layerVest, 'bottom_pant')
                            HideRenderersByTokens(vestVariant, new[] { "bottom_pant" });
                        }
                        else
                        {
                            // handleLayer2Hiding(clothingsLayer2, layerVest, 'bottom_skirt')
                            HideRenderersByTokens(vestVariant, new[] { "bottom_skirt" });
                        }
                    }
                }
                else
                {
                    // handleLayer2Hiding(clothingsLayer1, layerPant, 'belt')
                    if (pantVariant != null)
                    {
                        HideRenderersByTokens(pantVariant, new[] { "belt" });
                    }
                }
            }

            // Belt hiding (PirateFemale.py lines 3590-3593)
            if (beltVariant != null && beltVariant.ogIndex > 0)
            {
                // handleLayer2Hiding(clothingsLayer1, layerShirt, 'belt')
                if (shirtVariant != null) HideRenderersByTokens(shirtVariant, new[] { "belt" });
                // handleLayer2Hiding(clothingsLayer1, layerPant, 'belt')
                if (pantVariant != null) HideRenderersByTokens(pantVariant, new[] { "belt" });
                // handleLayer2Hiding(clothingsLayer2, layerVest, 'belt')
                if (vestVariant != null) HideRenderersByTokens(vestVariant, new[] { "belt" });
            }

            // Coat hiding (PirateFemale.py lines 3606-3628)
            if (coatVariant != null && coatVariant.ogIndex > 0)
            {
                if (coatVariant.ogIndex == 3)
                {
                    // handleLayer3Hiding(clothingsLayer2, layerVest, layerShirt, True) - hideAll=True
                    if (vestVariant != null) HideRenderersNotContaining(vestVariant, new string[0]);
                    if (shirtVariant != null) HideRenderersNotContaining(shirtVariant, new string[0]);
                    // handleLayer3Hiding(clothingsLayer2, layerBelt, None, True) - hideAll=True
                    if (beltVariant != null) HideRenderersNotContaining(beltVariant, new string[0]);
                    // handleLayer2Hiding(clothingsLayer1, layerPant, 'abs_interior', 'abs', 'belt_interior', 'side')
                    if (pantVariant != null)
                        HideRenderersByTokens(pantVariant, new[] { "abs_interior", "abs", "belt_interior", "side" });
                }
                else if (coatVariant.ogIndex == 4)
                {
                    // handleLayer3Hiding(clothingsLayer2, layerVest, layerShirt, True) - hideAll=True
                    if (vestVariant != null) HideRenderersNotContaining(vestVariant, new string[0]);
                    if (shirtVariant != null) HideRenderersNotContaining(shirtVariant, new string[0]);
                    // handleLayer3Hiding(clothingsLayer2, layerBelt, None, True) - hideAll=True
                    if (beltVariant != null) HideRenderersNotContaining(beltVariant, new string[0]);
                    // handleLayer2Hiding(clothingsLayer1, layerPant, 'abs_interior', 'longcoat_interior', 'abs', 'side')
                    if (pantVariant != null)
                        HideRenderersByTokens(pantVariant, new[] { "abs_interior", "longcoat_interior", "abs", "side" });
                }
                else
                {
                    // handleLayer3Hiding(clothingsLayer2, layerVest, layerShirt) - hide parts without 'front' OR 'vcut'
                    if (vestVariant != null) HideRenderersNotContaining(vestVariant, new[] { "front" });
                    if (shirtVariant != null) HideRenderersNotContaining(shirtVariant, new[] { "front", "vcut" });
                    // handleLayer2Hiding(clothingsLayer2, layerBelt, '_cloth', 'interior')
                    if (beltVariant != null) HideRenderersByTokens(beltVariant, new[] { "_cloth", "interior" });

                    // Additional pant hiding based on pant type and coat type (lines 3619-3628)
                    if (pantVariant != null && pantVariant.ogIndex == 2)
                    {
                        if (coatVariant.ogIndex == 1)
                        {
                            // handleLayer2Hiding(clothingsLayer1, layerPant, 'side', 'longcoat', 'tails')
                            HideRenderersByTokens(pantVariant, new[] { "side", "longcoat", "tails" });
                        }
                        else if (coatVariant.ogIndex == 2)
                        {
                            // handleLayer2Hiding(clothingsLayer1, layerPant, 'side', 'interior', 'back')
                            HideRenderersByTokens(pantVariant, new[] { "side", "interior", "back" });
                        }
                    }
                    else if (pantVariant != null)
                    {
                        // handleLayer2Hiding(clothingsLayer1, layerPant, 'interior')
                        HideRenderersByTokens(pantVariant, new[] { "interior" });
                    }
                }
            }
        }

        private void HideRenderersByTokens(SlotVariant variant, string[] tokens)
        {
            if (variant == null || variant.showGroups.Count == 0) return;

            int hiddenCount = 0;
            foreach (var groupName in variant.showGroups)
            {
                var renderers = rendererCache.GetExact(groupName);
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    var name = r.gameObject.name;

                    foreach (var token in tokens)
                    {
                        if (name.Contains(token))
                        {
                            r.enabled = false;
                            hiddenCount++;
                            break;
                        }
                    }
                }
            }
            if (hiddenCount > 0) Debug.Log($"DnaApplier: Hid {hiddenCount} '{variant.displayName}' submeshes by tokens: {string.Join(", ", tokens)}");
        }

        private void HideRenderersNotContaining(SlotVariant variant, string[] allowTokens)
        {
            if (variant == null || variant.showGroups.Count == 0) return;

            int hiddenCount = 0;
            foreach (var groupName in variant.showGroups)
            {
                var renderers = rendererCache.GetExact(groupName);
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    var name = r.gameObject.name;

                    // If no allow tokens, hide everything
                    if (allowTokens.Length == 0)
                    {
                        r.enabled = false;
                        hiddenCount++;
                        continue;
                    }

                    // Hide if doesn't contain ANY of the allow tokens
                    bool hasAllowToken = false;
                    foreach (var token in allowTokens)
                    {
                        if (name.Contains(token))
                        {
                            hasAllowToken = true;
                            break;
                        }
                    }

                    if (!hasAllowToken)
                    {
                        r.enabled = false;
                        hiddenCount++;
                    }
                }
            }
            if (hiddenCount > 0) Debug.Log($"DnaApplier: Hid {hiddenCount} '{variant.displayName}' submeshes (not containing: {string.Join(" or ", allowTokens)})");
        }

        private void ApplySkinColor(int skinColorIdx)
        {
            Color skinColor = palettes.GetSkinColor(skinColorIdx);

            // Apply to all body part groups using exact names (gender-specific)
            var bodyParts = ClothingCatalog.GetBodyIndexToGroup(currentDna.gender);
            foreach (var bodyPartName in bodyParts)
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
