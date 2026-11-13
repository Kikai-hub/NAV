using NAV.Gameplay.Interaction;
using NAV.Gameplay.Inventory;
using UnityEngine;

namespace NAV.Gameplay.Items
{
    /// <summary>
    /// An arrow stuck in the ground, a tree or a wall after a miss: E picks it back up into the
    /// inventory. Added by ArrowProjectile with a small trigger so PlayerInteractor can aim at it
    /// (triggers don't block anyone). Not saved - it disappears on its own after a while.
    /// </summary>
    public class StuckArrow : MonoBehaviour, IInteractable
    {
        private ItemDefinition _item;

        public string InteractionPrompt => _item != null ? NAV.Core.Localization.Loc.F("Pick up {0}", _item.DisplayName) : NAV.Core.Localization.Loc.T("Pick up");

        public static void Attach(GameObject arrow, ItemDefinition item)
        {
            var stuck = arrow.AddComponent<StuckArrow>();
            stuck._item = item;
            // Along the shaft (the model's pivot is the tip, the shaft runs back along -Z), a
            // little fatter than it, so it's easy to aim at.
            var trigger = arrow.AddComponent<CapsuleCollider>();
            trigger.isTrigger = true;
            trigger.direction = 2;
            trigger.radius = 0.08f;
            trigger.height = 0.8f;
            trigger.center = new Vector3(0f, 0f, -0.4f);
        }

        public bool CanInteract(GameObject interactor) => _item != null;

        public void Interact(GameObject interactor)
        {
            var inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null || inventory.Inventory.AddItem(_item, 1) > 0)
            {
                return;
            }

            Destroy(gameObject);
        }
    }
}
