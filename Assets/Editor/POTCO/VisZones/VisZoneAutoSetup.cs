using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using POTCO.VisZones;

namespace POTCO.Editor
{
    /// <summary>
    /// Automatic VisZone setup tool for islands that don't have viszones yet
    /// Finds collision zones, auto-assigns props, detects neighbors, and creates all necessary components
    /// </summary>
    public class VisZoneAutoSetup : EditorWindow
    {
        [MenuItem("POTCO/VisZones/Auto-Setup VisZones")]
        public static void ShowWindow()
        {
            VisZoneAutoSetup window = GetWindow<VisZoneAutoSetup>("VisZone Auto Setup");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        private GameObject targetRoot;
        private Vector2 scrollPosition;
        private bool includeNamedStatics = true;
        private bool autoDetectNeighbors = true;
        private float neighborDetectionDistance = 50f;

        // Progress tracking
        private List<string> setupLog = new List<string>();

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            GUILayout.Label("VisZone Auto Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This tool automatically sets up VisZones for islands that don't have them yet. It will:\n" +
                "• Find all collision_zone_* objects\n" +
                "• Create VisZoneVolume components\n" +
                "• Auto-assign props to zones based on position\n" +
                "• Create Section-* GameObjects\n" +
                "• Auto-detect neighboring zones\n" +
                "• Set up VisZoneData and VisZoneManager", MessageType.Info);

            EditorGUILayout.Space(10);

            // Target selection
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            targetRoot = (GameObject)EditorGUILayout.ObjectField("Island Root", targetRoot, typeof(GameObject), true);

            if (targetRoot == null)
            {
                EditorGUILayout.HelpBox("Select the root GameObject of your island (usually the top-level parent in the hierarchy)", MessageType.Warning);
            }

            EditorGUILayout.Space(10);

            // Options
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            autoDetectNeighbors = EditorGUILayout.Toggle("Auto-Detect Neighbors", autoDetectNeighbors);
            if (autoDetectNeighbors)
            {
                neighborDetectionDistance = EditorGUILayout.Slider("Detection Distance", neighborDetectionDistance, 10f, 200f);
            }
            includeNamedStatics = EditorGUILayout.Toggle("Include Named Statics", includeNamedStatics);

            EditorGUILayout.Space(10);

            // Setup button
            GUI.enabled = targetRoot != null;
            if (GUILayout.Button("Run Auto Setup", GUILayout.Height(40)))
            {
                RunAutoSetup();
            }
            GUI.enabled = true;

            EditorGUILayout.Space(10);

            // Log display
            if (setupLog.Count > 0)
            {
                EditorGUILayout.LabelField("Setup Log", EditorStyles.boldLabel);
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
                foreach (string log in setupLog)
                {
                    EditorGUILayout.LabelField(log, EditorStyles.wordWrappedLabel);
                }
                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("Clear Log"))
                {
                    setupLog.Clear();
                }
            }
        }

