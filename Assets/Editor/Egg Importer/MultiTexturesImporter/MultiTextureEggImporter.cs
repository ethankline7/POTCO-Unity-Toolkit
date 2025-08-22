using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using POTCO.Editor;
using System.IO;

// UV Transform matrix for per-texture coordinate transformations
public struct UVXform 
{
    public float m00, m01, m02; // first row
    public float m10, m11, m12; // second row
    
    public static UVXform Identity => new UVXform { m00 = 1, m11 = 1 };
    
    public static UVXform Mul(UVXform a, UVXform b) => new UVXform {
        m00 = a.m00 * b.m00 + a.m01 * b.m10,
        m01 = a.m00 * b.m01 + a.m01 * b.m11,
        m02 = a.m00 * b.m02 + a.m01 * b.m12 + a.m02,
        m10 = a.m10 * b.m00 + a.m11 * b.m10,
        m11 = a.m10 * b.m01 + a.m11 * b.m11,
        m12 = a.m10 * b.m02 + a.m11 * b.m12 + a.m12,
    };
    
    public static UVXform Translate(float u, float v) => new UVXform { m00 = 1, m11 = 1, m02 = u, m12 = v };
    public static UVXform Scale(float su, float sv) => new UVXform { m00 = su, m11 = sv };
    public static UVXform Rotate(float deg) {
        float r = deg * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new UVXform { m00 = c, m01 = -s, m10 = s, m11 = c };
    }
    
    public static Vector2 Apply(UVXform m, Vector2 uv)
        => new Vector2(m.m00 * uv.x + m.m01 * uv.y + m.m02,
                       m.m10 * uv.x + m.m11 * uv.y + m.m12);
}

public class MultiTextureEggImporter
{
    private MultiTextureParserUtilities _parserUtils;
    private MultiTextureGeometryProcessor _geometryProcessor;
    private MultiTextureAnimationProcessor _animationProcessor;
    private MultiTextureMaterialHandler _materialHandler;
    
    // Add main components for alpha parsing
    private ParserUtilities _mainParserUtils;
    private GeometryProcessor _mainGeometryProcessor;
    private MaterialHandler _mainMaterialHandler;
    
    
    // Fields exactly like the reference implementation
    private List<Material> _materials;
    private Dictionary<string, Material> _materialDict;
    private Vector3[] _masterVertices;
    private Vector3[] _masterNormals;
    private Vector2[] _masterUVs;
    private Color[] _masterColors;
    private Dictionary<string, EggJoint> _joints;
    private EggJoint _rootJoint;
    private bool _hasSkeletalData = false;
    private GameObject _rootBoneObject;
    
    // UV Transform system for per-texture transforms and named UV sets
    private Dictionary<string, string> _textureToUVSet = new Dictionary<string, string>();
    private Dictionary<string, UVXform> _textureUVXform = new Dictionary<string, UVXform>();
    private Dictionary<string, string> _textureWrapU = new Dictionary<string, string>();
    private Dictionary<string, string> _textureWrapV = new Dictionary<string, string>();
    
    // Public access for geometry processor
    public Dictionary<string, string> TextureToUVSet => _textureToUVSet;
    public Dictionary<string, UVXform> TextureUVXform => _textureUVXform;
    public Dictionary<string, string> TextureWrapU => _textureWrapU;
    public Dictionary<string, string> TextureWrapV => _textureWrapV;
    
    public MultiTextureEggImporter()
    {
        _parserUtils = new MultiTextureParserUtilities();
        _geometryProcessor = new MultiTextureGeometryProcessor();
        _animationProcessor = new MultiTextureAnimationProcessor();
        _materialHandler = new MultiTextureMaterialHandler();
        
        // Initialize main components for alpha parsing
        _mainParserUtils = new ParserUtilities();
        _mainGeometryProcessor = new GeometryProcessor();
        _mainMaterialHandler = new MaterialHandler();
        
    }
    
