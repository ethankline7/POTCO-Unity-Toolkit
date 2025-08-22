using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using POTCO.Editor;

public class MultiTextureMaterialHandler
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
    
    private void ApplyTextureWrapModes(Texture2D texture, string textureName, MultiTextureEggImporter importer)
    {
        if (texture == null || importer == null) return;
        
        // Set wrap mode U
        if (importer.TextureWrapU.TryGetValue(textureName, out string wrapU))
        {
            texture.wrapModeU = wrapU == "repeat" ? TextureWrapMode.Repeat :
                                wrapU == "mirror" ? TextureWrapMode.Mirror :
                                TextureWrapMode.Clamp;
            DebugLogger.LogEggImporter($"🔄 Applied wrap mode U: {wrapU} to texture '{textureName}'");
        }
        
        // Set wrap mode V  
        if (importer.TextureWrapV.TryGetValue(textureName, out string wrapV))
        {
            texture.wrapModeV = wrapV == "repeat" ? TextureWrapMode.Repeat :
                                wrapV == "mirror" ? TextureWrapMode.Mirror :
                                TextureWrapMode.Clamp;
            DebugLogger.LogEggImporter($"🔄 Applied wrap mode V: {wrapV} to texture '{textureName}'");
        }
    }
    
    public List<Material> CreateMaterials(Dictionary<string, string> texturePaths, GameObject rootGO, MultiTextureEggImporter importer = null)
    {
        DebugLogger.LogEggImporter($"Creating materials from {texturePaths.Count} texture paths");
        // Pre-size the list to avoid resizing during population
        var materials = new List<Material>(texturePaths.Count + 1);
        
        // Check if this looks like a DelFuego-pattern model that needs overlay treatment
        bool isDelFuegoModel = IsDelFuegoPatternModel(texturePaths);
        
        foreach (var kvp in texturePaths)
        {
            string materialName = kvp.Key;
            string texturePath = kvp.Value;
            
            DebugLogger.LogEggImporter($"Creating material: {materialName} with texture: {texturePath}");
            
            Material mat;
            
            // Special handling for DelFuego-pattern models needing overlay treatment
            if (isDelFuegoModel && ShouldCreateOverlayMaterial(materialName, texturePaths))
            {
                mat = CreateDelFuegoOverlayMaterial(materialName, texturePaths);
                DebugLogger.LogEggImporter($"🔥 Created DelFuego overlay material: {materialName}");
            }
            else
            {
                mat = CreateVertexColorMaterial(materialName);
                
                string textureFileName = Path.GetFileName(texturePath);
                Texture2D texture = FindTextureInProject(textureFileName);
                
                if (texture != null)
                {
                    mat.mainTexture = texture;
                    
                    // Apply wrap modes from EGG file data if available, otherwise use defaults
                    if (importer != null)
                    {
                        ApplyTextureWrapModes(texture, materialName, importer);
                    }
                    else
                    {
                        // Fallback to research-based wrap mode detection
                        texture.wrapMode = DetermineTextureWrapMode(textureFileName, materialName);
                    }
                    DebugLogger.LogEggImporter($"Assigned texture {textureFileName} with wrap modes U:{texture.wrapModeU} V:{texture.wrapModeV}");
                }
                else
                {
                    DebugLogger.LogWarningEggImporter($"Could not find texture: {textureFileName}");
                    mat.color = GetDefaultColorForMaterial(materialName);
                }
                
                // Use cached property IDs for better performance
                if (mat.HasProperty(MetallicPropertyId))
                    mat.SetFloat(MetallicPropertyId, 0.0f);
                if (mat.HasProperty(GlossinessPropertyId))
                    mat.SetFloat(GlossinessPropertyId, 0.0f);
            }
            
            materials.Add(mat);
        }
        
        // Always ensure Default-Material exists for polygons that don't specify textures
        var defaultMaterial = CreateVertexColorMaterial("Default-Material");
        materials.Add(defaultMaterial);
        
        if (materials.Count == 1) // Only default material was added
        {
            DebugLogger.LogEggImporter("No textures found, using default material only");
        }
        
        return materials;
    }
    
    public List<Material> CreateMaterialsWithMultiTexture(Dictionary<string, string> texturePaths, List<string> materialNames, GameObject rootGO, Vector4 uvBounds, MultiTextureEggImporter importer = null)
    {
        DebugLogger.LogEggImporter($"🔍 Creating materials from {texturePaths.Count} texture paths and {materialNames.Count} material names");
        DebugLogger.LogEggImporter($"🔍 Texture paths: {string.Join(", ", texturePaths.Keys)}");
        DebugLogger.LogEggImporter($"🔍 Material names: {string.Join(", ", materialNames)}");
        
        // Pre-size the list to avoid resizing during population
        var materials = new List<Material>();
        
        // First create single-texture materials
        foreach (var kvp in texturePaths)
        {
            string materialName = kvp.Key;
            string texturePath = kvp.Value;
            
            DebugLogger.LogEggImporter($"Creating single-texture material: {materialName} with texture: {texturePath}");
            
            Material mat = CreateVertexColorMaterial(materialName);
            
            string textureFileName = Path.GetFileName(texturePath);
            Texture2D texture = FindTextureInProject(textureFileName);
            
            if (texture != null)
            {
                mat.mainTexture = texture;
                
                // Apply wrap modes from EGG file data if available, otherwise use defaults
                if (importer != null)
                {
                    ApplyTextureWrapModes(texture, materialName, importer);
                }
                else
                {
                    // Fallback to research-based wrap mode detection
                    texture.wrapMode = DetermineTextureWrapMode(textureFileName, materialName);
                }
                DebugLogger.LogEggImporter($"Assigned texture {textureFileName} with wrap modes U:{texture.wrapModeU} V:{texture.wrapModeV}");
            }
            else
            {
                DebugLogger.LogWarningEggImporter($"Could not find texture: {textureFileName}");
                mat.color = GetDefaultColorForMaterial(materialName);
            }
            
            // Use cached property IDs for better performance
            if (mat.HasProperty(MetallicPropertyId))
                mat.SetFloat(MetallicPropertyId, 0.0f);
            if (mat.HasProperty(GlossinessPropertyId))
                mat.SetFloat(GlossinessPropertyId, 0.0f);
            
            materials.Add(mat);
        }
        
        // Create multi-texture materials for combined material names
        int multiTextureCount = 0;
        foreach (string materialName in materialNames)
        {
            if (materialName.Contains("||")) // Multi-texture material
            {
                multiTextureCount++;
                var textureNames = materialName.Split(new[] { "||" }, StringSplitOptions.RemoveEmptyEntries);
                if (textureNames.Length >= 2)
                {
                    DebugLogger.LogEggImporter($"🎨 Creating multi-texture material #{multiTextureCount}: {materialName}");
                    DebugLogger.LogEggImporter($"🎨 Texture components: [{string.Join(", ", textureNames)}]");
                    Material multiMat = CreateMultiTextureMaterial(materialName, textureNames, texturePaths, uvBounds, importer);
                    materials.Add(multiMat);
                }
            }
            else if (!texturePaths.ContainsKey(materialName) && materialName != "Default-Material")
            {
                // Check if this material has already been created
                bool alreadyExists = materials.Any(m => m.name == materialName);
                if (!alreadyExists)
                {
                    DebugLogger.LogEggImporter($"Creating basic material for: {materialName}");
                    Material basicMat = CreateVertexColorMaterial(materialName);
                    basicMat.color = GetDefaultColorForMaterial(materialName);
                    materials.Add(basicMat);
                }
            }
        }
        
        DebugLogger.LogEggImporter($"🎨 Created {multiTextureCount} multi-texture materials total");
        
        // Always ensure Default-Material exists for polygons that don't specify textures
        var defaultMaterial = CreateVertexColorMaterial("Default-Material");
        materials.Add(defaultMaterial);
        
        DebugLogger.LogEggImporter($"🔍 Total materials created: {materials.Count}");
        
        return materials;
    }
    
    private Texture2D FindTextureInProject(string textureFileName)
    {
        DebugLogger.LogEggImporter($"🔍 Searching for texture: '{textureFileName}'");
        string searchName = Path.GetFileNameWithoutExtension(textureFileName);
        DebugLogger.LogEggImporter($"🔍 Search name (without extension): '{searchName}'");
        
        string[] guids = AssetDatabase.FindAssets(searchName + " t:texture2D");
        DebugLogger.LogEggImporter($"🔍 Found {guids.Length} potential matches");
        
        // List ALL files found by Unity's search
        DebugLogger.LogEggImporter($"🔍 ALL FILES FOUND:");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string foundFileName = Path.GetFileName(path);
            DebugLogger.LogEggImporter($"🔍   File: '{foundFileName}' at '{path}'");
        }
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string foundFileName = Path.GetFileName(path);
            DebugLogger.LogEggImporter($"🔍 Checking: '{foundFileName}' at path: '{path}'");
            
            if (foundFileName.Equals(textureFileName, System.StringComparison.OrdinalIgnoreCase))
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    DebugLogger.LogEggImporter($"✅ Found texture at: {path}");
                    return texture;
                }
                else
                {
                    DebugLogger.LogErrorEggImporter($"🚨 Texture file exists but failed to load: {path}");
                }
            }
            else
            {
                DebugLogger.LogEggImporter($"🔍 Name mismatch: expected '{textureFileName}', found '{foundFileName}'");
            }
        }
        
        // Try alternative search method - search for any texture with this exact name
        DebugLogger.LogEggImporter($"🔍 Trying alternative search for exact filename...");
        string[] allTextureGuids = AssetDatabase.FindAssets("t:texture2D");
        DebugLogger.LogEggImporter($"🔍 Searching through {allTextureGuids.Length} total textures...");
        
        foreach (string guid in allTextureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string foundFileName = Path.GetFileName(path);
            
            if (foundFileName.Equals(textureFileName, System.StringComparison.OrdinalIgnoreCase))
            {
                DebugLogger.LogEggImporter($"🔍 ALTERNATIVE SEARCH: Found exact match '{foundFileName}' at '{path}'");
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    DebugLogger.LogEggImporter($"✅ Successfully loaded texture via alternative search: {path}");
                    return texture;
                }
            }
        }
        
        DebugLogger.LogWarningEggImporter($"🚨 Texture not found in project: {textureFileName}");
        return null;
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
        // REVERTED: Back to original vertex color shader approach
        Shader shader = GetCachedVertexColorShader();
        
        Material mat = new Material(shader) { name = materialName };
        
        // REVERTED: Original white color for proper vertex color display
        if (mat.HasProperty(ColorPropertyId))
            mat.SetColor(ColorPropertyId, Color.white);
        else
            mat.color = Color.white;
        
        // REVERTED: Minimal shader properties for vertex color shader
        if (mat.HasProperty(MetallicPropertyId))
            mat.SetFloat(MetallicPropertyId, 0.0f);
        if (mat.HasProperty(GlossinessPropertyId))
            mat.SetFloat(GlossinessPropertyId, 0.0f);
            
        DebugLogger.LogEggImporter($"Created vertex color material '{materialName}' using shader: {shader.name}");
        
        return mat;
    }
    
    // REMOVED: Dynamic tiling shader methods - reverted to pre-shader state
    
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
    
    // REMOVED: Standard shader helpers - reverted to vertex color approach
    
    private Material CreateMultiTextureMaterial(string materialName, string[] textureNames, Dictionary<string, string> texturePaths, Vector4 uvBounds, MultiTextureEggImporter importer = null)
    {
        DebugLogger.LogEggImporter($"🔧 Creating multi-texture material: {materialName}");
        DebugLogger.LogEggImporter($"🔧 Texture names array: [{string.Join(", ", textureNames)}]");
        
        // Use custom MultiTexture shader for proper multiplicative blending (darker when combined)
        Shader multiShader = Shader.Find("EggImporter/MultiTextureBlend");
        if (multiShader == null) multiShader = Shader.Find("Standard");
        Material mat = new Material(multiShader) { name = materialName };
        
        // Find the base/main texture (usually the island palette)
        Texture2D mainTexture = null;
        Texture2D overlayTexture = null;
        
        foreach (string textureName in textureNames)
        {
            if (texturePaths.ContainsKey(textureName))
            {
                string texturePath = texturePaths[textureName];
                string textureFileName = Path.GetFileName(texturePath);
                Texture2D texture = FindTextureInProject(textureFileName);
                
                if (texture != null)
                {
                    // Identify main vs overlay texture by UV set presence
                    // Textures with named UV sets (like "multi" or "grunge") are overlay textures
                    // Textures without named UV sets use the primary UV and are main textures
                    bool hasNamedUVSet = importer != null && importer.TextureToUVSet.ContainsKey(textureName);
                    
                    DebugLogger.LogEggImporter($"🔍 Texture analysis: '{textureName}' hasNamedUVSet={hasNamedUVSet}");
                    if (hasNamedUVSet && importer != null)
                    {
                        DebugLogger.LogEggImporter($"🔍   UV set name: '{importer.TextureToUVSet[textureName]}'");
                    }
                    
                    // Overlay textures have named UV sets (multi, grunge, etc.)
                    if (hasNamedUVSet)
                    {
                        if (overlayTexture == null) // Only assign if we don't already have an overlay
                        {
                            overlayTexture = texture;
                            // Apply wrap modes from EGG file data if available, otherwise default to repeat
                            if (importer != null)
                            {
                                ApplyTextureWrapModes(texture, textureName, importer);
                            }
                            else
                            {
                                texture.wrapMode = TextureWrapMode.Repeat; // Overlay texture uses repeat for tiling
                            }
                            DebugLogger.LogEggImporter($"🔄 Assigned tiling overlay texture: {textureFileName} (UV set: {importer?.TextureToUVSet[textureName] ?? "unknown"})");
                        }
                        else
                        {
                            DebugLogger.LogEggImporter($"⚠️ Skipping additional overlay texture: {textureFileName} (already have {overlayTexture.name})");
                        }
                    }
                    // Main textures use primary UV (no named UV set)
                    else
                    {
                        if (mainTexture == null) // Only assign if we don't already have a main texture
                        {
                            mainTexture = texture;
                            // Apply wrap modes from EGG file data if available, otherwise default to clamp
                            if (importer != null)
                            {
                                ApplyTextureWrapModes(texture, textureName, importer);
                                // Override wrap mode for palette textures that are incorrectly set to repeat in EGG files
                                if (textureName.ToLower().Contains("palette"))
                                {
                                    texture.wrapMode = TextureWrapMode.Clamp;
                                    DebugLogger.LogEggImporter($"🔧 Overrode wrap mode to clamp for palette texture: {textureFileName}");
                                }
                            }
                            else
                            {
                                texture.wrapMode = TextureWrapMode.Clamp; // Base texture uses clamp
                            }
                            DebugLogger.LogEggImporter($"🎨 Assigned main texture: {textureFileName}");
                        }
                        else
                        {
                            DebugLogger.LogEggImporter($"⚠️ Skipping additional main texture: {textureFileName} (already have {mainTexture.name})");
                        }
                    }
                }
            }
        }
        
        // Fallback strategy: If we have textures but both/neither have named UV sets,
        // use texture name patterns to determine main vs overlay
        if (mainTexture == null && overlayTexture == null && textureNames.Length >= 2)
        {
            DebugLogger.LogEggImporter($"⚠️ Fallback strategy: Both textures lack UV set info, using name patterns");
            
            // Find textures and assign based on name patterns
            Texture2D firstTexture = null, secondTexture = null;
            string firstName = "", secondName = "";
            
            for (int i = 0; i < Mathf.Min(2, textureNames.Length); i++)
            {
                if (texturePaths.ContainsKey(textureNames[i]))
                {
                    string texturePath = texturePaths[textureNames[i]];
                    string textureFileName = Path.GetFileName(texturePath);
                    Texture2D texture = FindTextureInProject(textureFileName);
                    
                    if (texture != null)
                    {
                        if (i == 0) { firstTexture = texture; firstName = textureNames[i]; }
                        else { secondTexture = texture; secondName = textureNames[i]; }
                    }
                }
            }
            
            // Use name patterns to decide which is main vs overlay
            if (firstTexture != null && secondTexture != null)
            {
                bool firstIsOverlay = firstName.ToLower().Contains("multi") || firstName.ToLower().Contains("overlay") || firstName.ToLower().Contains("grunge");
                bool secondIsOverlay = secondName.ToLower().Contains("multi") || secondName.ToLower().Contains("overlay") || secondName.ToLower().Contains("grunge");
                bool firstIsPalette = firstName.ToLower().Contains("palette");
                bool secondIsPalette = secondName.ToLower().Contains("palette");
                
                DebugLogger.LogEggImporter($"🔍 Pattern analysis: '{firstName}' isOverlay={firstIsOverlay} isPalette={firstIsPalette}");
                DebugLogger.LogEggImporter($"🔍 Pattern analysis: '{secondName}' isOverlay={secondIsOverlay} isPalette={secondIsPalette}");
                
                if (firstIsOverlay && !secondIsOverlay)
                {
                    // First texture is overlay, second is main
                    overlayTexture = firstTexture;
                    mainTexture = secondTexture;
                    DebugLogger.LogEggImporter($"📍 Pattern-based assignment: '{firstName}' → overlay, '{secondName}' → main");
                }
                else if (!firstIsOverlay && secondIsOverlay)
                {
                    // First texture is main, second is overlay  
                    mainTexture = firstTexture;
                    overlayTexture = secondTexture;
                    DebugLogger.LogEggImporter($"📍 Pattern-based assignment: '{firstName}' → main, '{secondName}' → overlay");
                }
                else if (firstIsPalette && !secondIsPalette)
                {
                    // First texture is palette (main), second is overlay
                    mainTexture = firstTexture;
                    overlayTexture = secondTexture;
                    DebugLogger.LogEggImporter($"📍 Palette-based assignment: '{firstName}' → main (palette), '{secondName}' → overlay");
                }
                else if (!firstIsPalette && secondIsPalette)
                {
                    // Second texture is palette (main), first is overlay
                    overlayTexture = firstTexture;
                    mainTexture = secondTexture;
                    DebugLogger.LogEggImporter($"📍 Palette-based assignment: '{firstName}' → overlay, '{secondName}' → main (palette)");
                }
                else
                {
                    // Both have same pattern, use first as main, second as overlay
                    mainTexture = firstTexture;
                    overlayTexture = secondTexture;
                    DebugLogger.LogEggImporter($"📍 Default assignment: '{firstName}' → main, '{secondName}' → overlay");
                }
                
                // Apply appropriate wrap modes
                if (importer != null)
                {
                    string mainTextureName = mainTexture == firstTexture ? firstName : secondName;
                    string overlayTextureName = overlayTexture == firstTexture ? firstName : secondName;
                    
                    if (mainTexture != null) 
                    {
                        ApplyTextureWrapModes(mainTexture, mainTextureName, importer);
                        // Override wrap mode for palette textures that are incorrectly set to repeat in EGG files
                        if (mainTextureName.ToLower().Contains("palette"))
                        {
                            mainTexture.wrapMode = TextureWrapMode.Clamp;
                            DebugLogger.LogEggImporter($"📍 Overrode wrap mode to clamp for palette texture: {mainTexture.name}");
                        }
                    }
                    if (overlayTexture != null) ApplyTextureWrapModes(overlayTexture, overlayTextureName, importer);
                }
                else
                {
                    // Apply default wrap modes when no importer data
                    if (mainTexture != null) mainTexture.wrapMode = TextureWrapMode.Clamp;
                    if (overlayTexture != null) overlayTexture.wrapMode = TextureWrapMode.Repeat;
                }
            }
        }
        
        // Set up the material with main texture and overlay
        DebugLogger.LogEggImporter($"🎨 Material '{materialName}' - Main: {mainTexture?.name ?? "none"}, Overlay: {overlayTexture?.name ?? "none"}");
        
        if (mainTexture != null)
        {
            mat.mainTexture = mainTexture;
            DebugLogger.LogEggImporter($"✅ Main texture applied: {mainTexture.name}");
        }
        
        // Set up overlay texture using MultiTextureBlend shader properties
        if (overlayTexture != null)
        {
            if (mat.HasProperty("_BlendTex"))
            {
                // Use custom MultiTexture shader
                mat.SetTexture("_BlendTex", overlayTexture);
                mat.SetFloat("_BlendMode", 0.5f); // 50% blend strength
                // Don't set _BlendScale - UVs are already properly scaled in the EGG file
                DebugLogger.LogEggImporter($"✅ Overlay texture applied with MultiTexture shader (multiplicative blend): {overlayTexture.name}");
            }
            else if (mat.HasProperty("_DetailAlbedoMap"))
            {
                // Fallback to Standard shader detail mapping
                mat.SetTexture("_DetailAlbedoMap", overlayTexture);
                mat.SetFloat("_DetailNormalMapScale", 1.0f);
                if (mat.HasProperty("_DetailAlbedoMapScale"))
                    mat.SetFloat("_DetailAlbedoMapScale", 0.5f);
                DebugLogger.LogEggImporter($"✅ Overlay texture applied as detail (fallback): {overlayTexture.name}");
            }
            else
            {
                // Last resort: Use overlay as main texture
                mat.mainTexture = overlayTexture;
                DebugLogger.LogEggImporter($"⚠️ Using overlay as main texture (no multi-texture support): {overlayTexture.name}");
            }
        }
        
        // Set material properties for better appearance
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0.0f);
        if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", 0.0f);
        if (mat.HasProperty("_SpecColor"))
            mat.SetColor("_SpecColor", Color.black);
        if (mat.HasProperty("_Shininess"))
            mat.SetFloat("_Shininess", 0.0f);
        
        return mat;
    }
    
    private TextureWrapMode DetermineTextureWrapMode(string textureFileName, string materialName)
    {
        // ENHANCED APPROACH: Force Repeat for all tiling textures, Clamp only for true atlases
        string lowerTexture = textureFileName.ToLower();
        string lowerMaterial = materialName.ToLower();
        
        // FORCE REPEAT for all DelFuego-type tiling textures
        if (lowerTexture.Contains("multi") || lowerTexture.Contains("rock") || lowerTexture.Contains("grass") || 
            lowerTexture.Contains("sand") || lowerTexture.Contains("cliff") || lowerTexture.Contains("ground") ||
            lowerMaterial.Contains("delfuego") || lowerMaterial.Contains("tortuga") || lowerMaterial.Contains("cuba"))
        {
            DebugLogger.LogEggImporter($"🔄 FORCING Repeat wrap mode for tiling texture: {textureFileName}");
            return TextureWrapMode.Repeat;
        }
        
        // Only clamp true palette/atlas textures (with palette in name)
        if (lowerTexture.Contains("palette") && !lowerTexture.Contains("multi"))
        {
            DebugLogger.LogEggImporter($"🔒 Using Clamp wrap mode for true palette texture: {textureFileName}");
            return TextureWrapMode.Clamp;
        }
        
        // Default to Repeat for everything else - better safe than sorry for tiling
        DebugLogger.LogEggImporter($"🔄 Using default Repeat wrap mode for texture: {textureFileName}");
        return TextureWrapMode.Repeat;
    }

    
    // DELFUEGO OVERLAY SYSTEM: Detect and create overlay materials for padres-style islands
    
    // Detect if this is a DelFuego-pattern model that needs overlay treatment
    private bool IsDelFuegoPatternModel(Dictionary<string, string> texturePaths)
    {
        // Look for DelFuego-style texture patterns (rule-compliant detection)
        bool hasIslandPalette = texturePaths.Keys.Any(k => k.ToLower().Contains("island") && k.ToLower().Contains("palette"));
        bool hasMultiTextures = texturePaths.Keys.Any(k => k.ToLower().Contains("multi_"));
        bool hasRockTextures = texturePaths.Keys.Any(k => k.ToLower().Contains("rock") || k.ToLower().Contains("cliff"));
        
        // DelFuego pattern: has base palette + multiple overlay textures
        return hasIslandPalette && (hasMultiTextures || hasRockTextures);
    }
    
    // Determine if this specific material should get overlay treatment
    private bool ShouldCreateOverlayMaterial(string materialName, Dictionary<string, string> texturePaths)
    {
        string lowerMaterial = materialName.ToLower();
        
        // Create overlay for materials that have both base and detail textures available
        bool isBaseTexture = lowerMaterial.Contains("island") && lowerMaterial.Contains("palette");
        
        if (isBaseTexture)
        {
            // Look for matching overlay textures
            bool hasMatchingOverlay = texturePaths.Keys.Any(k => 
                k.ToLower().Contains("multi_") || 
                k.ToLower().Contains("rock") || 
                k.ToLower().Contains("cliff"));
            
            return hasMatchingOverlay;
        }
        
        return false;
    }
    
    // Create DelFuego-style overlay material with base + tiling detail
    private Material CreateDelFuegoOverlayMaterial(string materialName, Dictionary<string, string> texturePaths)
    {
        // Use Standard shader for detail mapping support
        Material mat = new Material(Shader.Find("Standard")) { name = materialName };
        
        // Find base texture (island palette)
        string baseTexturePath = texturePaths[materialName];
        string baseTextureFileName = Path.GetFileName(baseTexturePath);
        Texture2D baseTexture = FindTextureInProject(baseTextureFileName);
        
        // Find overlay texture (multi/rock/cliff)
        Texture2D overlayTexture = null;
        foreach (var kvp in texturePaths)
        {
            string overlayMaterial = kvp.Key.ToLower();
            if (overlayMaterial.Contains("multi_") || overlayMaterial.Contains("rock") || overlayMaterial.Contains("cliff"))
            {
                string overlayTextureFileName = Path.GetFileName(kvp.Value);
                overlayTexture = FindTextureInProject(overlayTextureFileName);
                if (overlayTexture != null)
                {
                    DebugLogger.LogEggImporter($"🔥 Found overlay texture for DelFuego: {overlayTextureFileName}");
                    break;
                }
            }
        }
        
        // Set up base texture
        if (baseTexture != null)
        {
            mat.mainTexture = baseTexture;
            baseTexture.wrapMode = TextureWrapMode.Clamp; // Base uses clamp
            DebugLogger.LogEggImporter($"🎨 DelFuego base texture: {baseTexture.name}");
        }
        
        // Set up overlay texture as detail with proper tiling scale
        if (overlayTexture != null)
        {
            overlayTexture.wrapMode = TextureWrapMode.Repeat; // Overlay uses repeat for tiling
            
            if (mat.HasProperty("_DetailAlbedoMap"))
            {
                mat.SetTexture("_DetailAlbedoMap", overlayTexture);
                mat.SetFloat("_DetailNormalMapScale", 1.0f);
                
                // DelFuego analysis: uvNoise range ~55 vs regular UV range ~2.6 = ~21x scaling
                // Increase tiling for more prominent repetition
                Vector2 detailTiling = new Vector2(40.0f, 40.0f); // 40x tiling for more repetition
                mat.SetTextureScale("_DetailAlbedoMap", detailTiling);
                
                // Reduce detail intensity for darker blending instead of brighter
                if (mat.HasProperty("_DetailAlbedoMapScale"))
                    mat.SetFloat("_DetailAlbedoMapScale", 0.5f);
                    
                DebugLogger.LogEggImporter($"🔄 DelFuego overlay texture: {overlayTexture.name} (20x tiling as detail map)");
            }
        }
        
        // Set material properties for better appearance
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0.0f);
        if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", 0.0f);
        if (mat.HasProperty("_SpecColor"))
            mat.SetColor("_SpecColor", Color.black);
        if (mat.HasProperty("_Shininess"))
            mat.SetFloat("_Shininess", 0.0f);
            
        return mat;
    }
    
}