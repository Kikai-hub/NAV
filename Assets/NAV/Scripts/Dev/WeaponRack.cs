using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Interaction;
using NAV.Gameplay.Items;

namespace NAV.Dev
{
    /// <summary>
    /// TEMPORARY weapon rack (see WeaponArsenal): an interactable item container - E opens the
    /// same panel as a Gravestone (IItemContainer), and the weapons are dragged into the
    /// inventory. Unlike a gravestone it stays when emptied, and it isn't saved: racks are
    /// re-spawned full on every load. Shows its first weapon standing upright as a marker.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WeaponRack : MonoBehaviour, IInteractable, NAV.Gameplay.Inventory.IItemContainer
    {
        [Tooltip("Where the display weapon is placed (its grip), standing blade-up.")]
        [SerializeField] private Transform _displayAnchor;

        public NAV.Gameplay.Inventory.Inventory Contents { get; private set; }
        public string ContainerTitle { get; private set; } = "Weapon Rack";
        public bool IsStorage => false;

        public string InteractionPrompt => $"Open {ContainerTitle} (temporary)";

        public void Configure(string title, IReadOnlyList<ItemDefinition> items)
        {
            ContainerTitle = title;
            Contents = new NAV.Gameplay.Inventory.Inventory(Mathf.Max(1, items.Count));
            foreach (ItemDefinition item in items)
            {
                if (item != null)
                {
                    Contents.AddItem(item, 1);
                }
            }

            ItemDefinition display = items.Count > 0 ? items[0] : null;
            if (display != null && display.HeldPrefab != null && _displayAnchor != null)
            {
                GameObject model = Instantiate(display.HeldPrefab, _displayAnchor, false);
                foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                {
                    Destroy(collider);
                }
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            return Contents != null;
        }

        public void Interact(GameObject interactor)
        {
            // Intentionally empty - the container panel reacts to PlayerInteractor.Interacted,
            // same as for the Gravestone.
        }
    }
}
