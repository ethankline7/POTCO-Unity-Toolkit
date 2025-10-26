using UnityEngine;

namespace Player
{
    /// <summary>
    /// Simple admin controller for NPC possession
    /// Press P to toggle admin mode, then K to possess nearest NPC
    /// </summary>
    public class AdminController : MonoBehaviour
    {
        [Header("Admin Settings")]
        [SerializeField] private KeyCode toggleAdminKey = KeyCode.P;
        [SerializeField] private KeyCode possessNearestKey = KeyCode.K;
        [SerializeField] private float possessionRange = 50f;

        private bool adminModeEnabled = false;
        private GameObject originalPlayer;
        private GameObject currentlyPossessedNPC = null;

        private void Awake()
        {
            originalPlayer = gameObject;
        }

        private void Update()
        {
            // Toggle admin mode or return to player
            if (Input.GetKeyDown(toggleAdminKey))
            {
                // If currently possessing an NPC, return to original player
                if (currentlyPossessedNPC != null)
                {
                    ReturnToOriginalPlayer();
                }
                else
                {
                    adminModeEnabled = !adminModeEnabled;
                    Debug.Log($"<color=cyan>Admin Mode: {(adminModeEnabled ? "ENABLED" : "DISABLED")}</color>");
                }
            }

            if (!adminModeEnabled) return;

            // Possess nearest NPC
            if (Input.GetKeyDown(possessNearestKey))
            {
                PossessNearestNPC();
            }
        }

        private void PossessNearestNPC()
        {
            // Find all NPCs in range
            POTCO.NPCController[] allNPCs = FindObjectsOfType<POTCO.NPCController>();
            POTCO.NPCController nearestNPC = null;
            float nearestDistance = possessionRange;

            foreach (POTCO.NPCController npc in allNPCs)
            {
                // Skip if this is already us
                if (npc.gameObject == gameObject) continue;

                float distance = Vector3.Distance(transform.position, npc.transform.position);
                if (distance < nearestDistance)
                {
                    nearestNPC = npc;
                    nearestDistance = distance;
                }
            }

            if (nearestNPC != null)
            {
                PossessNPC(nearestNPC.gameObject);
            }
            else
            {
                Debug.Log("<color=red>No NPCs found within possession range!</color>");
            }
        }

