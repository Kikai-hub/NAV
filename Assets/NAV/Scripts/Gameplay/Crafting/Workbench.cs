using UnityEngine;

namespace NAV.Gameplay.Crafting
{
    /// <summary>
    /// A world-space workbench that grants nearby players access to recipes gated by
    /// RecipeDefinition.RequiredWorkbenchTier. Proximity is a trigger volume, not a raycast
    /// interaction (unlike ItemPickup/ResourceNode) - crafting should work while the player is
    /// simply standing near the bench, not looking straight at it. CharacterController
    /// (Player's collider) inherits from Collider, so it raises the same OnTriggerEnter/Exit
    /// events as any other collider without needing a Rigidbody on either side.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class Workbench : MonoBehaviour
    {
        [SerializeField] private WorkbenchDefinition _definition;

        public int Tier => _definition != null ? _definition.Tier : 0;
        public string DisplayName => _definition != null ? _definition.DisplayName : name;

        private void Awake()
        {
            if (_definition == null)
            {
                Debug.LogError($"{nameof(Workbench)} on '{name}' is missing its Definition.", this);
                enabled = false;
                return;
            }

            var range = GetComponent<SphereCollider>();
            range.isTrigger = true;
            range.radius = _definition.Range;
        }

        private void OnTriggerEnter(Collider other)
        {
            other.GetComponentInParent<PlayerCrafting>()?.RegisterNearbyWorkbench(this);
        }

        private void OnTriggerExit(Collider other)
        {
            other.GetComponentInParent<PlayerCrafting>()?.UnregisterNearbyWorkbench(this);
        }
    }
}
