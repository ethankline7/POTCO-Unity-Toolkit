using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using POTCO;
using Toolkit.Editor.WorldData.Contracts;
using UnityEditor;
using UnityEngine;
using WorldDataImporter.Utilities;

namespace Toontown.Editor
{
    public static class ToontownSceneDocumentImporter
    {
        private static readonly Regex NumberRegex = new Regex(
            @"[-+]?\d*\.?\d+(?:[eE][-+]?\d+)?",
            RegexOptions.Compiled);

        private static readonly Regex PhasePrefixRegex = new Regex(
            @"^phase_\d+(?:\.\d+)?/",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static ToontownSceneImportResult ImportDocument(
            WorldDataDocument document,
            ToontownSceneImportSettings settings)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (settings == null)
            {
                settings = new ToontownSceneImportSettings();
            }

            string rootName = string.IsNullOrWhiteSpace(settings.RootObjectName)
                ? $"ToontownDNA_{document.Name}"
                : settings.RootObjectName.Trim();

            GameObject root = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(root, "Import Toontown DNA Scene");

            var createdObjects = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            foreach (WorldDataObject obj in document.Objects)
            {
                var go = new GameObject(obj.Id);
                Undo.RegisterCreatedObjectUndo(go, "Create DNA Object");
                createdObjects[obj.Id] = go;
            }

            var result = new ToontownSceneImportResult
            {
                RootObjectName = root.name,
                TotalDocumentObjects = document.Objects.Count
            };

            foreach (WorldDataObject obj in document.Objects)
            {
                GameObject go = createdObjects[obj.Id];
                GameObject parent = root;
                if (!string.IsNullOrWhiteSpace(obj.ParentId) &&
                    createdObjects.TryGetValue(obj.ParentId, out GameObject parentObject))
                {
                    parent = parentObject;
                }

                go.transform.SetParent(parent.transform, false);
                ApplyTransform(go.transform, obj.Properties);
                string modelPath = ResolveModelPath(obj.Properties);
                bool modelInstantiated = false;
                if (!string.IsNullOrWhiteSpace(modelPath))
                {
                    GameObject modelInstance = AssetUtilities.InstantiatePrefab(
                        modelPath,
                        go,
                        settings.UseEggFiles,
                        null);

                    if (modelInstance != null)
                    {
                        if (obj.Properties.TryGetValue("ResolvedNode", out string resolvedNode) &&
                            ShouldAttemptResolvedNodeIsolation(obj.Properties, modelPath) &&
                            !string.IsNullOrWhiteSpace(resolvedNode))
                        {
                            if (TryIsolateResolvedNodeInstance(go.transform, ref modelInstance, resolvedNode))
                            {
                                result.ResolvedNodeIsolationsSucceeded++;
                            }
                            else
                            {
                                result.ResolvedNodeIsolationsFailed++;
                                result.FailedResolvedNodeEntries.Add(
                                    $"{obj.Id} :: node '{resolvedNode}' :: model '{modelPath}'");
                            }
                        }

                        result.InstantiatedModels++;
                        modelInstantiated = true;
                    }
                    else
                    {
                        result.MissingModels++;
                        if (!result.MissingModelPaths.Contains(modelPath, StringComparer.OrdinalIgnoreCase))
                        {
                            result.MissingModelPaths.Add(modelPath);
                        }
                    }
                }

                if (!modelInstantiated &&
                    !string.IsNullOrWhiteSpace(modelPath) &&
                    settings.CreatePlaceholderForMissingModel)
                {
                    CreateMissingModelPlaceholder(go);
                    result.PlaceholdersCreated++;
                }

                if (settings.AddObjectListInfo)
                {
                    AttachObjectListInfo(go, obj, modelPath);
                }

                result.CreatedSceneObjects++;
            }

            if (settings.ApplyPreviewLighting)
            {
                ToontownPreviewLightingUtility.ApplyToActiveScene(verbose: false);
            }

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            return result;
        }

        public static string ResolveModelPathFromProperties(Dictionary<string, string> properties)
        {
            return ResolveModelPath(properties);
        }

        private static void AttachObjectListInfo(GameObject target, WorldDataObject source, string modelPath)
        {
            ObjectListInfo info = target.GetComponent<ObjectListInfo>();
            if (info == null)
            {
                info = target.AddComponent<ObjectListInfo>();
            }

            info.objectId = source.Id;

            if (source.Properties.TryGetValue("Type", out string type) && !string.IsNullOrWhiteSpace(type))
            {
                info.objectType = type;
            }

            if (!string.IsNullOrWhiteSpace(modelPath))
            {
                info.modelPath = modelPath;
            }
        }

