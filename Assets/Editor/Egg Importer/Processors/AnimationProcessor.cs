using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using POTCO.Editor;

public class AnimationProcessor
{
    private ParserUtilities _parserUtils;

    // Cache commonly used separators to avoid repeated allocations
    private static readonly char[] SpaceSeparator = { ' ' };
    private static readonly char[] WhitespaceSeparators = { ' ', '\n', '\r', '\t' };
    private static readonly char[] SpaceTabSeparators = { ' ', '\t' };

    // Reusable StringBuilder for string concatenation
    private static readonly StringBuilder StringBuilderCache = new StringBuilder();

    // Body shape bones that shouldn't be animated (controlled by BodyShapeApplier)
    private static readonly HashSet<string> BodyShapeBones = new HashSet<string>
    {
        "def_spine02", "def_spine03", "def_spine04", "def_shoulders", "def_neck",
        "def_left_clav", "def_left_shoulder", "def_left_elbow", "def_left_wrist",
        "def_left_thumb01", "def_left_thumb02", "def_left_thumb03",
        "def_left_finger01", "def_left_finger02", "def_left_index01", "def_left_index02",
        "def_right_clav", "def_right_shoulder", "def_right_elbow", "def_right_wrist",
        "def_right_thumb01", "def_right_thumb02", "def_right_thumb03",
        "def_right_finger01", "def_right_finger02", "def_right_index01", "def_right_index02",
        "tr_left_clav", "tr_left_thumb01", "tr_right_clav", "tr_right_thumb01",
        "def_hips", "def_left_thigh", "def_left_knee", "def_left_ankle", "def_left_ball",
        "def_right_thigh", "def_right_knee", "def_right_ankle", "def_right_ball",
        "tr_sash01", "tr_left_thigh", "tr_right_thigh"
    };
    
    public AnimationProcessor()
    {
        _parserUtils = new ParserUtilities();
    }

