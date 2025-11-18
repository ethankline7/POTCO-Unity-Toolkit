using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Runtime animation browser for previewing mp_ and fp_ animations.
/// Attach to a character GameObject with an Animator component.
/// </summary>
public class AnimationBrowser : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Filter animations by prefix (mp_, fp_, or leave empty for all)")]
    public string filterPrefix = "mp_";

    [Tooltip("Search filter (case insensitive)")]
    public string searchFilter = "";

    [Header("UI Settings")]
    public KeyCode toggleUIKey = KeyCode.F1;
    public KeyCode nextAnimKey = KeyCode.RightArrow;
    public KeyCode prevAnimKey = KeyCode.LeftArrow;
    public KeyCode playPauseKey = KeyCode.Space;

    private bool showUI = true;
    private Vector2 scrollPosition;
    private List<AnimationClip> allAnimations = new List<AnimationClip>();
    private List<AnimationClip> filteredAnimations = new List<AnimationClip>();
    private int currentIndex = 0;
    private Animator animator;
    private string lastSearchFilter = "";
    private string lastFilterPrefix = "";
    private bool isPlaying = false;

    // Playables API fields
    private PlayableGraph playableGraph;
    private AnimationClipPlayable clipPlayable;
    private bool graphCreated = false;

    private GUIStyle boxStyle;
    private GUIStyle buttonStyle;
    private GUIStyle selectedButtonStyle;
    private GUIStyle labelStyle;
    private bool stylesInitialized = false;

    void Start()
    {
        // Get Animator component
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("AnimationBrowser: No Animator component found! Please attach an Animator to this GameObject.");
            enabled = false;
            return;
        }

        LoadAllAnimations();
        FilterAnimations();

        if (filteredAnimations.Count > 0)
        {
            PlayAnimation(0);
        }
    }

    void OnDestroy()
    {
        // Clean up the playable graph when destroyed
        if (graphCreated && playableGraph.IsValid())
        {
            playableGraph.Destroy();
        }
    }

    void Update()
    {
        // Toggle UI
        if (Input.GetKeyDown(toggleUIKey))
        {
            showUI = !showUI;
        }

        // Next animation
        if (Input.GetKeyDown(nextAnimKey))
        {
            NextAnimation();
        }

        // Previous animation
        if (Input.GetKeyDown(prevAnimKey))
        {
            PreviousAnimation();
        }

        // Play/Pause
        if (Input.GetKeyDown(playPauseKey))
        {
            TogglePlayPause();
        }

        // Check if filters changed
        if (searchFilter != lastSearchFilter || filterPrefix != lastFilterPrefix)
        {
            FilterAnimations();
            lastSearchFilter = searchFilter;
            lastFilterPrefix = filterPrefix;
        }
    }

    void LoadAllAnimations()
    {
        allAnimations.Clear();

        // Load all animation clips from Resources
        var clips = Resources.LoadAll<AnimationClip>("");

        foreach (var clip in clips)
        {
            if (clip == null) continue;

            // Get the base name (remove "_anim" suffix if present)
            string clipName = clip.name;
            if (clipName.EndsWith("_anim"))
            {
                clipName = clipName.Substring(0, clipName.Length - 5);
            }

            // Only include mp_ and fp_ animations
            if (clipName.StartsWith("mp_") || clipName.StartsWith("fp_"))
            {
                allAnimations.Add(clip);
            }
        }

        allAnimations = allAnimations.OrderBy(c => c.name).ToList();
        Debug.Log($"AnimationBrowser: Loaded {allAnimations.Count} animations");

        if (allAnimations.Count == 0)
        {
            Debug.LogWarning("AnimationBrowser: No mp_ or fp_ animations found in Resources! Make sure your animation .egg files are in a Resources folder.");
        }
    }

    void FilterAnimations()
    {
        filteredAnimations.Clear();

        foreach (var clip in allAnimations)
        {
            // Get the base name for filtering
            string clipName = clip.name;
            if (clipName.EndsWith("_anim"))
            {
                clipName = clipName.Substring(0, clipName.Length - 5);
            }

            // Apply prefix filter
            bool matchesPrefix = string.IsNullOrEmpty(filterPrefix) || clipName.StartsWith(filterPrefix);

            // Apply search filter
            bool matchesSearch = string.IsNullOrEmpty(searchFilter) ||
                                clipName.ToLower().Contains(searchFilter.ToLower());

            if (matchesPrefix && matchesSearch)
            {
                filteredAnimations.Add(clip);
            }
        }

        // Reset index if out of bounds
        if (currentIndex >= filteredAnimations.Count)
        {
            currentIndex = filteredAnimations.Count > 0 ? 0 : -1;
        }

        if (filteredAnimations.Count > 0 && currentIndex >= 0)
        {
            PlayAnimation(currentIndex);
        }
    }

    void PlayAnimation(int index)
    {
        if (index < 0 || index >= filteredAnimations.Count) return;

        currentIndex = index;
        var clip = filteredAnimations[currentIndex];

        if (clip == null)
        {
            Debug.LogWarning($"AnimationBrowser: Clip at index {index} is null");
            return;
        }

        // Destroy old graph if it exists and reset to bind pose
        if (graphCreated && playableGraph.IsValid())
        {
            playableGraph.Destroy();
        }

        // Reset all animator parameters to force a clean state
        if (animator != null)
        {
            animator.Rebind();
        }

        // Create a new PlayableGraph for this animation
        playableGraph = PlayableGraph.Create($"AnimationBrowser_{clip.name}");

        // Create an AnimationClipPlayable
        clipPlayable = AnimationClipPlayable.Create(playableGraph, clip);
        clipPlayable.SetDuration(clip.length);

        // Create an output and connect it to the Animator
        var output = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);
        output.SetSourcePlayable(clipPlayable);

        // Play the graph
        playableGraph.Play();
        graphCreated = true;
        isPlaying = true;
    }

    void NextAnimation()
    {
        if (filteredAnimations.Count == 0) return;

        currentIndex = (currentIndex + 1) % filteredAnimations.Count;
        PlayAnimation(currentIndex);
    }

    void PreviousAnimation()
    {
        if (filteredAnimations.Count == 0) return;

        currentIndex--;
        if (currentIndex < 0) currentIndex = filteredAnimations.Count - 1;
        PlayAnimation(currentIndex);
    }

    void TogglePlayPause()
    {
        if (filteredAnimations.Count == 0 || currentIndex < 0) return;
        if (!graphCreated || !playableGraph.IsValid()) return;

        if (isPlaying)
        {
            playableGraph.Stop();
            isPlaying = false;
        }
        else
        {
            playableGraph.Play();
            isPlaying = true;
        }
    }

    void InitializeStyles()
    {
        if (stylesInitialized) return;

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 10, 10, 10);

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.alignment = TextAnchor.MiddleLeft;
        buttonStyle.padding = new RectOffset(10, 10, 5, 5);

        selectedButtonStyle = new GUIStyle(buttonStyle);
        selectedButtonStyle.normal.background = Texture2D.whiteTexture;
        selectedButtonStyle.normal.textColor = Color.black;

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontStyle = FontStyle.Bold;

        stylesInitialized = true;
    }

    void OnGUI()
    {
        if (!showUI) return;

        InitializeStyles();

        float windowWidth = 400;
        float windowHeight = Screen.height * 0.8f;
        Rect windowRect = new Rect(10, 10, windowWidth, windowHeight);

        GUI.Box(windowRect, "");
        GUILayout.BeginArea(new Rect(windowRect.x + 10, windowRect.y + 10, windowRect.width - 20, windowRect.height - 20));

        // Title
        GUILayout.Label("Animation Browser", labelStyle);
        GUILayout.Space(10);

        // Current animation info
        if (currentIndex >= 0 && currentIndex < filteredAnimations.Count)
        {
            var currentClip = filteredAnimations[currentIndex];
            string displayName = currentClip.name.EndsWith("_anim") ? currentClip.name.Substring(0, currentClip.name.Length - 5) : currentClip.name;
            GUILayout.Label($"Current: {displayName}");
            GUILayout.Label($"Length: {currentClip.length:F2}s | FPS: {currentClip.frameRate}");
            GUILayout.Label($"Playing: {(isPlaying ? "Yes" : "No")}");
        }
        else
        {
            GUILayout.Label("No animations available");
        }

        GUILayout.Space(10);

        // Filter controls
        GUILayout.BeginHorizontal();
        GUILayout.Label("Prefix:", GUILayout.Width(50));
        filterPrefix = GUILayout.TextField(filterPrefix, GUILayout.Width(100));
        if (GUILayout.Button("mp_", GUILayout.Width(50))) filterPrefix = "mp_";
        if (GUILayout.Button("fp_", GUILayout.Width(50))) filterPrefix = "fp_";
        if (GUILayout.Button("All", GUILayout.Width(50))) filterPrefix = "";
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Search:", GUILayout.Width(50));
        searchFilter = GUILayout.TextField(searchFilter);
        GUILayout.EndHorizontal();

        GUILayout.Label($"Showing {filteredAnimations.Count} of {allAnimations.Count} animations");

        GUILayout.Space(10);

        // Playback controls
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◄ Prev", GUILayout.Height(30))) PreviousAnimation();
        if (GUILayout.Button(isPlaying ? "❚❚ Pause" : "▶ Play", GUILayout.Height(30))) TogglePlayPause();
        if (GUILayout.Button("Next ►", GUILayout.Height(30))) NextAnimation();
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Keyboard shortcuts
        GUILayout.Label($"Keys: {toggleUIKey}=Toggle UI | {nextAnimKey}=Next | {prevAnimKey}=Prev | {playPauseKey}=Play/Pause", GUI.skin.box);

        GUILayout.Space(10);

        // Animation list
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < filteredAnimations.Count; i++)
        {
            var clip = filteredAnimations[i];
            bool isSelected = i == currentIndex;
            string displayName = clip.name.EndsWith("_anim") ? clip.name.Substring(0, clip.name.Length - 5) : clip.name;

            if (GUILayout.Button(displayName, isSelected ? selectedButtonStyle : buttonStyle, GUILayout.Height(25)))
            {
                PlayAnimation(i);
            }
        }

        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }
}
