using UnityEngine;
using System.Collections.Generic;

namespace POTCO
{
    /// <summary>
    /// Spawn Node component for enemy/creature spawning points.
    /// Stores spawn configuration imported from POTCO world data.
    /// Spawns enemies/creatures at runtime based on spawnable type.
    /// FULLY DYNAMIC - Uses parsed data from AvatarTypes.py and EnemyGlobals.py
    /// </summary>
    [ExecuteAlways] // Run in both edit and play mode
    public class SpawnNode : MonoBehaviour
    {
        [Header("Spawn Configuration")]
        [Tooltip("Type of enemy/creature to spawn (e.g., 'Crab T1', 'Noob Navy', 'Alligator')")]
        public string spawnables;

        [Tooltip("Aggression radius - distance at which spawned entities become aggressive")]
        public float aggroRadius = 12f;

        [Tooltip("Patrol radius - area within which spawned entities patrol")]
        public float patrolRadius = 12f;

        [Tooltip("Initial behavior state (e.g., 'Idle', 'Patrol', 'Ambush')")]
        public string startState = "Idle";

        [Tooltip("Team ID for spawned entities")]
        public int teamId = 0;

        [Header("Spawn Timing")]
        [Tooltip("Spawn time begin (in hours, 0-24)")]
        public float spawnTimeBegin = 0f;

        [Tooltip("Spawn time end (in hours, 0-24)")]
        public float spawnTimeEnd = 0f;

        [Header("Runtime Spawning")]
        [Tooltip("Auto-spawn enemy/creature on Start")]
        public bool autoSpawn = true;

        [Tooltip("Number of enemies to spawn")]
        public int spawnCount = 1;

        [Header("Cached Type Info")]
        [Tooltip("Is this a creature type? (Set automatically during import)")]
        [SerializeField] private bool isCreatureType = false;

        [Tooltip("Creature species for model loading (e.g., 'alligator', 'crab')")]
        [SerializeField] private string creatureSpecies = "";

        [Tooltip("Creature model path from .py file (e.g., 'models/char/alligator_hi')")]
        [SerializeField] private string creatureModelPath = "";

        [Header("Editor Spawning")]
        [Tooltip("Has this spawn node already spawned its creatures?")]
        [SerializeField] private bool hasSpawned = false;

        // Spawned entity references
        private List<GameObject> spawnedEntities = new List<GameObject>();

        /// <summary>
        /// Set creature type flag, species, and model path (called during import from editor)
        /// </summary>
        public void SetCreatureInfo(bool isCreature, string species, string modelPath)
        {
            isCreatureType = isCreature;
            creatureSpecies = species;
            creatureModelPath = modelPath;
        }

        /// <summary>
        /// Set creature type flag (called during import from editor)
        /// </summary>
        public void SetIsCreatureType(bool value)
        {
            isCreatureType = value;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Manual spawn trigger for editor (right-click component → Force Spawn)
        /// </summary>
        [UnityEngine.ContextMenu("Force Spawn")]
        public void ForceSpawn()
        {
            Debug.Log($"[SpawnNode] Force spawn requested for '{gameObject.name}'");

            // Clear existing spawned entities
            ClearSpawnedEntities();

            // Reset flag and spawn
            hasSpawned = false;
            Start();
        }

        /// <summary>
        /// Clear spawned entities (right-click component → Clear Spawned)
        /// </summary>
        [UnityEngine.ContextMenu("Clear Spawned")]
        public void ClearSpawnedEntities()
        {
            Debug.Log($"[SpawnNode] Clearing spawned entities for '{gameObject.name}'");

            foreach (GameObject entity in spawnedEntities)
            {
                if (entity != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(entity);
                    }
                    else
                    {
                        DestroyImmediate(entity);
                    }
                }
            }

            spawnedEntities.Clear();
            hasSpawned = false;
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[SpawnNode] ✅ Cleared all spawned entities");
        }
#endif

