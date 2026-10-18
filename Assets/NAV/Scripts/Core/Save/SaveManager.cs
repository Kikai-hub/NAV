using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Building;
using NAV.Gameplay.Equipment;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;
using NAV.Gameplay.Player;
using NAV.Gameplay.Survival;
using NAV.World.Generation;
using NAV.World.TimeOfDay;
using NAV.World.Weather;

namespace NAV.Core.Save
{
    /// <summary>
    /// The gameplay scene's save/load bootstrapper. Lives under Core/Save but reaches into
    /// World (WorldGenerator) and Gameplay (player state, placed buildings) - ARCHITECTURE_v0.1.md
    /// lists "service/bootstrap" as a Core responsibility, and this is that integration point (see
    /// SceneLoader for the project's first, narrower example of the same idea), not scattered
    /// Core/Gameplay coupling elsewhere.
    ///
    /// On Start: if SaveSystem.PendingLoadSaveId was set before this scene loaded (by
    /// MainMenuUIController's Load list), loads that save and regenerates the world from its
    /// stored seed; otherwise starts a brand new game with a fresh random seed. Either way, this
    /// is the only thing that calls WorldGenerator.Generate() in the real play flow -
    /// WorldGenerator's own Generate-On-Start must be off (see UNITY_SETUP_NEXT_STEPS.md's Save
    /// step) so a second, wrongly-seeded generation doesn't immediately overwrite this one.
    ///
    /// Increment 1 scope (developer's explicit choice, see PROJECT_STATE.md): world seed + player
    /// (position/rotation/health/inventory) + placed buildings. NOT saved: which resource nodes
    /// were already gathered/depleted - regenerating from seed respawns every resource fresh on
    /// every load, a known, deliberate limitation.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        [SerializeField] private WorldGenerator _worldGenerator;
        [SerializeField] private PlayerMotor _playerMotor;
        [SerializeField] private PlayerHealth _playerHealth;
        [Tooltip("Optional - when assigned, respawning after death returns the player to the world's generated spawn point.")]
        [SerializeField] private PlayerDeath _playerDeath;
        [SerializeField] private PlayerInventory _playerInventory;
        [Tooltip("Optional - when assigned, active food and its remaining time are saved/restored.")]
        [SerializeField] private PlayerFood _playerFood;
        [Tooltip("Optional - when assigned, the held (main hand) item is saved/restored.")]
        [SerializeField] private PlayerEquipment _playerEquipment;
        [Tooltip("Optional - when assigned, the in-game day and time of day are saved/restored.")]
        [SerializeField] private WorldClock _worldClock;
        [Tooltip("Optional - when assigned, the current weather and its remaining time are saved/restored.")]
        [SerializeField] private WeatherSystem _weatherSystem;
        [SerializeField] private ItemDatabase _itemDatabase;
        [SerializeField] private BuildingPieceDatabase _buildingPieceDatabase;
        [Tooltip("How often this world autosaves into its own save slot (overwriting the previous autosave, never creating a second file).")]
        [SerializeField] private float _autosaveIntervalSeconds = 180f;