        private static void CreateMissingModelPlaceholder(GameObject parent)
        {
            GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            placeholder.name = "__MissingModel__";
            placeholder.transform.SetParent(parent.transform, false);
            placeholder.transform.localScale = new Vector3(1f, 1f, 1f);

            Collider collider = placeholder.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static string ResolveModelPath(Dictionary<string, string> properties)
        {
            if (properties == null)
            {
                return null;
            }

            string selected = null;
            if (properties.TryGetValue("ResolvedModel", out string resolvedModel) &&
                !string.IsNullOrWhiteSpace(resolvedModel))
            {
                selected = resolvedModel;
            }
            else if (properties.TryGetValue("Model", out string model) &&
                     !string.IsNullOrWhiteSpace(model))
            {
                selected = model;
            }

            if (string.IsNullOrWhiteSpace(selected))
            {
                return null;
            }

            string normalized = selected.Trim().Replace('\\', '/');
            normalized = normalized.TrimStart('/');
            normalized = PhasePrefixRegex.Replace(normalized, string.Empty);

            if (normalized.EndsWith(".bam", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith(".egg", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                normalized = System.IO.Path.ChangeExtension(normalized, null)?.Replace('\\', '/');
            }

            return normalized;
        }

        private static void ApplyTransform(Transform target, Dictionary<string, string> properties)
        {
            if (TryGetVector(properties, "Pos", out Vector3 pos))
            {
                target.localPosition = ConvertPandaVectorToUnity(pos);
            }

            if (TryGetVector(properties, "Hpr", out Vector3 hpr))
            {
                target.localEulerAngles = ConvertPandaHprToUnityEuler(hpr);
            }

            if (TryGetVector(properties, "Scale", out Vector3 scale))
            {
                target.localScale = ConvertPandaVectorToUnity(scale);
            }
        }

        private static bool TryIsolateResolvedNodeInstance(
            Transform parent,
            ref GameObject modelInstance,
            string resolvedNodeName)
        {
            if (parent == null || modelInstance == null || string.IsNullOrWhiteSpace(resolvedNodeName))
            {
                return false;
            }

            Transform match = FindResolvedNodeTransform(modelInstance.transform, resolvedNodeName);
            if (match == null)
            {
                return false;
            }

            if (match == modelInstance.transform)
            {
                return true;
            }

            string originalRootName = modelInstance.name;
            match.SetParent(parent, true);
            GameObject isolatedRoot = match.gameObject;
            isolatedRoot.name = originalRootName;
            UnityEngine.Object.DestroyImmediate(modelInstance);
            modelInstance = isolatedRoot;
            return true;
        }

        private static Transform FindResolvedNodeTransform(Transform root, string resolvedNodeName)
        {
            if (root == null || string.IsNullOrWhiteSpace(resolvedNodeName))
            {
                return null;
            }

            string normalizedTarget = NormalizeNodeName(resolvedNodeName);
            Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);

            Transform exactMatch = null;
            int bestDepth = int.MaxValue;
            foreach (Transform t in allTransforms)
            {
                if (!string.Equals(t.name, resolvedNodeName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int depth = GetTransformDepth(t, root);
                if (depth < bestDepth)
                {
                    bestDepth = depth;
                    exactMatch = t;
                }
            }

            if (exactMatch != null)
            {
                return exactMatch;
            }

            Transform normalizedMatch = null;
            bestDepth = int.MaxValue;
            foreach (Transform t in allTransforms)
            {
                if (!string.Equals(NormalizeNodeName(t.name), normalizedTarget, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int depth = GetTransformDepth(t, root);
                if (depth < bestDepth)
                {
                    bestDepth = depth;
                    normalizedMatch = t;
                }
            }

            if (normalizedMatch != null)
            {
                return normalizedMatch;
            }

            string withoutDirectionalSuffix = StripDirectionalSuffix(normalizedTarget);
            if (!string.Equals(withoutDirectionalSuffix, normalizedTarget, StringComparison.OrdinalIgnoreCase))
            {
                Transform directionalFallback = null;
                bestDepth = int.MaxValue;
                foreach (Transform t in allTransforms)
                {
                    if (!string.Equals(
                            NormalizeNodeName(t.name),
                            withoutDirectionalSuffix,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int depth = GetTransformDepth(t, root);
                    if (depth < bestDepth)
                    {
                        bestDepth = depth;
                        directionalFallback = t;
                    }
                }

                if (directionalFallback != null)
                {
                    return directionalFallback;
                }
            }

            // Last fallback: partial string match for imported node names that may have
            // importer-added suffixes/prefixes. Choose shallowest transform.
            Transform partialMatch = null;
            bestDepth = int.MaxValue;
            foreach (Transform t in allTransforms)
            {
                string normalizedName = NormalizeNodeName(t.name);
                if (normalizedName.IndexOf(normalizedTarget, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                int depth = GetTransformDepth(t, root);
                if (depth < bestDepth)
                {
                    bestDepth = depth;
                    partialMatch = t;
                }
            }

            return partialMatch;
        }

        private static int GetTransformDepth(Transform node, Transform root)
        {
            int depth = 0;
            Transform cursor = node;
            while (cursor != null && cursor != root)
            {
                depth++;
                cursor = cursor.parent;
            }

            return depth;
        }

        private static string NormalizeNodeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Trim().Trim('"').Replace(" ", string.Empty);
            // Some storage mappings use "clothesshop" while model nodes are "clothshop".
            normalized = normalized.Replace("clothesshop", "clothshop", StringComparison.OrdinalIgnoreCase);
            return normalized;
        }

        private static string StripDirectionalSuffix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string lower = value.ToLowerInvariant();
            if (lower.EndsWith("_ur", StringComparison.Ordinal) ||
                lower.EndsWith("_ul", StringComparison.Ordinal) ||
                lower.EndsWith("_dr", StringComparison.Ordinal) ||
                lower.EndsWith("_dl", StringComparison.Ordinal))
            {
                return value.Substring(0, value.Length - 3);
            }

            return value;
        }

        private static bool ShouldAttemptResolvedNodeIsolation(
            Dictionary<string, string> properties,
            string modelPath)
        {
            if (properties != null &&
                properties.TryGetValue("Keyword", out string keyword) &&
                !string.IsNullOrWhiteSpace(keyword))
            {
                if (string.Equals(keyword, "door", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(keyword, "window", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (string.IsNullOrWhiteSpace(modelPath))
            {
                return false;
            }

            string normalized = modelPath.Replace('\\', '/');
            return normalized.IndexOf("models/modules/doors", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("models/modules/windows", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Vector3 ConvertPandaVectorToUnity(Vector3 panda)
        {
            return new Vector3(panda.x, panda.z, panda.y);
        }

        private static Vector3 ConvertPandaHprToUnityEuler(Vector3 pandaHpr)
        {
            // Panda HPR is (heading, pitch, roll). For this parser path we keep raw HPR order,
            // so the Unity inverse mapping is (-pitch, -heading, -roll).
            return new Vector3(-pandaHpr.y, -pandaHpr.x, -pandaHpr.z);
        }

        private static bool TryGetVector(Dictionary<string, string> properties, string key, out Vector3 vector)
        {
            vector = Vector3.zero;
            if (properties == null || !properties.TryGetValue(key, out string raw) || string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            List<float> values = ParseNumbers(raw);
            if (values.Count < 3)
            {
                if (key == "Scale" && values.Count == 1)
                {
                    vector = new Vector3(values[0], values[0], values[0]);
                    return true;
                }

                return false;
            }

            vector = new Vector3(values[0], values[1], values[2]);
            return true;
        }

        private static List<float> ParseNumbers(string input)
        {
            var values = new List<float>();
            MatchCollection matches = NumberRegex.Matches(input ?? string.Empty);
            foreach (Match match in matches)
            {
                if (float.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    values.Add(value);
                }
            }

            return values;
        }
    }

    [Serializable]
    public sealed class ToontownSceneImportSettings
    {
        public bool UseEggFiles = true;
        public bool AddObjectListInfo = true;
        public bool CreatePlaceholderForMissingModel = false;
        public bool ApplyPreviewLighting = true;
        public string RootObjectName = string.Empty;
    }

    [Serializable]
    public sealed class ToontownSceneImportResult
    {
        public string RootObjectName;
        public int TotalDocumentObjects;
        public int CreatedSceneObjects;
        public int InstantiatedModels;
        public int MissingModels;
        public int PlaceholdersCreated;
        public int ResolvedNodeIsolationsSucceeded;
        public int ResolvedNodeIsolationsFailed;
        public List<string> MissingModelPaths = new List<string>();
        public List<string> FailedResolvedNodeEntries = new List<string>();
    }
}
