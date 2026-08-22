using UnityEngine;
using NAV.Gameplay.Interaction;
using NAV.Gameplay.Inventory;

namespace NAV.Gameplay.Items
{
    /// <summary>
    /// A world resource node (tree, rock, bush, ...) that yields items over several gather
    /// actions before depleting. Reuses the existing interaction system unchanged - each
    /// Interact() call is one "hit". No tool requirement and no respawn/persistence yet
    /// (Gathering roadmap items "Axe/tool interaction", "Mining interaction" and "Resource
    /// persistence" are explicitly future work; see PROJECT_STATE.md).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ResourceNode : MonoBehaviour, IInteractable
    {
        [SerializeField] private ResourceNodeDefinition _definition;

        private int _remainingHits;
        private Vector3 _initialScale;

        public string InteractionPrompt => _definition != null
            ? $"Gather {_definition.DisplayName} ({_remainingHits} left)"
            : "Gather";

        private void Awake()
        {
            if (_definition == null)
            {
                Debug.LogError($"{nameof(ResourceNode)} on '{name}' is missing its Definition.", this);
                enabled = false;
                return;
            }

            _remainingHits = _definition.HitsToDeplete;
            _initialScale = transform.localScale;
        }

        public bool CanInteract(GameObject interactor)
        {
            return _definition != null && _remainingHits > 0;
        }

        public void Interact(GameObject interactor)
        {
            var inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                Debug.LogWarning($"{name} was interacted with by '{interactor.name}', which has no PlayerInventory.", this);
                return;
            }

            int leftover = inventory.Inventory.AddItem(_definition.DropItem, _definition.AmountPerHit);
            int gathered = _definition.AmountPerHit - leftover;

            if (gathered <= 0)
            {
                Debug.Log($"{interactor.name}'s inventory is full; could not gather from {name}.", this);
                return;
            }

            Debug.Log($"{interactor.name} gathered {gathered}x {_definition.DropItem.DisplayName} from {name} (now has {inventory.Inventory.GetTotalQuantity(_definition.DropItem)}).", this);

            _remainingHits--;
            ApplyDepletionFeedback();

            if (_remainingHits <= 0)
            {
                Destroy(gameObject);
            }
        }

        private void ApplyDepletionFeedback()
        {
            // Cheap placeholder feedback (shrink toward depletion) until real gathering
            // VFX/audio exists - "Final audio/music" is explicitly Not Yet Decided.
            float remainingFraction = Mathf.Clamp01((float)_remainingHits / _definition.HitsToDeplete);
            float scaleFraction = Mathf.Lerp(0.6f, 1f, remainingFraction);
            transform.localScale = _initialScale * scaleFraction;
        }
    }
}
