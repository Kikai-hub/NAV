using UnityEngine;
using NAV.Gameplay.Interaction;
using NAV.Gameplay.Inventory;

namespace NAV.Gameplay.Items
{
    /// <summary>
    /// A world-space instance of an item that can be picked up via the interaction system
    /// (see PlayerInteractor/IInteractable) into whichever inventory the interactor carries.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemDefinition _definition;
        [SerializeField] private int _quantity = 1;

        public string InteractionPrompt => _definition != null ? $"Pick up {_definition.DisplayName}" : "Pick up";

        private void OnValidate()
        {
            _quantity = Mathf.Max(1, _quantity);
        }

        public bool CanInteract(GameObject interactor)
        {
            return _definition != null && _quantity > 0;
        }

        public void Interact(GameObject interactor)
        {
            var inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                Debug.LogWarning($"{name} was interacted with by '{interactor.name}', which has no PlayerInventory.", this);
                return;
            }

            int leftover = inventory.Inventory.AddItem(_definition, _quantity);
            int picked = _quantity - leftover;

            if (picked <= 0)
            {
                Debug.Log($"{interactor.name}'s inventory is full; could not pick up {_definition.DisplayName}.", this);
                return;
            }

            Debug.Log($"{interactor.name} picked up {picked}x {_definition.DisplayName} (now has {inventory.Inventory.GetTotalQuantity(_definition)}).", this);

            _quantity = leftover;
            if (_quantity <= 0)
            {
                Destroy(gameObject);
            }
        }
    }
}
