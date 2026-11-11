using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using NAV.Gameplay.Items;
using NAV.World.Generation;

namespace NAV.Network
{
    /// <summary>
    /// Gathering in an online session (inert solo) - one world, felled once for everyone:
    ///  - World nodes (trees, rocks, ore): every gather hit goes to the master client, which
    ///    keeps the one shared count (ResourceNode.Network = this) and tells everyone the count
    ///    and, when it runs out, who felled it. The feller's machine makes the result (drops,
    ///    the falling trunk); the others leave the stump (ResourceNode.DepleteBy).
    ///  - Stages (a falling trunk, its logs) belong to the machine that made them: its physics
    ///    runs there and is streamed (poses while moving, a cached resting pose); the others show
    ///    kinematic copies. A hit on a copy is sent to the owner, who counts it - so splitting a
    ///    trunk or a log works for every player, and the wood drops as shared items
    ///    (NetworkItemSync). Stages of a player who left go to the master client.
    /// Spawns, rests, fellings and removals are cached in the room, so a late joiner sees the
    /// same stumps, trunks and logs.
    /// </summary>
    public class NetworkResourceSync : MonoBehaviourPunCallbacks, IOnEventCallback, IResourceNodeNetwork
    {
        private const byte NodeHitCode = 120;
        private const byte NodeCountCode = 121;
        private const byte NodeFelledCode = 122;
        private const byte StageSpawnCode = 123;
        private const byte StagePosesCode = 124;
        private const byte StageRestCode = 125;
        private const byte StageHitCode = 126;
        private const byte StageGoneCode = 127;
        private const byte StageOwnerCode = 128;
        private const long IdsPerActor = 1_000_000_000L;

        [SerializeField] private WorldGenerator _world;
        [Tooltip("Every resource node definition - their next-stage prefabs (trunks, logs) are made here from the network by name.")]
        [SerializeField] private ResourceNodeDefinition[] _definitions = new ResourceNodeDefinition[0];
        [SerializeField, Min(0.03f)] private float _poseSeconds = 0.1f;
        [SerializeField, Min(1f)] private float _followSharpness = 12f;

        private class Stage
        {
            public ResourceNode Node;
            public Rigidbody Body;
            public int Owner;
            public bool Copy;
            public bool RestSent;
            public Vector3 TargetPosition;
            public Quaternion TargetRotation;
        }

        private static readonly RaiseEventOptions ToOthersCached = new() { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.AddToRoomCacheGlobal };
        private static readonly RaiseEventOptions ToOthers = new() { Receivers = ReceiverGroup.Others };
        private static readonly RaiseEventOptions ToMaster = new() { Receivers = ReceiverGroup.MasterClient };

        private readonly Dictionary<string, GameObject> _prefabs = new();
        private readonly Dictionary<long, Stage> _stages = new();
        private readonly Dictionary<int, int> _remaining = new();
        private readonly HashSet<int> _felled = new();
        private readonly List<long> _scratch = new();
        private long _counter;
        private float _nextPoses;

        private static int Me => PhotonNetwork.LocalPlayer.ActorNumber;

        public override void OnEnable()
        {
            if (!PhotonNetwork.InRoom)
            {
                enabled = false;
                return;
            }

            base.OnEnable();
            foreach (ResourceNodeDefinition definition in _definitions)
            {
                if (definition != null && definition.BecomesPrefab != null)
                {
                    _prefabs[definition.BecomesPrefab.name] = definition.BecomesPrefab;
                }
            }

            ResourceNode.Network = this;
            ResourceNode.StagesSpawned += HandleStagesSpawned;
            ResourceNode.Depleted += HandleDepleted;
        }

        public override void OnDisable()
        {
            base.OnDisable();
            if (ReferenceEquals(ResourceNode.Network, this))
            {
                ResourceNode.Network = null;
            }

            ResourceNode.StagesSpawned -= HandleStagesSpawned;
            ResourceNode.Depleted -= HandleDepleted;
        }

