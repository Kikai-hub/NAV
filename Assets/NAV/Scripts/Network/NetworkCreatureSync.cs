using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.AI;
using NAV.Gameplay.AI;
using NAV.Gameplay.Player;
using NAV.World.Generation;

namespace NAV.Network
{
    /// <summary>
    /// Creatures in an online session (inert solo). Creatures live around each player (the
    /// NavMesh is built around the local player), so every machine runs the creatures around
    /// its own player - and sends them to the others, who show copies (Creature.MakeRemoteCopy:
    /// no AI, moved from the snapshots, CreatureAnimator walks/runs and lunges as reported).
    ///   - Two players close together would each spawn a set: within ShareRadius of a player
    ///     with a lower actor number, this machine spawns none (CreaturePopulation.SpawningSuppressed)
    ///     and removes its own - the other's creatures (copies here) are the ones around.
    ///   - Snapshots (unreliable, ~8 per second): ids, prefab names, positions, facing, velocity,
    ///     "attacked" flags. A copy not heard of for a few seconds is removed (despawned there).
    ///   - A hit on a copy goes to its owner (TakeDamageFrom - the attacker's actor number as the
    ///     source); a death is announced with the killer: the copies die into their ragdolls and
    ///     the loot drops on the killer's machine only.
    ///   - The owner's creatures hunt remote players' avatars too (bites go to them -
    ///     IRemoteProxy.TakeCreatureDamage).
    /// </summary>
    public class NetworkCreatureSync : MonoBehaviour, IOnEventCallback
    {
        private const byte SnapshotCode = 110;
        private const byte HitCode = 111;
        private const byte DiedCode = 112;
        private const int IdsPerActor = 100000;

        [Tooltip("Every creature prefab that can spawn (copies are made from these by name).")]
        [SerializeField] private GameObject[] _creaturePrefabs = new GameObject[0];
        [SerializeField] private PlayerMotor _player;
        [Tooltip("Players closer than this share one set of creatures (the lower actor number runs them).")]
        [SerializeField, Min(20f)] private float _shareRadius = 170f;
        [SerializeField, Min(0.05f)] private float _snapshotSeconds = 0.12f;
        [Tooltip("A copy with no news for this long is removed.")]
        [SerializeField, Min(0.5f)] private float _copyTimeout = 3f;
        [SerializeField, Min(1f)] private float _followSharpness = 10f;

        private class Copy
        {
            public Creature Creature;
            public int Owner;
            public Vector3 Position;
            public float Yaw;
            public Vector3 Velocity;
            public float LastHeard;
        }

        private static readonly RaiseEventOptions ToOthers = new() { Receivers = ReceiverGroup.Others };
        private readonly Dictionary<string, GameObject> _prefabs = new();
        private readonly Dictionary<int, Copy> _copies = new();
        private readonly Dictionary<int, Creature> _own = new();
        private readonly HashSet<int> _attacked = new();
        private readonly List<int> _scratch = new();
        private int _counter;
        private float _nextSnapshot;

        private void OnEnable()
        {
            if (!PhotonNetwork.InRoom)
            {
                enabled = false;
                return;
            }

            foreach (GameObject prefab in _creaturePrefabs)
            {
                if (prefab != null)
                {
                    _prefabs[prefab.name] = prefab;
                }
            }

            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerMotor>();
            }

            PhotonNetwork.AddCallbackTarget(this);
        }

