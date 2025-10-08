/// <summary>
/// High-level DNA application system.
/// Ties together BodyShapeApplier, CharacterAssembler, and other systems
/// to apply a complete PirateDNA to a character model.
/// </summary>
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CharacterOG.Models;
using CharacterOG.Runtime.Utils;

namespace CharacterOG.Runtime.Systems
{
    public class DnaApplier
    {
        private BodyShapeApplier bodyShapeApplier;
        private FacialMorphApplier facialMorphApplier;
        private CharacterAssembler assembler;
        private GroupRendererCache rendererCache;
        private MaterialBinder materialBinder;

        private Dictionary<string, BodyShapeDef> bodyShapes;
        private ClothingCatalog catalog;
        private Palettes palettes;
        private JewelryTattooDefs jewelryTattoos;
        private FacialMorphDatabase facialMorphs;

        private PirateDNA currentDna;
        private Color? currentHairColor;

        public DnaApplier(
            GameObject characterRoot,
            Dictionary<string, BodyShapeDef> bodyShapes,
            ClothingCatalog catalog,
            Palettes palettes,
            JewelryTattooDefs jewelryTattoos,
            FacialMorphDatabase facialMorphs,
            string gender,
            Transform headRoot = null,
            Transform bodyRoot = null)
        {
            this.bodyShapes = bodyShapes;
            this.catalog = catalog;
            this.palettes = palettes;
            this.jewelryTattoos = jewelryTattoos;
            this.facialMorphs = facialMorphs;

            // Initialize systems
            rendererCache = new GroupRendererCache(characterRoot);
            materialBinder = new MaterialBinder();

            // CRITICAL: Resolve OG patterns to exact mesh group names using the character's actual mesh
            catalog.ResolvePatterns(rendererCache);

            assembler = new CharacterAssembler(rendererCache, materialBinder, catalog, palettes, gender);
            bodyShapeApplier = new BodyShapeApplier(characterRoot.transform, headRoot, bodyRoot);

            // Initialize facial morph applier if facial morphs database is provided
            if (facialMorphs != null && headRoot != null)
            {
                Debug.Log($"[DnaApplier] Initializing FacialMorphApplier with headRoot '{headRoot.name}', rigRoot '{characterRoot.transform.name}' and {facialMorphs.morphs.Count} morphs");
                facialMorphApplier = new FacialMorphApplier(headRoot, facialMorphs, characterRoot.transform);
            }
            else
            {
                if (facialMorphs == null)
                    Debug.LogWarning("[DnaApplier] FacialMorphApplier NOT initialized - facialMorphs is null");
                else if (facialMorphs.morphs.Count == 0)
                    Debug.LogWarning("[DnaApplier] FacialMorphApplier NOT initialized - facialMorphs database is empty");
                else if (headRoot == null)
                    Debug.LogWarning("[DnaApplier] FacialMorphApplier NOT initialized - headRoot is null");
            }
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
            Debug.Log($"[DnaApplier] Attempting to apply body shape '{dna.bodyShape}' (available shapes: {bodyShapes.Count})");
            if (bodyShapes.TryGetValue(dna.bodyShape, out var shape))
            {
                Debug.Log($"[DnaApplier] Found body shape '{dna.bodyShape}', applying to character...");
                bodyShapeApplier.ApplyBodyShape(shape);
                bodyShapeApplier.ApplyHeightBias(dna.bodyHeight);
            }
            else
            {
                Debug.LogWarning($"DnaApplier: Body shape '{dna.bodyShape}' not found in available shapes: {string.Join(", ", bodyShapes.Keys)}");
            }

            // 2. Apply underwear defaults first
            assembler.ApplyUnderwear(dna.gender);

            // 3. Apply clothing slots
            // Note: Index 0 typically means "underwear/default" so we use > 0 check
            // Hat is exception - index 0 means "no hat" which is fine
            ApplyClothingSlot(Slot.Hat, dna.hat, dna.hatTex, dna.hatColorIdx);

            // Male hat 7 (tricorn) has bald/non-bald variants (hardcoded from PirateMale.py lines 1611-1626)
            // If hair is 0, 9, 10, or 13 (bald or short), show bald hat parts, hide non-bald parts
            // Otherwise hide bald parts
            if (dna.gender == "m" && dna.hat == 7)
            {
                bool useBaldVariant = (dna.hair == 0 || dna.hair == 9 || dna.hair == 10 || dna.hair == 13);
                ApplyHatBaldVariant(useBaldVariant);
            }

            // Only override underwear if index > 0 (0 means keep underwear default)
            Debug.Log($"[DNA Values] shirt={dna.shirt}, vest={dna.vest}, coat={dna.coat}, pants={dna.pants}");

            if (dna.shirt > 0)
                ApplyClothingSlot(Slot.Shirt, dna.shirt, dna.shirtTex, dna.topColorIdx);

            if (dna.vest > 0)
                ApplyClothingSlot(Slot.Vest, dna.vest, dna.vestTex, dna.topColorIdx);

            if (dna.coat > 0)
                ApplyClothingSlot(Slot.Coat, dna.coat, dna.coatTex, dna.topColorIdx);

            if (dna.belt > 0)
                ApplyClothingSlot(Slot.Belt, dna.belt, dna.beltTex, -1);

            if (dna.pants > 0)
                ApplyClothingSlot(Slot.Pant, dna.pants, dna.pantsTex, dna.botColorIdx);

            // Male pants 4 (EITC) and 5 (Navy) hide shoes (hardcoded from PirateMale.py line 3513)
            bool hideShoes = false;
            if (dna.gender == "m" && (dna.pants == 4 || dna.pants == 5))
            {
                hideShoes = true;
                Debug.Log($"DnaApplier: Hiding shoes because male pants {dna.pants} (EITC/Navy) hide shoes (hardcoded from POTCO source)");
            }

            if (dna.shoes > 0 && !hideShoes)
                ApplyClothingSlot(Slot.Shoe, dna.shoes, dna.shoesTex, dna.botColorIdx);

            // 4. Apply hair/facial hair (with hair color palette, not dye palette)
            ApplyHairSlot(Slot.Hair, dna.hair, dna.hairColorIdx);
            ApplyHairSlot(Slot.Beard, dna.beard, dna.hairColorIdx);

            // Male beards 1, 2, 3 hide mustache (hardcoded from PirateMale.py lines 1588, 1642)
            // "if not currentBeardIdx > 0 and currentBeardIdx < 4: currentStache.unstash()"
            // Mustache only shows when beard is NOT 1, 2, or 3
            bool hideMustache = false;
            if (dna.gender == "m" && dna.beard >= 1 && dna.beard <= 3)
            {
                hideMustache = true;
                Debug.Log($"DnaApplier: Hiding mustache because male beard {dna.beard} hides mustache (hardcoded from POTCO source)");
            }

            if (!hideMustache)
                ApplyHairSlot(Slot.Mustache, dna.mustache, dna.hairColorIdx);

            // 4.5. Apply hair cuts for hat (PirateMale.py handleHeadHiding logic)
            // This shows cut versions of hair and hides full versions based on hat type
            ApplyHairCutsForHat(dna.hat);

            // 5. Apply skin color to body groups
            ApplySkinColor(dna.skinColorIdx);

            // 5.5. Apply face and iris textures
            ApplyFaceTexture(dna.headTexture);
            ApplyIrisTexture(dna.eyeColorIdx);

            // 6. Apply jewelry
            ApplyJewelry(dna.jewelry, dna.gender);

            // 7. Apply tattoos
            ApplyTattoos(dna.tattoos);

            // 8. Apply layer-to-layer hiding (AFTER all clothing is equipped)
            ApplyClothingLayerHiding();

            // 9. Apply facial morphs (head customization)
            if (dna.headMorphs != null && dna.headMorphs.Count > 0)
            {
                Debug.Log($"[DnaApplier] NPC '{dna.name}' has {dna.headMorphs.Count} facial morph values");

                // Log first few morphs
                var sampleMorphs = dna.headMorphs.Take(5).Select(kvp => $"{kvp.Key}={kvp.Value:F3}").ToList();
                Debug.Log($"[DnaApplier] Sample morph values: {string.Join(", ", sampleMorphs)}");

                if (facialMorphApplier != null && facialMorphs != null && facialMorphs.morphs.Count > 0)
                {
                    Debug.Log($"[DnaApplier] Applying facial morphs...");
                    facialMorphApplier.ApplyMorphs(dna.headMorphs);
                    Debug.Log($"[DnaApplier] Facial morphs applied successfully");
                }
                else
                {
                    Debug.LogWarning($"[DnaApplier] Cannot apply {dna.headMorphs.Count} facial morphs:");
                    if (facialMorphApplier == null)
                        Debug.LogWarning("  - facialMorphApplier is NULL");
                    if (facialMorphs == null)
                        Debug.LogWarning("  - facialMorphs database is NULL");
                    else if (facialMorphs.morphs.Count == 0)
                        Debug.LogWarning("  - facialMorphs database is EMPTY");
                }
            }

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

        /// <summary>Get facial morph applier for advanced manipulation</summary>
        public FacialMorphApplier GetFacialMorphApplier() => facialMorphApplier;

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
            var leftEyebrowRenderers = rendererCache.GetExact("hair_eyebrow_left");
            var rightEyebrowRenderers = rendererCache.GetExact("hair_eyebrow_right");

            Debug.Log($"[DnaApplier] Eyebrow check: Found {leftEyebrowRenderers.Count} left and {rightEyebrowRenderers.Count} right eyebrow renderers");

            // Check for any eyebrow-related meshes in the cache
            var allNames = rendererCache.AllNames();
            var eyebrowNames = allNames.Where(n => n.ToLower().Contains("eyebrow") || n.ToLower().Contains("brow")).ToList();
            if (eyebrowNames.Count > 0)
            {
                Debug.Log($"[DnaApplier] Found {eyebrowNames.Count} eyebrow-related meshes: {string.Join(", ", eyebrowNames)}");
            }
            else
            {
                Debug.LogWarning("[DnaApplier] No eyebrow meshes found in character model!");
            }

            rendererCache.EnableExact("hair_eyebrow_left", true);
            rendererCache.EnableExact("hair_eyebrow_right", true);

            // STEP 5: Hide gh_master_face (zombie PVP face)
            rendererCache.EnableExact("gh_master_face", false);

            Debug.Log("DnaApplier: Cleared all clothing slots, showed all body parts, enabled eyebrows, hid zombie face");
        }

