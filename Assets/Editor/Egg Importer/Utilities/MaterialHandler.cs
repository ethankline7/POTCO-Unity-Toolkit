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
    
    // New overload that accepts alpha textures specified by .egg files
    public List<Material> CreateMaterials(Dictionary<string, string> texturePaths, Dictionary<string, string> alphaPaths, GameObject rootGO)
    {
        DebugLogger.LogEggImporter($"Creating materials from {texturePaths.Count} texture paths and {alphaPaths.Count} alpha paths");
        var materials = new List<Material>(texturePaths.Count + 1);
        var createdMaterialNames = new HashSet<string>(); // Track what we've created

        foreach (var kvp in texturePaths)
        {
            string materialName = kvp.Key;
            string texturePath = kvp.Value;

            DebugLogger.LogEggImporter($"Creating material: {materialName} with texture: {texturePath}");

            // Check if this material has an alpha texture specified
            string alphaPath = alphaPaths.TryGetValue(materialName, out string alpha) ? alpha : null;

            Material mat;
            string textureFileName = Path.GetFileName(texturePath);
            Texture2D colorTex = FindTextureInProject(textureFileName);
            if (!colorTex) colorTex = LoadTextureByFileName(textureFileName);

            Texture2D alphaTex = null;
            if (!string.IsNullOrEmpty(alphaPath))
            {
                string alphaFileName = Path.GetFileName(alphaPath);
                alphaTex = FindTextureInProject(alphaFileName);
                if (!alphaTex) alphaTex = LoadTextureByFileName(alphaFileName);

                if (alphaTex)
                    DebugLogger.LogEggImporter($"[AlphaMask] Found alpha texture for {materialName}: {alphaFileName}");
                else
                    DebugLogger.LogWarningEggImporter($"[AlphaMask] Could not load alpha texture: {alphaFileName}");
            }

            // Always use the unified vertex color material
            mat = CreateVertexColorMaterial(materialName);

            if (colorTex)
            {
                mat.mainTexture = colorTex;
                DebugLogger.LogEggImporter($"Assigned texture {textureFileName} to material {materialName}");
            }
            else
            {
                DebugLogger.LogWarningEggImporter($"Could not find texture: {textureFileName}");
                mat.color = GetDefaultColorForMaterial(materialName);
            }

            // Set alpha texture if available
            if (alphaTex)
            {
                mat.SetTexture("_AlphaTex", alphaTex);
                if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.1f);
                // Set culling to off so both sides are visible for alpha-masked materials
                mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                DebugLogger.LogEggImporter($"[AlphaMask] Assigned alpha texture to {materialName}: {alphaTex.name} (Cull: Off)");
            }
            else
            {
                // Set culling to back for regular materials (only show front faces)
                mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                DebugLogger.LogEggImporter($"[Standard] Material {materialName} set to back-face culling (Cull: Back)");
            }

            // Use cached property IDs for better performance
            if (mat.HasProperty(MetallicPropertyId))
                mat.SetFloat(MetallicPropertyId, 0.0f);
            if (mat.HasProperty(GlossinessPropertyId))
                mat.SetFloat(GlossinessPropertyId, 0.1f);

            materials.Add(mat);
            createdMaterialNames.Add(materialName);
        }

        // Always ensure Default-Material exists
        var defaultMaterial = CreateVertexColorMaterial("Default-Material");
        materials.Add(defaultMaterial);
        createdMaterialNames.Add("Default-Material");

        if (materials.Count == 1)
        {
            DebugLogger.LogEggImporter("No textures found, using default material only");
        }

        return materials;
    }

    public void CreateMultiTextureMaterials(List<Material> materials, List<string> materialNames, Dictionary<string, string> texturePaths)
    {
        DebugLogger.LogEggImporter($"[MultiTex] Processing {materialNames.Count} material names for multi-texture support");

        foreach (string matName in materialNames)
        {
            if (matName.Contains("||"))
            {
                // Multi-texture material like "island_wild_palette_3cmla_1||pir_t_are_isl_multi_dirtRock"
                var texNames = matName.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries);

                if (texNames.Length >= 2)
                {
                    DebugLogger.LogEggImporter($"[MultiTex] Creating multi-texture material: {matName}");

                    // Load textures
                    Texture2D baseTex = null;
                    Texture2D overlayTex = null;

                    if (texturePaths.TryGetValue(texNames[0], out string basePath))
                    {
                        string baseFileName = Path.GetFileName(basePath);
                        baseTex = FindTextureInProject(baseFileName);
                        if (!baseTex) baseTex = LoadTextureByFileName(baseFileName);
                    }

                    if (texNames.Length > 1 && texturePaths.TryGetValue(texNames[1], out string overlayPath))
                    {
                        string overlayFileName = Path.GetFileName(overlayPath);
                        overlayTex = FindTextureInProject(overlayFileName);
                        if (!overlayTex) overlayTex = LoadTextureByFileName(overlayFileName);
                    }

                    // Create material with VertexColorTexture shader
                    Shader shader = GetCachedVertexColorShader();

                    Material mat = new Material(shader) { name = matName };

                    if (baseTex)
                    {
                        mat.SetTexture("_MainTex", baseTex);
                        DebugLogger.LogEggImporter($"[MultiTex] Set base texture: {baseTex.name}");
                    }

                    if (overlayTex)
                    {
                        mat.SetTexture("_BlendTex", overlayTex);
                        DebugLogger.LogEggImporter($"[MultiTex] Set blend texture: {overlayTex.name}");
                    }

                    mat.SetColor("_Color", Color.white);
                    materials.Add(mat);
                }
            }
        }
    }

    // Legacy overload for backward compatibility
    public List<Material> CreateMaterials(Dictionary<string, string> texturePaths, GameObject rootGO)
    {
        return CreateMaterials(texturePaths, new Dictionary<string, string>(), rootGO);
    }
    
    private Texture2D FindTextureInProject(string textureFileName)
    {
        // Initialize texture cache if needed
        if (!_textureCacheInitialized)
        {
            InitializeTextureCache();
        }
        
        // Check cache first
        string cacheKey = textureFileName.ToLowerInvariant();
        if (_textureCache.TryGetValue(cacheKey, out Texture2D cachedTexture))
        {
            if (cachedTexture != null)
            {
                DebugLogger.LogEggImporter($"Found cached texture: {textureFileName}");
                return cachedTexture;
            }
        }
        
        DebugLogger.LogWarningEggImporter($"Texture not found in cache: {textureFileName}");
        return null;
    }
    
    private static void InitializeTextureCache()
    {
        // More robust cache validation
        if (_textureCacheInitialized && _textureCache != null && _textureCache.Count > 0) return;
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _textureCache = new Dictionary<string, Texture2D>(System.StringComparer.OrdinalIgnoreCase);
        
        // Find ALL textures in the project once
        string[] guids = AssetDatabase.FindAssets("t:texture2D");
        DebugLogger.LogEggImporter($"Initializing texture cache with {guids.Length} textures...");
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileName(path).ToLowerInvariant();
            
            if (!_textureCache.ContainsKey(fileName))
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    _textureCache[fileName] = texture;
                }
            }
        }
        
        stopwatch.Stop();
        DebugLogger.LogEggImporter($"Texture cache initialized in {stopwatch.ElapsedMilliseconds}ms with {_textureCache.Count} textures");
        _textureCacheInitialized = true;
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
        var materialDict = new Dictionary<string, Material>(materials.Count);
        
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
    
    private Material CreateVertexColorMaterial(string materialName)
    {
        // Cache shaders to avoid repeated Shader.Find calls
        Shader shader = GetCachedVertexColorShader();
        
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
            
        DebugLogger.LogEggImporter($"Created vertex color material '{materialName}' using shader: {shader.name}");
        
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