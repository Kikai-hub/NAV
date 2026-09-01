using UnityEngine;
using NAV.Gameplay.Interaction;

namespace NAV.Gameplay.Items
{
    /// <summary>
    /// A world resource node (tree, rock, bush, ...) that absorbs several gather-hits before
    /// being felled/exhausted. Reuses the existing interaction system unchanged - each
    /// Interact() call is one "hit". Interim hits before depletion give no reward, only
    /// depletion-feedback (shrink); on the hit that empties the node it disappears and scatters
    /// its Definition.Drops into the world as physical ItemPickup objects the player then walks
    /// over/interacts with to actually collect - not auto-added to inventory (matches the
    /// developer's request: a chopped tree should drop its yield on the ground, not silently
    /// fill the inventory per swing). No tool requirement and no respawn/persistence yet
    /// (Gathering roadmap items "Axe/tool interaction", "Mining interaction" and "Resource
    /// persistence" are explicitly future work; see PROJECT_STATE.md).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ResourceNode : MonoBehaviour, IInteractable
    {
        [SerializeField] private ResourceNodeDefinition _definition;

        [Header("Depletion Drop")]
        [SerializeField] private float _dropScatterRadius = 1f;
        [SerializeField] private float _dropScatterForce = 1.5f;
        [SerializeField] private float _dropUpwardForce = 2f;

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
            _remainingHits--;

            if (_remainingHits > 0)
            {
                ApplyDepletionFeedback();
                return;
            }

            Debug.Log($"{name} was depleted by {interactor.name} and scattered its yield.", this);
            SpawnDrops();
            Destroy(gameObject);
        }

        /// <summary>
        /// Rolls each Drops entry independently and spawns whichever ones hit as physical
        /// ItemPickups scattered around the node's position (a small random horizontal offset
        /// plus an outward/upward impulse), reusing the same spawn path
        /// PlayerInventory.DropItem uses. A drop with no WorldPrefab on its item fails loudly
        /// via that shared path (Error Handling rule) rather than silently vanishing.
        /// </summary>
        private void SpawnDrops()
        {
            foreach (ResourceNodeDrop drop in _definition.Drops)
            {
                if (drop.Item == null || Random.value > drop.Chance)
                {
                    continue;
                }

                Vector2 offset = Random.insideUnitCircle * _dropScatterRadius;
                Vector3 spawnPosition = transform.position + new Vector3(offset.x, 0.5f, offset.y);
                Vector3 outward = new Vector3(offset.x, 0f, offset.y);
                Vector3 impulse = (outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward) * _dropScatterForce + Vector3.up * _dropUpwardForce;

                ItemPickup.SpawnInWorld(drop.Item, drop.Amount, spawnPosition, Quaternion.identity, impulse);
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
