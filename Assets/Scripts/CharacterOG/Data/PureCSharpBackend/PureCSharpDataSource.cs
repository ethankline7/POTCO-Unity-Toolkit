/// <summary>
/// Pure C# data source implementation.
/// Parses Python source files using OgPyReader and converts to Unity models.
/// No Python execution - literal data only.
/// </summary>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using CharacterOG.Models;

namespace CharacterOG.Data.PureCSharpBackend
{
    public class PureCSharpDataSource : IOgDataSource
    {
        public string BackendName => "Pure C# Parser";
        public bool IsAvailable => true;

        public Dictionary<string, BodyShapeDef> LoadBodyShapes(string gender = "m")
        {
            var reader = new OgPyReader("", OgPaths.BodyDefs);
            var data = reader.ParseFile(OgPaths.BodyDefs);

            var shapes = new Dictionary<string, BodyShapeDef>();

            foreach (var kvp in data)
            {
                if (kvp.Value is PyDict dict)
                {
                    shapes[kvp.Key] = ConvertBodyShape(kvp.Key, dict);
                }
            }

            return shapes;
        }

        public Palettes LoadPalettesAndDyeRules()
        {
            var reader = new OgPyReader("", OgPaths.HumanDNA);
            var data = reader.ParseFile(OgPaths.HumanDNA);

            var palettes = new Palettes();

            // Load skin colors
            if (data.TryGetValue("skinColors", out var skinNode) && skinNode is PyList skinList)
            {
                foreach (var item in skinList.items)
                {
                    if (item is PyFunctionCall call)
                        palettes.skin.Add(call.ToColor());
                }
            }

            // Load hair colors
            if (data.TryGetValue("hairColors", out var hairNode) && hairNode is PyList hairList)
            {
                foreach (var item in hairList.items)
                {
                    if (item is PyFunctionCall call)
                        palettes.hair.Add(call.ToColor());
                }
            }

            // Load dye colors from hatColorsOld[0] (base dye palette)
            if (data.TryGetValue("hatColorsOld", out var hatColorsNode) && hatColorsNode is PyList hatColorsList)
            {
                if (hatColorsList.items.Count > 0 && hatColorsList.items[0] is PyList dyeList)
                {
                    foreach (var item in dyeList.items)
                    {
                        if (item is PyFunctionCall call)
                            palettes.dye.Add(call.ToColor());
                    }
                }
            }

            // Load DYE_COLOR_LEVEL
            if (data.TryGetValue("DYE_COLOR_LEVEL", out var dyeLevelNode) && dyeLevelNode is PyDict dyeLevelDict)
            {
                foreach (var kvp in dyeLevelDict.items)
                {
                    if (int.TryParse(kvp.Key, out int level) && kvp.Value is PyList levelList)
                    {
                        palettes.dyeColorLevels[level] = new List<int>();
                        foreach (var item in levelList.items)
                        {
                            if (item is PyNumber num)
                                palettes.dyeColorLevels[level].Add(num.AsInt());
                        }
                    }
                }
            }

            return palettes;
        }

        public ClothingCatalog LoadClothingCatalog(string gender = "m")
        {
            var catalog = new ClothingCatalog();

            try
            {
                // Load ClothingGlobals.py
                Debug.Log($"Loading ClothingGlobals.py from {OgPaths.ClothingGlobals}");
                var globalsReader = new OgPyReader("", OgPaths.ClothingGlobals);
                var globalsData = globalsReader.ParseFile(OgPaths.ClothingGlobals);
                Debug.Log($"Parsed ClothingGlobals.py - Found {globalsData.Count} top-level variables: {string.Join(", ", globalsData.Keys)}");

                // Load gender-specific file
                string genderFile = OgPaths.GetPirateGenderFile(gender);
                Debug.Log($"Loading gender file {genderFile}");
                var genderReader = new OgPyReader("", genderFile);
                var genderData = genderReader.ParseFile(genderFile);
                Debug.Log($"Parsed gender file - Found {genderData.Count} top-level variables");

                // Parse CLOTHING_NAMES for display names
                if (globalsData.TryGetValue("CLOTHING_NAMES", out var clothingNamesNode) && clothingNamesNode is PyDict clothingNamesDict)
                {
                    Debug.Log($"Found CLOTHING_NAMES with {clothingNamesDict.items.Count} slots");
                    ParseClothingNames(catalog, clothingNamesDict, gender);
                }
                else
                {
                    Debug.LogWarning($"CLOTHING_NAMES not found or not a dict. Available keys: {string.Join(", ", globalsData.Keys)}");
                }

                // Parse UNDERWEAR
                if (globalsData.TryGetValue("UNDERWEAR", out var underwearNode) && underwearNode is PyDict underwearDict)
                {
                    ParseUnderwear(catalog, underwearDict);
                }

                // NEW: Parse layer lists and clothing arrays from gender file to get group patterns
                ParseClothingPatterns(catalog, genderData, gender);

                // AFTER populating variants, inject body hide masks:
                InjectBodyHideMasksFromGenderFile(catalog, genderFile);

                Debug.Log($"ClothingCatalog loaded: {catalog.variantsBySlot.Sum(kvp => kvp.Value.Count)} total variants across {catalog.variantsBySlot.Count} slots");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load ClothingCatalog: {ex.Message}\n{ex.StackTrace}");
            }

            return catalog;
        }

