using UnityEngine;

namespace NAV.Gameplay.Combat
{
    /// <summary>One striking surface in world space: a capsule from Start to End (a sphere when
    /// they're equal - a fist).</summary>
    public struct MeleeCapsule
    {
        public Vector3 Start;
        public Vector3 End;
        public float Radius;

        public MeleeCapsule(Vector3 start, Vector3 end, float radius)
        {
            Start = start;
            End = end;
            Radius = radius;
        }
    }

    /// <summary>
    /// Where the player's striking surfaces are right now and when the swing is actually
    /// striking. Implemented by Presentation (HeldItemView, which knows the animated hands, the
    /// held model's MeleeHitbox and the attack animation's HitActive curve) so PlayerCombat can
    /// test real contact without depending on any rig, model or Animator.
    /// </summary>
    public interface IMeleeHitboxSource
    {
        /// <summary>Fills buffer with the current striking capsules (the weapon, and/or the
        /// free fists) and returns how many were written.</summary>
        int GetHitCapsules(MeleeCapsule[] buffer);

        /// <summary>True when IsStriking is meaningful (the attack animations carry strike
        /// timing). False = PlayerCombat uses the weapon's Hit Window Start/End instead.</summary>
        bool HasStrikeTiming { get; }

        /// <summary>The current attack animation is in its striking part (HitActive).</summary>
        bool IsStriking { get; }
    }
}
