using UnityEngine;

namespace Toolkit.Core
{
    [CreateAssetMenu(fileName = "ToolkitProjectSettings", menuName = "Toolkit/Project Settings")]
    public sealed class ToolkitProjectSettings : ScriptableObject
    {
        public GameFlavor activeGameFlavor = GameFlavor.POTCO;
        public bool enableVerboseLogs;
    }
}