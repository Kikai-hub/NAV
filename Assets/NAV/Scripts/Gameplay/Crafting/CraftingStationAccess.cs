using UnityEngine;
using NAV.Gameplay.Interaction;

namespace NAV.Gameplay.Crafting
{
    /// <summary>
    /// E on a crafting station (workbench, forge): the crafting panel opens with this station's
    /// recipes (CraftingUIController listens to PlayerInteractor.Interacted), and the player
    /// learns the station's current level - reaching a new level is what unlocks its recipes
    /// (PlayerCrafting). Sits on the piece's solid collider; the station's Workbench (range
    /// trigger) is a child.
    /// </summary>
    public class CraftingStationAccess : MonoBehaviour, IInteractable
    {
        private Workbench _workbench;

        public Workbench Station => _workbench;

        public string InteractionPrompt => _workbench != null ? $"Use {_workbench.DisplayName}" : "Use";

        private void Awake()
        {
            _workbench = GetComponentInChildren<Workbench>(true);
            if (_workbench == null)
            {
                Debug.LogError($"{nameof(CraftingStationAccess)} on '{name}' has no Workbench below it.", this);
                enabled = false;
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            // A build-ghost preview of a station must not open anything.
            return enabled && Workbench.AllWorkbenches.Contains(_workbench);
        }

        public void Interact(GameObject interactor)
        {
            var crafting = interactor.GetComponent<PlayerCrafting>();
            if (crafting != null)
            {
                crafting.LearnStation(_workbench);
            }
        }
    }
}
