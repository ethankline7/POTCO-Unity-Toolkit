using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using WorldDataImporter.Data;
using POTCO.VisZones;
using POTCO.Editor;

namespace WorldDataImporter.Processors
{
    /// <summary>
    /// Processes VisZone data during world import
    /// Parses Vis Table, creates zone sections, sets up collision triggers
    /// </summary>
    public static class VisZoneProcessor
    {
        /// <summary>
        /// Main entry point - process VisZones for an imported scene
        /// </summary>
        public static void ProcessVisZones(GameObject root, Dictionary<string, ObjectData> objectDataMap, string pythonFilePath)
        {
            if (root == null)
            {
                DebugLogger.LogWorldImporter("⚠️ VisZone processing skipped: root is null");
                return;
            }

            DebugLogger.LogWorldImporter("🔷 Starting VisZone processing...");

            // Step 1: Find and parse Vis Table from Python file
            Dictionary<string, VisZoneEntry> visTable = ParseVisTableFromSource(pythonFilePath);

            if (visTable == null || visTable.Count == 0)
            {
                DebugLogger.LogWorldImporter("⚠️ No Vis Table found in source data - skipping VisZone setup");
                return;
            }

            DebugLogger.LogWorldImporter($"📋 Parsed Vis Table with {visTable.Count} zones");

            // Step 2: Find collision_zone_* transforms in the scene
            Dictionary<string, Transform> collisionZones = FindCollisionZones(root);
            DebugLogger.LogWorldImporter($"🔍 Found {collisionZones.Count} collision zones in scene");

            // Step 3: Create Section GameObjects for each zone
            Dictionary<string, GameObject> sections = CreateZoneSections(root, visTable, collisionZones);
            DebugLogger.LogWorldImporter($"📦 Created {sections.Count} zone sections");

            // Step 4: Parent objects to appropriate sections based on VisZone property
            ParentObjectsToSections(objectDataMap, sections);

            // Step 5: Create trigger colliders for player detection
            CreateZoneTriggers(collisionZones);

            // Step 6: Set up VisZoneData and VisZoneManager on root
            SetupVisZoneComponents(root, visTable, sections);

            DebugLogger.LogWorldImporter("✅ VisZone processing complete!");
        }

        /// <summary>
        /// Parse Vis Table from Python source file
        /// </summary>
        private static Dictionary<string, VisZoneEntry> ParseVisTableFromSource(string pythonFilePath)
        {
            // Use the provided file path directly
            if (!File.Exists(pythonFilePath))
            {
                DebugLogger.LogWorldImporter($"⚠️ Python source file not found: {pythonFilePath}");
                return null;
            }

            DebugLogger.LogWorldImporter($"📄 Reading Vis Table from: {Path.GetFileName(pythonFilePath)}");

            string[] lines = File.ReadAllLines(pythonFilePath);
            Dictionary<string, VisZoneEntry> visTable = new Dictionary<string, VisZoneEntry>();

            // Find 'Vis Table': { section
            bool inVisTable = false;
            string currentZoneName = null;
            int braceDepth = 0;
            int visTableStartDepth = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                // Track brace depth
                braceDepth += CountChar(line, '{');
                braceDepth -= CountChar(line, '}');

                if (!inVisTable && line.Contains("'Vis Table':"))
                {
                    inVisTable = true;
                    visTableStartDepth = braceDepth;
                    DebugLogger.LogWorldImporter($"📖 Found Vis Table at line {i + 1}");
                    continue;
                }

                if (inVisTable)
                {
                    // Check if we've exited the Vis Table section
                    if (braceDepth < visTableStartDepth)
                    {
                        break; // Done parsing Vis Table
                    }

                    // Parse zone entry: 'zoneName': ([visibleZones], [objectUids], [fortVis])
                    Match zoneMatch = Regex.Match(line, @"'([^']+)':\s*\(\[");
                    if (zoneMatch.Success)
                    {
                        currentZoneName = zoneMatch.Groups[1].Value;
                        VisZoneEntry entry = new VisZoneEntry { zoneName = currentZoneName };

                        // Parse the three arrays in the tuple
                        string fullEntry = line;
                        int lineIndex = i;

                        // Read until we find the closing ]),
                        while (!fullEntry.Contains("]),") && lineIndex < lines.Length - 1)
                        {
                            lineIndex++;
                            fullEntry += lines[lineIndex].Trim();
                        }

                        // Extract visible zones (first array)
                        entry.visibleZones = ExtractStringArray(fullEntry, 0);

                        // Extract object UIDs (second array)
                        entry.objectUids = ExtractStringArray(fullEntry, 1);

                        // Extract fort vis zones (third array, optional)
                        entry.fortVisZones = ExtractStringArray(fullEntry, 2);

                        visTable[currentZoneName] = entry;
                        DebugLogger.LogWorldImporter($"  ✓ Zone '{currentZoneName}': {entry.visibleZones.Count} visible zones, {entry.objectUids.Count} objects");
                    }
                }
            }

