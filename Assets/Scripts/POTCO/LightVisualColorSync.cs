using UnityEngine;

namespace POTCO
{
    /// <summary>
    /// Automatically syncs ObjectListInfo's Visual Color with the Light component's color
    /// This ensures lights always have their visual color matching their light emission
    /// </summary>
    [RequireComponent(typeof(Light))]
    [ExecuteAlways] // Run in both edit and play mode
    public class LightVisualColorSync : MonoBehaviour
    {
        private Light lightComponent;
        private ObjectListInfo objectListInfo;
        private VisualColorHandler visualColorHandler;
        private Color lastLightColor;

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
            SyncColors();
        }

        private void Initialize()
        {
            if (lightComponent == null)
                lightComponent = GetComponent<Light>();

            if (objectListInfo == null)
            {
                objectListInfo = GetComponent<ObjectListInfo>();
                if (objectListInfo == null)
                {
                    objectListInfo = gameObject.AddComponent<ObjectListInfo>();
                }
            }

            if (visualColorHandler == null)
            {
                visualColorHandler = GetComponent<VisualColorHandler>();
                if (visualColorHandler == null)
                {
                    visualColorHandler = gameObject.AddComponent<VisualColorHandler>();
                }
            }

            // Ensure object type is set correctly for lights
            if (objectListInfo != null && string.IsNullOrEmpty(objectListInfo.objectType))
            {
                objectListInfo.objectType = "Light - Dynamic";
            }
        }

        private void Update()
        {
            // Skip for preview objects
            if (gameObject.name.Contains("[SURFACE_PREVIEW]") ||
                gameObject.name.Contains("[PREVIEW]"))
            {
                return;
            }

            // Check if light color changed
            if (lightComponent != null && lightComponent.color != lastLightColor)
            {
                SyncColors();
            }
        }

        /// <summary>
        /// Sync the visual color with the light color
        /// </summary>
        public void SyncColors()
        {
            if (lightComponent == null || objectListInfo == null)
            {
                Initialize();
            }

            if (lightComponent != null && objectListInfo != null)
            {
                // Always set visual color to match light color
                objectListInfo.visualColor = lightComponent.color;
                lastLightColor = lightComponent.color;

                // Refresh the visual color handler
                if (visualColorHandler != null)
                {
                    visualColorHandler.RefreshVisualColor();
                }

#if UNITY_EDITOR
                // Mark as dirty in editor to ensure serialization
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorUtility.SetDirty(this);
                    UnityEditor.EditorUtility.SetDirty(objectListInfo);
                    if (visualColorHandler != null)
                    {
                        UnityEditor.EditorUtility.SetDirty(visualColorHandler);
                    }
                }
#endif

                Debug.Log($"💡 Synced Visual Color for light '{gameObject.name}': {lightComponent.color}");
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Force sync when values change in inspector
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null)
                    {
                        SyncColors();
                    }
                };
            }
        }

        /// <summary>
        /// Reset is called when component is first added or reset
        /// </summary>
        private void Reset()
        {
            Initialize();
            SyncColors();
        }
#endif
    }
}