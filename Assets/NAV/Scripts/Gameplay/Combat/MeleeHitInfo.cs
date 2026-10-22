using UnityEngine;

namespace NAV.Gameplay.Combat
{
    /// <summary>One landed melee contact, raised by PlayerCombat.HitLanded for Presentation
    /// (particles, sounds, flinch). Point is on the struck collider's surface, Direction is the
    /// weapon's travel at the moment of contact.</summary>
    public readonly struct MeleeHitInfo
    {
        public readonly Collider Collider;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;

        /// <summary>Horizontal unit vector from the attacker to the target - where a blow pushes
        /// it. Not the same as Direction: a hook or a backhand can be moving sideways or even
        /// back toward the attacker at the moment of contact.</summary>
        public readonly Vector3 Away;
        public readonly WeaponDefinition Weapon;
        public readonly HitSurface Surface;

        /// <summary>The hit did something: damage dealt, or a gather-hit that counted. False for
        /// e.g. an axe-less swing at a tree - it still touched, but bounced off.</summary>
        public readonly bool Counted;

        /// <summary>The struck target is an IDamageable (creature, dummy), not a resource.</summary>
        public readonly bool IsCombatant;

        /// <summary>This hit took the target's health to zero.</summary>
        public readonly bool Killed;

        public MeleeHitInfo(Collider collider, Vector3 point, Vector3 direction, Vector3 away, WeaponDefinition weapon,
            HitSurface surface, bool counted, bool isCombatant, bool killed)
        {
            Collider = collider;
            Point = point;
            Direction = direction;
            Away = away;
            Weapon = weapon;
            Surface = surface;
            Counted = counted;
            IsCombatant = isCombatant;
            Killed = killed;
        }
    }

    /// <summary>What happened to a hit the player received - PlayerCombat.DamageReceived.</summary>
    public enum IncomingHitResult
    {
        Hit,
        Blocked,
        Parried
    }
}
