using System.Collections.Generic;

namespace WorldDataImporter.Data
{
    /// <summary>
    /// Data class holding creature information parsed from POTCO source files
    /// </summary>
    public class CreatureData
    {
        public string species;              // Species name (e.g., "Chicken", "Dog", "Pig")
        public string modelPathHi;          // High-res model path (e.g., "models/char/chicken_hi")
        public string modelPathLo;          // Low-res model path (e.g., "models/char/chicken_")
        public Dictionary<string, string> animations; // Animation name → animation file mapping
        public Dictionary<string, List<(string animName, float playRate)>> animStates; // State → animation sequences

        public CreatureData(string species)
        {
            this.species = species;
            this.animations = new Dictionary<string, string>();
            this.animStates = new Dictionary<string, List<(string, float)>>();
        }

        /// <summary>
        /// Get the best model path to use (tries hi-res first, falls back to low-res)
        /// </summary>
        public string GetBestModelPath()
        {
            // Try hi-res first, fall back to low-res if not available
            if (!string.IsNullOrEmpty(modelPathHi))
                return modelPathHi;
            if (!string.IsNullOrEmpty(modelPathLo))
                return modelPathLo;
            return null;
        }

        /// <summary>
        /// Get animation file for a given animation name
        /// </summary>
        public string GetAnimationFile(string animName)
        {
            if (animations.TryGetValue(animName, out string animFile))
                return animFile;
            return null;
        }

        /// <summary>
        /// Get animation sequence for a given state (e.g., "LandRoam", "WaterRoam")
        /// </summary>
        public List<(string animName, float playRate)> GetAnimationsForState(string state)
        {
            if (animStates.TryGetValue(state, out var anims))
                return anims;
            return null;
        }
    }
}
