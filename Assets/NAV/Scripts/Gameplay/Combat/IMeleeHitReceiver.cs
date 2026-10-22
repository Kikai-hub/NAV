using UnityEngine;

namespace NAV.Gameplay.Combat
{
    /// <summary>
    /// Something a melee swing can strike that isn't a combatant (no health) - a tree or rock
    /// (ResourceNode) takes a gather-hit when the weapon physically touches it. PlayerCombat
    /// calls this at most once per swing per receiver.
    /// </summary>
    public interface IMeleeHitReceiver
    {
        /// <summary>Returns true if the hit counted (e.g. false when the tool is wrong) - only
        /// used for feedback, the receiver has already applied its own rules.</summary>
        bool ReceiveMeleeHit(GameObject attacker, WeaponDefinition weapon);
    }
}
