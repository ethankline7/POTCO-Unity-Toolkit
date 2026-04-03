using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Toontown.Editor
{
    internal static class ToontownPreviewLightingUtility
    {
        private const string PreviewSunName = "Toontown Preview Sun";

        [MenuItem("Toontown/World Data/Apply Toontown Preview Lighting")]
        public static void ApplyToActiveSceneMenu()
        {
            ApplyToActiveScene(verbose: true);
        }

        public static void ApplyToActiveScene(bool verbose)
        {
            Light previewSun = EnsurePreviewDirectionalLight();

            RenderSettings.sun = previewSun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.64f, 0.74f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.53f, 0.58f, 0.63f);
            RenderSettings.ambientGroundColor = new Color(0.37f, 0.34f, 0.30f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.67f, 0.76f, 0.86f);
            RenderSettings.fogStartDistance = 220f;
            RenderSettings.fogEndDistance = 980f;

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }

            SceneView.RepaintAll();

            if (verbose)
            {
                Debug.Log("Applied Toontown preview lighting (sun + ambient + fog) to active scene.");
            }
        }

        private static Light EnsurePreviewDirectionalLight()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            Light directional = FindDirectionalLight(activeScene);

            if (directional == null)
            {
                var lightObject = new GameObject(PreviewSunName);
                if (activeScene.IsValid())
                {
                    SceneManager.MoveGameObjectToScene(lightObject, activeScene);
                }

                Undo.RegisterCreatedObjectUndo(lightObject, "Create Toontown Preview Sun");
                directional = lightObject.AddComponent<Light>();
            }
            else
            {
                Undo.RecordObject(directional, "Configure Toontown Preview Sun");
            }

            directional.name = PreviewSunName;
            directional.type = LightType.Directional;
            directional.intensity = 1.25f;
            directional.color = new Color(1.0f, 0.98f, 0.94f);
            directional.shadows = LightShadows.Soft;
            directional.shadowStrength = 0.35f;
            directional.shadowBias = 0.05f;
            directional.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            return directional;
        }

        private static Light FindDirectionalLight(Scene scene)
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (Light light in lights)
            {
                if (light == null || light.type != LightType.Directional)
                {
                    continue;
                }

                if (!scene.IsValid() || light.gameObject.scene == scene)
                {
                    return light;
                }
            }

            return null;
        }
    }
}