            return visTable;
        }

        /// <summary>
        /// Extract string array from Python tuple format
        /// </summary>
        private static List<string> ExtractStringArray(string fullEntry, int arrayIndex)
        {
            List<string> result = new List<string>();

            // Find all [...]  patterns
            MatchCollection arrayMatches = Regex.Matches(fullEntry, @"\[([^\]]*)\]");

            if (arrayIndex < arrayMatches.Count)
            {
                string arrayContent = arrayMatches[arrayIndex].Groups[1].Value;

                // Extract strings within quotes
                MatchCollection stringMatches = Regex.Matches(arrayContent, @"'([^']*)'");
                foreach (Match match in stringMatches)
                {
                    result.Add(match.Groups[1].Value);
                }
            }

            return result;
        }

        /// <summary>
        /// Find all collision_zone_* transforms in the scene hierarchy
        /// </summary>
        private static Dictionary<string, Transform> FindCollisionZones(GameObject root)
        {
            Dictionary<string, Transform> collisionZones = new Dictionary<string, Transform>();

            // Search all transforms in the hierarchy
            Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);

            foreach (Transform t in allTransforms)
            {
                if (t.name.StartsWith("collision_zone_"))
                {
                    string zoneName = t.name.Substring("collision_zone_".Length);
                    collisionZones[zoneName] = t;
                    DebugLogger.LogWorldImporter($"  🔷 Found collision zone: {zoneName}");
                }
            }

