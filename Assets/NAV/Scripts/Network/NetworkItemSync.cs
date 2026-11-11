using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.Items;
using NAV.Gameplay.Player;

namespace NAV.Network
{
    /// <summary>
    /// Items lying in the world in an online session (inert solo) - one item, one owner:
    ///  - every pickup spawned here (a drop from the inventory, loot, a log's wood, a station's
    ///    product, a demolished chest's contents) is announced with its impulse; the others
    ///    spawn the same item and let it fall the same way; when it comes to rest on the
    ///    machine that spawned it, that resting pose is sent so everyone has it in one place;
    ///  - picking one up asks the master client, who gives it to whoever asked first; then it
    ///    is gone everywhere and in the winner's inventory (what doesn't fit is put back as a
    ///    new item). So players can hand each other things: drop it, the other picks it up.
    /// World-generated pickups (loose branches and stones) are world nodes - NetworkWorldSync
    /// removes them everywhere. Spawns, rests and pickups are cached for late joiners.
    /// </summary>
    public class NetworkItemSync : MonoBehaviourPunCallbacks, IOnEventCallback, IItemPickupNetwork
    {
        private const byte SpawnCode = 130;
        private const byte RestCode = 131;
        private const byte RequestCode = 132;
        private const byte TakenCode = 133;
        private const long IdsPerActor = 1_000_000_000L;
        private const float RequestTimeout = 2f;

        [SerializeField] private ItemDatabase _items;
        [SerializeField, Min(0.1f)] private float _restCheckSeconds = 0.5f;

        private class Entry
        {
            public ItemPickup Pickup;
            public Rigidbody Body;
            public bool Own;
            public bool Resting;
            public bool WaitingForGround;
        }

        private static readonly RaiseEventOptions ToOthersCached = new() { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.AddToRoomCacheGlobal };
        private static readonly RaiseEventOptions ToMaster = new() { Receivers = ReceiverGroup.MasterClient };

        private readonly Dictionary<long, Entry> _entries = new();
        private readonly HashSet<long> _taken = new();
        private readonly Dictionary<long, (float time, GameObject interactor)> _pending = new();
        private readonly List<long> _scratch = new();
        private long _counter;
        private float _nextRestCheck;

        private static int Me => PhotonNetwork.LocalPlayer.ActorNumber;

        public override void OnEnable()
        {
            if (!PhotonNetwork.InRoom)
            {
                enabled = false;
                return;
            }

            base.OnEnable();
            ItemPickup.Network = this;
        }

        public override void OnDisable()
        {
            base.OnDisable();
            if (ReferenceEquals(ItemPickup.Network, this))
            {
                ItemPickup.Network = null;
            }
        }

        // ---------------------------------------------------------------- ours out

        public void OnSpawned(ItemPickup pickup, Vector3 impulse)
        {
            if (pickup == null || pickup.NetworkId != 0 || pickup.Definition == null)
            {
                return;
            }

            long id = Me * IdsPerActor + ++_counter;
            pickup.NetworkId = id;
            _entries[id] = new Entry { Pickup = pickup, Body = pickup.GetComponent<Rigidbody>(), Own = true };
            Transform t = pickup.transform;
            PhotonNetwork.RaiseEvent(SpawnCode, new object[] { id, pickup.Definition.Id, pickup.Quantity, t.position, t.rotation, impulse },
                ToOthersCached, SendOptions.SendReliable);
        }

        public bool InterceptPickup(ItemPickup pickup, GameObject interactor)
        {
            long id = pickup.NetworkId;
            if (id == 0)
            {
                return false; // a world pickup or one from before the session: local
            }

            if (_pending.TryGetValue(id, out var pending) && Time.unscaledTime - pending.time < RequestTimeout)
            {
                return true; // already asked
            }

            _pending[id] = (Time.unscaledTime, interactor);
            if (PhotonNetwork.IsMasterClient)
            {
                Decide(id, Me);
            }
            else
            {
                PhotonNetwork.RaiseEvent(RequestCode, id, ToMaster, SendOptions.SendReliable);
            }

            return true;
        }

        /// <summary>Master client: the first to ask gets it.</summary>
        private void Decide(long id, int actor)
        {
            if (!_taken.Add(id))
            {
                return;
            }

            PhotonNetwork.RaiseEvent(TakenCode, new object[] { id, actor }, ToOthersCached, SendOptions.SendReliable);
            ApplyTaken(id, actor);
        }