        // ---------------------------------------------------------------- hits

        public bool InterceptGather(ResourceNode node, GameObject interactor, int hits)
        {
            Vector3 from = interactor != null ? interactor.transform.position : node.transform.position;
            if (node.NetworkId != 0)
            {
                if (!_stages.TryGetValue(node.NetworkId, out Stage stage) || !stage.Copy)
                {
                    return false; // ours - counted here like offline
                }

                var options = new RaiseEventOptions { TargetActors = new[] { stage.Owner } };
                PhotonNetwork.RaiseEvent(StageHitCode, new object[] { node.NetworkId, hits, from }, options, SendOptions.SendReliable);
                return true;
            }

            if (_world == null || !_world.TryGetNodeId(node.gameObject, out int id))
            {
                return false;
            }

            if (PhotonNetwork.IsMasterClient)
            {
                AuthorityHit(id, hits, node.Definition.HitsToDeplete, Me, from);
            }
            else
            {
                PhotonNetwork.RaiseEvent(NodeHitCode, new object[] { id, hits, node.Definition.HitsToDeplete, from }, ToMaster, SendOptions.SendReliable);
            }

            return true;
        }

        /// <summary>Master client: the one count of a world node.</summary>
        private void AuthorityHit(int id, int hits, int maxHits, int actor, Vector3 from)
        {
            if (_felled.Contains(id))
            {
                return;
            }

            if (!_remaining.TryGetValue(id, out int remaining))
            {
                remaining = maxHits;
            }

            remaining -= hits;
            if (remaining > 0)
            {
                _remaining[id] = remaining;
                PhotonNetwork.RaiseEvent(NodeCountCode, new object[] { id, remaining }, ToOthers, SendOptions.SendReliable);
                ApplyCount(id, remaining);
                return;
            }

            _remaining.Remove(id);
            _felled.Add(id);
            var data = new object[] { id, actor, from };
            PhotonNetwork.RaiseEvent(NodeFelledCode, data, ToOthersCached, SendOptions.SendReliable);
            ApplyFelled(id, actor, from);
        }

        private void ApplyCount(int id, int remaining)
        {
            GameObject instance = _world != null ? _world.GetNode(id) : null;
            if (instance != null && instance.TryGetComponent(out ResourceNode node))
            {
                node.SetRemainingHits(remaining);
            }
        }

        private void ApplyFelled(int id, int feller, Vector3 from)
        {
            GameObject instance = _world != null ? _world.GetNode(id) : null;
            if (instance != null && instance.TryGetComponent(out ResourceNode node))
            {
                // The feller makes the drops and the falling trunk; everyone else keeps the stump.
                node.DepleteBy(from, feller == Me);
            }
            else if (_world != null)
            {
                _world.RemoveNode(id); // not loaded here: it won't spawn
            }
        }

        // ---------------------------------------------------------------- stages (ours out)

        private void HandleStagesSpawned(ResourceNode source, List<GameObject> spawned)
        {
            foreach (GameObject instance in spawned)
            {
                if (instance == null || !instance.TryGetComponent(out ResourceNode node))
                {
                    continue;
                }

                long id = Me * IdsPerActor + ++_counter;
                node.NetworkId = id;
                Transform t = instance.transform;
                _stages[id] = new Stage { Node = node, Body = instance.GetComponent<Rigidbody>(), Owner = Me };
                string prefab = instance.name.Replace("(Clone)", string.Empty).Trim();
                PhotonNetwork.RaiseEvent(StageSpawnCode, new object[] { id, prefab, t.position, t.rotation, t.lossyScale }, ToOthersCached, SendOptions.SendReliable);
            }
        }

