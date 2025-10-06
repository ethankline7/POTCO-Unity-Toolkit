/// <summary>
/// Facial morph definition from PirateMale.py / PirateFemale.py ControlShapes.
/// Each morph parameter (headWidth, jawWidth, etc.) has bone transforms for positive and negative values.
/// </summary>
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterOG.Models
{
    /// <summary>Transform type for bone modification</summary>
    public enum TransformType
    {
        TX = 0, // Translation X
        TY = 1, // Translation Y
        TZ = 2, // Translation Z
        RX = 3, // Rotation X
        RY = 4, // Rotation Y
        RZ = 5, // Rotation Z
        SX = 6, // Scale X
        SY = 7, // Scale Y
        SZ = 8  // Scale Z
    }

    /// <summary>A single bone transform for a morph</summary>
    [Serializable]
    public class BoneTransform
    {
        public string boneName;
        public TransformType transformType;
        public float value;

        public BoneTransform(string boneName, TransformType transformType, float value)
        {
            this.boneName = boneName;
            this.transformType = transformType;
            this.value = value;
        }
    }

    /// <summary>Definition of a facial morph parameter</summary>
    [Serializable]
    public class FacialMorphDef
    {
        public string morphName;

        /// <summary>Bone transforms for positive values (morph value > 0)</summary>
        public List<BoneTransform> positiveTransforms = new();

        /// <summary>Bone transforms for negative values (morph value < 0)</summary>
        public List<BoneTransform> negativeTransforms = new();

        public FacialMorphDef() { }

        public FacialMorphDef(string morphName)
        {
            this.morphName = morphName;
        }
    }

    /// <summary>Collection of all facial morph definitions for a gender</summary>
    [Serializable]
    public class FacialMorphDatabase
    {
        public string gender; // "m" or "f"

        /// <summary>Morph name → definition</summary>
        public Dictionary<string, FacialMorphDef> morphs = new();

        public FacialMorphDatabase() { }

        public FacialMorphDatabase(string gender)
        {
            this.gender = gender;
        }

        /// <summary>Get morph definition by name</summary>
        public FacialMorphDef GetMorph(string morphName)
        {
            return morphs.TryGetValue(morphName, out var morph) ? morph : null;
        }
    }
}
