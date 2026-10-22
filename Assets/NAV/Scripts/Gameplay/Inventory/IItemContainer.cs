namespace NAV.Gameplay.Inventory
{
    /// <summary>
    /// A world object holding items the player can take out through the container panel
    /// (GravestoneUIController) - the Gravestone, the dev WeaponRack and the player-built
    /// StorageContainer (chest). The panel opens when the player interacts with an
    /// IInteractable that is also an IItemContainer.
    /// </summary>
    public interface IItemContainer
    {
        Inventory Contents { get; }

        /// <summary>Panel title, e.g. "Gravestone".</summary>
        string ContainerTitle { get; }

        /// <summary>True for storage (a chest): the player can put items in and rearrange them,
        /// and the panel stays open when it's empty. False for take-only containers (a
        /// gravestone), whose panel closes once they're emptied.</summary>
        bool IsStorage { get; }
    }
}