        private void Awake()
        {
            if (_worldGenerator == null || _playerMotor == null || _playerHealth == null
                || _playerInventory == null || _itemDatabase == null || _buildingPieceDatabase == null)
            {
                Debug.LogError($"{nameof(SaveManager)} on '{name}' is missing a required reference.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            string pendingId = SaveSystem.PendingLoadSaveId;
            SaveSystem.PendingLoadSaveId = null;

            if (!string.IsNullOrEmpty(pendingId))
            {
                LoadFrom(pendingId);
            }
            else
            {
                StartNewGame();
            }

            StartCoroutine(AutosaveLoop());
        }

        private void OnApplicationQuit()
        {
            Autosave();
        }

        /// <summary>Reads the name/seed MainMenuUIController's Create World screen set before
        /// this scene loaded. An empty name means that screen wasn't used (e.g. testing this
        /// scene directly without going through Main Menu) - falls back to a fully random seed
        /// and a default name, same safety net WorldGenerator's own Generate-On-Start provides
        /// for standalone testing.</summary>
        private void StartNewGame()
        {
            string name = SaveSystem.PendingNewGameName;
            SaveSystem.PendingNewGameName = null;

            int seed = string.IsNullOrEmpty(name) ? new System.Random().Next() : SaveSystem.PendingNewGameSeed;
            if (string.IsNullOrEmpty(name))
            {
                name = "New World";
            }

            _worldGenerator.Generate(seed);

            if (_worldGenerator.HasSpawnPoint)
            {
                _playerMotor.Teleport(_worldGenerator.SpawnPoint, _playerMotor.transform.rotation);
            }

            ApplyRespawnPoint();

            SaveSystem.CurrentSaveId = Guid.NewGuid().ToString();
            SaveSystem.CurrentSeed = seed;
            SaveSystem.CurrentWorldName = name;

            Debug.Log($"{nameof(SaveManager)}: started a new game '{name}' (seed {seed}, save id {SaveSystem.CurrentSaveId}).", this);
        }

        private void LoadFrom(string saveId)
        {
            SaveGameData data = SaveSystem.Load(saveId);
            if (data == null)
            {
                Debug.LogError($"{nameof(SaveManager)}: could not load save '{saveId}' - starting a new game instead.", this);
                StartNewGame();
                return;
            }

            _worldGenerator.Generate(data.WorldSeed, data.DepletedResourceIds != null ? new HashSet<int>(data.DepletedResourceIds) : null);
            ApplyRespawnPoint();

            if (_worldClock != null && data.Day > 0)
            {
                _worldClock.SetTime(data.Day, data.Hour);
            }

            if (_weatherSystem != null && !string.IsNullOrEmpty(data.WeatherId))
            {
                _weatherSystem.Restore(data.WeatherId, data.WeatherRemainingSeconds);
            }

            _playerMotor.Teleport(data.Player.Position, data.Player.Rotation);
            ApplyFoods(data.Player.ActiveFoods);
            _playerHealth.SetHealth(data.Player.CurrentHealth);
            ApplyInventory(data.Player.InventorySlots);
            if (_playerEquipment != null && !string.IsNullOrEmpty(data.Player.MainHandItemId))
            {
                _playerEquipment.Restore(_itemDatabase.Get(data.Player.MainHandItemId));
            }
            ApplyBuildings(data.Buildings);

            SaveSystem.CurrentSaveId = data.SaveId;
            SaveSystem.CurrentSeed = data.WorldSeed;
            SaveSystem.CurrentWorldName = data.WorldName;

            Debug.Log($"{nameof(SaveManager)}: loaded save '{data.WorldName}' (seed {data.WorldSeed}).", this);
        }

        private void ApplyInventory(List<InventorySlotSaveData> slots)
        {
            if (slots == null)
            {
                return;
            }

            foreach (InventorySlotSaveData slot in slots)
            {
                ItemDefinition definition = _itemDatabase.Get(slot.ItemId);
                if (definition != null)
                {
                    _playerInventory.Inventory.SetSlot(slot.SlotIndex, definition, slot.Quantity);
                }
            }
        }

        /// <summary>Respawn after death goes to the world's spawn point (deterministic per seed,
        /// so it's recomputed rather than saved) instead of the scene's start position.</summary>
        private void ApplyRespawnPoint()
        {
            if (_playerDeath != null && _worldGenerator.HasSpawnPoint)
            {
                _playerDeath.SetRespawnPoint(_worldGenerator.SpawnPoint, _playerMotor.transform.rotation);
            }
        }

        private void ApplyFoods(List<ActiveFoodSaveData> foods)
        {
            // Null for saves made before food existed - nothing to restore.
            if (_playerFood == null || foods == null)
            {
                return;
            }

            foreach (ActiveFoodSaveData food in foods)
            {
                _playerFood.RestoreFood(_itemDatabase.Get(food.ItemId), food.RemainingSeconds);
            }
        }

        private void ApplyBuildings(List<BuildingSaveData> buildings)
        {
            if (buildings == null)
            {
                return;
            }

            foreach (BuildingSaveData saved in buildings)
            {
                BuildingPieceDefinition definition = _buildingPieceDatabase.Get(saved.PieceId);
                if (definition == null || definition.Prefab == null)
                {
                    continue;
                }

                GameObject instance = Instantiate(definition.Prefab, saved.Position, saved.Rotation);
                BuildingPiece piece = instance.GetComponent<BuildingPiece>();
                if (piece != null)
                {
                    piece.Configure(definition);
                }
            }
        }

        private IEnumerator AutosaveLoop()
        {
            var wait = new WaitForSeconds(_autosaveIntervalSeconds);
            while (true)
            {
                yield return wait;
                Autosave();
            }
        }

        /// <summary>Gathers current player/building state and overwrites this session's own save
        /// file (SaveSystem.CurrentSaveId) - never a second file, per the developer's "the world
        /// autosaves into its own old save" requirement. Public so PauseUIController's "Leave to
        /// Main Menu" (and any future manual Save button) can trigger the same thing on demand.</summary>
        public void Autosave()
        {
            if (string.IsNullOrEmpty(SaveSystem.CurrentSaveId))
            {
                return;
            }

            var data = new SaveGameData
            {
                SaveId = SaveSystem.CurrentSaveId,
                WorldName = SaveSystem.CurrentWorldName,
                SavedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                WorldSeed = SaveSystem.CurrentSeed,
                Day = _worldClock != null ? _worldClock.Day : 0,
                Hour = _worldClock != null ? _worldClock.Hour : 0f,
                WeatherId = _weatherSystem != null && _weatherSystem.Current != null ? _weatherSystem.Current.Id : null,
                WeatherRemainingSeconds = _weatherSystem != null ? _weatherSystem.RemainingSeconds : 0f,
                Player = BuildPlayerData(),
                Buildings = BuildBuildingsData(),
                DepletedResourceIds = _worldGenerator.GetDepletedResourceIds()
            };

            SaveSystem.Save(data);
            Debug.Log($"{nameof(SaveManager)}: autosaved '{data.SaveId}'.", this);
        }

        private PlayerSaveData BuildPlayerData()
        {
            var player = new PlayerSaveData
            {
                Position = _playerMotor.transform.position,
                Rotation = _playerMotor.transform.rotation,
                CurrentHealth = _playerHealth.CurrentHealth
            };

            IReadOnlyList<ItemStack> slots = _playerInventory.Inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty)
                {
                    player.InventorySlots.Add(new InventorySlotSaveData
                    {
                        SlotIndex = i,
                        ItemId = slots[i].Definition.Id,
                        Quantity = slots[i].Quantity
                    });
                }
            }

            if (_playerEquipment != null && _playerEquipment.MainHand != null)
            {
                player.MainHandItemId = _playerEquipment.MainHand.Id;
            }

            if (_playerFood != null)
            {
                foreach (PlayerFood.ActiveFood food in _playerFood.ActiveFoods)
                {
                    player.ActiveFoods.Add(new ActiveFoodSaveData
                    {
                        ItemId = food.Item.Id,
                        RemainingSeconds = food.Remaining
                    });
                }
            }

            return player;
        }

        private List<BuildingSaveData> BuildBuildingsData()
        {
            var buildings = new List<BuildingSaveData>();
            foreach (BuildingPiece piece in BuildingPiece.AllPieces)
            {
                if (piece == null || piece.Definition == null)
                {
                    continue;
                }

                buildings.Add(new BuildingSaveData
                {
                    PieceId = piece.Definition.Id,
                    Position = piece.transform.position,
                    Rotation = piece.transform.rotation
                });
            }

            return buildings;
        }
    }
}
