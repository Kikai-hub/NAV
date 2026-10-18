namespace NAV.Gameplay.Items
{
    /// <summary>
    /// What kind of gathering/building tool an item is (ItemDefinition.ToolType). Resource
    /// nodes require one (ResourceNodeDefinition.RequiredTool) - trees need an Axe, stone and
    /// ore need a Pickaxe. Hammer is defined for the planned "hammer unlocks building" rule
    /// (ITEMS_AND_CRAFTING_v0.1.md section 7) but nothing requires it yet.
    /// </summary>
    public enum ToolType
    {
        None,
        Axe,
        Pickaxe,
        Hammer
    }
}