        private void ApplyClothingSlot(Slot slot, int ogIndex, int textureIdx, int colorIdx)
        {
            Debug.Log($"[ApplyClothingSlot] {slot}: ogIndex={ogIndex}, textureIdx={textureIdx}, colorIdx={colorIdx}");

            if (ogIndex < 0)
                return;

            // Check if variant exists in catalog before applying
            var variant = catalog.GetVariant(slot, ogIndex);
            if (variant == null)
            {
                Debug.LogWarning($"DnaApplier: {slot} variant at index {ogIndex} not found in catalog, skipping");
                return;
            }

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

        private void ApplyHairSlot(Slot slot, int ogIndex, int hairColorIdx)
        {
            if (ogIndex < 0)
                return;

            Color? hairColor = null;

            if (hairColorIdx >= 0)
            {
                hairColor = palettes.GetHairColor(hairColorIdx);
            }

            // Store hair color for later use when applying hair cuts
            if (slot == Slot.Hair)
            {
                currentHairColor = hairColor;
            }

            // Try to apply from catalog, but don't fail if variant not found
            // Hair meshes might exist in model even if catalog doesn't list them
            var variant = catalog.GetVariant(slot, ogIndex);
            if (variant == null)
            {
                Debug.Log($"DnaApplier: {slot} variant at index {ogIndex} not in catalog, attempting direct mesh activation");

                // Try to enable hair meshes directly by pattern matching
                // For hair index N, enable all hair_* meshes that match the hair pieces for that index
                // This is a fallback when catalog doesn't have the variant
                ApplyHairDirectly(slot, ogIndex, hairColor);
                return;
            }

            assembler.SetSlotByIndex(slot, ogIndex, 0, hairColor);

            // CRITICAL: Also apply color to ALL hair/beard/mustache meshes (including cut versions)
            // The assembler only colors the specific variant's showGroups, but cut versions
            // are separate meshes that need coloring too
            if (hairColor.HasValue)
            {
                int extraColoredCount = 0;
                foreach (var name in rendererCache.AllNames())
                {
                    bool matchesSlot = false;
                    if (slot == Slot.Hair && name.StartsWith("hair_") && !name.StartsWith("hair_eyebrow_"))
                        matchesSlot = true;
                    else if (slot == Slot.Beard && name.StartsWith("beard_"))
                        matchesSlot = true;
                    else if (slot == Slot.Mustache && name.StartsWith("mustache_"))
                        matchesSlot = true;

                    if (matchesSlot)
                    {
                        var renderers = rendererCache.GetExact(name);
                        foreach (var renderer in renderers)
                        {
                            materialBinder.ApplyDye(renderer, "base", hairColor.Value);
                            extraColoredCount++;
                        }
                    }
                }

                Debug.Log($"DnaApplier: Applied {slot} color to {extraColoredCount} total meshes (including cut versions)");
            }

            // Apply hair color to eyebrows (only when applying hair slot, not beard/mustache)
            if (slot == Slot.Hair)
            {
                var leftEyebrow = rendererCache.GetExact("hair_eyebrow_left");
                var rightEyebrow = rendererCache.GetExact("hair_eyebrow_right");

                // Use hair color if available, otherwise use a default dark brown
                Color eyebrowColor = hairColor ?? new Color(0.2f, 0.1f, 0.05f); // Dark brown default

                foreach (var renderer in leftEyebrow)
                {
                    materialBinder.ApplyDye(renderer, "base", eyebrowColor);
                }

                foreach (var renderer in rightEyebrow)
                {
                    materialBinder.ApplyDye(renderer, "base", eyebrowColor);
                }

                Debug.Log($"DnaApplier: Applied eyebrow color {eyebrowColor} to {leftEyebrow.Count + rightEyebrow.Count} eyebrow renderers (hairColor was {(hairColor.HasValue ? "set" : "null")})");
            }
        }

        private void ApplyHairDirectly(Slot slot, int ogIndex, Color? hairColor)
        {
            // Hair variant not in catalog - catalog data may be incomplete
            // Apply color to ALL hair pieces as a fallback (hat cuts will hide the right ones later)
            Debug.LogWarning($"DnaApplier: {slot} variant at index {ogIndex} not found in catalog. Applying color to all {slot} meshes as fallback.");

            int coloredCount = 0;
            foreach (var name in rendererCache.AllNames())
            {
                bool matchesSlot = false;
                if (slot == Slot.Hair && name.StartsWith("hair_") && !name.StartsWith("hair_eyebrow_"))
                    matchesSlot = true;
                else if (slot == Slot.Beard && name.StartsWith("beard_"))
                    matchesSlot = true;
                else if (slot == Slot.Mustache && name.StartsWith("mustache_"))
                    matchesSlot = true;

                if (matchesSlot && hairColor.HasValue)
                {
                    var renderers = rendererCache.GetExact(name);
                    foreach (var renderer in renderers)
                    {
                        materialBinder.ApplyDye(renderer, "base", hairColor.Value);
                        coloredCount++;
                    }
                }
            }

            Debug.Log($"DnaApplier: Applied {slot} color to {coloredCount} meshes as fallback");

            // Apply eyebrow color for hair
            if (slot == Slot.Hair)
            {
                var leftEyebrow = rendererCache.GetExact("hair_eyebrow_left");
                var rightEyebrow = rendererCache.GetExact("hair_eyebrow_right");

                // Use hair color if available, otherwise use a default dark brown
                Color eyebrowColor = hairColor ?? new Color(0.2f, 0.1f, 0.05f); // Dark brown default

                foreach (var renderer in leftEyebrow)
                {
                    materialBinder.ApplyDye(renderer, "base", eyebrowColor);
                }

                foreach (var renderer in rightEyebrow)
                {
                    materialBinder.ApplyDye(renderer, "base", eyebrowColor);
                }

                Debug.Log($"DnaApplier: Applied eyebrow color {eyebrowColor} (fallback path)");
            }
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

            Debug.Log($"[ApplyFemaleClothingHiding] vest={vestVariant?.ogIndex}, shirt={shirtVariant?.ogIndex}, coat={coatVariant?.ogIndex}");
            if (shirtVariant != null)
            {
                Debug.Log($"[ApplyFemaleClothingHiding] Shirt patterns: {string.Join(", ", shirtVariant.ogPatterns)}");
            }
            if (vestVariant != null)
            {
                Debug.Log($"[ApplyFemaleClothingHiding] Vest patterns: {string.Join(", ", vestVariant.ogPatterns)}");
            }

            // Vest hiding (PirateFemale.py lines 3533, 3562-3576)
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
                else // vest >= 3
                {
                    // PirateFemale.py line 3533: if vestIdx[0] < 3 (shirt only shown for vests 0,1,2)
                    // For vests 3+, hide ENTIRE shirt layer (including underwear)
                    // AND recompute body visibility WITHOUT the shirt's bodyHideIndices
                    if (shirtVariant != null)
                    {
                        HideRenderersNotContaining(shirtVariant, new string[0]); // Hide ALL shirt renderers
                        Debug.Log($"[ApplyFemaleClothingHiding] Vest {vestVariant.ogIndex} >= 3: hiding ALL shirt renderers");

                        // Recompute body visibility excluding the shirt slot
                        var excludeSlots = new HashSet<Slot> { Slot.Shirt };
                        assembler.RecomputeBodyVisibility(excludeSlots);
                        Debug.Log($"[ApplyFemaleClothingHiding] Recomputed body visibility excluding Shirt slot");
                    }

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

            // Also apply to head/face meshes (not in body parts list)
            string[] headMeshNames = { "body_master_face", "head", "face", "head_a", "head_b", "head_c", "head_d", "head_e" };
            foreach (var headName in headMeshNames)
            {
                var renderers = rendererCache.GetExact(headName);
                foreach (var renderer in renderers)
                {
                    materialBinder.ApplyDye(renderer, "base", skinColor);
                }
            }

            Debug.Log($"DnaApplier: Applied skin color {skinColor} (index {skinColorIdx}) to body and head");
        }

        private void ApplyFaceTexture(int headTextureIdx)
        {
            // Get gender-specific face texture
            string textureName = palettes.GetFaceTexture(headTextureIdx, currentDna.gender);

            if (string.IsNullOrEmpty(textureName))
            {
                Debug.LogWarning($"DnaApplier: No face texture found for index {headTextureIdx} (gender: {currentDna.gender})");
                return;
            }

            // Apply to face mesh (POTCO uses body_master_face plus eyelids)
            string[] faceMeshNames = { "body_master_face", "face", "head_face", "mesh_face" };

            bool applied = false;
            foreach (var faceName in faceMeshNames)
            {
                var renderers = rendererCache.GetExact(faceName);
                foreach (var renderer in renderers)
                {
                    materialBinder.ApplyTexture(renderer, textureName);
                    applied = true;
                }
            }

            if (applied)
            {
                Debug.Log($"DnaApplier: Applied {currentDna.gender} face texture '{textureName}' (index {headTextureIdx})");
            }
        }

        private void ApplyIrisTexture(int eyeColorIdx)
        {
            string textureName = palettes.GetIrisTexture(eyeColorIdx);

            if (string.IsNullOrEmpty(textureName))
            {
                Debug.LogWarning($"DnaApplier: No iris texture found for index {eyeColorIdx}");
                return;
            }

            // Apply to iris meshes (POTCO uses eye_iris_left and eye_iris_right)
            string[] irisMeshNames = { "eye_iris_left", "eye_iris_right", "eye_iris", "iris_left", "iris_right", "iris" };

            bool applied = false;
            foreach (var irisName in irisMeshNames)
            {
                var renderers = rendererCache.GetExact(irisName);
                foreach (var renderer in renderers)
                {
                    materialBinder.ApplyTexture(renderer, textureName);
                    applied = true;
                }
            }

            if (applied)
            {
                Debug.Log($"DnaApplier: Applied iris texture '{textureName}' (index {eyeColorIdx})");
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

        private void ApplyHatBaldVariant(bool useBaldVariant)
        {
            // Male hat 7 (tricorn) has separate bald/non-bald submeshes (PirateMale.py lines 1611-1626)
            // If hair is bald/short (0, 9, 10, 13), show only bald parts
            // Otherwise show only non-bald parts
            var hatVariant = assembler.GetCurrentVariant(Slot.Hat);
            if (hatVariant == null || hatVariant.showGroups.Count == 0)
                return;

            int baldHidden = 0;
            int nonBaldHidden = 0;

            foreach (var groupName in hatVariant.showGroups)
            {
                var renderers = rendererCache.GetExact(groupName);
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    var name = renderer.gameObject.name;

                    bool isBaldPart = name.Contains("bald");

                    if (useBaldVariant)
                    {
                        // Hide non-bald parts, show bald parts
                        if (!isBaldPart)
                        {
                            renderer.enabled = false;
                            nonBaldHidden++;
                        }
                    }
                    else
                    {
                        // Hide bald parts, show non-bald parts
                        if (isBaldPart)
                        {
                            renderer.enabled = false;
                            baldHidden++;
                        }
                    }
                }
            }

            if (baldHidden > 0 || nonBaldHidden > 0)
            {
                Debug.Log($"DnaApplier: Hat 7 bald variant: useBald={useBaldVariant}, hid {baldHidden} bald parts, {nonBaldHidden} non-bald parts");
            }
        }

        private void ApplyHairCutsForHat(int hatIndex)
        {
            // Hat cuts mapping - GENDER SPECIFIC!
            // Males from PirateMale.py lines 4310-4335
            // Females from PirateFemale.py lines 4442-4466
            string[] maleCuts = new[]
            {
                "cut_none",          // 0 - No hat
                "cut_captain",       // 1
                "cut_tricorn",       // 2
                "cut_navy",          // 3
                "cut_admiral",       // 4
                "cut_admiral",       // 5
                "cut_bandanna_full", // 6
                "cut_bandanna",      // 7
                "cut_beanie",        // 8
                "cut_admiral",       // 9
                "cut_bandanna_full", // 10
                "cut_bandanna_full", // 11
                "cut_bandanna_full", // 12
                "cut_bandanna_full", // 13
                "cut_bandanna_full", // 14
                "cut_bandanna_full", // 15
                "cut_bandanna_full", // 16
                "cut_bandanna_full", // 17
                "cut_bandanna_full", // 18
                "cut_bandanna_full"  // 19
            };

            string[] femaleCuts = new[]
            {
                "",       // 0 - No hat
                "cut_c",  // 1
                "cut_c",  // 2
                "cut_c",  // 3
                "cut_e",  // 4
                "cut_c",  // 5
                "cut_d",  // 6 - bandanna_reg (with cut_c fallback)
                "cut_c",  // 7
                "cut_c",  // 8
                "cut_c",  // 9
                "cut_c",  // 10
                "cut_c",  // 11
                "cut_c",  // 12
                "cut_c",  // 13
                "cut_c",  // 14
                "cut_c",  // 15
                "cut_c",  // 16
                "cut_c",  // 17
                "cut_c",  // 18
                "cut_c",  // 19
                "cut_c",  // 20
                "cut_c",  // 21
                "cut_c",  // 22
                "cut_c"   // 23
            };

            // Select cuts array based on gender
            string[] cuts = (currentDna?.gender == "f") ? femaleCuts : maleCuts;

            if (hatIndex < 0 || hatIndex >= cuts.Length)
                return;

            string cutName = cuts[hatIndex];

            // If no hat (hatIndex == 0), show all hair normally
            if (hatIndex == 0)
                return;

            // PirateMale.py logic (lines 4360-4380):
            // For each hair piece index:
            // - partIdx == 1 (hair_base): ALWAYS show, never cut
            // - partIdx > 0: If cut version exists, show cut, hide full. Otherwise hide.
            // - partIdx == 0 (hair_none): Always hidden

            int cutVersionsShown = 0;
            int fullVersionsHidden = 0;
            var allNames = rendererCache.AllNames().ToList();

            // Debug: Log all hair-related meshes
            var hairMeshes = allNames.Where(n => n.StartsWith("hair_")).ToList();
            Debug.Log($"DnaApplier: Found {hairMeshes.Count} hair meshes for hat {hatIndex}: {string.Join(", ", hairMeshes)}");

            // Track which hair pieces are equipped (part of current hair style)
            // These are the pieces that were enabled before we apply hat cuts
            var equippedHairPieces = new List<string>();

            // First pass: identify which hair pieces are currently enabled (part of hair style)
            foreach (var name in allNames)
            {
                // Only check full hair meshes (not cuts)
                if (!name.StartsWith("hair_") || name.Contains("_cut") || name.StartsWith("hair_eyebrow_"))
                    continue;

                if (name.Contains("hair_base") || name.Contains("hair_none"))
                    continue;

                // Check if this hair piece is currently enabled (part of the hair style)
                var renderers = rendererCache.GetExact(name);
                if (renderers.Count > 0 && renderers.Any(r => r != null && r.enabled))
                {
                    equippedHairPieces.Add(name);
                    Debug.Log($"DnaApplier: Hair piece '{name}' is equipped (part of current style)");
                }
            }

            // Step 1: Process all non-cut hair pieces
            foreach (var name in allNames)
            {
                // Only process full hair meshes (not cuts, not eyebrows, not beard/mustache)
                if (!name.StartsWith("hair_") || name.Contains("_cut") || name.StartsWith("hair_eyebrow_"))
                    continue;

                // Male-specific: hair_base (index 1) always shows (PirateMale.py line 4364: partIdx == 1)
                // Females don't have hair_base
                if (name.Contains("hair_base"))
                {
                    equippedHairPieces.Add(name);
                    rendererCache.EnableExact(name, true);
                    Debug.Log($"DnaApplier: Keeping base hair '{name}' visible (always shown)");
                    continue;
                }

                // hair_none (index 0) always hidden
                if (name.Contains("hair_none"))
                {
                    rendererCache.EnableExact(name, false);
                    continue;
                }

                // For other hair pieces, check if cut version exists
                // Python does substring search: hairCut[j].getName().find(cuts[hatIdx]) >= 0
                // So we look for any cut mesh containing both the hair piece name and the cut name
                // e.g., "hair_m0" in mesh name and "cut_d" in mesh name
                bool hasCutVersion = allNames.Any(n => n.StartsWith(name + "_") && n.Contains(cutName));

                // Female special case: hat index 6 (bandanna_reg) can also use cut_c
                // PirateFemale.py line 4512-4516: if hatIdx == 6, also check cuts[7] (cut_c)
                if (!hasCutVersion && currentDna?.gender == "f" && hatIndex == 6)
                {
                    // For hat 6, also check if cut_c exists
                    hasCutVersion = allNames.Any(n => n.StartsWith(name + "_") && n.Contains("cut_c"));
                }

                if (hasCutVersion)
                {
                    // Hide full version, cut version will be shown in step 2
                    rendererCache.EnableExact(name, false);
                    fullVersionsHidden++;
                }
                else
                {
                    // No cut version exists for this hat type
                    // Female special case: partIdx == 2 (hair_c0) shows full if no cut exists
                    // PirateFemale.py line 4520-4523: if not cutFound and partIdx == 2, show full
                    bool isFemalePart2 = (currentDna?.gender == "f" && name.Contains("hair_c0"));

                    if (isFemalePart2)
                    {
                        // Show full hair_c0 (female base hair)
                        rendererCache.EnableExact(name, true);
                        Debug.Log($"DnaApplier: Keeping female hair_c0 '{name}' visible (partIdx 2 fallback)");
                    }
                    else
                    {
                        // Hide the hair piece
                        rendererCache.EnableExact(name, false);
                        fullVersionsHidden++;
                    }
                }
            }

            // Step 2: Show only cut versions that match this hat
            // (Color was already applied in ApplyHairSlot to all hair meshes)
            // Match Python logic from PirateFemale.py lines 4506-4516

            // Debug: Log all cut versions found
            var cutVersions = allNames.Where(n => n.Contains("_cut")).ToList();
            if (cutVersions.Count > 0)
            {
                Debug.Log($"DnaApplier: Found {cutVersions.Count} cut versions: {string.Join(", ", cutVersions)}");
            }

            foreach (var name in allNames)
            {
                if (!name.Contains("_cut"))
                    continue;

                // CRITICAL: Only show cut versions of hair pieces that are EQUIPPED (part of current style)
                // Python loops through hairParts (e.g., [12] for style 18), only processes those pieces
                bool belongsToEquippedPiece = false;
                string basePieceName = "";
                foreach (var equipped in equippedHairPieces)
                {
                    // Check if this cut mesh belongs to an equipped hair piece
                    // e.g., "hair_m0_cut_c" belongs to "hair_m0"
                    if (name.StartsWith(equipped + "_cut"))
                    {
                        belongsToEquippedPiece = true;
                        basePieceName = equipped;
                        break;
                    }
                }

                if (!belongsToEquippedPiece)
                {
                    // This cut belongs to a hair piece that's not equipped - hide it
                    rendererCache.EnableExact(name, false);
                    continue;
                }

                // Python line 4507: if hairCut[j].getName().find(cuts[hatIdx]) >= 0
                // This checks if the cut mesh name contains the cut string (e.g., "cut_d")
                bool shouldShow = name.Contains(cutName);

                // Python line 4512-4513: Female hat 6 fallback
                // if hatIdx == 6: if hairCut[j].getName().find(cuts[hatIdx + 1]) >= 0
                // This adds cut_c meshes as fallback for hat 6 (in addition to cut_d if it exists)
                if (!shouldShow && currentDna?.gender == "f" && hatIndex == 6)
                {
                    // cuts[6+1] = cuts[7] = 'cut_c'
                    shouldShow = name.Contains("cut_c");
                }

                rendererCache.EnableExact(name, shouldShow);

                if (shouldShow)
                {
                    cutVersionsShown++;
                    Debug.Log($"DnaApplier: Showing cut hair '{name}' for equipped piece '{basePieceName}' with hat cut '{cutName}'");
                }
            }

            Debug.Log($"DnaApplier: Applied hair cuts for hat {hatIndex} (cut: {cutName}) - {cutVersionsShown} cut versions shown, {fullVersionsHidden} full versions hidden");
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

            if (facialMorphApplier != null)
            {
                sb.AppendLine();
                sb.AppendLine(facialMorphApplier.GetDiagnosticInfo());
            }

            if (currentDna != null)
            {
                sb.AppendLine();
                sb.AppendLine($"Current DNA: {currentDna.name} ({currentDna.gender})");
                sb.AppendLine($"  Body: {currentDna.bodyShape} (height: {currentDna.bodyHeight})");
                sb.AppendLine($"  Clothing: Hat={currentDna.hat}, Shirt={currentDna.shirt}, Pants={currentDna.pants}");
                sb.AppendLine($"  Colors: Top={currentDna.topColorIdx}, Bot={currentDna.botColorIdx}, Hat={currentDna.hatColorIdx}");
                sb.AppendLine($"  Jewelry: {currentDna.jewelry.Count} zones");
                sb.AppendLine($"  Tattoos: {currentDna.tattoos.Count}");
                sb.AppendLine($"  Facial Morphs: {currentDna.headMorphs.Count}");
            }

            return sb.ToString();
        }
    }
}
