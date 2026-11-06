using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace NAV.Network
{
    /// <summary>
    /// The game's one connection to Photon (PUN 2) - lives across scenes (DontDestroyOnLoad),
    /// created on demand by Instance. Owns:
    ///   - connecting (PhotonServerSettings: App Id, fixed region "eu" so every player sees the
    ///     same room list) and staying in the lobby for the room list;
    ///   - rooms = online worlds: Create (host: room name, password, PvE/PvP, world name + seed)
    ///     and Join (by name + password).
    /// Room properties: "seed" (the world is generated from it on every machine - the terrain
    /// and resources match without sending them), "world" (its name), "pvp" (players can hurt
    /// each other), "lock" (has a password) - those three shown in the lobby - and "pwh", a
    /// salted hash of the password (not in the lobby list). A joining player checks the hash
    /// right after joining and leaves on a mismatch; the host checks it too (the joiner's own
    /// "pwh" player property) and closes the connection of anyone without it.
    /// UI (MultiplayerMenuUIController) only calls this and listens to its events.
    /// </summary>
    public class NetworkSession : MonoBehaviourPunCallbacks
    {
        public const string SeedKey = "seed";
        public const string WorldKey = "world";
        public const string PvpKey = "pvp";
        public const string LockedKey = "lock";
        public const string PasswordHashKey = "pwh";
        public const string EquipmentKey = "eq";
        public const string ClockKey = "clk";
        public const string WeatherKey = "wth";
        private const string NicknamePref = "nav.nickname";
        private const int MaxPlayers = 4;

        private static NetworkSession _instance;
        private readonly Dictionary<string, RoomInfo> _rooms = new();
        private Action _whenConnected;
        private string _pendingPasswordHash;

        /// <summary>The session, created (and kept across scenes) on first use.</summary>
        public static NetworkSession Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("NetworkSession");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<NetworkSession>();
                }

                return _instance;
            }
        }

        /// <summary>True when an online session exists (without creating one).</summary>
        public static bool Exists => _instance != null;

        public static bool InRoom => PhotonNetwork.InRoom;
        public static bool IsHost => PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient;

        /// <summary>Players can damage each other in this room.</summary>
        public static bool IsPvp => PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PvpKey, out object pvp) && pvp is bool on && on;

        public static int RoomSeed => PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(SeedKey, out object seed) && seed is int value ? value : 0;
        public static string RoomWorldName => PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(WorldKey, out object world) ? world as string : null;

        public IReadOnlyCollection<RoomInfo> Rooms => _rooms.Values;
        public bool IsConnected => PhotonNetwork.IsConnectedAndReady;

        public string Nickname
        {
            get
            {
                // A random default once, then kept (it used to change on every read).
                if (!PlayerPrefs.HasKey(NicknamePref))
                {
                    PlayerPrefs.SetString(NicknamePref, $"Wanderer{UnityEngine.Random.Range(100, 999)}");
                }

                return PlayerPrefs.GetString(NicknamePref);
            }
            set
            {
                string clean = string.IsNullOrWhiteSpace(value) ? "Wanderer" : value.Trim();
                PlayerPrefs.SetString(NicknamePref, clean);
                PhotonNetwork.NickName = clean;
            }
        }

        /// <summary>A line for the UI ("Connecting...", "Wrong password"...).</summary>
        public event Action<string> StatusChanged;

        public event Action RoomListChanged;

        /// <summary>Joined (or created) a room - the world can load. Argument: true for the host.</summary>
        public event Action<bool> JoinedRoom;

        /// <summary>Connecting, creating or joining failed - the message says why.</summary>
        public event Action<string> Failed;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            PhotonNetwork.AutomaticallySyncScene = false;
            PhotonNetwork.SendRate = 20;
            PhotonNetwork.SerializationRate = 15;
            PhotonNetwork.EnableCloseConnection = true;
        }

        /// <summary>Connects (if not yet) and joins the lobby; then runs the action.</summary>
        public void Connect(Action whenConnected = null)
        {
            if (string.IsNullOrEmpty(PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime))
            {
                Failed?.Invoke("No Photon App Id in PhotonServerSettings - online play is unavailable.");
                return;
            }

            PhotonNetwork.NickName = Nickname;
            if (PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby)
            {
                whenConnected?.Invoke();
                return;
            }

            _whenConnected += whenConnected;
            if (PhotonNetwork.IsConnectedAndReady)
            {
                PhotonNetwork.JoinLobby();
                return;
            }

            if (!PhotonNetwork.IsConnected)
            {
                StatusChanged?.Invoke("Connecting...");
                PhotonNetwork.ConnectUsingSettings();
            }
        }

        /// <summary>The host opens a room for a new online world.</summary>
        public void CreateRoom(string roomName, string password, bool pvp, string worldName, int seed)
        {
            roomName = Clean(roomName);
            if (roomName.Length == 0)
            {
                Failed?.Invoke("Enter a room name.");
                return;
            }

            Connect(() =>
            {
                if (_rooms.ContainsKey(roomName))
                {
                    Failed?.Invoke($"A room called '{roomName}' already exists.");
                    return;
                }

                _pendingPasswordHash = HashPassword(roomName, password);
                bool locked = !string.IsNullOrEmpty(password);
                var properties = new Hashtable
                {
                    { SeedKey, seed },
                    { WorldKey, worldName },
                    { PvpKey, pvp },
                    { LockedKey, locked },
                    { PasswordHashKey, _pendingPasswordHash },
                };
                var options = new RoomOptions
                {
                    MaxPlayers = MaxPlayers,
                    IsVisible = true,
                    IsOpen = true,
                    CleanupCacheOnLeave = true,
                    CustomRoomProperties = properties,
                    CustomRoomPropertiesForLobby = new[] { WorldKey, PvpKey, LockedKey },
                };
                SetLocalPasswordHash(_pendingPasswordHash);
                StatusChanged?.Invoke($"Creating '{roomName}'...");
                PhotonNetwork.CreateRoom(roomName, options);
            });
        }

        /// <summary>Joins someone's room by its name and password.</summary>
        public void JoinRoom(string roomName, string password)
        {
            roomName = Clean(roomName);
            if (roomName.Length == 0)
            {
                Failed?.Invoke("Choose a room or type its name.");
                return;
            }

            Connect(() =>
            {
                _pendingPasswordHash = HashPassword(roomName, password);
                SetLocalPasswordHash(_pendingPasswordHash);
                StatusChanged?.Invoke($"Joining '{roomName}'...");
                PhotonNetwork.JoinRoom(roomName);
            });
        }

        /// <summary>Leaves the room (back to the lobby). Safe to call when not in one.</summary>
        public void LeaveRoom()
        {
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }
        }

        public void RefreshRooms()
        {
            Connect(() => RoomListChanged?.Invoke());
        }

        /// <summary>Salted hash of a room's password (the salt is the room's name).</summary>
        public static string HashPassword(string roomName, string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return string.Empty;
            }

            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes($"NAV:{roomName}:{password}"));
            var text = new StringBuilder(32);
            for (int i = 0; i < 16; i++)
            {
                text.Append(bytes[i].ToString("x2"));
            }

            return text.ToString();
        }

        private static string Clean(string roomName) => string.IsNullOrWhiteSpace(roomName) ? string.Empty : roomName.Trim();

        private static void SetLocalPasswordHash(string hash)
        {
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PasswordHashKey, hash } });
        }

        // ---- Photon callbacks ----

        public override void OnConnectedToMaster()
        {
            StatusChanged?.Invoke("Connected.");
            PhotonNetwork.JoinLobby();
        }

        public override void OnJoinedLobby()
        {
            _rooms.Clear();
            Action pending = _whenConnected;
            _whenConnected = null;
            RoomListChanged?.Invoke();
            pending?.Invoke();
        }

        public override void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            foreach (RoomInfo room in roomList)
            {
                if (room.RemovedFromList || !room.IsOpen || !room.IsVisible)
                {
                    _rooms.Remove(room.Name);
                }
                else
                {
                    _rooms[room.Name] = room;
                }
            }

            RoomListChanged?.Invoke();
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            _rooms.Clear();
            _whenConnected = null;
            if (cause != DisconnectCause.DisconnectByClientLogic)
            {
                Failed?.Invoke($"Disconnected: {cause}.");
            }

            RoomListChanged?.Invoke();
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            Failed?.Invoke($"Could not create the room: {message}");
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            Failed?.Invoke(returnCode == ErrorCode.GameFull ? "The room is full." : returnCode == ErrorCode.GameDoesNotExist ? "No such room." : $"Could not join: {message}");
        }

        public override void OnJoinedRoom()
        {
            Room room = PhotonNetwork.CurrentRoom;
            string expected = room.CustomProperties.TryGetValue(PasswordHashKey, out object hash) ? hash as string ?? string.Empty : string.Empty;
            if (!PhotonNetwork.IsMasterClient && expected != (_pendingPasswordHash ?? string.Empty))
            {
                Failed?.Invoke("Wrong password.");
                PhotonNetwork.LeaveRoom();
                return;
            }

            StatusChanged?.Invoke($"In '{room.Name}'.");
            JoinedRoom?.Invoke(PhotonNetwork.IsMasterClient);
        }

        /// <summary>The host turns away anyone who joined without the room's password (a client
        /// that skipped its own check).</summary>
        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                return;
            }

            string expected = PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PasswordHashKey, out object hash) ? hash as string ?? string.Empty : string.Empty;
            string given = newPlayer.CustomProperties.TryGetValue(PasswordHashKey, out object theirs) ? theirs as string ?? string.Empty : string.Empty;
            if (expected.Length > 0 && given != expected)
            {
                Debug.LogWarning($"{nameof(NetworkSession)}: '{newPlayer.NickName}' joined without the password - closing their connection.");
                PhotonNetwork.CloseConnection(newPlayer);
            }
        }
    }
}