        private void HandleDepleted(ResourceNode node)
        {
            if (node.NetworkId != 0 && _stages.TryGetValue(node.NetworkId, out Stage stage) && !stage.Copy)
            {
                _stages.Remove(node.NetworkId);
                PhotonNetwork.RaiseEvent(StageGoneCode, node.NetworkId, ToOthersCached, SendOptions.SendReliable);
            }
        }

        private void Update()
        {
            if (!PhotonNetwork.InRoom)
            {
                return;
            }

            float follow = 1f - Mathf.Exp(-_followSharpness * Time.deltaTime);
            _scratch.Clear();
            foreach (KeyValuePair<long, Stage> pair in _stages)
            {
                Stage stage = pair.Value;
                if (stage.Node == null)
                {
                    _scratch.Add(pair.Key);
                    continue;
                }

                if (stage.Copy)
                {
                    Transform t = stage.Node.transform;
                    t.SetPositionAndRotation(Vector3.Lerp(t.position, stage.TargetPosition, follow), Quaternion.Slerp(t.rotation, stage.TargetRotation, follow));
                }
            }

            foreach (long id in _scratch)
            {
                // Ours went without a depletion (its chunk unloaded): gone for the others too.
                if (_stages.TryGetValue(id, out Stage gone) && !gone.Copy)
                {
                    PhotonNetwork.RaiseEvent(StageGoneCode, id, ToOthersCached, SendOptions.SendReliable);
                }

                _stages.Remove(id);
            }

            if (Time.unscaledTime >= _nextPoses)
            {
                _nextPoses = Time.unscaledTime + _poseSeconds;
                SendPoses();
            }
        }

        private void SendPoses()
        {
            var ids = new List<long>();
            var poses = new List<float>();
            foreach (KeyValuePair<long, Stage> pair in _stages)
            {
                Stage stage = pair.Value;
                if (stage.Copy || stage.Node == null || stage.Body == null)
                {
                    continue;
                }

                Transform t = stage.Node.transform;
                if (stage.Body.IsSleeping())
                {
                    if (!stage.RestSent)
                    {
                        stage.RestSent = true;
                        PhotonNetwork.RaiseEvent(StageRestCode, new object[] { pair.Key, t.position, t.rotation }, ToOthersCached, SendOptions.SendReliable);
                    }

                    continue;
                }

                stage.RestSent = false;
                ids.Add(pair.Key);
                Vector3 p = t.position;
                Quaternion r = t.rotation;
                poses.Add(p.x); poses.Add(p.y); poses.Add(p.z);
                poses.Add(r.x); poses.Add(r.y); poses.Add(r.z); poses.Add(r.w);
            }

            if (ids.Count > 0)
            {
                PhotonNetwork.RaiseEvent(StagePosesCode, new object[] { ids.ToArray(), poses.ToArray() }, ToOthers, SendOptions.SendUnreliable);
            }
        }

        // ---------------------------------------------------------------- events in