        private void PossessNPC(GameObject targetNPC)
        {
            Debug.Log($"<color=magenta>Possessing: {targetNPC.name}</color>");

            // FIRST: Disable original player's controller
            PlayerController originalPlayerController = originalPlayer.GetComponent<PlayerController>();
            if (originalPlayerController != null)
            {
                originalPlayerController.enabled = false;
                Debug.Log($"   Disabled original PlayerController");
            }

            // Disable NPC AI on target
            POTCO.NPCController npcController = targetNPC.GetComponent<POTCO.NPCController>();
            if (npcController != null)
            {
                npcController.enabled = false;
                Debug.Log($"   Disabled NPCController on {targetNPC.name}");
            }

            // Disable NPC-specific animation systems
            POTCO.NPCAnimationPlayer npcAnim = targetNPC.GetComponent<POTCO.NPCAnimationPlayer>();
            if (npcAnim != null)
            {
                npcAnim.enabled = false;
                Debug.Log($"   Disabled NPCAnimationPlayer on {targetNPC.name}");
            }

            // COPY PlayerController from original player (with all settings!)
            PlayerController playerController = targetNPC.GetComponent<PlayerController>();
            if (playerController == null)
            {
                playerController = targetNPC.AddComponent<PlayerController>();
                // Copy settings from original player's controller
                if (originalPlayerController != null)
                {
                    CopyComponentValues(originalPlayerController, playerController);
                    Debug.Log($"   Added PlayerController to {targetNPC.name} (copied from player)");
                }
            }
            else
            {
                playerController.enabled = true;
                Debug.Log($"   Enabled PlayerController on {targetNPC.name}");
            }

            // COPY SimpleAnimationPlayer from original player (with ALL settings like bone reset!)
            SimpleAnimationPlayer originalSimpleAnim = originalPlayer.GetComponent<SimpleAnimationPlayer>();
            SimpleAnimationPlayer simpleAnim = targetNPC.GetComponent<SimpleAnimationPlayer>();

            // Remove existing SimpleAnimationPlayer if it exists (start fresh)
            if (simpleAnim != null)
            {
                DestroyImmediate(simpleAnim);
                Debug.Log($"   Removed old SimpleAnimationPlayer from {targetNPC.name}");
            }

            // Add fresh SimpleAnimationPlayer and copy everything from player
            simpleAnim = targetNPC.AddComponent<SimpleAnimationPlayer>();
            if (originalSimpleAnim != null)
            {
                CopyComponentValues(originalSimpleAnim, simpleAnim);
                Debug.Log($"   Added SimpleAnimationPlayer to {targetNPC.name} (copied settings from player)");

                // Force Awake() to run
                System.Reflection.MethodInfo awakeMethod = typeof(SimpleAnimationPlayer).GetMethod("Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (awakeMethod != null)
                {
                    awakeMethod.Invoke(simpleAnim, null);
                }

                // Detect NPC's gender and set it BEFORE loading animations
                CharacterOG.Runtime.CharacterGenderData genderData = targetNPC.GetComponentInChildren<CharacterOG.Runtime.CharacterGenderData>();
                if (genderData != null)
                {
                    string npcGender = genderData.GetGender();
                    simpleAnim.SetGender(npcGender);
                }

                // Force Start() to run so it finds the Animation component
                System.Reflection.MethodInfo startMethod = typeof(SimpleAnimationPlayer).GetMethod("Start",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (startMethod != null)
                {
                    startMethod.Invoke(simpleAnim, null);
                    Debug.Log($"   Initialized SimpleAnimationPlayer (Animation component found)");
                }

                // Copy animation clips with correct gender prefix (uses NPC's gender, not player's)
                CopyAnimationClipsWithGender(originalSimpleAnim, simpleAnim, targetNPC);
            }

            // COPY WorldCollisionManager if player has it
            POTCO.WorldCollisionManager originalCollisionManager = originalPlayer.GetComponent<POTCO.WorldCollisionManager>();
            if (originalCollisionManager != null)
            {
                POTCO.WorldCollisionManager collisionManager = targetNPC.GetComponent<POTCO.WorldCollisionManager>();
                if (collisionManager == null)
                {
                    collisionManager = targetNPC.AddComponent<POTCO.WorldCollisionManager>();
                    Debug.Log($"   Added WorldCollisionManager to {targetNPC.name}");
                }
            }

            // Update camera to follow NPC
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                PlayerCamera playerCamera = mainCamera.GetComponent<PlayerCamera>();
                if (playerCamera != null)
                {
                    playerCamera.SetTarget(targetNPC.transform);
                    Debug.Log($"   Updated camera target to {targetNPC.name}");
                }
            }

            // Track the currently possessed NPC
            currentlyPossessedNPC = targetNPC;

            Debug.Log($"<color=green>Successfully possessed {targetNPC.name}!</color>");
            Debug.Log($"<color=cyan>All player components copied with exact settings!</color>");
        }

        private void ReturnToOriginalPlayer()
        {
            if (currentlyPossessedNPC == null)
            {
                Debug.LogWarning("No NPC is currently possessed!");
                return;
            }

            Debug.Log($"<color=magenta>Returning to original player from {currentlyPossessedNPC.name}</color>");

            // Re-enable original player's controller
            PlayerController originalPlayerController = originalPlayer.GetComponent<PlayerController>();
            if (originalPlayerController != null)
            {
                originalPlayerController.enabled = true;
                Debug.Log($"   Re-enabled original PlayerController");
            }

            // Remove/disable player components from NPC
            PlayerController npcPlayerController = currentlyPossessedNPC.GetComponent<PlayerController>();
            if (npcPlayerController != null)
            {
                Destroy(npcPlayerController);
                Debug.Log($"   Removed PlayerController from {currentlyPossessedNPC.name}");
            }

            // Remove SimpleAnimationPlayer from NPC
            SimpleAnimationPlayer npcSimpleAnim = currentlyPossessedNPC.GetComponent<SimpleAnimationPlayer>();
            if (npcSimpleAnim != null)
            {
                Destroy(npcSimpleAnim);
                Debug.Log($"   Removed SimpleAnimationPlayer from {currentlyPossessedNPC.name}");
            }

            // Remove WorldCollisionManager from NPC if it was added
            POTCO.WorldCollisionManager npcCollisionManager = currentlyPossessedNPC.GetComponent<POTCO.WorldCollisionManager>();
            if (npcCollisionManager != null)
            {
                Destroy(npcCollisionManager);
                Debug.Log($"   Removed WorldCollisionManager from {currentlyPossessedNPC.name}");
            }

            // Re-enable NPC's original components
            POTCO.NPCController npcController = currentlyPossessedNPC.GetComponent<POTCO.NPCController>();
            if (npcController != null)
            {
                npcController.enabled = true;
                Debug.Log($"   Re-enabled NPCController on {currentlyPossessedNPC.name}");
            }

            POTCO.NPCAnimationPlayer npcAnim = currentlyPossessedNPC.GetComponent<POTCO.NPCAnimationPlayer>();
            if (npcAnim != null)
            {
                npcAnim.enabled = true;
                Debug.Log($"   Re-enabled NPCAnimationPlayer on {currentlyPossessedNPC.name}");
            }

            // Return camera to original player
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                PlayerCamera playerCamera = mainCamera.GetComponent<PlayerCamera>();
                if (playerCamera != null)
                {
                    playerCamera.SetTarget(originalPlayer.transform);
                    Debug.Log($"   Returned camera to original player");
                }
            }

            Debug.Log($"<color=green>Successfully returned to original player!</color>");
            Debug.Log($"<color=cyan>{currentlyPossessedNPC.name} restored to default NPC behavior</color>");

            // Clear the possession tracking
            currentlyPossessedNPC = null;
        }

