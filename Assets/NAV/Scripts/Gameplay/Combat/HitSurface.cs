namespace NAV.Gameplay.Combat
{
    /// <summary>What a struck object is made of - Presentation picks particles and sounds by
    /// it. Gameplay only declares it (IHitSurface); nothing in combat rules depends on it.</summary>
    public enum HitSurface
    {
        Other,
        Flesh,
        Wood,
        Stone
    }

    /// <summary>Put on anything a melee swing can strike to say what it's made of. Objects
    /// without it count as HitSurface.Other.</summary>
    public interface IHitSurface
    {
        HitSurface Surface { get; }
    }

    /// <summary>Something that physically reacts to being hit (stagger, knockback) - called by
    /// PlayerCombat right after TakeDamage, with the swing's direction. Separate from
    /// IDamageable because IDamageable.TakeDamage carries no direction and is also used by
    /// non-melee damage.</summary>
    public interface IHitReactive
    {
        void ReactToHit(UnityEngine.Vector3 direction);
    }
}
