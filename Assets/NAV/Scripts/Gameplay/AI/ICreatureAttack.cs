using UnityEngine;
using NAV.Gameplay.Combat;

namespace NAV.Gameplay.AI
{
    /// <summary>
    /// A creature's own attack in place of Creature's instant bite - for bosses and anything
    /// with telegraphed, timed blows (wind-up, strike, recovery). Found on the creature's object
    /// by Creature. While IsBusy the creature is committed: it doesn't move, turn or change
    /// state; perception, health and death stay Creature's.
    /// </summary>
    public interface ICreatureAttack
    {
        /// <summary>In the middle of an action (attack, roar) - the creature stands still.</summary>
        bool IsBusy { get; }

        /// <summary>Starts an attack on a target in range, facing it. False = not now (the
        /// creature tries again next frame, the cooldown doesn't start).</summary>
        bool TryBeginAttack(IDamageable target, Transform targetTransform);
    }
}
