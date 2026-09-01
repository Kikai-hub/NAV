using UnityEngine;
using NAV.Gameplay.Items;

namespace NAV.Gameplay.Inventory
{
    /// <summary>
    /// MonoBehaviour holder for a per-player Inventory instance. Also owns dropping items
    /// into the world (spawning an ItemPickup from ItemDefinition.WorldPrefab) - this is a
    /// gameplay action, so it lives here rather than in the UI layer that triggers it
    /// (ARCHITECTURE_v0.1.md's "UI observes/queries gameplay state" rule).
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private int _capacity = 20;
        [SerializeField] private float _maxWeight = 400f;

        [Header("Item Dropping")]
        [SerializeField] private float _dropForwardDistance = 1.2f;
        [SerializeField] private float _dropHeight = 1f;
        [SerializeField] private float _dropForwardForce = 2f;
        [SerializeField] private float _dropUpwardForce = 2f;

        public Inventory Inventory { get; private set; }

        private void Awake()
        {
            Inventory = new Inventory(_capacity, _maxWeight);
        }

        /// <summary>
        /// Removes amount from the given slot and spawns it as a world item in front of the
        /// player. Returns whether anything was actually dropped.
        /// </summary>
        public bool DropItem(int slotIndex, int amount)
        {
            ItemDefinition definition = Inventory.RemoveFromSlot(slotIndex, amount, out int removed);
            if (removed <= 0 || definition == null)
            {
                return false;
            }

            SpawnWorldItem(definition, removed);
            return true;
        }

        private void SpawnWorldItem(ItemDefinition definition, int quantity)
        {
            Vector3 spawnPosition = transform.position + transform.forward * _dropForwardDistance + Vector3.up * _dropHeight;
            Vector3 impulse = transform.forward * _dropForwardForce + Vector3.up * _dropUpwardForce;
            ItemPickup.SpawnInWorld(definition, quantity, spawnPosition, Quaternion.identity, impulse);
        }
    }
}
