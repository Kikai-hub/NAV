using System;
using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Equipment;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;
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
    ///
    /// Building needs the hammer (2026-10-18): build mode is only available while a Hammer
    /// tool is held in the main hand (PlayerEquipment). RMB (PlayerInputHandler.Secondary)
    /// toggles it on and off - the old B toggle is no longer listened to - and putting the
    /// hammer away (switching items, dropping it) leaves build mode. In build mode LMB places
    /// the piece (PiecePlaced fires - PlayerAnimator plays a hammer strike) with a short
    /// cooldown matching that strike.
    ///
    /// Build menu (BuildMenuUIController, opened with the inventory key in build mode) picks
    /// either a piece (SelectPiece) or the Demolish tool (SelectDemolish). With Demolish the
    /// ghost is hidden, the placed piece under the crosshair is tinted red (DemolishTarget) and
    /// LMB removes it and drops its full cost as pickups where it stood (not straight into the
    /// inventory) - Valheim-style. PieceDemolished fires for the same hammer strike.
    ///
    /// Build area (2026-10-18): a piece whose definition RequiresWorkbench can only be placed
    /// inside a placed workbench's BuildRadius (Workbench.AnyBuildAreaContains) - the ghost goes
    /// red and PlacementHint explains why. The workbench and campfire themselves can go anywhere.
    /// </summary>
    public class PlayerBuilding : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private PlayerInventory _playerInventory;
        [SerializeField] private Transform _cameraTransform;
        [Tooltip("The held item - build mode needs a Hammer tool in the main hand.")]
        [SerializeField] private PlayerEquipment _equipment;
        [SerializeField] private List<BuildingPieceDefinition> _knownPieces = new();

        [Header("Placement")]
        [Tooltip("Seconds between two placements - about the length of the hammer strike animation.")]
        [SerializeField, Min(0f)] private float _placeCooldown = 0.6f;
        [SerializeField] private float _maxPlacementDistance = 15f;
        [SerializeField] private float _rotationStepDegrees = 45f;
        [SerializeField] private LayerMask _placementLayers = ~0;

        [Header("Snapping")]
        [SerializeField] private float _snapRadius = 0.75f;
        [SerializeField] private float _snapSearchRadius = 6f;

        [Header("Ghost Materials")]
        [SerializeField] private Material _validGhostMaterial;
        [SerializeField] private Material _invalidGhostMaterial;

        [Header("Demolish")]
        [Tooltip("How far from the player (not the camera) a piece can be demolished.")]
        [SerializeField, Min(0.5f)] private float _demolishRange = 8f;
        [Tooltip("Tint applied to the piece that LMB would demolish.")]
        [SerializeField] private Color _demolishTint = new(1f, 0.35f, 0.3f, 1f);
        [Tooltip("Sideways push on the materials dropped by a demolished piece.")]
        [SerializeField, Min(0f)] private float _dropScatterForce = 1.5f;
        [Tooltip("Upward push on the materials dropped by a demolished piece.")]
        [SerializeField, Min(0f)] private float _dropUpwardForce = 2f;

        private int _selectedIndex;
        private GameObject _ghostInstance;
        private readonly List<BuildingSnapPoint> _ghostSnapPoints = new();
        private float _ghostYawOffset;
        private float _pieceHalfHeight;
        private bool _hasValidHit;
        private bool? _ghostAppliedValid;
        private float _nextPlaceTime;
        private bool _demolishSelected;
        private BuildingPiece _demolishTarget;
        private MaterialPropertyBlock _tintBlock;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorId = Shader.PropertyToID("_Color");

        /// <summary>Fires after a piece is built - for the hammer strike animation/sound.</summary>
        public event Action<BuildingPieceDefinition> PiecePlaced;

        /// <summary>Fires after a piece is demolished with the hammer.</summary>
        public event Action<BuildingPieceDefinition> PieceDemolished;

        /// <summary>Build mode turned on/off, or the selection (piece / Demolish) changed - for
        /// the build menu.</summary>
        public event Action StateChanged;

        public bool IsBuildModeActive { get; private set; }

        public IReadOnlyList<BuildingPieceDefinition> KnownPieces => _knownPieces;
        public int SelectedIndex => _selectedIndex;

        /// <summary>The Demolish tool is selected instead of a piece.</summary>
        public bool IsDemolishSelected => _demolishSelected;

        /// <summary>The placed piece LMB would demolish right now, or null.</summary>
        public BuildingPiece DemolishTarget => _demolishTarget;

        /// <summary>A Hammer tool is in the main hand - build mode can be used.</summary>
        public bool HoldsHammer => _equipment != null && _equipment.HasTool(ToolType.Hammer);

        /// <summary>RMB belongs to building (toggling build mode) right now, not to blocking -
        /// PlayerCombat checks this.</summary>
        public bool UsesSecondaryAction => HoldsHammer || IsBuildModeActive;
        public BuildingPieceDefinition SelectedPiece => _knownPieces.Count > 0 ? _knownPieces[_selectedIndex] : null;
        public bool IsPlacementValid => _hasValidHit;
        public bool IsSnapped { get; private set; }
        public bool CanAffordSelected => SelectedPiece != null && SelectedPiece.CanAfford(_playerInventory.Inventory);
        public bool CanPlace => IsBuildModeActive && !_demolishSelected && _hasValidHit && CanAffordSelected && IsInBuildArea;

        /// <summary>The ghost stands where the selected piece may go: anywhere for pieces that
        /// don't need a workbench, else inside some placed workbench's build radius.</summary>
        public bool IsInBuildArea => SelectedPiece == null || !SelectedPiece.RequiresWorkbench
            || (_ghostInstance != null && NAV.Gameplay.Crafting.Workbench.AnyBuildAreaContains(_ghostInstance.transform.position));

        /// <summary>Why the selected piece can't be placed right now (for the HUD), or null
        /// when it can (or build mode is off / Demolish is selected).</summary>
        public string PlacementHint
        {
            get
            {
                if (!IsBuildModeActive || _demolishSelected || SelectedPiece == null)
                {
                    return null;
                }

                if (!_hasValidHit)
                {
                    return "Aim at the ground";
                }

                if (!IsInBuildArea)
                {
                    return "Needs a workbench nearby";
                }

                return CanAffordSelected ? null : "Not enough materials";
            }
        }

        private void Awake()
        {
            if (_inputHandler == null || _playerInventory == null || _cameraTransform == null || _equipment == null)
            {
                Debug.LogError($"{nameof(PlayerBuilding)} on '{name}' is missing a required reference (InputHandler/PlayerInventory/CameraTransform/Equipment).", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_inputHandler == null || _equipment == null)
            {
                return;
            }

            _inputHandler.SecondaryPerformed += HandleToggleBuild;
            _inputHandler.AttackPerformed += HandlePlace;
            _inputHandler.RotatePiecePerformed += HandleRotate;
            _inputHandler.CycleNextPerformed += HandleCycleNext;
            _inputHandler.CyclePreviousPerformed += HandleCyclePrevious;
            _equipment.Changed += HandleEquipmentChanged;
        }

        private void OnDisable()
        {
            if (_inputHandler == null || _equipment == null)
            {
                return;
            }

            _inputHandler.SecondaryPerformed -= HandleToggleBuild;
            _inputHandler.AttackPerformed -= HandlePlace;
            _inputHandler.RotatePiecePerformed -= HandleRotate;
            _inputHandler.CycleNextPerformed -= HandleCycleNext;
            _inputHandler.CyclePreviousPerformed -= HandleCyclePrevious;
            _equipment.Changed -= HandleEquipmentChanged;

            // Disabled (e.g. on death) - leave build mode entirely, not just hide the ghost.
            ExitBuildMode();
        }

        private void HandleEquipmentChanged()
        {
            if (IsBuildModeActive && !HoldsHammer)
            {
                ExitBuildMode();
            }
        }

        private void Update()
        {
            if (!IsBuildModeActive)
            {
                return;
            }

            if (_demolishSelected)
            {
                UpdateDemolishTarget();
            }
            else
            {
                UpdateGhost();
            }
        }

        /// <summary>Build menu: switch to placing this piece.</summary>
        public void SelectPiece(int index)
        {
            if (index < 0 || index >= _knownPieces.Count)
            {
                return;
            }

            _demolishSelected = false;
            SetDemolishTarget(null);
            _selectedIndex = index;
            if (IsBuildModeActive)
            {
                SpawnGhost();
            }

            StateChanged?.Invoke();
        }

        /// <summary>Build menu: switch to the Demolish tool.</summary>
        public void SelectDemolish()
        {
            _demolishSelected = true;
            DestroyGhost();
            StateChanged?.Invoke();
        }

        private void HandleToggleBuild()
        {
            if (IsBuildModeActive)
            {
                ExitBuildMode();
            }
            else if (HoldsHammer)
            {
                EnterBuildMode();
            }
        }

        private void EnterBuildMode()
        {
            if (!HoldsHammer)
            {
                return;
            }

            if (_knownPieces.Count == 0)
            {
                Debug.LogWarning($"{nameof(PlayerBuilding)} on '{name}' has no known building pieces to place.", this);
                return;
            }

            IsBuildModeActive = true;
            _ghostYawOffset = 0f;
            if (!_demolishSelected)
            {
                SpawnGhost();
            }

            StateChanged?.Invoke();
        }

        private void ExitBuildMode()
        {
            bool wasActive = IsBuildModeActive;
            IsBuildModeActive = false;
            DestroyGhost();
            SetDemolishTarget(null);
            if (wasActive)
            {
                StateChanged?.Invoke();
            }
        }

        // The wheel steps through the pieces (and leaves the Demolish tool).
        private void HandleCycleNext()
        {
            if (!IsBuildModeActive || _knownPieces.Count == 0)
            {
                return;
            }

            SelectPiece(_demolishSelected ? _selectedIndex : (_selectedIndex + 1) % _knownPieces.Count);
        }

        private void HandleCyclePrevious()
        {
            if (!IsBuildModeActive || _knownPieces.Count == 0)
            {
                return;
            }

            SelectPiece(_demolishSelected ? _selectedIndex : (_selectedIndex - 1 + _knownPieces.Count) % _knownPieces.Count);
        }

        private void HandleRotate()
        {
            if (!IsBuildModeActive || _demolishSelected)
            {
                return;
            }

            _ghostYawOffset = (_ghostYawOffset + _rotationStepDegrees) % 360f;
        }

        private void HandlePlace()
        {
            if (IsBuildModeActive && _demolishSelected)
            {
                TryDemolishTarget();
                return;
            }

            if (!CanPlace || Time.time < _nextPlaceTime)
            {
                return;
            }

            _nextPlaceTime = Time.time + _placeCooldown;
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
            PiecePlaced?.Invoke(piece);
        }

        // --- Demolish ---

        private void UpdateDemolishTarget()
        {
            BuildingPiece target = null;
            if (Physics.Raycast(_cameraTransform.position, _cameraTransform.forward, out RaycastHit hit, _maxPlacementDistance, _placementLayers, QueryTriggerInteraction.Ignore)
                && Vector3.Distance(transform.position, hit.point) <= _demolishRange)
            {
                BuildingPiece piece = hit.collider.GetComponentInParent<BuildingPiece>();
                if (piece != null && piece.IsPlaced)
                {
                    target = piece;
                }
            }

            SetDemolishTarget(target);
        }

        private void TryDemolishTarget()
        {
            if (_demolishTarget == null || Time.time < _nextPlaceTime)
            {
                return;
            }

            _nextPlaceTime = Time.time + _placeCooldown;
            BuildingPiece piece = _demolishTarget;
            BuildingPieceDefinition definition = piece.Definition;
            SetDemolishTarget(null);

            Collider pieceCollider = piece.GetComponentInChildren<Collider>();
            Bounds bounds = pieceCollider != null ? pieceCollider.bounds : new Bounds(piece.transform.position, Vector3.one);
            // Deactivate first: Destroy only happens at the end of the frame, and the drops
            // spawned inside the still-solid piece would be shoved out by depenetration.
            piece.gameObject.SetActive(false);
            Destroy(piece.gameObject);
            DropCost(definition, bounds);

            // A chest spills what it held rather than deleting it.
            var storage = piece.GetComponent<NAV.Gameplay.Inventory.StorageContainer>();
            if (storage != null)
            {
                float scatter = Mathf.Clamp(Mathf.Min(bounds.extents.x, bounds.extents.z), 0.3f, 1.2f);
                storage.DropContents(bounds.center, scatter, _dropScatterForce, _dropUpwardForce);
            }
            Debug.Log($"{name} demolished {definition.DisplayName} (materials dropped).", this);
            PieceDemolished?.Invoke(definition);
        }

        /// <summary>Drops the piece's whole cost into the world as pickups (one stack per
        /// ingredient), thrown out from where the piece stood - the player picks them up like any
        /// other drop (same as a felled tree's yield, ResourceNode.SpawnDrops), nothing goes
        /// straight into the inventory.</summary>
        private void DropCost(BuildingPieceDefinition definition, Bounds pieceBounds)
        {
            float scatter = Mathf.Clamp(Mathf.Min(pieceBounds.extents.x, pieceBounds.extents.z), 0.3f, 1.2f);
            foreach (NAV.Gameplay.Crafting.RecipeIngredient ingredient in definition.Cost)
            {
                if (ingredient.Item == null || ingredient.Amount <= 0)
                {
                    continue;
                }

                Vector2 offset = UnityEngine.Random.insideUnitCircle * scatter;
                Vector3 outward = new Vector3(offset.x, 0f, offset.y);
                Vector3 position = pieceBounds.center + outward;
                Vector3 impulse = (outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward) * _dropScatterForce
                    + Vector3.up * _dropUpwardForce;
                ItemPickup.SpawnInWorld(ingredient.Item, ingredient.Amount, position, Quaternion.identity, impulse);
            }
        }

        private void SetDemolishTarget(BuildingPiece target)
        {
            if (target == _demolishTarget)
            {
                return;
            }

            if (_demolishTarget != null)
            {
                foreach (Renderer renderer in _demolishTarget.GetComponentsInChildren<Renderer>())
                {
                    renderer.SetPropertyBlock(null);
                }
            }

            _demolishTarget = target;
            if (_demolishTarget == null)
            {
                return;
            }

            _tintBlock ??= new MaterialPropertyBlock();
            foreach (Renderer renderer in _demolishTarget.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(_tintBlock);
                _tintBlock.SetColor(BaseColorId, _demolishTint);
                _tintBlock.SetColor(LegacyColorId, _demolishTint);
                renderer.SetPropertyBlock(_tintBlock);
            }
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

            // The ghost is a visual only: switch off every behaviour on it except the snap
            // points (data the snap search reads). Otherwise the copy's own scripts run - its
            // BuildingPiece registered as a placed piece (the prefab carries its Definition) and
            // a workbench ghost counted as a real workbench, opening a build area wherever the
            // player merely aimed it.
            foreach (MonoBehaviour behaviour in _ghostInstance.GetComponentsInChildren<MonoBehaviour>())
            {
                if (behaviour is not BuildingSnapPoint)
                {
                    behaviour.enabled = false;
                }
            }

            _ghostSnapPoints.Clear();
            _ghostSnapPoints.AddRange(_ghostInstance.GetComponentsInChildren<BuildingSnapPoint>());

            _ghostAppliedValid = null;
            _ghostInstance.SetActive(false);
        }

        // Uses the piece's own Collider bounds, not its Renderer(s) - every piece is
        // guaranteed exactly one via [RequireComponent(typeof(Collider))], and unlike a
        // Renderer it stays a clean, level footprint no matter what a piece's *visual* mesh
        // looks like (e.g. a purely cosmetic tilted child mesh on a roof piece). Bounds are
        // still read right after Instantiate, before anything below disables the collider -
        // a disabled Collider's .bounds collapses to zero, which is exactly the silent-zero
        // bug this method was already written once to avoid (see the SpawnGhost comment).
        private static float ComputeHalfHeight(GameObject instance)
        {
            Collider collider = instance.GetComponentInChildren<Collider>();
            return collider != null ? collider.bounds.extents.y : 0f;
        }

        private void DestroyGhost()
        {
            if (_ghostInstance != null)
            {
                // OnDisable can run while Play Mode is shutting down (edit mode), where Destroy
                // is not allowed.
                if (Application.isPlaying)
                {
                    Destroy(_ghostInstance);
                }
                else
                {
                    DestroyImmediate(_ghostInstance);
                }

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
                        // Only match points of the same Kind - e.g. a Palisade's Corner points
                        // should only pull toward a Foundation's actual corners, never its
                        // edge midpoints (which Walls/Doors/Roof use). Without this, whichever
                        // point is merely closest wins regardless of what it conceptually is.
                        if (targetPoint.Kind != ghostPoint.Kind)
                        {
                            continue;
                        }

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
