using System.IO;
using System.Linq;
using UnityEngine;
using POTCO.Editor;

public static class LODFilteringUtility
{
    public static bool ShouldImportHighestLODOnly(string fileName)
    {
        DebugLogger.LogEggImporter($"🔍 Checking LOD for file: {fileName}");
        
        // Handle character LODs: _hi/_high, _med/_medium, _low, _super/_superlow (super/superlow is lowest quality)
        if (fileName.EndsWith("_hi") || fileName.EndsWith("_high"))
        {
            DebugLogger.LogEggImporter($"✅ Importing character hi/high LOD: {fileName}");
            return true; // Always import highest quality
        }
        else if (fileName.EndsWith("_med") || fileName.EndsWith("_medium") || fileName.EndsWith("_low") || fileName.EndsWith("_super") || fileName.EndsWith("_superlow"))
        {
            // Check if a higher quality version exists
            string baseName = fileName;
            if (fileName.EndsWith("_med")) baseName = fileName.Substring(0, fileName.LastIndexOf("_med"));
            else if (fileName.EndsWith("_medium")) baseName = fileName.Substring(0, fileName.LastIndexOf("_medium"));
            else if (fileName.EndsWith("_low")) baseName = fileName.Substring(0, fileName.LastIndexOf("_low"));
            else if (fileName.EndsWith("_super")) baseName = fileName.Substring(0, fileName.LastIndexOf("_super"));
            else if (fileName.EndsWith("_superlow")) baseName = fileName.Substring(0, fileName.LastIndexOf("_superlow"));
            
            // Check if _hi or _high version exists (prefer _hi over _high)
            string hiVersion = baseName + "_hi.egg";
            string highVersion = baseName + "_high.egg";
            string[] hiFiles = System.IO.Directory.GetFiles(Application.dataPath, hiVersion, System.IO.SearchOption.AllDirectories);
            string[] highFiles = System.IO.Directory.GetFiles(Application.dataPath, highVersion, System.IO.SearchOption.AllDirectories);
            
            if (hiFiles.Length > 0)
            {
                DebugLogger.LogEggImporter($"🚫 Skipping {fileName} - higher quality version exists: {baseName}_hi");
                return false;
            }
            else if (highFiles.Length > 0)
            {
                DebugLogger.LogEggImporter($"🚫 Skipping {fileName} - higher quality version exists: {baseName}_high");
                return false;
            }
        }
        
        // Handle simple numeric LODs: model_1000, model_2000, etc.
        var numericMatch = System.Text.RegularExpressions.Regex.Match(fileName, @"(.+)_(\d+)$");
        if (numericMatch.Success)
        {
            string baseName = numericMatch.Groups[1].Value;
            int currentLOD = int.Parse(numericMatch.Groups[2].Value);
            
            DebugLogger.LogEggImporter($"🔍 Found numeric LOD: {fileName} (base: '{baseName}', number: {currentLOD})");
            
            // Find all numeric variants for this model
            string[] allFiles = System.IO.Directory.GetFiles(Application.dataPath, "*.egg", System.IO.SearchOption.AllDirectories);
            
            int highestLOD = currentLOD;
            foreach (string file in allFiles)
            {
                string fileNameOnly = System.IO.Path.GetFileNameWithoutExtension(file).ToLower();
                var fileMatch = System.Text.RegularExpressions.Regex.Match(fileNameOnly, @"(.+)_(\d+)$");
                if (fileMatch.Success && fileMatch.Groups[1].Value == baseName)
                {
                    int fileLOD = int.Parse(fileMatch.Groups[2].Value);
                    if (fileLOD > highestLOD)
                    {
                        highestLOD = fileLOD;
                        DebugLogger.LogEggImporter($"🔍 Found higher LOD: {baseName}_{fileLOD}");
                    }
                }
            }
            
            if (currentLOD < highestLOD)
            {
                DebugLogger.LogEggImporter($"🚫 Skipping {fileName} - higher numeric LOD exists: {baseName}_{highestLOD}");
                return false;
            }
            else
            {
                DebugLogger.LogEggImporter($"✅ Importing highest numeric LOD: {fileName}");
            }
        }
        
        return true; // Import if no higher LOD found
    }
}