using System.Collections.Generic;
using UnityEngine;

namespace NAV.Gameplay.Building
{
    /// <summary>
    /// Marker on a placed building piece in the world: which definition it was built from (for
    /// saving, snapping and demolish refunds) and its snap points. Demolishing is the hammer's
    /// job - PlayerBuilding's Demolish tool (picked in the build menu) removes the piece the
    /// player aims at and drops its cost as pickups. It used to be an IInteractable demolished by E
    /// with no refund (Building increment 1); that was removed 2026-10-18 so a stray E press
    /// can't knock a wall down and E reaches the piece's own interactables (workbench,
    /// campfire) instead.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BuildingPiece : MonoBehaviour
    {
        /// <summary>Every currently-placed (Configure()d) piece in the scene, for
        /// PlayerBuilding's snap search and demolish targeting. Ghost previews never call
        /// Configure, so they never appear here. A List, not a per-frame FindObjectsByType scan -
        /// registration is cheap (Configure/OnDisable), and the number of placed pieces is what
        /// actually matters for the snap search cost, not how it's gathered.</summary>
        public static readonly List<BuildingPiece> AllPieces = new();

        [SerializeField] private BuildingPieceDefinition _definition;

        private BuildingSnapPoint[] _snapPoints;

        public BuildingPieceDefinition Definition => _definition;
        public IReadOnlyList<BuildingSnapPoint> SnapPoints => _snapPoints;

        /// <summary>True for a real placed piece (not a ghost preview).</summary>
        public bool IsPlaced => _definition != null && AllPieces.Contains(this);

        private void Awake()
        {
            _snapPoints = GetComponentsInChildren<BuildingSnapPoint>();
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            AllPieces.Remove(this);
        }

        /// <summary>Assigned at runtime when PlayerBuilding instantiates this piece. Also
        /// registers it - OnEnable already ran (with no Definition yet) by the time Instantiate
        /// returns, so registration has to happen here too, not just in OnEnable.</summary>
        public void Configure(BuildingPieceDefinition definition)
        {
            _definition = definition;
            TryRegister();
        }

        private void TryRegister()
        {
            if (_definition != null && !AllPieces.Contains(this))
            {
                AllPieces.Add(this);
            }
        }
    }
}
