using UnityEngine;

namespace CaveGenerator.Data
{
    [System.Serializable]
    public class GenerationSettings
    {
        public int caveLength = 10;
        public float generationDelay = 0.1f;
        public bool capOpenEnds = true;
        public bool forceCapOpenEnds = false;
        public bool useEggFiles = false;
        public int maxBranches = 3;
        public float branchProbability = 0.3f;
        public bool enableBranching = true;
        public int maxDepth = 8;
        public bool allowLoops = false;
        public int seed = -1; // -1 for random
        public bool visualizeConnectors = false;
        public bool realtimePreview = true;
        public bool enableOverlapDetection = true;
        public float overlapTolerance = 0.5f; // Allow slight overlap for seamless mesh touching
        public bool enableBacktracking = true;
        public int maxPrefabRetries = 5; // Try multiple prefabs per connector before giving up
        public int maxBacktrackSteps = 3; // How many pieces to backtrack when stuck
    }
}