        public JewelryTattooDefs LoadJewelryAndTattoos(string gender = "m")
        {
            var defs = new JewelryTattooDefs();

            string genderFile = OgPaths.GetPirateGenderFile(gender);
            var reader = new OgPyReader("", genderFile);
            var data = reader.ParseFile(genderFile);

            // Load jewelry_geos_face
            if (data.TryGetValue("jewelry_geos_face", out var faceNode) && faceNode is PyList faceList)
            {
                var faceGroups = new List<string>();
                foreach (var item in faceList.items)
                {
                    if (item is PyString str)
                        faceGroups.Add(str.value);
                }
                defs.jewelryGroupsByZone[$"{gender}_face"] = faceGroups;
            }

            // Load jewelry_geos_body
            if (data.TryGetValue("jewelry_geos_body", out var bodyNode) && bodyNode is PyList bodyList)
            {
                var bodyGroups = new List<string>();
                foreach (var item in bodyList.items)
                {
                    if (item is PyString str)
                        bodyGroups.Add(str.value);
                }
                defs.jewelryGroupsByZone[$"{gender}_body"] = bodyGroups;
            }

            // Load vector_tattoos for tattoo zone mapping (indices map to zones)
            if (data.TryGetValue("vector_tattoos", out var tattoosNode) && tattoosNode is PyList tattoosList)
            {
                for (int i = 0; i < tattoosList.items.Count; i++)
                {
                    defs.tattooZonesToBodyGroups[$"zone{i}"] = new List<string> { $"body_zone{i}" };
                }
            }

            return defs;
        }

        public Dictionary<string, PirateDNA> LoadNpcDna()
        {
            var npcs = new Dictionary<string, PirateDNA>();

            if (!File.Exists(OgPaths.NPCList))
            {
                Debug.LogError($"NPCList.py not found at: {OgPaths.NPCList}");
                return npcs;
            }

            var reader = new OgPyReader("", OgPaths.NPCList);
            Dictionary<string, PyNode> data;

            try
            {
                Debug.Log("Parsing NPCList.py (35k+ lines, this may take a moment)...");
                data = reader.ParseFile(OgPaths.NPCList);
                Debug.Log($"Successfully parsed NPCList.py - Found {data.Count} top-level variables: {string.Join(", ", data.Keys)}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse NPCList.py: {ex.Message}\n{ex.StackTrace}");
                return npcs;
            }

            if (!data.TryGetValue("NPC_LIST", out var npcListNode) || !(npcListNode is PyDict npcListDict))
            {
                Debug.LogWarning($"NPC_LIST not found in NPCList.py. Available keys: {string.Join(", ", data.Keys)}");
                return npcs;
            }

            foreach (var kvp in npcListDict.items)
            {
                string npcId = kvp.Key;

                if (kvp.Value is PyDict npcDict)
                {
                    try
                    {
                        var dna = ConvertNpcDict(npcId, npcDict);
                        npcs[npcId] = dna;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to parse NPC {npcId}: {ex.Message}");
                    }
                }
            }

            return npcs;
        }

        // ===== CONVERSION HELPERS =====