            return collisionZones;
        }

        /// <summary>
        /// Create Section-<ZoneName> GameObjects for each zone in vis table
        /// </summary>
        private static Dictionary<string, GameObject> CreateZoneSections(GameObject root, Dictionary<string, VisZoneEntry> visTable, Dictionary<string, Transform> collisionZones)
        {
            Dictionary<string, GameObject> sections = new Dictionary<string, GameObject>();

            // Create a parent container for all sections
            GameObject sectionsContainer = new GameObject("VisZone_Sections");
            sectionsContainer.transform.SetParent(root.transform, false);

            foreach (var kvp in visTable)
            {
                string zoneName = kvp.Key;
                VisZoneEntry entry = kvp.Value;

                // Create section GameObject
                GameObject section = new GameObject($"Section-{zoneName}");
                section.transform.SetParent(sectionsContainer.transform, false);

                // Add VisZoneSection component
                VisZoneSection sectionComp = Undo.AddComponent<VisZoneSection>(section);
                sectionComp.zoneName = zoneName;

                // Calculate zone bounds from collision zone if available
                if (collisionZones.TryGetValue(zoneName, out Transform collisionTransform))
                {
                    Bounds bounds = CalculateBounds(collisionTransform);
                    sectionComp.zoneBounds = bounds;

                    // Position section at zone center
                    section.transform.position = bounds.center;
                }

                sections[zoneName] = section;
                DebugLogger.LogWorldImporter($"  📦 Created section: {zoneName}");
            }

            return sections;
        }

        /// <summary>
        /// Parent objects to appropriate sections based on VisZone property
        /// </summary>
        private static void ParentObjectsToSections(Dictionary<string, ObjectData> objectDataMap, Dictionary<string, GameObject> sections)
        {
            int parentedCount = 0;

            foreach (var kvp in objectDataMap)
            {
                ObjectData data = kvp.Value;

                // Skip if no VisZone assigned
                if (string.IsNullOrEmpty(data.visZone))
                    continue;

                // Skip "Large" objects (they stay visible always, don't parent to sections)
                if (!string.IsNullOrEmpty(data.visSize) && data.visSize == "Large")
                {
                    DebugLogger.LogWorldImporter($"  ⏭️ Skipping Large object: {data.id}");
                    continue;
                }

                // Find the section for this zone
                if (sections.TryGetValue(data.visZone, out GameObject section))
                {
                    // Parent this object to the section
                    if (data.gameObject != null)
                    {
                        Undo.SetTransformParent(data.gameObject.transform, section.transform, "Parent to VisZone Section");
                        parentedCount++;
                    }
                }
                else
                {
                    DebugLogger.LogWorldImporter($"  ⚠️ VisZone '{data.visZone}' not found in Vis Table for object {data.id}");
                }
            }

            DebugLogger.LogWorldImporter($"  ✓ Parented {parentedCount} objects to zone sections");
        }

        /// <summary>
        /// Create trigger colliders for player zone detection
        /// Extended vertically for flying gameplay
        /// </summary>
        private static void CreateZoneTriggers(Dictionary<string, Transform> collisionZones)
        {
            const float VERTICAL_EXTENSION = 1000f; // Extend 1000 units up and down for flying

            foreach (var kvp in collisionZones)
            {
                Transform collisionTransform = kvp.Value;

                // Check if it already has a collider
                Collider existingCollider = collisionTransform.GetComponent<Collider>();
                if (existingCollider != null)
                {
                    // Extend existing collider vertically
                    if (existingCollider is BoxCollider boxCol)
                    {
                        Vector3 size = boxCol.size;
                        Vector3 center = boxCol.center;

                        // Extend Y size and center
                        center.y = 0; // Center at origin
                        size.y = VERTICAL_EXTENSION * 2; // Total height

                        boxCol.center = center;
                        boxCol.size = size;
                        boxCol.isTrigger = true;

                        DebugLogger.LogWorldImporter($"  ✓ Extended existing collider vertically: {kvp.Key} (height: {size.y})");
                    }
                    else if (existingCollider is MeshCollider meshCol)
                    {
                        // Keep mesh collider shape, just configure it as a trigger
                        meshCol.isTrigger = true;
                        meshCol.convex = true; // Required for triggers in Unity
                        DebugLogger.LogWorldImporter($"  ✓ Configured mesh collider as trigger: {kvp.Key} (convex: {meshCol.convex})");
                    }
                    else
                    {
                        existingCollider.isTrigger = true;
                        DebugLogger.LogWorldImporter($"  ✓ Configured existing collider as trigger: {kvp.Key}");
                    }
                }
                else
                {
                    CreateColliderForZone(collisionTransform, kvp.Key);
                }
            }
        }

        /// <summary>
        /// Create collider for zone detection - uses mesh geometry if available, otherwise creates box
        /// </summary>
        private static void CreateColliderForZone(Transform collisionTransform, string zoneName)
        {
            // First, try to find mesh geometry to use for a MeshCollider
            MeshFilter meshFilter = collisionTransform.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                // Check children for mesh filters
                meshFilter = collisionTransform.GetComponentInChildren<MeshFilter>();
            }

            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                // Use the actual mesh geometry for collision
                MeshCollider meshCollider = Undo.AddComponent<MeshCollider>(collisionTransform.gameObject);
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = true; // Required for triggers
                meshCollider.isTrigger = true;

                DebugLogger.LogWorldImporter($"  ✓ Created mesh trigger collider: {zoneName} (mesh: {meshFilter.sharedMesh.name}, vertices: {meshFilter.sharedMesh.vertexCount})");
            }
            else
            {
                // Fallback: create tall box collider for flying gameplay
                CreateTallBoxCollider(collisionTransform, zoneName);
            }
        }

        /// <summary>
        /// Create a tall box collider for zone detection (fallback when no mesh is found)
        /// </summary>
        private static void CreateTallBoxCollider(Transform collisionTransform, string zoneName)
        {
            const float VERTICAL_EXTENSION = 1000f;

            BoxCollider boxCollider = Undo.AddComponent<BoxCollider>(collisionTransform.gameObject);
            boxCollider.isTrigger = true;

            // Calculate horizontal bounds from child meshes
            Bounds bounds = CalculateBounds(collisionTransform);

            // Use horizontal extent but extend vertically for flying
            Vector3 center = bounds.center - collisionTransform.position;
            center.y = 0; // Center at origin height

            Vector3 size = bounds.size;
            size.y = VERTICAL_EXTENSION * 2; // Make it 2000 units tall (±1000 from center)

            boxCollider.center = center;
            boxCollider.size = size;

            DebugLogger.LogWorldImporter($"  ✓ Created tall box trigger collider (fallback): {zoneName} (size: {size.x:F1} x {size.y:F1} x {size.z:F1})");
        }

        /// <summary>
        /// Set up VisZoneData and VisZoneManager components on root
        /// </summary>
        private static void SetupVisZoneComponents(GameObject root, Dictionary<string, VisZoneEntry> visTable, Dictionary<string, GameObject> sections)
        {
            // Add VisZoneData component
            VisZoneData visZoneData = root.GetComponent<VisZoneData>();
            if (visZoneData == null)
            {
                visZoneData = Undo.AddComponent<VisZoneData>(root);
            }

            visZoneData.areaName = root.name;
            visZoneData.visTable = new List<VisZoneEntry>(visTable.Values);

            // Add VisZoneManager component
            VisZoneManager visZoneManager = root.GetComponent<VisZoneManager>();
            if (visZoneManager == null)
            {
                visZoneManager = Undo.AddComponent<VisZoneManager>(root);
            }

            visZoneManager.visZoneData = visZoneData;

            // Populate section references
            visZoneManager.zoneSections.Clear();
            foreach (var section in sections.Values)
            {
                VisZoneSection sectionComp = section.GetComponent<VisZoneSection>();
                if (sectionComp != null)
                {
                    visZoneManager.zoneSections.Add(sectionComp);
                }
            }

            EditorUtility.SetDirty(root);
            DebugLogger.LogWorldImporter($"  ✓ Configured VisZoneManager with {visZoneManager.zoneSections.Count} sections");
        }

        /// <summary>
        /// Calculate bounds from a transform and all its children
        /// </summary>
        private static Bounds CalculateBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                // Fallback: use colliders
                Collider[] colliders = root.GetComponentsInChildren<Collider>();
                if (colliders.Length > 0)
                {
                    Bounds bounds = colliders[0].bounds;
                    foreach (Collider col in colliders)
                    {
                        bounds.Encapsulate(col.bounds);
                    }
                    return bounds;
                }

                // Last resort: return a default bounds at root position
                return new Bounds(root.position, Vector3.one * 10f);
            }

            Bounds combinedBounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }

            return combinedBounds;
        }

        /// <summary>
        /// Count occurrences of a character in a string
        /// </summary>
        private static int CountChar(string str, char c)
        {
            int count = 0;
            foreach (char ch in str)
            {
                if (ch == c) count++;
            }
            return count;
        }
    }
}
