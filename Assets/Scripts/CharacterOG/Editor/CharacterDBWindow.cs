/// <summary>
/// Editor window for inspecting POTCO character database.
/// Allows testing data loading backends and previewing character data.
/// Window → POTCO → Character Database
/// </summary>
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using CharacterOG.Data;
using CharacterOG.Data.PureCSharpBackend;
using CharacterOG.Models;
using CharacterOG.Runtime.Systems;
using CharacterOG.Runtime.Utils;

namespace CharacterOG.Editor
{
    public class CharacterDBWindow : EditorWindow
    {
        private enum ViewMode { Paths, BodyShapes, Clothing, Palettes, Jewelry, NPCs }

        private ViewMode viewMode = ViewMode.Paths;
        private string selectedGender = "m";

        private IOgDataSource dataSource;
        private Dictionary<string, BodyShapeDef> bodyShapes;
        private ClothingCatalog clothingCatalog;
        private Palettes palettes;
        private JewelryTattooDefs jewelryTattoos;
        private Dictionary<string, PirateDNA> npcDna;

        private Vector2 scrollPos;
        private bool dataLoaded = false;
        private string loadError;

        private GameObject selectedCharacter;
        private Slot previewSlot = Slot.Hat;
        private int previewVariantIdx = 0;

        [MenuItem("Window/POTCO/Character Database")]
        public static void ShowWindow()
        {
            var window = GetWindow<CharacterDBWindow>("Character DB");
            window.minSize = new Vector2(600, 400);
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("POTCO Character Database Inspector", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // Controls
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Load Data", GUILayout.Width(100)))
            {
                LoadData();
            }

            if (GUILayout.Button("Refresh Paths", GUILayout.Width(100)))
            {
                AssetDatabase.Refresh();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // View mode tabs
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(viewMode == ViewMode.Paths, "Paths", "Button")) viewMode = ViewMode.Paths;
            if (GUILayout.Toggle(viewMode == ViewMode.BodyShapes, "Body Shapes", "Button")) viewMode = ViewMode.BodyShapes;
            if (GUILayout.Toggle(viewMode == ViewMode.Clothing, "Clothing", "Button")) viewMode = ViewMode.Clothing;
            if (GUILayout.Toggle(viewMode == ViewMode.Palettes, "Palettes", "Button")) viewMode = ViewMode.Palettes;
            if (GUILayout.Toggle(viewMode == ViewMode.Jewelry, "Jewelry/Tattoos", "Button")) viewMode = ViewMode.Jewelry;
            if (GUILayout.Toggle(viewMode == ViewMode.NPCs, "NPCs", "Button")) viewMode = ViewMode.NPCs;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Main content area
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            switch (viewMode)
            {
                case ViewMode.Paths: DrawPathsView(); break;
                case ViewMode.BodyShapes: DrawBodyShapesView(); break;
                case ViewMode.Clothing: DrawClothingView(); break;
                case ViewMode.Palettes: DrawPalettesView(); break;
                case ViewMode.Jewelry: DrawJewelryView(); break;
                case ViewMode.NPCs: DrawNPCsView(); break;
            }

            EditorGUILayout.EndScrollView();

            // Status bar
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (dataLoaded)
            {
                EditorGUILayout.LabelField($"✓ Data loaded successfully", EditorStyles.miniLabel);
            }
            else if (!string.IsNullOrEmpty(loadError))
            {
                EditorGUILayout.LabelField($"✗ {loadError}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("Press 'Load Data' to begin", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        private void LoadData()
        {
            try
            {
                dataSource = new PureCSharpDataSource();

                if (!dataSource.IsAvailable)
                {
                    loadError = $"{dataSource.BackendName} is not available";
                    dataLoaded = false;
                    return;
                }

                bodyShapes = dataSource.LoadBodyShapes(selectedGender);
                clothingCatalog = dataSource.LoadClothingCatalog(selectedGender);
                palettes = dataSource.LoadPalettesAndDyeRules();
                jewelryTattoos = dataSource.LoadJewelryAndTattoos(selectedGender);
                npcDna = dataSource.LoadNpcDna();

                dataLoaded = true;
                loadError = null;
                Debug.Log($"Character DB: Loaded data using {dataSource.BackendName}");
            }
            catch (System.Exception ex)
            {
                loadError = ex.Message;
                dataLoaded = false;
                Debug.LogError($"Character DB Load Error: {ex}");
            }
        }

        private void DrawPathsView()
        {
            EditorGUILayout.LabelField("File Paths Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.TextArea(OgPaths.GetDiagnosticInfo(), EditorStyles.wordWrappedLabel);
        }

        private void DrawBodyShapesView()
        {
            if (!dataLoaded || bodyShapes == null)
            {
                EditorGUILayout.HelpBox("No data loaded. Press 'Load Data' first.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Body Shapes ({bodyShapes.Count})", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            foreach (var kvp in bodyShapes.OrderBy(x => x.Key))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(kvp.Key, EditorStyles.boldLabel);

                var shape = kvp.Value;
                EditorGUILayout.LabelField($"  Head Scale: {shape.headScale}");
                EditorGUILayout.LabelField($"  Body Scale: {shape.bodyScale}");
                EditorGUILayout.LabelField($"  Height Bias: {shape.heightBias}");
                EditorGUILayout.LabelField($"  Frame: {shape.frameType}, Anim: {shape.animType}");
                EditorGUILayout.LabelField($"  Bone Scales: {shape.boneScales.Count}");
                EditorGUILayout.LabelField($"  Bone Offsets: {shape.boneOffsets.Count}");

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }

        private void DrawClothingView()
        {
            if (!dataLoaded || clothingCatalog == null)
            {
                EditorGUILayout.HelpBox("No data loaded. Press 'Load Data' first.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Gender:", GUILayout.Width(60));
            string newGender = EditorGUILayout.Popup(selectedGender == "m" ? 0 : 1, new[] { "Male", "Female" }, GUILayout.Width(100)) == 0 ? "m" : "f";

            if (newGender != selectedGender)
            {
                selectedGender = newGender;
                if (dataLoaded) LoadData(); // Reload for new gender
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            foreach (Slot slot in System.Enum.GetValues(typeof(Slot)))
            {
                if (!clothingCatalog.variantsBySlot.TryGetValue(slot, out var variants) || variants.Count == 0)
                    continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{slot} ({variants.Count} variants)", EditorStyles.boldLabel);

                foreach (var variant in variants.Take(10)) // Show first 10
                {
                    EditorGUILayout.LabelField($"  [{variant.ogIndex}] {variant.displayName}");
                }

                if (variants.Count > 10)
                {
                    EditorGUILayout.LabelField($"  ... and {variants.Count - 10} more", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            // Live preview section
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Live Preview", EditorStyles.boldLabel);
            selectedCharacter = EditorGUILayout.ObjectField("Character:", selectedCharacter, typeof(GameObject), true) as GameObject;

            if (selectedCharacter != null)
            {
                previewSlot = (Slot)EditorGUILayout.EnumPopup("Slot:", previewSlot);

                if (clothingCatalog.variantsBySlot.TryGetValue(previewSlot, out var slotVariants) && slotVariants.Count > 0)
                {
                    string[] variantNames = slotVariants.Select(v => v.displayName).ToArray();
                    previewVariantIdx = EditorGUILayout.Popup("Variant:", previewVariantIdx, variantNames);

                    if (GUILayout.Button("Apply to Character"))
                    {
                        ApplyPreviewToCharacter(slotVariants[previewVariantIdx]);
                    }
                }
            }
        }

        private void DrawPalettesView()
        {
            if (!dataLoaded || palettes == null)
            {
                EditorGUILayout.HelpBox("No data loaded. Press 'Load Data' first.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Skin Colors ({palettes.skin.Count})", EditorStyles.boldLabel);
            DrawColorPalette(palettes.skin);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField($"Hair Colors ({palettes.hair.Count})", EditorStyles.boldLabel);
            DrawColorPalette(palettes.hair);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField($"Dye Colors ({palettes.dye.Count})", EditorStyles.boldLabel);
            DrawColorPalette(palettes.dye);
        }

        private void DrawColorPalette(List<Color> colors)
        {
            int columns = 8;
            int rows = Mathf.CeilToInt((float)colors.Count / columns);

            for (int row = 0; row < rows; row++)
            {
                EditorGUILayout.BeginHorizontal();

                for (int col = 0; col < columns; col++)
                {
                    int idx = row * columns + col;

                    if (idx < colors.Count)
                    {
                        EditorGUI.DrawRect(GUILayoutUtility.GetRect(30, 30), colors[idx]);
                    }
                    else
                    {
                        GUILayout.Space(30);
                    }

                    GUILayout.Space(2);
                }

                EditorGUILayout.EndHorizontal();
                GUILayout.Space(2);
            }
        }

        private void DrawJewelryView()
        {
            if (!dataLoaded || jewelryTattoos == null)
            {
                EditorGUILayout.HelpBox("No data loaded. Press 'Load Data' first.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Jewelry Groups", EditorStyles.boldLabel);

            foreach (var kvp in jewelryTattoos.jewelryGroupsByZone)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{kvp.Key} ({kvp.Value.Count} items)", EditorStyles.boldLabel);

                foreach (var groupName in kvp.Value.Take(20))
                {
                    EditorGUILayout.LabelField($"  {groupName}", EditorStyles.miniLabel);
                }

                if (kvp.Value.Count > 20)
                {
                    EditorGUILayout.LabelField($"  ... and {kvp.Value.Count - 20} more", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tattoo Zones", EditorStyles.boldLabel);

            foreach (var kvp in jewelryTattoos.tattooZonesToBodyGroups)
            {
                EditorGUILayout.LabelField($"{kvp.Key}: {string.Join(", ", kvp.Value)}");
            }
        }

        private void DrawNPCsView()
        {
            if (!dataLoaded || npcDna == null)
            {
                EditorGUILayout.HelpBox("No data loaded. Press 'Load Data' first.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"NPCs ({npcDna.Count})", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            foreach (var kvp in npcDna.Take(50))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"{kvp.Value.name} ({kvp.Key})", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"  Gender: {kvp.Value.gender}, Shape: {kvp.Value.bodyShape}");
                EditorGUILayout.LabelField($"  Clothing: Hat={kvp.Value.hat}, Shirt={kvp.Value.shirt}, Pants={kvp.Value.pants}");
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }

            if (npcDna.Count > 50)
            {
                EditorGUILayout.LabelField($"... and {npcDna.Count - 50} more NPCs", EditorStyles.miniLabel);
            }
        }

        private void ApplyPreviewToCharacter(SlotVariant variant)
        {
            if (selectedCharacter == null)
                return;

            // This is a simplified preview - full implementation would use DnaApplier
            Debug.Log($"Apply {variant.displayName} to {selectedCharacter.name}");

            var cache = new GroupRendererCache(selectedCharacter);

            // Resolve OG patterns to exact names if not already done
            if (variant.showGroups.Count == 0 && variant.ogPatterns.Count > 0)
            {
                variant.showGroups = CharacterOG.Runtime.Utils.PatternResolver.ResolveToExact(cache, variant.ogPatterns);
            }

            // Enable exact group names only
            foreach (var groupName in variant.showGroups)
            {
                cache.EnableExact(groupName, true);
            }

            EditorUtility.SetDirty(selectedCharacter);
        }
    }
}
#endif