    public void ParseAnimations(string[] lines, GameObject rootGO, AssetImportContext ctx, GameObject rootBoneObject)
    {
        DebugLogger.LogEggImporter("🎬 ANIMATION: Starting animation parsing");

        int bundleCount = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.StartsWith("<Bundle>"))
            {
                bundleCount++;
                var parts = line.Split(SpaceSeparator, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    string bundleName = parts[1];
                    DebugLogger.LogEggImporter($"🎬 ANIMATION: Found animation bundle #{bundleCount}: '{bundleName}'");

                    var clip = new AnimationClip { name = bundleName + "_anim" };
                    DebugLogger.LogEggImporter($"🎬 ANIMATION: Created clip: '{clip.name}'");

                    // For animation files, bones are created under Armature, but we want direct bone names for paths
                    string armaturePath = "";
                    DebugLogger.LogEggImporter($"🎬 ANIMATION: Armature path: '{armaturePath}' (bones will be accessed directly)");

                    int bundleEnd = _parserUtils.FindMatchingBrace(lines, i);
                    if (bundleEnd != -1)
                    {
                        DebugLogger.LogEggImporter($"🎬 ANIMATION: Bundle spans lines {i} to {bundleEnd}");

                        ParseAnimationBundle(lines, i + 1, bundleEnd, clip, armaturePath);

                        clip.wrapMode = WrapMode.Loop;
                        clip.legacy = false;

                        DebugLogger.LogEggImporter($"🎬 ANIMATION: Configured clip - WrapMode: {clip.wrapMode}, Legacy: {clip.legacy}");

                        var curveBindings = AnimationUtility.GetCurveBindings(clip);
                        DebugLogger.LogEggImporter($"🎬 ANIMATION: Clip has {curveBindings.Length} curve bindings");

                        if (curveBindings.Length > 0)
                        {
                            foreach (var binding in curveBindings)
                            {
                                DebugLogger.LogEggImporter($"🎬 ANIMATION: Curve - Path: '{binding.path}', Property: '{binding.propertyName}', Type: {binding.type}");
                            }

                            DebugLogger.LogEggImporter($"🎬 ANIMATION: Adding clip '{clip.name}' to asset context");
                            ctx.AddObjectToAsset(clip.name, clip);

                            DebugLogger.LogEggImporter($"🎬 ANIMATION: Starting controller creation...");
                            CreateAnimatorControllerForClip(clip, rootGO, ctx);

                            DebugLogger.LogEggImporter($"🎮 ANIMATION: SUCCESS! Animation '{clip.name}' is ready to use!");
                        }
                        else
                        {
                            DebugLogger.LogWarningEggImporter($"❌ ANIMATION: Animation clip '{clip.name}' has no curves - skipping");

                            DebugLogger.LogEggImporter("🔍 ANIMATION: Debugging clip creation...");
                            if (clip == null)
                            {
                                DebugLogger.LogErrorEggImporter("🔍 ANIMATION: Clip is null!");
                            }
                            else
                            {
                                DebugLogger.LogEggImporter($"🔍 ANIMATION: Clip exists: {clip.name}");
                                DebugLogger.LogEggImporter($"🔍 ANIMATION: Clip length: {clip.length}");
                                DebugLogger.LogEggImporter($"🔍 ANIMATION: Clip frameRate: {clip.frameRate}");
                            }
                        }

                        i = bundleEnd;
                    }
                    else
                    {
                        DebugLogger.LogErrorEggImporter($"❌ ANIMATION: Could not find matching brace for bundle at line {i}");
                        i++;
                    }
                }
                else
                {
                    DebugLogger.LogWarningEggImporter($"⚠️ ANIMATION: Bundle line malformed: '{line}'");
                    i++;
                }
            }
            else
            {
                i++;
            }
        }

        DebugLogger.LogEggImporter($"🎬 ANIMATION: Completed. Found {bundleCount} bundles total");
        }

    public void ParseStandaloneAnimationBundle(string[] lines, int start, int end, AnimationClip clip)
    {
        DebugLogger.LogEggImporter($"🎬 STANDALONE: Parsing standalone animation bundle from line {start} to {end}");

        int i = start;
        while (i < end)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                i++;
                continue;
            }

            if (line.StartsWith("<Table>"))
            {
                var parts = line.Split(SpaceSeparator, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    string tableName = parts[1].Trim('"');
                    DebugLogger.LogEggImporter($"🎬 STANDALONE: Processing table '{tableName}'");

                    if (tableName == "<skeleton>")
                    {
                        int tableEnd = _parserUtils.FindMatchingBrace(lines, i);
                        if (tableEnd != -1)
                        {
                            ParseStandaloneAnimationBundle(lines, i + 1, tableEnd, clip);
                            i = tableEnd + 1;
                        }
                        else
                        {
                            i++;
                        }
                    }
                    else
                    {
                        // This is a bone table - parse its animations with proper bone path
                        string bonePath = tableName;
                        DebugLogger.LogEggImporter($"🎬 STANDALONE: Processing bone '{bonePath}'");

                        int tableEnd = _parserUtils.FindMatchingBrace(lines, i);
                        if (tableEnd != -1)
                        {
                            ParseStandaloneBoneAnimationTable(lines, i + 1, tableEnd, clip, bonePath);
                            i = tableEnd + 1;
                        }
                        else
                        {
                            i++;
                        }
                    }
                }
                else
                {
                    i++;
                }
            }
            else
            {
                i++;
            }
        }

        DebugLogger.LogEggImporter($"🎬 STANDALONE: Completed parsing standalone animation bundle");
    }

    private void ParseStandaloneBoneAnimationTable(string[] lines, int start, int end, AnimationClip clip, string bonePath)
    {
        DebugLogger.LogEggImporter($"🦴 STANDALONE: Parsing bone '{bonePath}' from line {start} to {end}");

        int i = start;
        while (i < end)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                i++;
                continue;
            }

            if (line.StartsWith("<Table>"))
            {
                var parts = line.Split(SpaceSeparator, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    string tableName = parts[1].Trim('"');
                    string childPath = bonePath + "/" + tableName;
                    DebugLogger.LogEggImporter($"🦴 STANDALONE: Found child bone '{childPath}'");

                    int tableEnd = _parserUtils.FindMatchingBrace(lines, i);
                    if (tableEnd != -1)
                    {
                        ParseStandaloneBoneAnimationTable(lines, i + 1, tableEnd, clip, childPath);
                        i = tableEnd + 1;
                    }
                    else
                    {
                        i++;
                    }
                }
                else
                {
                    i++;
                }
            }
            else if (line.StartsWith("<Xfm$Anim_S$>"))
            {
                DebugLogger.LogEggImporter($"🦴 STANDALONE: Found transform animation for bone: {bonePath}");
                int xfmEnd = _parserUtils.FindMatchingBrace(lines, i);
                if (xfmEnd != -1)
                {
                    ParseXfmAnim(lines, i + 1, xfmEnd, clip, bonePath);
                    i = xfmEnd + 1;
                }
                else
                {
                    i++;
                }
            }
            else
            {
                i++;
            }
        }

        DebugLogger.LogEggImporter($"🦴 STANDALONE: Completed parsing bone '{bonePath}'");
    }

    public void ParseBundleBonesAndAnimations(string[] lines, int start, int end, EggJoint parentJoint, string currentPath, AnimationClip clip, Dictionary<string, EggJoint> joints)
    {
        DebugLogger.LogEggImporter($"🔍 BUNDLE BONES: Parsing from line {start} to {end}, path: '{currentPath}'");

        int i = start;
        while (i < end)
        {
            string line = lines[i].Trim();
            
            if (line.StartsWith("<Joint>"))
            {
                var parts = line.Split(SpaceSeparator, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    string jointName = parts[1];
                    string jointPath = string.IsNullOrEmpty(currentPath) ? jointName : currentPath + "/" + jointName;
                    
                    DebugLogger.LogEggImporter($"🦴 BUNDLE: Found joint '{jointName}' at path '{jointPath}'");

                    EggJoint joint = new EggJoint
                    {
                        name = jointName,
                        parent = parentJoint,
                        transform = Matrix4x4.identity,
                        defaultPose = Matrix4x4.identity
                    };

                    if (parentJoint != null)
                    {
                        parentJoint.children.Add(joint);
                    }

                    joints[jointName] = joint;

                    int jointEnd = _parserUtils.FindMatchingBrace(lines, i);
                    if (jointEnd != -1)
                    {
                        ParseBoneContentAndAnimation(lines, i + 1, jointEnd, joint, jointPath, clip);
                        ParseBundleBonesAndAnimations(lines, i + 1, jointEnd, joint, jointPath, clip, joints);
                        i = jointEnd + 1;
                    }
                    else
                    {
                        DebugLogger.LogErrorEggImporter($"❌ BUNDLE: Could not find matching brace for joint at line {i}");
                        i++;
                    }
                }
                else
                {
                    i++;
                }
            }
            else if (line.StartsWith("<Xfm$Anim_S$>"))
            {
                int xfmEnd = _parserUtils.FindMatchingBrace(lines, i);
                if (xfmEnd != -1)
                {
                    ParseXfmAnim(lines, i + 1, xfmEnd, clip, currentPath);
                    i = xfmEnd + 1;
                }
                else
                {
                    DebugLogger.LogErrorEggImporter($"❌ BUNDLE: Could not find matching brace for Xfm$Anim_S$ at line {i}");
                    i++;
                }
            }
            else
            {
                i++;
            }
        }
        }

    private void ParseAnimationBundle(string[] lines, int start, int end, AnimationClip clip, string currentPath)
    {
        DebugLogger.LogEggImporter($"📦 BUNDLE: Parsing bundle from line {start} to {end}, currentPath: '{currentPath}'");

        int i = start;
        int tableCount = 0;

        while (i < end)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                i++;
                continue;
            }

            if (line.StartsWith("<Table>"))
            {
                tableCount++;
                var parts = line.Split(SpaceSeparator, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    string tableName = parts[1].Trim('"');
                    DebugLogger.LogEggImporter($"📦 BUNDLE: Found table #{tableCount}: '{tableName}'");

                    if (tableName == "<skeleton>")
                    {
                        DebugLogger.LogEggImporter($"📦 BUNDLE: Entering skeleton table");
                        int tableEnd = _parserUtils.FindMatchingBrace(lines, i);
                        if (tableEnd != -1)
                        {
                            ParseAnimationBundle(lines, i + 1, tableEnd, clip, currentPath);
                            i = tableEnd + 1;
                        }
                        else
                        {
                            DebugLogger.LogErrorEggImporter($"❌ BUNDLE: Could not find matching brace for skeleton table at line {i}");
                            i++;
                        }
                    }
                    else
                    {
                        string bonePath = string.IsNullOrEmpty(currentPath) ? tableName : currentPath + "/" + tableName;
                        DebugLogger.LogEggImporter($"📦 BUNDLE: Processing bone table '{tableName}' with path '{bonePath}'");

                        int tableEnd = _parserUtils.FindMatchingBrace(lines, i);
                        if (tableEnd != -1)
                        {
                            ParseBoneAnimationTable(lines, i + 1, tableEnd, clip, bonePath);
                            i = tableEnd + 1;
                        }
                        else
                        {
                            DebugLogger.LogErrorEggImporter($"❌ BUNDLE: Could not find matching brace for bone table '{tableName}' at line {i}");
                            i++;
                        }
                    }
                }
                else
                {
                    DebugLogger.LogWarningEggImporter($"⚠️ BUNDLE: Table line malformed: '{line}'");
                    i++;
                }
            }
            else
            {
                i++;
            }
        }

        DebugLogger.LogEggImporter($"📦 BUNDLE: Completed. Processed {tableCount} tables");
        }

    private void ParseBoneAnimationTable(string[] lines, int start, int end, AnimationClip clip, string bonePath)
    {
        DebugLogger.LogEggImporter($"🦴 BONE: Parsing bone '{bonePath}' from line {start} to {end}");

        int i = start;
        int xfmCount = 0;
        int childTableCount = 0;

        while (i < end)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                i++;
                continue;
            }

            if (line.StartsWith("<Table>"))
            {
                childTableCount++;
                var parts = line.Split(SpaceSeparator, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    string tableName = parts[1].Trim('"');
                    string childPath = bonePath + "/" + tableName;
                    DebugLogger.LogEggImporter($"🦴 BONE: Found child table #{childTableCount}: '{tableName}' -> '{childPath}'");

                    int tableEnd = _parserUtils.FindMatchingBrace(lines, i);
                    if (tableEnd != -1)
                    {
                        ParseBoneAnimationTable(lines, i + 1, tableEnd, clip, childPath);
                        i = tableEnd + 1;
                    }
                    else
                    {
                        DebugLogger.LogErrorEggImporter($"❌ BONE: Could not find matching brace for child table '{tableName}' at line {i}");
                        i++;
                    }
                }
                else
                {
                    DebugLogger.LogWarningEggImporter($"⚠️ BONE: Child table line malformed: '{line}'");
                    i++;
                }
            }
            else if (line.StartsWith("<Xfm$Anim_S$>"))
            {
                xfmCount++;
                DebugLogger.LogEggImporter($"🦴 BONE: Found transform animation #{xfmCount} for bone: {bonePath}");
                int xfmEnd = _parserUtils.FindMatchingBrace(lines, i);
                if (xfmEnd != -1)
                {
                    ParseXfmAnim(lines, i + 1, xfmEnd, clip, bonePath);
                    i = xfmEnd + 1;
                }
                else
                {
                    DebugLogger.LogErrorEggImporter($"❌ BONE: Could not find matching brace for Xfm$Anim_S$ at line {i}");
                    i++;
                }
            }
            else
            {
                i++;
            }
        }

        DebugLogger.LogEggImporter($"🦴 BONE: Completed '{bonePath}'. Found {xfmCount} transforms, {childTableCount} child tables");
        }

    private void ParseBoneContentAndAnimation(string[] lines, int start, int end, EggJoint joint, string bonePath, AnimationClip clip)
    {
        DebugLogger.LogEggImporter($"🔍 BONE CONTENT: Parsing bone '{joint.name}' from line {start} to {end}");

        for (int i = start; i < end; i++)
        {
            string line = lines[i].Trim();

            if (line.StartsWith("<Transform>"))
            {
                DebugLogger.LogEggImporter($"🔍 BONE CONTENT: Found transform for joint '{joint.name}'");
                int transEnd = _parserUtils.FindMatchingBrace(lines, i);
                if (transEnd != -1)
                {
                    _parserUtils.ParseTransformMatrix(lines, i + 1, transEnd, ref joint.transform);
                    joint.defaultPose = joint.transform;
                    DebugLogger.LogEggImporter($"🔍 BONE CONTENT: Set transform for joint '{joint.name}'");
                }
            }
            else if (line.StartsWith("<Xfm$Anim_S$>"))
            {
                DebugLogger.LogEggImporter($"🔍 BONE CONTENT: Found animation data for joint '{joint.name}'");
                int xfmEnd = _parserUtils.FindMatchingBrace(lines, i);
                if (xfmEnd != -1)
                {
                    ParseXfmAnim(lines, i + 1, xfmEnd, clip, bonePath);
                    i = xfmEnd;
                }
            }
        }
        }

    private void ParseXfmAnim(string[] lines, int start, int end, AnimationClip clip, string bonePath)
    {
        DebugLogger.LogEggImporter($"🔄 TRANSFORM: Parsing transform animation for '{bonePath}' from line {start} to {end}");

        float fps = 24f;
        // Pre-size channels dictionary based on typical animation channels (x,y,z,h,p,r,i,j,k)
        var channels = new Dictionary<string, List<float>>(9);
        int numKeyframes = 0;

        int i = start;
        while (i < end)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                i++;
                continue;
            }

            if (line.StartsWith("<Scalar> fps"))
            {
                int openBrace = line.IndexOf('{');
                int closeBrace = line.LastIndexOf('}');
                if (openBrace != -1 && closeBrace != -1)
                {
                    string val = line.Substring(openBrace + 1, closeBrace - openBrace - 1).Trim();
                    if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedFps))
                    {
                        fps = parsedFps;
                        DebugLogger.LogEggImporter($"🔄 TRANSFORM: Set FPS to {fps}");
                    }
                }
                i++;
            }
            else if (line.StartsWith("<S$Anim>"))
            {
                var headerParts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string channelName = headerParts.Length > 1 ? headerParts[1] : "UNKNOWN";
                DebugLogger.LogEggImporter($"🔄 TRANSFORM: Processing channel '{channelName}'");

                if (line.Contains("<V>") && line.Contains("}"))
                {
                    DebugLogger.LogEggImporter($"🔄 TRANSFORM: Single-line format detected for channel '{channelName}'");

                    int vStart = line.IndexOf("<V>");
                    if (vStart != -1)
                    {
                        string vPart = line.Substring(vStart);
                        int firstBrace = vPart.IndexOf('{');
                        int lastBrace = vPart.LastIndexOf('}');

                        if (firstBrace != -1 && lastBrace != -1 && lastBrace > firstBrace)
                        {
                            string valuesString = vPart.Substring(firstBrace + 1, lastBrace - firstBrace - 1).Trim();
                            DebugLogger.LogEggImporter($"🔄 TRANSFORM: Extracted single-line values: '{valuesString}'");

                            if (!string.IsNullOrWhiteSpace(valuesString))
                            {
                                var stringValues = valuesString.Split(SpaceTabSeparators, StringSplitOptions.RemoveEmptyEntries);
                                // Pre-size values list based on typical keyframe counts (estimate 60-120 frames)
                                var values = new List<float>(stringValues.Length);

                                foreach (string sv in stringValues)
                                {
                                    if (float.TryParse(sv, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                                    {
                                        values.Add(val);
                                    }
                                }

                                if (values.Count > 0)
                                {
                                    channels[channelName] = values;
                                    if (values.Count > numKeyframes)
                                        numKeyframes = values.Count;
                                    DebugLogger.LogEggImporter($"✅ TRANSFORM: Parsed {values.Count} keyframes for single-line channel '{channelName}'");
                                }
                            }
                        }
                    }
                    i++;
                }
                else
                {
                    DebugLogger.LogEggImporter($"🔄 TRANSFORM: Multi-line format for channel '{channelName}'");

                    int sAnimEnd = _parserUtils.FindMatchingBrace(lines, i);
                    if (sAnimEnd == -1)
                    {
                        DebugLogger.LogErrorEggImporter($"❌ TRANSFORM: Could not find matching brace for S$Anim at line {i}");
                        i++;
                        continue;
                    }

                    bool foundVBlock = false;
                    for (int j = i + 1; j <= sAnimEnd; j++)
                    {
                        string vLine = lines[j].Trim();
                        if (vLine.StartsWith("<V>"))
                        {
                            foundVBlock = true;
                            DebugLogger.LogEggImporter($"🔄 TRANSFORM: Found <V> block at line {j}");

                            int vEnd = _parserUtils.FindMatchingBrace(lines, j);
                            if (vEnd != -1)
                            {
                                DebugLogger.LogEggImporter($"🔄 TRANSFORM: <V> block spans lines {j} to {vEnd}");

                                // Use StringBuilder for efficient string concatenation
                                StringBuilderCache.Clear();
                                for (int k = j; k <= vEnd; k++)
                                {
                                    StringBuilderCache.Append(lines[k]).Append(' ');
                                }
                                string fullVBlock = StringBuilderCache.ToString();

                                int lastOpenBrace = fullVBlock.LastIndexOf('{');
                                int firstCloseBraceAfter = fullVBlock.IndexOf('}', lastOpenBrace);

                                if (lastOpenBrace != -1 && firstCloseBraceAfter != -1)
                                {
                                    string valuesString = fullVBlock.Substring(lastOpenBrace + 1, firstCloseBraceAfter - lastOpenBrace - 1).Trim();
                                    DebugLogger.LogEggImporter($"🔄 TRANSFORM: Extracted multi-line values (length {valuesString.Length})");

                                    if (!string.IsNullOrWhiteSpace(valuesString))
                                    {
                                        var stringValues = valuesString.Split(WhitespaceSeparators, StringSplitOptions.RemoveEmptyEntries);
                                        // Pre-size values list based on parsed string count
                                        var values = new List<float>(stringValues.Length);

                                        DebugLogger.LogEggImporter($"🔄 TRANSFORM: Found {stringValues.Length} string values to parse");

                                        foreach (string sv in stringValues)
                                        {
                                            if (float.TryParse(sv, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                                            {
                                                values.Add(val);
                                            }
                                        }

                                        if (values.Count > 0)
                                        {
                                            channels[channelName] = values;
                                            if (values.Count > numKeyframes)
                                                numKeyframes = values.Count;
                                            DebugLogger.LogEggImporter($"✅ TRANSFORM: Parsed {values.Count} keyframes for multi-line channel '{channelName}'");
                                        }
                                    }
                                }
                            }
                            break;
                        }
                    }

                    if (!foundVBlock)
                    {
                        DebugLogger.LogWarningEggImporter($"⚠️ TRANSFORM: No <V> block found for channel '{channelName}'");
                    }

                    i = sAnimEnd + 1;
                }
            }
            else
            {
                i++;
            }
        }

        DebugLogger.LogEggImporter($"🔄 TRANSFORM: Finished parsing. Found {channels.Count} channels, {numKeyframes} max keyframes");
        foreach (var channel in channels)
        {
            DebugLogger.LogEggImporter($"🔄 TRANSFORM: Channel '{channel.Key}' has {channel.Value.Count} values");
        }

        if (channels.Count > 0 && numKeyframes > 0)
        {
            DebugLogger.LogEggImporter($"🔄 TRANSFORM: Creating animation curves for bone '{bonePath}'");
            CreateAnimationCurvesForBone(clip, bonePath, channels, numKeyframes, fps);
        }
        else
        {
            DebugLogger.LogWarningEggImporter($"⚠️ TRANSFORM: No valid animation data found for bone '{bonePath}'");
        }
        }

    private void CreateAnimatorControllerForClip(AnimationClip clip, GameObject rootGO, AssetImportContext ctx)
    {
        DebugLogger.LogEggImporter($"🎯 LEGACY: Setting up legacy animation for clip '{clip.name}'");

        try
        {
            clip.legacy = true;
            clip.wrapMode = WrapMode.Loop;

            DebugLogger.LogEggImporter($"🎯 LEGACY: Configured clip as legacy with loop wrap mode");

            var animator = rootGO.GetComponent<Animator>();
            if (animator != null)
            {
                UnityEngine.Object.DestroyImmediate(animator);
                DebugLogger.LogEggImporter($"🎯 LEGACY: Removed Animator component");
            }

            var animationComponent = rootGO.GetComponent<Animation>();
            if (animationComponent == null)
            {
                animationComponent = rootGO.AddComponent<Animation>();
                DebugLogger.LogEggImporter($"🎯 LEGACY: Added Animation component");
            }

            animationComponent.AddClip(clip, clip.name);
            animationComponent.clip = clip;
            animationComponent.playAutomatically = true;

            DebugLogger.LogEggImporter($"✅ LEGACY: Complete! Legacy animation '{clip.name}' ready to play automatically");
            DebugLogger.LogEggImporter($"🎮 READY: Just drag '{rootGO.name}' into your scene and it will animate immediately!");
        }
        catch (System.Exception e)
        {
            DebugLogger.LogErrorEggImporter($"❌ LEGACY: Exception during setup: {e.Message}\nStack: {e.StackTrace}");
        }
        }

    private void CreateAnimationCurvesForBone(AnimationClip clip, string bonePath, Dictionary<string, List<float>> channels, int numKeyframes, float fps)
    {
        // Skip body shape bones - these are controlled by BodyShapeApplier and shouldn't be animated
        string boneName = bonePath.Split('/').Last();
        if (BodyShapeBones.Contains(boneName))
        {
            DebugLogger.LogEggImporter($"⏭️ CURVES: Skipping body shape bone '{bonePath}' (controlled by BodyShapeApplier)");
            return;
        }

        // Fix bone path to match actual hierarchy created by GeometryProcessor
        string correctedBonePath = CorrectBonePath(bonePath);
        DebugLogger.LogEggImporter($"📈 CURVES: Creating curves for bone '{bonePath}' -> corrected to '{correctedBonePath}' with {numKeyframes} keyframes at {fps} fps");

        // Pre-size keyframe lists to avoid resizing during population
        var posXKeys = new List<Keyframe>(numKeyframes);
        var posYKeys = new List<Keyframe>(numKeyframes);
        var posZKeys = new List<Keyframe>(numKeyframes);
        var rotXKeys = new List<Keyframe>(numKeyframes);
        var rotYKeys = new List<Keyframe>(numKeyframes);
        var rotZKeys = new List<Keyframe>(numKeyframes);
        var rotWKeys = new List<Keyframe>(numKeyframes);
        var scaleXKeys = new List<Keyframe>(numKeyframes);
        var scaleYKeys = new List<Keyframe>(numKeyframes);
        var scaleZKeys = new List<Keyframe>(numKeyframes);

        // Cache channel lookups to avoid repeated dictionary access
        var xChannel = channels.TryGetValue("x", out var xList) ? xList : null;
        var yChannel = channels.TryGetValue("y", out var yList) ? yList : null;
        var zChannel = channels.TryGetValue("z", out var zList) ? zList : null;
        var hChannel = channels.TryGetValue("h", out var hList) ? hList : null;
        var pChannel = channels.TryGetValue("p", out var pList) ? pList : null;
        var rChannel = channels.TryGetValue("r", out var rList) ? rList : null;
        var iChannel = channels.TryGetValue("i", out var iList) ? iList : null;
        var jChannel = channels.TryGetValue("j", out var jList) ? jList : null;
        var kChannel = channels.TryGetValue("k", out var kList) ? kList : null;
        
        for (int k = 0; k < numKeyframes; k++)
        {
            float time = k / fps;

            float p_x = GetChannelValueDirect(xChannel, k);
            float p_y = GetChannelValueDirect(yChannel, k);
            float p_z = GetChannelValueDirect(zChannel, k);
            float p_h = GetChannelValueDirect(hChannel, k);
            float p_p = GetChannelValueDirect(pChannel, k);
            float p_r = GetChannelValueDirect(rChannel, k);
            float s_i = GetChannelValueDirect(iChannel, k, 1.0f);
            float s_j = GetChannelValueDirect(jChannel, k, 1.0f);
            float s_k = GetChannelValueDirect(kChannel, k, 1.0f);

            posXKeys.Add(new Keyframe(time, p_x));
            posYKeys.Add(new Keyframe(time, p_z));
            posZKeys.Add(new Keyframe(time, p_y));

            Quaternion pandaQuat = Quaternion.AngleAxis(p_h, Vector3.forward) *
                                   Quaternion.AngleAxis(p_p, Vector3.right) *
                                   Quaternion.AngleAxis(p_r, Vector3.up);
            Quaternion unityQuat = new Quaternion(pandaQuat.x, pandaQuat.z, pandaQuat.y, -pandaQuat.w);

            rotXKeys.Add(new Keyframe(time, unityQuat.x));
            rotYKeys.Add(new Keyframe(time, unityQuat.y));
            rotZKeys.Add(new Keyframe(time, unityQuat.z));
            rotWKeys.Add(new Keyframe(time, unityQuat.w));

            scaleXKeys.Add(new Keyframe(time, s_i));
            scaleYKeys.Add(new Keyframe(time, s_k));
            scaleZKeys.Add(new Keyframe(time, s_j));
        }

        int curvesAdded = 0;

        // Use cached channel references for faster checks
        if (xChannel != null || yChannel != null || zChannel != null)
        {
            clip.SetCurve(correctedBonePath, typeof(Transform), "m_LocalPosition.x", new AnimationCurve(posXKeys.ToArray()));
            clip.SetCurve(correctedBonePath, typeof(Transform), "m_LocalPosition.y", new AnimationCurve(posYKeys.ToArray()));
            clip.SetCurve(correctedBonePath, typeof(Transform), "m_LocalPosition.z", new AnimationCurve(posZKeys.ToArray()));
            curvesAdded += 3;
            DebugLogger.LogEggImporter($"📈 CURVES: Set position curves for {correctedBonePath}");
        }

        if (hChannel != null || pChannel != null || rChannel != null)
        {
            clip.SetCurve(correctedBonePath, typeof(Transform), "m_LocalRotation.x", new AnimationCurve(rotXKeys.ToArray()));
            clip.SetCurve(correctedBonePath, typeof(Transform), "m_LocalRotation.y", new AnimationCurve(rotYKeys.ToArray()));
            clip.SetCurve(correctedBonePath, typeof(Transform), "m_LocalRotation.z", new AnimationCurve(rotZKeys.ToArray()));
            clip.SetCurve(correctedBonePath, typeof(Transform), "m_LocalRotation.w", new AnimationCurve(rotWKeys.ToArray()));
            curvesAdded += 4;
            DebugLogger.LogEggImporter($"📈 CURVES: Set rotation curves for {correctedBonePath}");
        }

        if (iChannel != null || jChannel != null || kChannel != null)
        {
            clip.SetCurve(correctedBonePath, typeof(Transform), "m_LocalScale.x", new AnimationCurve(scaleXKeys.ToArray()));
            clip.SetCurve(correctedBonePath, typeof(Transform), "m_LocalScale.y", new AnimationCurve(scaleYKeys.ToArray()));
            clip.SetCurve(correctedBonePath, typeof(Transform), "m_LocalScale.z", new AnimationCurve(scaleZKeys.ToArray()));
            curvesAdded += 3;
            DebugLogger.LogEggImporter($"📈 CURVES: Set scale curves for {correctedBonePath}");
        }

        DebugLogger.LogEggImporter($"📈 CURVES: Added {curvesAdded} curves for '{correctedBonePath}' with orientation fix");
        }

    private string CorrectBonePath(string bonePath)
    {
        // For standalone animations, ensure they target the Armature/ hierarchy
        // that is created when importing models with skeletal data
        
        // Remove any leading slash first
        bonePath = bonePath.TrimStart('/');
        
        // If the path doesn't already include Armature/, prepend it
        if (!bonePath.StartsWith("Armature/") && !string.IsNullOrEmpty(bonePath))
        {
            return "Armature/" + bonePath;
        }
        
        return bonePath;
    }

    private float GetChannelValue(Dictionary<string, List<float>> channels, string channelName, int keyframeIndex, float defaultValue = 0f)
    {
        if (!channels.ContainsKey(channelName) || channels[channelName].Count == 0)
            return defaultValue;

        var channel = channels[channelName];
        int index = Mathf.Min(keyframeIndex, channel.Count - 1);
        return channel[index];
    }
    
    // Optimized version that works directly with cached channel lists
    private float GetChannelValueDirect(List<float> channel, int keyframeIndex, float defaultValue = 0f)
    {
        if (channel == null || channel.Count == 0)
            return defaultValue;

        int index = Mathf.Min(keyframeIndex, channel.Count - 1);
        return channel[index];
    }
}