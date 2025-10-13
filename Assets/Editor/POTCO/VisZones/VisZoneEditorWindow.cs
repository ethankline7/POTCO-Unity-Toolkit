using UnityEngine;
using UnityEditor;
using POTCO.VisZones;
using System.Collections.Generic;
using System.Linq;

namespace POTCO.Editor
{
    /// <summary>
    /// Comprehensive VisZone authoring tool
    /// 3-panel layout: Zone List | Zone Inspector | Problems & Tools
    /// </summary>
    public class VisZoneEditorWindow : EditorWindow
    {
        [MenuItem("POTCO/VisZones/Open VisZone Editor")]
        public static void ShowWindow()
        {
            VisZoneEditorWindow window = GetWindow<VisZoneEditorWindow>("VisZone Editor");
            window.minSize = new Vector2(900, 600);
            window.Show();
        }

        // ========== State ==========
        private VisZoneManager manager;
        private VisZoneData visZoneData;
        private List<VisZoneVolume> allZoneVolumes = new List<VisZoneVolume>();

        private VisZoneVolume selectedZone;
        private int selectedZoneIndex = -1;

        private string searchFilter = "";
        private Vector2 zoneListScroll;
        private Vector2 centerPanelScroll;
        private Vector2 neighborListScroll;
        private Vector2 availableListScroll;
        private Vector2 namedStaticsListScroll;
        private Vector2 availableStaticsScroll;
        private Vector2 memberListScroll;
        private Vector2 problemsScroll;

        private bool previewMode = false;
        private string previewZoneName = "";
        private Dictionary<VisZoneSection, bool> previewOriginalStates = new Dictionary<VisZoneSection, bool>();
        private Dictionary<GameObject, bool> previewOriginalStaticStates = new Dictionary<GameObject, bool>();

        // Filter toggles
        private bool showPropsFilter = true;
        private bool showEffectsFilter = true;
        private bool showNamedStaticsFilter = true;

        // Panel widths
        private float leftPanelWidth = 250f;
        private float rightPanelWidth = 250f;

        // Validation problems
        private List<string> validationProblems = new List<string>();

        // ========== Unity Lifecycle ==========
        private void OnEnable()
        {
            RefreshData();
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            ExitPreviewMode();
        }

        private void OnHierarchyChanged()
        {
            RefreshData();
            Repaint();
        }

        private void OnGUI()
        {
            HandleHotkeys();

            // Toolbar
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();

            // Left Panel: Zone List
            DrawLeftPanel();

            // Center Panel: Zone Inspector
            DrawCenterPanel();

            // Right Panel: Problems & Tools
            DrawRightPanel();

            EditorGUILayout.EndHorizontal();
        }

        // ========== Hotkeys ==========
        private void HandleHotkeys()
        {
            Event e = Event.current;

            if (e.type == EventType.KeyDown)
            {
                // V = Toggle preview
                if (e.keyCode == KeyCode.V && selectedZone != null)
                {
                    TogglePreview();
                    e.Use();
                }
                // A = Assign selected objects
                else if (e.keyCode == KeyCode.A && selectedZone != null)
                {
                    AssignSelectedObjectsToZone();
                    e.Use();
                }
                // F = Frame zone in scene view
                else if (e.keyCode == KeyCode.F && selectedZone != null)
                {
                    FrameZone();
                    e.Use();
                }
            }
        }

        // ========== Toolbar ==========
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button(new GUIContent("Refresh", "Reload all zones and vis data from the scene"),
                EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshData();
            }

            GUILayout.Space(10);

