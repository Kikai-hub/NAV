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
        void ReceiveMeleeHit(GameObject attacker, WeaponDefinition weapon);
    }
}