        private void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            CreaturePopulation.SpawningSuppressed = false;
            NAV.World.Encounters.ForestWrath.EncountersSuppressed = false;
        }

        private void Update()
        {
            if (!PhotonNetwork.InRoom || _player == null)
            {
                return;
            }

            UpdateAuthority();
            RegisterOwnCreatures();
            MoveCopies();
            if (Time.unscaledTime >= _nextSnapshot)
            {
                _nextSnapshot = Time.unscaledTime + _snapshotSeconds;
                SendSnapshot();
            }
        }

        // ---- Who runs the creatures here ----

        private void UpdateAuthority()
        {
            bool suppressed = false;
            int me = PhotonNetwork.LocalPlayer.ActorNumber;
            foreach (NetworkAvatar avatar in FindObjectsByType<NetworkAvatar>(FindObjectsSortMode.None))
            {
                if (!avatar.photonView.IsMine && avatar.photonView.OwnerActorNr < me
                    && (avatar.transform.position - _player.transform.position).sqrMagnitude < _shareRadius * _shareRadius)
                {
                    suppressed = true;
                    break;
                }
            }

            if (suppressed && !CreaturePopulation.SpawningSuppressed)
            {
                // The other player's creatures are the ones around here now - ours go.
                foreach (Creature creature in new List<Creature>(Creature.All))
                {
                    if (!creature.IsRemoteCopy && creature.IsAlive)
                    {
                        Destroy(creature.gameObject);
                    }
                }

                Debug.Log($"{nameof(NetworkCreatureSync)}: another player is near - their machine runs the creatures here.", this);
            }

            CreaturePopulation.SpawningSuppressed = suppressed;
            // The Leshy too: the player who runs the creatures here runs the forest's master.
            NAV.World.Encounters.ForestWrath.EncountersSuppressed = suppressed;
        }

        private void RegisterOwnCreatures()
        {
            foreach (Creature creature in Creature.All)
            {
                if (creature.IsRemoteCopy || creature.NetworkId != 0)
                {
                    continue;
                }

                creature.NetworkId = PhotonNetwork.LocalPlayer.ActorNumber * IdsPerActor + ++_counter % IdsPerActor;
                int id = creature.NetworkId;
                _own[id] = creature;
                creature.AttackPerformed += () => _attacked.Add(id);
                creature.Died += () => AnnounceDeath(creature, id);
            }
        }

        private void AnnounceDeath(Creature creature, int id)
        {
            int killer = creature.KilledBy is int actor ? actor : PhotonNetwork.LocalPlayer.ActorNumber;
            PhotonNetwork.RaiseEvent(DiedCode, new object[] { id, killer }, ToOthers, SendOptions.SendReliable);
            _own.Remove(id);
        }

        // ---- Snapshots ----

        private void SendSnapshot()
        {
            _scratch.Clear();
            foreach (KeyValuePair<int, Creature> pair in _own)
            {
                if (pair.Value != null && pair.Value.IsAlive)
                {
                    _scratch.Add(pair.Key);
                }
            }

            // Despawned / dead ones leave the table.
            var gone = new List<int>();
            foreach (KeyValuePair<int, Creature> pair in _own)
            {
                if (pair.Value == null)
                {
                    gone.Add(pair.Key);
                }
            }

            foreach (int id in gone)
            {
                _own.Remove(id);
            }

            int count = _scratch.Count;
            if (count == 0)
            {
                _attacked.Clear();
                return;
            }

            var ids = new int[count];
            var names = new string[count];
            var state = new float[count * 7];
            var attacked = new bool[count];
            for (int i = 0; i < count; i++)
            {
                Creature creature = _own[_scratch[i]];
                ids[i] = _scratch[i];
                names[i] = creature.name.Replace("(Clone)", string.Empty).Trim();
                Vector3 position = creature.transform.position;
                var agent = creature.GetComponent<NavMeshAgent>();
                Vector3 velocity = agent != null && agent.enabled ? agent.velocity : Vector3.zero;
                int o = i * 7;
                state[o] = position.x;
                state[o + 1] = position.y;
                state[o + 2] = position.z;
                state[o + 3] = creature.transform.eulerAngles.y;
                state[o + 4] = velocity.x;
                state[o + 5] = velocity.y;
                state[o + 6] = velocity.z;
                attacked[i] = _attacked.Contains(ids[i]);
            }

            _attacked.Clear();
            PhotonNetwork.RaiseEvent(SnapshotCode, new object[] { ids, names, state, attacked }, ToOthers, SendOptions.SendUnreliable);
        }

        public void OnEvent(EventData photonEvent)
        {
            switch (photonEvent.Code)
            {
                case SnapshotCode:
                    ReadSnapshot(photonEvent.Sender, (object[])photonEvent.CustomData);
                    break;
                case HitCode:
                    var hit = (object[])photonEvent.CustomData;
                    if (_own.TryGetValue((int)hit[0], out Creature target) && target != null)
                    {
                        target.TakeDamageFrom((float)hit[1], photonEvent.Sender);
                    }

                    break;
                case DiedCode:
                    var death = (object[])photonEvent.CustomData;
                    if (_copies.TryGetValue((int)death[0], out Copy copy))
                    {
                        _copies.Remove((int)death[0]);
                        if (copy.Creature != null)
                        {
                            copy.Creature.DieAsCopy((int)death[1] == PhotonNetwork.LocalPlayer.ActorNumber);
                        }
                    }

                    break;
            }
        }

        private void ReadSnapshot(int sender, object[] data)
        {
            var ids = (int[])data[0];
            var names = (string[])data[1];
            var state = (float[])data[2];
            var attacked = (bool[])data[3];
            for (int i = 0; i < ids.Length; i++)
            {
                int o = i * 7;
                var position = new Vector3(state[o], state[o + 1], state[o + 2]);
                if (!_copies.TryGetValue(ids[i], out Copy copy))
                {
                    if (!_prefabs.TryGetValue(names[i], out GameObject prefab))
                    {
                        continue;
                    }

                    GameObject instance;
                    Creature.CreatingRemoteCopy = true;
                    try
                    {
                        instance = Instantiate(prefab, position, Quaternion.Euler(0f, state[o + 3], 0f));
                    }
                    finally
                    {
                        Creature.CreatingRemoteCopy = false;
                    }

                    var creature = instance.GetComponent<Creature>();
                    creature.MakeRemoteCopy(ids[i]);
                    creature.RemoteDamageRequested += SendHit;
                    copy = new Copy { Creature = creature, Owner = sender };
                    _copies[ids[i]] = copy;
                }

                copy.Position = position;
                copy.Yaw = state[o + 3];
                copy.Velocity = new Vector3(state[o + 4], state[o + 5], state[o + 6]);
                copy.LastHeard = Time.time;
                if (attacked[i] && copy.Creature != null)
                {
                    copy.Creature.PlayAttackAsCopy();
                }
            }
        }

        private void SendHit(Creature creature, float amount)
        {
            if (_copies.TryGetValue(creature.NetworkId, out Copy copy))
            {
                var options = new RaiseEventOptions { TargetActors = new[] { copy.Owner } };
                PhotonNetwork.RaiseEvent(HitCode, new object[] { creature.NetworkId, amount }, options, SendOptions.SendReliable);
            }
        }

        private void MoveCopies()
        {
            _scratch.Clear();
            float follow = 1f - Mathf.Exp(-_followSharpness * Time.deltaTime);
            foreach (KeyValuePair<int, Copy> pair in _copies)
            {
                Copy copy = pair.Value;
                if (copy.Creature == null || Time.time - copy.LastHeard > _copyTimeout)
                {
                    _scratch.Add(pair.Key);
                    continue;
                }

                if (!copy.Creature.IsAlive)
                {
                    continue;
                }

                Transform t = copy.Creature.transform;
                copy.Position += copy.Velocity * Time.deltaTime;
                t.position = Vector3.Lerp(t.position, copy.Position, follow);
                t.rotation = Quaternion.Slerp(t.rotation, Quaternion.Euler(0f, copy.Yaw, 0f), follow);
                copy.Creature.RemoteVelocity = copy.Velocity;
            }

            foreach (int id in _scratch)
            {
                if (_copies.TryGetValue(id, out Copy copy) && copy.Creature != null && copy.Creature.IsAlive)
                {
                    Destroy(copy.Creature.gameObject);
                }

                _copies.Remove(id);
            }
        }
    }
}