        private void RunAutoSetup()
        {
            setupLog.Clear();
            Log("Starting VisZone Auto Setup...");

            try
            {
                Undo.SetCurrentGroupName("VisZone Auto Setup");
                int undoGroup = Undo.GetCurrentGroup();

                // Step 1: Find collision zones
                Dictionary<string, Transform> collisionZones = FindCollisionZones();
                if (collisionZones.Count == 0)
                {
                    Log("ERROR: No collision_zone_* objects found! Please create collision zones first.");
                    EditorUtility.DisplayDialog("No Collision Zones", "No collision_zone_* objects found in the scene. Please create collision zones first.", "OK");
                    return;
                }

                // Step 2: Create VisZoneVolume components
                CreateVisZoneVolumes(collisionZones);

                // Step 3: Create VisZoneData component
                VisZoneData visZoneData = CreateVisZoneData(collisionZones);

                // Step 4: Create zone sections
                Dictionary<string, VisZoneSection> sections = CreateZoneSections(collisionZones);

                // Step 5: Auto-assign objects to zones
                AutoAssignObjectsToZones(collisionZones, sections);

                // Step 6: Auto-detect neighbors
                if (autoDetectNeighbors)
                {
                    AutoDetectNeighbors(collisionZones, visZoneData);
                }

                // Step 7: Handle named statics
                if (includeNamedStatics)
                {
                    AutoDetectNamedStatics(collisionZones, visZoneData);
                }

                // Step 8: Link components together
                LinkComponents(collisionZones, sections, visZoneData);

                // Step 9: Create VisZoneManager
                CreateVisZoneManager(sections.Values.ToList(), visZoneData);

                Undo.CollapseUndoOperations(undoGroup);

                Log("=================================");
                Log("✅ VisZone Auto Setup Complete!");
                Log($"✅ Created {collisionZones.Count} zones");
                Log($"✅ Created {sections.Count} sections");

                EditorUtility.DisplayDialog("Setup Complete",
                    $"VisZone auto setup complete!\n\n" +
                    $"Created {collisionZones.Count} zones with {sections.Count} sections.\n\n" +
                    $"You can now use the VisZone Editor to fine-tune the setup.", "OK");
            }
            catch (System.Exception e)
            {
                Log($"ERROR: {e.Message}");
                Debug.LogError($"[VisZoneAutoSetup] Error: {e}");
                EditorUtility.DisplayDialog("Setup Failed", $"An error occurred during setup:\n{e.Message}", "OK");
            }
        }

        private Dictionary<string, Transform> FindCollisionZones()
        {
            Log("Step 1: Finding collision zones...");
            Dictionary<string, Transform> collisionZones = new Dictionary<string, Transform>();

            Transform[] allTransforms = targetRoot.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTransforms)
            {
                if (t.name.StartsWith("collision_zone_"))
                {
                    string zoneName = t.name.Substring("collision_zone_".Length);
                    collisionZones[zoneName] = t;
                    Log($"  Found: {zoneName}");
                }
            }

