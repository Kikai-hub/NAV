namespace NAV.Gameplay.Combat
{
    /// <summary>
    /// Something that may be нечисть (the unclean - swamp beasts, the restless dead...): silver
    /// hurts it more (WeaponDefinition.UncleanDamageMultiplier). Creatures implement it from their
    /// CreatureDefinition.
    /// </summary>
    public interface IUncleanTarget
    {
        bool IsUnclean { get; }
    }
}
