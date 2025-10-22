using UnityEngine;
using UnityEditor;
using Player;

namespace PlayerEditor
{
    /// <summary>
    /// Custom editor for PlayerController that ensures required components are attached
    /// </summary>
    [CustomEditor(typeof(PlayerController))]
    public class PlayerControllerEditor : Editor
    {
        private void OnEnable()
        {
            // Ensure WorldCollisionManager is attached
            PlayerController playerController = (PlayerController)target;

            if (playerController.GetComponent<POTCO.WorldCollisionManager>() == null)
            {
                playerController.gameObject.AddComponent<POTCO.WorldCollisionManager>();
                Debug.Log("✅ Auto-attached WorldCollisionManager to PlayerController in Edit mode");
            }
        }
    }
}