        /// <summary>
        /// Copy all serialized field values from one component to another using JSON
        /// This preserves all settings like bone reset, speeds, etc.
        /// </summary>
        private void CopyComponentValues<T>(T source, T destination) where T : Component
        {
            // Use JsonUtility to serialize and deserialize component data
            string json = JsonUtility.ToJson(source);
            JsonUtility.FromJsonOverwrite(json, destination);
        }

        /// <summary>
        /// Load and add animation clips with correct gender prefix for the NPC
        /// This ensures female NPCs get fp_ animations and male NPCs get mp_ animations
        /// </summary>
        private void CopyAnimationClipsWithGender(SimpleAnimationPlayer source, SimpleAnimationPlayer destination, GameObject targetNPC)
        {
            // Get the Animation component from destination
            var animComponentField = typeof(SimpleAnimationPlayer).GetField("animComponent",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Animation destAnimComp = (Animation)animComponentField.GetValue(destination);

            if (destAnimComp == null)
            {
                Debug.LogError("   ❌ Animation component not found on destination!");
                return;
            }

            // CRITICAL: Clear all existing animation clips (loaded by Start() with wrong gender)
            // We need to remove all clips so we can load fresh ones with correct gender prefix
            destAnimComp.Stop();

            // Collect all clips first (can't modify collection while iterating)
            System.Collections.Generic.List<AnimationClip> clipsToRemove = new System.Collections.Generic.List<AnimationClip>();
            foreach (AnimationState state in destAnimComp)
            {
                if (state.clip != null)
                {
                    clipsToRemove.Add(state.clip);
                }
            }

            // Now remove them
            foreach (AnimationClip clip in clipsToRemove)
            {
                destAnimComp.RemoveClip(clip);
            }

            // Get NPC's gender prefix
            var genderPrefixField = typeof(SimpleAnimationPlayer).GetField("genderPrefix",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            string genderPrefix = (string)genderPrefixField.GetValue(destination);

            // Load gender-specific animations from Resources
            string[] phases = { "phase_2", "phase_3", "phase_4", "phase_5", "phase_6" };
            string[] paths = { "char", "models/char" };

            // Get all AnimationClip fields from source to know what animations we need
            var fields = typeof(SimpleAnimationPlayer).GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic
            );

            int loadedCount = 0;
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(AnimationClip))
                {
                    // Get the animation name without gender prefix (e.g., "idle", "walk", "run")
                    string clipName = GetStandardClipName(field.Name);
                    // Keep underscores intact! Animation files are named like "fp_walk_back_diagonal_right"
                    string animName = clipName;

                    // Try to load with gender prefix
                    AnimationClip loadedClip = LoadAnimationClip(genderPrefix + animName, phases, paths);

                    if (loadedClip != null)
                    {
                        // Set the field value
                        field.SetValue(destination, loadedClip);

                        // FORCE REPLACE: Remove existing clip if it exists, then add the new gender-specific one
                        AnimationClip existingClip = destAnimComp.GetClip(clipName);
                        if (existingClip != null)
                        {
                            destAnimComp.RemoveClip(existingClip);
                        }

                        // Add the new gender-specific clip
                        destAnimComp.AddClip(loadedClip, clipName);
                        loadedCount++;
                    }
                    else
                    {
                        // Fallback: copy from player if gender-specific version doesn't exist
                        AnimationClip playerClip = (AnimationClip)field.GetValue(source);
                        if (playerClip != null)
                        {
                            field.SetValue(destination, playerClip);
                            if (!destAnimComp.GetClip(clipName))
                            {
                                destAnimComp.AddClip(playerClip, clipName);
                                loadedCount++;
                            }
                        }
                    }
                }
            }

