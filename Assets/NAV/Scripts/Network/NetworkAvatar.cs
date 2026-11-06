using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.Combat;
using NAV.Gameplay.Equipment;
using NAV.Gameplay.Items;
using NAV.Gameplay.Player;
using NAV.Presentation.Animation;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace NAV.Network
{
    /// <summary>
    /// A player as the others see them. Every player spawns one (NetworkGameManager,
    /// PhotonNetwork.Instantiate "NetworkPlayer"); the real, playable character stays the scene's
    /// Player - this is its stand-in on the other machines ("proxy avatar").
    ///   - On the owner's machine it is hidden and only reads the local player: position and
    ///     facing, every Animator parameter the hero's controller has (except curve-driven ones and
    ///     triggers), each layer's weight and current state, and whether the player is dead.
    ///   - On the others it shows the hero (same model, controller, hand grip, held item, armour
    ///     and ragdoll as the local one), glides to the received position, sets the same
    ///     parameters, cross-fades to the same states (that is how one-shot clips started by
    ///     triggers - attacks, hammer strikes - get there), turns the spine by the attack clips'
    ///     own twist like PlayerAnimator does, falls into its ragdoll when the owner dies and
    ///     shows the owner's name over its head.
    ///   - Equipment comes from the owner's "eq" player property (main hand | worn ids).
    ///   - PvP: a remote avatar is an IDamageable only in a PvP room. A hit on it (the local
    ///     player's weapon, PlayerCombat) is sent to its owner (RPC NetHit), whose PlayerCombat
    ///     takes it with block, parry and armour - the owner's machine decides. It is an
    ///     IRemoteProxy, so local creatures ignore it (they are simulated per machine for now).
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    [DefaultExecutionOrder(-50)]
    public class NetworkAvatar : MonoBehaviourPunCallbacks, IPunObservable, IDamageable, IHitReactive, IHitSurface, IRemoteProxy
    {
        private static readonly int AttackTwistHash = Animator.StringToHash("AttackTwist");
        private static readonly int FullStanceTwistHash = Animator.StringToHash("FullStanceTwist");

        [SerializeField] private GameObject _model;
        [SerializeField] private Animator _animator;
        [SerializeField] private Ragdoll _ragdoll;
        [SerializeField] private NetworkEquipmentMirror _equipmentMirror;
        [SerializeField] private ItemDatabase _itemDatabase;
        [Tooltip("The body others hit (PvP) and bump into.")]
        [SerializeField] private Collider _body;
        [SerializeField] private TextMesh _nameTag;
        [Tooltip("How quickly the avatar closes on the received position (1/s).")]
        [SerializeField, Min(1f)] private float _followSharpness = 12f;
        [Tooltip("Speed (m/s) of the push back a dying avatar falls with.")]
        [SerializeField, Min(0f)] private float _deathPush = 1.5f;

        // Parameters that travel (same controller on every machine, so the same order).
        private readonly List<AnimatorControllerParameter> _parameters = new();

        // Owner side - the local player.
        private Transform _localPlayer;
        private Animator _localAnimator;
        private PlayerEquipment _localEquipment;
        private PlayerDeath _localDeath;
        private PlayerCombat _localCombat;
        private PlayerHealth _localHealth;

        // Faster than any player moves (a sprint, a boat) - caps a bad sample.
        private const float MaxVelocity = 20f;

        // Remote side - the latest received state.
        private Vector3 _lastReceived;
        private double _lastSentTime;
        private float _lastReceiveTime;
        private Vector3 _targetPosition;
        private float _targetYaw;
        private Vector3 _velocity;
        private bool _dead;
        private bool _hasState;
        private float[] _floats;
        private int[] _ints;
        private bool[] _bools;
        private float[] _layerWeights;
        private int[] _layerStates;
        private float[] _layerTimes;
        private Transform _spine;
        private Transform _chest;
        private Transform _upperChest;
        private bool _hasTwist;

        /// <summary>Alive as far as this machine knows (creatures hunt it while it is).</summary>
        public bool IsAlive => !photonView.IsMine && !_dead;
        public HitSurface Surface => HitSurface.Flesh;

        /// <summary>Players' weapons hurt it only in a PvP room.</summary>
        public bool CanBeHitByPlayers => IsAlive && NetworkSession.IsPvp;

        /// <summary>The owner's health as last received (their PlayerHealth).</summary>
        public float CurrentHealth { get; private set; } = 100f;
        public float MaxHealth { get; private set; } = 100f;

        private void Awake()
        {
            if (_model == null || _animator == null || _equipmentMirror == null || _itemDatabase == null || _body == null)
            {
                Debug.LogError($"{nameof(NetworkAvatar)} on '{name}' is missing a reference - rebuild it with NAV > Network > Build Network Player.", this);
                enabled = false;
                return;
            }

            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.type != AnimatorControllerParameterType.Trigger && !_animator.IsParameterControlledByCurve(parameter.nameHash))
                {
                    _parameters.Add(parameter);
                }

                _hasTwist |= parameter.nameHash == AttackTwistHash;
            }

            _floats = new float[_parameters.Count];
            _ints = new int[_parameters.Count];
            _bools = new bool[_parameters.Count];
            int layers = _animator.layerCount;
            _layerWeights = new float[layers];
            _layerStates = new int[layers];
            _layerTimes = new float[layers];
            _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _chest = _animator.GetBoneTransform(HumanBodyBones.Chest);
            _upperChest = _animator.GetBoneTransform(HumanBodyBones.UpperChest);
        }

        private void Start()
        {
            if (!enabled)
            {
                return;
            }

            if (photonView.IsMine)
            {
                BindLocalPlayer();
                return;
            }

            _targetPosition = transform.position;
            _targetYaw = transform.eulerAngles.y;
            if (_nameTag != null)
            {
                _nameTag.text = photonView.Owner != null ? photonView.Owner.NickName : "?";
            }

            ApplyEquipment(photonView.Owner);
        }

        /// <summary>Owner side: hide the stand-in, follow the scene's Player.</summary>
        private void BindLocalPlayer()
        {
            _model.SetActive(false);
            _body.enabled = false;
            if (_nameTag != null)
            {
                _nameTag.gameObject.SetActive(false);
            }

            var motor = FindAnyObjectByType<PlayerMotor>();
            if (motor == null)
            {
                Debug.LogError($"{nameof(NetworkAvatar)}: no local player (PlayerMotor) in the scene.", this);
                return;
            }

            _localPlayer = motor.transform;
            _localDeath = motor.GetComponent<PlayerDeath>();
            _localCombat = motor.GetComponent<PlayerCombat>();
            _localHealth = motor.GetComponent<PlayerHealth>();
            _localEquipment = motor.GetComponent<PlayerEquipment>();
            foreach (Animator candidate in motor.GetComponentsInChildren<Animator>())
            {
                if (candidate.isHuman)
                {
                    _localAnimator = candidate;
                }
            }

            if (_localEquipment != null)
            {
                _localEquipment.Changed += PublishEquipment;
                PublishEquipment();
            }
        }

        public override void OnDisable()
        {
            base.OnDisable();
            if (_localEquipment != null)
            {
                _localEquipment.Changed -= PublishEquipment;
            }
        }

        /// <summary>Owner side: "main|worn1,worn2" into the player's "eq" property.</summary>
        private void PublishEquipment()
        {
            var worn = new List<string>();
            foreach (ItemDefinition item in _localEquipment.Worn.Values)
            {
                worn.Add(item.Id);
            }

            string main = _localEquipment.MainHand != null ? _localEquipment.MainHand.Id : string.Empty;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { NetworkSession.EquipmentKey, $"{main}|{string.Join(",", worn)}" } });
        }

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (!photonView.IsMine && targetPlayer == photonView.Owner && changedProps.ContainsKey(NetworkSession.EquipmentKey))
            {
                ApplyEquipment(targetPlayer);
            }
        }

        private void ApplyEquipment(Player owner)
        {
            if (owner == null || !owner.CustomProperties.TryGetValue(NetworkSession.EquipmentKey, out object value) || value is not string text)
            {
                return;
            }

            string[] parts = text.Split('|');
            ItemDefinition main = parts.Length > 0 && parts[0].Length > 0 ? _itemDatabase.Get(parts[0]) : null;
            var worn = new List<ItemDefinition>();
            if (parts.Length > 1)
            {
                foreach (string id in parts[1].Split(','))
                {
                    if (id.Length > 0)
                    {
                        worn.Add(_itemDatabase.Get(id));
                    }
                }
            }

            _equipmentMirror.Show(main, worn);
        }

        // ---- State stream ----

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                WriteState(stream);
            }
            else
            {
                ReadState(stream, info);
            }
        }

        private void WriteState(PhotonStream stream)
        {
            if (_localPlayer == null || _localAnimator == null)
            {
                stream.SendNext(false);
                return;
            }

            stream.SendNext(true);
            stream.SendNext(_localPlayer.position);
            stream.SendNext(_localPlayer.eulerAngles.y);
            stream.SendNext(_localDeath != null && _localDeath.IsDead);
            stream.SendNext(_localHealth != null ? _localHealth.CurrentHealth : 0f);
            stream.SendNext(_localHealth != null ? _localHealth.MaxHealth : 0f);
            foreach (AnimatorControllerParameter parameter in _parameters)
            {
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        stream.SendNext(_localAnimator.GetFloat(parameter.nameHash));
                        break;
                    case AnimatorControllerParameterType.Int:
                        stream.SendNext(_localAnimator.GetInteger(parameter.nameHash));
                        break;
                    default:
                        stream.SendNext(_localAnimator.GetBool(parameter.nameHash));
                        break;
                }
            }

            for (int layer = 0; layer < _layerStates.Length && layer < _localAnimator.layerCount; layer++)
            {
                AnimatorStateInfo state = _localAnimator.IsInTransition(layer) ? _localAnimator.GetNextAnimatorStateInfo(layer) : _localAnimator.GetCurrentAnimatorStateInfo(layer);
                stream.SendNext(layer == 0 ? 1f : _localAnimator.GetLayerWeight(layer));
                stream.SendNext(state.fullPathHash);
                stream.SendNext(state.normalizedTime);
            }
        }

        private void ReadState(PhotonStream stream, PhotonMessageInfo info)
        {
            if (!(bool)stream.ReceiveNext())
            {
                return;
            }

            Vector3 position = (Vector3)stream.ReceiveNext();
            // Velocity from the last two RECEIVED positions and their send times - never from the
            // extrapolated target (that fed its own error back and blew up to 1e18 m).
            double sentTime = info.SentServerTime;
            float interval = (float)(sentTime - _lastSentTime);
            _velocity = _hasState && interval > 0.01f && interval < 1f ? Vector3.ClampMagnitude((position - _lastReceived) / interval, MaxVelocity) : Vector3.zero;
            _lastReceived = position;
            _lastSentTime = sentTime;
            _lastReceiveTime = Time.time;
            float lag = Mathf.Clamp((float)(PhotonNetwork.Time - sentTime), 0f, 0.3f);
            _targetPosition = position + _velocity * lag;
            _targetYaw = (float)stream.ReceiveNext();
            bool dead = (bool)stream.ReceiveNext();
            CurrentHealth = (float)stream.ReceiveNext();
            MaxHealth = (float)stream.ReceiveNext();
            for (int i = 0; i < _parameters.Count; i++)
            {
                switch (_parameters[i].type)
                {
                    case AnimatorControllerParameterType.Float:
                        _floats[i] = (float)stream.ReceiveNext();
                        break;
                    case AnimatorControllerParameterType.Int:
                        _ints[i] = (int)stream.ReceiveNext();
                        break;
                    default:
                        _bools[i] = (bool)stream.ReceiveNext();
                        break;
                }
            }

            for (int layer = 0; layer < _layerStates.Length; layer++)
            {
                _layerWeights[layer] = (float)stream.ReceiveNext();
                _layerStates[layer] = (int)stream.ReceiveNext();
                _layerTimes[layer] = (float)stream.ReceiveNext();
            }

            if (!_hasState)
            {
                transform.SetPositionAndRotation(position, Quaternion.Euler(0f, _targetYaw, 0f));
                _hasState = true;
            }

            SetDead(dead);
        }

        private void SetDead(bool dead)
        {
            if (dead == _dead)
            {
                return;
            }

            _dead = dead;
            if (_ragdoll == null)
            {
                return;
            }

            if (dead)
            {
                _ragdoll.Activate(_velocity, -transform.forward * _deathPush);
            }
            else
            {
                _ragdoll.Deactivate();
            }
        }

        // ---- Remote presentation ----

        private void Update()
        {
            if (photonView.IsMine || !_hasState)
            {
                return;
            }

            float follow = 1f - Mathf.Exp(-_followSharpness * Time.deltaTime);
            // The target moves on between packets (no further than one late packet's worth).
            // Nothing new for a while = the owner stands still (UnreliableOnChange stops sending).
            float sinceSent = Mathf.Clamp((float)(PhotonNetwork.Time - _lastSentTime), 0f, 0.3f);
            Vector3 target = Time.time - _lastReceiveTime > 0.25f ? _lastReceived : _lastReceived + _velocity * sinceSent;
            transform.position = Vector3.Lerp(transform.position, target, follow);
            _targetPosition = target;
            Quaternion yaw = Quaternion.Euler(0f, _targetYaw, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, yaw, follow);
            if (_dead || !_animator.enabled)
            {
                return;
            }

            for (int i = 0; i < _parameters.Count; i++)
            {
                AnimatorControllerParameter parameter = _parameters[i];
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        _animator.SetFloat(parameter.nameHash, _floats[i]);
                        break;
                    case AnimatorControllerParameterType.Int:
                        _animator.SetInteger(parameter.nameHash, _ints[i]);
                        break;
                    default:
                        _animator.SetBool(parameter.nameHash, _bools[i]);
                        break;
                }
            }

            for (int layer = 0; layer < _layerStates.Length; layer++)
            {
                if (layer > 0)
                {
                    _animator.SetLayerWeight(layer, _layerWeights[layer]);
                }

                int wanted = _layerStates[layer];
                if (wanted == 0)
                {
                    continue;
                }

                AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(layer);
                bool heading = _animator.IsInTransition(layer) && _animator.GetNextAnimatorStateInfo(layer).fullPathHash == wanted;
                if (current.fullPathHash != wanted && !heading && _animator.HasState(layer, wanted))
                {
                    _animator.CrossFadeInFixedTime(wanted, 0.12f, layer, 0f);
                }
            }
        }

        private void LateUpdate()
        {
            if (photonView.IsMine)
            {
                return;
            }

            if (_nameTag != null && Camera.main != null)
            {
                _nameTag.transform.rotation = Quaternion.LookRotation(_nameTag.transform.position - Camera.main.transform.position);
            }

            if (_dead || !_hasTwist || !_animator.enabled)
            {
                return;
            }

            // The same swing correction the local player gets (PlayerAnimator).
            float twist = _animator.GetFloat(AttackTwistHash) - _animator.GetFloat(FullStanceTwistHash);
            if (Mathf.Abs(twist) > 0.01f)
            {
                SpineTwist.Apply(_spine, _chest, _upperChest, twist);
            }
        }

        // ---- PvP ----

        /// <summary>The local player's weapon hit this avatar - its owner decides what it does.</summary>
        public void TakeDamage(float amount)
        {
            if (!CanBeHitByPlayers || amount <= 0f)
            {
                return;
            }

            photonView.RPC(nameof(NetHit), photonView.Owner, amount, PhotonNetwork.LocalPlayer.ActorNumber);
        }

        /// <summary>A creature run on this machine bit the player this avatar stands for.</summary>
        public void TakeCreatureDamage(float amount, float poisonDamage, float poisonSeconds)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            photonView.RPC(nameof(NetCreatureHit), photonView.Owner, amount, poisonDamage, poisonSeconds);
        }

        [PunRPC]
        private void NetCreatureHit(float amount, float poisonDamage, float poisonSeconds)
        {
            if (!photonView.IsMine || _localCombat == null)
            {
                return;
            }

            _localCombat.TakeDamage(amount);
            if (poisonDamage > 0f && _localCombat.GetComponent<IPoisonable>() is IPoisonable poisonable)
            {
                poisonable.ApplyPoison(poisonDamage, poisonSeconds);
            }
        }

        public void ReactToHit(Vector3 direction)
        {
            // The owner's machine plays the reaction (its own PlayerCombat/HitFeedback).
        }

        [PunRPC]
        private void NetHit(float amount, int attackerActor)
        {
            if (!photonView.IsMine || !NetworkSession.IsPvp || _localCombat == null)
            {
                return;
            }

            Player attacker = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.GetPlayer(attackerActor) : null;
            Debug.Log($"{nameof(NetworkAvatar)}: hit by {(attacker != null ? attacker.NickName : "someone")} for {amount:F1}.", this);
            _localCombat.TakeDamage(amount);
        }
    }
}
