using UnityEngine;
using UnityEditor;

namespace CaveGenerator
{
    /// <summary>
    /// Static class for tracking selected cave connectors for interactive placement
    /// </summary>
    public static class CaveConnectorSelector
    {
        private static Transform selectedConnector;
        private static GameObject selectedCavePiece;

        public static Transform SelectedConnector => selectedConnector;
        public static GameObject SelectedCavePiece => selectedCavePiece;
        public static bool HasSelection => selectedConnector != null;

        public static void SelectConnector(Transform connector)
        {
            selectedConnector = connector;
            selectedCavePiece = connector != null ? GetCavePieceFromConnector(connector) : null;

            if (connector != null)
            {
                Debug.Log($"🔗 Selected connector: {connector.name} on piece {selectedCavePiece?.name}");
            }
            else
            {
                Debug.Log($"❌ Deselected connector");
            }

            SceneView.RepaintAll();
        }

        public static void ClearSelection()
        {
            selectedConnector = null;
            selectedCavePiece = null;
            SceneView.RepaintAll();
        }

        private static GameObject GetCavePieceFromConnector(Transform connector)
        {
            Transform current = connector;
            while (current != null)
            {
                // Look for cave piece wrapper or the root piece
                if (current.name.Contains("CavePiece_") || current.name.Contains("pir_m_are_cav"))
                {
                    return current.gameObject;
                }
                current = current.parent;
            }
            return connector.gameObject;
        }
    }
}
