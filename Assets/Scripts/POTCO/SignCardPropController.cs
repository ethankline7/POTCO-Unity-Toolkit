using System.Collections.Generic;
using UnityEngine;

namespace POTCO
{
    public enum SignCardPropDisplayMode
    {
        Show2DCardProps = 0,
        Hide2DCardPropsForReplacement = 1,
        ShowReplacementPropsOnly = 2
    }

    [DisallowMultipleComponent]
    public sealed class SignCardPropController : MonoBehaviour
    {
        [SerializeField] private SignCardPropDisplayMode displayMode = SignCardPropDisplayMode.Show2DCardProps;
        [SerializeField] private string signFrameModelPath = "";
        [SerializeField] private string signImageModelPath = "";
        [SerializeField] private List<GameObject> cardProps = new List<GameObject>();
        [SerializeField] private List<GameObject> replacementProps = new List<GameObject>();
        [SerializeField] private bool applyModeOnStart = true;

        public SignCardPropDisplayMode DisplayMode => displayMode;
        public string SignFrameModelPath => signFrameModelPath;
        public string SignImageModelPath => signImageModelPath;

        private void Start()
        {
            if (applyModeOnStart)
            {
                ApplyDisplayMode();
            }
        }

        private void OnValidate()
        {
            ApplyDisplayMode();
        }

        public void SetSourcePaths(string frameModelPath, string imageModelPath)
        {
            signFrameModelPath = frameModelPath ?? string.Empty;
            signImageModelPath = imageModelPath ?? string.Empty;
        }

        public void RegisterCardProp(GameObject cardProp)
        {
            if (cardProp == null)
            {
                return;
            }

            if (!cardProps.Contains(cardProp))
            {
                cardProps.Add(cardProp);
            }

            ApplyDisplayMode();
        }

        public void RegisterReplacementProp(GameObject replacementProp)
        {
            if (replacementProp == null)
            {
                return;
            }

            if (!replacementProps.Contains(replacementProp))
            {
                replacementProps.Add(replacementProp);
            }

            ApplyDisplayMode();
        }

        public void SetDisplayMode(SignCardPropDisplayMode mode)
        {
            displayMode = mode;
            ApplyDisplayMode();
        }

        public void ApplyDisplayMode()
        {
            RemoveNullEntries(cardProps);
            RemoveNullEntries(replacementProps);

            bool showCards = displayMode == SignCardPropDisplayMode.Show2DCardProps;
            bool showReplacements = displayMode == SignCardPropDisplayMode.ShowReplacementPropsOnly;

            SetActiveState(cardProps, showCards);
            SetActiveState(replacementProps, showReplacements);
        }

        [ContextMenu("Sign Cards/Show 2D Card Props")]
        private void ContextShowCards()
        {
            SetDisplayMode(SignCardPropDisplayMode.Show2DCardProps);
        }

        [ContextMenu("Sign Cards/Hide 2D Card Props For Replacement")]
        private void ContextHideCards()
        {
            SetDisplayMode(SignCardPropDisplayMode.Hide2DCardPropsForReplacement);
        }

        [ContextMenu("Sign Cards/Show Replacement Props Only")]
        private void ContextShowReplacement()
        {
            SetDisplayMode(SignCardPropDisplayMode.ShowReplacementPropsOnly);
        }

        private static void RemoveNullEntries(List<GameObject> targets)
        {
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                if (targets[i] == null)
                {
                    targets.RemoveAt(i);
                }
            }
        }

        private static void SetActiveState(List<GameObject> targets, bool active)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                GameObject target = targets[i];
                if (target == null || target.activeSelf == active)
                {
                    continue;
                }

                target.SetActive(active);
            }
        }
    }
}