        public void OnEvent(EventData photonEvent)
        {
            switch (photonEvent.Code)
            {
                case NodeHitCode:
                    if (PhotonNetwork.IsMasterClient)
                    {
                        var hit = (object[])photonEvent.CustomData;
                        AuthorityHit((int)hit[0], (int)hit[1], (int)hit[2], photonEvent.Sender, (Vector3)hit[3]);
                    }

                    break;
                case NodeCountCode:
                    var count = (object[])photonEvent.CustomData;
                    ApplyCount((int)count[0], (int)count[1]);
                    break;
                case NodeFelledCode:
                    var felled = (object[])photonEvent.CustomData;
                    _felled.Add((int)felled[0]);
                    ApplyFelled((int)felled[0], (int)felled[1], (Vector3)felled[2]);
                    break;
                case StageSpawnCode:
                    SpawnCopy(photonEvent.Sender, (object[])photonEvent.CustomData);
                    break;
                case StagePosesCode:
                    var data = (object[])photonEvent.CustomData;
                    var ids = (long[])data[0];
                    var poses = (float[])data[1];
                    for (int i = 0; i < ids.Length; i++)
                    {
                        if (_stages.TryGetValue(ids[i], out Stage stage) && stage.Copy)
                        {
                            int o = i * 7;
                            stage.TargetPosition = new Vector3(poses[o], poses[o + 1], poses[o + 2]);
                            stage.TargetRotation = new Quaternion(poses[o + 3], poses[o + 4], poses[o + 5], poses[o + 6]);
                        }
                    }

                    break;
                case StageRestCode:
                    var rest = (object[])photonEvent.CustomData;
                    if (_stages.TryGetValue((long)rest[0], out Stage resting) && resting.Copy)
                    {
                        resting.TargetPosition = (Vector3)rest[1];
                        resting.TargetRotation = (Quaternion)rest[2];
                    }

                    break;
                case StageHitCode:
                    var stageHit = (object[])photonEvent.CustomData;
                    if (_stages.TryGetValue((long)stageHit[0], out Stage hitStage) && !hitStage.Copy && hitStage.Node != null)
                    {
                        hitStage.Node.ApplyGatherHits((int)stageHit[1], (Vector3)stageHit[2]);
                    }

                    break;
                case StageGoneCode:
                    long goneId = (long)photonEvent.CustomData;
                    if (_stages.TryGetValue(goneId, out Stage goneStage))
                    {
                        _stages.Remove(goneId);
                        if (goneStage.Copy && goneStage.Node != null)
                        {
                            Destroy(goneStage.Node.gameObject);
                        }
                    }

                    break;
                case StageOwnerCode:
                    var owner = (object[])photonEvent.CustomData;
                    foreach (long id in (long[])owner[0])
                    {
                        SetOwner(id, (int)owner[1]);
                    }

                    break;
            }
        }

        private void SpawnCopy(int sender, object[] data)
        {
            long id = (long)data[0];
            if (_stages.ContainsKey(id) || !_prefabs.TryGetValue(data[1] as string, out GameObject prefab))
            {
                return;
            }

            var position = (Vector3)data[2];
            var rotation = (Quaternion)data[3];
            GameObject instance = Instantiate(prefab, position, rotation);
            instance.transform.localScale = (Vector3)data[4];
            var node = instance.GetComponent<ResourceNode>();
            node.NetworkId = id;
            node.IsNetworkCopy = true;
            var body = instance.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
            }

            _stages[id] = new Stage { Node = node, Body = body, Owner = sender, Copy = true, TargetPosition = position, TargetRotation = rotation };

            // Its maker already left (a late join): the master client takes it over.
            if (PhotonNetwork.CurrentRoom.GetPlayer(sender) == null)
            {
                TakeOver(sender);
            }
        }

        private void SetOwner(long id, int owner)
        {
            if (!_stages.TryGetValue(id, out Stage stage) || stage.Node == null)
            {
                return;
            }

            stage.Owner = owner;
            bool mine = owner == Me;
            stage.Copy = !mine;
            stage.Node.IsNetworkCopy = !mine;
            if (stage.Body != null)
            {
                stage.Body.isKinematic = !mine;
                stage.Body.WakeUp();
            }
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            TakeOver(otherPlayer.ActorNumber);
        }

        /// <summary>Master client: the stages of a player who left become ours (their physics
        /// runs here, hits come here).</summary>
        private void TakeOver(int formerOwner)
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                return;
            }

            var ids = new List<long>();
            foreach (KeyValuePair<long, Stage> pair in _stages)
            {
                if (pair.Value.Owner == formerOwner)
                {
                    ids.Add(pair.Key);
                }
            }

            if (ids.Count == 0)
            {
                return;
            }

            foreach (long id in ids)
            {
                SetOwner(id, Me);
            }

            PhotonNetwork.RaiseEvent(StageOwnerCode, new object[] { ids.ToArray(), Me }, ToOthersCached, SendOptions.SendReliable);
        }
    }
}
