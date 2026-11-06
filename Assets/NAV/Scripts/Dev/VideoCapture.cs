#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;
using NAV.Core.Save;
using NAV.Gameplay.AI;
using NAV.Gameplay.Building;
using NAV.Gameplay.Combat;
using NAV.Gameplay.Equipment;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;
using NAV.Gameplay.Player;
using NAV.Presentation.Camera;
using NAV.Presentation.Lighting;
using NAV.UI;
using NAV.World.Generation;
using NAV.World.Generation.Hybrid;
using NAV.World.TimeOfDay;
using NAV.World.Weather;

namespace NAV.Dev
{
    /// <summary>
    /// Video capture aid (editor only): stages one clip for the devlogs - world seed, place, time,
    /// weather, camera move, held item, creatures, scripted walking and swings - and tells the
    /// editor-side NavVideoRecorder when to record. Nothing here runs unless a capture queue is
    /// active (EditorPrefs navcap.enabled = 1), and no game code depends on it. The clip is a
    /// "key=value;key=value" string (see ProjectTitle/VIDEO_CAPTURE.md for the keys).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class VideoCapture : MonoBehaviour
    {
        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const string GameScene = "WorldGenTest";
        private const float RecordFps = 60f;

        private readonly Dictionary<string, string> _clip = new();
        private PlayerMotor _motor;
        private PlayerHealth _health;
        private PlayerInputHandler _input;
        private WorldGenerator _world;
        private WorldClock _clock;
        private WeatherSystem _weather;
        private ThirdPersonCameraController _follow;
        private UnityEngine.Camera _camera;
        private PropertyInfo _moveInput;
        private PropertyInfo _sprintHeld;
        private Action _attack;

        private bool _setUp;
        private bool _clipRunning;
        private bool _finished;
        private float _settleUntil;
        private int _recFrames = -1;
        private float _nextSwing;
        private Vector3 _orbitCentre;
        private float _holdHour = -1f;
        private Creature _foe;
        private GameObject _ball;
        private VideoCaptureLift _lift;

        private string S(string key, string fallback = "") => _clip.TryGetValue(key, out string value) ? value : fallback;
        private float F(string key, float fallback = 0f) => _clip.TryGetValue(key, out string value) ? float.Parse(value, CultureInfo.InvariantCulture) : fallback;
        private bool On(string key, bool fallback = false) => _clip.TryGetValue(key, out string value) ? value != "0" : fallback;

        private static void Parse(string text, IDictionary<string, string> into)
        {
            foreach (string pair in text.Split(';'))
            {
                string[] parts = pair.Split(new[] { '=' }, 2);
                if (parts.Length == 2)
                {
                    into[parts[0].Trim()] = parts[1].Trim();
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureWorld()
        {
            if (EditorPrefs.GetInt("navcap.enabled", 0) != 1)
            {
                return;
            }

            var clip = new Dictionary<string, string>();
            Parse(EditorPrefs.GetString("navcap.clip", string.Empty), clip);
            SaveSystem.PendingLoadSaveId = null;
            SaveSystem.PendingNewGameName = "NAV video capture";
            SaveSystem.PendingNewGameSeed = clip.TryGetValue("seed", out string seed) ? int.Parse(seed, CultureInfo.InvariantCulture) : 1337;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Spawn()
        {
            if (EditorPrefs.GetInt("navcap.enabled", 0) == 1
                && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == GameScene)
            {
                new GameObject(nameof(VideoCapture)).AddComponent<VideoCapture>();
            }
        }

        private void Awake()
        {
            Parse(EditorPrefs.GetString("navcap.clip", string.Empty), _clip);
            Application.runInBackground = true;
        }

        private void Update()
        {
            if (_finished)
            {
                return;
            }

            if (!_setUp)
            {
                TrySetUp();
                return;
            }

            // Losing the editor's focus can raise the pause menu; a capture never pauses.
            Time.timeScale = 1f;

            if (_holdHour >= 0f)
            {
                if (_clipRunning)
                {
                    _holdHour = Mathf.Repeat(_holdHour + F("hourSpeed") * Time.deltaTime, 24f);
                }

                _clock.SetTime(_clock.Day, _holdHour);
            }

            if (!On("mortal"))
            {
                _health.SetHealth(_health.MaxHealth);
            }

            if (On("pink"))
            {
                MakeRainPink();
            }

            if (!_clipRunning)
            {
                if (_world.PendingTerrainTiles == 0 && Time.realtimeSinceStartup >= _settleUntil)
                {
                    BeginClip();
                }

                return;
            }

            Perform();
            AdvanceRecording();
        }

        private void TrySetUp()
        {
            _motor = FindAnyObjectByType<PlayerMotor>();
            _world = FindAnyObjectByType<WorldGenerator>();
            if (_motor == null || _world == null || !_world.HasSpawnPoint)
            {
                return;
            }

            EditorPrefs.SetString("navcap.saveId", SaveSystem.CurrentSaveId ?? string.Empty);
            _health = _motor.GetComponent<PlayerHealth>();
            _input = _motor.GetComponent<PlayerInputHandler>();
            _clock = FindAnyObjectByType<WorldClock>();
            _weather = FindAnyObjectByType<WeatherSystem>();
            _follow = FindAnyObjectByType<ThirdPersonCameraController>();
            _camera = _follow.GetComponent<UnityEngine.Camera>();

            // Replaces the real input for the clip: Update here sets what PlayerMotor reads.
            _moveInput = typeof(PlayerInputHandler).GetProperty(nameof(PlayerInputHandler.MoveInput), Any);
            _sprintHeld = typeof(PlayerInputHandler).GetProperty(nameof(PlayerInputHandler.SprintHeld), Any);
            var attackField = typeof(PlayerInputHandler).GetField(nameof(PlayerInputHandler.AttackPerformed), Any);
            _attack = () => (attackField.GetValue(_input) as Action)?.Invoke();
            _input.enabled = false;

            foreach (PauseUIController pause in FindObjectsByType<PauseUIController>(FindObjectsSortMode.None))
            {
                pause.enabled = false;
            }

            if (!On("hud"))
            {
                HideInterface();
            }

            if (_clip.ContainsKey("hour"))
            {
                _holdHour = F("hour");
            }

            string weather = S("weather");
            if (weather.Length > 0)
            {
                _weather.Restore(weather, 100000f);
            }

            string held = S("hold");
            if (held.Length > 0)
            {
                // PlayerEquipment only equips what the inventory really holds.
                var item = LoadAsset<ItemDefinition>("Assets/NAV/ScriptableObjects/Items/" + held + ".asset");
                _motor.GetComponent<PlayerInventory>().Inventory.SetSlot(0, item, 1);
                _motor.GetComponent<PlayerEquipment>().Restore(item);
            }

            Vector3 place = FindPlace();
            _motor.Teleport(place + Vector3.up, Quaternion.Euler(0f, F("face"), 0f));
            _settleUntil = Time.realtimeSinceStartup + F("settle", 6f);
            _setUp = true;
        }

        private void HideInterface()
        {
            foreach (UIDocument document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (document.rootVisualElement != null)
                {
                    document.rootVisualElement.style.display = DisplayStyle.None;
                }
            }

            foreach (PlayerDebugHud debugHud in FindObjectsByType<PlayerDebugHud>(FindObjectsSortMode.None))
            {
                debugHud.enabled = false;
            }
        }

        /// <summary>at=x,z is an exact spot; biome=Name is the nearest land of that biome around
        /// the spawn (minR metres out at least, for when the spawn's safe zone must be left);
        /// otherwise the spawn point.</summary>
        private Vector3 FindPlace()
        {
            Vector3 spawn = _world.SpawnPoint;
            string at = S("at");
            if (at.Length > 0)
            {
                string[] xz = at.Split(',');
                float x = float.Parse(xz[0], CultureInfo.InvariantCulture);
                float z = float.Parse(xz[1], CultureInfo.InvariantCulture);
                return new Vector3(x, Mathf.Max(_world.Shape.Sample(x, z).Height, WorldGenerator.SeaLevel), z);
            }

            string biome = S("biome");
            if (biome.Length == 0)
            {
                return spawn;
            }

            bool water = biome == "Water"; // open water deep enough for the boat, not a biome
            float minRadius = F("minR");
            for (float radius = Mathf.Max(minRadius, 8f); radius < 15000f; radius += 32f)
            {
                int steps = Mathf.Max(8, Mathf.RoundToInt(radius / 16f));
                for (int i = 0; i < steps; i++)
                {
                    float angle = i * Mathf.PI * 2f / steps;
                    float x = spawn.x + Mathf.Cos(angle) * radius;
                    float z = spawn.z + Mathf.Sin(angle) * radius;
                    WorldPoint point = _world.Shape.Sample(x, z);
                    if (water && point.Height < WorldGenerator.SeaLevel - 3f)
                    {
                        return new Vector3(x, WorldGenerator.SeaLevel, z);
                    }

                    if (!water && point.Height > WorldGenerator.SeaLevel + 0.5f && point.Height < F("maxH", 100000f)
                        && point.Biome != null && point.Biome.name.Contains(biome))
                    {
                        return new Vector3(x, point.Height, z);
                    }
                }
            }

            Debug.LogError($"{nameof(VideoCapture)}: no land of biome '{biome}' found, using the spawn point.", this);
            return spawn;
        }

        private void BeginClip()
        {
            StandNear();
            SpawnProps();
            SpawnArsenal();
            SpawnCreatures();
            if (_clip.ContainsKey("hp"))
            {
                _health.SetHealth(F("hp"));
            }

            if (On("hide"))
            {
                foreach (Renderer part in _motor.GetComponentsInChildren<Renderer>())
                {
                    part.enabled = false;
                }
            }

            if (_clip.ContainsKey("fight"))
            {
                _motor.GetComponent<PlayerCombat>().HitLanded += LogHit;            }

            _orbitCentre = _motor.transform.position;
            _follow.enabled = S("cam", "orbit") == "follow";
            if (_follow.enabled)
            {
                SetField(_follow, "_yaw", F("yaw"));
                SetField(_follow, "_pitch", F("pitch", 12f));
                SetField(_follow, "_distance", F("dist", 4.5f));
            }

            if (_clip.ContainsKey("fov"))
            {
                _camera.fieldOfView = F("fov");
            }

            // Big pull-backs: the view-distance fog is built for eye level and hides everything
            // from high up, so fog=0 lifts it and far= opens the view.
            if (!On("fog", true))
            {
                foreach (AtmosphereFog fog in FindObjectsByType<AtmosphereFog>(FindObjectsSortMode.None))
                {
                    fog.enabled = false;
                }

                RenderSettings.fog = false;
            }

            if (_clip.ContainsKey("far"))
            {
                _camera.farClipPlane = F("far");
            }

            _clipRunning = true;
            EditorPrefs.SetInt("navcap.rec", 1);
        }

        /// <summary>near=Spruce:1.6 - stands the player that far from the nearest resource node
        /// with that in its name, facing it (for chopping and mining shots).</summary>
        private void StandNear()
        {
            string near = S("near");
            if (near.Length == 0)
            {
                return;
            }

            string[] parts = near.Split(':');
            Vector3 from = _motor.transform.position;
            ResourceNode nearest = null;
            foreach (ResourceNode node in FindObjectsByType<ResourceNode>(FindObjectsSortMode.None))
            {
                if (node.name.Contains(parts[0]) && (nearest == null
                    || (node.transform.position - from).sqrMagnitude < (nearest.transform.position - from).sqrMagnitude))
                {
                    nearest = node;
                }
            }

            if (nearest == null)
            {
                Debug.LogError($"{nameof(VideoCapture)}: no resource node named '{parts[0]}' is loaded here.", this);
                return;
            }

            Vector3 target = nearest.transform.position;
            Vector3 toTarget = Vector3.ProjectOnPlane(target - from, Vector3.up).normalized;
            float gap = parts.Length > 1 ? float.Parse(parts[1], CultureInfo.InvariantCulture) : 1.6f;
            Vector3 stand = target - toTarget * gap;
            stand.y = _world.GroundHeight(stand.x, stand.z) + 0.1f;
            _motor.Teleport(stand, Quaternion.LookRotation(toTarget));
            _clip["yaw"] = Quaternion.LookRotation(toTarget).eulerAngles.y.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>props=Build/Forge:2:6:0:0|capsule:0:5:0:0 - a prefab under Assets/NAV/Prefabs
        /// (or capsule, sphere) : metres right : metres ahead of the player : yaw : extra height.
        /// Set dressing for a shot, resting on the ground or the water. level=1 puts every prop
        /// on one height, the highest ground under them: the pieces of a building stay aligned.</summary>
        private void SpawnProps()
        {
            string props = S("props");
            if (props.Length == 0)
            {
                return;
            }

            float level = float.MinValue;
            if (On("level"))
            {
                foreach (string prop in props.Split('|'))
                {
                    string[] parts = prop.Split(':');
                    Vector3 spot = _motor.transform.position
                        + _motor.transform.right * float.Parse(parts[1], CultureInfo.InvariantCulture)
                        + _motor.transform.forward * float.Parse(parts[2], CultureInfo.InvariantCulture);
                    level = Mathf.Max(level, _world.GroundHeight(spot.x, spot.z));
                }
            }

            foreach (string prop in props.Split('|'))
            {
                string[] parts = prop.Split(':');
                float Number(int index) => parts.Length > index ? float.Parse(parts[index], CultureInfo.InvariantCulture) : 0f;

                Vector3 spot = _motor.transform.position + _motor.transform.right * Number(1) + _motor.transform.forward * Number(2);
                spot.y = Mathf.Max(level, _world.GroundHeight(spot.x, spot.z), WorldGenerator.SeaLevel) + Number(4);
                Quaternion rotation = _motor.transform.rotation * Quaternion.Euler(0f, Number(3), 0f);

                if (parts[0] == "capsule" || parts[0] == "sphere")
                {
                    GameObject shape = GameObject.CreatePrimitive(parts[0] == "capsule" ? PrimitiveType.Capsule : PrimitiveType.Sphere);
                    shape.transform.SetPositionAndRotation(spot + Vector3.up, rotation);
                    if (parts[0] == "sphere" && _clip.ContainsKey("lift"))
                    {
                        _ball = shape;
                        shape.transform.localScale = Vector3.one * F("ballSize", 1f);
                        shape.GetComponent<Collider>().enabled = false; // the hero walks right up to it
                    }

                    continue;
                }

                var prefab = LoadAsset<GameObject>("Assets/NAV/Prefabs/" + parts[0] + ".prefab");
                if (prefab != null)
                {
                    MarkPlaced(Instantiate(prefab, spot, rotation), prefab.name);
                }
            }
        }

        /// <summary>Some building pieces (the boat) only work once placed, which the hammer does
        /// through Configure.</summary>
        private static void MarkPlaced(GameObject instance, string prefabName)
        {
            if (!instance.TryGetComponent(out BuildingPiece piece) || piece.IsPlaced)
            {
                return;
            }

            string[] found = AssetDatabase.FindAssets(prefabName + " t:" + nameof(BuildingPieceDefinition));
            if (found.Length > 0)
            {
                piece.Configure(AssetDatabase.LoadAssetAtPath<BuildingPieceDefinition>(AssetDatabase.GUIDToAssetPath(found[0])));
            }
        }

        /// <summary>arsenal=1 lays every weapon of the pack out on the grass in rows ahead of the
        /// player: the 155 weapons that once stood on racks by the spawn.</summary>
        private void SpawnArsenal()
        {
            if (!On("arsenal"))
            {
                return;
            }

            const int Columns = 20;
            const float Spacing = 0.95f;
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/LowPolyWeapons/Prefabs" });
            for (int i = 0; i < guids.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
                float right = (i % Columns - (Columns - 1) * 0.5f) * Spacing;
                float ahead = 2f + (i / Columns) * Spacing * 1.4f;
                Vector3 spot = _motor.transform.position + _motor.transform.right * right + _motor.transform.forward * ahead;
                spot.y = _world.GroundHeight(spot.x, spot.z) + 0.8f;
                GameObject weapon = Instantiate(prefab, spot, _motor.transform.rotation * Quaternion.Euler(0f, 180f, 0f));
                weapon.transform.localScale = Vector3.one * F("arsenalScale", 0.45f);
            }
        }

        /// <summary>A reconstruction of the build-only bug: the shader that is missing renders magenta.</summary>
        private void MakeRainPink()
        {
            var missing = new Material(Shader.Find("Hidden/InternalErrorShader"));
            foreach (ParticleSystemRenderer rain in FindObjectsByType<ParticleSystemRenderer>(FindObjectsSortMode.None))
            {
                if (rain.gameObject.name.Contains("Rain") && rain.sharedMaterial != missing)
                {
                    rain.sharedMaterial = missing;
                }
            }

            _clip["pink"] = "0";
        }

        /// <summary>spawn=ForestWolf:2:12 - prefab name : count : metres in front of the player.
        /// calm=1 leaves them standing still (no AI), for a stare-down.</summary>
        private void SpawnCreatures()
        {
            string spawn = S("spawn");
            if (spawn.Length == 0)
            {
                return;
            }

            string[] parts = spawn.Split(':');
            var prefab = LoadAsset<GameObject>("Assets/NAV/Prefabs/Creatures/" + parts[0] + ".prefab");
            int count = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 1;
            float distance = parts.Length > 2 ? float.Parse(parts[2], CultureInfo.InvariantCulture) : 10f;
            Vector3 front = _motor.transform.position + _motor.transform.forward * distance;

            for (int i = 0; i < count; i++)
            {
                Vector2 scatter = UnityEngine.Random.insideUnitCircle * 3f;
                if (NavMesh.SamplePosition(front + new Vector3(scatter.x, 0f, scatter.y), out NavMeshHit hit, 12f, NavMesh.AllAreas))
                {
                    GameObject creature = Instantiate(prefab, hit.position, Quaternion.LookRotation(_motor.transform.position - hit.position));
                    if (_foe == null)
                    {
                        _foe = creature.GetComponent<Creature>();
                    }

                    if (On("calm"))
                    {
                        creature.GetComponent<Creature>().enabled = false;
                        creature.GetComponent<NavMeshAgent>().enabled = false;
                    }
                }
                else
                {
                    Debug.LogError($"{nameof(VideoCapture)}: no NavMesh for '{parts[0]}' near the player.", this);
                }
            }
        }

        /// <summary>walk=heading in degrees (world space); swing=seconds between attacks;
        /// lift=metres stops the walk that far from the ball and lifts it; fight=metres steps up
        /// to the first spawned creature until it is that close and swings only from there.</summary>
        private void Perform()
        {
            Vector3 direction = Vector3.zero;
            if (_clip.ContainsKey("walk") && ReadyToWalk() && !ReachedBall())
            {
                direction = Quaternion.Euler(0f, F("walk"), 0f) * Vector3.forward;
            }

            bool hasFoe = FoeDirection(out Vector3 toFoe);
            bool inReach = hasFoe && toFoe.magnitude <= F("fight");
            if (hasFoe && !inReach)
            {
                direction = toFoe.normalized;
            }

            Vector3 forward = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            _moveInput.SetValue(_input, new Vector2(Vector3.Dot(direction, right), Vector3.Dot(direction, forward)));
            _sprintHeld.SetValue(_input, On("sprint"));

            float swing = F("swing");
            if (swing > 0f && ClipSeconds >= _nextSwing && (inReach || !_clip.ContainsKey("fight")))
            {
                _nextSwing = ClipSeconds + swing;
                _attack();
            }
        }

        // The ball is close: the walk to it has to be on film, the recorder takes a moment to start.
        private bool ReadyToWalk() => !_clip.ContainsKey("lift") || _recFrames >= 0;

        private bool ReachedBall()
        {
            if (_ball == null)
            {
                return false;
            }

            if (_lift == null)
            {
                Vector3 gap = Vector3.ProjectOnPlane(_ball.transform.position - _motor.transform.position, Vector3.up);
                if (gap.magnitude > F("lift"))
                {
                    return false;
                }

                _lift = _motor.GetComponentInChildren<Animator>().gameObject.AddComponent<VideoCaptureLift>();
                _lift.Begin(_ball.transform, F("ballSize", 1f) * 0.5f);
            }

            return true;
        }

        private bool FoeDirection(out Vector3 toFoe)
        {
            toFoe = Vector3.zero;
            if (_foe == null || !_foe.IsAlive || !_clip.ContainsKey("fight"))
            {
                return false;
            }

            toFoe = Vector3.ProjectOnPlane(_foe.transform.position - _motor.transform.position, Vector3.up);
            return toFoe.sqrMagnitude > 0.01f;
        }

        // How to check a fight take: every swing that connected is in NavVideoRecorder.LogText.
        private void LogHit(MeleeHitInfo hit)
        {
            if (hit.IsCombatant)
            {
                string line = $"{(hit.Killed ? "killed" : "hit")} {hit.Collider.name} at {ClipSeconds:F2}s\n";
                EditorPrefs.SetString("navcap.log", EditorPrefs.GetString("navcap.log", string.Empty) + line);
            }
        }

        private float ClipSeconds => Mathf.Max(0, _recFrames) / RecordFps;

        private void AdvanceRecording()
        {
            if (EditorPrefs.GetInt("navcap.recording", 0) != 1)
            {
                return;
            }

            _recFrames++;
            if (ClipSeconds >= F("recLen", 8f))
            {
                _finished = true;
                EditorPrefs.SetInt("navcap.rec", 0);
                EditorPrefs.SetInt("navcap.done", 1);
            }
        }

        private void LateUpdate()
        {
            if (!_clipRunning)
            {
                return;
            }

            // An attack turns the body to the camera's direction, which on a staged camera can be
            // away from the fight. Before PlayerCombat's LateUpdate, so the sweep goes this way too.
            if (FoeDirection(out Vector3 toFoe))
            {
                _motor.transform.rotation = Quaternion.LookRotation(toFoe);
            }

            if (S("cam", "orbit") == "follow")
            {
                return;
            }

            // A camera move: yaw + orbit deg/s around the player's place, dist -> dist2 and
            // h -> h2 over the clip, eased. Covers orbits, push-ins and big pull-backs.
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(ClipSeconds / Mathf.Max(0.1f, F("recLen", 8f))));
            float distance = Mathf.Lerp(F("dist", 6f), F("dist2", F("dist", 6f)), t);
            float height = Mathf.Lerp(F("h", 2f), F("h2", F("h", 2f)), t);
            Vector3 centre = On("track", true) ? _motor.transform.position : _orbitCentre;
            Quaternion around = Quaternion.Euler(0f, F("yaw") + F("orbit") * ClipSeconds, 0f);
            Vector3 position = centre + Vector3.up * height - around * Vector3.forward * distance;

            float clearance = Mathf.Max(_world.GroundHeight(position.x, position.z), WorldGenerator.SeaLevel) + 0.4f;
            position.y = Mathf.Max(position.y, clearance);

            Transform view = _camera.transform;
            view.position = position;
            view.rotation = Quaternion.LookRotation(centre + Vector3.up * F("look", 1.2f) - position);
        }

        private static T LoadAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                Debug.LogError($"{nameof(VideoCapture)}: no {typeof(T).Name} at {path}.");
            }

            return asset;
        }

        private static void SetField(object target, string field, float value)
        {
            target.GetType().GetField(field, Any)?.SetValue(target, value);
        }
    }
}
#endif
