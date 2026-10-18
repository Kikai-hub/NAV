namespace NAV.Gameplay.Combat
{
    /// <summary>
    /// What kind of weapon this is - the family it belongs to, independent of its stats/tier.
    /// Each class has one attack style (see WeaponPackGenerator's class table):
    ///   Dagger, Sword, Mace          - OneHanded (right/left combo; daggers fastest, maces slowest)
    ///   HandAxe, BattleAxe, Greatsword, Warhammer - TwoHanded (overhead chop; heavier = slower)
    ///   Spear                        - Polearm (thrust, longest reach)
    ///   Shield                       - Shield (bash; held in the left hand, strong block)
    /// Unarmed = fists, Tool = gathering tools that can also hit (pickaxe).
    /// </summary>
    public enum WeaponClass
    {
        Unarmed = 0,
        Tool = 1,
        Dagger = 2,
        Sword = 3,
        Greatsword = 4,
        HandAxe = 5,
        BattleAxe = 6,
        Mace = 7,
        Warhammer = 8,
        Spear = 9,
        Shield = 10,
    }
}
