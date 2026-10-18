namespace NAV.Gameplay.Combat
{
    /// <summary>
    /// How a weapon swings - picks the attack animation set (PlayerAnimator / the player
    /// Animator's "Upper Body" layer, AttackStyle parameter) and so the attack's feel. The int
    /// values are the Animator's AttackStyle values - don't renumber them.
    /// </summary>
    public enum WeaponAttackStyle
    {
        /// <summary>Fists - a quick single-handed swipe.</summary>
        Unarmed = 0,

        /// <summary>Swords and light one-handers - alternating right/left slashes (combo).</summary>
        OneHanded = 1,

        /// <summary>Axes, pickaxes, heavy weapons - a slow overhead chop with both hands.</summary>
        TwoHanded = 2,

        /// <summary>Spears/polearms - a forward thrust.</summary>
        Polearm = 3,

        /// <summary>Shields - a bash with the shield arm.</summary>
        Shield = 4,
    }
}
