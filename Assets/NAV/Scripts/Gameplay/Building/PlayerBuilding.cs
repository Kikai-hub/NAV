using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Player;

namespace NAV.Gameplay.Building
{
    /// <summary>
    /// Drives placement mode: aiming a translucent "ghost" preview of the selected
    /// BuildingPieceDefinition in front of the camera, rotating/cycling it, snapping it onto
    /// nearby placed pieces' BuildingSnapPoints, and building the real piece (paying its cost)
    /// when placement is valid. "Known pieces" is a fixed serialized list, same deliberate
    /// simplification as PlayerCrafting's "known recipes" - no unlock system yet. Structural
    /// validation (overlap/support checks) is its own separate Building-phase roadmap item and
    /// is NOT implemented here; validity in this increment is just "the aim ray hit something
    /// within range".
    /// </summary>
    public class PlayerBuilding : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private PlayerInventory _playerInventory;
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private List<BuildingPieceDefinition> _knownPieces = new();

        [Header("Placement")]
        [SerializeField] private float _maxPlacementDistance = 15f;
        [SerializeField] private float _rotationStepDegrees = 45f;
        [SerializeField] private LayerMask _placementLayers = ~0;

        [Header("Snapping")]
        [SerializeField] private float _snapRadius = 0.75f;
        [SerializeField] private float _snapSearchRadius = 6f;

        [Header("Ghost Materials")]
        [SerializeField] private Material _validGhostMaterial;
        [SerializeField] private Material _invalidGhostMaterial;

        private int _selectedIndex;
        private GameObject _ghostInstance;
        private readonly List<BuildingSnapPoint> _ghostSnapPoints = new();
        private float _ghostYawOffset;
        private float _pieceHalfHeight;
        private bool _hasValidHit;
        private bool? _ghostAppliedValid;

        public bool IsBuildModeActive { get; private set; }
        public BuildingPieceDefinition SelectedPiece => _knownPieces.Count > 0 ? _knownPieces[_selectedIndex] : null;
        public bool IsPlacementValid => _hasValidHit;
        public bool IsSnapped { get; private set; }
        public bool CanAffordSelected => SelectedPiece != null && SelectedPiece.CanAfford(_playerInventory.Inventory);
        public bool CanPlace => IsBuildModeActive && _hasValidHit && CanAffordSelected;

