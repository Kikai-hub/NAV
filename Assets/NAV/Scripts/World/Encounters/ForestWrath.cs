using System;
using UnityEngine;
using NAV.Gameplay.AI;
using NAV.Gameplay.Bosses;
using NAV.Gameplay.Combat;
using NAV.Gameplay.Items;
using NAV.Gameplay.Player;
using NAV.World.Biomes;
using NAV.World.Generation;
using NAV.World.TimeOfDay;

namespace NAV.World.Encounters
{
    /// <summary>
    /// The wrath of the forest and the Leshy's coming (boss 1 has no altar - the developer's
    /// design 2026-11-06: met by chance in the forest, killing him is mandatory, his heartwood
    /// makes the first pickaxe; the encounter is "wrath + chance"):
    ///  - every tree felled in a wrath biome (the world's own trees, not stumps or logs) adds
    ///    wrath; it fades slowly while the player leaves the forest alone;
    ///  - every CheckInterval, in the deep forest (chance biomes), he may come: a small base
    ///    chance that grows with the wrath and doubles at night;
    ///  - at GuaranteedWrath he comes for sure, anywhere in a wrath biome - whoever builds from
    ///    the forest meets its master sooner or later;
    ///  - while the forest isn't angry (wrath below AngryWrathFraction) the Leshy met by chance
    ///    is PEACEFUL (developer, 2026-11-06): he walks his forest and leaves the player alone,
    ///    unless provoked - hit, or a tree felled within ProvokeTreeDistance of him - then he
    ///    roars and fights; left alone he goes back into the thicket after PeacefulStaySeconds;
    ///  - the wrath rising past its omen shares, and his coming, are announced (Message) - the
    ///    forest falls silent for OmenSeconds, then he steps out of the trees SpawnDistance away
    ///    and roars;
    ///  - killed: the wrath is spent, he can't come again for a while (and his drop is his
    ///    Creature's loot); the player got away: he goes back into the thicket, most of the
    ///    wrath stays.
    /// Saved by SaveManager (wrath, kills, cooldown); a Leshy on the way or in a fight is not.
    /// </summary>
    public class ForestWrath : MonoBehaviour
    {
        private enum Phase { Calm, Omen, Active }

        [SerializeField] private ForestWrathSettings _settings;
        [SerializeField] private WorldGenerator _worldGenerator;
        [Tooltip("Optional - more chance at night.")]
        [SerializeField] private WorldClock _clock;
        [Tooltip("Found by its PlayerMotor when empty.")]
        [SerializeField] private Transform _player;

        private const float SpawnRetrySeconds = 1f;
        private const float SpawnGiveUpSeconds = 12f;

        private Phase _phase;
        private float _phaseTimer;
        private float _nextCheck;
        private float _nextSpawnTry;
        private int _omensShown;
        private Creature _boss;
        private bool _angry;
        private bool _cupFull;
        private float _spawnTime;

        /// <summary>Multiplayer: a guest's machine leaves the forest's master to the host.</summary>
        public static bool EncountersSuppressed { get; set; }

        /// <summary>Wrath now (0..MaxWrath).</summary>
        public float Wrath { get; private set; }

        /// <summary>How many times the Leshy was killed in this world.</summary>
        public int Defeats { get; private set; }

        /// <summary>Seconds before he can come again.</summary>
        public float CooldownSeconds { get; private set; }

        /// <summary>A line for the player (an omen, his arrival, his death) - the HUD shows it.</summary>
        public event Action<string> Message;

        /// <summary>He is coming (true = the angry forest sends him) - for the omen sounds.</summary>
        public event Action<bool> OmenStarted;

        /// <summary>How hushed the forest's life is (0 = birds as usual, 1 = dead silence): the
        /// forest falls silent while an angry Leshy comes and fights, a little while a peaceful
        /// one walks by; the birds come back when it's over. AmbienceController listens.</summary>
        public float Silence
        {
            get
            {
                // Online: the forest round another player's Leshy is silent here too.
                float remote = Time.time < _remoteSilenceUntil ? _remoteSilence : 0f;
                return Mathf.Max(remote, LocalSilence);
            }
        }

        private float _remoteSilence;
        private float _remoteSilenceUntil;

