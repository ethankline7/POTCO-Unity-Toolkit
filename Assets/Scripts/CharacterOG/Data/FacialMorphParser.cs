/// <summary>
/// Parses facial morph definitions from PirateMale.py and PirateFemale.py ControlShapes dictionaries.
/// Extends existing OgPyReader to extract facial bone transforms.
/// </summary>
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using CharacterOG.Models;
using CharacterOG.Data.PureCSharpBackend;

namespace CharacterOG.Data
{
    public static class FacialMorphParser
    {
        // Store parsed variables for resolving references (TX, TY, RZ, etc.)
        private static Dictionary<string, PyNode> parsedVariables;

        /// <summary>Parse ControlShapes from PirateMale.py or PirateFemale.py</summary>
        public static FacialMorphDatabase ParseFromFile(string filePath, string gender)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"[FacialMorphParser] File not found: {filePath}");
                return null;
            }

            Debug.Log($"[FacialMorphParser] Reading {Path.GetFileName(filePath)}...");

            string fileContent = File.ReadAllText(filePath);

            // Check if ControlShapes exists in the file at all
            if (!fileContent.Contains("ControlShapes = {"))
            {
                Debug.LogError($"[FacialMorphParser] ControlShapes definition not found in file content!");
                return new FacialMorphDatabase(gender);
            }

            Debug.Log($"[FacialMorphParser] ControlShapes found in file, attempting to parse...");

            var reader = new OgPyReader(fileContent, filePath);
            parsedVariables = reader.ParseFile(filePath);

            Debug.Log($"[FacialMorphParser] Successfully parsed {parsedVariables.Count} variables. Checking for ControlShapes...");

            if (!parsedVariables.TryGetValue("ControlShapes", out var controlShapesNode))
            {
                Debug.LogError($"[FacialMorphParser] ControlShapes was in the file but NOT parsed by OgPyReader! Check Unity console for parse warnings from OgPyReader.");
                Debug.LogError($"[FacialMorphParser] This means the parser failed to handle the ControlShapes dictionary. Looking for parse error logs above...");
                return new FacialMorphDatabase(gender);
            }

            Debug.Log($"[FacialMorphParser] ControlShapes successfully parsed!");

            var database = new FacialMorphDatabase(gender);

            // ControlShapes should be a dictionary
            if (controlShapesNode is PyDict controlShapesDict)
            {
                Debug.Log($"[FacialMorphParser] ControlShapes has {controlShapesDict.items.Count} morph definitions");

                foreach (var kvp in controlShapesDict.items)
                {
                    // kvp.Key is already a string in PyDict
                    string morphName = kvp.Key;
                    if (string.IsNullOrEmpty(morphName))
                        continue;

                    var morphDef = ParseMorphDefinition(morphName, kvp.Value);
                    if (morphDef != null)
                    {
                        database.morphs[morphName] = morphDef;
                        Debug.Log($"[FacialMorphParser] Loaded morph '{morphName}': {morphDef.positiveTransforms.Count} positive transforms, {morphDef.negativeTransforms.Count} negative transforms");
                    }
                }

                Debug.Log($"[FacialMorphParser] SUCCESS! Parsed {database.morphs.Count} facial morphs from {Path.GetFileName(filePath)} for gender '{gender}'");

                // Log first 5 morph names
                var morphNames = database.morphs.Keys.Take(5).ToList();
                Debug.Log($"[FacialMorphParser] Sample morphs: {string.Join(", ", morphNames)}");
            }
            else
            {
                Debug.LogError($"[FacialMorphParser] ControlShapes is not a dictionary in {filePath}");
            }

            return database;
        }

        /// <summary>Parse a single morph definition (list with positive and negative transforms)</summary>
        private static FacialMorphDef ParseMorphDefinition(string morphName, PyNode node)
        {
            if (!(node is PyList morphList))
            {
                Debug.LogWarning($"[FacialMorphParser] Morph '{morphName}' is not a list");
                return null;
            }

            var morphDef = new FacialMorphDef(morphName);

            // ControlShapes format: [positiveTransforms, negativeTransforms]
            if (morphList.items.Count >= 1)
            {
                morphDef.positiveTransforms = ParseTransformList(morphList.items[0]);
            }

            if (morphList.items.Count >= 2)
            {
                morphDef.negativeTransforms = ParseTransformList(morphList.items[1]);
            }

            return morphDef;
        }

        /// <summary>Parse a list of bone transforms</summary>
        private static List<BoneTransform> ParseTransformList(PyNode node)
        {
            var transforms = new List<BoneTransform>();

            if (!(node is PyList transformList))
                return transforms;

            foreach (var transformNode in transformList.items)
            {
                if (!(transformNode is PyList transformData))
                    continue;

                // Format: ['boneName', transformType, value, 0, 0, 0]
                if (transformData.items.Count < 3)
                    continue;

                string boneName = GetString(transformData.items[0]);
                int transformTypeInt = GetInt(transformData.items[1]);
                float value = GetFloat(transformData.items[2]);

                if (string.IsNullOrEmpty(boneName))
                    continue;

                var transformType = (TransformType)transformTypeInt;
                transforms.Add(new BoneTransform(boneName, transformType, value));
            }

            return transforms;
        }

        /// <summary>Extract string from PyNode</summary>
        private static string GetString(PyNode node)
        {
            if (node is PyString pyStr)
                return pyStr.value;
            return null;
        }

        /// <summary>Extract int from PyNode, resolving variable references</summary>
        private static int GetInt(PyNode node)
        {
            if (node is PyNumber pyNum)
                return pyNum.AsInt();

            // Handle variable references (TX, TY, RZ, etc.)
            if (node is PyVariable pyVar && parsedVariables != null)
            {
                if (parsedVariables.TryGetValue(pyVar.name, out var resolvedNode))
                {
                    if (resolvedNode is PyNumber resolvedNum)
                    {
                        Debug.Log($"[FacialMorphParser] Resolved variable '{pyVar.name}' to {resolvedNum.AsInt()}");
                        return resolvedNum.AsInt();
                    }
                }
                Debug.LogWarning($"[FacialMorphParser] Could not resolve variable '{pyVar.name}', defaulting to 0");
            }

            return 0;
        }

        /// <summary>Extract float from PyNode</summary>
        private static float GetFloat(PyNode node)
        {
            if (node is PyNumber pyNum)
                return pyNum.AsFloat();
            return 0f;
        }
    }
}