            // Mark as initialized
            var isInitializedField = typeof(SimpleAnimationPlayer).GetField("isInitialized",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (isInitializedField != null)
            {
                isInitializedField.SetValue(destination, true);
            }
        }

        /// <summary>
        /// Load an animation clip from Resources with fallback through multiple phases and paths
        /// </summary>
        private AnimationClip LoadAnimationClip(string animName, string[] phases, string[] paths)
        {
            foreach (string phase in phases)
            {
                foreach (string path in paths)
                {
                    string fullPath = $"{phase}/{path}/{animName}";
                    AnimationClip clip = Resources.Load<AnimationClip>(fullPath);
                    if (clip != null)
                    {
                        return clip;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Convert field name to standard animation clip name
        /// e.g., "walkBackDiagonalLeftClip" -> "walk_back_diagonal_left"
        /// </summary>
        private string GetStandardClipName(string fieldName)
        {
            // Remove "Clip" suffix
            if (fieldName.EndsWith("Clip"))
            {
                fieldName = fieldName.Substring(0, fieldName.Length - 4);
            }

            // Convert camelCase to snake_case
            string result = "";
            for (int i = 0; i < fieldName.Length; i++)
            {
                char c = fieldName[i];
                if (char.IsUpper(c) && i > 0)
                {
                    result += "_";
                }
                result += char.ToLower(c);
            }

            return result;
        }

        private void OnGUI()
        {
            if (!adminModeEnabled && currentlyPossessedNPC == null) return;

            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.cyan;
            style.fontSize = 14;
            style.fontStyle = FontStyle.Bold;

            string controls;
            if (currentlyPossessedNPC != null)
            {
                controls = $"POSSESSING: {currentlyPossessedNPC.name}\n" +
                    $"[{toggleAdminKey}] Return to Original Player";
            }
            else
            {
                controls = "ADMIN MODE ACTIVE\n" +
                    $"[{possessNearestKey}] Possess Nearest NPC\n" +
                    $"[{toggleAdminKey}] Exit Admin Mode";
            }

            GUI.Label(new Rect(10, 10, 400, 100), controls, style);
        }
    }
}
