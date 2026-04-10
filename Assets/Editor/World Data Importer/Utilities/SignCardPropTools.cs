using POTCO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WorldDataImporter.Utilities
{
    public static class SignCardPropTools
    {
        [MenuItem("POTCO/World Data/Signs/Show 2D Card Props")]
        public static void Show2DCardProps()
        {
            ApplyModeToAll(SignCardPropDisplayMode.Show2DCardProps);
        }

        [MenuItem("POTCO/World Data/Signs/Hide 2D Card Props For Replacement")]
        public static void Hide2DCardProps()
        {
            ApplyModeToAll(SignCardPropDisplayMode.Hide2DCardPropsForReplacement);
        }

        [MenuItem("POTCO/World Data/Signs/Show Replacement Props Only")]
        public static void ShowReplacementPropsOnly()
        {
            ApplyModeToAll(SignCardPropDisplayMode.ShowReplacementPropsOnly);
        }

        private static void ApplyModeToAll(SignCardPropDisplayMode mode)
        {
            SignCardPropController[] controllers = Object.FindObjectsByType<SignCardPropController>(FindObjectsSortMode.None);
            if (controllers == null || controllers.Length == 0)
            {
                Debug.Log("No SignCardPropController components found in the active scene.");
                return;
            }

            int updated = 0;
            for (int i = 0; i < controllers.Length; i++)
            {
                SignCardPropController controller = controllers[i];
                if (controller == null)
                {
                    continue;
                }

                Undo.RecordObject(controller, "Update Sign Card Prop Mode");
                controller.SetDisplayMode(mode);
                EditorUtility.SetDirty(controller);
                updated++;
            }

            if (updated > 0)
            {
                EditorSceneManager.MarkAllScenesDirty();
            }

            Debug.Log($"Updated {updated} sign card controller(s) to mode '{mode}'.");
        }
    }
}
