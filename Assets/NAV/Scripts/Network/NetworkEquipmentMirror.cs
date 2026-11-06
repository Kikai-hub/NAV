using System;
using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Equipment;
using NAV.Gameplay.Items;

namespace NAV.Network
{
    /// <summary>
    /// A network avatar's equipment as its owner reports it (NetworkAvatar reads the owner's
    /// "eq" player property) - the IEquipmentView the avatar's HeldItemView and ArmorView show.
    /// Holds no gameplay state: nothing is in an inventory, nothing can be used.
    /// </summary>
    public class NetworkEquipmentMirror : MonoBehaviour, IEquipmentView
    {
        private readonly Dictionary<ArmorSlot, ItemDefinition> _worn = new();

        public ItemDefinition MainHand { get; private set; }
        public IReadOnlyDictionary<ArmorSlot, ItemDefinition> Worn => _worn;
        public event Action Changed;

        public void Show(ItemDefinition mainHand, IEnumerable<ItemDefinition> worn)
        {
            MainHand = mainHand;
            _worn.Clear();
            foreach (ItemDefinition item in worn)
            {
                if (item != null && item.Armor != null)
                {
                    _worn[item.Armor.Slot] = item;
                }
            }

            Changed?.Invoke();
        }
    }
}