        private BodyShapeDef ConvertBodyShape(string name, PyDict dict)
        {
            var shape = new BodyShapeDef(name);

            shape.headScale = dict.GetFloat("headScale", 1f);
            shape.bodyScale = dict.GetFloat("bodyScale", 1f);
            shape.heightBias = dict.GetFloat("heightBias", 0f);
            shape.frameType = dict.GetString("frameType", "");
            shape.animType = dict.GetString("AnimType", "");

            // Parse headPostion (VBase3)
            if (dict.Get<PyFunctionCall>("headPostion") is PyFunctionCall headPosCall)
            {
                shape.headPosition = headPosCall.ToVector3();
            }

            // Parse bodyTextures
            if (dict.GetList("bodyTextures") is PyList bodyTexList)
            {
                foreach (var item in bodyTexList.items)
                {
                    if (item is PyString str)
                        shape.bodyTextures.Add(str.value);
                }
            }

            // Parse boneScales
            if (dict.GetDict("boneScales") is PyDict boneScalesDict)
            {
                foreach (var boneKvp in boneScalesDict.items)
                {
                    if (boneKvp.Value is PyFunctionCall call)
                    {
                        shape.boneScales[boneKvp.Key] = call.ToVector3();
                    }
                }
            }

            // Parse bone offsets (tr_* entries in boneScales are actually offsets)
            // In OG data, tr_* bones are position offsets, not scales
            var offsetKeys = shape.boneScales.Keys.Where(k => k.StartsWith("tr_")).ToList();
            foreach (var key in offsetKeys)
            {
                shape.boneOffsets[key] = shape.boneScales[key];
                shape.boneScales.Remove(key);
            }

            return shape;
        }

