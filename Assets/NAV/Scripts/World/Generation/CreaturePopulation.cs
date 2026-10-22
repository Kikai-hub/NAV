using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.AI;
using NAV.Gameplay.Player;
using NAV.World.Biomes;
using NAV.World.TimeOfDay;

namespace NAV.World.Generation
{
    /// <summary>
    /// Keeps each biome's creature numbers alive after the world is generated (GDD Stage B:
    /// "spawning by biome and time of day, respawn"). WorldGenerator places the starting animals;
    /// this tops each BiomeCreatureSpawn entry back up to its target - Count by day, NightCount
    /// at night (more wolves after dark) - one group per entry every RespawnSeconds, on the
    /// NavMesh in the right biome and out of the player's way (between Min/Max Spawn Distance).
    /// When an entry has more than its target (night ended), the extras that are far from the
    /// player and not fighting are quietly removed.
    ///
    /// Not saved: creatures re-spawn from the seed on every load, same as before.
    /// </summary>
    public class CreaturePopulation : MonoBehaviour
    {
        [SerializeField] private WorldGenerator _worldGenerator;
        [Tooltip("Optional. Without it the day count is used all the time.")]
        [SerializeField] private WorldClock _clock;
        [Tooltip("Who new animals spawn away from. Found by its PlayerMotor when empty.")]
        [SerializeField] private Transform _player;

        [Header("Placement")]
        [SerializeField, Min(0f)] private float _minSpawnDistance = 50f;
        [SerializeField, Min(1f)] private float _maxSpawnDistance = 120f;
        [Tooltip("Extra animals (above the target) are removed only when at least this far from the player.")]
        [SerializeField, Min(0f)] private float _despawnDistance = 80f;
        [SerializeField, Min(0.5f)] private float _checkInterval = 5f;
        [SerializeField, Min(1)] private int _placementAttempts = 20;

        private readonly Dictionary<BiomeCreatureSpawn, float> _nextRespawn = new();
        private float _nextCheck;

        private void OnEnable()
        {
            if (_worldGenerator == null)
            {
                Debug.LogError($"{nameof(CreaturePopulation)} on '{name}' has no World Generator assigned.", this);
                enabled = false;
                return;
            }

            _worldGenerator.CreaturesSpawned += HandleCreaturesSpawned;
        }

        private void OnDisable()
        {
            if (_worldGenerator != null)
            {
                _worldGenerator.CreaturesSpawned -= HandleCreaturesSpawned;
            }
        }

        private void HandleCreaturesSpawned()
        {
            // A fresh world starts full - nobody respawns until an entry's interval has passed.
            _nextRespawn.Clear();
            foreach (BiomeDefinition biome in _worldGenerator.Biomes)
            {
                if (biome == null)
                {
                    continue;
                }

                foreach (BiomeCreatureSpawn spawn in biome.CreatureSpawns)
                {
                    _nextRespawn[spawn] = UnityEngine.Time.time + spawn.RespawnSeconds;
                }
            }
        }

        private void Update()
        {
            if (UnityEngine.Time.time < _nextCheck || !_worldGenerator.HasCreatureWorld)
            {
                return;
            }

            _nextCheck = UnityEngine.Time.time + _checkInterval;

            if (_player == null)
            {
                PlayerMotor motor = FindAnyObjectByType<PlayerMotor>();
                if (motor == null)
                {
                    return;
                }

                _player = motor.transform;
            }

            bool night = _clock != null && _clock.IsNight;

            foreach (BiomeDefinition biome in _worldGenerator.Biomes)
            {
                if (biome == null)
                {
                    continue;
                }

                foreach (BiomeCreatureSpawn spawn in biome.CreatureSpawns)
                {
                    if (spawn.Prefab != null)
                    {
                        Balance(biome, spawn, spawn.GetCount(night));
                    }
                }
            }
        }

        private void Balance(BiomeDefinition biome, BiomeCreatureSpawn spawn, int target)
        {
            int alive = CountAlive(spawn);

            if (alive > target)
            {
                RemoveExtras(spawn, alive - target);
                return;
            }

            if (alive >= target)
            {
                return;
            }

            float now = UnityEngine.Time.time;
            if (_nextRespawn.TryGetValue(spawn, out float due) && now < due)
            {
                return;
            }

            _nextRespawn[spawn] = now + spawn.RespawnSeconds;

            if (!_worldGenerator.TryFindCreaturePointNear(biome, _player.position, _minSpawnDistance, _maxSpawnDistance, _placementAttempts, out Vector3 point))
            {
                return;
            }

            int size = Random.Range(spawn.MinGroupSize, spawn.MaxGroupSize + 1);
            _worldGenerator.SpawnCreatureGroup(biome, spawn, point, Mathf.Min(size, target - alive));
        }

        private static int CountAlive(BiomeCreatureSpawn spawn)
        {
            int alive = 0;
            foreach (Creature creature in Creature.All)
            {
                if (ReferenceEquals(creature.SpawnSource, spawn) && creature.IsAlive)
                {
                    alive++;
                }
            }

            return alive;
        }

        private void RemoveExtras(BiomeCreatureSpawn spawn, int extra)
        {
            float minSqr = _despawnDistance * _despawnDistance;

            // Backwards: Destroy disables at end of frame, but stay safe if that ever changes.
            for (int i = Creature.All.Count - 1; i >= 0 && extra > 0; i--)
            {
                Creature creature = Creature.All[i];
                if (!ReferenceEquals(creature.SpawnSource, spawn) || !creature.IsAlive || creature.IsEngaged)
                {
                    continue;
                }

                if ((creature.transform.position - _player.position).sqrMagnitude < minSqr)
                {
                    continue;
                }

                // Counted as gone right away, so the next check doesn't remove another.
                creature.SpawnSource = null;
                Destroy(creature.gameObject);
                extra--;
            }
        }
    }
}
