/// <summary>
/// Editor window for previewing and applying NPC DNA to characters.
/// Window → POTCO → NPC Preview
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
    public class NPCPreviewWindow : EditorWindow
    {
        private IOgDataSource dataSource;

        private Dictionary<string, PirateDNA> npcDatabase;
        private Dictionary<string, BodyShapeDef> bodyShapes;
        private ClothingCatalog clothingCatalog;
        private Palettes palettes;
        private JewelryTattooDefs jewelryTattoos;

        private Vector2 scrollPos;
        private string searchFilter = "";
        private bool dataLoaded = false;
        private string loadError;

        private GameObject selectedCharacter;
        private Transform headRoot;
        private Transform bodyRoot;

        private string selectedNpcId;
        private PirateDNA selectedNpcDna;

        // Auto-spawn settings
        private const string MALE_MODEL_PATH = "phase_2/models/char/mp_2000";
        private const string FEMALE_MODEL_PATH = "phase_2/models/char/fp_2000";

        [MenuItem("Window/POTCO/NPC Preview")]
        public static void ShowWindow()
        {
            var window = GetWindow<NPCPreviewWindow>("NPC Preview");
            window.minSize = new Vector2(700, 500);
        }

        private void OnEnable()
        {
            // Auto-load database when window opens
            if (!dataLoaded)
            {
                LoadNPCDatabase();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("POTCO NPC Preview & Applier", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            // Load controls
            if (GUILayout.Button("Reload NPC Database"))
            {
                LoadNPCDatabase();
            }

            if (!dataLoaded)
            {
                if (!string.IsNullOrEmpty(loadError))
                {
                    EditorGUILayout.HelpBox($"Load Error: {loadError}", MessageType.Error);
                }
                else
                {
                    EditorGUILayout.HelpBox("Press 'Load NPC Database' to begin.", MessageType.Info);
                }
                return;
            }

            EditorGUILayout.Space();

            // NPC search and list
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(60));
            searchFilter = EditorGUILayout.TextField(searchFilter);

            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                searchFilter = "";
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // NPC list
            EditorGUILayout.LabelField($"NPCs ({GetFilteredNPCs().Count()})", EditorStyles.boldLabel);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(300));

            foreach (var kvp in GetFilteredNPCs().Take(100))
            {
                DrawNPCEntry(kvp.Key, kvp.Value);
            }

            EditorGUILayout.EndScrollView();

            // Selected NPC details
            if (selectedNpcDna != null)
            {
                EditorGUILayout.Space();
                DrawSelectedNPCDetails();
            }

            // Status bar
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"✓ Loaded {npcDatabase?.Count ?? 0} NPCs", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void LoadNPCDatabase()
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

                // Load all required data
                npcDatabase = dataSource.LoadNpcDna();
                bodyShapes = new Dictionary<string, BodyShapeDef>();

                // Load both male and female body shapes
                foreach (var shape in dataSource.LoadBodyShapes("m"))
                    bodyShapes[shape.Key] = shape.Value;

                foreach (var shape in dataSource.LoadBodyShapes("f"))
                    if (!bodyShapes.ContainsKey(shape.Key))
                        bodyShapes[shape.Key] = shape.Value;

                clothingCatalog = dataSource.LoadClothingCatalog("m");
                palettes = dataSource.LoadPalettesAndDyeRules();
                jewelryTattoos = dataSource.LoadJewelryAndTattoos("m");

                dataLoaded = true;
                loadError = null;
                Debug.Log($"NPC Preview: Loaded {npcDatabase.Count} NPCs");
            }
            catch (System.Exception ex)
            {
                loadError = ex.Message;
                dataLoaded = false;
                Debug.LogError($"NPC Preview Load Error: {ex}");
            }
        }

        private IEnumerable<KeyValuePair<string, PirateDNA>> GetFilteredNPCs()
        {
            if (npcDatabase == null)
                return System.Linq.Enumerable.Empty<KeyValuePair<string, PirateDNA>>();

            if (string.IsNullOrWhiteSpace(searchFilter))
                return npcDatabase;

            string filter = searchFilter.ToLower();
            return npcDatabase.Where(kvp =>
                kvp.Key.ToLower().Contains(filter) ||
                kvp.Value.name.ToLower().Contains(filter));
        }

        private void DrawNPCEntry(string npcId, PirateDNA dna)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(dna.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{dna.gender} | {dna.bodyShape} | ID: {npcId}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                selectedNpcId = npcId;
                selectedNpcDna = dna;
            }

            if (GUILayout.Button("Spawn", GUILayout.Width(60)))
            {
                SpawnAndApplyNPC(dna, npcId);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectedNPCDetails()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Selected NPC: {selectedNpcDna.name}", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField($"ID: {selectedNpcId}");
            EditorGUILayout.LabelField($"Gender: {NameMap.GetGenderDisplayName(selectedNpcDna.gender)}");
            EditorGUILayout.LabelField($"Body Shape: {selectedNpcDna.bodyShape}");
            EditorGUILayout.LabelField($"Height: {selectedNpcDna.bodyHeight:F2}");
            EditorGUILayout.LabelField($"Skin Color: {selectedNpcDna.skinColorIdx}");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Clothing:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"  Hat: {selectedNpcDna.hat} (tex:{selectedNpcDna.hatTex}, color:{selectedNpcDna.hatColorIdx})");
            EditorGUILayout.LabelField($"  Shirt: {selectedNpcDna.shirt} (tex:{selectedNpcDna.shirtTex}, color:{selectedNpcDna.topColorIdx})");
            EditorGUILayout.LabelField($"  Vest: {selectedNpcDna.vest} (tex:{selectedNpcDna.vestTex})");
            EditorGUILayout.LabelField($"  Coat: {selectedNpcDna.coat} (tex:{selectedNpcDna.coatTex})");
            EditorGUILayout.LabelField($"  Pants: {selectedNpcDna.pants} (tex:{selectedNpcDna.pantsTex}, color:{selectedNpcDna.botColorIdx})");
            EditorGUILayout.LabelField($"  Shoes: {selectedNpcDna.shoes} (tex:{selectedNpcDna.shoesTex})");
            EditorGUILayout.LabelField($"  Belt: {selectedNpcDna.belt} (tex:{selectedNpcDna.beltTex})");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Hair: {selectedNpcDna.hair} (color:{selectedNpcDna.hairColorIdx})");
            EditorGUILayout.LabelField($"Beard: {selectedNpcDna.beard}");
            EditorGUILayout.LabelField($"Mustache: {selectedNpcDna.mustache}");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Jewelry Zones: {selectedNpcDna.jewelry.Count}");
            EditorGUILayout.LabelField($"Tattoos: {selectedNpcDna.tattoos.Count}");

            EditorGUILayout.Space();

            if (GUILayout.Button("Spawn NPC in Scene", GUILayout.Height(30)))
            {
                SpawnAndApplyNPC(selectedNpcDna, selectedNpcId);
            }

            EditorGUILayout.EndVertical();
        }

        private void SpawnAndApplyNPC(PirateDNA dna, string npcId)
        {
            try
            {
                // Determine model path based on gender
                string modelPath = dna.gender.ToLower() == "f" ? FEMALE_MODEL_PATH : MALE_MODEL_PATH;

                // Load model asset
                GameObject modelPrefab = Resources.Load<GameObject>(modelPath);

                if (modelPrefab == null)
                {
                    EditorUtility.DisplayDialog("Error", $"Could not load character model at:\nResources/{modelPath}\n\nMake sure the .egg file has been imported.", "OK");
                    Debug.LogError($"Failed to load character model: Resources/{modelPath}");
                    return;
                }

                // Instantiate in scene
                GameObject character = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
                if (character == null)
                {
                    character = GameObject.Instantiate(modelPrefab);
                }

                // Name it after the NPC
                character.name = $"NPC_{dna.name}_{npcId}";

                // Position at scene view camera (or origin)
                if (SceneView.lastActiveSceneView != null)
                {
                    character.transform.position = SceneView.lastActiveSceneView.camera.transform.position + SceneView.lastActiveSceneView.camera.transform.forward * 3f;
                }

                // Auto-find head and body roots
                Transform headRoot = null;
                Transform bodyRoot = null;

                Transform[] allTransforms = character.GetComponentsInChildren<Transform>();

                string[] headCandidates = { "def_head", "Head", "head", "HeadRoot" };
                foreach (var candidate in headCandidates)
                {
                    var found = System.Array.Find(allTransforms, t => t.name == candidate);
                    if (found != null)
                    {
                        headRoot = found;
                        break;
                    }
                }

                string[] bodyCandidates = { "def_spine01", "Spine", "spine01", "BodyRoot", "def_spine02" };
                foreach (var candidate in bodyCandidates)
                {
                    var found = System.Array.Find(allTransforms, t => t.name == candidate);
                    if (found != null)
                    {
                        bodyRoot = found;
                        break;
                    }
                }

                Debug.Log($"Auto-Find Roots: Head={headRoot?.name ?? "not found"}, Body={bodyRoot?.name ?? "not found"}");

                // Load clothing catalog for the correct gender
                ClothingCatalog genderClothing = dataSource.LoadClothingCatalog(dna.gender);
                JewelryTattooDefs genderJewelry = dataSource.LoadJewelryAndTattoos(dna.gender);

                // Create DnaApplier
                var dnaApplier = new DnaApplier(
                    character,
                    bodyShapes,
                    genderClothing,
                    palettes,
                    genderJewelry,
                    headRoot,
                    bodyRoot
                );

                // Apply DNA
                dnaApplier.ApplyDNA(dna);

                // Select the spawned character
                Selection.activeGameObject = character;

                // Mark scene dirty
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(character.scene);

                Debug.Log($"Successfully spawned and applied NPC '{dna.name}' ({dna.gender}) at {character.transform.position}");
                EditorUtility.DisplayDialog("Success", $"Spawned NPC '{dna.name}' in scene!\n\nGender: {dna.gender}\nModel: {modelPath}", "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to spawn NPC: {ex}");
                EditorUtility.DisplayDialog("Error", $"Failed to spawn NPC:\n{ex.Message}", "OK");
            }
        }

        private void ApplyNPCToCharacter(PirateDNA dna)
        {
            if (selectedCharacter == null)
            {
                EditorUtility.DisplayDialog("Error", "No character selected. Please assign a character GameObject first.", "OK");
                return;
            }

            try
            {
                // Create DnaApplier
                var dnaApplier = new DnaApplier(
                    selectedCharacter,
                    bodyShapes,
                    clothingCatalog,
                    palettes,
                    jewelryTattoos,
                    headRoot,
                    bodyRoot
                );

                // Apply DNA
                dnaApplier.ApplyDNA(dna);

                Debug.Log($"Successfully applied NPC '{dna.name}' to {selectedCharacter.name}");
                EditorUtility.DisplayDialog("Success", $"Applied NPC '{dna.name}' to {selectedCharacter.name}", "OK");

                EditorUtility.SetDirty(selectedCharacter);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to apply NPC: {ex}");
                EditorUtility.DisplayDialog("Error", $"Failed to apply NPC:\n{ex.Message}", "OK");
            }
        }

        private void AutoFindRoots()
        {
            if (selectedCharacter == null)
                return;

            // Try to find common head/body root names
            Transform[] allTransforms = selectedCharacter.GetComponentsInChildren<Transform>();

            // Head root candidates
            string[] headCandidates = { "def_head", "Head", "head", "HeadRoot" };
            foreach (var candidate in headCandidates)
            {
                var found = System.Array.Find(allTransforms, t => t.name == candidate);
                if (found != null)
                {
                    headRoot = found;
                    break;
                }
            }

            // Body root candidates
            string[] bodyCandidates = { "def_spine01", "Spine", "spine01", "BodyRoot", "def_spine02" };
            foreach (var candidate in bodyCandidates)
            {
                var found = System.Array.Find(allTransforms, t => t.name == candidate);
                if (found != null)
                {
                    bodyRoot = found;
                    break;
                }
            }

            Debug.Log($"Auto-Find: Head={headRoot?.name ?? "not found"}, Body={bodyRoot?.name ?? "not found"}");
        }
    }
}
#endif
