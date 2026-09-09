using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Items;

namespace NAV.Core.Save
{
    /// <summary>
    /// Hand-maintained list of every ItemDefinition that can end up in a save file, so
    /// SaveManager can resolve an InventorySlotSaveData.ItemId string back to a real
    /// ItemDefinition asset on load (JSON can't hold a direct Unity object reference). Same
    /// "fixed serialized list, no auto-discovery system yet" pattern as PlayerCrafting.KnownRecipes/
    /// PlayerBuilding.KnownPieces - add new items here by hand as they're created, same as those.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "NAV/Core/Save/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private List<ItemDefinition> _items = new();

        private Dictionary<string, ItemDefinition> _lookup;

        public ItemDefinition Get(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            if (_lookup == null)
            {
                BuildLookup();
            }

            if (_lookup.TryGetValue(id, out ItemDefinition definition))
            {
                return definition;
            }

            Debug.LogError($"{nameof(ItemDatabase)}: no ItemDefinition with id '{id}' - add it to this database's Items list.", this);
            return null;
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, ItemDefinition>();
            foreach (ItemDefinition item in _items)
            {
                if (item != null && !string.IsNullOrEmpty(item.Id))
                {
                    _lookup[item.Id] = item;
                }
            }
        }
    }
}
