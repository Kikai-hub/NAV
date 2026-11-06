using System;
using NAV.Gameplay.Combat;
using UnityEngine;

namespace NAV.Gameplay.Player
{
    /// <summary>
    /// Poison on the player (tier 4, the swamp): damage over time through PlayerHealth, once per
    /// Tick Interval, so health regeneration stays paused while it lasts. Applied by poisonous
    /// creatures (CreatureDefinition Poison Damage) - IPoisonable. Gone on death; not saved (it
    /// lasts seconds). The HUD tints the health bar while it runs (Changed).
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerPoison : MonoBehaviour, IPoisonable
    {
        [Tooltip("Seconds between poison damage ticks.")]
        [SerializeField, Min(0.1f)] private float _tickInterval = 1f;

        private PlayerHealth _health;
        private float _damagePerSecond;
        private float _remaining;
        private float _tickTimer;

        // Time.time until which no poison takes hold (an antidote's protection).
        private float _immuneUntil;

        public bool IsPoisoned => _remaining > 0f;
        public float RemainingSeconds => _remaining;
        public bool IsImmune => Time.time < _immuneUntil;

        /// <summary>Raised when poisoning starts or ends.</summary>
        public event Action Changed;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
        }

        public void ApplyPoison(float totalDamage, float seconds)
        {
            if (totalDamage <= 0f || seconds <= 0f || !_health.IsAlive || IsImmune)
            {
                return;
            }

            bool wasPoisoned = IsPoisoned;
            _damagePerSecond = Mathf.Max(wasPoisoned ? _damagePerSecond : 0f, totalDamage / seconds);
            _remaining = Mathf.Max(_remaining, seconds);
            if (!wasPoisoned)
            {
                _tickTimer = _tickInterval;
                Changed?.Invoke();
            }
        }

        /// <summary>Drinks one antidote (ItemDefinition.IsAntidote) from an inventory slot: the
        /// poison ends and none takes hold for its Poison Immunity Seconds. False if the slot
        /// holds no antidote.</summary>
        public bool TryDrinkAntidote(NAV.Gameplay.Inventory.Inventory inventory, int slotIndex)
        {
            if (inventory == null || !_health.IsAlive || slotIndex < 0 || slotIndex >= inventory.Slots.Count)
            {
                return false;
            }

            NAV.Gameplay.Items.ItemStack stack = inventory.Slots[slotIndex];
            if (stack == null || stack.IsEmpty || !stack.Definition.IsAntidote)
            {
                return false;
            }

            NAV.Gameplay.Items.ItemDefinition antidote = stack.Definition;
            inventory.RemoveFromSlot(slotIndex, 1, out int removed);
            if (removed <= 0)
            {
                return false;
            }

            _immuneUntil = Mathf.Max(_immuneUntil, Time.time + antidote.PoisonImmunitySeconds);
            Cure();
            Debug.Log($"{name} drank {antidote.DisplayName}.", this);
            return true;
        }

        /// <summary>Ends the poison at once (an antidote, death).</summary>
        public void Cure()
        {
            if (IsPoisoned)
            {
                End();
            }
        }

        private void End()
        {
            _remaining = 0f;
            _damagePerSecond = 0f;
            Changed?.Invoke();
        }

        private void Update()
        {
            if (!IsPoisoned)
            {
                return;
            }

            if (!_health.IsAlive)
            {
                Cure();
                return;
            }

            float step = Mathf.Min(Time.deltaTime, _remaining);
            _remaining -= step;
            _tickTimer -= step;
            if (_tickTimer <= 0f || _remaining <= 0f)
            {
                // The time since the last tick, so the total comes out right even at the end.
                float elapsed = _tickInterval - _tickTimer;
                _health.TakeDamage(_damagePerSecond * elapsed);
                _tickTimer += _tickInterval;
            }

            if (_remaining <= 0f)
            {
                End();
            }
        }
    }
}
