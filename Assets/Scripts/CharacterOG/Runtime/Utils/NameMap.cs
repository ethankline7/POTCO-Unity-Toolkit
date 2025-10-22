/// <summary>
/// Utility for mapping OG Python names to Unity-friendly names.
/// Handles case-insensitive lookups and common naming variations.
/// </summary>
using System.Collections.Generic;

namespace CharacterOG.Runtime.Utils
{
    public static class NameMap
    {
        private static Dictionary<string, string> s_genderMap = new()
        {
            { "m", "male" },
            { "f", "female" },
            { "male", "m" },
            { "female", "f" }
        };

        private static Dictionary<string, string> s_slotMap = new()
        {
            { "hat", "HAT" },
            { "shirt", "SHIRT" },
            { "vest", "VEST" },
            { "coat", "COAT" },
            { "pant", "PANT" },
            { "pants", "PANT" },
            { "belt", "BELT" },
            { "sock", "SOCK" },
            { "socks", "SOCK" },
            { "shoe", "SHOE" },
            { "shoes", "SHOE" }
        };

        /// <summary>Normalize gender code (m/f)</summary>
        public static string NormalizeGender(string gender)
        {
            if (string.IsNullOrEmpty(gender))
                return "m";

            string lower = gender.ToLower();
            return s_genderMap.TryGetValue(lower, out string mapped) ? mapped : lower;
        }

        /// <summary>Get display name for gender</summary>
        public static string GetGenderDisplayName(string gender)
        {
            gender = NormalizeGender(gender);
            return gender == "m" ? "Male" : "Female";
        }

        /// <summary>Normalize slot name</summary>
        public static string NormalizeSlot(string slot)
        {
            if (string.IsNullOrEmpty(slot))
                return slot;

            string lower = slot.ToLower();
            return s_slotMap.TryGetValue(lower, out string mapped) ? mapped : slot;
        }

        /// <summary>Convert snake_case to Title Case</summary>
        public static string SnakeCaseToTitleCase(string snakeCase)
        {
            if (string.IsNullOrEmpty(snakeCase))
                return snakeCase;

            var words = snakeCase.Split('_');
            var titleWords = new List<string>();

            foreach (var word in words)
            {
                if (string.IsNullOrEmpty(word))
                    continue;

                titleWords.Add(char.ToUpper(word[0]) + word.Substring(1).ToLower());
            }

            return string.Join(" ", titleWords);
        }

        /// <summary>Convert Title Case to snake_case</summary>
        public static string TitleCaseToSnakeCase(string titleCase)
        {
            if (string.IsNullOrEmpty(titleCase))
                return titleCase;

            return titleCase.ToLower().Replace(" ", "_");
        }

        /// <summary>Get model prefix for gender (mp_ or fp_)</summary>
        public static string GetModelPrefix(string gender)
        {
            return NormalizeGender(gender) == "f" ? "fp_" : "mp_";
        }

        /// <summary>Get model name for gender and detail level</summary>
        public static string GetModelName(string gender, int detailLevel = 500)
        {
            string prefix = GetModelPrefix(gender);
            return $"{prefix}{detailLevel}";
        }
    }
}
