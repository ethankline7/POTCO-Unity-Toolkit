using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Toontown.Editor.Validation
{
    public static class ToontownTextureRepairRunner
    {
        [MenuItem("Toontown/Validation/Repair RGB Textures + Rebuild DNA Demo")]
        public static void Run()
        {
            RunInternal(exitOnFinish: false);
        }

        // Used by batch mode:
        // -executeMethod Toontown.Editor.Validation.ToontownTextureRepairRunner.RunBatch
        public static void RunBatch()
        {
            RunInternal(exitOnFinish: true);
        }

        private static void RunInternal(bool exitOnFinish)
        {
            try
            {
                int reimported = ReimportRgbTextures();
                global::MaterialHandler.InvalidateTextureCache();
                Debug.Log($"Reimported {reimported} SGI .rgb textures. Rebuilding Toontown DNA demo scene...");

                ToontownDnaMvpDemoRunner.Run();
                ExitBatch(0, exitOnFinish);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Toontown texture repair failed: {ex}");
                ExitBatch(1, exitOnFinish);
            }
        }

        private static int ReimportRgbTextures()
        {
            string resourcesRoot = Path.Combine(Application.dataPath, "Resources");
            if (!Directory.Exists(resourcesRoot))
            {
                return 0;
            }

            string[] rgbFiles = Directory.GetFiles(resourcesRoot, "*.rgb", SearchOption.AllDirectories);
            int imported = 0;
            for (int i = 0; i < rgbFiles.Length; i++)
            {
                string fullPath = rgbFiles[i].Replace('\\', '/');
                if (!fullPath.StartsWith(Application.dataPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relativePart = fullPath.Substring(Application.dataPath.Length).TrimStart('\\', '/');
                string assetPath = ("Assets/" + relativePart).Replace('\\', '/');

                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                imported++;

                if (imported % 200 == 0)
                {
                    Debug.Log($"Reimported {imported}/{rgbFiles.Length} .rgb textures...");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return imported;
        }

        private static void ExitBatch(int exitCode, bool exitOnFinish)
        {
            if (!Application.isBatchMode || !exitOnFinish)
            {
                return;
            }

            EditorApplication.Exit(exitCode);
        }
    }
}
