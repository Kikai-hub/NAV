using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Building;

namespace NAV.Gameplay.Crafting
{
    /// <summary>
    /// Raises the level of a nearby crafting station of one kind, Valheim style: a placed piece
    /// with this component within the station's Extension Radius adds 1 to its level (each
    /// Extension Id counts once per station - two anvils don't make level 3). The tanning rack
    /// upgrades the workbench, the anvil and the bellows upgrade the forge. Recipes then ask
    /// for a station level (RecipeDefinition.RequiredWorkbenchTier).
    /// </summary>
    public class WorkbenchExtension : MonoBehaviour
    {
        /// <summary>Every placed (not build-ghost) extension.</summary>
        public static readonly List<WorkbenchExtension> All = new();

        [Tooltip("The kind of station this upgrades (e.g. Forge).")]
        [SerializeField] private WorkbenchDefinition _upgrades;
        [Tooltip("Same id = same upgrade; only one of each counts per station.")]
        [SerializeField] private string _extensionId;

        private BuildingPiece _ownPiece;
        private bool _registered;

        public WorkbenchDefinition Upgrades => _upgrades;
        public string ExtensionId => string.IsNullOrEmpty(_extensionId) ? name : _extensionId;

        private void Awake()
        {
            _ownPiece = GetComponentInParent<BuildingPiece>();
            if (_upgrades == null)
            {
                Debug.LogError($"{nameof(WorkbenchExtension)} on '{name}' doesn't say which station it upgrades.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            // A build-ghost preview carries this too and must never count.
            bool real = _ownPiece == null || _ownPiece.IsPlaced;
            if (real != _registered)
            {
                _registered = real;
                if (real)
                {
                    All.Add(this);
                }
                else
                {
                    All.Remove(this);
                }
            }
        }

        private void OnDisable()
        {
            if (_registered)
            {
                All.Remove(this);
                _registered = false;
            }
        }
    }
}