    public void ImportEggFile(string[] lines, GameObject rootGO, UnityEditor.AssetImporters.AssetImportContext ctx)
    {
        DebugLogger.LogEggImporter($"🔥 Starting multi-texture EGG import: {rootGO.name}");
        
        try
        {
            if (lines == null || lines.Length == 0)
            {
                DebugLogger.LogErrorEggImporter($"Failed to read EGG file or file is empty");
                return;
            }
            
            // Use the exact same approach as the reference HandleGeometryFile
            HandleGeometryFile(lines, rootGO, ctx);
            
            DebugLogger.LogEggImporter($"✅ Multi-texture EGG import completed: {rootGO.name}");
        }
        catch (System.Exception e)
        {
            DebugLogger.LogErrorEggImporter($"Error during multi-texture EGG import: {e.Message}");
            DebugLogger.LogErrorEggImporter($"Stack trace: {e.StackTrace}");
            throw; // Re-throw to let the main importer handle cleanup
        }
    }
    
    // Copy the exact HandleGeometryFile method from the reference implementation
    private void HandleGeometryFile(string[] lines, GameObject rootGO, UnityEditor.AssetImporters.AssetImportContext ctx)
    {
        DebugLogger.LogEggImporter("Processing geometry EGG file");
        // --- Pass 1: Parse all raw data into memory ---
        // Pre-size collections based on typical EGG file contents
        var vertexPool = new List<EggVertex>(1024); // Typical vertex count estimate
        var texturePaths = new Dictionary<string, string>(16); // Typical texture count
        var alphaPaths = new Dictionary<string, string>(16); // Alpha texture paths
        _joints = new Dictionary<string, EggJoint>(32); // Typical joint count
        
        // Parse textures with MultiTexture system (for UV mappings and multi-texture support)
        ParseAllTexturesAndVertices(lines, vertexPool, texturePaths);
        
        // Also parse alpha paths using main GeometryProcessor (just for alpha-file entries)
        ParseAlphaTextures(lines, texturePaths, alphaPaths);
        DebugLogger.LogEggImporter($"Parsed {vertexPool.Count} vertices and {texturePaths.Count} textures");
        ParseAllJoints(lines);
        DebugLogger.LogEggImporter($"Parsed {_joints.Count} joints, hasSkeletalData: {_hasSkeletalData}");
        PopulateJointWeightsFromVertices(vertexPool);
        // Calculate UV bounds for automatic texture scaling
        Vector4 uvBounds = _geometryProcessor.CalculateUVBounds(vertexPool);
        CreateMasterVertexBuffer(vertexPool);
        if (_hasSkeletalData && _rootJoint != null)
        {
            _rootBoneObject = new GameObject("Armature");
            _rootBoneObject.transform.SetParent(rootGO.transform, false);
            try
            {
                CreateBoneHierarchy(_rootBoneObject.transform, _rootJoint);
                DebugBoneHierarchy(_rootBoneObject.transform);
            }
            catch (System.Exception e)
            {
                DebugLogger.LogWarningEggImporter($"Failed to create bone hierarchy: {e.Message}. Falling back to static mesh.");
                _hasSkeletalData = false;
                if (_rootBoneObject != null)
                {
                    Object.DestroyImmediate(_rootBoneObject);
                    _rootBoneObject = null;
                }
            }
        }
        // --- Pass 2: Build Hierarchy and Map Geometry ---
        // Pre-size dictionaries based on typical EGG hierarchy complexity
        var geometryMap = new Dictionary<string, GeometryData>(64);
        var hierarchyMap = new Dictionary<string, Transform>(64);
        hierarchyMap[""] = rootGO.transform; // Root path
        BuildHierarchyAndMapGeometry(lines, 0, lines.Length, "", hierarchyMap, geometryMap);
        DebugLogger.LogEggImporter($"Built hierarchy with {hierarchyMap.Count} objects and {geometryMap.Count} geometry groups");
        
        // Collect all unique material names for multi-texture support
        var allMaterialNames = new HashSet<string>();
        foreach (var kvp in geometryMap)
        {
            DebugLogger.LogEggImporter($"Geometry group '{kvp.Key}' has {kvp.Value.subMeshes.Count} submeshes");
            foreach (string matName in kvp.Value.materialNames)
            {
                allMaterialNames.Add(matName);
            }
        }
        
        // SMART MATERIAL CREATION: Use multi-texture for DelFuego-pattern models, simple for others
        bool hasMultiTextureMaterials = allMaterialNames.Any(name => name.Contains("||"));
        
        if (hasMultiTextureMaterials)
        {
            DebugLogger.LogEggImporter("🔥 Detected multi-texture materials - using DelFuego overlay system");
            _materials = CreateMaterialsWithMultiTexture(texturePaths, allMaterialNames.ToList(), rootGO, Vector4.zero);
            // Apply alpha textures to DelFuego materials after creation
            ApplyAlphaTexturesToMaterials(_materials, alphaPaths);
        }
        else
        {
            DebugLogger.LogEggImporter("📦 Using unified material creation with alpha support");
            _materials = CreateMaterialsWithAlpha(texturePaths, alphaPaths, rootGO);
        }
        
        // Use optimized material dictionary creation from MaterialHandler
        _materialDict = _materialHandler.CreateMaterialDictionary(_materials);
        // --- Pass 3: Create Meshes from Mapped Geometry ---
        foreach (var kvp in geometryMap)
        {
            string path = kvp.Key;
            GeometryData geo = kvp.Value;
            if (hierarchyMap.TryGetValue(path, out Transform targetTransform))
            {
                CreateMeshForGameObject(targetTransform.gameObject, geo.subMeshes, geo.materialNames, ctx);
            }
        }
        // --- Pass 4: Parse and create animations ---
        ParseAnimations(lines, rootGO, ctx);
    }
    
