using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using POTCO.Editor;
using System.Linq;

public class MaterialHandler
{
    // Cache frequently used shaders to avoid repeated Shader.Find calls
    private static Shader _vertexColorShader;
    private static Shader _vertexColorTransparentShader;
    private static Shader _legacyDiffuseShader;
    private static Shader _standardShader;
    
    // Cache common property IDs for better performance
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int MetallicPropertyId = Shader.PropertyToID("_Metallic");
    private static readonly int GlossinessPropertyId = Shader.PropertyToID("_Glossiness");
    
    // Cache for default colors to avoid repeated string operations
    private static readonly Dictionary<string, Color> DefaultColorCache = new Dictionary<string, Color>();
    
    // Static texture cache to avoid expensive AssetDatabase calls
    private static Dictionary<string, Texture2D> _textureCache;
    private static bool _textureCacheInitialized = false;
    private static readonly string[] KnownPhasePrefixes =
    {
        "phase_2/",
        "phase_3/",
        "phase_3.5/",
        "phase_4/",
        "phase_5/",
        "phase_5.5/",
        "phase_6/",
        "phase_7/",
        "phase_8/",
        "phase_9/",
        "phase_10/",
        "phase_11/",
        "phase_12/",
        "phase_13/",
        "phase_14/"
    };
    
