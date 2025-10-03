using UnityEngine;
using UnityEditor;

namespace POTCO.ShipBuilder.SceneEditing
{
    /// <summary>
    /// Static class for tracking selected ship components for interactive editing
    /// </summary>
    public static class ShipComponentSelector
    {
        private static GameObject selectedComponent;
        private static string selectedLocatorName;
        private static ComponentType selectedType;

        public enum ComponentType
        {
            Unknown,
            Mast,
            Cannon,
            Wheel,
            Ram,
            Bowsprit,
            RepairSpot
        }

        public static GameObject SelectedComponent => selectedComponent;
        public static string SelectedLocatorName => selectedLocatorName;
        public static ComponentType SelectedType => selectedType;
        public static bool HasSelection => selectedComponent != null;

        public static void SelectComponent(GameObject component)
        {
            // If we're selecting a different component while in preview mode, clear the preview first
            if (selectedComponent != component && selectedComponent != null)
            {
                // Clear any active preview from the previous selection
                POTCO.ShipBuilder.SceneEditing.ShipComponentPreview.ClearPreview();
            }

            selectedComponent = component;
            selectedLocatorName = DetectLocatorName(component);
            selectedType = DetectComponentType(component);

            if (component != null)
            {
                string parentInfo = component.transform.parent != null ? component.transform.parent.name : "No Parent";
                Debug.Log($"🔗 Selected ship component: {component.name}");
                Debug.Log($"  📍 Parent: {parentInfo}");
                Debug.Log($"  🏷️ Locator Name: {selectedLocatorName}");
                Debug.Log($"  🎯 Component Type: {selectedType}");
                Debug.Log($"  🔧 Prefix: {GetComponentPrefix()}");
            }
            else
            {
                Debug.Log($"❌ Deselected ship component");
            }

            SceneView.RepaintAll();
        }

        public static void ClearSelection()
        {
            // Clear any active preview when clearing selection
            POTCO.ShipBuilder.SceneEditing.ShipComponentPreview.ClearPreview();

            selectedComponent = null;
            selectedLocatorName = null;
            selectedType = ComponentType.Unknown;
            SceneView.RepaintAll();
        }

        private static string DetectLocatorName(GameObject component)
        {
            if (component == null) return null;

            // Check if the component's parent is a ship part category
            Transform parent = component.transform.parent;
            if (parent != null)
            {
                // If parent is "Masts", "Cannons", etc., this component's name is the locator
                string parentName = parent.name;
                if (parentName == "Masts" || parentName == "Broadside Cannons (Left)" ||
                    parentName == "Broadside Cannons (Right)" || parentName == "Deck Cannons" ||
                    parentName == "Bowsprits" || parentName == "Ship Parts")
                {
                    return component.name;
                }
            }

            // Otherwise try to detect from component name
            if (component.name.StartsWith("location_"))
            {
                return component.name;
            }

            return component.name;
        }

        private static ComponentType DetectComponentType(GameObject component)
        {
            if (component == null) return ComponentType.Unknown;

            string name = component.name.ToLower();

            // Check by category parent FIRST (most reliable)
            Transform parent = component.transform.parent;
            if (parent != null)
            {
                string parentName = parent.name;
                if (parentName == "Masts") return ComponentType.Mast;
                if (parentName.Contains("Cannons")) return ComponentType.Cannon;
                if (parentName == "Bowsprits") return ComponentType.Bowsprit;
                if (parentName == "Ship Parts")
                {
                    if (name.Contains("wheel") || name.Contains("whl")) return ComponentType.Wheel;
                    if (name.Contains("ram")) return ComponentType.Ram;
                    if (name.Contains("repair") || name.Contains("rep")) return ComponentType.RepairSpot;
                }
            }

            // Check by component name patterns (be more specific to avoid false matches)
            // Check for cannon patterns first (cannon_1, cannon_2, etc. or deck_cannon)
            if (name.StartsWith("cannon_") || name.StartsWith("deck_cannon_") ||
                name.StartsWith("broadside_left_") || name.StartsWith("broadside_right_"))
            {
                return ComponentType.Cannon;
            }

            // Check for location-based patterns
            if (name.Contains("mast")) return ComponentType.Mast;
            if (name.Contains("cannon") || name.Contains("can_")) return ComponentType.Cannon;
            if (name.Contains("wheel") || name.Contains("whl")) return ComponentType.Wheel;
            if (name.Contains("ram")) return ComponentType.Ram;
            if (name.Contains("bowsprit") || name.Contains("prow")) return ComponentType.Bowsprit;
            if (name.Contains("repair") || name.Contains("rep")) return ComponentType.RepairSpot;

            return ComponentType.Unknown;
        }

        public static string GetComponentPrefix()
        {
            switch (selectedType)
            {
                case ComponentType.Mast:
                    return "pir_r_shp_mst_";
                case ComponentType.Cannon:
                    // Determine if broadside or deck based on locator name or parent name
                    if (selectedLocatorName != null &&
                        (selectedLocatorName.Contains("deck") || selectedLocatorName.StartsWith("cannon_")))
                    {
                        return "pir_r_shp_can_deck_";
                    }

                    // Check parent name as well
                    if (selectedComponent != null && selectedComponent.transform.parent != null)
                    {
                        string parentName = selectedComponent.transform.parent.name;
                        if (parentName.Contains("Deck"))
                            return "pir_r_shp_can_deck_";
                    }

                    return "pir_r_shp_can_broadside_";
                case ComponentType.Wheel:
                    return "pir_m_shp_prt_wheel";
                case ComponentType.Ram:
                    return "pir_m_shp_ram_";
                case ComponentType.Bowsprit:
                    return "prow_";
                case ComponentType.RepairSpot:
                    return "repair_spot_";
                default:
                    return "";
            }
        }
    }
}
