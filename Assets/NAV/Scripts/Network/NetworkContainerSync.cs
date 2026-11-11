using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.Building;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;

namespace NAV.Network
{
    /// <summary>
    /// What players do with buildings, online (inert solo):
    ///  - chests and stations (IStoredContents on a building piece): after any change made on a
    ///    machine its whole contents go to the others (last change wins; two players moving
    ///    the same slot at the same moment can still clash - see PROJECT_STATE);
    ///  - stations process only on the master client (ProcessingStation.SimulationSuppressed
    ///    elsewhere): a furnace smelts once, its product drops as a shared item, everyone sees
    ///    the inputs go down;
    ///  - doors: opened/closed for everyone.
    /// Pieces are found by their BuildingPiece.NetworkId (set by NetworkWorldSync). Cached for
    /// late joiners.
    /// </summary>
    public class NetworkContainerSync : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        private const byte ContentsCode = 140;
        private const byte DoorCode = 141;

        [SerializeField] private ItemDatabase _items;
        [SerializeField, Min(0.2f)] private float _scanSeconds = 1f;

        private static readonly RaiseEventOptions ToOthersCached = new() { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.AddToRoomCacheGlobal };

        private readonly HashSet<BuildingPiece> _hooked = new();
        private readonly HashSet<BuildingPiece> _dirty = new();
        private readonly Dictionary<long, object[]> _waitingContents = new();
        private readonly Dictionary<long, bool> _waitingDoors = new();
        private bool _applying;
        private float _nextScan;

        public override void OnEnable()
        {
            if (!PhotonNetwork.InRoom)
            {
                enabled = false;
                return;
            }

            base.OnEnable();
            UpdateStationAuthority();
        }

        public override void OnDisable()
        {
            base.OnDisable();
            ProcessingStation.SimulationSuppressed = false;
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            UpdateStationAuthority();
        }

        private static void UpdateStationAuthority()
        {
            ProcessingStation.SimulationSuppressed = PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient;
        }

        private void Update()
        {
            if (!PhotonNetwork.InRoom)
            {
                return;
            }

            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + _scanSeconds;
                HookNewPieces();
            }

            // Changes of this frame go out once (an action can change several slots).
            foreach (BuildingPiece piece in _dirty)
            {
                if (piece != null && piece.NetworkId != 0)
                {
                    SendContents(piece);
                }
            }

            _dirty.Clear();
        }

        private void HookNewPieces()
        {
            _hooked.RemoveWhere(piece => piece == null);
            foreach (BuildingPiece piece in BuildingPiece.AllPieces)
            {
                if (piece == null || piece.NetworkId == 0 || !_hooked.Add(piece))
                {
                    continue;
                }

                var storage = piece.GetComponentInChildren<IStoredContents>(true);
                if (storage != null && storage.Contents != null)
                {
                    BuildingPiece owner = piece;
                    storage.Contents.Changed += () =>
                    {
                        if (!_applying)
                        {
                            _dirty.Add(owner);
                        }
                    };
                }

                var door = piece.GetComponentInChildren<BuildingDoor>(true);
                if (door != null)
                {
                    long id = piece.NetworkId;
                    door.Toggled += (_, open) => PhotonNetwork.RaiseEvent(DoorCode, new object[] { id, open }, ToOthersCached, SendOptions.SendReliable);
                }

                // Something arrived for it before it was built here (event order on joining).
                if (_waitingContents.TryGetValue(piece.NetworkId, out object[] contents))
                {
                    _waitingContents.Remove(piece.NetworkId);
                    ApplyContents(piece, contents);
                }

                if (_waitingDoors.TryGetValue(piece.NetworkId, out bool doorOpen))
                {
                    _waitingDoors.Remove(piece.NetworkId);
                    if (door != null)
                    {
                        door.SetOpen(doorOpen);
                    }
                }
            }
        }

        private void SendContents(BuildingPiece piece)
        {
            var storage = piece.GetComponentInChildren<IStoredContents>(true);
            if (storage == null || storage.Contents == null)
            {
                return;
            }

            IReadOnlyList<ItemStack> slots = storage.Contents.Slots;
            var ids = new string[slots.Count];
            var quantities = new int[slots.Count];
            for (int i = 0; i < slots.Count; i++)
            {
                ids[i] = slots[i].IsEmpty ? string.Empty : slots[i].Definition.Id;
                quantities[i] = slots[i].IsEmpty ? 0 : slots[i].Quantity;
            }

            PhotonNetwork.RaiseEvent(ContentsCode, new object[] { piece.NetworkId, ids, quantities }, ToOthersCached, SendOptions.SendReliable);
        }

        public void OnEvent(EventData photonEvent)
        {
            switch (photonEvent.Code)
            {
                case ContentsCode:
                    var contents = (object[])photonEvent.CustomData;
                    BuildingPiece piece = Find((long)contents[0]);
                    if (piece == null)
                    {
                        _waitingContents[(long)contents[0]] = contents;
                    }
                    else
                    {
                        ApplyContents(piece, contents);
                    }

                    break;
                case DoorCode:
                    var door = (object[])photonEvent.CustomData;
                    BuildingPiece doorPiece = Find((long)door[0]);
                    BuildingDoor leaf = doorPiece != null ? doorPiece.GetComponentInChildren<BuildingDoor>(true) : null;
                    if (leaf != null)
                    {
                        leaf.SetOpen((bool)door[1]);
                    }
                    else
                    {
                        _waitingDoors[(long)door[0]] = (bool)door[1];
                    }

                    break;
            }
        }

        private void ApplyContents(BuildingPiece piece, object[] data)
        {
            var storage = piece.GetComponentInChildren<IStoredContents>(true);
            if (storage == null || storage.Contents == null)
            {
                return;
            }

            var ids = (string[])data[1];
            var quantities = (int[])data[2];
            var definitions = new ItemDefinition[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                definitions[i] = string.IsNullOrEmpty(ids[i]) || _items == null ? null : _items.Get(ids[i]);
            }

            _applying = true;
            try
            {
                storage.Contents.SetAllSlots(definitions, quantities);
            }
            finally
            {
                _applying = false;
            }
        }

        private static BuildingPiece Find(long id)
        {
            foreach (BuildingPiece piece in BuildingPiece.AllPieces)
            {
                if (piece != null && piece.NetworkId == id)
                {
                    return piece;
                }
            }

            return null;
        }
    }
}
