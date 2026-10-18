using UnityEngine;

namespace NAV.Gameplay.Crafting
{
    /// <summary>
    /// Static data for a workbench: its display name, progression tier, and how close a
    /// player needs to be to use it. A recipe's RequiredWorkbenchTier gates crafting against
    /// whatever the player's PlayerCrafting currently reports as nearby (see Workbench for
    /// the proximity trigger, PlayerCrafting.NearbyWorkbenchTier for the query surface).
    /// "Workbench levels" from DEVELOPMENT_ROADMAP_v0.1.md Phase 4 is just higher-Tier
    /// WorkbenchDefinition content on the same system, not a separate mechanism.
    /// </summary>
    [CreateAssetMenu(fileName = "WorkbenchDefinition", menuName = "NAV/Crafting/Workbench Definition")]
    public class WorkbenchDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _displayName;

        [Header("Progression")]
        [SerializeField] private int _tier = 1;

        [Header("Range")]
        [Tooltip("How close the player must stand to craft at it (trigger sphere).")]
        [SerializeField] private float _range = 4f;
        [Tooltip("Building pieces that need a workbench can only be placed within this distance of one (Valheim-style build area).")]
        [SerializeField] private float _buildRadius = 20f;

        [Header("Shelter")]
        [Tooltip("Crafting at it only works while it stands under a roof (a placed building piece overhead).")]
        [SerializeField] private bool _requiresRoof = true;

        public string DisplayName => _displayName;
        public int Tier => _tier;
        public float Range => _range;
        public float BuildRadius => _buildRadius;
        public bool RequiresRoof => _requiresRoof;

        private void OnValidate()
        {
            _tier = Mathf.Max(1, _tier);
            _range = Mathf.Max(0.5f, _range);
            _buildRadius = Mathf.Max(_range, _buildRadius);

            if (string.IsNullOrWhiteSpace(_displayName))
            {
                _displayName = name;
            }
        }
    }
}