            Log($"✓ Found {collisionZones.Count} collision zones");
            return collisionZones;
        }

        private void CreateVisZoneVolumes(Dictionary<string, Transform> collisionZones)
        {
            Log("Step 2: Creating VisZoneVolume components...");

            foreach (var kvp in collisionZones)
            {
                Transform zone = kvp.Value;

                // Skip if already has VisZoneVolume
                if (zone.GetComponent<VisZoneVolume>() != null)
                {
                    Log($"  Skipped {kvp.Key} (already has VisZoneVolume)");
                    continue;
                }

                // Ensure collider exists - create from mesh if needed
                Collider collider = zone.GetComponent<Collider>();
                if (collider == null)
                {
                    // Try to create MeshCollider from mesh geometry
                    MeshFilter meshFilter = zone.GetComponent<MeshFilter>();
                    if (meshFilter == null)
                    {
                        meshFilter = zone.GetComponentInChildren<MeshFilter>();
                    }

                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        const float VERTICAL_EXTENSION = 1000f;

                        // Extrude mesh vertically for flying gameplay
                        Mesh extrudedMesh = ExtrudeMeshVertically(meshFilter.sharedMesh, VERTICAL_EXTENSION);

                        MeshCollider meshCol = Undo.AddComponent<MeshCollider>(zone.gameObject);
                        meshCol.sharedMesh = extrudedMesh;
                        meshCol.convex = true;
                        meshCol.isTrigger = true;
                        collider = meshCol;

                        Log($"  ✓ Created extruded MeshCollider for {kvp.Key} (height: ±{VERTICAL_EXTENSION})");
                    }
                    else
                    {
                        Log($"  ❌ ERROR: {kvp.Key} has no collider and no mesh geometry to create one!");
                        continue;
                    }
                }
                else
                {
                    collider.isTrigger = true;
                    Log($"  ✓ Using existing {collider.GetType().Name} for {kvp.Key}");
                }

                // Add VisZoneVolume component
                VisZoneVolume volume = Undo.AddComponent<VisZoneVolume>(zone.gameObject);
                volume.displayColor = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.8f, 1f);
                volume.zoneCollider = collider;

                Log($"  Created VisZoneVolume for {kvp.Key}");
            }

            Log($"✓ Created {collisionZones.Count} VisZoneVolume components");
        }

        private VisZoneData CreateVisZoneData(Dictionary<string, Transform> collisionZones)
        {
            Log("Step 3: Creating VisZoneData component...");

            VisZoneData visZoneData = targetRoot.GetComponent<VisZoneData>();
            if (visZoneData == null)
            {
                visZoneData = Undo.AddComponent<VisZoneData>(targetRoot);
            }

            visZoneData.areaName = targetRoot.name;
            visZoneData.visTable.Clear();

            // Create entries for each zone
            foreach (var kvp in collisionZones)
            {
                VisZoneEntry entry = new VisZoneEntry
                {
                    zoneName = kvp.Key,
                    visibleZones = new List<string>(),
                    objectUids = new List<string>(),
                    fortVisZones = new List<string>()
                };
                visZoneData.visTable.Add(entry);
            }

            EditorUtility.SetDirty(visZoneData);
            Log($"✓ Created VisZoneData with {visZoneData.visTable.Count} zone entries");

            return visZoneData;
        }

        private Dictionary<string, VisZoneSection> CreateZoneSections(Dictionary<string, Transform> collisionZones)
        {
            Log("Step 4: Creating zone sections...");
            Dictionary<string, VisZoneSection> sections = new Dictionary<string, VisZoneSection>();

            // Find or create sections container
            Transform sectionsContainer = targetRoot.transform.Find("VisZone_Sections");
            if (sectionsContainer == null)
            {
                GameObject container = new GameObject("VisZone_Sections");
                Undo.RegisterCreatedObjectUndo(container, "Create Sections Container");
                container.transform.SetParent(targetRoot.transform, false);
                sectionsContainer = container.transform;
            }

            foreach (var kvp in collisionZones)
            {
                string zoneName = kvp.Key;
                Transform zoneTransform = kvp.Value;

                // Check if section already exists
                Transform existingSection = sectionsContainer.Find($"Section-{zoneName}");
                if (existingSection != null)
                {
                    VisZoneSection existingComp = existingSection.GetComponent<VisZoneSection>();
                    if (existingComp != null)
                    {
                        sections[zoneName] = existingComp;
                        Log($"  Reused existing section: {zoneName}");
                        continue;
                    }
                }

                // Create new section
                GameObject sectionObj = new GameObject($"Section-{zoneName}");
                Undo.RegisterCreatedObjectUndo(sectionObj, "Create Section");
                sectionObj.transform.SetParent(sectionsContainer, false);

                VisZoneSection section = Undo.AddComponent<VisZoneSection>(sectionObj);
                section.zoneName = zoneName;

                // Calculate bounds from collision zone
                Bounds bounds = CalculateBounds(zoneTransform);
                section.zoneBounds = bounds;
                sectionObj.transform.position = bounds.center;

                // Link collider reference
                Collider collider = zoneTransform.GetComponent<Collider>();
                if (collider != null)
                {
                    section.zoneCollider = collider;
                }

                sections[zoneName] = section;
                Log($"  Created section: {zoneName}");
            }

            Log($"✓ Created {sections.Count} zone sections");
            return sections;
        }

        private void AutoAssignObjectsToZones(Dictionary<string, Transform> collisionZones, Dictionary<string, VisZoneSection> sections)
        {
            Log("Step 5: Auto-assigning objects to zones...");

            ObjectListInfo[] allObjects = FindObjectsByType<ObjectListInfo>(FindObjectsSortMode.None);
            int assignedCount = 0;
            int skippedLarge = 0;
            int skippedAlreadyAssigned = 0;
            int outsideAllZones = 0;

            Log($"  Found {allObjects.Length} total objects to process");

            foreach (var info in allObjects)
            {
                // Skip Large objects (they stay visible always)
                if (info.visSize == "Large")
                {
                    skippedLarge++;
                    continue;
                }

                // Check if already correctly assigned (has visZone AND is parented to correct section)
                if (!string.IsNullOrEmpty(info.visZone))
                {
                    if (sections.TryGetValue(info.visZone, out VisZoneSection correctSection))
                    {
                        if (info.transform.parent == correctSection.transform)
                        {
                            skippedAlreadyAssigned++;
                            continue;
                        }
                    }
                }

                // Skip if it's a collision zone or section itself
                if (info.GetComponent<VisZoneVolume>() != null || info.GetComponent<VisZoneSection>() != null)
                {
                    continue;
                }

                // Find which zone this object is in by checking collision
                Vector3 position = info.transform.position;
                string assignedZone = null;

                foreach (var kvp in collisionZones)
                {
                    Collider collider = kvp.Value.GetComponent<Collider>();
                    if (collider == null) continue;

                    // Use bounds check - works for vertically-extruded mesh colliders
                    Bounds bounds = collider.bounds;
                    bool isInside = bounds.Contains(position);

                    if (assignedCount < 5)
                    {
                        Log($"    Testing '{info.gameObject.name}' in zone '{kvp.Key}': bounds={bounds.size}, pos={position}, inside={isInside}");
                    }

                    if (isInside)
                    {
                        assignedZone = kvp.Key;
                        break;
                    }
                }

                // Assign to zone section
                if (assignedZone != null && sections.TryGetValue(assignedZone, out VisZoneSection section))
                {
                    Undo.SetTransformParent(info.transform, section.transform, "Assign to Zone");
                    info.visZone = assignedZone;
                    EditorUtility.SetDirty(info);
                    assignedCount++;

                    if (assignedCount <= 5)
                    {
                        Log($"    ✓ Assigned '{info.gameObject.name}' to zone '{assignedZone}'");
                    }
                }
                else if (assignedZone == null)
                {
                    outsideAllZones++;
                }
            }

            Log($"✓ Auto-assigned {assignedCount} objects to zones");
            Log($"  Skipped {skippedLarge} Large objects");
            Log($"  Skipped {skippedAlreadyAssigned} already assigned objects");
            Log($"  {outsideAllZones} objects outside all zones");
        }

        private void AutoDetectNeighbors(Dictionary<string, Transform> collisionZones, VisZoneData visZoneData)
        {
            Log("Step 6: Auto-detecting neighbors...");
            int neighborCount = 0;

            List<string> zoneNames = collisionZones.Keys.ToList();

            for (int i = 0; i < zoneNames.Count; i++)
            {
                string zone1Name = zoneNames[i];
                Transform zone1 = collisionZones[zone1Name];
                Bounds bounds1 = CalculateBounds(zone1);

                VisZoneEntry entry1 = visZoneData.visTable.Find(e => e.zoneName == zone1Name);
                if (entry1 == null) continue;

                for (int j = i + 1; j < zoneNames.Count; j++)
                {
                    string zone2Name = zoneNames[j];
                    Transform zone2 = collisionZones[zone2Name];
                    Bounds bounds2 = CalculateBounds(zone2);

                    // Check if zones are neighbors (intersect or are close)
                    float distance = Vector3.Distance(bounds1.center, bounds2.center);
                    float maxDistance = (bounds1.extents.magnitude + bounds2.extents.magnitude) + neighborDetectionDistance;

                    if (bounds1.Intersects(bounds2) || distance < maxDistance)
                    {
                        // Add symmetric neighbor relationships
                        VisZoneEntry entry2 = visZoneData.visTable.Find(e => e.zoneName == zone2Name);
                        if (entry2 != null)
                        {
                            if (!entry1.visibleZones.Contains(zone2Name))
                            {
                                entry1.visibleZones.Add(zone2Name);
                                neighborCount++;
                            }
                            if (!entry2.visibleZones.Contains(zone1Name))
                            {
                                entry2.visibleZones.Add(zone1Name);
                                neighborCount++;
                            }
                        }
                    }
                }
            }

            EditorUtility.SetDirty(visZoneData);
            Log($"✓ Auto-detected {neighborCount} neighbor relationships");
        }

        private void AutoDetectNamedStatics(Dictionary<string, Transform> collisionZones, VisZoneData visZoneData)
        {
            Log("Step 7: Auto-detecting named statics...");

            // Named statics are typically objects with specific naming patterns
            // that represent large environmental pieces like rocks, barriers, etc.
            // For now, we'll leave this as a placeholder for future enhancement

            Log("  Named static detection is optional and can be refined later");
        }

        private void LinkComponents(Dictionary<string, Transform> collisionZones, Dictionary<string, VisZoneSection> sections, VisZoneData visZoneData)
        {
            Log("Step 8: Linking components...");

            foreach (var kvp in collisionZones)
            {
                string zoneName = kvp.Key;
                Transform zoneTransform = kvp.Value;

                VisZoneVolume volume = zoneTransform.GetComponent<VisZoneVolume>();
                if (volume != null && sections.TryGetValue(zoneName, out VisZoneSection section))
                {
                    volume.sectionRoot = section;
                    EditorUtility.SetDirty(volume);
                }
            }

            Log("✓ Linked VisZoneVolumes to sections");
        }

        private void CreateVisZoneManager(List<VisZoneSection> sections, VisZoneData visZoneData)
        {
            Log("Step 9: Creating VisZoneManager...");

            VisZoneManager manager = targetRoot.GetComponent<VisZoneManager>();
            if (manager == null)
            {
                manager = Undo.AddComponent<VisZoneManager>(targetRoot);
            }

            manager.visZoneData = visZoneData;
            manager.zoneSections.Clear();
            manager.zoneSections.AddRange(sections);

            EditorUtility.SetDirty(manager);
            Log($"✓ Created VisZoneManager with {sections.Count} sections");
        }

        private Bounds CalculateBounds(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                return bounds;
            }

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

            // Last resort: return default bounds
            return new Bounds(root.position, Vector3.one * 50f);
        }

        private void Log(string message)
        {
            setupLog.Add(message);
            Debug.Log($"[VisZoneAutoSetup] {message}");
            Repaint();
        }

        /// <summary>
        /// Extrude a mesh vertically for flying gameplay
        /// Creates a new mesh with vertices duplicated at +/- vertical extension
        /// Unity's convex hull will create the proper collision volume
        /// </summary>
        private static Mesh ExtrudeMeshVertically(Mesh originalMesh, float verticalExtension)
        {
            // Get original mesh data
            Vector3[] originalVertices = originalMesh.vertices;
            int[] originalTriangles = originalMesh.triangles;

            // Calculate the center Y of the original mesh
            Bounds bounds = originalMesh.bounds;
            float centerY = bounds.center.y;

            // Create new vertex arrays (original + top copy + bottom copy)
            int originalVertCount = originalVertices.Length;
            Vector3[] newVertices = new Vector3[originalVertCount * 3];

            // Copy original vertices (middle layer)
            for (int i = 0; i < originalVertCount; i++)
            {
                newVertices[i] = originalVertices[i];
            }

            // Create top layer (offset up)
            for (int i = 0; i < originalVertCount; i++)
            {
                Vector3 vert = originalVertices[i];
                vert.y = centerY + verticalExtension;
                newVertices[originalVertCount + i] = vert;
            }

            // Create bottom layer (offset down)
            for (int i = 0; i < originalVertCount; i++)
            {
                Vector3 vert = originalVertices[i];
                vert.y = centerY - verticalExtension;
                newVertices[originalVertCount * 2 + i] = vert;
            }

            // Create triangle arrays (original + top + bottom)
            // The convex hull algorithm will connect these automatically
            int originalTriCount = originalTriangles.Length;
            int[] newTriangles = new int[originalTriCount * 3];

            // Original triangles
            for (int i = 0; i < originalTriCount; i++)
            {
                newTriangles[i] = originalTriangles[i];
            }

            // Top layer triangles (offset indices by originalVertCount)
            for (int i = 0; i < originalTriCount; i++)
            {
                newTriangles[originalTriCount + i] = originalTriangles[i] + originalVertCount;
            }

            // Bottom layer triangles (offset indices by originalVertCount * 2)
            for (int i = 0; i < originalTriCount; i++)
            {
                newTriangles[originalTriCount * 2 + i] = originalTriangles[i] + (originalVertCount * 2);
            }

            // Create new mesh
            Mesh extrudedMesh = new Mesh();
            extrudedMesh.name = originalMesh.name + "_Extruded";
            extrudedMesh.vertices = newVertices;
            extrudedMesh.triangles = newTriangles;
            extrudedMesh.RecalculateNormals();
            extrudedMesh.RecalculateBounds();

            return extrudedMesh;
        }
    }
}