    // New overload that accepts alpha textures and wrap modes specified by .egg files
    public List<Material> CreateMaterials(Dictionary<string, string> texturePaths, Dictionary<string, string> alphaPaths, Dictionary<string, TextureWrapData> textureWrapModes, GameObject rootGO, string assetPath = "")
    {
        DebugLogger.LogEggImporter($"Creating materials from {texturePaths.Count} texture paths and {alphaPaths.Count} alpha paths");
        var materials = new List<Material>(texturePaths.Count + 1);
        var createdMaterialNames = new HashSet<string>(); // Track what we've created

        // Check if we should use ParticleGUI shader based on asset path
        bool useParticleGUI = false;
        if (!string.IsNullOrEmpty(assetPath))
        {
            string lowerPath = assetPath.ToLowerInvariant();
            if (lowerPath.Contains("/effects/") || 
                lowerPath.Contains("/fonts/") || 
                lowerPath.Contains("/gui/") || 
                lowerPath.Contains("/texturecards/") ||
                lowerPath.Contains("/sky/"))
            {
                useParticleGUI = true;
                DebugLogger.LogEggImporter($"[Shader] Using ParticleGUI shader for asset: {assetPath}");
            }
        }

        foreach (var kvp in texturePaths)
        {
            string materialName = kvp.Key;
            string texturePath = kvp.Value;

            // Check for alpha blend marker
            bool needsAlphaBlend = materialName.EndsWith("_ALPHABLEND");
            string cleanMatName = needsAlphaBlend ? materialName.Substring(0, materialName.Length - 11) : materialName;

            DebugLogger.LogEggImporter($"Creating material: {cleanMatName} with texture: {texturePath}");

            // Check if this material has an alpha texture specified (use clean name)
            string alphaPath = alphaPaths.TryGetValue(cleanMatName, out string alpha) ? alpha : null;

            // Get wrap mode for this texture
            TextureWrapData wrapData = textureWrapModes.TryGetValue(cleanMatName, out TextureWrapData wrap) ? wrap : new TextureWrapData();

            Material mat;
            Texture2D colorTex = FindTextureInProject(texturePath);

            // Ensure texture is set to repeat mode (we control wrap per-material)
            if (colorTex)
            {
                ApplyWrapModeToTexture(colorTex, wrapData);
            }

            Texture2D alphaTex = null;
            if (!string.IsNullOrEmpty(alphaPath))
            {
                alphaTex = FindTextureInProject(alphaPath);

                if (alphaTex)
                    DebugLogger.LogEggImporter($"[AlphaMask] Found alpha texture for {materialName}: {alphaPath}");
                else
                    DebugLogger.LogWarningEggImporter($"[AlphaMask] Could not load alpha texture: {alphaPath}");
            }

            // Create material with appropriate shader
            if (useParticleGUI)
            {
                Shader guiShader = Shader.Find("EggImporter/ParticleGUI");
                if (guiShader == null)
                {
                    DebugLogger.LogWarningEggImporter("Shader 'EggImporter/ParticleGUI' not found! Falling back to standard.");
                    guiShader = GetCachedVertexColorShader();
                }
                
                mat = new Material(guiShader);
                mat.name = materialName;
                mat.enableInstancing = true;
                
                // Ensure tint is white by default for GUI
                if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", Color.white);
            }
            else
            {
                mat = CreateVertexColorMaterial(materialName, needsAlphaBlend);
            }

            if (colorTex)
            {
                mat.mainTexture = colorTex;
                DebugLogger.LogEggImporter($"Assigned texture {texturePath} to material {cleanMatName}");
            }
            else
            {
                DebugLogger.LogWarningEggImporter($"Could not find texture: {texturePath}");
                mat.color = GetDefaultColorForMaterial(cleanMatName);
            }

            // Set alpha texture if available
            if (alphaTex)
            {
                mat.SetTexture("_AlphaTex", alphaTex);
                if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.1f);
                // Set culling to off so both sides are visible for alpha-masked materials
                mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                DebugLogger.LogEggImporter($"[AlphaMask] Assigned alpha texture to {cleanMatName}: {alphaTex.name} (Cull: Off)");
            }
            else
            {
                // For ParticleGUI, default cull is Off (0), but for standard meshes default is Back (2)
                // Only set Back culling if NOT using ParticleGUI (as GUI often needs double-sided or explicit control)
                // Actually, particles usually need Cull Off.
                if (!useParticleGUI)
                {
                    mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back);
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                    DebugLogger.LogEggImporter($"[Standard] Material {cleanMatName} set to back-face culling (Cull: Back)");
                }
                else
                {
                    // Ensure ParticleGUI materials render in transparent queue
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
            }

            // Use cached property IDs for better performance (only for standard shaders)
            if (!useParticleGUI)
            {
                if (mat.HasProperty(MetallicPropertyId))
                    mat.SetFloat(MetallicPropertyId, 0.0f);
                if (mat.HasProperty(GlossinessPropertyId))
                    mat.SetFloat(GlossinessPropertyId, 0.1f);
            }

            // Don't apply wrap mode in shader - causes stretching artifacts
            // Let textures use natural UV wrapping (repeat mode)
            if (mat.HasProperty("_MainTexWrap"))
            {
                mat.SetVector("_MainTexWrap", Vector4.zero); // 0 = repeat
            }

            // Only reset color for standard shaders, ParticleGUI might use tint
            if (!useParticleGUI)
            {
                if (mat.HasProperty(ColorPropertyId))
                    mat.SetColor(ColorPropertyId, Color.white);
            }

            materials.Add(mat);
            createdMaterialNames.Add(materialName);
        }

        // Always ensure Default-Material exists
        var defaultMaterial = CreateVertexColorMaterial("Default-Material");
        if (defaultMaterial.HasProperty("_MainTex"))
        {
            defaultMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
        }
        materials.Add(defaultMaterial);
        createdMaterialNames.Add("Default-Material");

        // Always ensure Collision-Material exists (invisible material for collision geometry)
        if (!createdMaterialNames.Contains("Collision-Material"))
        {
            var collisionMaterial = CreateInvisibleCollisionMaterial();
            materials.Add(collisionMaterial);
            createdMaterialNames.Add("Collision-Material");
        }

        if (materials.Count == 1)
        {
            DebugLogger.LogEggImporter("No textures found, using default material only");
        }

