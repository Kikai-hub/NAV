namespace NAV.Gameplay.Equipment
{
    /// <summary>Where an armour piece is worn - one piece per slot (PlayerEquipment).</summary>
    public enum ArmorSlot
    {
        Head,
        Chest,
        Legs,
        // Over the shoulders (tier 5): fur cloaks keep the mountain cold off (ArmorDefinition.Warmth).
        Cloak
    }

    /// <summary>Character bone a worn armour model is attached to (ArmorView maps these to the
    /// rig's bones).</summary>
    public enum ArmorBone
    {
        Head,
        Chest,
        ThighLeft,
        ThighRight,
        ShinLeft,
        ShinRight
    }
}
