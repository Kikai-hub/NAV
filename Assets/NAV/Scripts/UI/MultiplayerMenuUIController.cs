using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UIElements;
using NAV.Network;

namespace NAV.UI
{
    /// <summary>
    /// The main menu's online parts (same UIDocument as MainMenuUIController):
    ///   - Multiplayer window: your name, the server list (rooms in the lobby: name, world,
    ///     players, PvE/PvP, a lock when it has a password) - a click picks a room into the Room
    ///     field, a double click joins it - the Room and Password fields and Connect;
    ///   - Create World's "Online" toggle with the room name, password and PvE/PvP choice: the
    ///     world is created as a room and then loads.
    /// Joining reads the world's seed and name from the room and loads it like a new game
    /// (every machine generates the same world from the seed). Photon's message queue is paused
    /// for the scene load; NetworkGameManager resumes it in the game scene.
    /// All networking goes through NetworkSession.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MultiplayerMenuUIController : MonoBehaviour
    {
        [SerializeField] private MainMenuUIController _mainMenu;

        private VisualElement _multiplayerPanel;
        private TextField _nicknameField;
        private ScrollView _roomList;
        private Label _roomListEmpty;
        private TextField _joinNameField;
        private TextField _joinPasswordField;
        private Label _status;
        private Toggle _onlineToggle;
        private VisualElement _onlineOptions;
        private TextField _hostNameField;
        private TextField _hostPasswordField;
        private RadioButtonGroup _pvpChoice;
        private Label _createStatus;

        private NetworkSession _session;
        private string _selectedRoom;
        private string _hostWorldName;
        private bool _busy;

        /// <summary>Create World's Online toggle is on.</summary>
        public bool OnlineChosen => _onlineToggle != null && _onlineToggle.value;

        private void Start()
        {
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            _multiplayerPanel = root.Q<VisualElement>("multiplayer-panel");
            _nicknameField = root.Q<TextField>("nickname-field");
            _roomList = root.Q<ScrollView>("room-list");
            _roomListEmpty = root.Q<Label>("room-list-empty");
            _joinNameField = root.Q<TextField>("join-room-name-field");
            _joinPasswordField = root.Q<TextField>("join-room-password-field");
            _status = root.Q<Label>("multiplayer-status");
            _onlineToggle = root.Q<Toggle>("online-toggle");
            _onlineOptions = root.Q<VisualElement>("online-options");
            _hostNameField = root.Q<TextField>("host-room-name-field");
            _hostPasswordField = root.Q<TextField>("host-room-password-field");
            _pvpChoice = root.Q<RadioButtonGroup>("host-pvp-choice");
            _createStatus = root.Q<Label>("create-world-status");
            var multiplayerButton = root.Q<Button>("multiplayer-button");
            var joinButton = root.Q<Button>("join-room-button");
            var backButton = root.Q<Button>("multiplayer-back-button");
            var refreshButton = root.Q<Button>("refresh-rooms-button");
            if (_mainMenu == null || _multiplayerPanel == null || _roomList == null || _onlineToggle == null || _onlineOptions == null
                || multiplayerButton == null || joinButton == null || backButton == null || _pvpChoice == null)
            {
                Debug.LogError($"{nameof(MultiplayerMenuUIController)} on '{name}' is missing its MainMenuUIController or elements of the menu document.", this);
                enabled = false;
                return;
            }

            multiplayerButton.clicked += Open;
            joinButton.clicked += JoinTyped;
            backButton.clicked += Close;
            refreshButton?.RegisterCallback<ClickEvent>(_ => Session.RefreshRooms());
            _onlineToggle.RegisterValueChangedCallback(change => ShowOnlineOptions(change.newValue));
            ShowOnlineOptions(false);
            _multiplayerPanel.style.display = DisplayStyle.None;
            SetText(_createStatus, string.Empty);
        }

        private NetworkSession Session
        {
            get
            {
                if (_session == null)
                {
                    _session = NetworkSession.Instance;
                    _session.StatusChanged += HandleStatus;
                    _session.Failed += HandleFailed;
                    _session.RoomListChanged += BuildRoomList;
                    _session.JoinedRoom += HandleJoinedRoom;
                }

                return _session;
            }
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.StatusChanged -= HandleStatus;
                _session.Failed -= HandleFailed;
                _session.RoomListChanged -= BuildRoomList;
                _session.JoinedRoom -= HandleJoinedRoom;
            }
        }

