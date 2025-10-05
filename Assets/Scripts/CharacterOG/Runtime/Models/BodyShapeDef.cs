/// <summary>
/// Body shape definition from BodyDefs.py.
/// Contains bone scales, offsets, and overall shape parameters.
/// </summary>
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterOG.Models
{
    [Serializable]
    public class BodyShapeDef
    {
        public string name;
        public float headScale = 1f;
        public float bodyScale = 1f;
        public float heightBias = 0f;
        public string frameType;
        public string animType;

        /// <summary>Bone name → scale multiplier (X,Y,Z)</summary>
        public Dictionary<string, Vector3> boneScales = new();

        /// <summary>Bone name → position offset (X,Y,Z) - used for tr_* bones</summary>
        public Dictionary<string, Vector3> boneOffsets = new();

        /// <summary>Body texture names from OG data</summary>
        public List<string> bodyTextures = new();

        /// <summary>Head position offset from OG headPostion field</summary>
        public Vector3 headPosition = Vector3.zero;

        public BodyShapeDef() { }

        public BodyShapeDef(string name)
        {
            this.name = name;
        }
    }
}
