using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.Building;
using NAV.Gameplay.Items;
using NAV.World.Generation;

namespace NAV.Network
{
    /// <summary>
    /// Keeps the shared world the same on every machine during an online session (inert solo).
    /// The terrain and the resources already match (one seed); what players change is sent as
    /// Photon events:
    ///   - a piece built (definition id, position, rotation, network id) and demolished;
    ///   - a resource node gathered out (its world-generation id) - the others remove it too
    ///     (its stump stays; the felled trunk, logs and drops exist only for whoever felled it);
    ///   - chat lines (NetworkChatUI shows them).
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

        [SerializeField] private WorldGenerator _world;
        [SerializeField] private PlayerBuilding _building;
        [SerializeField] private BuildingPieceDatabase _pieces;

        private static readonly RaiseEventOptions CachedToOthers = new() { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.AddToRoomCacheGlobal };
        private static readonly RaiseEventOptions ToAll = new() { Receivers = ReceiverGroup.All };
        private long _pieceCounter;

        /// <summary>A chat line: who, what.</summary>
        public event Action<string, string> ChatReceived;

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
            ResourceNode.Depleted += HandleNodeDepleted;
            if (_building != null)
            {
                _building.PieceBuilt += HandlePieceBuilt;
                _building.PieceDemolishing += HandlePieceDemolishing;
            }
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            PhotonNetwork.RemoveCallbackTarget(this);
            ResourceNode.Depleted -= HandleNodeDepleted;
            if (_building != null)
            {
                _building.PieceBuilt -= HandlePieceBuilt;
                _building.PieceDemolishing -= HandlePieceDemolishing;
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

        private void HandleNodeDepleted(ResourceNode node)
        {
            if (PhotonNetwork.InRoom && _world != null && _world.TryGetNodeId(node.gameObject, out int id))
            {
                PhotonNetwork.RaiseEvent(DepleteCode, id, CachedToOthers, SendOptions.SendReliable);
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
                    ChatReceived?.Invoke(chat[0] as string, chat[1] as string);
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
