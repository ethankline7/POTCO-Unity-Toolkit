using UnityEngine;

namespace POTCO
{
    /// <summary>
    /// Spawn Node component for enemy/creature spawning points.
    /// Stores spawn configuration imported from POTCO world data.
    /// </summary>
    public class SpawnNode : MonoBehaviour
    {
        [Header("Spawn Configuration")]
        [Tooltip("Type of enemy/creature to spawn (e.g., 'Crab T1', 'Noob Navy')")]
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
