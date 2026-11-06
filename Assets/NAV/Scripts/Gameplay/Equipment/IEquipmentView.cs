using System;
using System.Collections.Generic;
using NAV.Gameplay.Items;

namespace NAV.Gameplay.Equipment
{
    /// <summary>
    /// What a character visibly holds and wears - all HeldItemView and ArmorView need. The local
    /// player's PlayerEquipment is one; a networked player's avatar shows another
    /// (NAV.Network.NetworkEquipmentMirror, fed from the owner's player properties).
    /// </summary>
    public interface IEquipmentView
    {
        /// <summary>The held item, or null for empty hands.</summary>
        ItemDefinition MainHand { get; }

        /// <summary>Every worn armour piece, by slot.</summary>
        IReadOnlyDictionary<ArmorSlot, ItemDefinition> Worn { get; }

        /// <summary>Raised when the held item or any worn armour changes.</summary>
        event Action Changed;
    }
}
