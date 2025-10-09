/// <summary>
/// Helper editor script to spawn a player character using the NPC Creator system
/// Integrates with CustomNPCCreatorWindow to create randomized player models
/// </summary>
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using CharacterOG.Data.PureCSharpBackend;
using CharacterOG.Models;

namespace Player.Editor
{
    public static class PlayerSpawner
    {
        private const string MALE_MODEL_PATH = "phase_2/models/char/mp_2000";
        private const string FEMALE_MODEL_PATH = "phase_2/models/char/fp_2000";

        [MenuItem("POTCO/Player/Spawn Player Character (Random)")]
        public static void SpawnRandomPlayer()
        {
            Debug.Log("🎮 Spawning random player character...");

            // Create random DNA
            PirateDNA randomDna = CreateRandomDNA();

            // Spawn character model
            GameObject playerModel = SpawnCharacterModel(randomDna);

            if (playerModel == null)
            {
                Debug.LogError("❌ Failed to spawn player character model!");
                return;
            }

            // Set up player controller components
            SetupPlayerComponents(playerModel, randomDna);

            // Set up camera
            SetupPlayerCamera(playerModel);

            Debug.Log($"✅ Player character spawned: {playerModel.name}");
            Selection.activeGameObject = playerModel;
        }

        [MenuItem("POTCO/Player/Convert Selected NPC to Player")]
        public static void ConvertNPCToPlayer()
        {
            GameObject selected = Selection.activeGameObject;

            if (selected == null)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select an NPC character in the scene first.", "OK");
                return;
            }

            Debug.Log($"🔄 Converting '{selected.name}' to player character...");

            // Set up player controller components
            SetupPlayerComponents(selected);

            // Set up camera
            SetupPlayerCamera(selected);

            Debug.Log($"✅ Converted '{selected.name}' to player character");
        }

        private static PirateDNA CreateRandomDNA()
        {
            System.Random random = new System.Random();
            string gender = random.Next(2) == 0 ? "m" : "f";

            PirateDNA dna = new PirateDNA("Player", gender);

            // Randomize appearance
            dna.bodyShape = gender == "m" ? "MaleIdeal" : "FemaleIdeal";
            dna.bodyHeight = 0.5f + (float)random.NextDouble() * 0.5f; // 0.5 to 1.0
            dna.skinColorIdx = random.Next(8);
            dna.headTexture = random.Next(5);
            dna.eyeColorIdx = random.Next(6);

            // Randomize clothing
            dna.shirt = random.Next(1, 10);
            dna.pants = random.Next(1, 10);
            dna.shoes = random.Next(1, 5);
            dna.hair = random.Next(1, 15);
            dna.hairColorIdx = random.Next(12);

            // Randomize colors
            dna.topColorIdx = random.Next(20);
            dna.botColorIdx = random.Next(20);
            dna.hatColorIdx = random.Next(20);

            return dna;
        }

        private static GameObject SpawnCharacterModel(PirateDNA dna)
        {
            string modelPath = dna.gender == "f" ? FEMALE_MODEL_PATH : MALE_MODEL_PATH;
            GameObject modelPrefab = Resources.Load<GameObject>(modelPath);

            if (modelPrefab == null)
            {
                Debug.LogError($"❌ Could not load character model from Resources/{modelPath}");
                return null;
            }

            GameObject character = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
            if (character == null)
            {
                character = GameObject.Instantiate(modelPrefab);
            }

            character.name = "Player";

            // Position in front of scene view camera
            if (SceneView.lastActiveSceneView != null)
            {
                character.transform.position = SceneView.lastActiveSceneView.camera.transform.position +
                                               SceneView.lastActiveSceneView.camera.transform.forward * 3f;
            }

            return character;
        }

        private static void SetupPlayerComponents(GameObject playerObject)
        {
            SetupPlayerComponents(playerObject, null);
        }

        private static void SetupPlayerComponents(GameObject playerObject, PirateDNA dna)
        {
            // Add CharacterController if not present
            CharacterController controller = playerObject.GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = playerObject.AddComponent<CharacterController>();
                controller.height = 1.8f;
                controller.radius = 0.3f;
                controller.center = new Vector3(0f, 0.9f, 0f);
                Debug.Log("✅ Added CharacterController");
            }

            // Add Animation component for simple animation playback
            Animation animComponent = playerObject.GetComponent<Animation>();
            if (animComponent == null)
            {
                animComponent = playerObject.AddComponent<Animation>();
                Debug.Log("✅ Added Animation component");
            }

            // Add SimpleAnimationPlayer script
            Player.SimpleAnimationPlayer animPlayer = playerObject.GetComponent<Player.SimpleAnimationPlayer>();
            if (animPlayer == null)
            {
                animPlayer = playerObject.AddComponent<Player.SimpleAnimationPlayer>();

                // Manually set gender if DNA is provided
                if (dna != null)
                {
                    SerializedObject so = new SerializedObject(animPlayer);
                    so.FindProperty("manualGenderOverride").boolValue = true;

                    // Set gender enum: 0 = Male, 1 = Female
                    int genderValue = dna.gender == "f" ? 1 : 0;
                    so.FindProperty("manualGender").enumValueIndex = genderValue;

                    so.ApplyModifiedProperties();

                    string genderName = dna.gender == "f" ? "Female" : "Male";
                    Debug.Log($"✅ Added SimpleAnimationPlayer script with manual gender: {genderName}");
                }
                else
                {
                    Debug.Log("✅ Added SimpleAnimationPlayer script (auto-detects gender and loads animations)");
                }
            }

            // Add PlayerController script
            Player.PlayerController playerController = playerObject.GetComponent<Player.PlayerController>();
            if (playerController == null)
            {
                playerController = playerObject.AddComponent<Player.PlayerController>();
                Debug.Log("✅ Added PlayerController script");
            }

            EditorUtility.SetDirty(playerObject);
        }

        private static void SetupPlayerCamera(GameObject playerObject)
        {
            // Find or create camera
            Camera mainCamera = Camera.main;
            Player.PlayerCamera playerCamera = null;

            if (mainCamera != null)
            {
                playerCamera = mainCamera.GetComponent<Player.PlayerCamera>();
            }

            if (playerCamera == null)
            {
                // Create new camera object
                GameObject cameraObject = new GameObject("Player Camera");
                mainCamera = cameraObject.AddComponent<Camera>();
                playerCamera = cameraObject.AddComponent<Player.PlayerCamera>();

                // Position camera behind player
                cameraObject.transform.position = playerObject.transform.position + new Vector3(0f, 1.6f, -3.5f);
                cameraObject.transform.LookAt(playerObject.transform.position + Vector3.up * 1.5f);

                // Tag as main camera
                cameraObject.tag = "MainCamera";

                Debug.Log("✅ Created Player Camera");
            }

            // Set player as camera target
            SerializedObject so = new SerializedObject(playerCamera);
            SerializedProperty targetProp = so.FindProperty("target");
            targetProp.objectReferenceValue = playerObject.transform;
            so.ApplyModifiedProperties();

            Debug.Log("✅ Assigned player as camera target");
            EditorUtility.SetDirty(playerCamera);
        }
    }
}
#endif
