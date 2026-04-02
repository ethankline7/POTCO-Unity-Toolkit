using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Toolkit.Editor.WorldData.Adapters.Toontown
{
    public sealed class ToontownObjectTypeMapper
    {
        private const string RelativeConfigPath = "Assets/Editor/Toontown/Config/ObjectTypeMap.json";
        private readonly ObjectTypeMapConfig config;

        private ToontownObjectTypeMapper(ObjectTypeMapConfig config)
        {
            this.config = config ?? CreateDefaultConfig();
        }

        public static ToontownObjectTypeMapper LoadOrCreateDefault(out string loadWarning)
        {
            loadWarning = null;
            string fullPath = GetFullConfigPath();

            if (!File.Exists(fullPath))
            {
                EnsureConfigDirectoryExists(fullPath);
                var defaultConfig = CreateDefaultConfig();
                File.WriteAllText(fullPath, JsonUtility.ToJson(defaultConfig, true));
                loadWarning = $"Type map config not found. Created default config at {RelativeConfigPath}.";
                return new ToontownObjectTypeMapper(defaultConfig);
            }

            try
            {
                string json = File.ReadAllText(fullPath);
                var parsed = JsonUtility.FromJson<ObjectTypeMapConfig>(json);
                if (parsed == null)
                {
                    loadWarning = $"Type map config could not be parsed. Falling back to defaults ({RelativeConfigPath}).";
                    return new ToontownObjectTypeMapper(CreateDefaultConfig());
                }

                if (parsed.rules == null)
                {
                    parsed.rules = new List<ObjectTypeRule>();
                }

                if (string.IsNullOrWhiteSpace(parsed.defaultType))
                {
                    parsed.defaultType = "Unknown";
                }

                return new ToontownObjectTypeMapper(parsed);
            }
            catch (Exception ex)
            {
                loadWarning = $"Type map load failed: {ex.Message}. Falling back to defaults.";
                return new ToontownObjectTypeMapper(CreateDefaultConfig());
            }
        }

        public string InferTypeFromModel(string modelPath, out bool usedDefault)
        {
            usedDefault = true;

            if (string.IsNullOrWhiteSpace(modelPath))
            {
                return config.defaultType;
            }

            for (int i = 0; i < config.rules.Count; i++)
            {
                var rule = config.rules[i];
                if (rule == null || string.IsNullOrWhiteSpace(rule.modelContains) || string.IsNullOrWhiteSpace(rule.type))
                {
                    continue;
                }

                if (modelPath.IndexOf(rule.modelContains, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    usedDefault = false;
                    return rule.type.Trim();
                }
            }

            return config.defaultType;
        }

        private static string GetFullConfigPath()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), RelativeConfigPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void EnsureConfigDirectoryExists(string fullPath)
        {
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static ObjectTypeMapConfig CreateDefaultConfig()
        {
            return new ObjectTypeMapConfig
            {
                defaultType = "Unknown",
                rules = new List<ObjectTypeRule>
                {
                    new ObjectTypeRule { modelContains = "models/props/", type = "Prop" },
                    new ObjectTypeRule { modelContains = "models/buildings/", type = "Building" },
                    new ObjectTypeRule { modelContains = "models/char/", type = "Character" },
                    new ObjectTypeRule { modelContains = "models/streets/", type = "Street" },
                    new ObjectTypeRule { modelContains = "models/neighborhoods/", type = "Neighborhood" }
                }
            };
        }

        [Serializable]
        private sealed class ObjectTypeMapConfig
        {
            public string defaultType = "Unknown";
            public List<ObjectTypeRule> rules = new List<ObjectTypeRule>();
        }

        [Serializable]
        private sealed class ObjectTypeRule
        {
            public string modelContains;
            public string type;
        }
    }
}
