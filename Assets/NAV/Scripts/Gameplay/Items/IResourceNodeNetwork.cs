using UnityEngine;

namespace NAV.Gameplay.Items
{
    /// <summary>
    /// Multiplayer's say over gathering (ResourceNode.Network; NAV.Network.NetworkResourceSync).
    /// Kept as an interface so Gameplay doesn't know about the network.
    /// </summary>
    public interface IResourceNodeNetwork
    {
        /// <summary>A valid gather hit landed on a node here (tool checked, hits = how many it is
        /// worth). True = the network takes it over: the shared count lives with the authority,
        /// which answers with ResourceNode.SetRemainingHits / DepleteBy. False = handle it
        /// locally as offline (a stage this machine owns).</summary>
        bool InterceptGather(ResourceNode node, GameObject interactor, int hits);
    }
}
