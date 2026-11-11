using UnityEngine;

namespace NAV.Gameplay.Items
{
    /// <summary>
    /// Multiplayer's say over items lying in the world (ItemPickup.Network;
    /// NAV.Network.NetworkItemSync). Kept as an interface so Gameplay doesn't know the network.
    /// </summary>
    public interface IItemPickupNetwork
    {
        /// <summary>A pickup was spawned on this machine (a drop, loot, a station's product,
        /// a felled log's wood) with this impulse - share it.</summary>
        void OnSpawned(ItemPickup pickup, Vector3 impulse);

        /// <summary>Someone here wants to pick it up. True = the network decides who gets it
        /// (and later calls GiveTo on the winner's machine); false = pick it up locally.</summary>
        bool InterceptPickup(ItemPickup pickup, GameObject interactor);
    }
}
