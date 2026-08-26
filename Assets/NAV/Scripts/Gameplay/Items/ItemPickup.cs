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
    [RequireComponent(typeof(Rigidbody))]
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemDefinition _definition;
        [SerializeField] private int _quantity = 1;

        public string InteractionPrompt => _definition != null ? $"Pick up {_definition.DisplayName}" : "Pick up";

        private void Awake()
        {
            ApplyRigidbodyMass();
        }

        private void OnValidate()
        {
            _quantity = Mathf.Max(1, _quantity);
        }

        /// <summary>
        /// Assigns this pickup's item/quantity at runtime (used when spawning a dropped item
        /// from a generic WorldPrefab - see PlayerInventory.DropItem). Hand-placed pickups in
        /// the scene set these via the Inspector instead and never call this.
        /// </summary>
        public void Configure(ItemDefinition definition, int quantity)
        {
            _definition = definition;
            _quantity = Mathf.Max(1, quantity);
            ApplyRigidbodyMass();
        }

        private void ApplyRigidbodyMass()
        {
            if (_definition != null && TryGetComponent(out Rigidbody rb))
            {
                rb.mass = Mathf.Max(0.1f, _definition.Weight);
            }
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
