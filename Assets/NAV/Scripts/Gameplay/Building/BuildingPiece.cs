using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Interaction;

namespace NAV.Gameplay.Building
{
    /// <summary>
    /// Marker/behavior on a placed building piece in the world. IInteractable (raycast + E,
    /// same pattern as ItemPickup/ResourceNode) rather than a trigger (unlike Workbench) -
    /// demolishing something should require deliberately looking at it. No resource refund on
    /// demolish yet - out of scope for Building increment 1, same "explicitly deferred" style
    /// as ResourceNode's tool requirement.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BuildingPiece : MonoBehaviour, IInteractable
    {
        /// <summary>Every currently-placed (Configure()d) piece in the scene, for
        /// PlayerBuilding's snap search. Ghost previews never call Configure, so they never
        /// appear here. A List, not a per-frame FindObjectsByType scan - registration is cheap
        /// (Configure/OnDisable), and the number of placed pieces is what actually matters for
        /// the snap search cost, not how it's gathered.</summary>
        public static readonly List<BuildingPiece> AllPieces = new();

        [SerializeField] private BuildingPieceDefinition _definition;

        private BuildingSnapPoint[] _snapPoints;

        public BuildingPieceDefinition Definition => _definition;
        public IReadOnlyList<BuildingSnapPoint> SnapPoints => _snapPoints;
        public string InteractionPrompt => $"Demolish {(_definition != null ? _definition.DisplayName : name)}";

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

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor)
        {
            Debug.Log($"{name} demolished by {interactor.name}.", this);
            Destroy(gameObject);
        }
    }
}
