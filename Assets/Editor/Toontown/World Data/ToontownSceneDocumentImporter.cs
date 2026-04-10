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

        private static readonly Regex NodeTokenRegex = new Regex(
            @"[A-Z]+(?=[A-Z][a-z]|\b)|[A-Z]?[a-z]+|\d+",
            RegexOptions.Compiled);

        private static readonly string[] FakeShadowNameTokens =
        {
            "drop-shadow",
            "drop_shadow",
            "square_drop_shadow",
            "shadow_card",
            "toon_shadow"
        };

        private static readonly string[] DirectionalSuffixes = { "ur", "ul", "dr", "dl" };
        private static readonly string[] DoorAnchorFallbackNodeNames =
        {
            "door_origin",
            "door_front",
            "door_locator",
            "door"
        };

        private static readonly string[] WindowAnchorFallbackNodeNames =
        {
            "window_origin",
            "window_locator",
            "window"
        };

        private static readonly HashSet<string> NodeNoiseTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "prop",
            "toon",
            "landmark",
            "building",
            "animated",
            "dna",
            "root",
            "tt"
        };

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
            var instantiatedModelRootsByObjectId =
                new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            var sourceObjectsById = new Dictionary<string, WorldDataObject>(StringComparer.OrdinalIgnoreCase);
            foreach (WorldDataObject obj in document.Objects)
            {
                sourceObjectsById[obj.Id] = obj;
            }

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
                bool skipModelInstantiation = ShouldSkipZeroCountWindowGroup(obj.Properties);
                if (skipModelInstantiation)
                {
                    result.ZeroCountWindowGroupsSkipped++;
                }

                bool shouldAttemptParentAnchor =
                    !skipModelInstantiation &&
                    ShouldAttemptParentAnchorPlacement(obj.Properties, modelPath) &&
                    !HasExplicitTransformProperties(obj.Properties);
                if (shouldAttemptParentAnchor &&
                    TryRegisterWindowCountLayoutPending(obj, sourceObjectsById, result))
                {
                    shouldAttemptParentAnchor = false;
                }

                if (shouldAttemptParentAnchor)
                {
                    result.DoorWindowParentAnchorsAttempted++;
                    if (TryApplyParentAnchorPlacement(
                            go.transform,
                            obj,
                            instantiatedModelRootsByObjectId,
                            modelPath,
                            out string anchorDetails))
                    {
                        result.DoorWindowParentAnchorsApplied++;
                    }
                    else
                    {
                        result.DoorWindowParentAnchorsMissed++;
                        result.AddWarningCategory(ToontownSceneImportResult.FallbackPlacementCategory);
                        if (!string.IsNullOrWhiteSpace(anchorDetails) &&
                            result.DoorWindowParentAnchorWarnings.Count < 100)
                        {
                            result.DoorWindowParentAnchorWarnings.Add($"{obj.Id} :: {anchorDetails}");
                        }
                    }
                }

                bool modelInstantiated = false;
                if (!skipModelInstantiation && !string.IsNullOrWhiteSpace(modelPath))
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
                            bool allowFuzzyMatch = ShouldAllowFuzzyResolvedNodeMatch(obj.Properties, modelPath);
                            if (TryIsolateResolvedNodeInstance(
                                    go.transform,
                                    ref modelInstance,
                                    resolvedNode,
                                    allowFuzzyMatch,
                                    out string isolateDetails))
                            {
                                result.ResolvedNodeIsolationsSucceeded++;
                            }
                            else
                            {
                                result.ResolvedNodeIsolationsFailed++;
                                result.AddWarningCategory(ToontownSceneImportResult.MissingResolvedNodeCategory);
                                result.FailedResolvedNodeEntries.Add(
                                    $"{obj.Id} :: node '{resolvedNode}' :: model '{modelPath}' :: {isolateDetails}");
                            }
                        }

                        if (settings.RemoveFakeShadowsByDefault)
                        {
                            int disabledFakeShadows = RemoveFakeShadowRenderers(modelInstance.transform);
                            result.FakeShadowRenderersDisabled += disabledFakeShadows;
                            result.AddWarningCategory(
                                ToontownSceneImportResult.FakeShadowRemovalCategory,
                                disabledFakeShadows);
                        }

                        result.InstantiatedModels++;
                        modelInstantiated = true;
                        instantiatedModelRootsByObjectId[obj.Id] = modelInstance.transform;
                    }
                    else
                    {
                        result.MissingModels++;
                        result.AddWarningCategory(ToontownSceneImportResult.MissingModelCategory);
                        if (!result.MissingModelPaths.Contains(modelPath, StringComparer.OrdinalIgnoreCase))
                        {
                            result.MissingModelPaths.Add(modelPath);
                        }
                    }
                }

                if (!modelInstantiated &&
                    !skipModelInstantiation &&
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

        internal static bool TryFindResolvedNodeForRegression(
            Transform root,
            string resolvedNodeName,
            bool allowFuzzyMatch,
            out string matchedName,
            out string strategy,
            out string diagnostics)
        {
            ResolvedNodeSearchResult result = FindResolvedNodeTransform(root, resolvedNodeName, allowFuzzyMatch);
            matchedName = result.Match != null ? result.Match.name : string.Empty;
            strategy = result.Strategy ?? string.Empty;
            diagnostics = result.Diagnostics ?? string.Empty;
            return result.Match != null;
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

        private static int RemoveFakeShadowRenderers(Transform modelRoot)
        {
            if (modelRoot == null)
            {
                return 0;
            }

            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            int disabledCount = 0;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Material[] mats = renderer.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    continue;
                }

                bool anyNonShadowMaterial = false;
                foreach (Material mat in mats)
                {
                    if (!IsFakeShadowMaterial(mat))
                    {
                        anyNonShadowMaterial = true;
                        break;
                    }
                }

                if (anyNonShadowMaterial)
                {
                    continue;
                }

                renderer.enabled = false;
                disabledCount++;
            }

            return disabledCount;
        }

        private static bool IsFakeShadowMaterial(Material material)
        {
            if (material == null)
            {
                return false;
            }

            if (ContainsFakeShadowToken(material.name))
            {
                return true;
            }

            if (!material.HasProperty("_MainTex"))
            {
                return false;
            }

            Texture mainTex = material.GetTexture("_MainTex");
            if (mainTex == null)
            {
                return false;
            }

            if (ContainsFakeShadowToken(mainTex.name))
            {
                return true;
            }

            string texturePath = AssetDatabase.GetAssetPath(mainTex);
            return ContainsFakeShadowToken(texturePath);
        }

        private static bool ContainsFakeShadowToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (string token in FakeShadowNameTokens)
            {
                if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
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

        private static bool ShouldAttemptParentAnchorPlacement(
            Dictionary<string, string> properties,
            string modelPath)
        {
            if (properties != null &&
                properties.TryGetValue("Keyword", out string keyword) &&
                !string.IsNullOrWhiteSpace(keyword))
            {
                if (string.Equals(keyword, "door", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(keyword, "windows", StringComparison.OrdinalIgnoreCase) ||
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

        private static bool HasExplicitTransformProperties(Dictionary<string, string> properties)
        {
            if (properties == null)
            {
                return false;
            }

            return properties.ContainsKey("Pos") ||
                   properties.ContainsKey("Hpr") ||
                   properties.ContainsKey("Scale");
        }

        private static bool TryApplyParentAnchorPlacement(
            Transform targetObjectTransform,
            WorldDataObject sourceObject,
            Dictionary<string, Transform> instantiatedModelRootsByObjectId,
            string modelPath,
            out string anchorDetails)
        {
            anchorDetails = "no anchor details";
            if (targetObjectTransform == null || sourceObject == null)
            {
                anchorDetails = "missing target transform or source object";
                return false;
            }

            if (string.IsNullOrWhiteSpace(sourceObject.ParentId))
            {
                anchorDetails = "no parent id";
                return false;
            }

            if (instantiatedModelRootsByObjectId == null ||
                !instantiatedModelRootsByObjectId.TryGetValue(sourceObject.ParentId, out Transform parentModelRoot) ||
                parentModelRoot == null)
            {
                anchorDetails = $"parent model root not found for '{sourceObject.ParentId}'";
                return false;
            }

            List<string> candidates = BuildParentAnchorCandidates(sourceObject.Properties, modelPath);
            if (candidates.Count == 0)
            {
                anchorDetails = "no parent anchor candidates";
                return false;
            }

            foreach (string candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                bool allowFuzzyMatch = ShouldAllowParentAnchorFuzzyMatch(candidate);
                ResolvedNodeSearchResult search = FindResolvedNodeTransform(parentModelRoot, candidate, allowFuzzyMatch);
                if (search.Match == null)
                {
                    continue;
                }

                targetObjectTransform.SetPositionAndRotation(search.Match.position, search.Match.rotation);
                anchorDetails = $"anchored to parent node '{search.Match.name}' via {search.Strategy}";
                return true;
            }

            string candidateSummary = string.Join(", ", candidates.Distinct(StringComparer.OrdinalIgnoreCase).Take(8));
            anchorDetails = $"failed parent anchor lookup; tried: {candidateSummary}";
            return false;
        }

        private static List<string> BuildParentAnchorCandidates(
            Dictionary<string, string> properties,
            string modelPath)
        {
            var candidates = new List<string>();
            if (properties != null &&
                properties.TryGetValue("ResolvedNode", out string resolvedNode) &&
                !string.IsNullOrWhiteSpace(resolvedNode))
            {
                candidates.Add(resolvedNode);

                string normalizedNode = NormalizeNodeName(resolvedNode);
                string strippedNode = StripDirectionalSuffix(normalizedNode);
                if (!string.IsNullOrWhiteSpace(strippedNode))
                {
                    candidates.Add(strippedNode);
                }

                string directionalSuffix = GetDirectionalSuffix(normalizedNode);
                if (!string.IsNullOrWhiteSpace(directionalSuffix) &&
                    !string.IsNullOrWhiteSpace(strippedNode))
                {
                    candidates.Add($"{strippedNode}_{directionalSuffix}");
                }
            }

            bool isDoor = false;
            bool isWindow = false;
            if (properties != null &&
                properties.TryGetValue("Keyword", out string keyword) &&
                !string.IsNullOrWhiteSpace(keyword))
            {
                isDoor = string.Equals(keyword, "door", StringComparison.OrdinalIgnoreCase);
                isWindow = string.Equals(keyword, "window", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(keyword, "windows", StringComparison.OrdinalIgnoreCase);
            }

            if (!isDoor && !isWindow && !string.IsNullOrWhiteSpace(modelPath))
            {
                string normalizedModel = modelPath.Replace('\\', '/');
                isDoor = normalizedModel.IndexOf("models/modules/doors", StringComparison.OrdinalIgnoreCase) >= 0;
                isWindow = normalizedModel.IndexOf("models/modules/windows", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (isDoor)
            {
                candidates.AddRange(DoorAnchorFallbackNodeNames);
            }

            if (isWindow)
            {
                candidates.AddRange(WindowAnchorFallbackNodeNames);
            }

            return candidates
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool ShouldAllowParentAnchorFuzzyMatch(string candidate)
        {
            string normalized = NormalizeNodeName(candidate);
            return string.Equals(normalized, "door_origin", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "door_front", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "window_origin", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "window_locator", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryIsolateResolvedNodeInstance(
            Transform parent,
            ref GameObject modelInstance,
            string resolvedNodeName,
            bool allowFuzzyMatch,
            out string isolateDetails)
        {
            isolateDetails = "uninitialized";
            if (parent == null || modelInstance == null || string.IsNullOrWhiteSpace(resolvedNodeName))
            {
                isolateDetails = "invalid isolate parameters";
                return false;
            }

            ResolvedNodeSearchResult searchResult =
                FindResolvedNodeTransform(modelInstance.transform, resolvedNodeName, allowFuzzyMatch);
            Transform match = searchResult.Match;
            if (match == null)
            {
                isolateDetails = searchResult.Diagnostics;
                return false;
            }

            if (match == modelInstance.transform)
            {
                isolateDetails = $"matched root via {searchResult.Strategy}";
                return true;
            }

            string originalRootName = modelInstance.name;
            string matchedName = match.name;
            // Never re-parent a child inside a prefab instance directly; clone the subtree instead.
            GameObject isolatedRoot = UnityEngine.Object.Instantiate(match.gameObject, parent, true);
            isolatedRoot.name = originalRootName;
            UnityEngine.Object.DestroyImmediate(modelInstance);
            modelInstance = isolatedRoot;
            isolateDetails = $"matched '{matchedName}' via {searchResult.Strategy}";
            return true;
        }

        private static ResolvedNodeSearchResult FindResolvedNodeTransform(
            Transform root,
            string resolvedNodeName,
            bool allowFuzzyMatch)
        {
            if (root == null || string.IsNullOrWhiteSpace(resolvedNodeName))
            {
                return ResolvedNodeSearchResult.Failure("no root or resolved-node supplied");
            }

            string normalizedTarget = NormalizeNodeName(resolvedNodeName);
            string strippedTarget = StripDirectionalSuffix(normalizedTarget);
            Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
            string targetDirectionalSuffix = GetDirectionalSuffix(normalizedTarget);

            Transform exactMatch = FindBestTransform(
                allTransforms,
                root,
                t => string.Equals(t.name, resolvedNodeName, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
            {
                return ResolvedNodeSearchResult.Success(exactMatch, "exact-name");
            }

            Transform normalizedMatch = FindBestTransform(
                allTransforms,
                root,
                t => string.Equals(NormalizeNodeName(t.name), normalizedTarget, StringComparison.OrdinalIgnoreCase));
            if (normalizedMatch != null)
            {
                return ResolvedNodeSearchResult.Success(normalizedMatch, "normalized-name");
            }

            if (!string.Equals(strippedTarget, normalizedTarget, StringComparison.OrdinalIgnoreCase))
            {
                Transform strippedMatch = FindBestTransform(
                    allTransforms,
                    root,
                    t => string.Equals(NormalizeNodeName(t.name), strippedTarget, StringComparison.OrdinalIgnoreCase));
                if (strippedMatch != null)
                {
                    return ResolvedNodeSearchResult.Success(strippedMatch, "directional-strip");
                }
            }

            if (!string.IsNullOrWhiteSpace(targetDirectionalSuffix))
            {
                foreach (string suffix in GetDirectionalFallbackOrder(targetDirectionalSuffix))
                {
                    string candidateName = $"{strippedTarget}_{suffix}";
                    Transform directionalMatch = FindBestTransform(
                        allTransforms,
                        root,
                        t => string.Equals(
                            NormalizeNodeName(t.name),
                            candidateName,
                            StringComparison.OrdinalIgnoreCase));
                    if (directionalMatch != null)
                    {
                        string strategy = string.Equals(suffix, targetDirectionalSuffix, StringComparison.OrdinalIgnoreCase)
                            ? "directional-exact"
                            : $"directional-fallback:{suffix}";
                        return ResolvedNodeSearchResult.Success(directionalMatch, strategy);
                    }
                }
            }

            if (!allowFuzzyMatch)
            {
                return ResolvedNodeSearchResult.Failure(BuildResolvedNodeFailureDiagnostics(
                    resolvedNodeName,
                    normalizedTarget,
                    strippedTarget,
                    allTransforms));
            }

            Transform tokenOverlapMatch = FindBestTokenOverlapTransform(allTransforms, root, normalizedTarget);
            if (tokenOverlapMatch != null)
            {
                return ResolvedNodeSearchResult.Success(tokenOverlapMatch, "token-overlap");
            }

            Transform startsWithMatch = FindBestTransform(
                allTransforms,
                root,
                t => NormalizeNodeName(t.name).StartsWith(normalizedTarget, StringComparison.OrdinalIgnoreCase));
            if (startsWithMatch != null)
            {
                return ResolvedNodeSearchResult.Success(startsWithMatch, "fuzzy-starts-with");
            }

            Transform containsMatch = FindBestTransform(
                allTransforms,
                root,
                t => NormalizeNodeName(t.name).IndexOf(normalizedTarget, StringComparison.OrdinalIgnoreCase) >= 0);
            if (containsMatch != null)
            {
                return ResolvedNodeSearchResult.Success(containsMatch, "fuzzy-contains");
            }

            if (!string.IsNullOrWhiteSpace(strippedTarget))
            {
                Transform strippedContainsMatch = FindBestTransform(
                    allTransforms,
                    root,
                    t => NormalizeNodeName(t.name).IndexOf(strippedTarget, StringComparison.OrdinalIgnoreCase) >= 0);
                if (strippedContainsMatch != null)
                {
                    return ResolvedNodeSearchResult.Success(strippedContainsMatch, "fuzzy-contains-stripped");
                }
            }

            return ResolvedNodeSearchResult.Failure(BuildResolvedNodeFailureDiagnostics(
                resolvedNodeName,
                normalizedTarget,
                strippedTarget,
                allTransforms));
        }

        private static Transform FindBestTransform(
            IEnumerable<Transform> transforms,
            Transform root,
            Func<Transform, bool> predicate)
        {
            Transform best = null;
            int bestDepth = int.MaxValue;
            int bestNameLength = int.MaxValue;
            string bestName = string.Empty;

            foreach (Transform candidate in transforms)
            {
                if (candidate == null || !predicate(candidate))
                {
                    continue;
                }

                int depth = GetTransformDepth(candidate, root);
                int nameLength = candidate.name?.Length ?? int.MaxValue;
                if (best == null ||
                    depth < bestDepth ||
                    (depth == bestDepth && nameLength < bestNameLength) ||
                    (depth == bestDepth &&
                     nameLength == bestNameLength &&
                     string.Compare(candidate.name, bestName, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    best = candidate;
                    bestDepth = depth;
                    bestNameLength = nameLength;
                    bestName = candidate.name ?? string.Empty;
                }
            }

            return best;
        }

        private static Transform FindBestTokenOverlapTransform(
            IEnumerable<Transform> transforms,
            Transform root,
            string normalizedTarget)
        {
            HashSet<string> targetTokens = TokenizeNodeName(normalizedTarget);
            if (targetTokens.Count == 0)
            {
                return null;
            }

            Transform best = null;
            int bestTokenScore = 0;
            int bestDepth = int.MaxValue;
            int bestNameLength = int.MaxValue;
            string bestName = string.Empty;

            foreach (Transform candidate in transforms)
            {
                if (candidate == null)
                {
                    continue;
                }

                HashSet<string> candidateTokens = TokenizeNodeName(NormalizeNodeName(candidate.name));
                if (candidateTokens.Count == 0)
                {
                    continue;
                }

                int sharedTokenCount = targetTokens.Count(token => candidateTokens.Contains(token));
                if (sharedTokenCount <= 0)
                {
                    continue;
                }

                int depth = GetTransformDepth(candidate, root);
                int nameLength = candidate.name?.Length ?? int.MaxValue;
                if (best == null ||
                    sharedTokenCount > bestTokenScore ||
                    (sharedTokenCount == bestTokenScore && depth < bestDepth) ||
                    (sharedTokenCount == bestTokenScore &&
                     depth == bestDepth &&
                     nameLength < bestNameLength) ||
                    (sharedTokenCount == bestTokenScore &&
                     depth == bestDepth &&
                     nameLength == bestNameLength &&
                     string.Compare(candidate.name, bestName, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    best = candidate;
                    bestTokenScore = sharedTokenCount;
                    bestDepth = depth;
                    bestNameLength = nameLength;
                    bestName = candidate.name ?? string.Empty;
                }
            }

            return best;
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
            normalized = normalized.Replace("clothes_shop", "clothshop", StringComparison.OrdinalIgnoreCase);
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

        private static string GetDirectionalSuffix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            foreach (string suffix in DirectionalSuffixes)
            {
                if (value.EndsWith("_" + suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return suffix;
                }
            }

            return string.Empty;
        }

        private static IEnumerable<string> GetDirectionalFallbackOrder(string targetSuffix)
        {
            if (!string.IsNullOrWhiteSpace(targetSuffix))
            {
                yield return targetSuffix.ToLowerInvariant();
            }

            foreach (string suffix in DirectionalSuffixes)
            {
                if (!string.Equals(suffix, targetSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    yield return suffix;
                }
            }
        }

        private static bool ShouldAttemptResolvedNodeIsolation(
            Dictionary<string, string> properties,
            string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                return false;
            }

            string normalizedModelPath = modelPath.Replace('\\', '/');
            if (normalizedModelPath.IndexOf("models/modules/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

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

            return false;
        }

        private static HashSet<string> TokenizeNodeName(string normalizedNodeName)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(normalizedNodeName))
            {
                return tokens;
            }

            string[] parts = normalizedNodeName.Split(
                new[] { '_', '-', '.', ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                MatchCollection matches = NodeTokenRegex.Matches(part);
                if (matches.Count == 0)
                {
                    AddNodeToken(tokens, part);
                    continue;
                }

                foreach (Match match in matches)
                {
                    AddNodeToken(tokens, match.Value);
                }
            }

            return tokens;
        }

        private static void AddNodeToken(HashSet<string> tokens, string rawToken)
        {
            string token = rawToken?.Trim();
            if (string.IsNullOrWhiteSpace(token) || NodeNoiseTokens.Contains(token))
            {
                return;
            }

            tokens.Add(token);
        }

        private static string BuildResolvedNodeFailureDiagnostics(
            string resolvedNodeName,
            string normalizedTarget,
            string strippedTarget,
            IReadOnlyList<Transform> candidates)
        {
            IEnumerable<Transform> likelyCandidates = candidates
                .Where(t =>
                {
                    string normalized = NormalizeNodeName(t.name);
                    return normalized.IndexOf(normalizedTarget, StringComparison.OrdinalIgnoreCase) >= 0 ||
                           (!string.IsNullOrWhiteSpace(strippedTarget) &&
                            normalized.IndexOf(strippedTarget, StringComparison.OrdinalIgnoreCase) >= 0);
                });

            List<string> topNames = likelyCandidates
                .Select(t => t.name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToList();

            if (topNames.Count == 0)
            {
                topNames = candidates
                    .Select(t => t.name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(8)
                    .ToList();
            }

            if (topNames.Count == 0)
            {
                return $"no candidates in model for resolved node '{resolvedNodeName}'";
            }

            return $"no match for '{resolvedNodeName}', closest candidates: {string.Join(", ", topNames)}";
        }

        private static bool ShouldAllowFuzzyResolvedNodeMatch(
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
            if (normalized.IndexOf("models/modules/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return normalized.IndexOf("models/modules/doors", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("models/modules/windows", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryRegisterWindowCountLayoutPending(
            WorldDataObject sourceObject,
            Dictionary<string, WorldDataObject> sourceObjectsById,
            ToontownSceneImportResult result)
        {
            if (sourceObject == null ||
                sourceObject.Properties == null ||
                result == null ||
                !IsWindowKeyword(sourceObject.Properties) ||
                !TryGetIntProperty(sourceObject.Properties, "Count", out int requestedCount) ||
                requestedCount <= 0)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(sourceObject.ParentId) ||
                sourceObjectsById == null ||
                !sourceObjectsById.TryGetValue(sourceObject.ParentId, out WorldDataObject parentObject) ||
                parentObject == null ||
                parentObject.Properties == null ||
                !HasKeyword(parentObject.Properties, "wall"))
            {
                return false;
            }

            result.WindowCountLayoutGroupsPending++;
            result.WindowCountLayoutRequestedInstances += requestedCount;
            result.AddWarningCategory(ToontownSceneImportResult.FallbackPlacementCategory);
            result.AddWarningCategory(ToontownSceneImportResult.WindowCountLayoutCategory);

            if (result.WindowCountLayoutWarnings.Count < 100)
            {
                result.WindowCountLayoutWarnings.Add(
                    $"{sourceObject.Id} :: wall/window count layout pending; count={requestedCount}; parent={sourceObject.ParentId}");
            }

            return true;
        }

        private static bool ShouldSkipZeroCountWindowGroup(Dictionary<string, string> properties)
        {
            return IsWindowKeyword(properties) &&
                   TryGetIntProperty(properties, "Count", out int count) &&
                   count <= 0;
        }

        private static bool IsWindowKeyword(Dictionary<string, string> properties)
        {
            return HasKeyword(properties, "window") || HasKeyword(properties, "windows");
        }

        private static bool HasKeyword(Dictionary<string, string> properties, string expected)
        {
            return properties != null &&
                   properties.TryGetValue("Keyword", out string keyword) &&
                   string.Equals(keyword, expected, StringComparison.OrdinalIgnoreCase);
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

        private static bool TryGetIntProperty(
            Dictionary<string, string> properties,
            string key,
            out int value)
        {
            value = 0;
            if (properties == null ||
                !properties.TryGetValue(key, out string raw) ||
                string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            List<float> values = ParseNumbers(raw);
            if (values.Count == 0)
            {
                return false;
            }

            value = Mathf.RoundToInt(values[0]);
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

        private sealed class ResolvedNodeSearchResult
        {
            public Transform Match;
            public string Strategy;
            public string Diagnostics;

            public static ResolvedNodeSearchResult Success(Transform match, string strategy)
            {
                return new ResolvedNodeSearchResult
                {
                    Match = match,
                    Strategy = strategy,
                    Diagnostics = string.Empty
                };
            }

            public static ResolvedNodeSearchResult Failure(string diagnostics)
            {
                return new ResolvedNodeSearchResult
                {
                    Match = null,
                    Strategy = string.Empty,
                    Diagnostics = diagnostics ?? string.Empty
                };
            }
        }
    }

    [Serializable]
    public sealed class ToontownSceneImportSettings
    {
        public bool UseEggFiles = true;
        public bool AddObjectListInfo = true;
        public bool CreatePlaceholderForMissingModel = false;
        public bool ApplyPreviewLighting = true;
        public bool RemoveFakeShadowsByDefault = true;
        public string RootObjectName = string.Empty;
    }

    [Serializable]
    public sealed class ToontownSceneImportResult
    {
        public const string MissingModelCategory = "missing model";
        public const string MissingResolvedNodeCategory = "missing resolved node";
        public const string FallbackPlacementCategory = "fallback placement";
        public const string WindowCountLayoutCategory = "window count layout pending";
        public const string MaterialFallbackCategory = "material fallback";
        public const string FakeShadowRemovalCategory = "fake shadow removal";
        public const string UncategorizedDocumentWarningCategory = "uncategorized document warning";

        public string RootObjectName;
        public int TotalDocumentObjects;
        public int CreatedSceneObjects;
        public int InstantiatedModels;
        public int MissingModels;
        public int PlaceholdersCreated;
        public int FakeShadowRenderersDisabled;
        public int ResolvedNodeIsolationsSucceeded;
        public int ResolvedNodeIsolationsFailed;
        public int DoorWindowParentAnchorsAttempted;
        public int DoorWindowParentAnchorsApplied;
        public int DoorWindowParentAnchorsMissed;
        public int ZeroCountWindowGroupsSkipped;
        public int WindowCountLayoutGroupsPending;
        public int WindowCountLayoutRequestedInstances;
        public List<string> MissingModelPaths = new List<string>();
        public List<string> FailedResolvedNodeEntries = new List<string>();
        public List<string> DoorWindowParentAnchorWarnings = new List<string>();
        public List<string> WindowCountLayoutWarnings = new List<string>();
        public Dictionary<string, int> WarningCategoryCounts =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public void AddWarningCategory(string category, int count = 1)
        {
            if (string.IsNullOrWhiteSpace(category) || count <= 0)
            {
                return;
            }

            if (WarningCategoryCounts.TryGetValue(category, out int current))
            {
                WarningCategoryCounts[category] = current + count;
            }
            else
            {
                WarningCategoryCounts[category] = count;
            }
        }

        public int GetWarningCategoryCount(string category)
        {
            if (string.IsNullOrWhiteSpace(category) || WarningCategoryCounts == null)
            {
                return 0;
            }

            return WarningCategoryCounts.TryGetValue(category, out int count) ? count : 0;
        }
    }
}