        /// <summary>The silence as this machine's own encounter makes it (what is sent to others).</summary>
        public float LocalSilence
        {
            get
            {
                bool peaceful = _boss != null ? _boss.IsPeaceful : !_angry;
                switch (_phase)
                {
                    case Phase.Omen: return _angry ? 1f : 0.4f;
                    case Phase.Active: return _boss != null && _boss.IsAlive ? (peaceful ? 0.3f : 1f) : 0f;
                    default: return 0f;
                }
            }
        }

        private void OnEnable()
        {
            if (_settings == null || _worldGenerator == null || _settings.BossPrefab == null)
            {
                Debug.LogError($"{nameof(ForestWrath)} on '{name}' is missing its Settings (with a Boss Prefab) or World Generator.", this);
                enabled = false;
                return;
            }

            ResourceNode.Depleted += HandleNodeDepleted;
            ResourceNode.DepletedRemotely += HandleRemoteNodeDepleted; // felled by another player, online
        }

        private void OnDisable()
        {
            ResourceNode.Depleted -= HandleNodeDepleted;
            ResourceNode.DepletedRemotely -= HandleRemoteNodeDepleted;
        }

        /// <summary>Online: a line from the encounter another player's machine runs nearby.</summary>
        public void ShowRemoteMessage(string text)
        {
            Message?.Invoke(text);
        }

        /// <summary>Online: the omen of a Leshy coming on another player's machine nearby.</summary>
        public void PlayRemoteOmen(bool angry)
        {
            OmenStarted?.Invoke(angry);
        }

        /// <summary>Online: the silence round another player's Leshy, valid for a few seconds
        /// (renewed while it lasts).</summary>
        public void SetRemoteSilence(float silence)
        {
            _remoteSilence = Mathf.Clamp01(silence);
            _remoteSilenceUntil = Time.time + 3f;
        }

        /// <summary>Restores the saved state (SaveManager), before any tree is felled.</summary>
        public void Restore(float wrath, int defeats, float cooldownSeconds)
        {
            Wrath = Mathf.Clamp(wrath, 0f, _settings != null ? _settings.MaxWrath : wrath);
            Defeats = Mathf.Max(0, defeats);
            CooldownSeconds = Mathf.Max(0f, cooldownSeconds);
            _omensShown = CountOmensBelow(Wrath);
        }

        /// <summary>Adds wrath as if trees were felled - for tests (and future rituals).</summary>
        public void AddWrath(float amount)
        {
            Wrath = Mathf.Clamp(Wrath + amount, 0f, _settings.MaxWrath);
            AnnounceOmens();
            if (Wrath >= _settings.GuaranteedWrath)
            {
                // The tree that filled the cup: the forest answers at the next frame - and the
                // cup stays full even if the wrath fades a hair below the mark before then (or
                // while the player is out of the forest), until he has come.
                _cupFull = true;
                _nextCheck = 0f;
            }
        }

        /// <summary>Starts his coming now (omens first) - for tests.</summary>
        [ContextMenu("Summon the Leshy now")]
        public void SummonNow()
        {
            if (_phase == Phase.Calm && FindPlayer())
            {
                StartOmen();
            }
        }

        // Another player's felling angers the forest just the same (but provokes nobody here -
        // the provoked Leshy, if any, is on the machine that runs him).
        private void HandleRemoteNodeDepleted(ResourceNode node)
        {
            if (IsWorldTreeInWrathBiome(node))
            {
                AddWrath(_settings.WrathPerTree);
            }
        }

        private bool IsWorldTreeInWrathBiome(ResourceNode node)
        {
            return node != null && node.Definition != null && node.Definition.RequiredTool == ToolType.Axe
                && _worldGenerator.TryGetNodeId(node.gameObject, out _)
                && Contains(_settings.WrathBiomes, _worldGenerator.GetBiomeAt(node.transform.position));
        }