    // Copy all the helper methods from reference implementation
    private void ParseAllTexturesAndVertices(string[] lines, List<EggVertex> vertexPool, Dictionary<string, string> texturePaths)
    {
        _geometryProcessor.ParseAllTexturesAndVertices(lines, vertexPool, texturePaths, _parserUtils, this);
    }

    private List<Material> CreateMaterials(Dictionary<string, string> texturePaths, GameObject rootGO)
    {
        return _materialHandler.CreateMaterialsWithMultiTexture(texturePaths, new List<string>(), rootGO, Vector4.zero, this);
    }
    
    private List<Material> CreateMaterialsWithMultiTexture(Dictionary<string, string> texturePaths, List<string> materialNames, GameObject rootGO, Vector4 uvBounds)
    {
        return _materialHandler.CreateMaterialsWithMultiTexture(texturePaths, materialNames, rootGO, uvBounds, this);
    }
    
    private List<Material> CreateMaterialsWithAlpha(Dictionary<string, string> texturePaths, Dictionary<string, string> alphaPaths, GameObject rootGO)
    {
        return _mainMaterialHandler.CreateMaterials(texturePaths, alphaPaths, rootGO);
    }
    
    private void ParseAlphaTextures(string[] lines, Dictionary<string, string> texturePaths, Dictionary<string, string> alphaPaths)
    {
        // Use main GeometryProcessor to extract just the alpha-file entries
        var dummyVertexPool = new List<EggVertex>();
        var dummyTexturePaths = new Dictionary<string, string>();
        _mainGeometryProcessor.ParseAllTexturesAndVertices(lines, dummyVertexPool, dummyTexturePaths, alphaPaths, _mainParserUtils);
    }
    