        private void ApplyTaken(long id, int taker)
        {
            _taken.Add(id);
            _entries.TryGetValue(id, out Entry entry);
            _entries.Remove(id);
            ItemPickup pickup = entry != null ? entry.Pickup : null;
            bool asked = _pending.TryGetValue(id, out var pending);
            _pending.Remove(id);
            if (pickup == null)
            {
                return;
            }

            if (taker != Me)
            {
                pickup.gameObject.SetActive(false);
                Destroy(pickup.gameObject);
                return;
            }

            GameObject interactor = asked && pending.interactor != null ? pending.interactor : FindLocalPlayer();
            Vector3 position = pickup.transform.position;
            Quaternion rotation = pickup.transform.rotation;
            ItemDefinition definition = pickup.Definition;
            int before = pickup.Quantity;
            int picked = interactor != null ? pickup.GiveTo(interactor) : 0;
            int leftover = before - picked;
            if (pickup != null && pickup.gameObject.activeSelf)
            {
                pickup.gameObject.SetActive(false);
                Destroy(pickup.gameObject);
            }

            // What didn't fit goes back into the world as a new item for everyone.
            if (leftover > 0 && definition != null)
            {
                ItemPickup.SpawnInWorld(definition, leftover, position, rotation, Vector3.zero);
            }
        }

        private static GameObject FindLocalPlayer()
        {
            PlayerMotor motor = FindAnyObjectByType<PlayerMotor>();
            return motor != null ? motor.gameObject : null;
        }

        private void Update()
        {
            if (!PhotonNetwork.InRoom || Time.unscaledTime < _nextRestCheck)
            {
                return;
            }

            _nextRestCheck = Time.unscaledTime + _restCheckSeconds;
            _scratch.Clear();
            foreach (KeyValuePair<long, Entry> pair in _entries)
            {
                Entry entry = pair.Value;
                if (entry.Pickup == null)
                {
                    _scratch.Add(pair.Key);
                    continue;
                }

                if (entry.WaitingForGround && GroundUnder(entry.Pickup.transform.position))
                {
                    entry.WaitingForGround = false;
                    entry.Body.isKinematic = false;
                }

                if (!entry.Own || entry.Body == null)
                {
                    continue;
                }

                bool sleeping = entry.Body.IsSleeping();
                if (sleeping && !entry.Resting)
                {
                    Transform t = entry.Pickup.transform;
                    PhotonNetwork.RaiseEvent(RestCode, new object[] { pair.Key, t.position, t.rotation }, ToOthersCached, SendOptions.SendReliable);
                }

                entry.Resting = sleeping;
            }

            foreach (long id in _scratch)
            {
                _entries.Remove(id);
            }
        }

        // ---------------------------------------------------------------- events in

        public void OnEvent(EventData photonEvent)
        {
            switch (photonEvent.Code)
            {
                case SpawnCode:
                    SpawnCopy((object[])photonEvent.CustomData);
                    break;
                case RestCode:
                    var rest = (object[])photonEvent.CustomData;
                    if (_entries.TryGetValue((long)rest[0], out Entry resting) && resting.Pickup != null)
                    {
                        resting.Pickup.transform.SetPositionAndRotation((Vector3)rest[1], (Quaternion)rest[2]);
                        if (resting.Body != null && !resting.Body.isKinematic)
                        {
                            resting.Body.linearVelocity = Vector3.zero;
                            resting.Body.angularVelocity = Vector3.zero;
                            resting.Body.Sleep();
                        }
                    }

                    break;
                case RequestCode:
                    if (PhotonNetwork.IsMasterClient)
                    {
                        Decide((long)photonEvent.CustomData, photonEvent.Sender);
                    }

                    break;
                case TakenCode:
                    var taken = (object[])photonEvent.CustomData;
                    ApplyTaken((long)taken[0], (int)taken[1]);
                    break;
            }
        }

        private void SpawnCopy(object[] data)
        {
            long id = (long)data[0];
            if (_entries.ContainsKey(id) || _taken.Contains(id))
            {
                return;
            }

            ItemDefinition definition = _items != null ? _items.Get(data[1] as string) : null;
            if (definition == null)
            {
                Debug.LogWarning($"{nameof(NetworkItemSync)}: unknown item '{data[1]}' from the network - skipped.", this);
                return;
            }

            var position = (Vector3)data[3];
            ItemPickup pickup;
            ItemPickup.SpawningFromNetwork = true;
            try
            {
                pickup = ItemPickup.SpawnInWorld(definition, (int)data[2], position, (Quaternion)data[4], (Vector3)data[5]);
            }
            finally
            {
                ItemPickup.SpawningFromNetwork = false;
            }

            if (pickup == null)
            {
                return;
            }

            pickup.NetworkId = id;
            var entry = new Entry { Pickup = pickup, Body = pickup.GetComponent<Rigidbody>() };
            // No ground under it yet (a late joiner whose world is still loading): it waits.
            if (entry.Body != null && !GroundUnder(position))
            {
                entry.Body.isKinematic = true;
                entry.WaitingForGround = true;
            }

            _entries[id] = entry;
        }

        private static bool GroundUnder(Vector3 point)
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (point.x >= origin.x && point.z >= origin.z && point.x < origin.x + size.x && point.z < origin.z + size.z)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
