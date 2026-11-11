using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.Building;
using NAV.Gameplay.Items;
using NAV.Gameplay.Player;
using NAV.World.Encounters;
using NAV.World.Generation;

namespace NAV.Network
{
    /// <summary>
    /// Keeps the shared world the same on every machine during an online session (inert solo).
    /// The terrain and the resources already match (one seed); what players change is sent as
    /// Photon events:
    ///   - a piece built (definition id, position, rotation, network id) and demolished;
    ///   - a world-generated pickup (a loose branch or stone) picked up - gone for everyone
    ///     (trees, rocks, their trunks, logs and drops: NetworkResourceSync / NetworkItemSync);
    ///   - chat lines (NAV.Core.ChatChannel - the chat window UI ChatUI shows them);
    ///   - the forest's wrath round a player (ForestWrath): its lines, its omen and its silence
    ///     reach the players near the one whose machine runs the Leshy.
    /// World events are kept in the room's global cache, so someone joining later - and the
    /// host after they reload - gets every change made before, in order, even from players who
    /// have left. Remote builds go into BuildingPiece.AllPieces, so the host's save keeps them.
    /// </summary>
    public class NetworkWorldSync : MonoBehaviour, IOnEventCallback
    {
        private const byte PlaceCode = 101;
        private const byte DemolishCode = 102;
        private const byte DepleteCode = 103;
        private const byte ChatCode = 104;
        private const byte WrathMessageCode = 105;
        private const byte WrathOmenCode = 106;
        private const byte WrathSilenceCode = 107;

        [SerializeField] private WorldGenerator _world;
        [SerializeField] private PlayerBuilding _building;
        [SerializeField] private BuildingPieceDatabase _pieces;
        [Tooltip("Optional - its encounter is shared with the players nearby.")]
        [SerializeField] private ForestWrath _forestWrath;
        [Tooltip("Players this close (m) to the one whose Leshy it is hear and see the encounter.")]
        [SerializeField, Min(10f)] private float _wrathShareDistance = 220f;

        private static readonly RaiseEventOptions CachedToOthers = new() { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.AddToRoomCacheGlobal };
        private static readonly RaiseEventOptions ToAll = new() { Receivers = ReceiverGroup.All };
        private static readonly RaiseEventOptions ToOthersOnly = new() { Receivers = ReceiverGroup.Others };
        private long _pieceCounter;
        private bool _applyingWrath;
        private float _nextSilence;
        private float _lastSentSilence;
        private Transform _player;


        public static NetworkWorldSync Instance { get; private set; }

        private void OnEnable()
        {
            if (!PhotonNetwork.InRoom)
            {
                enabled = false;
                return;
            }

            Instance = this;
            PhotonNetwork.AddCallbackTarget(this);
            NAV.Core.ChatChannel.Sender = SendChat;
            NAV.Core.ChatChannel.LocalName = PhotonNetwork.NickName;
            NAV.Core.ChatChannel.Receive(null, $"Online: {PhotonNetwork.CurrentRoom.Name} ({(NetworkSession.IsPvp ? "PvP" : "PvE")}).");
            ItemPickup.PickedUp += HandlePickedUp;
            if (_building != null)
            {
                _building.PieceBuilt += HandlePieceBuilt;
                _building.PieceDemolishing += HandlePieceDemolishing;
            }

            if (_forestWrath != null)
            {
                _forestWrath.Message += HandleWrathMessage;
                _forestWrath.OmenStarted += HandleWrathOmen;
            }
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            PhotonNetwork.RemoveCallbackTarget(this);
            if (NAV.Core.ChatChannel.Sender == (System.Action<string>)SendChat)
            {
                NAV.Core.ChatChannel.Sender = null;
            }

            ItemPickup.PickedUp -= HandlePickedUp;
            if (_building != null)
            {
                _building.PieceBuilt -= HandlePieceBuilt;
                _building.PieceDemolishing -= HandlePieceDemolishing;
            }

            if (_forestWrath != null)
            {
                _forestWrath.Message -= HandleWrathMessage;
                _forestWrath.OmenStarted -= HandleWrathOmen;
            }
        }

