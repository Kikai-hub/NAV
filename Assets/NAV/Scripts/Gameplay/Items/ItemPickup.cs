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

        public string InteractionPrompt => _definition != null ? NAV.Core.Localization.Loc.F("Pick up {0}", _definition.DisplayName) : NAV.Core.Localization.Loc.T("Pick up");

        public ItemDefinition Definition => _definition;
        public int Quantity => _quantity;

        /// <summary>Multiplayer: told about every pickup spawned here and asked before one is
        /// picked up (IItemPickupNetwork). Null offline.</summary>
        public static IItemPickupNetwork Network { get; set; }

        /// <summary>Network id (0 = not shared: offline, or a world-generated pickup).</summary>
        public long NetworkId { get; set; }

        /// <summary>Set while the network itself spawns a copy, so it isn't announced back.</summary>
        public static bool SpawningFromNetwork { get; set; }

        /// <summary>Something was picked up here: the pickup (destroyed at the end of the
        /// frame when emptied), who, how many, and whether it is now empty.</summary>
        public static event System.Action<ItemPickup, GameObject, int, bool> PickedUp;

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

        /// <summary>
        /// Instantiates definition.WorldPrefab, configures it as a pickup for quantity, and
        /// applies an optional physics impulse - the shared spawn path for anything that needs
        /// to put an item into the world as a physical pickup (PlayerInventory.DropItem when
        /// the player throws an item out; ResourceNode when a depleted node scatters its
        /// yield). Fails loudly (no silent no-op) if the definition has no WorldPrefab or that
        /// prefab has no ItemPickup, same as the pre-existing DropItem behavior. Returns null
        /// on failure.
        /// </summary>
        public static ItemPickup SpawnInWorld(ItemDefinition definition, int quantity, Vector3 position, Quaternion rotation, Vector3 impulse)
        {
            if (definition.WorldPrefab == null)
            {
                Debug.LogError($"'{definition.DisplayName}' has no WorldPrefab assigned; cannot spawn it into the world.");
                return null;
            }

            GameObject instance = Instantiate(definition.WorldPrefab, position, rotation);

            if (!instance.TryGetComponent(out ItemPickup pickup))
            {
                Debug.LogError($"WorldPrefab for '{definition.DisplayName}' has no ItemPickup component.", instance);
                Destroy(instance);
                return null;
            }

            pickup.Configure(definition, quantity);

            if (impulse != Vector3.zero && instance.TryGetComponent(out Rigidbody rb))
            {
                rb.AddForce(impulse, ForceMode.Impulse);
            }

            if (!SpawningFromNetwork)
            {
                Network?.OnSpawned(pickup, impulse);
            }

            return pickup;
        }

        private void ApplyRigidbodyMass()
        {
            if (_definition != null && TryGetComponent(out Rigidbody rb))
            {
                rb.mass = Mathf.Max(0.1f, _definition.Weight);

                // In water it floats or sinks by its material (ItemDefinition.Density).
                if (!TryGetComponent(out NAV.Gameplay.Environment.WaterBuoyancy buoyancy))
                {
                    buoyancy = gameObject.AddComponent<NAV.Gameplay.Environment.WaterBuoyancy>();
                }

                buoyancy.Density = _definition.Density;
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            return _definition != null && _quantity > 0;
        }

        public void Interact(GameObject interactor)
        {
            // Online: one item, one owner - the network asks who gets it first (GiveTo then).
            if (Network != null && Network.InterceptPickup(this, interactor))
            {
                return;
            }

            GiveTo(interactor);
        }

        /// <summary>Puts as much as fits into the interactor's inventory (the rest stays here).
        /// Returns how many were taken.</summary>
        public int GiveTo(GameObject interactor)
        {
            var inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                Debug.LogWarning($"{name} was interacted with by '{interactor.name}', which has no PlayerInventory.", this);
                return 0;
            }

            int leftover = inventory.Inventory.AddItem(_definition, _quantity);
            int picked = _quantity - leftover;

            if (picked <= 0)
            {
                Debug.Log($"{interactor.name}'s inventory is full; could not pick up {_definition.DisplayName}.", this);
                return 0;
            }

            Debug.Log($"{interactor.name} picked up {picked}x {_definition.DisplayName} (now has {inventory.Inventory.GetTotalQuantity(_definition)}).", this);

            _quantity = leftover;
            if (_quantity <= 0)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
            }

            PickedUp?.Invoke(this, interactor, picked, _quantity <= 0);
            return picked;
        }
    }
}