        private void Start()
        {
            // Only spawn once (either in editor or play mode, but not both)
            if (hasSpawned)
            {
                Debug.Log($"[SpawnNode] Already spawned for '{gameObject.name}', skipping");
                return;
            }

            Debug.Log($"[SpawnNode] Start() called for '{gameObject.name}', autoSpawn={autoSpawn}, spawnables='{spawnables}'");
            Debug.Log($"[SpawnNode] Cached data: isCreatureType={isCreatureType}, creatureSpecies='{creatureSpecies}', creatureModelPath='{creatureModelPath}'");

            if (autoSpawn && !string.IsNullOrEmpty(spawnables))
            {
                SpawnEnemies();
                hasSpawned = true;
#if UNITY_EDITOR
                // Mark dirty to save hasSpawned flag
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
            else
            {
                Debug.LogWarning($"[SpawnNode] Not spawning: autoSpawn={autoSpawn}, spawnables empty={string.IsNullOrEmpty(spawnables)}");
            }
        }

        /// <summary>
        /// Spawn enemies/creatures based on spawnable type
        /// </summary>
        private void SpawnEnemies()
        {
            // Parse spawnable name (e.g., "Crab T1" -> "Crab")
            string baseSpawnable = GetBaseSpawnableName(spawnables);
            bool isCreature = IsCreatureType(baseSpawnable);

            Debug.Log($"[SpawnNode] ========================================");
            Debug.Log($"[SpawnNode] SpawnEnemies() called");
            Debug.Log($"[SpawnNode] Spawnables: '{spawnables}' -> Base: '{baseSpawnable}'");
            Debug.Log($"[SpawnNode] IsCreature: {isCreature} (cached: {isCreatureType})");
            Debug.Log($"[SpawnNode] Spawn Count: {spawnCount}");
            Debug.Log($"[SpawnNode] ========================================");

            for (int i = 0; i < spawnCount; i++)
            {
                GameObject spawned = null;

                if (isCreature)
                {
                    Debug.Log($"[SpawnNode] Attempting to spawn creature: {baseSpawnable}");
                    spawned = SpawnCreature(baseSpawnable);
                }
                else
                {
                    Debug.Log($"[SpawnNode] Attempting to spawn human enemy: {baseSpawnable}");
                    spawned = SpawnHumanEnemy(baseSpawnable);
                }

                if (spawned != null)
                {
                    spawnedEntities.Add(spawned);
                    Debug.Log($"[SpawnNode] ✅ Successfully spawned entity #{i}: {spawned.name}");

                    // Apply spawn point offset for multiple spawns
                    if (i > 0)
                    {
                        Vector3 offset = Random.insideUnitCircle * 2f;
                        spawned.transform.position += new Vector3(offset.x, 0, offset.y);
                    }
                }
                else
                {
                    Debug.LogError($"[SpawnNode] ❌ Failed to spawn entity #{i}");
                }
            }

            Debug.Log($"[SpawnNode] SpawnEnemies() complete. Total spawned: {spawnedEntities.Count}");
        }

        // Static caches to prevent redundant Resources.Load calls across multiple SpawnNodes
        private static Dictionary<string, GameObject> s_creaturePrefabCache = new Dictionary<string, GameObject>();
        private static Dictionary<string, AnimationClip> s_creatureAnimCache = new Dictionary<string, AnimationClip>();

        /// <summary>
        /// Spawn a creature (uses Animal AI system)
        /// Replicates the working logic from PropertyProcessor.SpawnCreature
        /// </summary>
        private GameObject SpawnCreature(string creatureName)
        {
            // Debug.Log($"[SpawnNode] SpawnCreature: {creatureName}");

            // Use cached model path from import (set by PropertyProcessor)
            if (string.IsNullOrEmpty(creatureModelPath)) return null;

            string species = string.IsNullOrEmpty(creatureSpecies) ? creatureName.ToLower() : creatureSpecies.ToLower();

            GameObject creaturePrefab = null;

            // CHECK CACHE FIRST
            if (!s_creaturePrefabCache.TryGetValue(creatureModelPath, out creaturePrefab))
            {
                // Try to load the model from Resources using cached path
                creaturePrefab = Resources.Load<GameObject>(creatureModelPath);

                if (creaturePrefab == null)
                {
                    // If not found, try adding phase prefixes (matches PropertyProcessor logic)
                    string[] phasePrefixes = new string[] { "", "phase_2/", "phase_3/", "phase_4/", "phase_5/", "phase_6/" };
                    foreach (string prefix in phasePrefixes)
                    {
                        string testPath = prefix + creatureModelPath;
                        creaturePrefab = Resources.Load<GameObject>(testPath);
                        if (creaturePrefab != null) break;
                    }
                }

                if (creaturePrefab != null)
                {
                    s_creaturePrefabCache[creatureModelPath] = creaturePrefab;
                }
            }

            if (creaturePrefab == null)
            {
                Debug.LogError($"[SpawnNode] ❌ FAILED to load creature model: {creatureModelPath}");
                return null;
            }

            // Instantiate creature (match PropertyProcessor instantiation)
            GameObject instance = null;

#if UNITY_EDITOR
            // In editor, use PrefabUtility to maintain prefab link
            if (!Application.isPlaying)
            {
                instance = UnityEditor.PrefabUtility.InstantiatePrefab(creaturePrefab) as GameObject;
            }
#endif

            // Fallback to regular Instantiate (play mode or if PrefabUtility failed)
            if (instance == null)
            {
                instance = Instantiate(creaturePrefab);
            }

            // Parent to this spawn node (using false to maintain world scale/rotation)
            instance.transform.SetParent(transform, false);

            // Reset local position to (0,0,0) - creature spawns at SpawnNode position
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // Add RuntimeAnimatorPlayer component to the instance
            RuntimeAnimatorPlayer animComponent = instance.GetComponent<RuntimeAnimatorPlayer>();
            if (animComponent == null)
            {
                animComponent = instance.AddComponent<RuntimeAnimatorPlayer>();
                animComponent.Initialize();
            }

            // Add AI components - pass the PARENT (this gameObject), not the instance
            AddCreatureAIComponents(gameObject, species);

            return instance;
        }

        /// <summary>
        /// Spawn a human enemy (uses NPC AI system)
        /// </summary>
        private GameObject SpawnHumanEnemy(string enemyName)
        {
            // TODO: Implement human enemy spawning using DNA system
            GameObject enemyParent = new GameObject(enemyName);
            enemyParent.transform.SetParent(transform);
            enemyParent.transform.position = transform.position;

            return enemyParent;
        }

        /// <summary>
        /// Add AI components to spawned creature
        /// </summary>
        private void AddCreatureAIComponents(GameObject parentNode, string species)
        {
            // Find the creature model (first child of parent)
            GameObject creatureModel = null;
            if (parentNode.transform.childCount > 0)
            {
                creatureModel = parentNode.transform.GetChild(0).gameObject;
            }
            else
            {
                return;
            }

            // Ensure RuntimeAnimatorPlayer component exists on creature model
            RuntimeAnimatorPlayer animComponent = null;
            if (creatureModel != null)
            {
                animComponent = creatureModel.GetComponent<RuntimeAnimatorPlayer>();
                if (animComponent == null)
                {
                    animComponent = creatureModel.AddComponent<RuntimeAnimatorPlayer>();
                    animComponent.Initialize();
                }

                LoadCreatureAnimations(animComponent, species);
            }

            // Add NPCData component to parent node
            NPCData npcData = parentNode.GetComponent<NPCData>();
            if (npcData == null)
            {
                npcData = parentNode.AddComponent<NPCData>();
            }

            // Configure NPC data
            npcData.npcId = $"{spawnables}_{gameObject.name}";
            npcData.category = "Animal";
            npcData.team = "Animal";
            npcData.startState = string.IsNullOrEmpty(startState) ? "LandRoam" : startState;
            npcData.patrolRadius = patrolRadius;
            npcData.aggroRadius = 0f;
            npcData.animSet = species.ToLower();

            // Add CharacterController
            CharacterController controller = parentNode.GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = parentNode.AddComponent<CharacterController>();
                controller.radius = 0.5f;
                controller.height = 1.5f;
                controller.center = new Vector3(0, 0.75f, 0);
            }

            // Add NPCController
            NPCController npcController = parentNode.GetComponent<NPCController>();
            if (npcController == null)
            {
                npcController = parentNode.AddComponent<NPCController>();
            }

            // Enable patrol
            var enablePatrolField = typeof(NPCController).GetField("enablePatrol",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (enablePatrolField != null)
            {
                enablePatrolField.SetValue(npcController, true);
            }

            npcController.enabled = true;

            // Add AnimalAnimationPlayer
            if (creatureModel != null)
            {
                AnimalAnimationPlayer animalAnimPlayer = creatureModel.GetComponent<AnimalAnimationPlayer>();
                if (animalAnimPlayer == null)
                {
                    animalAnimPlayer = creatureModel.AddComponent<AnimalAnimationPlayer>();
                    string animPrefix = species.ToLower();
                    animPrefix = System.Text.RegularExpressions.Regex.Replace(animPrefix, "_hi$|_lo$|_mid$", "");
                    animalAnimPlayer.animationPrefix = animPrefix;
                    animalAnimPlayer.currentState = string.IsNullOrEmpty(startState) ? "LandRoam" : startState;
                }
            }
        }

        /// <summary>
        /// Load animation clips for creature's Animation component from Resources
        /// Matches PropertyProcessor.LoadCreatureAnimations logic
        /// </summary>
        private void LoadCreatureAnimations(RuntimeAnimatorPlayer animComponent, string species)
        {
            if (animComponent == null) return;

            // Get the base model name without LOD suffix (e.g., "alligator_hi" -> "alligator")
            string baseModelName = System.Text.RegularExpressions.Regex.Replace(species, "_hi$|_lo$|_mid$", "");

            // Common animation names for creatures
            string[] commonAnimations = new string[]
            {
                "idle", "walk", "run", "swim", "eat", "sleep", "attack", "hit", "death"
            };

            foreach (string animName in commonAnimations)
            {
                // Animation files are at: phase_#/models/char/alligator_idle.egg
                string clipName = $"{baseModelName}_{animName}";
                string cacheKey = $"{species}_{animName}"; // Use species as key base for safety

                AnimationClip clip = null;

                if (!s_creatureAnimCache.TryGetValue(cacheKey, out clip))
                {
                    string[] pathsToTry = new string[]
                    {
                        $"phase_4/models/char/{baseModelName}_{animName}",
                        $"phase_3/models/char/{baseModelName}_{animName}",
                        $"phase_5/models/char/{baseModelName}_{animName}",
                        $"phase_2/models/char/{baseModelName}_{animName}",
                        $"phase_6/models/char/{baseModelName}_{animName}",
                    };

                    foreach (string path in pathsToTry)
                    {
                        clip = Resources.Load<AnimationClip>(path);
                        if (clip != null) break;
                    }

                    if (clip != null)
                    {
                        s_creatureAnimCache[cacheKey] = clip;
                    }
                }

                if (clip != null)
                {
                    animComponent.AddClip(clip, clipName);
                    animComponent.SetWrapMode(clipName, WrapMode.Loop);
                }
            }
        }

        /// <summary>
        /// Check if spawnable is a creature type (uses Animal AI)
        /// Uses cached value set during import
        /// </summary>
        private bool IsCreatureType(string spawnableName)
        {
            // Use the cached value that was set during import
            return isCreatureType;
        }

        /// <summary>
        /// Get base spawnable name from spawn string (e.g., "Crab T1" -> "Crab")
        /// </summary>
        private string GetBaseSpawnableName(string spawnableName)
        {
            if (string.IsNullOrEmpty(spawnableName))
                return "";

            // Remove tier indicators (T1, T2, etc.)
            string[] parts = spawnableName.Split(' ');
            return parts[0];
        }

        /// <summary>
        /// Convert team ID to team name
        /// </summary>
        private string GetTeamName(int teamId)
        {
            switch (teamId)
            {
                case 0: return "default";
                case 1: return "Villager";
                case 2: return "Navy";
                case 3: return "EITC";
                case 4: return "Undead";
                default: return "default";
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Draw gizmos in editor to visualize spawn area
        /// </summary>
        private void OnDrawGizmos()
        {
            // Draw patrol radius as green wire sphere
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, patrolRadius);

            // Draw aggro radius as red wire sphere
            if (aggroRadius > 0)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
                Gizmos.DrawWireSphere(transform.position, aggroRadius);
            }

            // Draw center point
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.position, 0.3f);
        }

        /// <summary>
        /// Draw labels in scene view
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            // Draw detailed info when selected
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
                $"Spawn Node\n{spawnables}\nTeam: {teamId}\nState: {startState}");
        }
#endif
    }
}
