using UnityEngine;
using UnityEditor;
using POTCO.Sky;

public static class SkyMenuItems
{
    [MenuItem("POTCO/Create Sky", false, 100)]
    public static void CreateSky()
    {
        // Check if one already exists
        SkyboxManager existing = Object.FindObjectOfType<SkyboxManager>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            Debug.LogWarning("SkyboxManager already exists in scene. Selected existing instance.");
            return;
        }

        // Create new GameObject with SkyboxManager
        GameObject skyObj = new GameObject("POTCO Sky");
        SkyboxManager skyManager = skyObj.AddComponent<SkyboxManager>();

        // Auto-create material and load textures
        skyManager.CreateSkyboxMaterial();
        RenderSettings.skybox = skyManager.skyboxMaterial;
        DynamicGI.UpdateEnvironment();

        // Select it
        Selection.activeGameObject = skyObj;
        EditorGUIUtility.PingObject(skyObj);

        Debug.Log("POTCO Sky created! All textures loaded. Press Play to see cloud drift and change 'Current Preset' for different times of day.");
    }
}