        private void ParseClothingNames(ClothingCatalog catalog, PyDict clothingNamesDict, string gender)
        {
            int totalVariants = 0;
            foreach (var slotKvp in clothingNamesDict.items)
            {
                if (!int.TryParse(slotKvp.Key, out int slotNum))
                {
                    Debug.LogWarning($"Skipping non-numeric slot key: {slotKvp.Key}");
                    continue;
                }

                Slot slot = (Slot)slotNum;

                if (slotKvp.Value is PyDict genderDict)
                {
                    string genderKey = gender.ToUpper() == "F" ? "FEMALE" : "MALE";

                    if (genderDict.items.TryGetValue(genderKey, out var variantsNode) && variantsNode is PyDict variantsDict)
                    {
                        int slotVariants = 0;
                        foreach (var variantKvp in variantsDict.items)
                        {
                            if (!int.TryParse(variantKvp.Key, out int idx))
                            {
                                Debug.LogWarning($"Skipping non-numeric variant index for slot {slot}: {variantKvp.Key}");
                                continue;
                            }

                            if (variantKvp.Value is PyString nameStr)
                            {
                                var variant = new SlotVariant
                                {
                                    id = nameStr.value.ToLower().Replace(" ", "_"),
                                    displayName = nameStr.value,
                                    ogIndex = idx
                                };

                                catalog.variantsBySlot[slot].Add(variant);
                                slotVariants++;
                                totalVariants++;
                            }
                            else
                            {
                                Debug.LogWarning($"Variant value for slot {slot}[{idx}] is not a string, it's {variantKvp.Value?.GetType().Name}");
                            }
                        }
                        Debug.Log($"Loaded {slotVariants} variants for slot {slot} ({genderKey})");
                    }
                    else
                    {
                        Debug.LogWarning($"Gender key '{genderKey}' not found or not a dict for slot {slot}. Available keys: {string.Join(", ", genderDict.items.Keys)}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Slot {slotNum} value is not a dict, it's {slotKvp.Value?.GetType().Name}");
                }
            }
            Debug.Log($"ParseClothingNames total: {totalVariants} variants");
        }

        private void ParseUnderwear(ClothingCatalog catalog, PyDict underwearDict)
        {
            foreach (var genderKvp in underwearDict.items)
            {
                string gender = genderKvp.Key;

                if (genderKvp.Value is PyDict slotsDict)
                {
                    var underwearSet = new Dictionary<Slot, (int idx, int texIdx, int colorIdx)>();

                    foreach (var slotKvp in slotsDict.items)
                    {
                        if (!int.TryParse(slotKvp.Key, out int slotNum))
                            continue;

                        Slot slot = (Slot)slotNum;

                        if (slotKvp.Value is PyTuple tuple && tuple.Count >= 3)
                        {
                            int idx = (tuple.Get<PyNumber>(0))?.AsInt() ?? 0;
                            int texIdx = (tuple.Get<PyNumber>(1))?.AsInt() ?? 0;
                            int colorIdx = (tuple.Get<PyNumber>(2))?.AsInt() ?? 0;

                            underwearSet[slot] = (idx, texIdx, colorIdx);
                        }
                    }

                    catalog.underwear[gender] = underwearSet;
                }
            }
        }

        private void ParseClothingPatterns(ClothingCatalog catalog, Dictionary<string, PyNode> genderData, string gender)
        {
            // Extract layer pattern lists from gender file
            // Note: These are inside class methods, so they won't be in top-level variables
            // We'll need to search for them in the source or use a different approach

            Debug.Log("ParseClothingPatterns: Building group patterns for clothing variants");

            // Add Hair/Beard/Mustache variants (not in CLOTHING_NAMES)
            AddHairBeardMustacheVariants(catalog, gender);

            // Parse hairList, layer1List from Python source
            string genderFile = OgPaths.GetPirateGenderFile(gender);
            ParseLayerListsFromSource(catalog, genderFile);

            // Build patterns from variant names (fallback for items not in lists)
            BuildPatternsFromNames(catalog, gender);
        }

        private void ParseLayerListsFromSource(ClothingCatalog catalog, string genderFilePath)
        {
            var text = File.ReadAllText(genderFilePath);

            // Parse hairList
            var hairMatches = Regex.Match(text, @"hairList = \[(.*?)\]", RegexOptions.Singleline);
            if (hairMatches.Success)
            {
                var patterns = ExtractPatternList(hairMatches.Groups[1].Value);
                for (int i = 0; i < patterns.Count; i++)
                {
                    var variant = catalog.GetVariantByOgIndex(Slot.Hair, i);
                    if (variant != null)
                    {
                        // Don't set pattern for "none" variants (index 0) - we want them to hide everything
                        if (i > 0)
                        {
                            variant.pattern = patterns[i];
                        }
                    }
                }
                Debug.Log($"Parsed {patterns.Count} hairList patterns");
            }

            // Parse layer1List (contains hats, shirts, pants, shoes, etc.)
            var layer1Matches = Regex.Match(text, @"layer1List = \[(.*?)\]", RegexOptions.Singleline);
            if (layer1Matches.Success)
            {
                var patterns = ExtractPatternList(layer1Matches.Groups[1].Value);
                Debug.Log($"Parsed {patterns.Count} layer1List patterns");

                // Map layer1List indices to slot variants
                // Based on the order in layer1List:
                // 0-15: Shirt parts
                // 16-21: Pant parts
                // 22-27: Shoe parts
                // 28-30: Apron parts
                // 31+: Hat parts

                // For now, just store them for manual mapping or debugging
                for (int i = 0; i < patterns.Count; i++)
                {
                    string pattern = patterns[i];

                    // Shoes are indices 22-27
                    if (i >= 22 && i <= 27)
                    {
                        int shoeIdx = i - 22;
                        var variant = catalog.GetVariantByOgIndex(Slot.Shoe, shoeIdx);
                        if (variant != null && shoeIdx > 0) // Skip index 0 (shoe_none)
                        {
                            variant.pattern = pattern;
                            Debug.Log($"Shoe[{shoeIdx}]: {pattern}");
                        }
                    }
                    // Hats start around index 31
                    else if (i >= 31 && pattern.Contains("hat"))
                    {
                        int hatIdx = i - 31;
                        var variant = catalog.GetVariantByOgIndex(Slot.Hat, hatIdx);
                        if (variant != null)
                        {
                            variant.pattern = pattern;
                            Debug.Log($"Hat[{hatIdx}]: {pattern}");
                        }
                    }
                }
            }
        }

        private List<string> ExtractPatternList(string listContent)
        {
            var patterns = new List<string>();
            // Match quoted strings like '**/hair_base' or '**/clothing_layer1_hat_tricorn;+s'
            var stringRx = new Regex(@"['""]([^'""]+)['""]", RegexOptions.Compiled);

            foreach (Match m in stringRx.Matches(listContent))
            {
                string pattern = m.Groups[1].Value;
                // Strip POTCO-specific ;+s suffix (show groups marker)
                pattern = pattern.Replace(";+s", "");
                patterns.Add(pattern);
            }

            return patterns;
        }

        private void AddHairBeardMustacheVariants(ClothingCatalog catalog, string gender)
        {
            // Hair styles - actual count matches hairList in PirateMale.py (12 items)
            string[] hairNames = new[]
            {
                "None", "Base", "A0", "A1", "A2", "B1",
                "D0", "E1", "F0", "G0", "H0", "I0"
            };

            for (int i = 0; i < hairNames.Length; i++)
            {
                catalog.variantsBySlot[Slot.Hair].Add(new SlotVariant
                {
                    id = $"hair_{i}",
                    displayName = hairNames[i],
                    ogIndex = i
                });
            }

            // Beard styles (placeholder - actual count varies, increase to 20 to be safe)
            for (int i = 0; i < 20; i++)
            {
                catalog.variantsBySlot[Slot.Beard].Add(new SlotVariant
                {
                    id = $"beard_{i}",
                    displayName = i == 0 ? "None" : $"Beard {i}",
                    ogIndex = i
                });
            }

            // Mustache styles (placeholder - actual count varies, increase to 10 to be safe)
            for (int i = 0; i < 10; i++)
            {
                catalog.variantsBySlot[Slot.Mustache].Add(new SlotVariant
                {
                    id = $"mustache_{i}",
                    displayName = i == 0 ? "None" : $"Mustache {i}",
                    ogIndex = i
                });
            }

            Debug.Log($"Added Hair: {hairNames.Length}, Beard: 20, Mustache: 10 variants");
        }

        private void BuildPatternsFromNames(ClothingCatalog catalog, string gender)
        {
            // Build patterns for each slot based on naming conventions
            // Only builds patterns for variants that don't already have one from ParseLayerListsFromSource
            int totalPatterns = 0;
            foreach (var slotKvp in catalog.variantsBySlot)
            {
                Slot slot = slotKvp.Key;
                var variants = slotKvp.Value;

                foreach (var variant in variants)
                {
                    if (variant.ogIndex < 0)
                        continue;

                    // Skip if pattern already set by ParseLayerListsFromSource
                    if (!string.IsNullOrEmpty(variant.pattern))
                        continue;

                    string pattern = BuildPatternForVariant(slot, variant, gender);
                    if (!string.IsNullOrEmpty(pattern))
                    {
                        variant.pattern = pattern;
                        totalPatterns++;

                        // Debug log first few patterns
                        if (totalPatterns <= 10)
                        {
                            Debug.Log($"Pattern: {slot}[{variant.ogIndex}] '{variant.displayName}' → '{pattern}'");
                        }
                    }
                }
            }
            Debug.Log($"BuildPatternsFromNames: Generated {totalPatterns} fallback patterns");
        }

        private string BuildPatternForVariant(Slot slot, SlotVariant variant, string gender)
        {
            // Convert display name to pattern-friendly format
            // Handle special cases like "Bandana Full" → "bandanna_full", "EITC" → "eitc"
            string nameLower = variant.displayName.ToLower()
                .Replace(" ", "_")
                .Replace("bandana", "bandanna") // POTCO uses "bandanna" spelling
                .Replace("regular", "reg");      // "Regular" abbreviated to "reg"

            switch (slot)
            {
                case Slot.Hat:
                    if (variant.ogIndex == 0 || variant.displayName == "None")
                        return null; // No pattern for "None"
                    // POTCO uses ;+s to mark "show" groups, we use * wildcard
                    return $"**/clothing_layer1_hat_{nameLower}*";

                case Slot.Shirt:
                    // Shirts are complex - use generic pattern for now
                    // TODO: Parse actual shirt group mappings
                    if (variant.ogIndex == 0)
                        return null;
                    return "**/clothing_layer1_shirt_common*";

                case Slot.Vest:
                    if (variant.ogIndex == 0 || variant.displayName == "None")
                        return "**/clothing_layer2_vest_none*";
                    if (nameLower.Contains("long"))
                        return "**/clothing_layer2_vest_long_closed*";
                    if (nameLower.Contains("open"))
                        return "**/clothing_layer2_vest_open*";
                    return "**/clothing_layer2_vest_closed*";

                case Slot.Coat:
                    if (variant.ogIndex == 0 || variant.displayName == "None")
                        return "**/clothing_layer3_coat_none*";
                    if (nameLower.Contains("long"))
                        return "**/clothing_layer3_coat_long*";
                    if (nameLower.Contains("short"))
                        return "**/clothing_layer3_coat_short*";
                    if (nameLower.Contains("navy"))
                        return "**/clothing_layer3_coat_navy*";
                    if (nameLower.Contains("eitc"))
                        return "**/clothing_layer3_coat_eitc*";
                    return "**/clothing_layer3_coat*";

                case Slot.Pant:
                    // Pants are complex - use generic pattern for now
                    if (variant.ogIndex == 0)
                        return null;
                    return "**/clothing_layer1_pant*";

                case Slot.Belt:
                    if (variant.ogIndex == 0 || variant.displayName == "None")
                        return "**/clothing_layer2_belt_none*";
                    if (nameLower.Contains("sash"))
                        return "**/clothing_layer2_belt_sash*";
                    if (nameLower.Contains("oval"))
                        return "**/clothing_layer2_belt*oval*";
                    if (nameLower.Contains("square"))
                        return "**/clothing_layer2_belt*square*";
                    return "**/clothing_layer2_belt*";

                case Slot.Shoe:
                    // Shoes are complex - use generic pattern for now
                    if (variant.ogIndex == 0)
                        return "**/clothing_layer1_shoe_none*";
                    return "**/clothing_layer1_shoe*";

                case Slot.Hair:
                    // Hair will need special handling - multiple groups per style
                    if (variant.ogIndex == 0)
                        return "**/hair_none*";
                    // For now, show all hair (will refine later)
                    return "**/hair_*";

                case Slot.Beard:
                    if (variant.ogIndex == 0)
                        return "**/beard_none*";
                    return "**/beard_*";

                case Slot.Mustache:
                    if (variant.ogIndex == 0)
                        return "**/mustache_none*";
                    return "**/mustache_*";

                default:
                    return null;
            }
        }

        private PirateDNA ConvertNpcDict(string npcId, PyDict npcDict)
        {
            var dna = new PirateDNA { name = npcId };

            // Iterate through function calls and apply them
            foreach (var kvp in npcDict.items)
            {
                string funcName = kvp.Key;
                PyNode argsNode = kvp.Value;

                // Handle both tuple and non-tuple values
                PyTuple tuple;
                if (argsNode is PyTuple t)
                {
                    tuple = t;
                }
                else if (argsNode is PyNull)
                {
                    // Skip None values (from unresolvable expressions like PLocalizer.NPCNames['id'])
                    continue;
                }
                else
                {
                    // Wrap bare values in a single-element tuple
                    tuple = new PyTuple();
                    tuple.items.Add(argsNode);
                }

                ApplyNpcFunction(dna, funcName, tuple);
            }

            return dna;
        }

        private void ApplyNpcFunction(PirateDNA dna, string funcName, PyTuple args)
        {
            switch (funcName)
            {
                case "setName":
                    if (args.Get<PyString>(0) is PyString nameStr)
                        dna.name = nameStr.value;
                    break;

                case "setGender":
                    if (args.Get<PyString>(0) is PyString genderStr)
                        dna.gender = genderStr.value;
                    break;

                case "setBodyShape":
                    if (args.Get<PyString>(0) is PyString shapeStr)
                        dna.bodyShape = shapeStr.value;
                    break;

                case "setBodyHeight":
                    if (args.Get<PyNumber>(0) is PyNumber heightNum)
                        dna.bodyHeight = heightNum.AsFloat();
                    break;

                case "setBodyColor":
                case "setBodySkin":
                    if (args.Get<PyNumber>(0) is PyNumber skinNum)
                        dna.skinColorIdx = skinNum.AsInt();
                    break;

                case "setClothesHat":
                    if (args.Get<PyNumber>(0) is PyNumber hatIdx)
                        dna.hat = hatIdx.AsInt();
                    if (args.Count > 1 && args.Get<PyNumber>(1) is PyNumber hatTex)
                        dna.hatTex = hatTex.AsInt();
                    break;

                case "setClothesShirt":
                    if (args.Get<PyNumber>(0) is PyNumber shirtIdx)
                        dna.shirt = shirtIdx.AsInt();
                    if (args.Count > 1 && args.Get<PyNumber>(1) is PyNumber shirtTex)
                        dna.shirtTex = shirtTex.AsInt();
                    break;

                case "setClothesVest":
                    if (args.Get<PyNumber>(0) is PyNumber vestIdx)
                        dna.vest = vestIdx.AsInt();
                    if (args.Count > 1 && args.Get<PyNumber>(1) is PyNumber vestTex)
                        dna.vestTex = vestTex.AsInt();
                    break;

                case "setClothesCoat":
                    if (args.Get<PyNumber>(0) is PyNumber coatIdx)
                        dna.coat = coatIdx.AsInt();
                    if (args.Count > 1 && args.Get<PyNumber>(1) is PyNumber coatTex)
                        dna.coatTex = coatTex.AsInt();
                    break;

                case "setClothesPant":
                    if (args.Get<PyNumber>(0) is PyNumber pantIdx)
                        dna.pants = pantIdx.AsInt();
                    if (args.Count > 1 && args.Get<PyNumber>(1) is PyNumber pantTex)
                        dna.pantsTex = pantTex.AsInt();
                    break;

                case "setClothesBelt":
                    if (args.Get<PyNumber>(0) is PyNumber beltIdx)
                        dna.belt = beltIdx.AsInt();
                    if (args.Count > 1 && args.Get<PyNumber>(1) is PyNumber beltTex)
                        dna.beltTex = beltTex.AsInt();
                    break;

                case "setClothesShoe":
                    if (args.Get<PyNumber>(0) is PyNumber shoeIdx)
                        dna.shoes = shoeIdx.AsInt();
                    if (args.Count > 1 && args.Get<PyNumber>(1) is PyNumber shoeTex)
                        dna.shoesTex = shoeTex.AsInt();
                    break;

                case "setClothesTopColor":
                    if (args.Get<PyNumber>(0) is PyNumber topColor)
                        dna.topColorIdx = topColor.AsInt();
                    break;

                case "setClothesBotColor":
                    if (args.Get<PyNumber>(0) is PyNumber botColor)
                        dna.botColorIdx = botColor.AsInt();
                    break;

                case "setHatColor":
                    if (args.Get<PyNumber>(0) is PyNumber hatColor)
                        dna.hatColorIdx = hatColor.AsInt();
                    break;

                case "setHairHair":
                    if (args.Get<PyNumber>(0) is PyNumber hairIdx)
                        dna.hair = hairIdx.AsInt();
                    break;

                case "setHairBeard":
                    if (args.Get<PyNumber>(0) is PyNumber beardIdx)
                        dna.beard = beardIdx.AsInt();
                    break;

                case "setHairMustache":
                    if (args.Get<PyNumber>(0) is PyNumber mustacheIdx)
                        dna.mustache = mustacheIdx.AsInt();
                    break;

                case "setHairColor":
                    if (args.Get<PyNumber>(0) is PyNumber hairColor)
                        dna.hairColorIdx = hairColor.AsInt();
                    break;

                case "setHeadTexture":
                    if (args.Get<PyNumber>(0) is PyNumber headTex)
                        dna.headTexture = headTex.AsInt();
                    break;

                case "setEyesColor":
                    if (args.Get<PyNumber>(0) is PyNumber eyeColor)
                        dna.eyeColorIdx = eyeColor.AsInt();
                    break;

                case string s when s.StartsWith("setTattoo"):
                    ParseTattoo(dna, funcName, args);
                    break;

                case string s when s.StartsWith("setJewelry"):
                    ParseJewelry(dna, funcName, args);
                    break;
            }
        }

        private void ParseTattoo(PirateDNA dna, string funcName, PyTuple args)
        {
            // Extract zone from function name (e.g., setTattooZone2 → 2)
            string zoneStr = funcName.Replace("setTattoo", "").Replace("Zone", "").Replace("Chest", "0");
            int zone = int.TryParse(zoneStr, out int z) ? z : 0;

            if (args.Count >= 6)
            {
                int idx = args.Get<PyNumber>(0)?.AsInt() ?? 0;
                float u = args.Get<PyNumber>(1)?.AsFloat() ?? 0f;
                float v = args.Get<PyNumber>(2)?.AsFloat() ?? 0f;
                float scale = args.Get<PyNumber>(3)?.AsFloat() ?? 1f;
                float rot = args.Get<PyNumber>(4)?.AsFloat() ?? 0f;
                int colorIdx = args.Get<PyNumber>(5)?.AsInt() ?? 0;

                dna.tattoos.Add(new TattooSpec(zone, idx, u, v, scale, rot, colorIdx));
            }
        }

        private void ParseJewelry(PirateDNA dna, string funcName, PyTuple args)
        {
            // Extract zone from function name (e.g., setJewelryZone1 → zone1)
            string zone = funcName.Replace("setJewelry", "").ToLower();

            if (args.Get<PyNumber>(0) is PyNumber idxNum)
            {
                dna.jewelry[zone] = idxNum.AsInt();
            }
        }

        // ===== BODY HIDE MASK PARSING =====

        private static readonly Dictionary<string, Slot> _slotByPyName = new()
        {
            { "Shirt", Slot.Shirt },
            { "Vest",  Slot.Vest  },
            { "Coat",  Slot.Coat  },
            { "Pant",  Slot.Pant },
            { "Shoe",  Slot.Shoe },
            { "Belt",  Slot.Belt  },
        };

        private void InjectBodyHideMasksFromGenderFile(ClothingCatalog catalog, string genderFilePath)
        {
            if (!File.Exists(genderFilePath))
            {
                Debug.LogWarning($"[CharacterOG] Gender file not found: {genderFilePath}");
                return;
            }

            var text = File.ReadAllText(genderFilePath);

            // Matches: self.clothingsShirt.append( [ ... ] )
            var blockRx = new Regex(@"self\.clothings(?<slot>[A-Za-z]+)\.append\s*\(\s*(?<payload>\[.*?\])\s*\)",
                RegexOptions.Singleline | RegexOptions.Compiled);

            // Matches integers like -12 or 34 inside payload
            var intRx = new Regex(@"[-+]?\d+", RegexOptions.Compiled);

            // Variant order is the OG index, so we track how many append calls we've seen per slot
            var variantIndexPerSlot = new Dictionary<Slot, int>();

            foreach (Match m in blockRx.Matches(text))
            {
                var slotName = m.Groups["slot"].Value;           // e.g., "Shirt"
                if (!_slotByPyName.TryGetValue(slotName, out var slot))
                    continue;

                if (!variantIndexPerSlot.TryGetValue(slot, out var ogIndex))
                    ogIndex = 0;

                // Extract all integers from the payload
                var payload = m.Groups["payload"].Value;
                var numbers = new List<int>();
                foreach (Match im in intRx.Matches(payload))
                {
                    if (int.TryParse(im.Value, out var n))
                        numbers.Add(n);
                }

                // In the OG, element[0] is a list of clothing part indices; the rest are negative body indices to hide.
                // We only need the negative entries here and convert them to positive OG body indices.
                var bodyHideIndices = new List<int>();
                foreach (var n in numbers)
                {
                    if (n < 0) bodyHideIndices.Add(-n); // -(-5) -> 5
                }

                // Attach to our catalog variant with matching ogIndex
                var variant = catalog.GetVariantByOgIndex(slot, ogIndex);
                if (variant != null)
                {
                    variant.bodyHideIndices = bodyHideIndices;
                }
                else
                {
                    // If variants weren't built yet, ensure there's a stub so we don't lose the mask
                    variant = new SlotVariant { ogIndex = ogIndex, id = $"{slot}_{ogIndex}", displayName = $"{slot} {ogIndex}" };
                    variant.bodyHideIndices = bodyHideIndices;
                    if (!catalog.variantsBySlot.ContainsKey(slot))
                        catalog.variantsBySlot[slot] = new List<SlotVariant>();
                    catalog.variantsBySlot[slot].Add(variant);
                }

                variantIndexPerSlot[slot] = ogIndex + 1; // increment OG index for this slot
            }

            Debug.Log($"[CharacterOG] Injected body hide masks from {Path.GetFileName(genderFilePath)}");
        }
    }
}
