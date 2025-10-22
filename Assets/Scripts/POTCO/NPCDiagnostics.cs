using UnityEngine;

namespace POTCO
{
    /// <summary>
    /// Diagnostic component to help debug NPC issues
    /// Attach this to an NPC in the scene to see detailed info
    /// </summary>
    public class NPCDiagnostics : MonoBehaviour
    {
        [Header("Run Diagnostics")]
        [SerializeField] private bool runOnStart = true;

        private void Start()
        {
            if (runOnStart)
            {
                RunDiagnostics();
            }
        }

        [ContextMenu("Run Diagnostics")]
        public void RunDiagnostics()
        {
            Debug.Log($"========== NPC DIAGNOSTICS: {gameObject.name} ==========");

            // Check components
            Debug.Log($"--- COMPONENTS ---");
            var npcData = GetComponent<NPCData>();
            var npcController = GetComponent<NPCController>();
            var npcAnimPlayer = GetComponent<NPCAnimationPlayer>();
            var charController = GetComponent<CharacterController>();

            Debug.Log($"NPCData: {(npcData != null ? "✓" : "✗")}");
            Debug.Log($"NPCController: {(npcController != null ? "✓" : "✗")}");
            Debug.Log($"NPCAnimationPlayer: {(npcAnimPlayer != null ? "✓" : "✗")}");
            Debug.Log($"CharacterController: {(charController != null ? "✓" : "✗")}");

            // Check for Animation component
            Debug.Log($"--- ANIMATION COMPONENT ---");
            Animation anim = GetComponent<Animation>();
            if (anim == null)
            {
                anim = GetComponentInChildren<Animation>();
                if (anim != null)
                {
                    Debug.Log($"Animation component found on child: {anim.gameObject.name}");
                }
                else
                {
                    Debug.LogError($"❌ NO Animation component found!");
                }
            }
            else
            {
                Debug.Log($"Animation component found on this GameObject");
            }

            if (anim != null)
            {
                Debug.Log($"Animation clip count: {anim.GetClipCount()}");
                foreach (AnimationState state in anim)
                {
                    Debug.Log($"  - Clip: {state.name} (Length: {state.length}s)");
                }

                Debug.Log($"Is Playing: {anim.isPlaying}");
                if (anim.isPlaying)
                {
                    Debug.Log($"Current clip: {anim.clip?.name}");
                }
            }

            // Check hierarchy
            Debug.Log($"--- HIERARCHY ---");
            Debug.Log($"Child count: {transform.childCount}");
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                Debug.Log($"  Child {i}: {child.name}");

                // Check for SkinnedMeshRenderer
                var smr = child.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    Debug.Log($"    - Has SkinnedMeshRenderer on: {smr.gameObject.name}");
                }

                // Check for Animation on child
                var childAnim = child.GetComponent<Animation>();
                if (childAnim != null)
                {
                    Debug.Log($"    - Has Animation component with {childAnim.GetClipCount()} clips");
                }
            }

            // Check NPCData values
            if (npcData != null)
            {
                Debug.Log($"--- NPC DATA ---");
                Debug.Log($"Category: {npcData.category}");
                Debug.Log($"Team: {npcData.team}");
                Debug.Log($"AnimSet: {npcData.animSet}");
                Debug.Log($"Greeting Animation: {npcData.greetingAnimation}");
                Debug.Log($"Notice Animation 1: {npcData.noticeAnimation1}");
                Debug.Log($"Patrol Radius: {npcData.patrolRadius}");
            }

            // Check CharacterGenderData
            var genderData = GetComponentInChildren<CharacterOG.Runtime.CharacterGenderData>();
            if (genderData != null)
            {
                Debug.Log($"--- GENDER DATA ---");
                Debug.Log($"Gender: {genderData.GetGender()}");
                Debug.Log($"Gender Prefix: {genderData.GetGenderPrefix()}");
            }
            else
            {
                Debug.LogWarning($"⚠️ No CharacterGenderData found!");
            }

            Debug.Log($"========== END DIAGNOSTICS ==========");
        }
    }
}
