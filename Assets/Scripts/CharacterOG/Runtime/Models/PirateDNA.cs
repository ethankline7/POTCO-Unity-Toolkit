/// <summary>
/// Complete pirate DNA specification.
/// Loaded from NPCList.py or created procedurally.
/// </summary>
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterOG.Models
{
    [Serializable]
    public class PirateDNA
    {
        // Identity
        public string name = "Pirate";
        public string gender = "m"; // "m" or "f"

        // Body shape
        public string bodyShape = "MaleIdeal";
        public float bodyHeight = 0.5f;
        public int skinColorIdx = 0;

        // Clothing indices (OG indices)
        public int hat = 0;
        public int shirt = 0;
        public int vest = 0;
        public int coat = 0;
        public int belt = 0;
        public int pants = 0;
        public int shoes = 0;

        // Clothing texture indices
        public int hatTex = 0;
        public int shirtTex = 0;
        public int vestTex = 0;
        public int coatTex = 0;
        public int beltTex = 0;
        public int pantsTex = 0;
        public int shoesTex = 0;

        // Clothing color indices
        public int topColorIdx = 0;
        public int botColorIdx = 0;
        public int hatColorIdx = 0;

        // Hair/facial hair
        public int hair = 0;
        public int beard = 0;
        public int mustache = 0;
        public int hairColorIdx = 0;

        // Head/face
        public int headTexture = 0;
        public int eyeColorIdx = 0;

        // Jewelry (zoneName → index)
        public Dictionary<string, int> jewelry = new();

        // Tattoos
        public List<TattooSpec> tattoos = new();

        // Additional head morph sliders (optional for future expansion)
        public Dictionary<string, float> headMorphs = new();

        public PirateDNA() { }

        public PirateDNA(string name, string gender)
        {
            this.name = name;
            this.gender = gender;
        }

        /// <summary>Clone this DNA</summary>
        public PirateDNA Clone()
        {
            var clone = new PirateDNA
            {
                name = name,
                gender = gender,
                bodyShape = bodyShape,
                bodyHeight = bodyHeight,
                skinColorIdx = skinColorIdx,
                hat = hat,
                shirt = shirt,
                vest = vest,
                coat = coat,
                belt = belt,
                pants = pants,
                shoes = shoes,
                hatTex = hatTex,
                shirtTex = shirtTex,
                vestTex = vestTex,
                coatTex = coatTex,
                beltTex = beltTex,
                pantsTex = pantsTex,
                shoesTex = shoesTex,
                topColorIdx = topColorIdx,
                botColorIdx = botColorIdx,
                hatColorIdx = hatColorIdx,
                hair = hair,
                beard = beard,
                mustache = mustache,
                hairColorIdx = hairColorIdx,
                headTexture = headTexture,
                eyeColorIdx = eyeColorIdx,
                jewelry = new Dictionary<string, int>(jewelry),
                tattoos = new List<TattooSpec>(tattoos),
                headMorphs = new Dictionary<string, float>(headMorphs)
            };

            return clone;
        }

        /// <summary>Export DNA to JSON string</summary>
        public string ToJson(bool prettyPrint = true)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }

        /// <summary>Import DNA from JSON string</summary>
        public static PirateDNA FromJson(string json)
        {
            return JsonUtility.FromJson<PirateDNA>(json);
        }

        /// <summary>Save DNA to file</summary>
        public void SaveToFile(string filePath)
        {
            System.IO.File.WriteAllText(filePath, ToJson(true));
        }

        /// <summary>Load DNA from file</summary>
        public static PirateDNA LoadFromFile(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
                return null;

            string json = System.IO.File.ReadAllText(filePath);
            return FromJson(json);
        }
    }
}
