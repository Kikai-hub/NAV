using System;
using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;
using NAV.Gameplay.Player;

namespace NAV.Gameplay.Survival
{
    /// <summary>
    /// Valheim-style food (GDD 16): the player can have up to MaxActiveFoods different foods
    /// active at once; each one adds its FoodDefinition bonuses (max health, max stamina,
    /// health regen) for its duration, then expires. No hunger meter and no starvation damage -
    /// not eating just means base stats. The same food can't be stacked: it can only be eaten
    /// again once its remaining time drops to RefreshThreshold of its duration, which restarts
    /// its timer (eat-before-it-runs-out, like Valheim). All active food is lost on death.
    ///
    /// Owns the timers and pushes the summed bonuses into PlayerHealth/PlayerStamina through
    /// their SetBonus* methods - those classes never learn food exists. UI observes Changed and
    /// ActiveFoods (ARCHITECTURE_v0.1.md's "UI observes gameplay state" rule); eating is
    /// triggered by InventoryUIController (right-click a slot) calling TryEatFromSlot.
    /// </summary>
    public class PlayerFood : MonoBehaviour
    {
        /// <summary>One currently active food. Remaining is in seconds.</summary>
        public class ActiveFood
        {
            public ItemDefinition Item { get; }
            public FoodDefinition Food => Item.Food;
            public float Remaining { get; internal set; }
            public float RemainingFraction => Food.DurationSeconds > 0f ? Mathf.Clamp01(Remaining / Food.DurationSeconds) : 0f;

            internal ActiveFood(ItemDefinition item, float remaining)
            {
                Item = item;
                Remaining = remaining;
            }
        }

        [SerializeField] private PlayerInventory _playerInventory;
        [SerializeField] private PlayerHealth _health;
        [SerializeField] private PlayerStamina _stamina;
        [SerializeField, Min(1)] private int _maxActiveFoods = 3;

        [Tooltip("An already-active food can be eaten again once its remaining time is at or below this fraction of its duration (restarts its timer).")]
        [SerializeField, Range(0f, 1f)] private float _refreshThreshold = 0.25f;

        private readonly List<ActiveFood> _activeFoods = new();

        public IReadOnlyList<ActiveFood> ActiveFoods => _activeFoods;
        public int MaxActiveFoods => _maxActiveFoods;

        /// <summary>Fires when a food is eaten, refreshed or expires (not every frame - timers
        /// tick silently; UI polls Remaining for countdowns).</summary>
        public event Action Changed;

        private void Awake()
        {
            if (_playerInventory == null || _health == null || _stamina == null)
            {
                Debug.LogError($"{nameof(PlayerFood)} on '{name}' is missing a required reference (PlayerInventory/Health/Stamina).", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.Died -= HandleDied;
            }
        }

        private void Update()
        {
            bool expired = false;
            for (int i = _activeFoods.Count - 1; i >= 0; i--)
            {
                _activeFoods[i].Remaining -= Time.deltaTime;
                if (_activeFoods[i].Remaining <= 0f)
                {
                    _activeFoods.RemoveAt(i);
                    expired = true;
                }
            }

            if (expired)
            {
                ApplyBonuses();
            }
        }

        /// <summary>Why a given item can't be eaten right now, or null if it can.</summary>
        public string GetCannotEatReason(ItemDefinition item)
        {
            if (item == null || !item.IsEdible)
            {
                return "not edible";
            }

            ActiveFood existing = Find(item);
            if (existing != null)
            {
                return existing.RemainingFraction > _refreshThreshold ? "already eaten" : null;
            }

            return _activeFoods.Count >= _maxActiveFoods ? "too full" : null;
        }

        public bool CanEat(ItemDefinition item) => GetCannotEatReason(item) == null;

        /// <summary>Eats one item from the given player inventory slot, if that item is edible
        /// and there's room for it. Returns true if something was eaten.</summary>
        public bool TryEatFromSlot(int slotIndex)
        {
            if (!enabled || !_health.IsAlive)
            {
                return false;
            }

            IReadOnlyList<ItemStack> slots = _playerInventory.Inventory.Slots;
            if (slotIndex < 0 || slotIndex >= slots.Count || slots[slotIndex].IsEmpty)
            {
                return false;
            }

            ItemDefinition item = slots[slotIndex].Definition;
            string reason = GetCannotEatReason(item);
            if (reason != null)
            {
                Debug.Log($"{name} can't eat {item.DisplayName}: {reason}.", this);
                return false;
            }

            _playerInventory.Inventory.RemoveFromSlot(slotIndex, 1, out int removed);
            if (removed <= 0)
            {
                return false;
            }

            AddOrRefresh(item, item.Food.DurationSeconds);
            Debug.Log($"{name} ate {item.DisplayName}.", this);
            return true;
        }

        /// <summary>Restores one active food with a specific remaining time, bypassing the
        /// inventory and the refresh rule - used by SaveManager on load. Ignores entries beyond
        /// MaxActiveFoods or for non-edible items.</summary>
        public void RestoreFood(ItemDefinition item, float remainingSeconds)
        {
            if (item == null || !item.IsEdible || remainingSeconds <= 0f)
            {
                return;
            }

            if (Find(item) == null && _activeFoods.Count >= _maxActiveFoods)
            {
                return;
            }

            AddOrRefresh(item, Mathf.Min(remainingSeconds, item.Food.DurationSeconds));
        }

        public void ClearAll()
        {
            if (_activeFoods.Count == 0)
            {
                return;
            }

            _activeFoods.Clear();
            ApplyBonuses();
        }

        private void AddOrRefresh(ItemDefinition item, float remaining)
        {
            ActiveFood existing = Find(item);
            if (existing != null)
            {
                existing.Remaining = remaining;
            }
            else
            {
                _activeFoods.Add(new ActiveFood(item, remaining));
            }

            ApplyBonuses();
        }

        private ActiveFood Find(ItemDefinition item)
        {
            foreach (ActiveFood food in _activeFoods)
            {
                if (food.Item == item)
                {
                    return food;
                }
            }

            return null;
        }

        private void ApplyBonuses()
        {
            float health = 0f, stamina = 0f, regen = 0f;
            foreach (ActiveFood food in _activeFoods)
            {
                health += food.Food.HealthBonus;
                stamina += food.Food.StaminaBonus;
                regen += food.Food.HealthRegenPerSecond;
            }

            _health.SetBonusMaxHealth(health);
            _health.SetBonusRegenPerSecond(regen);
            _stamina.SetBonusMaxStamina(stamina);
            Changed?.Invoke();
        }

        private void HandleDied()
        {
            ClearAll();
        }
    }
}