            if (GUILayout.Button(new GUIContent("Discover Zones", "Find all collision_zone_* objects and add VisZoneVolume components to them"),
                EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                DiscoverAndCreateZones();
            }

            if (GUILayout.Button(new GUIContent("Create Sections", "Create Section-<ZoneName> GameObjects for all zones"),
                EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                CreateAllSections();
            }

            GUILayout.Space(10);

            if (GUILayout.Button(new GUIContent("Validate", "Check for problems: missing sections, one-way neighbors, duplicate names, etc."),
                EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                RunValidation();
            }

            GUILayout.FlexibleSpace();

            // Preview mode indicator
            if (previewMode)
            {
                GUI.color = Color.yellow;
                GUILayout.Label($"PREVIEW MODE: {previewZoneName}", EditorStyles.toolbarButton);
                GUI.color = Color.white;

                if (GUILayout.Button("Exit Preview", EditorStyles.toolbarButton, GUILayout.Width(90)))
                {
                    ExitPreviewMode();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // ========== Left Panel: Zone List ==========
        private void DrawLeftPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(leftPanelWidth));

            GUILayout.Label("Zone List", EditorStyles.boldLabel);

            // Search bar
            EditorGUILayout.BeginHorizontal();
            searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("", GUI.skin.FindStyle("SearchCancelButton"), GUILayout.Width(18)))
            {
                searchFilter = "";
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Zone count
            EditorGUILayout.LabelField($"Total Zones: {allZoneVolumes.Count}", EditorStyles.miniLabel);

            EditorGUILayout.Space(5);

            // Zone list
            zoneListScroll = EditorGUILayout.BeginScrollView(zoneListScroll);

            for (int i = 0; i < allZoneVolumes.Count; i++)
            {
                VisZoneVolume zone = allZoneVolumes[i];
                if (zone == null) continue;

                // Apply search filter
                if (!string.IsNullOrEmpty(searchFilter) &&
                    !zone.zoneName.ToLower().Contains(searchFilter.ToLower()) &&
                    !zone.zoneGuid.ToLower().Contains(searchFilter.ToLower()))
                {
                    continue;
                }

                // Draw zone item
                DrawZoneListItem(zone, i);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            // Toolbar buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Add Zone", "Create a new collision_zone with a box collider"), GUILayout.Height(25)))
            {
                CreateNewZone();
            }
            if (GUILayout.Button(new GUIContent("Delete", "Delete the selected zone, its section, and all neighbor relationships"), GUILayout.Height(25)) && selectedZone != null)
            {
                DeleteZone(selectedZone);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawZoneListItem(VisZoneVolume zone, int index)
        {
            bool isSelected = selectedZone == zone;

            EditorGUILayout.BeginHorizontal(isSelected ? "selectionRect" : "box");

            // Color swatch
            EditorGUI.BeginChangeCheck();
            Color newColor = EditorGUILayout.ColorField(GUIContent.none, zone.displayColor, false, false, false, GUILayout.Width(20), GUILayout.Height(20));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(zone, "Change Zone Color");
                zone.displayColor = newColor;
                EditorUtility.SetDirty(zone);
            }

            // Zone name and member counts
            EditorGUILayout.BeginVertical();
            GUILayout.Label(zone.zoneName, EditorStyles.boldLabel);

            int memberCount = GetZoneMemberCount(zone.zoneName);
            int neighborCount = GetNeighborCount(zone.zoneName);
            GUILayout.Label($"{neighborCount} neighbors, {memberCount} members", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Handle selection
            Rect rect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                selectedZone = zone;
                selectedZoneIndex = index;

                // Select the Section GameObject if it exists, otherwise select collision_zone
                if (zone.sectionRoot != null)
                {
                    Selection.activeGameObject = zone.sectionRoot.gameObject;
                }
                else
                {
                    Selection.activeGameObject = zone.gameObject;
                }

                Event.current.Use();
                Repaint();
            }
        }

        // ========== Center Panel: Zone Inspector ==========
        private void DrawCenterPanel()
        {
            float centerWidth = position.width - leftPanelWidth - rightPanelWidth - 20;
            EditorGUILayout.BeginVertical(GUILayout.Width(centerWidth));

            if (selectedZone == null)
            {
                GUILayout.Label("Select a zone to edit", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            GUILayout.Label($"Zone Inspector: {selectedZone.zoneName}", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Wrap all sections in a scroll view
            centerPanelScroll = EditorGUILayout.BeginScrollView(centerPanelScroll);

            // Identity section
            DrawIdentitySection();

            EditorGUILayout.Space(10);

            // Volume section
            DrawVolumeSection();

            EditorGUILayout.Space(10);

            // Neighbors section
            DrawNeighborsSection();

            EditorGUILayout.Space(10);

            // Named Statics section
            DrawNamedStaticsSection();

            EditorGUILayout.Space(10);

            // Members section
            DrawMembersSection();

            EditorGUILayout.Space(10);

            // Preview section
            DrawPreviewSection();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawIdentitySection()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Identity", EditorStyles.boldLabel);

            // Zone name (editable with rename safety)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Zone Name:", GUILayout.Width(100));
            string newName = EditorGUILayout.TextField(selectedZone.zoneName);
            if (newName != selectedZone.zoneName)
            {
                RenameZone(selectedZone, newName);
            }
            EditorGUILayout.EndHorizontal();

            // GUID (copy button)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("GUID:", GUILayout.Width(100));
            EditorGUILayout.SelectableLabel(selectedZone.zoneGuid, EditorStyles.textField, GUILayout.Height(18));
            if (GUILayout.Button("Copy", GUILayout.Width(50)))
            {
                EditorGUIUtility.systemCopyBuffer = selectedZone.zoneGuid;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawVolumeSection()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Volume", EditorStyles.boldLabel);

            if (selectedZone.zoneCollider != null)
            {
                string colliderType = selectedZone.zoneCollider.GetType().Name;
                EditorGUILayout.LabelField("Collider Type:", colliderType);

                Bounds bounds = selectedZone.GetBounds();
                EditorGUILayout.LabelField("Size:", $"{bounds.size.x:F1} x {bounds.size.y:F1} x {bounds.size.z:F1}");
            }
            else
            {
                EditorGUILayout.HelpBox("No collider found on this zone!", MessageType.Warning);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Move to Bounds Center", "Move the collision_zone object to the center of its collider bounds")))
            {
                MoveZoneToBoundsCenter(selectedZone);
            }
            if (GUILayout.Button(new GUIContent("Rebuild Bounds", "Recalculate and update the section's zone bounds from the collider")))
            {
                RebuildZoneBounds(selectedZone);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawNeighborsSection()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Neighbors (Visible From Here)", EditorStyles.boldLabel);

            if (visZoneData == null)
            {
                EditorGUILayout.HelpBox("No VisZoneData found! Import a world with VisZones enabled.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            VisZoneEntry entry = visZoneData.visTable.Find(e => e.zoneName == selectedZone.zoneName);
            if (entry == null)
            {
                EditorGUILayout.HelpBox($"Zone '{selectedZone.zoneName}' not found in Vis Table!", MessageType.Error);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();

            // Left: Visible neighbors
            EditorGUILayout.BeginVertical("box", GUILayout.Width(200));
            GUILayout.Label($"Visible ({entry.visibleZones.Count})", EditorStyles.boldLabel);

            neighborListScroll = EditorGUILayout.BeginScrollView(neighborListScroll, GUILayout.Height(150));
            for (int i = 0; i < entry.visibleZones.Count; i++)
            {
                string neighbor = entry.visibleZones[i];
                EditorGUILayout.BeginHorizontal();

                // Check symmetry status
                int symmetryStatus = visZoneData.GetNeighborSymmetryStatus(selectedZone.zoneName, neighbor);
                if (symmetryStatus == 1)
                {
                    GUI.color = new Color(1f, 0.6f, 0f); // Orange for one-way
                }
                else if (symmetryStatus == 2)
                {
                    GUI.color = Color.green; // Green for symmetric
                }

                GUILayout.Label(neighbor);
                GUI.color = Color.white;

                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    RemoveNeighbor(entry, neighbor);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // Middle: Add/Remove buttons
            EditorGUILayout.BeginVertical(GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("←", GUILayout.Height(25)))
            {
                // Remove will be handled by X button in list
            }
            GUILayout.Space(5);
            if (GUILayout.Button("→", GUILayout.Height(25)))
            {
                ShowAddNeighborMenu(entry);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();

            // Right: Available zones
            EditorGUILayout.BeginVertical("box", GUILayout.Width(200));
            GUILayout.Label("Available Zones", EditorStyles.boldLabel);

            availableListScroll = EditorGUILayout.BeginScrollView(availableListScroll, GUILayout.Height(150));
            List<string> availableZones = GetAvailableZones(entry);
            foreach (string zoneName in availableZones)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+", GUILayout.Width(20)))
                {
                    AddNeighbor(entry, zoneName);
                }
                GUILayout.Label(zoneName);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Make Symmetric button
            EditorGUILayout.Space(5);
            if (GUILayout.Button(new GUIContent("Make All Neighbors Symmetric",
                "For each neighbor of this zone, add a reverse relationship so they also see this zone (if A sees B, make B see A)"),
                GUILayout.Height(25)))
            {
                MakeAllNeighborsSymmetric(entry);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawNamedStaticsSection()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Named Statics (Rock Formations / Barriers)", EditorStyles.boldLabel);

            if (visZoneData == null)
            {
                EditorGUILayout.HelpBox("No VisZoneData found! Import a world with VisZones enabled.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            VisZoneEntry entry = visZoneData.visTable.Find(e => e.zoneName == selectedZone.zoneName);
            if (entry == null)
            {
                EditorGUILayout.HelpBox($"Zone '{selectedZone.zoneName}' not found in Vis Table!", MessageType.Error);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.HelpBox("Named statics are environmental pieces (rocks, barriers) visible when player is in this zone", MessageType.Info);

            EditorGUILayout.BeginHorizontal();

            // Left: Current named statics
            EditorGUILayout.BeginVertical("box", GUILayout.Width(200));
            GUILayout.Label($"Visible Statics ({entry.fortVisZones.Count})", EditorStyles.boldLabel);

            namedStaticsListScroll = EditorGUILayout.BeginScrollView(namedStaticsListScroll, GUILayout.Height(120));
            for (int i = 0; i < entry.fortVisZones.Count; i++)
            {
                string staticName = entry.fortVisZones[i];
                EditorGUILayout.BeginHorizontal();

                GUILayout.Label(staticName, EditorStyles.miniLabel);

                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    RemoveNamedStatic(entry, staticName);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // Middle: Add/Remove buttons
            EditorGUILayout.BeginVertical(GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("←", GUILayout.Height(25)))
            {
                // Remove will be handled by X button in list
            }
            GUILayout.Space(5);
            if (GUILayout.Button("→", GUILayout.Height(25)))
            {
                ShowAddNamedStaticMenu(entry);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();

            // Right: Available named statics
            EditorGUILayout.BeginVertical("box", GUILayout.Width(200));
            GUILayout.Label("Available in Scene", EditorStyles.boldLabel);

            availableStaticsScroll = EditorGUILayout.BeginScrollView(availableStaticsScroll, GUILayout.Height(120));
            List<string> availableStatics = GetAvailableNamedStatics(entry);
            foreach (string staticName in availableStatics)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+", GUILayout.Width(20)))
                {
                    AddNamedStatic(entry, staticName);
                }
                GUILayout.Label(staticName, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            if (availableStatics.Count == 0)
            {
                EditorGUILayout.LabelField("No named statics found", EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Scan button
            EditorGUILayout.Space(5);
            if (GUILayout.Button(new GUIContent("Scan Scene for Named Statics",
                "Show all named statics imported from world data and their usage across zones"),
                GUILayout.Height(25)))
            {
                ScanForNamedStatics();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawMembersSection()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Members", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            showPropsFilter = GUILayout.Toggle(showPropsFilter, "Props", EditorStyles.toolbarButton);
            showEffectsFilter = GUILayout.Toggle(showEffectsFilter, "Effects", EditorStyles.toolbarButton);
            showNamedStaticsFilter = GUILayout.Toggle(showNamedStaticsFilter, "Named Statics", EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Get members
            List<GameObject> members = GetZoneMembers(selectedZone.zoneName);
            EditorGUILayout.LabelField($"Total: {members.Count}", EditorStyles.miniLabel);

            memberListScroll = EditorGUILayout.BeginScrollView(memberListScroll, GUILayout.Height(120));
            foreach (GameObject member in members)
            {
                EditorGUILayout.BeginHorizontal();

                // Check if Large
                ObjectListInfo info = member.GetComponent<ObjectListInfo>();
                if (info != null && info.visSize == "Large")
                {
                    GUI.color = Color.yellow;
                }

                if (GUILayout.Button(member.name, EditorStyles.label))
                {
                    Selection.activeGameObject = member;
                    EditorGUIUtility.PingObject(member);
                }

                GUI.color = Color.white;

                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    RemoveMemberFromZone(member);
                }

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Add Selected to Zone (A)",
                "Parent currently selected GameObjects to this zone's section (skips Large objects). Hotkey: A"),
                GUILayout.Height(25)))
            {
                AssignSelectedObjectsToZone();
            }
            if (GUILayout.Button(new GUIContent("Auto-Assign Inside Volume",
                "Automatically find and assign all objects inside this zone's collision volume to its section (skips Large objects)"),
                GUILayout.Height(25)))
            {
                AutoAssignObjectsInsideVolume();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawPreviewSection()
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Preview", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox("Preview shows exactly what will be visible when player enters this zone: sections (Z ∪ neighbors) + named statics", MessageType.Info);

            bool isPreviewingThis = previewMode && previewZoneName == selectedZone.zoneName;

            GUI.color = isPreviewingThis ? Color.green : Color.white;
            if (GUILayout.Button(new GUIContent(
                isPreviewingThis ? "Exit Preview (V)" : "Preview This Zone (V)",
                "Show exactly what will be visible when player enters this zone: zone sections (Z ∪ neighbors) + named statics. Hides everything else. Hotkey: V"),
                GUILayout.Height(30)))
            {
                TogglePreview();
            }
            GUI.color = Color.white;

            EditorGUILayout.BeginHorizontal();
            bool persistPreview = EditorPrefs.GetBool("VisZone_PersistPreview", false);
            bool newPersist = GUILayout.Toggle(persistPreview, "Persist preview while editing");
            if (newPersist != persistPreview)
            {
                EditorPrefs.SetBool("VisZone_PersistPreview", newPersist);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // ========== Right Panel: Problems & Tools ==========
        private void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(rightPanelWidth));

            GUILayout.Label("Problems & Tools", EditorStyles.boldLabel);

            // Validation problems
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label($"Problems ({validationProblems.Count})", EditorStyles.boldLabel);

            problemsScroll = EditorGUILayout.BeginScrollView(problemsScroll, GUILayout.Height(200));
            foreach (string problem in validationProblems)
            {
                EditorGUILayout.HelpBox(problem, MessageType.Warning);
            }
            if (validationProblems.Count == 0)
            {
                EditorGUILayout.LabelField("No problems detected", EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Batch tools
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Batch Tools", EditorStyles.boldLabel);

            if (GUILayout.Button(new GUIContent("Auto-Stitch Neighbors",
                "Automatically detect zones with overlapping or adjacent bounds and create neighbor relationships between them"),
                GUILayout.Height(25)))
            {
                AutoStitchNeighbors();
            }

            if (GUILayout.Button(new GUIContent("Auto-Assign Inside Volumes",
                "For all zones, automatically assign objects inside their collision volumes to the corresponding sections (skips Large objects)"),
                GUILayout.Height(25)))
            {
                AutoAssignAllZones();
            }

            if (GUILayout.Button(new GUIContent("Auto-Create All Sections",
                "Create Section-<ZoneName> GameObjects for any zones that don't have them yet"),
                GUILayout.Height(25)))
            {
                CreateAllSections();
            }

            if (GUILayout.Button(new GUIContent("Make All Symmetric",
                "Convert all one-way neighbor relationships to symmetric (if A sees B, make B see A too)"),
                GUILayout.Height(25)))
            {
                MakeAllSymmetric();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Hotkeys reference
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Hotkeys", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("V", "Toggle Preview", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("A", "Assign Selected", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("F", "Frame Zone", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
        }

        // ========== Data Management ==========
        private void RefreshData()
        {
            // Find VisZoneManager
            manager = FindFirstObjectByType<VisZoneManager>();
            if (manager != null)
            {
                visZoneData = manager.visZoneData;
            }

            // Find all VisZoneVolumes
            allZoneVolumes = FindObjectsByType<VisZoneVolume>(FindObjectsSortMode.None).ToList();

            // Sort by name
            allZoneVolumes.Sort((a, b) => a.zoneName.CompareTo(b.zoneName));
        }

        private int GetZoneMemberCount(string zoneName)
        {
            VisZoneSection section = FindObjectsByType<VisZoneSection>(FindObjectsSortMode.None)
                .FirstOrDefault(s => s.zoneName == zoneName);

            if (section != null)
            {
                return section.transform.childCount;
            }

            return 0;
        }

        private int GetNeighborCount(string zoneName)
        {
            if (visZoneData == null) return 0;

            VisZoneEntry entry = visZoneData.visTable.Find(e => e.zoneName == zoneName);
            return entry != null ? entry.visibleZones.Count : 0;
        }

        private List<string> GetAvailableZones(VisZoneEntry currentEntry)
        {
            List<string> available = new List<string>();

            if (visZoneData == null) return available;

            foreach (var zone in visZoneData.visTable)
            {
                // Skip self
                if (zone.zoneName == currentEntry.zoneName)
                    continue;

                // Skip if already a neighbor
                if (currentEntry.visibleZones.Contains(zone.zoneName))
                    continue;

                available.Add(zone.zoneName);
            }

            return available;
        }

        private List<GameObject> GetZoneMembers(string zoneName)
        {
            List<GameObject> members = new List<GameObject>();

            VisZoneSection section = FindObjectsByType<VisZoneSection>(FindObjectsSortMode.None)
                .FirstOrDefault(s => s.zoneName == zoneName);

            if (section != null)
            {
                foreach (Transform child in section.transform)
                {
                    members.Add(child.gameObject);
                }
            }

            return members;
        }

        // ========== Zone Operations ==========
        private void CreateNewZone()
        {
            GameObject zoneObj = new GameObject("collision_zone_NewZone");
            BoxCollider collider = zoneObj.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(50, 100, 50);

            VisZoneVolume volume = zoneObj.AddComponent<VisZoneVolume>();
            volume.displayColor = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.8f, 1f);

            Selection.activeGameObject = zoneObj;
            Undo.RegisterCreatedObjectUndo(zoneObj, "Create Zone");

            RefreshData();
        }

        private void DeleteZone(VisZoneVolume zone)
        {
            if (EditorUtility.DisplayDialog("Delete Zone",
                $"Delete zone '{zone.zoneName}'? This will also remove its section and all neighbor relationships.",
                "Delete", "Cancel"))
            {
                // Remove from vis table
                if (visZoneData != null)
                {
                    visZoneData.visTable.RemoveAll(e => e.zoneName == zone.zoneName);
                    EditorUtility.SetDirty(visZoneData);
                }

                // Delete section
                VisZoneSection section = zone.sectionRoot;
                if (section != null)
                {
                    Undo.DestroyObjectImmediate(section.gameObject);
                }

                // Delete zone object
                Undo.DestroyObjectImmediate(zone.gameObject);

                selectedZone = null;
                RefreshData();
            }
        }

        private void RenameZone(VisZoneVolume zone, string newName)
        {
            if (string.IsNullOrEmpty(newName) || newName == zone.zoneName)
                return;

            // Check for duplicates
            if (allZoneVolumes.Any(z => z != zone && z.zoneName == newName))
            {
                EditorUtility.DisplayDialog("Rename Failed", $"Zone '{newName}' already exists!", "OK");
                return;
            }

            Undo.RecordObject(zone, "Rename Zone");
            string oldName = zone.zoneName;
            zone.zoneName = newName;
            zone.gameObject.name = $"collision_zone_{newName}";

            // Update vis table
            if (visZoneData != null)
            {
                VisZoneEntry entry = visZoneData.visTable.Find(e => e.zoneName == oldName);
                if (entry != null)
                {
                    entry.zoneName = newName;
                    EditorUtility.SetDirty(visZoneData);
                }
            }

            // Rename section
            if (zone.sectionRoot != null)
            {
                Undo.RecordObject(zone.sectionRoot, "Rename Section");
                zone.sectionRoot.zoneName = newName;
                zone.sectionRoot.gameObject.name = $"Section-{newName}";
            }

            EditorUtility.SetDirty(zone);
        }

        private void MoveZoneToBoundsCenter(VisZoneVolume zone)
        {
            Bounds bounds = zone.GetBounds();
            Undo.RecordObject(zone.transform, "Move Zone to Bounds Center");
            zone.transform.position = bounds.center;
            EditorUtility.SetDirty(zone);
        }

        private void RebuildZoneBounds(VisZoneVolume zone)
        {
            Bounds bounds = zone.GetBounds();
            if (zone.sectionRoot != null)
            {
                Undo.RecordObject(zone.sectionRoot, "Rebuild Zone Bounds");
                zone.sectionRoot.zoneBounds = bounds;
                zone.sectionRoot.transform.position = bounds.center;
                EditorUtility.SetDirty(zone.sectionRoot);
            }
        }

        // ========== Neighbor Operations ==========
        private void AddNeighbor(VisZoneEntry entry, string neighborName)
        {
            if (visZoneData != null)
            {
                Undo.RecordObject(visZoneData, "Add Neighbor");
                visZoneData.AddNeighbor(entry.zoneName, neighborName);
                EditorUtility.SetDirty(visZoneData);
                RefreshPreviewIfActive();
                Repaint();
            }
        }

        private void RemoveNeighbor(VisZoneEntry entry, string neighborName)
        {
            if (visZoneData != null)
            {
                Undo.RecordObject(visZoneData, "Remove Neighbor");
                visZoneData.RemoveNeighbor(entry.zoneName, neighborName);
                EditorUtility.SetDirty(visZoneData);
                RefreshPreviewIfActive();
                Repaint();
            }
        }

        private void ShowAddNeighborMenu(VisZoneEntry entry)
        {
            GenericMenu menu = new GenericMenu();
            List<string> available = GetAvailableZones(entry);

            foreach (string zoneName in available)
            {
                menu.AddItem(new GUIContent(zoneName), false, () => AddNeighbor(entry, zoneName));
            }

            if (available.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No available zones"));
            }

            menu.ShowAsContext();
        }

        private void MakeAllNeighborsSymmetric(VisZoneEntry entry)
        {
            if (visZoneData != null)
            {
                Undo.RecordObject(visZoneData, "Make Neighbors Symmetric");
                foreach (string neighbor in entry.visibleZones)
                {
                    visZoneData.AddNeighbor(neighbor, entry.zoneName);
                }
                EditorUtility.SetDirty(visZoneData);
                RefreshPreviewIfActive();
                Debug.Log($"Made all neighbors of '{entry.zoneName}' symmetric");
            }
        }

        // ========== Named Static Operations ==========
        private List<string> GetAvailableNamedStatics(VisZoneEntry currentEntry)
        {
            HashSet<string> available = new HashSet<string>();

            if (visZoneData == null)
                return new List<string>();

            // Collect all named statics from OTHER zones' fortVisZones lists
            foreach (var entry in visZoneData.visTable)
            {
                if (entry.zoneName == currentEntry.zoneName)
                    continue; // Skip current zone

                foreach (string staticName in entry.fortVisZones)
                {
                    // Only add if not already in current zone's list
                    if (!currentEntry.fortVisZones.Contains(staticName))
                    {
                        available.Add(staticName);
                    }
                }
            }

            // Additionally scan scene for common named static containers
            string[] staticContainerPatterns = new string[]
            {
                "island_terrain",
                "island_nat_wall",
                "island_nat_rock",
                "island_nat_formation",
                "island_barrier"
            };

            GameObject[] allGameObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (GameObject obj in allGameObjects)
            {
                foreach (string pattern in staticContainerPatterns)
                {
                    if (obj.name.StartsWith(pattern))
                    {
                        // Add all children of this container as potential named statics
                        foreach (Transform child in obj.transform)
                        {
                            if (child != null && !currentEntry.fortVisZones.Contains(child.name))
                            {
                                available.Add(child.name);
                            }
                        }
                        break;
                    }
                }
            }

            // Sort and return
            List<string> result = available.ToList();
            result.Sort();
            return result;
        }

        private void AddNamedStatic(VisZoneEntry entry, string staticName)
        {
            if (visZoneData != null)
            {
                Undo.RecordObject(visZoneData, "Add Named Static");
                if (!entry.fortVisZones.Contains(staticName))
                {
                    entry.fortVisZones.Add(staticName);
                    EditorUtility.SetDirty(visZoneData);
                    RefreshPreviewIfActive();
                    Debug.Log($"[VisZone] Added named static '{staticName}' to zone '{entry.zoneName}'");
                }
                Repaint();
            }
        }

        private void RemoveNamedStatic(VisZoneEntry entry, string staticName)
        {
            if (visZoneData != null)
            {
                Undo.RecordObject(visZoneData, "Remove Named Static");
                if (entry.fortVisZones.Remove(staticName))
                {
                    EditorUtility.SetDirty(visZoneData);
                    RefreshPreviewIfActive();
                    Debug.Log($"[VisZone] Removed named static '{staticName}' from zone '{entry.zoneName}'");
                }
                Repaint();
            }
        }

        private void ShowAddNamedStaticMenu(VisZoneEntry entry)
        {
            GenericMenu menu = new GenericMenu();
            List<string> available = GetAvailableNamedStatics(entry);

            foreach (string staticName in available)
            {
                menu.AddItem(new GUIContent(staticName), false, () => AddNamedStatic(entry, staticName));
            }

            if (available.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No available named statics"));
            }

            menu.ShowAsContext();
        }

        private void ScanForNamedStatics()
        {
            if (visZoneData == null)
            {
                EditorUtility.DisplayDialog("Scan Failed", "No VisZoneData found!", "OK");
                return;
            }

            // Collect all named statics from imported world data
            Dictionary<string, List<string>> staticsByZone = new Dictionary<string, List<string>>();
            HashSet<string> allStatics = new HashSet<string>();

            foreach (var entry in visZoneData.visTable)
            {
                if (entry.fortVisZones.Count > 0)
                {
                    staticsByZone[entry.zoneName] = new List<string>(entry.fortVisZones);
                    foreach (string staticName in entry.fortVisZones)
                    {
                        allStatics.Add(staticName);
                    }
                }
            }

            // Additionally scan scene for common named static containers
            HashSet<string> sceneStatics = new HashSet<string>();
            string[] staticContainerPatterns = new string[]
            {
                "island_terrain",
                "island_nat_wall",
                "island_nat_rock",
                "island_nat_formation",
                "island_barrier"
            };

            GameObject[] allGameObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (GameObject obj in allGameObjects)
            {
                foreach (string pattern in staticContainerPatterns)
                {
                    if (obj.name.StartsWith(pattern))
                    {
                        // Add all children of this container as potential named statics
                        foreach (Transform child in obj.transform)
                        {
                            if (child != null && !allStatics.Contains(child.name))
                            {
                                sceneStatics.Add(child.name);
                            }
                        }
                        break;
                    }
                }
            }

            // Log results
            Debug.Log($"[VisZone] Named Statics Report:");
            Debug.Log($"  From imported world data: {allStatics.Count}");
            Debug.Log($"  From scene containers: {sceneStatics.Count}");
            Debug.Log($"  Zones with named statics: {staticsByZone.Count}");
            Debug.Log("");

            // List all unique statics from world data
            List<string> sortedStatics = allStatics.ToList();
            sortedStatics.Sort();
            Debug.Log($"  Named statics (from world data):");
            foreach (string name in sortedStatics)
            {
                Debug.Log($"    - {name}");
            }

            // List all scene statics
            if (sceneStatics.Count > 0)
            {
                Debug.Log("");
                List<string> sortedSceneStatics = sceneStatics.ToList();
                sortedSceneStatics.Sort();
                Debug.Log($"  Named statics (from scene containers):");
                foreach (string name in sortedSceneStatics)
                {
                    Debug.Log($"    - {name}");
                }
            }

            Debug.Log("");
            Debug.Log($"  Breakdown by zone:");
            foreach (var kvp in staticsByZone)
            {
                Debug.Log($"    '{kvp.Key}' → {kvp.Value.Count} statics: [{string.Join(", ", kvp.Value)}]");
            }

            // Show summary dialog
            EditorUtility.DisplayDialog("Scan Complete",
                $"Total from world data: {allStatics.Count}\n" +
                $"Total from scene containers: {sceneStatics.Count}\n" +
                $"Zones with named statics: {staticsByZone.Count}\n\n" +
                $"Check Console for full breakdown.",
                "OK");
        }

        // ========== Member Operations ==========
        private void AssignSelectedObjectsToZone()
        {
            if (selectedZone == null || Selection.gameObjects.Length == 0)
                return;

            VisZoneSection section = selectedZone.sectionRoot;
            if (section == null)
            {
                Debug.LogWarning($"No section found for zone '{selectedZone.zoneName}'");
                return;
            }

            int assigned = 0;
            foreach (GameObject obj in Selection.gameObjects)
            {
                // Don't parent if it's a zone or section itself
                if (obj.GetComponent<VisZoneVolume>() != null || obj.GetComponent<VisZoneSection>() != null)
                    continue;

                // Check if marked as Large
                ObjectListInfo info = obj.GetComponent<ObjectListInfo>();
                if (info != null && info.visSize == "Large")
                {
                    Debug.LogWarning($"Skipping '{obj.name}' - marked as Large (should not be in section)");
                    continue;
                }

                Undo.SetTransformParent(obj.transform, section.transform, "Assign to Zone");
                assigned++;
            }

            Debug.Log($"Assigned {assigned} objects to zone '{selectedZone.zoneName}'");
            RefreshData();
        }

        private void RemoveMemberFromZone(GameObject member)
        {
            Undo.SetTransformParent(member.transform, null, "Remove from Zone");
            RefreshData();
        }

        private void AutoAssignObjectsInsideVolume()
        {
            if (selectedZone == null || selectedZone.zoneCollider == null)
                return;

            VisZoneSection section = selectedZone.sectionRoot;
            if (section == null)
            {
                Debug.LogWarning($"No section found for zone '{selectedZone.zoneName}'");
                return;
            }

            ObjectListInfo[] allObjects = FindObjectsByType<ObjectListInfo>(FindObjectsSortMode.None);

            Debug.Log($"[VisZone] Starting auto-assign for zone '{selectedZone.zoneName}'");
            Debug.Log($"[VisZone] Found {allObjects.Length} total objects with ObjectListInfo");
            Debug.Log($"[VisZone] Zone collider type: {selectedZone.zoneCollider.GetType().Name}");
            Debug.Log($"[VisZone] Zone bounds: {selectedZone.GetBounds()}");

            int assigned = 0;
            int skippedLarge = 0;
            int skippedAlreadyAssigned = 0;
            int outsideZone = 0;
            int tested = 0;

            foreach (var info in allObjects)
            {
                if (info == null) continue;

                tested++;

                // Skip if Large
                if (info.visSize == "Large")
                {
                    skippedLarge++;
                    continue;
                }

                // Skip if already has a zone
                if (!string.IsNullOrEmpty(info.visZone))
                {
                    skippedAlreadyAssigned++;
                    continue;
                }

                // Check if point is inside the collider
                Vector3 position = info.transform.position;
                bool isInside = false;

                // For MeshColliders, use bounds check (ClosestPoint is unreliable for non-convex meshes)
                MeshCollider meshCollider = selectedZone.zoneCollider as MeshCollider;
                if (meshCollider != null)
                {
                    Bounds bounds = meshCollider.bounds;
                    isInside = bounds.Contains(position);
                }
                else
                {
                    // For other collider types, use ClosestPoint method
                    Vector3 closestPoint = selectedZone.zoneCollider.ClosestPoint(position);
                    float distance = Vector3.Distance(position, closestPoint);
                    isInside = distance < 1.0f;
                }

                if (assigned < 5) // Log first few tests for debugging
                {
                    Debug.Log($"[VisZone] Testing '{info.gameObject.name}': pos={position}, collider={selectedZone.zoneCollider.GetType().Name}, inside={isInside}");
                }

                if (isInside)
                {
                    Undo.SetTransformParent(info.transform, section.transform, "Auto-Assign to Zone");
                    info.visZone = selectedZone.zoneName;
                    EditorUtility.SetDirty(info);
                    assigned++;
                    Debug.Log($"[VisZone] ✓ Assigned '{info.gameObject.name}' to zone '{selectedZone.zoneName}'");
                }
                else
                {
                    outsideZone++;
                }
            }

            Debug.Log($"[VisZone] Auto-assign complete: {assigned} assigned, {outsideZone} outside zone, {skippedLarge} Large, {skippedAlreadyAssigned} already assigned (tested {tested} total)");
            RefreshData();
        }

        // ========== Preview Operations ==========
        private void TogglePreview()
        {
            if (selectedZone == null)
                return;

            // If we're in preview mode
            if (previewMode)
            {
                // If previewing the same zone, exit preview
                if (previewZoneName == selectedZone.zoneName)
                {
                    ExitPreviewMode();
                }
                // If previewing a different zone, switch to the new zone
                else
                {
                    // Exit current preview first, then enter new preview
                    ExitPreviewMode();
                    EnterPreviewMode(selectedZone.zoneName);
                }
            }
            // If not in preview mode, enter it
            else
            {
                EnterPreviewMode(selectedZone.zoneName);
            }
        }

        private void EnterPreviewMode(string zoneName)
        {
            if (manager == null)
            {
                EditorUtility.DisplayDialog("Preview Failed",
                    "No VisZoneManager found!\n\nMake sure you've imported a world with VisZones enabled.",
                    "OK");
                Debug.LogWarning("[VisZone] Cannot enter preview mode: No VisZoneManager found");
                return;
            }

            if (visZoneData == null)
            {
                EditorUtility.DisplayDialog("Preview Failed",
                    "No VisZoneData found!\n\nMake sure you've imported a world with VisZones enabled.",
                    "OK");
                Debug.LogWarning("[VisZone] Cannot enter preview mode: No VisZoneData found");
                return;
            }

            // Check if zone exists in vis table
            if (!visZoneData.HasZone(zoneName))
            {
                EditorUtility.DisplayDialog("Preview Failed",
                    $"Zone '{zoneName}' not found in Vis Table!\n\nThe zone may need to be added to the VisZoneData component.",
                    "OK");
                Debug.LogWarning($"[VisZone] Cannot preview '{zoneName}': Zone not in Vis Table");
                return;
            }

            // Check if sections exist
            VisZoneSection[] allSections = FindObjectsByType<VisZoneSection>(FindObjectsSortMode.None);
            if (allSections.Length == 0)
            {
                EditorUtility.DisplayDialog("Preview Failed",
                    "No VisZone Sections found in scene!\n\nClick 'Create Sections' in the toolbar to create Section-* GameObjects.",
                    "OK");
                Debug.LogWarning("[VisZone] Cannot preview: No sections found. Click 'Create Sections' to generate them.");
                return;
            }

            previewMode = true;
            previewZoneName = zoneName;

            // Clear previous state tracking
            previewOriginalStates.Clear();
            previewOriginalStaticStates.Clear();

            // Ensure dictionaries are built (needed in edit mode)
            manager.EnsureDictionariesBuilt();

            // Use shared visibility update method with state saving
            manager.UpdateVisibilityForZone(zoneName, previewOriginalStates, previewOriginalStaticStates);

            Debug.Log($"[VisZone] Entered preview mode for zone '{zoneName}'");
            SceneView.RepaintAll();
            Repaint();
        }

        private void ExitPreviewMode()
        {
            if (!previewMode)
                return;

            string previousZone = previewZoneName;
            previewMode = false;
            previewZoneName = "";

            // Use shared restore method
            if (manager != null)
            {
                manager.RestoreVisibilityStates(previewOriginalStates, previewOriginalStaticStates);
            }

            // Clear the state tracking
            previewOriginalStates.Clear();
            previewOriginalStaticStates.Clear();

            Debug.Log($"[VisZone] Exited preview mode (was previewing '{previousZone}')");
            SceneView.RepaintAll();
            Repaint();
        }

        private void FrameZone()
        {
            if (selectedZone != null && SceneView.lastActiveSceneView != null)
            {
                Bounds bounds = selectedZone.GetBounds();
                SceneView.lastActiveSceneView.Frame(bounds, false);
            }
        }

        /// <summary>
        /// Refresh preview if currently active for the selected zone
        /// Call this after modifying neighbors or named statics
        /// </summary>
        private void RefreshPreviewIfActive()
        {
            if (previewMode && selectedZone != null && previewZoneName == selectedZone.zoneName && manager != null)
            {
                // Clear the saved states and rebuild preview with updated neighbor list
                previewOriginalStates.Clear();
                previewOriginalStaticStates.Clear();

                // Re-apply visibility with new neighbors/statics
                manager.UpdateVisibilityForZone(previewZoneName, previewOriginalStates, previewOriginalStaticStates);
                SceneView.RepaintAll();
                Debug.Log($"[VisZone] Refreshed preview for zone '{previewZoneName}' with updated neighbors/statics");
            }
        }

        // ========== Batch Operations ==========
        private void DiscoverAndCreateZones()
        {
            Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            int created = 0;
            int skipped = 0;

            foreach (Transform t in allTransforms)
            {
                if (t == null || !t.name.StartsWith("collision_zone_"))
                    continue;

                try
                {
                    VisZoneVolume existing = t.GetComponent<VisZoneVolume>();
                    if (existing == null)
                    {
                        // Ensure collider exists (required by VisZoneVolume)
                        Collider collider = t.GetComponent<Collider>();
                        if (collider == null)
                        {
                            Debug.LogWarning($"[VisZone] Skipping '{t.name}': No collider found (required for VisZoneVolume)");
                            skipped++;
                            continue;
                        }

                        // Add VisZoneVolume component
                        VisZoneVolume volume = Undo.AddComponent<VisZoneVolume>(t.gameObject);

                        if (volume != null)
                        {
                            volume.displayColor = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.8f, 1f);
                            created++;
                        }
                        else
                        {
                            Debug.LogError($"[VisZone] Failed to add VisZoneVolume to '{t.name}'");
                            skipped++;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[VisZone] Error processing '{t.name}': {ex.Message}");
                    skipped++;
                }
            }

            RefreshData();

            if (created > 0)
            {
                Debug.Log($"[VisZone] Discovered {created} new zones");
            }
            if (skipped > 0)
            {
                Debug.LogWarning($"[VisZone] Skipped {skipped} objects (no collider or errors)");
            }
            if (created == 0 && skipped == 0)
            {
                Debug.Log("[VisZone] No new collision_zone_* objects found");
            }
        }

        private void CreateAllSections()
        {
            int created = 0;

            // Find or create VisZone_Sections container
            GameObject sectionsContainer = GameObject.Find("VisZone_Sections");
            if (sectionsContainer == null && manager != null)
            {
                sectionsContainer = new GameObject("VisZone_Sections");
                sectionsContainer.transform.SetParent(manager.transform, false);
                Undo.RegisterCreatedObjectUndo(sectionsContainer, "Create Sections Container");
            }

            foreach (VisZoneVolume zone in allZoneVolumes)
            {
                if (zone == null)
                    continue;

                if (zone.sectionRoot == null)
                {
                    try
                    {
                        // Create section
                        GameObject sectionObj = new GameObject($"Section-{zone.zoneName}");

                        // Parent to container if it exists
                        if (sectionsContainer != null)
                        {
                            sectionObj.transform.SetParent(sectionsContainer.transform, false);
                        }

                        VisZoneSection section = Undo.AddComponent<VisZoneSection>(sectionObj);
                        section.zoneName = zone.zoneName;
                        section.zoneBounds = zone.GetBounds();
                        sectionObj.transform.position = section.zoneBounds.center;

                        // Link section to volume
                        zone.sectionRoot = section;
                        section.zoneCollider = zone.zoneCollider;

                        Undo.RegisterCreatedObjectUndo(sectionObj, "Create Section");
                        EditorUtility.SetDirty(zone);
                        created++;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[VisZone] Failed to create section for '{zone.zoneName}': {ex.Message}");
                    }
                }
            }

            RefreshData();
            Debug.Log($"[VisZone] Created {created} new sections");
        }

        private void AutoStitchNeighbors()
        {
            if (visZoneData == null)
            {
                Debug.LogWarning("[VisZone] Cannot auto-stitch: No VisZoneData found");
                return;
            }

            int stitched = 0;

            foreach (VisZoneVolume zone1 in allZoneVolumes)
            {
                if (zone1 == null || zone1.zoneCollider == null)
                    continue;

                Bounds bounds1 = zone1.GetBounds();

                foreach (VisZoneVolume zone2 in allZoneVolumes)
                {
                    if (zone2 == null || zone1 == zone2 || zone2.zoneCollider == null)
                        continue;

                    Bounds bounds2 = zone2.GetBounds();

                    // Check if bounds intersect or are close
                    if (bounds1.Intersects(bounds2) || Vector3.Distance(bounds1.center, bounds2.center) < (bounds1.extents.magnitude + bounds2.extents.magnitude) * 1.2f)
                    {
                        // Add neighbor relationships if not already present
                        if (visZoneData.AddNeighbor(zone1.zoneName, zone2.zoneName))
                        {
                            stitched++;
                        }
                        if (visZoneData.AddNeighbor(zone2.zoneName, zone1.zoneName))
                        {
                            stitched++;
                        }
                    }
                }
            }

            if (stitched > 0)
            {
                EditorUtility.SetDirty(visZoneData);
            }

            Debug.Log($"[VisZone] Auto-stitched {stitched} neighbor relationships");
        }

        private void AutoAssignAllZones()
        {
            int totalAssigned = 0;
            int skippedLarge = 0;
            int skippedAlreadyAssigned = 0;

            // Get all objects once
            ObjectListInfo[] allObjects = FindObjectsByType<ObjectListInfo>(FindObjectsSortMode.None);

            foreach (VisZoneVolume zone in allZoneVolumes)
            {
                if (zone == null || zone.zoneCollider == null || zone.sectionRoot == null)
                    continue;

                foreach (var info in allObjects)
                {
                    if (info == null)
                        continue;

                    // Skip if Large
                    if (info.visSize == "Large")
                    {
                        skippedLarge++;
                        continue;
                    }

                    // Skip if already has a zone
                    if (!string.IsNullOrEmpty(info.visZone))
                    {
                        skippedAlreadyAssigned++;
                        continue;
                    }

                    // Check if point is inside the collider
                    Vector3 position = info.transform.position;
                    bool isInside = false;

                    // For MeshColliders, use bounds check (ClosestPoint is unreliable for non-convex meshes)
                    MeshCollider meshCollider = zone.zoneCollider as MeshCollider;
                    if (meshCollider != null)
                    {
                        Bounds bounds = meshCollider.bounds;
                        isInside = bounds.Contains(position);
                    }
                    else
                    {
                        // For other collider types, use ClosestPoint method
                        Vector3 closestPoint = zone.zoneCollider.ClosestPoint(position);
                        float distance = Vector3.Distance(position, closestPoint);
                        isInside = distance < 1.0f;
                    }

                    if (isInside)
                    {
                        Undo.SetTransformParent(info.transform, zone.sectionRoot.transform, "Auto-Assign to Zone");
                        info.visZone = zone.zoneName;
                        EditorUtility.SetDirty(info);
                        totalAssigned++;
                    }
                }
            }

            Debug.Log($"[VisZone] Auto-assigned {totalAssigned} total objects across all zones (skipped {skippedLarge} Large, {skippedAlreadyAssigned} already assigned)");
            RefreshData();
        }

        private void MakeAllSymmetric()
        {
            if (visZoneData == null)
                return;

            Undo.RecordObject(visZoneData, "Make All Symmetric");

            List<(string, string)> oneWayNeighbors = visZoneData.GetOneWayNeighbors();
            foreach (var (zone1, zone2) in oneWayNeighbors)
            {
                visZoneData.AddNeighbor(zone2, zone1);
            }

            EditorUtility.SetDirty(visZoneData);
            Debug.Log($"Made {oneWayNeighbors.Count} neighbor relationships symmetric");
        }

        private void RunValidation()
        {
            validationProblems.Clear();

            // Check for VisZoneManager
            if (manager == null)
            {
                validationProblems.Add("No VisZoneManager found in scene");
            }

            // Check for VisZoneData
            if (visZoneData == null)
            {
                validationProblems.Add("No VisZoneData found");
            }

            // Check each zone
            foreach (VisZoneVolume zone in allZoneVolumes)
            {
                if (zone == null)
                    continue;

                // Check for collider
                if (zone.zoneCollider == null)
                {
                    validationProblems.Add($"Zone '{zone.zoneName}': Missing collider");
                }

                // Check for section
                if (zone.sectionRoot == null)
                {
                    validationProblems.Add($"Zone '{zone.zoneName}': Missing section root");
                }

                // Check for duplicate names
                int duplicates = allZoneVolumes.Count(z => z != null && z.zoneName == zone.zoneName);
                if (duplicates > 1)
                {
                    validationProblems.Add($"Zone '{zone.zoneName}': Duplicate name found");
                }

                // Check if in vis table
                if (visZoneData != null && !visZoneData.HasZone(zone.zoneName))
                {
                    validationProblems.Add($"Zone '{zone.zoneName}': Not found in Vis Table");
                }
            }

            // Check for one-way neighbors
            if (visZoneData != null)
            {
                List<(string, string)> oneWayNeighbors = visZoneData.GetOneWayNeighbors();
                foreach (var (zone1, zone2) in oneWayNeighbors)
                {
                    validationProblems.Add($"One-way neighbor: '{zone1}' → '{zone2}' (not symmetric)");
                }
            }

            // Check for Large objects in sections
            VisZoneSection[] allSections = FindObjectsByType<VisZoneSection>(FindObjectsSortMode.None);
            foreach (var section in allSections)
            {
                if (section == null)
                    continue;

                foreach (Transform child in section.transform)
                {
                    if (child == null)
                        continue;

                    ObjectListInfo info = child.GetComponent<ObjectListInfo>();
                    if (info != null && info.visSize == "Large")
                    {
                        validationProblems.Add($"Section '{section.zoneName}': Contains Large object '{child.name}' (should not be sectioned)");
                    }
                }
            }

            Debug.Log($"[VisZone] Validation complete: {validationProblems.Count} problems found");
            Repaint();
        }
    }
}