        /// <summary>Sends a chat line to everyone (this machine included).</summary>
        public void SendChat(string text)
        {
            if (!PhotonNetwork.InRoom || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string line = text.Trim();
            if (line.Length > 200)
            {
                line = line.Substring(0, 200);
            }

            PhotonNetwork.RaiseEvent(ChatCode, new object[] { PhotonNetwork.NickName, line }, ToAll, SendOptions.SendReliable);
        }

        // ---- Local changes out ----

        /// <summary>A world-generated pickup taken here: gone on the other machines too.</summary>
        private void HandlePickedUp(ItemPickup pickup, GameObject interactor, int picked, bool emptied)
        {
            if (emptied && pickup.NetworkId == 0 && PhotonNetwork.InRoom && _world != null && _world.TryGetNodeId(pickup.gameObject, out int id))
            {
                PhotonNetwork.RaiseEvent(DepleteCode, id, CachedToOthers, SendOptions.SendReliable);
            }
        }

        // ---- The forest's wrath (the Leshy encounter of the machine that runs it) ----

        private void HandleWrathMessage(string text)
        {
            if (!_applyingWrath && PlayerPosition(out Vector3 at))
            {
                PhotonNetwork.RaiseEvent(WrathMessageCode, new object[] { text, at }, ToOthersOnly, SendOptions.SendReliable);
            }
        }

        private void HandleWrathOmen(bool angry)
        {
            if (!_applyingWrath && PlayerPosition(out Vector3 at))
            {
                PhotonNetwork.RaiseEvent(WrathOmenCode, new object[] { angry, at }, ToOthersOnly, SendOptions.SendReliable);
            }
        }

        private void Update()
        {
            if (_forestWrath == null || Time.unscaledTime < _nextSilence || !PhotonNetwork.InRoom)
            {
                return;
            }

            _nextSilence = Time.unscaledTime + 1f;
            float silence = _forestWrath.LocalSilence;
            // Renewed every second while it lasts (it expires on the others), once more at 0.
            if ((silence > 0f || _lastSentSilence > 0f) && PlayerPosition(out Vector3 at))
            {
                PhotonNetwork.RaiseEvent(WrathSilenceCode, new object[] { silence, at }, ToOthersOnly, SendOptions.SendUnreliable);
            }

            _lastSentSilence = silence;
        }

        private bool PlayerPosition(out Vector3 position)
        {
            if (_player == null)
            {
                PlayerMotor motor = FindAnyObjectByType<PlayerMotor>();
                _player = motor != null ? motor.transform : null;
            }

            position = _player != null ? _player.position : Vector3.zero;
            return _player != null;
        }

        private bool Near(Vector3 point)
        {
            return PlayerPosition(out Vector3 me) && (me - point).sqrMagnitude <= _wrathShareDistance * _wrathShareDistance;
        }

        private void ApplyWrath(System.Action apply)
        {
            if (_forestWrath == null)
            {
                return;
            }

            _applyingWrath = true;
            try
            {
                apply();
            }
            finally
            {
                _applyingWrath = false;
            }
        }

        private void HandlePieceBuilt(BuildingPiece piece)
        {
            if (!PhotonNetwork.InRoom || piece.Definition == null)
            {
                return;
            }

            piece.NetworkId = PhotonNetwork.LocalPlayer.ActorNumber * 1_000_000_000L + ++_pieceCounter;
            Transform t = piece.transform;
            PhotonNetwork.RaiseEvent(PlaceCode, new object[] { piece.NetworkId, piece.Definition.Id, t.position, t.rotation }, CachedToOthers, SendOptions.SendReliable);
        }

        private void HandlePieceDemolishing(BuildingPiece piece)
        {
            if (PhotonNetwork.InRoom && piece.NetworkId != 0)
            {
                PhotonNetwork.RaiseEvent(DemolishCode, piece.NetworkId, CachedToOthers, SendOptions.SendReliable);
            }
        }

        // ---- Remote changes in ----

        public void OnEvent(EventData photonEvent)
        {
            switch (photonEvent.Code)
            {
                case PlaceCode:
                    PlacePiece((object[])photonEvent.CustomData);
                    break;
                case DemolishCode:
                    RemovePiece((long)photonEvent.CustomData);
                    break;
                case DepleteCode:
                    if (_world != null)
                    {
                        _world.RemoveNode((int)photonEvent.CustomData);
                    }

                    break;
                case ChatCode:
                    var chat = (object[])photonEvent.CustomData;
                    NAV.Core.ChatChannel.Receive(chat[0] as string, chat[1] as string);
                    break;
                case WrathMessageCode:
                    var message = (object[])photonEvent.CustomData;
                    if (Near((Vector3)message[1]))
                    {
                        ApplyWrath(() => _forestWrath.ShowRemoteMessage(message[0] as string));
                    }

                    break;
                case WrathOmenCode:
                    var omen = (object[])photonEvent.CustomData;
                    if (Near((Vector3)omen[1]))
                    {
                        ApplyWrath(() => _forestWrath.PlayRemoteOmen((bool)omen[0]));
                    }

                    break;
                case WrathSilenceCode:
                    var silence = (object[])photonEvent.CustomData;
                    if (Near((Vector3)silence[1]))
                    {
                        ApplyWrath(() => _forestWrath.SetRemoteSilence((float)silence[0]));
                    }

                    break;
            }
        }

        private void PlacePiece(object[] data)
        {
            long id = (long)data[0];
            BuildingPieceDefinition definition = _pieces != null ? _pieces.Get(data[1] as string) : null;
            if (definition == null || definition.Prefab == null)
            {
                Debug.LogWarning($"{nameof(NetworkWorldSync)}: unknown building piece '{data[1]}' from the network - skipped.", this);
                return;
            }

            foreach (BuildingPiece existing in BuildingPiece.AllPieces)
            {
                if (existing.NetworkId == id)
                {
                    return;
                }
            }

            GameObject instance = Instantiate(definition.Prefab, (Vector3)data[2], (Quaternion)data[3]);
            if (instance.TryGetComponent(out BuildingPiece piece))
            {
                piece.Configure(definition);
                piece.NetworkId = id;
            }
        }

        private static void RemovePiece(long id)
        {
            foreach (BuildingPiece piece in BuildingPiece.AllPieces)
            {
                if (piece.NetworkId == id)
                {
                    piece.gameObject.SetActive(false);
                    Destroy(piece.gameObject);
                    return;
                }
            }
        }
    }
}
