/// <summary>
/// Interface for loading POTCO character data from Python source files.
/// Implemented by both IronPython and Pure C# backends.
/// </summary>
using System.Collections.Generic;
using CharacterOG.Models;

namespace CharacterOG.Data
{
    public interface IOgDataSource
    {
        /// <summary>Load all body shape definitions from BodyDefs.py</summary>
        Dictionary<string, BodyShapeDef> LoadBodyShapes(string gender = "m");

        /// <summary>Load color palettes and dye rules from HumanDNA.py</summary>
        Palettes LoadPalettesAndDyeRules();

        /// <summary>Load clothing catalog from ClothingGlobals.py and PirateMale/Female.py</summary>
        ClothingCatalog LoadClothingCatalog(string gender = "m");

        /// <summary>Load jewelry and tattoo definitions from PirateMale/Female.py</summary>
        JewelryTattooDefs LoadJewelryAndTattoos(string gender = "m");

        /// <summary>Load NPC DNA presets from NPCList.py</summary>
        Dictionary<string, PirateDNA> LoadNpcDna();

        /// <summary>Backend name for debugging/selection</summary>
        string BackendName { get; }

        /// <summary>Check if backend is available/initialized</summary>
        bool IsAvailable { get; }
    }
}
