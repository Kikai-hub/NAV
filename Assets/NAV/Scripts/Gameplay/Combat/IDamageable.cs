namespace NAV.Gameplay.Combat
{
    /// <summary>
    /// Anything that can take combat damage - PlayerCombat (which applies block/parry
    /// mitigation before forwarding to PlayerHealth) and CombatDummy for now. Creatures
    /// (Phase 8) will implement this too once they exist.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        float CurrentHealth { get; }
        float MaxHealth { get; }

        void TakeDamage(float amount);
    }
}