        private void Awake()
        {
            if (_inputHandler == null || _playerInventory == null || _cameraTransform == null)
            {
                Debug.LogError($"{nameof(PlayerBuilding)} on '{name}' is missing a required reference (InputHandler/PlayerInventory/CameraTransform).", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_inputHandler == null)
            {
                return;
            }

            _inputHandler.ToggleBuildPerformed += HandleToggleBuild;
            _inputHandler.AttackPerformed += HandlePlace;
            _inputHandler.RotatePiecePerformed += HandleRotate;
            _inputHandler.CycleNextPerformed += HandleCycleNext;
            _inputHandler.CyclePreviousPerformed += HandleCyclePrevious;
        }

        private void OnDisable()
        {
            if (_inputHandler == null)
            {
                return;
            }

            _inputHandler.ToggleBuildPerformed -= HandleToggleBuild;
            _inputHandler.AttackPerformed -= HandlePlace;
            _inputHandler.RotatePiecePerformed -= HandleRotate;
            _inputHandler.CycleNextPerformed -= HandleCycleNext;
            _inputHandler.CyclePreviousPerformed -= HandleCyclePrevious;

            DestroyGhost();
        }

        private void Update()
        {
            if (!IsBuildModeActive)
            {
                return;
            }

            UpdateGhost();
        }

        private void HandleToggleBuild()
        {
            if (IsBuildModeActive)
            {
                ExitBuildMode();
            }
            else
            {
                EnterBuildMode();
            }
        }

        private void EnterBuildMode()
        {
            if (_knownPieces.Count == 0)
            {
                Debug.LogWarning($"{nameof(PlayerBuilding)} on '{name}' has no known building pieces to place.", this);
                return;
            }

            IsBuildModeActive = true;
            _ghostYawOffset = 0f;
            SpawnGhost();
        }

        private void ExitBuildMode()
        {
            IsBuildModeActive = false;
            DestroyGhost();
        }

        private void HandleCycleNext()
        {
            if (!IsBuildModeActive || _knownPieces.Count == 0)
            {
                return;
            }

            _selectedIndex = (_selectedIndex + 1) % _knownPieces.Count;
            SpawnGhost();
        }

        private void HandleCyclePrevious()
        {
            if (!IsBuildModeActive || _knownPieces.Count == 0)
            {
                return;
            }

            _selectedIndex = (_selectedIndex - 1 + _knownPieces.Count) % _knownPieces.Count;
            SpawnGhost();
        }

        private void HandleRotate()
        {
            if (!IsBuildModeActive)
            {
                return;
            }

            _ghostYawOffset = (_ghostYawOffset + _rotationStepDegrees) % 360f;
        }

        private void HandlePlace()
        {
            if (!CanPlace)
            {
                return;
            }

            BuildingPieceDefinition piece = SelectedPiece;
            piece.PayCost(_playerInventory.Inventory);

            GameObject instance = Instantiate(piece.Prefab, _ghostInstance.transform.position, _ghostInstance.transform.rotation);
            if (instance.TryGetComponent(out BuildingPiece buildingPiece))
            {
                buildingPiece.Configure(piece);
            }
            else
            {
                Debug.LogError($"Prefab for '{piece.DisplayName}' has no BuildingPiece component.", instance);
            }

            Debug.Log($"{name} built {piece.DisplayName}.", this);
        }

        private void SpawnGhost()
        {
            DestroyGhost();

            BuildingPieceDefinition piece = SelectedPiece;
            if (piece == null || piece.Prefab == null)
            {
                return;
            }

            _ghostInstance = Instantiate(piece.Prefab);
            _ghostInstance.name = $"{piece.Prefab.name} (Ghost)";

            // Bounds are read right after Instantiate, while the ghost still sits at the
            // identity transform Instantiate(prefab) gives it - at that instant world bounds
            // and "local, unrotated" bounds are the same thing. Assumes pieces only ever yaw
            // (RotatePiece is Y-only), so this half-height stays valid at any yaw. Must run
            // BEFORE colliders are disabled below - a disabled Collider's .bounds collapses to
            // zero, which silently zeroed this out and put the ghost's pivot (not its bottom)
            // back on the hit point, exactly the sinking-into-the-ground bug this was meant to
            // fix. Renderer.bounds is used instead of Collider.bounds for the same reason and
            // because it's the visible mesh that actually needs to sit on the surface.
            _pieceHalfHeight = ComputeHalfHeight(_ghostInstance);

            foreach (Collider col in _ghostInstance.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            foreach (Rigidbody rb in _ghostInstance.GetComponentsInChildren<Rigidbody>())
            {
                rb.isKinematic = true;
            }

            _ghostSnapPoints.Clear();
            _ghostSnapPoints.AddRange(_ghostInstance.GetComponentsInChildren<BuildingSnapPoint>());

            _ghostAppliedValid = null;
            _ghostInstance.SetActive(false);
        }

        private static float ComputeHalfHeight(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return 0f;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds.extents.y;
        }

        private void DestroyGhost()
        {
            if (_ghostInstance != null)
            {
                Destroy(_ghostInstance);
                _ghostInstance = null;
            }

            _ghostSnapPoints.Clear();
            IsSnapped = false;
        }

        private void UpdateGhost()
        {
            if (_ghostInstance == null)
            {
                return;
            }

            _hasValidHit = Physics.Raycast(_cameraTransform.position, _cameraTransform.forward, out RaycastHit hit, _maxPlacementDistance, _placementLayers, QueryTriggerInteraction.Ignore);

            if (!_hasValidHit)
            {
                _ghostInstance.SetActive(false);
                IsSnapped = false;
                return;
            }

            Vector3 anchoredPosition = hit.point + Vector3.up * _pieceHalfHeight;

            _ghostInstance.SetActive(true);
            _ghostInstance.transform.SetPositionAndRotation(anchoredPosition, GhostRotation());

            TryApplySnap();

            bool canPlaceNow = CanPlace;
            if (_ghostAppliedValid != canPlaceNow)
            {
                ApplyGhostMaterial(canPlaceNow ? _validGhostMaterial : _invalidGhostMaterial);
                _ghostAppliedValid = canPlaceNow;
            }
        }

        /// <summary>
        /// If any of the ghost's own snap points lies within _snapRadius of a snap point
        /// belonging to an already-placed piece, snaps the whole ghost onto it - both position
        /// AND rotation, so e.g. two foundations end up edge-to-edge and parallel instead of
        /// just touching at a corner at whatever angle the player was holding it. Rotation
        /// comes from the target snap point's own world rotation (parent piece rotation +
        /// the point's own local rotation), not just the parent's - that per-point rotation is
        /// what lets, say, a foundation's four edge points each orient an attaching wall
        /// correctly regardless of which of the four edges it's snapping to (see
        /// UNITY_SETUP_NEXT_STEPS.md's snap point authoring steps for how those are set up).
        /// Only the closest pair across all nearby pieces wins.
        /// </summary>
        private void TryApplySnap()
        {
            IsSnapped = false;

            if (_ghostSnapPoints.Count == 0)
            {
                return;
            }

            float bestSqrDistance = _snapRadius * _snapRadius;
            BuildingSnapPoint bestGhostPoint = null;
            BuildingSnapPoint bestTargetPoint = null;

            foreach (BuildingPiece placed in BuildingPiece.AllPieces)
            {
                if (placed == null)
                {
                    continue;
                }

                // Cheap broad-phase cull before touching its snap points - keeps the search
                // bounded regardless of how many pieces exist in the world.
                if (Vector3.Distance(placed.transform.position, _ghostInstance.transform.position) > _snapSearchRadius)
                {
                    continue;
                }

                foreach (BuildingSnapPoint targetPoint in placed.SnapPoints)
                {
                    if (targetPoint == null)
                    {
                        continue;
                    }

                    foreach (BuildingSnapPoint ghostPoint in _ghostSnapPoints)
                    {
                        float sqrDistance = (targetPoint.transform.position - ghostPoint.transform.position).sqrMagnitude;
                        if (sqrDistance < bestSqrDistance)
                        {
                            bestSqrDistance = sqrDistance;
                            bestGhostPoint = ghostPoint;
                            bestTargetPoint = targetPoint;
                        }
                    }
                }
            }

            if (bestGhostPoint == null)
            {
                return;
            }

            // Orient first - rotating the ghost moves every child snap point with it, so the
            // ghost snap point's world position has to be re-read afterward, not reused from
            // the search above, or the translate step below would align against a stale
            // (pre-rotation) position and miss.
            _ghostInstance.transform.rotation = bestTargetPoint.transform.rotation;
            _ghostInstance.transform.position += bestTargetPoint.transform.position - bestGhostPoint.transform.position;
            IsSnapped = true;
        }

        private Quaternion GhostRotation()
        {
            return Quaternion.Euler(0f, _cameraTransform.eulerAngles.y + _ghostYawOffset, 0f);
        }

        private void ApplyGhostMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            foreach (Renderer renderer in _ghostInstance.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial = material;
            }
        }
    }
}
