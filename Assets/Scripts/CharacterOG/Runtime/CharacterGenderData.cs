/// <summary>
/// Stores character gender information that persists through play mode
/// Used by SimpleAnimationPlayer to detect correct animation set (fp_ vs mp_)
/// </summary>
using UnityEngine;

namespace CharacterOG.Runtime
{
    [ExecuteAlways]
    public class CharacterGenderData : MonoBehaviour
    {
        [SerializeField, HideInInspector]
        private string gender = "m"; // "m" or "f"

        /// <summary>
        /// Set the character's gender
        /// </summary>
        public void SetGender(string genderValue)
        {
            gender = genderValue;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.EditorUtility.SetDirty(gameObject);
            }
#endif
        }

        /// <summary>
        /// Get the character's gender
        /// </summary>
        public string GetGender()
        {
            return gender;
        }

        /// <summary>
        /// Get the gender prefix for animation names (mp_ or fp_)
        /// </summary>
        public string GetGenderPrefix()
        {
            return gender == "f" ? "fp_" : "mp_";
        }

        /// <summary>
        /// Check if character is female
        /// </summary>
        public bool IsFemale()
        {
            return gender == "f";
        }

        /// <summary>
        /// Check if character is male
        /// </summary>
        public bool IsMale()
        {
            return gender == "m";
        }
    }
}