    private void ApplyAlphaTexturesToMaterials(List<Material> materials, Dictionary<string, string> alphaPaths)
    {
        if (alphaPaths.Count == 0) return;
        
        foreach (var material in materials)
        {
            if (alphaPaths.TryGetValue(material.name, out string alphaPath))
            {
                // Find the alpha texture
                string alphaFileName = Path.GetFileName(alphaPath);
                Texture2D alphaTex = FindTextureInProject(alphaFileName);
                if (!alphaTex) alphaTex = LoadTextureByFileName(alphaFileName);
                
                if (alphaTex)
                {
                    material.SetTexture("_AlphaTex", alphaTex);
                    if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.1f);
                    DebugLogger.LogEggImporter($"[AlphaMask] Applied alpha texture to DelFuego material {material.name}: {alphaTex.name}");
                }
                else
                {
                    DebugLogger.LogWarningEggImporter($"[AlphaMask] Could not find alpha texture for DelFuego material {material.name}: {alphaFileName}");
                }
            }
        }
    }
    
    // Helper methods from MaterialHandler for texture loading
    private Texture2D FindTextureInProject(string textureFileName)
    {
        string[] guids = UnityEditor.AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(textureFileName) + " t:texture2D");
        
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            string foundFileName = Path.GetFileName(path);
            
            if (foundFileName.Equals(textureFileName, System.StringComparison.OrdinalIgnoreCase))
            {
                Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null) return texture;
            }
        }
        return null;
    }

    private Texture2D LoadTextureByFileName(string fileName)
    {
        var guids = UnityEditor.AssetDatabase.FindAssets($"{Path.GetFileNameWithoutExtension(fileName)} t:Texture2D");
        foreach (var g in guids)
        {
            var p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
            if (string.Equals(Path.GetFileName(p), fileName, System.StringComparison.OrdinalIgnoreCase))
                return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }
        return null;
    }

    private void CreateMasterVertexBuffer(List<EggVertex> vertexPool)
    {
        _geometryProcessor.CreateMasterVertexBuffer(vertexPool, out _masterVertices, out _masterNormals, out _masterUVs, out _masterColors);
    }

    private void CreateBoneHierarchy(Transform parent, EggJoint joint)
    {
        _geometryProcessor.CreateBoneHierarchy(parent, joint);
    }

    private void BuildHierarchyAndMapGeometry(string[] lines, int start, int end, string currentPath, Dictionary<string, Transform> hierarchyMap, Dictionary<string, GeometryData> geometryMap)
    {
        _geometryProcessor.BuildHierarchyAndMapGeometry(lines, start, end, currentPath, hierarchyMap, geometryMap);
    }

    private void CreateMeshForGameObject(GameObject go, Dictionary<string, List<int>> subMeshes, List<string> materialNames, UnityEditor.AssetImporters.AssetImportContext ctx)
    {
        _geometryProcessor.CreateMeshForGameObject(go, subMeshes, materialNames, ctx,
            _masterVertices, _masterNormals, _masterUVs, _masterColors, _materialDict,
            _hasSkeletalData, _rootJoint, _rootBoneObject, _joints, this);
    }

    private void ParseAnimations(string[] lines, GameObject rootGO, UnityEditor.AssetImporters.AssetImportContext ctx)
    {
        _animationProcessor.ParseAnimations(lines, rootGO, ctx, _rootBoneObject);
    }

    private void DebugBoneHierarchy(Transform bone, string indent = "")
    {
        DebugLogger.LogEggImporter($"{indent}Bone: {bone.name}");
        for (int i = 0; i < bone.childCount; i++)
        {
            DebugBoneHierarchy(bone.GetChild(i), indent + "  ");
        }
    }

    private void ParseAllJoints(string[] lines)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.StartsWith("<Joint>"))
            {
                var joint = _geometryProcessor.ParseJoint(lines, ref i, _joints, _parserUtils);
                if (joint != null)
                {
                    _joints[joint.name] = joint;
                    if (joint.parent == null) _rootJoint = joint;
                    _hasSkeletalData = true;
                }
            }
        }
    }

    private void PopulateJointWeightsFromVertices(List<EggVertex> vertexPool)
    {
        for (int i = 0; i < vertexPool.Count; i++)
        {
            var vertex = vertexPool[i];
            foreach (var kvp in vertex.boneWeights)
            {
                if (_joints.TryGetValue(kvp.Key, out EggJoint joint))
                {
                    joint.vertexWeights[i] = kvp.Value;
                }
            }
        }
    }
    
    /// <summary>
    /// Gets the materials created during import for adding to AssetImportContext
    /// </summary>
    public List<Material> GetMaterials()
    {
        return _materials;
    }
    
    /// <summary>
    /// Detects if an EGG file contains multi-texture patterns that require specialized processing
    /// </summary>
    public static bool RequiresMultiTextureProcessing(string eggFilePath)
    {
        try
        {
            string[] lines = File.ReadAllLines(eggFilePath);
            
            // Look for specific multi-texture indicator texture
            foreach (string line in lines)
            {
                if (line.Contains("pir_t_are_isl_multi_"))
                {
                    DebugLogger.LogEggImporter($"🔥 Multi-texture model detected (pir_t_are_isl_multi_): {Path.GetFileName(eggFilePath)}");
                    return true;
                }
            }
        }
        catch (System.Exception e)
        {
            DebugLogger.LogWarningEggImporter($"Error checking multi-texture requirements: {e.Message}");
        }
        
        return false;
    }
}