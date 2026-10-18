using NAV.Gameplay.Combat;

namespace NAV.Presentation.Animation
{
    /// <summary>
    /// How many attack variants each style's combo has - consecutive attacks step through
    /// them in order (PlayerAnimator's AttackVariant). Must match the attack states built by
    /// NAV > Animation > Setup Player Combat Layers (it checks this table):
    ///   Unarmed:   left jab, right jab (mirrored), left hook          (Brawler punches)
    ///   OneHanded: slash right, slash left, overhead                   (Kevin 1H R/L, 2H chop)
    ///   TwoHanded: chop, chop from the other side (mirrored)           (Kevin 2H)
    ///   Polearm:   thrust, thrust other side (mirrored), overhead slam (Kevin polearm, 2H)
    ///   Shield:    shield bash, right punch                            (Kevin shield, Brawler)
    /// </summary>
    public static class AttackCombo
    {
        public static int VariantCount(WeaponAttackStyle style)
        {
            switch (style)
            {
                case WeaponAttackStyle.Unarmed: return 3;
                case WeaponAttackStyle.OneHanded: return 3;
                case WeaponAttackStyle.TwoHanded: return 2;
                case WeaponAttackStyle.Polearm: return 3;
                case WeaponAttackStyle.Shield: return 2;
                default: return 1;
            }
        }
    }
}
