using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Toontown.Editor.Validation
{
    public static class ToontownSceneMaterialAuditRunner
    {
        private const string DemoScenePath = "Assets/Editor/Toontown/Samples/Generated/toontown_dna_mvp_demo.unity";

        [MenuItem("Toontown/Validation/Audit Current Scene Materials")]
        public static void Run()
        {
            string scenePath = EditorSceneManager.GetActiveScene().path;
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                Debug.LogWarning("No saved scene is open. Open a scene and run the audit again.");
                return;
            }

            var rendererList = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int totalMaterials = 0;
            int materialsMissingMainTex = 0;
            int renderersUsingMissingMainTex = 0;
            var missingMaterialNames = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            var missingRendererDetails = new List<string>();

            foreach (Renderer renderer in rendererList)
            {
                if (renderer == null)
                {
                    continue;
                }

                bool rendererHasMissing = false;
                var rendererMissingMaterials = new List<string>();
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat == null)
                    {
                        continue;
                    }

                    totalMaterials++;
                    if (!mat.HasProperty("_MainTex"))
                    {
                        continue;
                    }

                    Texture mainTex = mat.GetTexture("_MainTex");
                    if (mainTex != null)
                    {
                        continue;
                    }

                    rendererHasMissing = true;
                    materialsMissingMainTex++;
                    rendererMissingMaterials.Add(mat.name);

                    if (!missingMaterialNames.ContainsKey(mat.name))
                    {
                        missingMaterialNames[mat.name] = 0;
                    }
                    missingMaterialNames[mat.name]++;
                }

                if (rendererHasMissing)
                {
                    renderersUsingMissingMainTex++;
                    if (missingRendererDetails.Count < 25)
                    {
                        string objectPath = BuildHierarchyPath(renderer.transform);
                        string materialList = string.Join(", ", rendererMissingMaterials.Distinct());
                        missingRendererDetails.Add($"{objectPath} :: {materialList}");
                    }
                }
            }

            var report = new StringBuilder();
            report.AppendLine("Toontown Scene Material Audit");
            report.AppendLine($"Scene: {scenePath}");
            report.AppendLine($"Renderers: {rendererList.Length}");
            report.AppendLine($"Materials (referenced): {totalMaterials}");
            report.AppendLine($"Materials missing _MainTex: {materialsMissingMainTex}");
            report.AppendLine($"Renderers using missing _MainTex materials: {renderersUsingMissingMainTex}");

            if (missingMaterialNames.Count > 0)
            {
                report.AppendLine("Top missing _MainTex material names:");
                foreach (KeyValuePair<string, int> kvp in missingMaterialNames.OrderByDescending(kvp => kvp.Value).Take(25))
                {
                    report.AppendLine($"- {kvp.Key}: {kvp.Value}");
                }
            }

            if (missingRendererDetails.Count > 0)
            {
                report.AppendLine("Renderers using missing _MainTex materials:");
                foreach (string detail in missingRendererDetails)
                {
                    report.AppendLine($"- {detail}");
                }
            }

            Debug.Log(report.ToString());
        }

        private static string BuildHierarchyPath(Transform target)
        {
            if (target == null)
            {
                return "<missing>";
            }

            var parts = new Stack<string>();
            Transform current = target;
            while (current != null)
            {
                parts.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", parts);
        }

        // Used by batch mode:
        // -executeMethod Toontown.Editor.Validation.ToontownSceneMaterialAuditRunner.RunBatch
        public static void RunBatch()
        {
            if (!System.IO.File.Exists(DemoScenePath))
            {
                Debug.LogError($"Demo scene not found: {DemoScenePath}");
                EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Single);
            Run();
            EditorApplication.Exit(0);
        }
    }
}