        return materials;
    }

    // Legacy overload without wrap modes
    public List<Material> CreateMaterials(Dictionary<string, string> texturePaths, Dictionary<string, string> alphaPaths, GameObject rootGO)
    {
        var textureWrapModes = new Dictionary<string, TextureWrapData>();
        return CreateMaterials(texturePaths, alphaPaths, textureWrapModes, rootGO);
    }

    private void ApplyWrapModeToTexture(Texture2D texture, TextureWrapData wrapData)
    {
        // Set texture to repeat by default - we'll control wrap mode per-material via shader
        texture.wrapMode = TextureWrapMode.Repeat;
    }

    private Vector4 GetWrapModeVector(TextureWrapData wrapData)
    {
        // Convert wrap mode to shader vector: x=wrapU, y=wrapV (0=repeat, 1=clamp)
        float wrapU = wrapData.wrapU == "clamp" ? 1.0f : 0.0f;
        float wrapV = wrapData.wrapV == "clamp" ? 1.0f : 0.0f;
        return new Vector4(wrapU, wrapV, 0, 0);
    }

    public void CreateMultiTextureMaterials(List<Material> materials, List<string> materialNames, Dictionary<string, string> texturePaths, Dictionary<string, string> textureUVNames)
    {
        var textureWrapModes = new Dictionary<string, TextureWrapData>();
        CreateMultiTextureMaterials(materials, materialNames, texturePaths, textureUVNames, textureWrapModes);
    }

    public void CreateMultiTextureMaterials(List<Material> materials, List<string> materialNames, Dictionary<string, string> texturePaths, Dictionary<string, string> textureUVNames, Dictionary<string, TextureWrapData> textureWrapModes)
    {
        DebugLogger.LogEggImporter($"[MultiTex] Processing {materialNames.Count} material names for multi-texture support");

        // Track created materials to avoid duplicates
        var createdMultiTexMaterials = new HashSet<string>();

        foreach (string matName in materialNames)
        {
            // Check for alpha blend marker
            bool needsAlphaBlend = matName.EndsWith("_ALPHABLEND");
            string cleanMatName = needsAlphaBlend ? matName.Substring(0, matName.Length - 11) : matName;

            if (cleanMatName.Contains("||"))
            {
                // Skip if already created
                if (createdMultiTexMaterials.Contains(matName))
                {
                    DebugLogger.LogEggImporter($"[MultiTex] Skipping duplicate material: {matName}");
                    continue;
                }

                // Multi-texture material like "volcano_palette_3cmla_1||sand"
                var texNames = cleanMatName.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries);

                if (texNames.Length >= 2)
                {
                    DebugLogger.LogEggImporter($"[MultiTex] Creating multi-texture material: {matName}");

                    // Detect if we need to swap based on uv-name presence
                    string firstTexName = texNames[0];
                    string secondTexName = texNames[1];

                    bool firstHasUVName = textureUVNames.ContainsKey(firstTexName);
                    bool secondHasUVName = textureUVNames.ContainsKey(secondTexName);

                    // Check wrap modes - palette textures use clamp, tiling textures use repeat
                    bool firstIsClamp = textureWrapModes.ContainsKey(firstTexName) &&
                        (textureWrapModes[firstTexName].wrapU == "clamp" || textureWrapModes[firstTexName].wrapV == "clamp");
                    bool secondIsClamp = textureWrapModes.ContainsKey(secondTexName) &&
                        (textureWrapModes[secondTexName].wrapU == "clamp" || textureWrapModes[secondTexName].wrapV == "clamp");

                    // Shader has: _MainTex→UV0, _BlendTex→UV1
                    // Smart swap logic using wrap modes as hint:
                    // Clamp textures (palettes) typically use named UVs (UV1), repeat textures use primary UVs (UV0)
                    bool needsSwap;
                    if (firstHasUVName && secondHasUVName)
                    {
                        // Both have uv-names: use wrap mode to determine assignment
                        // If first is clamp (palette), it should go to BlendTex/UV1 (swap=true)
                        // If second is clamp (palette), it should go to BlendTex/UV1 (swap=false)
                        needsSwap = firstIsClamp;
                        DebugLogger.LogEggImporter($"[MultiTex] Both have uv-names, using wrap mode: first clamp={firstIsClamp}, second clamp={secondIsClamp}, swap={needsSwap}");
                    }
                    else
                    {
                        // Standard logic: swap if only first needs UV1
                        needsSwap = firstHasUVName && !secondHasUVName;
                    }

                    string firstUVNameValue = firstHasUVName ? textureUVNames[firstTexName] : "none";
                    string secondUVNameValue = secondHasUVName ? textureUVNames[secondTexName] : "none";

                    DebugLogger.LogEggImporter($"[MultiTex] First TRef '{firstTexName}' uv-name={firstUVNameValue}, hasUVName={firstHasUVName}, clamp={firstIsClamp}");
                    DebugLogger.LogEggImporter($"[MultiTex] Second TRef '{secondTexName}' uv-name={secondUVNameValue}, hasUVName={secondHasUVName}, clamp={secondIsClamp}");
                    DebugLogger.LogEggImporter($"[MultiTex] Needs swap: {needsSwap}");

                    // Load textures
                    Texture2D mainTex = null;
                    Texture2D blendTex = null;

                    if (texturePaths.TryGetValue(firstTexName, out string firstPath))
                    {
                        Texture2D firstTex = FindTextureInProject(firstPath);
                        if (firstTex) ApplyWrapModeToTexture(firstTex, new TextureWrapData());

                        if (needsSwap)
                        {
                            blendTex = firstTex;  // First needs UV1, so goes to _BlendTex
                            DebugLogger.LogEggImporter($"[MultiTex] First texture '{firstTexName}' → _BlendTex (UV1)");
                        }
                        else
                        {
                            mainTex = firstTex;   // First needs UV0, so goes to _MainTex
                            DebugLogger.LogEggImporter($"[MultiTex] First texture '{firstTexName}' → _MainTex (UV0)");
                        }
                    }

                    if (texturePaths.TryGetValue(secondTexName, out string secondPath))
                    {
                        Texture2D secondTex = FindTextureInProject(secondPath);
                        if (secondTex) ApplyWrapModeToTexture(secondTex, new TextureWrapData());

                        if (needsSwap)
                        {
                            mainTex = secondTex;  // Second needs UV0, so goes to _MainTex
                            DebugLogger.LogEggImporter($"[MultiTex] Second texture '{secondTexName}' → _MainTex (UV0)");
                        }
                        else
                        {
                            blendTex = secondTex; // Second needs UV1, so goes to _BlendTex
                            DebugLogger.LogEggImporter($"[MultiTex] Second texture '{secondTexName}' → _BlendTex (UV1)");
                        }
                    }

                    // Create material with appropriate shader (transparent if alpha blend needed)
                    Shader shader = needsAlphaBlend ? GetCachedTransparentShader() : GetCachedVertexColorShader();

                    Material mat = new Material(shader) { name = matName };

                    if (mainTex)
                    {
                        mat.SetTexture("_MainTex", mainTex);
                        DebugLogger.LogEggImporter($"[MultiTex] Set _MainTex: {mainTex.name} (samples UV0)");
                    }

                    if (blendTex)
                    {
                        mat.SetTexture("_BlendTex", blendTex);
                        DebugLogger.LogEggImporter($"[MultiTex] Set _BlendTex: {blendTex.name} (samples UV1)");
                    }

                    // Shader uses: _MainTex → UV0, _BlendTex → UV1
                    // We've assigned textures to match their UV requirements
                    mat.SetFloat("_SwapUVChannels", 0.0f);
                    mat.DisableKeyword("SWAP_UV_CHANNELS");
                    
                    // Enable GPU Instancing
                    mat.enableInstancing = true;

                    // For multi-texture materials, let UVs work naturally
                    if (mat.HasProperty("_MainTexWrap"))
                    {
                        mat.SetVector("_MainTexWrap", Vector4.zero); // 0 = repeat for both
                    }

                    if (mat.HasProperty("_BlendTexWrap"))
                    {
                        mat.SetVector("_BlendTexWrap", Vector4.zero); // 0 = repeat for both
                    }

                    DebugLogger.LogEggImporter($"[WrapMode] Multi-texture material using natural UV wrapping");

                    mat.SetColor("_Color", Color.white);
                    materials.Add(mat);
                    createdMultiTexMaterials.Add(matName);
                }
            }
            else if (needsAlphaBlend && !cleanMatName.Contains("||"))
            {
                // Single-texture material with alpha blend
                // Check if base material exists
                Material baseMat = materials.FirstOrDefault(m => m.name == cleanMatName);
                if (baseMat != null)
                {
                    // Skip if already created
                    if (createdMultiTexMaterials.Contains(matName))
                    {
                        DebugLogger.LogEggImporter($"[AlphaBlend] Skipping duplicate single-tex material: {matName}");
                        continue;
                    }

                    // Create alpha blend version with transparent shader
                    Shader transparentShader = GetCachedTransparentShader();
                    Material alphaMat = new Material(transparentShader) { name = matName };
                    
                    // Enable GPU Instancing
                    alphaMat.enableInstancing = true;

                    // Copy textures and properties from base material
                    if (baseMat.mainTexture) alphaMat.mainTexture = baseMat.mainTexture;
                    if (baseMat.HasProperty("_BlendTex") && alphaMat.HasProperty("_BlendTex"))
                        alphaMat.SetTexture("_BlendTex", baseMat.GetTexture("_BlendTex"));
                    if (baseMat.HasProperty("_AlphaTex") && alphaMat.HasProperty("_AlphaTex"))
                        alphaMat.SetTexture("_AlphaTex", baseMat.GetTexture("_AlphaTex"));
                    if (baseMat.HasProperty("_Color") && alphaMat.HasProperty("_Color"))
                        alphaMat.SetColor("_Color", baseMat.GetColor("_Color"));
                    if (baseMat.HasProperty("_Cull") && alphaMat.HasProperty("_Cull"))
                        alphaMat.SetFloat("_Cull", baseMat.GetFloat("_Cull"));

                    materials.Add(alphaMat);
                    createdMultiTexMaterials.Add(matName);
                    DebugLogger.LogEggImporter($"[AlphaBlend] Created single-tex alpha blend material: {matName}");
                }
                else
                {
                    DebugLogger.LogWarningEggImporter($"[AlphaBlend] Base material '{cleanMatName}' not found for alpha blend variant '{matName}'");
                }
            }
        }
    }

    public void CreateMultiTextureMaterials(List<Material> materials, List<string> materialNames, Dictionary<string, string> texturePaths)
    {
        var textureUVNames = new Dictionary<string, string>();
        CreateMultiTextureMaterials(materials, materialNames, texturePaths, textureUVNames);
    }

    // Legacy overload for backward compatibility
    public List<Material> CreateMaterials(Dictionary<string, string> texturePaths, GameObject rootGO)
    {
        return CreateMaterials(texturePaths, new Dictionary<string, string>(), rootGO);
    }
    
    private Texture2D FindTextureInProject(string texturePath)
    {
        if (string.IsNullOrWhiteSpace(texturePath))
        {
            return null;
        }

        // Initialize texture cache if needed
        if (!_textureCacheInitialized)
        {
            InitializeTextureCache();
        }

        if (TryResolveTextureFromCache(texturePath, out Texture2D cachedTexture))
        {
            return cachedTexture;
        }

        // Cache can be stale while assets are being imported; rebuild once and retry.
        InitializeTextureCache(forceRebuild: true);
        if (TryResolveTextureFromCache(texturePath, out cachedTexture))
        {
            DebugLogger.LogEggImporter($"Resolved texture after cache rebuild: {texturePath}");
            return cachedTexture;
        }

        // Last resort: targeted AssetDatabase lookup by filename.
        string fileName = Path.GetFileNameWithoutExtension(texturePath);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            string[] guids = AssetDatabase.FindAssets($"{fileName} t:Texture2D");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    continue;
                }

                string candidateName = Path.GetFileNameWithoutExtension(assetPath);
                if (!string.Equals(candidateName, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture == null)
                {
                    continue;
                }

                RegisterTextureAliases(assetPath, texture);
                DebugLogger.LogEggImporter($"Resolved texture via AssetDatabase fallback: {texturePath} -> {assetPath}");
                return texture;
            }
        }

        DebugLogger.LogWarningEggImporter($"Texture not found in cache: {texturePath}");
        return null;
    }
    
    private static bool TryResolveTextureFromCache(string texturePath, out Texture2D texture)
    {
        texture = null;
        if (_textureCache == null || _textureCache.Count == 0)
        {
            return false;
        }

        foreach (string candidate in GetTextureLookupCandidates(texturePath))
        {
            if (_textureCache.TryGetValue(candidate, out Texture2D cachedTexture) && cachedTexture != null)
            {
                DebugLogger.LogEggImporter($"Found cached texture: {texturePath} -> {candidate}");
                texture = cachedTexture;
                return true;
            }
        }

        string normalized = NormalizeTextureLookupPath(texturePath);
        if (!string.IsNullOrWhiteSpace(normalized) && normalized.Contains("/"))
        {
            string suffix = "/" + normalized;
            foreach (KeyValuePair<string, Texture2D> kvp in _textureCache)
            {
                if (kvp.Value == null)
                {
                    continue;
                }

                if (kvp.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    DebugLogger.LogEggImporter($"Found cached texture by suffix match: {texturePath} -> {kvp.Key}");
                    texture = kvp.Value;
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<string> GetTextureLookupCandidates(string texturePath)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string normalized = NormalizeTextureLookupPath(texturePath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return candidates;
        }

        candidates.Add(normalized);

        string withoutPhase = StripKnownPhasePrefix(normalized);
        candidates.Add(withoutPhase);

        if (withoutPhase.StartsWith("maps/", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(withoutPhase.Substring("maps/".Length));
        }
        else if (!withoutPhase.Contains("/"))
        {
            candidates.Add("maps/" + withoutPhase);
        }

        string fileName = Path.GetFileNameWithoutExtension(withoutPhase);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            candidates.Add(fileName.ToLowerInvariant());
        }

        return candidates;
    }

    private static void InitializeTextureCache(bool forceRebuild = false)
    {
        // More robust cache validation
        if (!forceRebuild && _textureCacheInitialized && _textureCache != null && _textureCache.Count > 0)
        {
            return;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _textureCache = new Dictionary<string, Texture2D>(System.StringComparer.OrdinalIgnoreCase);

        // Find ALL textures in the project once
        string[] guids = AssetDatabase.FindAssets("t:texture2D");
        DebugLogger.LogEggImporter($"Initializing texture cache with {guids.Length} textures...");

        foreach (string guid in guids)
        {
            string fullPath = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
            if (texture == null)
            {
                continue;
            }

            RegisterTextureAliases(fullPath, texture);
        }

        stopwatch.Stop();
        DebugLogger.LogEggImporter($"Texture cache initialized in {stopwatch.ElapsedMilliseconds}ms with {_textureCache.Count} entries");
        _textureCacheInitialized = true;
    }

    private static void RegisterTextureAliases(string assetPath, Texture2D texture)
    {
        if (texture == null || string.IsNullOrWhiteSpace(assetPath) || _textureCache == null)
        {
            return;
        }

        string normalizedAssetPath = assetPath.Replace("\\", "/");
        AddTextureCacheKey(Path.GetFileNameWithoutExtension(normalizedAssetPath), texture);

        int resourcesIndex = normalizedAssetPath.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
        if (resourcesIndex < 0)
        {
            return;
        }

        string relativePath = normalizedAssetPath.Substring(resourcesIndex + "/Resources/".Length);
        string normalizedRelative = NormalizeTextureLookupPath(relativePath);
        AddTextureCacheKey(normalizedRelative, texture);

        string withoutPhase = StripKnownPhasePrefix(normalizedRelative);
        AddTextureCacheKey(withoutPhase, texture);

        if (withoutPhase.StartsWith("maps/", StringComparison.OrdinalIgnoreCase))
        {
            AddTextureCacheKey(withoutPhase.Substring("maps/".Length), texture);
        }

        int mapsIndex = withoutPhase.IndexOf("/maps/", StringComparison.OrdinalIgnoreCase);
        if (mapsIndex >= 0)
        {
            AddTextureCacheKey(withoutPhase.Substring(mapsIndex + "/maps/".Length), texture);
        }
    }

    private static void AddTextureCacheKey(string key, Texture2D texture)
    {
        if (texture == null || string.IsNullOrWhiteSpace(key) || _textureCache == null)
        {
            return;
        }

        string normalized = NormalizeTextureLookupPath(key);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (!_textureCache.ContainsKey(normalized))
        {
            _textureCache[normalized] = texture;
        }
    }

    private static string NormalizeTextureLookupPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string normalized = path.Trim().Trim('"').Replace('\\', '/');

        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized.Substring(2);
        }

        normalized = normalized.TrimStart('/');

        if (normalized.StartsWith("resources/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring("resources/".Length);
        }

        int extIndex = normalized.LastIndexOf('.');
        if (extIndex > 0)
        {
            normalized = normalized.Substring(0, extIndex);
        }

        return normalized.ToLowerInvariant();
    }

    private static string StripKnownPhasePrefix(string normalizedPath)
    {
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return normalizedPath;
        }

        foreach (string phasePrefix in KnownPhasePrefixes)
        {
            if (normalizedPath.StartsWith(phasePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedPath.Substring(phasePrefix.Length);
            }
        }

        return normalizedPath;
    }

    private Color GetDefaultColorForMaterial(string materialName)
    {
        // Use cache to avoid repeated string operations for same material names
        if (DefaultColorCache.TryGetValue(materialName, out Color cachedColor))
            return cachedColor;
            
        string lowerName = materialName.ToLower();
        Color color;
        
        if (lowerName.Contains("skin") || lowerName.Contains("flesh"))
            color = new Color(1f, 0.8f, 0.7f);
        else if (lowerName.Contains("metal") || lowerName.Contains("steel"))
            color = new Color(0.7f, 0.7f, 0.8f);
        else if (lowerName.Contains("wood"))
            color = new Color(0.6f, 0.4f, 0.2f);
        else if (lowerName.Contains("grass") || lowerName.Contains("leaf"))
            color = new Color(0.2f, 0.8f, 0.2f);
        else if (lowerName.Contains("water"))
            color = new Color(0.2f, 0.5f, 0.8f);
        else if (lowerName.Contains("stone") || lowerName.Contains("rock"))
            color = new Color(0.5f, 0.5f, 0.5f);
        else
            color = new Color(0.7f, 0.7f, 0.7f);
            
        // Cache the result for future use
        DefaultColorCache[materialName] = color;
        return color;
    }
    
    public Dictionary<string, Material> CreateMaterialDictionary(List<Material> materials)
    {
        // Pre-size dictionary to avoid resizing
        var materialDict = new Dictionary<string, Material>(materials.Count, System.StringComparer.OrdinalIgnoreCase);
        
        foreach (var mat in materials)
        {
            materialDict[mat.name] = mat;
        }
        
        if (!materialDict.ContainsKey("Default-Material") && materials.Count > 0)
        {
            // Find the Default-Material or use the first material
            var defaultMat = materials.FirstOrDefault(m => m.name == "Default-Material") ?? materials[0];
            materialDict["Default-Material"] = defaultMat;
        }
        
        return materialDict;
    }
    
    private Material CreateVertexColorMaterial(string materialName, bool useAlphaBlend = false)
    {
        // Cache shaders to avoid repeated Shader.Find calls
        Shader shader = useAlphaBlend ? GetCachedTransparentShader() : GetCachedVertexColorShader();

        Material mat = new Material(shader) { name = materialName };

        // Set material color to white so vertex colors show through properly
        if (mat.HasProperty(ColorPropertyId))
            mat.SetColor(ColorPropertyId, Color.white);
        else
            mat.color = Color.white;

        // Set standard shader properties if they exist using cached property IDs
        if (mat.HasProperty(MetallicPropertyId))
            mat.SetFloat(MetallicPropertyId, 0.0f);
        if (mat.HasProperty(GlossinessPropertyId))
            mat.SetFloat(GlossinessPropertyId, 0.1f);
            
        // Enable GPU Instancing
        mat.enableInstancing = true;

        DebugLogger.LogEggImporter($"Created vertex color material '{materialName}' using shader: {shader.name}");

        return mat;
    }

    private Material CreateInvisibleCollisionMaterial()
    {
        // Use custom invisible collision shader
        Shader invisibleShader = Shader.Find("EggImporter/InvisibleCollision");

        if (invisibleShader == null)
        {
            DebugLogger.LogWarningEggImporter("EggImporter/InvisibleCollision shader not found!");
            invisibleShader = Shader.Find("Hidden/InternalErrorShader");
        }

        Material mat = new Material(invisibleShader) { name = "Collision-Material" };

        DebugLogger.LogEggImporter($"Created invisible Collision-Material using shader: {invisibleShader.name}");

        return mat;
    }
    
    private Shader GetCachedVertexColorShader()
    {
        // Return cached shader if available
        if (_vertexColorShader != null) return _vertexColorShader;
        
        // First try to use our custom vertex color shader
        _vertexColorShader = Shader.Find("EggImporter/VertexColorTexture");
        
        if (_vertexColorShader != null) return _vertexColorShader;
        
        // Fallback to Legacy Shaders/Diffuse if custom shader not found
        if (_legacyDiffuseShader == null)
        {
            _legacyDiffuseShader = Shader.Find("Legacy Shaders/Diffuse");
            if (_legacyDiffuseShader != null)
            {
                DebugLogger.LogWarningEggImporter("Custom EggImporter/VertexColorTexture shader not found, falling back to Legacy Shaders/Diffuse");
                return _legacyDiffuseShader;
            }
        }
        else
        {
            return _legacyDiffuseShader;
        }
        
        // Last resort - Standard shader
        if (_standardShader == null)
        {
            _standardShader = Shader.Find("Standard");
            if (_standardShader != null)
            {
                DebugLogger.LogWarningEggImporter("Legacy Shaders/Diffuse not found, using Standard shader (vertex colors may not display)");
            }
        }
        
        return _standardShader;
    }

    private Shader GetCachedTransparentShader()
    {
        // Return cached shader if available
        if (_vertexColorTransparentShader != null) return _vertexColorTransparentShader;

        // Try to use our custom transparent vertex color shader
        _vertexColorTransparentShader = Shader.Find("EggImporter/VertexColorTextureTransparent");

        if (_vertexColorTransparentShader != null) return _vertexColorTransparentShader;

        // Fallback to standard transparent shader
        Shader transparentFallback = Shader.Find("Legacy Shaders/Transparent/Diffuse");
        if (transparentFallback != null)
        {
            DebugLogger.LogWarningEggImporter("Custom EggImporter/VertexColorTextureTransparent shader not found, falling back to Legacy Shaders/Transparent/Diffuse");
            return transparentFallback;
        }

        // Last resort - use opaque shader
        DebugLogger.LogWarningEggImporter("No transparent shader found, falling back to opaque shader");
        return GetCachedVertexColorShader();
    }



    private static Texture2D LoadTextureByFileName(string fileName)
    {
        // Initialize texture cache if needed
        if (!_textureCacheInitialized)
        {
            InitializeTextureCache();
        }
        
        // Use cache instead of expensive AssetDatabase.FindAssets
        string cacheKey = fileName.ToLowerInvariant();
        if (_textureCache.TryGetValue(cacheKey, out Texture2D texture))
        {
            return texture;
        }
        
        return null;
    }
    
    /// <summary>
    /// Force refresh of texture cache - call this if textures are added/removed
    /// </summary>
    public static void InvalidateTextureCache()
    {
        _textureCacheInitialized = false;
        _textureCache = null;
        DebugLogger.LogEggImporter("Texture cache invalidated - will rebuild on next access");
    }
}
