namespace NAV.Gameplay.Combat
{
    /// <summary>
    /// A stand-in for a player simulated on another machine (a networked player's avatar).
    /// Local creatures can hunt it: their bites go to its owner (TakeCreatureDamage), whose
    /// own PlayerCombat takes them. Players' weapons only hurt it in a PvP room
    /// (CanBeHitByPlayers) - in PvE a swing passes through without a hit.
    /// </summary>
    public interface IRemoteProxy
    {
        bool CanBeHitByPlayers { get; }

        /// <summary>A local creature's bite (and its poison, if any) - sent to the owner.</summary>
        void TakeCreatureDamage(float amount, float poisonDamage, float poisonSeconds);
    }
}
