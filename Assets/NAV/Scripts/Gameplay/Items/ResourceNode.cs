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

        /// <summary>Any node gathered out (by a hit on this machine), just before it is replaced
        /// by its next stage - multiplayer removes the same node on the other machines.</summary>
        public static event System.Action<ResourceNode> Depleted;

        /// <summary>A node gathered out on ANOTHER machine, shown here (stump, no stages, no
        /// drops) - so whatever counts felled trees here (ForestWrath) counts it too.</summary>
        public static event System.Action<ResourceNode> DepletedRemotely;

        /// <summary>The next stages a depletion spawned on this machine (a falling trunk, its
        /// logs) - multiplayer gives them network ids and shows them on the other machines.</summary>
        public static event System.Action<ResourceNode, System.Collections.Generic.List<GameObject>> StagesSpawned;

        /// <summary>Multiplayer: when set, decides about every gather hit landed here (see
        /// IResourceNodeNetwork). Null offline - everything is local.</summary>
        public static IResourceNodeNetwork Network { get; set; }

        /// <summary>A stage spawned on another machine and shown here (kinematic, moved by the
        /// network): its hits are sent to the owner, it never depletes on its own.</summary>
        public bool IsNetworkCopy { get; set; }

        /// <summary>Network id of a stage (trunk, log); 0 for world nodes and offline.</summary>
        public long NetworkId { get; set; }

        public int RemainingHits => _remainingHits;

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

            // A better tool than needed works faster: each tier above the minimum is one more hit
            // (a copper axe fells a tree in fewer swings than a stone one).
            int hits = 1 + ExtraHitsFromTool(interactor);

            // Online: the network decides (a shared count, felled once for everyone). Here the
            // hit only shows - the count drops as a guess, the authority's answer corrects it.
            if (Network != null && Network.InterceptGather(this, interactor, hits))
            {
                SetRemainingHits(Mathf.Max(1, _remainingHits - hits));
                return true;
            }

            ApplyGatherHits(hits, interactor != null ? interactor.transform.position : transform.position - transform.forward);
            return true;
        }

        /// <summary>Gather hits already judged valid (here, or by the owner of this node on
        /// another machine): counts them and depletes the node when it is empty - drops, next
        /// stages falling away from fromPosition.</summary>
        public void ApplyGatherHits(int hits, Vector3 fromPosition)
        {
            if (_remainingHits <= 0)
            {
                return;
            }

            _remainingHits -= hits;
            if (_remainingHits > 0)
            {
                if (_definition.ShrinkOnHit)
                {
                    ApplyDepletionFeedback();
                }

                return;
            }

            DepleteBy(fromPosition, true);
        }

        /// <summary>The count as the authority has it (multiplayer) - shrinks to match.</summary>
        public void SetRemainingHits(int remaining)
        {
            _remainingHits = Mathf.Clamp(remaining, 0, _definition.HitsToDeplete);
            if (_remainingHits > 0 && _definition.ShrinkOnHit)
            {
                ApplyDepletionFeedback();
            }
        }

        /// <summary>Depletes it now. owner: this machine makes the result - drops and the next
        /// stages (the trunk falls away from fromPosition); otherwise only the leftover (stump)
        /// stays, the result comes from the machine that made it.</summary>
        public void DepleteBy(Vector3 fromPosition, bool owner)
        {
            if (this == null || !gameObject.activeSelf)
            {
                return;
            }

            _remainingHits = 0;
            if (owner)
            {
                Debug.Log($"{name} was depleted and scattered its yield.", this);
                Depleted?.Invoke(this);
                SpawnDrops();
                SpawnNextStage(fromPosition);
            }
            else
            {
                DepletedRemotely?.Invoke(this);
                LeaveLeftover();
            }

            gameObject.SetActive(false); // gone this frame (a second hit in the same swing finds nothing)
            Destroy(gameObject);
        }

        /// <summary>Leaves the stump (Definition.LeavesBehindPrefab) and spawns the next stage
        /// (Definition.BecomesPrefab) at each spawn point. A next stage with a Rigidbody inherits
        /// this node's own motion (a log split from a still-rolling trunk keeps rolling) and gets
        /// Definition.FallImpulse pushed into its top, away from the interactor - that's what
        /// topples a felled tree away from the player.</summary>
        private void SpawnNextStage(Vector3 fromPosition)
        {
            LeaveLeftover();
            if (_definition.BecomesPrefab == null)
            {
                return;
            }

            Vector3 away = transform.position - fromPosition;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;

            var spawned = new System.Collections.Generic.List<GameObject>();
            if (_becomesSpawnPoints.Length == 0)
            {
                spawned.Add(SpawnStageAt(transform.position, transform.rotation, away));
            }
            else
            {
                foreach (Transform point in _becomesSpawnPoints)
                {
                    if (point != null)
                    {
                        spawned.Add(SpawnStageAt(point.position, point.rotation, away));
                    }
                }
            }

            StagesSpawned?.Invoke(this, spawned);
        }

        private void LeaveLeftover()
        {
            if (_definition.LeavesBehindPrefab != null)
            {
                // Same parent/layer as the node, so render-distance culling treats it alike.
                GameObject leftover = Instantiate(_definition.LeavesBehindPrefab, transform.position, transform.rotation, transform.parent);
                InheritScale(leftover.transform);
                SetLayerRecursively(leftover.transform, gameObject.layer);
            }
        }

        /// <summary>WorldGenerator varies tree sizes (BiomeResourceSpawn.Scale) - the stump,
        /// felled trunk and its logs keep this node's size so a big tree leaves a big trunk.</summary>
        private void InheritScale(Transform spawned)
        {
            spawned.localScale = Vector3.Scale(spawned.localScale, transform.localScale);
        }

        private GameObject SpawnStageAt(Vector3 position, Quaternion rotation, Vector3 away)
        {
            GameObject instance = Instantiate(_definition.BecomesPrefab, position, rotation, transform.parent);
            InheritScale(instance.transform);
            SetLayerRecursively(instance.transform, gameObject.layer);
            var body = instance.GetComponent<Rigidbody>();
            if (body == null)
            {
                return instance;
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

            return instance;
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

        private int ExtraHitsFromTool(GameObject interactor)
        {
            if (_definition.RequiredTool == ToolType.None)
            {
                return 0;
            }

            var equipment = interactor.GetComponent<NAV.Gameplay.Equipment.PlayerEquipment>();
            ItemDefinition tool = equipment != null ? equipment.MainHand : null;
            return tool != null && tool.ToolType == _definition.RequiredTool ? Mathf.Max(0, tool.ToolTier - _definition.MinToolTier) : 0;
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
