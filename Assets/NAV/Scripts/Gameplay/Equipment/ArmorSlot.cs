namespace NAV.Gameplay.Equipment
{
    /// <summary>Where an armour piece is worn - one piece per slot (PlayerEquipment).</summary>
    public enum ArmorSlot
    {
        Head,
        Chest,
        Legs
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
