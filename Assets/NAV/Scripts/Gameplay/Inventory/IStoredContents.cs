using UnityEngine;

namespace NAV.Gameplay.Inventory
{
    /// <summary>
    /// A building piece part that holds items of its own - a chest (StorageContainer), a
    /// processing station's queued inputs and fuel (ProcessingStation). SaveManager stores and
    /// restores Contents with the building; PlayerBuilding drops them into the world when the
    /// piece is demolished.
    /// </summary>
    public interface IStoredContents
    {
        Inventory Contents { get; }

        void DropContents(Vector3 center, float scatterRadius, float scatterForce, float upwardForce);
    }
}