        private void HandleNodeDepleted(ResourceNode node)
        {
            if (node == null || node.Definition == null || node.Definition.RequiredTool != ToolType.Axe)
            {
                return;
            }

            // Only the world's own trees - a felled trunk splitting into logs is the same tree.
            if (!_worldGenerator.TryGetNodeId(node.gameObject, out _))
            {
                return;
            }

            if (!Contains(_settings.WrathBiomes, _worldGenerator.GetBiomeAt(node.transform.position)))
            {
                return;
            }

            AddWrath(_settings.WrathPerTree);

            // Felling trees before the eyes of a peaceful Leshy is a provocation.
            if (_boss != null && _boss.IsPeaceful && FindPlayer()
                && Vector3.Distance(node.transform.position, _boss.transform.position) <= _settings.ProvokeTreeDistance)
            {
                _boss.Provoke(_player.GetComponentInChildren<IDamageable>(), _player);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            CooldownSeconds = Mathf.Max(0f, CooldownSeconds - dt);
            if (_phase != Phase.Active)
            {
                Wrath = Mathf.Max(0f, Wrath - _settings.DecayPerMinute / 60f * dt);
            }

            if (EncountersSuppressed || !_worldGenerator.HasCreatureWorld || !FindPlayer())
            {
                return;
            }

            switch (_phase)
            {
                case Phase.Calm: TickCalm(); break;
                case Phase.Omen: TickOmen(dt); break;
                case Phase.Active: TickActive(); break;
            }
        }

        private void TickCalm()
        {
            if (Time.time < _nextCheck || CooldownSeconds > 0f)
            {
                return;
            }

            _nextCheck = Time.time + _settings.CheckIntervalSeconds;
            BiomeDefinition biome = _worldGenerator.GetBiomeAt(_player.position);
            // Faded well below the mark since (the player stayed away long enough): not full any more.
            if (_cupFull && Wrath < _settings.GuaranteedWrath * 0.8f)
            {
                _cupFull = false;
            }

            if ((_cupFull || Wrath >= _settings.GuaranteedWrath) && Contains(_settings.WrathBiomes, biome))
            {
                _cupFull = false;
                StartOmen();
                return;
            }

            if (!Contains(_settings.ChanceBiomes, biome))
            {
                return;
            }

            float chance = _settings.BaseChance + _settings.WrathChance * Mathf.Clamp01(Wrath / _settings.GuaranteedWrath);
            if (_clock != null && _clock.IsNight)
            {
                chance *= _settings.NightChanceMultiplier;
            }

            if (UnityEngine.Random.value < chance)
            {
                StartOmen();
            }
        }

        private void StartOmen()
        {
            _phase = Phase.Omen;
            _phaseTimer = 0f;
            // An angry forest sends him for the player; otherwise he only walks his forest.
            _angry = _cupFull || Wrath >= _settings.GuaranteedWrath * _settings.AngryWrathFraction;
            Message?.Invoke(_angry ? _settings.ArrivalOmen : _settings.PeacefulOmen);
            OmenStarted?.Invoke(_angry);
            Debug.Log($"{nameof(ForestWrath)}: the Leshy is coming, {(_angry ? "angry" : "peaceful")} (wrath {Wrath:F1}).", this);
        }

        private void TickOmen(float dt)
        {
            _phaseTimer += dt;
            if (_phaseTimer < _settings.OmenSeconds)
            {
                return;
            }

            // A try a second until a point on the NavMesh is found (water, cliffs) - then give up.
            if (_phaseTimer - _settings.OmenSeconds > SpawnGiveUpSeconds)
            {
                _phase = Phase.Calm;
                CooldownSeconds = _settings.CheckIntervalSeconds;
                return;
            }

            if (Time.time < _nextSpawnTry)
            {
                return;
            }

            _nextSpawnTry = Time.time + SpawnRetrySeconds;
            BiomeDefinition biome = _worldGenerator.GetBiomeAt(_player.position);
            Vector2 distance = _settings.SpawnDistance;
            if (biome == null || !_worldGenerator.TryFindCreaturePointNear(biome, _player.position, distance.x, distance.y, 30, out Vector3 point))
            {
                return;
            }

            Spawn(point);
        }

        private void Spawn(Vector3 point)
        {
            Vector3 toPlayer = _player.position - point;
            toPlayer.y = 0f;
            Quaternion facing = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;
            GameObject instance = Instantiate(_settings.BossPrefab, point, facing);
            _boss = instance.GetComponent<Creature>();
            if (_boss == null)
            {
                Debug.LogError($"{nameof(ForestWrath)}: the boss prefab '{_settings.BossPrefab.name}' has no Creature.", this);
                Destroy(instance);
                _phase = Phase.Calm;
                return;
            }

            _boss.Died += HandleBossDied;
            _boss.Provoked += HandleBossProvoked;
            _phase = Phase.Active;
            _spawnTime = Time.time;

            if (!_angry)
            {
                // The forest isn't angry: he wanders his forest and leaves the player alone
                // unless provoked.
                _boss.MakePeaceful();
                Message?.Invoke(_settings.PeacefulArrivalText);
                Debug.Log($"{nameof(ForestWrath)}: a peaceful Leshy stepped out at {point}.", this);
                return;
            }

            Message?.Invoke(_settings.ArrivalText);

            if (instance.TryGetComponent(out LeshyBoss leshy))
            {
                leshy.PlayEntrance();
            }

            if (instance.TryGetComponent(out BossActor actor))
            {
                actor.ShowBarFor(6f);
            }

            IDamageable target = _player.GetComponentInChildren<IDamageable>();
            if (target != null)
            {
                _boss.Alert(target, _player);
            }

            Debug.Log($"{nameof(ForestWrath)}: the Leshy stepped out at {point} ({Vector3.Distance(point, _player.position):F0} m from the player).", this);
        }

        private void TickActive()
        {
            if (_boss == null)
            {
                // Removed by something else - count it as gone back into the forest.
                EndEncounter(false);
                return;
            }

            if (!_boss.IsAlive || _boss.IsEngaged)
            {
                return;
            }

            Vector3 offset = _boss.transform.position - _player.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance > _settings.LeaveDistance)
            {
                bool peaceful = _boss.IsPeaceful;
                Destroy(_boss.gameObject);
                EndEncounter(false, peaceful);
                return;
            }

            // A peaceful one walks back into the thicket after a while - never before the eyes.
            if (_boss.IsPeaceful && Time.time - _spawnTime > _settings.PeacefulStaySeconds && distance > _settings.PeacefulLeaveDistance)
            {
                Destroy(_boss.gameObject);
                EndEncounter(false, true);
            }
        }

