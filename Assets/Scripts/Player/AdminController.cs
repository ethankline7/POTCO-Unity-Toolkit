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

                // Force Start() to run so it finds the Animation component
                System.Reflection.MethodInfo startMethod = typeof(SimpleAnimationPlayer).GetMethod("Start",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (startMethod != null)
                {
                    startMethod.Invoke(simpleAnim, null);
                    Debug.Log($"   Initialized SimpleAnimationPlayer (Animation component found)");
                }

                // NOW copy all the animation clips (after Animation component is found)
                CopyAnimationClips(originalSimpleAnim, simpleAnim);
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
        /// Copy ALL animation clip references from player's SimpleAnimationPlayer to NPC's
        /// This includes diagonal walking, jumping, running, strafing, everything!
        /// </summary>
        private void CopyAnimationClips(SimpleAnimationPlayer source, SimpleAnimationPlayer destination)
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

            // Use reflection to copy all AnimationClip fields AND add them to Animation component
            var fields = typeof(SimpleAnimationPlayer).GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic
            );

            int copiedCount = 0;
            foreach (var field in fields)
            {
                // Copy AnimationClip references
                if (field.FieldType == typeof(AnimationClip))
                {
                    AnimationClip clipValue = (AnimationClip)field.GetValue(source);
                    if (clipValue != null)
                    {
                        // Set the field value
                        field.SetValue(destination, clipValue);

                        // Determine the standard name for this clip based on field name
                        string clipName = GetStandardClipName(field.Name);

                        // Add to Animation component if not already there
                        if (!destAnimComp.GetClip(clipName))
                        {
                            destAnimComp.AddClip(clipValue, clipName);
                            copiedCount++;
                            Debug.Log($"      Copied & added: {field.Name} → '{clipName}' ({clipValue.name})");
                        }
                    }
                }
            }

            Debug.Log($"   ✅ Copied {copiedCount} animation clips from player to NPC");

            // Mark as initialized so it doesn't try to load/map animations again
            var isInitializedField = typeof(SimpleAnimationPlayer).GetField("isInitialized",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (isInitializedField != null)
            {
                isInitializedField.SetValue(destination, true);
            }
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
