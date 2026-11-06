namespace NAV.Gameplay.Combat
{
    /// <summary>
    /// Something poison works on (the player - PlayerPoison). A poisonous attacker calls
    /// ApplyPoison after its hit lands; the target takes the damage over time itself.
    /// </summary>
    public interface IPoisonable
    {
        bool IsPoisoned { get; }

        /// <summary>totalDamage spread over seconds. A new dose while poisoned keeps the stronger
        /// rate and the longer time left - it doesn't stack without limit.</summary>
        void ApplyPoison(float totalDamage, float seconds);
    }
}
