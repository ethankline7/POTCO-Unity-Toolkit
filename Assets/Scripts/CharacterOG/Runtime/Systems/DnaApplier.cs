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

            // 0. Hide all clothing groups first (important for clean slate)
            HideAllClothing();

            // 0.5. Always show head/face
            rendererCache.EnablePattern("**/body_head*", true);
            rendererCache.EnablePattern("**/body*head*", true);

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
            // Hide all clothing layers to start with a clean slate
            rendererCache.EnablePattern("**/clothing_layer1_*", false);
            rendererCache.EnablePattern("**/clothing_layer2_*", false);
            rendererCache.EnablePattern("**/clothing_layer3_*", false);

            // Hide all hair/beard/mustache
            rendererCache.EnablePattern("**/hair_*", false);
            rendererCache.EnablePattern("**/beard_*", false);
            rendererCache.EnablePattern("**/mustache_*", false);

            // Show body by default (hide later based on clothing)
            rendererCache.EnablePattern("**/body*", true);

            // Hide accessories by default (show only when jewelry is worn)
            rendererCache.EnablePattern("**/acc_*", false);
            rendererCache.EnablePattern("**/jewelry_*", false);

            Debug.Log("DnaApplier: Hid all clothing/hair, showed body");
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

        private void ApplySkinColor(int skinColorIdx)
        {
            Color skinColor = palettes.GetSkinColor(skinColorIdx);

            // Apply to body mesh groups (typically "body_*" groups)
            var bodyGroups = new string[] { "body", "body_head", "body_torso", "body_arms", "body_legs" };

            foreach (var groupName in bodyGroups)
            {
                var renderers = rendererCache.GetRenderers(groupName);

                foreach (var renderer in renderers)
                {
                    materialBinder.ApplyDye(renderer, "base", skinColor);
                }
            }

            // Also apply to pattern-matched body groups
            var bodyRenderers = rendererCache.GetRenderersMatchingPattern("**/body*");

            foreach (var renderer in bodyRenderers)
            {
                materialBinder.ApplyDye(renderer, "base", skinColor);
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

                    // Enable the jewelry group
                    rendererCache.EnableGroup(groupName, true);

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
                    var renderers = rendererCache.GetRenderers(groupName);

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
