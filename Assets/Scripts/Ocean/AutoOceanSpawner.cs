using UnityEngine;

namespace POTCO.Ocean
{
    /// <summary>
    /// Automatically spawns ocean system when Play mode starts if a player controller exists.
    /// No manual setup required - completely automatic!
    /// </summary>
    public class AutoOceanSpawner
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSpawnOcean()
        {
            // Check if ocean already exists
            if (Object.FindAnyObjectByType<OceanFollowController>() != null)
            {
                Debug.Log("AutoOceanSpawner: Ocean system already exists, skipping auto-spawn.");
                return;
            }

            // Look for Player Camera with MainCamera tag
            GameObject playerCamera = GameObject.Find("Player Camera");
            Camera mainCam = null;

            if (playerCamera != null)
            {
                mainCam = playerCamera.GetComponent<Camera>();
                if (mainCam != null && mainCam.CompareTag("MainCamera"))
                {
                    Debug.Log("AutoOceanSpawner: Found Player Camera with MainCamera tag, spawning ocean!");
                    CreateOceanSystem();
                    return;
                }
            }

            // Fallback: Check for any camera with MainCamera tag
            mainCam = Camera.main;
            if (mainCam != null)
            {
                Debug.Log("AutoOceanSpawner: Found MainCamera, spawning ocean!");
                CreateOceanSystem();
                return;
            }

            Debug.Log("AutoOceanSpawner: No Player Camera or MainCamera detected, skipping ocean spawn.");
        }

        static void CreateOceanSystem()
        {
            // Create ocean system object
            GameObject oceanSystem = new GameObject("OceanSystem_Auto");
            oceanSystem.transform.position = Vector3.zero;

            // Add controller component
            OceanFollowController controller = oceanSystem.AddComponent<OceanFollowController>();
            controller.patchSize = 100f;
            controller.updateInterval = 2;
            controller.autoTrackMainCamera = true;

            // Find Player Camera or MainCamera for follow target
            GameObject playerCamera = GameObject.Find("Player Camera");
            if (playerCamera != null)
            {
                Camera cam = playerCamera.GetComponent<Camera>();
                if (cam != null)
                {
                    controller.followTarget = playerCamera.transform;
                    Debug.Log("AutoOceanSpawner: Ocean following Player Camera");
                }
            }

            // Fallback to Camera.main
            if (controller.followTarget == null && Camera.main != null)
            {
                controller.followTarget = Camera.main.transform;
                Debug.Log("AutoOceanSpawner: Ocean following MainCamera");
            }

            Debug.Log("AutoOceanSpawner: Successfully created ocean system at Y=0! Ocean will follow camera and auto-configure.");
        }
    }
}
