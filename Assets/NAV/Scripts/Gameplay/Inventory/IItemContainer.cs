namespace NAV.Gameplay.Inventory
{
    /// <summary>
    /// A world object holding items the player can take out through the container panel
    /// (GravestoneUIController) - the Gravestone, and the dev WeaponRack. The panel opens when
    /// the player interacts with an IInteractable that is also an IItemContainer.
    /// </summary>
    public interface IItemContainer
    {
        Inventory Contents { get; }

        /// <summary>Panel title, e.g. "Gravestone".</summary>
        string ContainerTitle { get; }
    }
}
