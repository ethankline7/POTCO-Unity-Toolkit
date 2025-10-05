/// <summary>
/// Jewelry and tattoo definitions from PirateMale/Female.py.
/// Maps zone names to geometry groups and tattoo target body groups.
/// </summary>
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterOG.Models
{
    [Serializable]
    public class JewelryTattooDefs
    {
        /// <summary>Jewelry zone → list of group names (e.g., "face" → ["acc_face_brow_ring_left", ...])</summary>
        public Dictionary<string, List<string>> jewelryGroupsByZone = new();

        /// <summary>Tattoo zone → target body group names (e.g., "zone2" → ["body_left_forearm", ...])</summary>
        public Dictionary<string, List<string>> tattooZonesToBodyGroups = new();

        /// <summary>Gender-specific jewelry options (m/f → zone → variants)</summary>
        public Dictionary<string, Dictionary<string, List<string>>> genderJewelryOptions = new();

        /// <summary>Get jewelry group names for a zone and gender</summary>
        public List<string> GetJewelryGroups(string zone, string gender = "m")
        {
            string key = $"{gender}_{zone}";
            if (jewelryGroupsByZone.TryGetValue(key, out var groups))
                return groups;

            // Fallback to non-gendered zone
            if (jewelryGroupsByZone.TryGetValue(zone, out groups))
                return groups;

            return new List<string>();
        }

        /// <summary>Get tattoo target body groups for a zone</summary>
        public List<string> GetTattooBodyGroups(string zone)
        {
            if (tattooZonesToBodyGroups.TryGetValue(zone, out var groups))
                return groups;

            return new List<string>();
        }
    }

    /// <summary>
    /// Tattoo specification with UV placement and styling.
    /// Maps to vector_tattoos entries from OG data.
    /// </summary>
    [Serializable]
    public class TattooSpec
    {
        public int zone;
        public int idx;
        public float u;
        public float v;
        public float scale;
        public float rotation;
        public int colorIdx;

        public TattooSpec() { }

        public TattooSpec(int zone, int idx, float u, float v, float scale, float rotation, int colorIdx)
        {
            this.zone = zone;
            this.idx = idx;
            this.u = u;
            this.v = v;
            this.scale = scale;
            this.rotation = rotation;
            this.colorIdx = colorIdx;
        }
    }
}
