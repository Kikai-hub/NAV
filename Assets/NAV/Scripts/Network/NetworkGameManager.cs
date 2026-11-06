using System.Collections;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.Player;
using NAV.World.TimeOfDay;
using NAV.World.Weather;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace NAV.Network
{
    /// <summary>
    /// The gameplay scene's side of an online session (inert when playing solo):
    ///   - the world was generated from the room's seed (MultiplayerMenuUIController put it into
    ///     SaveSystem's pending new game before the scene loaded) - same terrain and resources
    ///     everywhere without sending any of it;
    ///   - once the world is up: Photon's message queue resumes (paused for the scene load, so
    ///     the other players' avatars arrive into a ready scene), this player's NetworkAvatar is
    ///     spawned, and a guest steps a little aside from the shared spawn point;
    ///   - a guest doesn't save (SaveSystem.SavingSuspended) - the world is the host's;
    ///   - the host publishes the time of day and the weather every few seconds (room
    ///     properties), the guests follow them;
    ///   - leaving the scene (back to the main menu) leaves the room.
    /// </summary>
    public class NetworkGameManager : MonoBehaviourPunCallbacks
    {
        [SerializeField] private PlayerMotor _playerMotor;
        [SerializeField] private WorldClock _clock;
        [SerializeField] private WeatherSystem _weather;
        [Tooltip("Resources path of the networked player avatar prefab.")]
        [SerializeField] private string _avatarPrefab = "NetworkPlayer";
        [Tooltip("How often the host publishes the time of day and the weather (s).")]
        [SerializeField, Min(1f)] private float _worldSyncSeconds = 5f;
        [Tooltip("Guests spawn around the shared spawn point at this distance (m).")]
        [SerializeField, Min(0f)] private float _guestSpawnSpacing = 2.5f;

        private float _nextWorldSync;

        public static bool IsOnline => PhotonNetwork.InRoom;

        private void Awake()
        {
            if (!PhotonNetwork.InRoom)
            {
                // Solo game - nothing to do.
                SaveSystem.SavingSuspended = false;
                enabled = false;
                return;
            }

            SaveSystem.SavingSuspended = !PhotonNetwork.IsMasterClient;
        }

        private IEnumerator Start()
        {
            if (!PhotonNetwork.InRoom)
            {
                yield break;
            }

            // SaveManager generates the world and places the player in its own Start.
            yield return null;
            if (!PhotonNetwork.InRoom)
            {
                yield break;
            }

            if (!PhotonNetwork.IsMasterClient && _playerMotor != null)
            {
                int index = PhotonNetwork.LocalPlayer.ActorNumber;
                float angle = index * 137.5f * Mathf.Deg2Rad;
                Vector3 aside = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _guestSpawnSpacing;
                _playerMotor.Teleport(_playerMotor.transform.position + aside + Vector3.up * 0.5f, _playerMotor.transform.rotation);
                ApplyWorldState(PhotonNetwork.CurrentRoom.CustomProperties);
            }

            PhotonNetwork.IsMessageQueueRunning = true;
            Transform player = _playerMotor != null ? _playerMotor.transform : transform;
            PhotonNetwork.Instantiate(_avatarPrefab, player.position, player.rotation);
            Debug.Log($"{nameof(NetworkGameManager)}: online in '{PhotonNetwork.CurrentRoom.Name}' as {(PhotonNetwork.IsMasterClient ? "host" : "guest")} ({PhotonNetwork.CurrentRoom.PlayerCount} players, {(NetworkSession.IsPvp ? "PvP" : "PvE")}).", this);
        }

        private void Update()
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || Time.unscaledTime < _nextWorldSync)
            {
                return;
            }

            _nextWorldSync = Time.unscaledTime + _worldSyncSeconds;
            var properties = new Hashtable();
            if (_clock != null)
            {
                properties[NetworkSession.ClockKey] = new float[] { _clock.Day, _clock.Hour };
            }

            if (_weather != null && _weather.Current != null)
            {
                properties[NetworkSession.WeatherKey] = $"{_weather.Current.Id}|{_weather.RemainingSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            }

            if (properties.Count > 0)
            {
                PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
            }
        }

        public override void OnRoomPropertiesUpdate(Hashtable changed)
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                ApplyWorldState(changed);
            }
        }

        /// <summary>A guest follows the host's clock (if more than a few minutes apart) and weather.</summary>
        private void ApplyWorldState(Hashtable properties)
        {
            if (_clock != null && properties.TryGetValue(NetworkSession.ClockKey, out object clock) && clock is float[] time && time.Length == 2)
            {
                float hostHours = time[0] * 24f + time[1];
                float ourHours = _clock.Day * 24f + _clock.Hour;
                if (Mathf.Abs(hostHours - ourHours) > 0.05f)
                {
                    _clock.SetTime(Mathf.RoundToInt(time[0]), time[1]);
                }
            }

            if (_weather != null && properties.TryGetValue(NetworkSession.WeatherKey, out object weather) && weather is string text)
            {
                string[] parts = text.Split('|');
                if (parts.Length == 2 && (_weather.Current == null || _weather.Current.Id != parts[0])
                    && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float remaining))
                {
                    _weather.Restore(parts[0], remaining);
                }
            }
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            // The host left: the new host's machine keeps the world going (and saving).
            if (newMasterClient.IsLocal)
            {
                SaveSystem.SavingSuspended = false;
                Debug.Log($"{nameof(NetworkGameManager)}: the host left - this machine hosts now.", this);
            }
        }

        public override void OnLeftRoom()
        {
            SaveSystem.SavingSuspended = false;
        }

        private void OnDestroy()
        {
            // Back to the main menu (or quitting): leave the room, stay connected for the lobby.
            SaveSystem.SavingSuspended = false;
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }
        }
    }
}
