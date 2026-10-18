using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Building;

namespace NAV.Gameplay.Crafting
{
    /// <summary>
    /// A world-space workbench that grants nearby players access to recipes gated by
    /// RecipeDefinition.RequiredWorkbenchTier. Proximity is a trigger volume, not a raycast
    /// interaction (unlike ItemPickup/ResourceNode) - crafting should work while the player is
    /// simply standing near the bench, not looking straight at it. CharacterController
    /// (Player's collider) inherits from Collider, so it raises the same OnTriggerEnter/Exit
    /// events as any other collider without needing a Rigidbody on either side.
    ///
    /// Valheim rules (2026-10-18):
    ///   - Build area: building pieces that RequireWorkbench can only be placed within
    ///     BuildRadius of a placed workbench (PlayerBuilding asks IsInBuildArea).
    ///   - Shelter: if the definition RequiresRoof, crafting only works while the bench is
    ///     under a roof - several rays cast straight up from above the bench must hit a placed
    ///     building piece (not the bench itself, terrain or trees). Re-checked twice a second;
    ///     when it changes, players in range are told so the crafting panel updates.
    ///     UsableTier is 0 while unsheltered.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class Workbench : MonoBehaviour
    {
        /// <summary>Every enabled, real (not a build-ghost preview) workbench.</summary>
        public static readonly List<Workbench> AllWorkbenches = new();

        private const float ShelterCheckInterval = 0.5f;
        private const float RoofRayStartHeight = 1.4f;
        private const float RoofRayLength = 25f;
        private const float RoofRayOffset = 0.45f;
        // Rays (out of RoofRayOffsets.Length) that must hit a roof - forgives a small gap.
        private const int RoofRaysNeeded = 4;

        private static readonly Vector2[] RoofRayOffsets =
        {
            Vector2.zero, new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f),
        };

        [SerializeField] private WorkbenchDefinition _definition;

        private readonly HashSet<PlayerCrafting> _playersInRange = new();
        private readonly RaycastHit[] _roofHits = new RaycastHit[16];
        private BuildingPiece _ownPiece;
        private float _nextShelterCheck;
        private bool _registered;

        public int Tier => _definition != null ? _definition.Tier : 0;
        public string DisplayName => _definition != null ? _definition.DisplayName : name;
        public float BuildRadius => _definition != null ? _definition.BuildRadius : 0f;

        /// <summary>Under a roof (or the definition doesn't require one).</summary>
        public bool IsSheltered { get; private set; }

        /// <summary>The tier crafting can use here: Tier when sheltered, else 0.</summary>
        public int UsableTier => IsSheltered ? Tier : 0;

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
            _ownPiece = GetComponentInParent<BuildingPiece>();
            IsSheltered = !_definition.RequiresRoof;
        }

        private void OnEnable()
        {
            _nextShelterCheck = 0f;
        }

        private void Update()
        {
            // A build-ghost preview also carries this component; it must never count as a bench.
            bool real = _ownPiece == null || _ownPiece.IsPlaced;
            if (real != _registered)
            {
                _registered = real;
                if (real)
                {
                    AllWorkbenches.Add(this);
                }
                else
                {
                    AllWorkbenches.Remove(this);
                }
            }

            if (!real || Time.time < _nextShelterCheck)
            {
                return;
            }

            _nextShelterCheck = Time.time + ShelterCheckInterval;
            bool sheltered = !_definition.RequiresRoof || HasRoof();
            if (sheltered != IsSheltered)
            {
                IsSheltered = sheltered;
                foreach (PlayerCrafting crafting in _playersInRange)
                {
                    if (crafting != null)
                    {
                        crafting.NotifyWorkbenchChanged();
                    }
                }
            }
        }

        /// <summary>True if position is within this bench's build area.</summary>
        public bool IsInBuildArea(Vector3 position)
        {
            return (position - transform.position).sqrMagnitude <= BuildRadius * BuildRadius;
        }

        /// <summary>True if any real workbench's build area contains position.</summary>
        public static bool AnyBuildAreaContains(Vector3 position)
        {
            foreach (Workbench workbench in AllWorkbenches)
            {
                if (workbench != null && workbench.IsInBuildArea(position))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasRoof()
        {
            int covered = 0;
            Vector3 origin = transform.position + Vector3.up * RoofRayStartHeight;
            foreach (Vector2 offset in RoofRayOffsets)
            {
                Vector3 start = origin + new Vector3(offset.x, 0f, offset.y) * RoofRayOffset;
                if (RayHitsRoof(start))
                {
                    covered++;
                }
            }

            return covered >= RoofRaysNeeded;
        }

        // The first solid thing straight above must be a placed building piece other than this
        // bench's own; terrain, trees or nothing at all mean open sky.
        private bool RayHitsRoof(Vector3 start)
        {
            int count = Physics.RaycastNonAlloc(start, Vector3.up, _roofHits, RoofRayLength, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool roof = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _roofHits[i];
                if (hit.distance >= nearest)
                {
                    continue;
                }

                BuildingPiece piece = hit.collider.GetComponentInParent<BuildingPiece>();
                if (piece != null && piece == _ownPiece)
                {
                    continue;
                }

                // Characters (the player standing on the roof, creatures) aren't a roof.
                if (hit.collider is CharacterController || hit.collider.attachedRigidbody != null)
                {
                    continue;
                }

                nearest = hit.distance;
                roof = piece != null && piece.IsPlaced;
            }

            return roof;
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerCrafting crafting = other.GetComponentInParent<PlayerCrafting>();
            if (crafting != null && _playersInRange.Add(crafting))
            {
                crafting.RegisterNearbyWorkbench(this);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerCrafting crafting = other.GetComponentInParent<PlayerCrafting>();
            if (crafting != null && _playersInRange.Remove(crafting))
            {
                crafting.UnregisterNearbyWorkbench(this);
            }
        }

        // A placed workbench can be demolished while the player stands inside its range -
        // OnTriggerExit never fires for a destroyed collider, so without this the player would
        // keep a stale registration and the crafting panel would not refresh.
        private void OnDisable()
        {
            if (_registered)
            {
                AllWorkbenches.Remove(this);
                _registered = false;
            }

            foreach (PlayerCrafting crafting in _playersInRange)
            {
                if (crafting != null)
                {
                    crafting.UnregisterNearbyWorkbench(this);
                }
            }

            _playersInRange.Clear();
        }
    }
}
