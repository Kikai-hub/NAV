using UnityEngine;

namespace NAV.Gameplay.Interaction
{
    /// <summary>
    /// Optional companion to IInteractable for prompts that depend on who is looking - e.g. a
    /// resource node saying "needs Axe" only when the interactor isn't holding one.
    /// PlayerInteractor.CurrentPrompt prefers this over IInteractable.InteractionPrompt.
    /// </summary>
    public interface IContextualPrompt
    {
        string GetPrompt(GameObject interactor);
    }
}
