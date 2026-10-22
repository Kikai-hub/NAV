using System;
using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Interaction;
using NAV.Gameplay.Items;

namespace NAV.Gameplay.Inventory
{
    /// <summary>
    /// A player-built storage container (the chest building piece): a fixed number of slots the
    /// player puts items into and takes them out of through the container panel
    /// (GravestoneUIController - opened by PlayerInteractor.Interacted, like a Gravestone).
    /// Interact() itself does nothing; the UI owns opening and moving items (same split as
    /// Gravestone). IsOpen is set by that panel while it shows this container, so Presentation
    /// can open the lid. Contents are saved with the building (SaveManager) and dropped into the
    /// world when the piece is demolished (DropContents, called by PlayerBuilding).
    /// </summary>
    public class StorageContainer : MonoBehaviour, IInteractable, IItemContainer
    {
        [SerializeField] private string _title = "Chest";
        [Tooltip("Number of slots (Valheim's wooden chest has 10).")]
        [SerializeField, Min(1)] private int _capacity = 10;

        public Inventory Contents { get; private set; }
        public string ContainerTitle => _title;
        public bool IsStorage => true;
        public string InteractionPrompt => $"Open {_title}";

        /// <summary>True while the container panel shows this container.</summary>
        public bool IsOpen { get; private set; }

        public event Action<bool> OpenChanged;

        private void Awake()
        {
            Contents = new Inventory(_capacity);
        }

        public bool CanInteract(GameObject interactor)
        {
            return Contents != null;
        }

        public void Interact(GameObject interactor)
        {
            // Intentionally empty - GravestoneUIController opens the panel on
            // PlayerInteractor.Interacted (see class doc comment).
        }

        /// <summary>Called by the container panel when it starts/stops showing this container.</summary>
        public void SetOpen(bool open)
        {
            if (IsOpen == open)
            {
                return;
            }

            IsOpen = open;
            OpenChanged?.Invoke(open);
        }

        /// <summary>Throws every stack out into the world as pickups around center and empties
        /// the container - a demolished chest spills what it held instead of deleting it.</summary>
        public void DropContents(Vector3 center, float scatterRadius, float scatterForce, float upwardForce)
        {
            IReadOnlyList<ItemStack> slots = Contents.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                ItemStack stack = slots[i];
                if (stack.IsEmpty)
                {
                    continue;
                }

                ItemDefinition item = stack.Definition;
                Contents.RemoveFromSlot(i, stack.Quantity, out int removed);

                Vector2 offset = UnityEngine.Random.insideUnitCircle * scatterRadius;
                Vector3 outward = new Vector3(offset.x, 0f, offset.y);
                Vector3 impulse = (outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward) * scatterForce + Vector3.up * upwardForce;
                ItemPickup.SpawnInWorld(item, removed, center + outward, Quaternion.identity, impulse);
            }
        }
    }
}
