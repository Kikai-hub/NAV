using UnityEngine;
using NAV.Gameplay.Combat;
using NAV.Gameplay.Equipment;

namespace NAV.Gameplay.Items
{
    /// <summary>
    /// A world resource node (tree, rock, bush, ...) that absorbs several gather-hits before
    /// being felled/exhausted. Gathering is done with tools only, Valheim-style: a melee swing
    /// whose weapon physically touches the node (PlayerCombat contact hit, IMeleeHitReceiver) is
    /// one gather-hit. It is deliberately NOT an IInteractable (since 2026-10-21, developer's
    /// request): E is only for picking up, opening and using things, so a node shows no E prompt
    /// and E does nothing to it. Interim hits before depletion give no reward, only
    /// depletion-feedback (shrink, for definitions that want it); on the hit that empties the
    /// node it disappears and scatters its Definition.Drops into the world as physical
    /// ItemPickup objects the player picks up - not auto-added to inventory.
    /// Definition.RequiredTool must be held in the main hand (PlayerEquipment) for a hit to
    /// count - trees need an Axe, stone/ore a Pickaxe; a wrong-tool swing bounces off (dull
    /// clunk, see HitFeedback). Depletion persistence is handled by WorldGenerator/SaveManager
    /// (spawn-index ids), not here.
    ///
    /// Depletion can also chain into a next stage (Definition.BecomesPrefab / LeavesBehindPrefab,
    /// see SpawnNextStage): a felled tree leaves a stump and becomes a falling trunk, which is
    /// itself a ResourceNode (with a Rigidbody) that splits into logs, which drop the wood. Only
    /// the original world node is tracked by saves; the later stages, like items on the ground,
    /// aren't saved yet.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ResourceNode : MonoBehaviour, IMeleeHitReceiver, IHitSurface
    {
        [SerializeField] private ResourceNodeDefinition _definition;

        [Header("Depletion Drop")]
        [SerializeField] private float _dropScatterRadius = 1f;
        [SerializeField] private float _dropScatterForce = 1.5f;
        [SerializeField] private float _dropUpwardForce = 2f;

        [Header("Next Stage")]
        [Tooltip("Where Definition.BecomesPrefab is spawned on depletion, one instance per point (position + rotation) - e.g. the two log positions along a fallen trunk. Empty = one instance at this node's own pose.")]
        [SerializeField] private Transform[] _becomesSpawnPoints = new Transform[0];

        // A stage spawned by a depleting hit would otherwise take a hit from the rest of that same
        // swing (the weapon is still sweeping through where the tree stood).
        private const float SpawnGraceSeconds = 0.4f;

        private int _remainingHits;
        private Vector3 _initialScale;
        private float _spawnTime;

        public ResourceNodeDefinition Definition => _definition;

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
            _spawnTime = Time.time;
        }

        /// <summary>Wood for axe nodes (trees), stone for pickaxe nodes (rock, flint, ore) -
        /// derived from the required tool so no asset needs a new field.</summary>
        public HitSurface Surface => _definition == null ? HitSurface.Other
            : _definition.RequiredTool == ToolType.Axe ? HitSurface.Wood
            : _definition.RequiredTool == ToolType.Pickaxe ? HitSurface.Stone
            : HitSurface.Other;

        public bool ReceiveMeleeHit(GameObject attacker, WeaponDefinition weapon)
        {
            return TryGather(attacker);
        }

        /// <summary>One gather-hit. Returns false when it didn't count (already depleted, or
        /// the wrong/missing tool).</summary>
        private bool TryGather(GameObject interactor)
        {
            // Already depleted (Destroy only happens at the end of the frame), or just spawned.
            if (_remainingHits <= 0 || Time.time - _spawnTime < SpawnGraceSeconds)
            {
                return false;
            }

            if (!HasRequiredTool(interactor))
            {
                Debug.Log($"{interactor.name} needs a {_definition.RequiredTool} (tier {_definition.MinToolTier}+) to gather {_definition.DisplayName}.", this);
                return false;
            }

            _remainingHits--;

            if (_remainingHits > 0)
            {
                if (_definition.ShrinkOnHit)
                {
                    ApplyDepletionFeedback();
                }

                return true;
            }

            Debug.Log($"{name} was depleted by {interactor.name} and scattered its yield.", this);
            SpawnDrops();
            SpawnNextStage(interactor);
            Destroy(gameObject);
            return true;
        }

        /// <summary>Leaves the stump (Definition.LeavesBehindPrefab) and spawns the next stage
        /// (Definition.BecomesPrefab) at each spawn point. A next stage with a Rigidbody inherits
        /// this node's own motion (a log split from a still-rolling trunk keeps rolling) and gets
        /// Definition.FallImpulse pushed into its top, away from the interactor - that's what
        /// topples a felled tree away from the player.</summary>
        private void SpawnNextStage(GameObject interactor)
        {
            if (_definition.LeavesBehindPrefab != null)
            {
                // Same parent/layer as the node, so render-distance culling treats it alike.
                GameObject leftover = Instantiate(_definition.LeavesBehindPrefab, transform.position, transform.rotation, transform.parent);
                InheritScale(leftover.transform);
                SetLayerRecursively(leftover.transform, gameObject.layer);
            }

            if (_definition.BecomesPrefab == null)
            {
                return;
            }

            Vector3 away = interactor != null ? transform.position - interactor.transform.position : transform.forward;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;

            if (_becomesSpawnPoints.Length == 0)
            {
                SpawnStageAt(transform.position, transform.rotation, away);
                return;
            }

            foreach (Transform point in _becomesSpawnPoints)
            {
                if (point != null)
                {
                    SpawnStageAt(point.position, point.rotation, away);
                }
            }
        }

        /// <summary>WorldGenerator varies tree sizes (BiomeResourceSpawn.Scale) - the stump,
        /// felled trunk and its logs keep this node's size so a big tree leaves a big trunk.</summary>
        private void InheritScale(Transform spawned)
        {
            spawned.localScale = Vector3.Scale(spawned.localScale, transform.localScale);
        }

        private void SpawnStageAt(Vector3 position, Quaternion rotation, Vector3 away)
        {
            GameObject instance = Instantiate(_definition.BecomesPrefab, position, rotation, transform.parent);
            InheritScale(instance.transform);
            SetLayerRecursively(instance.transform, gameObject.layer);
            var body = instance.GetComponent<Rigidbody>();
            if (body == null)
            {
                return;
            }

            var ownBody = GetComponent<Rigidbody>();
            if (ownBody != null)
            {
                body.linearVelocity = ownBody.GetPointVelocity(position);
                body.angularVelocity = ownBody.angularVelocity;
            }

            if (_definition.FallImpulse > 0f)
            {
                // Pushed at the top so it tips over rather than sliding.
                var collider = instance.GetComponent<Collider>();
                Vector3 top = collider != null ? new Vector3(position.x, collider.bounds.max.y, position.z) : position + Vector3.up;
                body.AddForceAtPosition(away * _definition.FallImpulse, top, ForceMode.Impulse);
            }
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

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
            {
                SetLayerRecursively(child, layer);
            }
        }

        private bool HasRequiredTool(GameObject interactor)
        {
            if (_definition.RequiredTool == ToolType.None)
            {
                return true;
            }

            var equipment = interactor != null ? interactor.GetComponent<PlayerEquipment>() : null;
            return equipment != null && equipment.HasTool(_definition.RequiredTool, _definition.MinToolTier);
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
