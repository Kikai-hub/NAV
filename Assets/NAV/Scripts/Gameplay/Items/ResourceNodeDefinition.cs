using System.Collections.Generic;
using UnityEngine;

namespace NAV.Gameplay.Items
{
    /// <summary>
    /// Static data for a world resource node (tree, rock, bush, ...): how many hits it takes
    /// to deplete it, and what it scatters into the world (one or more ResourceNodeDrop
    /// entries, each with its own independent chance) once that happens - e.g. a felled tree
    /// mostly drops Wood but occasionally also drops Resin. Interim hits before depletion carry
    /// no reward of their own, only depletion-feedback (see ResourceNode). See ItemDefinition
    /// for the items being dropped - this only describes the node itself.
    ///
    /// A node can also turn into the next stage when depleted (Becomes Prefab) and leave
    /// something behind (Leaves Behind Prefab) - Valheim-style tree felling is three chained
    /// definitions: standing tree (no drops, becomes a falling trunk, leaves a stump) -> fallen
    /// trunk (no drops, becomes two logs) -> log (drops the wood).
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceNodeDefinition", menuName = "NAV/Items/Resource Node Definition")]
    public class ResourceNodeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _displayName;

        [Header("Yield")]
        [SerializeField] private List<ResourceNodeDrop> _drops = new();
        [SerializeField] private int _hitsToDeplete = 3;

        [Header("Tool Requirement")]
        [Tooltip("Tool that must be held (PlayerEquipment) to hit this node. None = bare hands work.")]
        [SerializeField] private ToolType _requiredTool;
        [SerializeField, Min(1)] private int _minToolTier = 1;

        [Header("Hit Feedback")]
        [Tooltip("Shrink a little with every hit (rocks). Off for trees and logs, which stay their size until they break.")]
        [SerializeField] private bool _shrinkOnHit = true;

        [Header("Depletion Stages")]
        [Tooltip("Optional. Spawned when the node is depleted, at each of the node's Becomes Spawn Points (or at the node itself) - a felled tree becomes a falling trunk, a trunk becomes logs. Usually a prefab with its own ResourceNode for the next stage.")]
        [SerializeField] private GameObject _becomesPrefab;
        [Tooltip("Optional. Spawned at the node's own position when depleted and left there - a tree's stump.")]
        [SerializeField] private GameObject _leavesBehindPrefab;
        [Tooltip("Impulse (N·s) pushed into the top of a spawned Becomes Prefab with a Rigidbody, horizontally away from whoever depleted the node - what topples a felled tree away from the player. 0 = none.")]
        [SerializeField, Min(0f)] private float _fallImpulse;

        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) && _drops.Count > 0 && _drops[0].Item != null
            ? _drops[0].Item.DisplayName
            : _displayName;

        public IReadOnlyList<ResourceNodeDrop> Drops => _drops;
        public int HitsToDeplete => _hitsToDeplete;
        public ToolType RequiredTool => _requiredTool;
        public int MinToolTier => _minToolTier;
        public bool ShrinkOnHit => _shrinkOnHit;
        public GameObject BecomesPrefab => _becomesPrefab;
        public GameObject LeavesBehindPrefab => _leavesBehindPrefab;
        public float FallImpulse => _fallImpulse;

        private void OnValidate()
        {
            _hitsToDeplete = Mathf.Max(1, _hitsToDeplete);
        }
    }
}