        // ---- Multiplayer window ----

        private void Open()
        {
            _mainMenu.HideAllPanels();
            _multiplayerPanel.style.display = DisplayStyle.Flex;
            _nicknameField.value = Session.Nickname;
            SetText(_status, string.Empty);
            BuildRoomList();
            Session.Connect();
        }

        private void Close()
        {
            Session.Nickname = _nicknameField.value;
            _multiplayerPanel.style.display = DisplayStyle.None;
            _mainMenu.ShowMainMenu();
        }

        private void BuildRoomList()
        {
            if (_roomList == null)
            {
                return;
            }

            _roomList.Clear();
            List<RoomInfo> rooms = _session != null ? _session.Rooms.OrderBy(r => r.Name).ToList() : new List<RoomInfo>();
            _roomListEmpty.style.display = rooms.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (RoomInfo room in rooms)
            {
                var item = new VisualElement();
                item.AddToClassList("room-list-item");
                if (room.Name == _selectedRoom)
                {
                    item.AddToClassList("room-list-item--selected");
                }

                var info = new VisualElement { pickingMode = PickingMode.Ignore };
                info.AddToClassList("room-list-item__info");
                bool locked = room.CustomProperties.TryGetValue(NetworkSession.LockedKey, out object lockValue) && lockValue is bool isLocked && isLocked;
                bool pvp = room.CustomProperties.TryGetValue(NetworkSession.PvpKey, out object pvpValue) && pvpValue is bool isPvp && isPvp;
                string world = room.CustomProperties.TryGetValue(NetworkSession.WorldKey, out object worldValue) ? worldValue as string : null;
                var name = new Label(room.Name) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("room-list-item__name");
                info.Add(name);
                var details = new Label($"{(string.IsNullOrEmpty(world) ? "World" : world)}  ·  {(pvp ? "PvP" : "PvE")}{(locked ? "  ·  password" : string.Empty)}") { pickingMode = PickingMode.Ignore };
                details.AddToClassList("room-list-item__details");
                info.Add(details);
                item.Add(info);
                var players = new Label($"{room.PlayerCount}/{room.MaxPlayers}") { pickingMode = PickingMode.Ignore };
                players.AddToClassList("room-list-item__players");
                item.Add(players);

                string roomName = room.Name;
                item.RegisterCallback<ClickEvent>(click =>
                {
                    _selectedRoom = roomName;
                    _joinNameField.value = roomName;
                    if (click.clickCount >= 2)
                    {
                        JoinTyped();
                    }
                    else
                    {
                        BuildRoomList();
                    }
                });
                _roomList.Add(item);
            }
        }

        private void JoinTyped()
        {
            if (_busy)
            {
                return;
            }

            Session.Nickname = _nicknameField.value;
            _busy = true;
            Session.JoinRoom(_joinNameField.value, _joinPasswordField.value);
        }

        // ---- Create World: online ----

        private void ShowOnlineOptions(bool online)
        {
            _onlineOptions.style.display = online ? DisplayStyle.Flex : DisplayStyle.None;
            if (online && string.IsNullOrWhiteSpace(_hostNameField.value))
            {
                _hostNameField.value = $"{Session.Nickname}'s world";
            }
        }

        /// <summary>Create World with Online on: open the room, the world loads once it exists.</summary>
        public void HostWorld(string worldName, int seed)
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            _hostWorldName = worldName;
            Session.CreateRoom(_hostNameField.value, _hostPasswordField.value, _pvpChoice.value == 1, worldName, seed);
        }

        // ---- Session events ----

        private void HandleStatus(string message)
        {
            SetText(_status, message);
            SetText(_createStatus, OnlineChosen ? message : string.Empty);
        }

        private void HandleFailed(string message)
        {
            _busy = false;
            SetText(_status, message);
            SetText(_createStatus, message);
        }

        private void HandleJoinedRoom(bool isHost)
        {
            _busy = false;
            string worldName = isHost ? _hostWorldName : NetworkSession.RoomWorldName;
            // Avatars and messages for the game scene wait until it's ready (NetworkGameManager).
            PhotonNetwork.IsMessageQueueRunning = false;
            _mainMenu.LaunchNewGame(string.IsNullOrEmpty(worldName) ? PhotonNetwork.CurrentRoom.Name : worldName, NetworkSession.RoomSeed);
        }

        private static void SetText(Label label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }
    }
}