        private void HandleBossProvoked()
        {
            _angry = true;
            Message?.Invoke(_settings.ProvokedText);
            if (_boss != null && _boss.TryGetComponent(out BossActor actor))
            {
                actor.ShowBarFor(6f);
            }

            Debug.Log($"{nameof(ForestWrath)}: the Leshy was provoked.", this);
        }

        private void HandleBossDied()
        {
            Defeats++;
            EndEncounter(true);
        }

        private void EndEncounter(bool killed, bool quietly = false)
        {
            if (_boss != null)
            {
                _boss.Died -= HandleBossDied;
                _boss.Provoked -= HandleBossProvoked;
            }

            _boss = null;
            _phase = Phase.Calm;
            if (killed)
            {
                Wrath = 0f;
                CooldownSeconds = _settings.CooldownAfterDefeatSeconds;
                Message?.Invoke(_settings.DefeatText);
            }
            else if (quietly)
            {
                // A peaceful visit that ended in peace: the wrath is untouched, no message.
                CooldownSeconds = _settings.CooldownAfterEscapeSeconds;
            }
            else
            {
                Wrath *= _settings.WrathKeptAfterEscape;
                CooldownSeconds = _settings.CooldownAfterEscapeSeconds;
                Message?.Invoke(_settings.EscapeText);
            }

            _omensShown = CountOmensBelow(Wrath);
            Debug.Log($"{nameof(ForestWrath)}: encounter over ({(killed ? "killed" : "got away")}), wrath {Wrath:F1}, defeats {Defeats}.", this);
        }

        private void AnnounceOmens()
        {
            var omens = _settings.WrathOmens;
            while (_omensShown < omens.Count && Wrath >= omens[_omensShown].Fraction * _settings.GuaranteedWrath)
            {
                if (_phase == Phase.Calm && !string.IsNullOrEmpty(omens[_omensShown].Text))
                {
                    Message?.Invoke(omens[_omensShown].Text);
                }

                _omensShown++;
            }
        }

        private int CountOmensBelow(float wrath)
        {
            int count = 0;
            if (_settings == null)
            {
                return 0;
            }

            foreach (WrathOmen omen in _settings.WrathOmens)
            {
                if (wrath >= omen.Fraction * _settings.GuaranteedWrath)
                {
                    count++;
                }
            }

            return count;
        }

        private bool FindPlayer()
        {
            if (_player != null)
            {
                return true;
            }

            PlayerMotor motor = FindAnyObjectByType<PlayerMotor>();
            _player = motor != null ? motor.transform : null;
            return _player != null;
        }

        private static bool Contains(System.Collections.Generic.IReadOnlyList<BiomeDefinition> list, BiomeDefinition biome)
        {
            if (biome == null)
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], biome))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
