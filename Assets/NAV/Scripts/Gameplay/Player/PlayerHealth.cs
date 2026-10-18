using System;
using UnityEngine;

namespace NAV.Gameplay.Player
{
    /// <summary>
    /// Holds current/max HP and exposes TakeDamage/Heal/Revive for Combat to call. Fires Died
    /// once (guarded) the instant CurrentHealth reaches 0; PlayerDeath is the thing that
    /// actually reacts to it (freezing control, showing the death screen). Max health and
    /// regeneration can be raised by food (SetBonusMaxHealth/SetBonusRegenPerSecond, driven by
    /// PlayerFood) - this class doesn't know food exists. Regeneration pauses for
    /// Stats.RegenDelayAfterDamage after every hit. Mirrors PlayerStamina's shape (stats SO + a Changed event for UI to observe, per
    /// ARCHITECTURE_v0.1.md's "UI observes gameplay state" rule).
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private PlayerHealthStats _stats;

        private float _bonusMaxHealth;
        private float _bonusRegenPerSecond;
        private float _regenDelayTimer;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => _stats.MaxHealth + _bonusMaxHealth;
        public bool IsAlive => CurrentHealth > 0f;

        public event Action Changed;
        public event Action Died;

        private void Awake()
        {
            if (_stats == null)
            {
                Debug.LogError($"{nameof(PlayerHealth)} on '{name}' is missing its Stats reference.", this);
                enabled = false;
                return;
            }

            CurrentHealth = _stats.MaxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || !IsAlive)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            _regenDelayTimer = _stats.RegenDelayAfterDamage;
            Changed?.Invoke();

            if (CurrentHealth <= 0f)
            {
                Died?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || !IsAlive)
            {
                return;
            }

            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            Changed?.Invoke();
        }

        private void Update()
        {
            float regen = _stats.BaseRegenPerSecond + _bonusRegenPerSecond;
            if (!IsAlive || regen <= 0f || CurrentHealth >= MaxHealth)
            {
                return;
            }

            if (_regenDelayTimer > 0f)
            {
                _regenDelayTimer -= Time.deltaTime;
                return;
            }

            Heal(regen * Time.deltaTime);
        }

        /// <summary>Extra max health on top of Stats.MaxHealth (food - see PlayerFood). When the
        /// bonus shrinks, current health is clamped down to the new max; when it grows, current
        /// health stays where it is and regenerates into the new headroom (Valheim behaviour).</summary>
        public void SetBonusMaxHealth(float bonus)
        {
            _bonusMaxHealth = Mathf.Max(0f, bonus);
            if (CurrentHealth > MaxHealth)
            {
                CurrentHealth = MaxHealth;
            }

            Changed?.Invoke();
        }

        /// <summary>Extra health regeneration per second on top of Stats.BaseRegenPerSecond
        /// (food - see PlayerFood).</summary>
        public void SetBonusRegenPerSecond(float bonus)
        {
            _bonusRegenPerSecond = Mathf.Max(0f, bonus);
        }

        /// <summary>Resets health to full and clears the dead state - called by PlayerDeath on
        /// respawn. Deliberately unconditional (no IsAlive guard, unlike Heal/TakeDamage) since
        /// this is exactly how a dead character is meant to become alive again.</summary>
        public void Revive()
        {
            CurrentHealth = MaxHealth;
            Changed?.Invoke();
        }

        /// <summary>Directly sets current health to an arbitrary saved value (clamped to
        /// 0..MaxHealth) - used by SaveManager on load. Deliberately bypasses TakeDamage/Heal's
        /// IsAlive guard and Died-event firing, same "force state directly" precedent as
        /// Revive().</summary>
        public void SetHealth(float value)
        {
            CurrentHealth = Mathf.Clamp(value, 0f, MaxHealth);
            Changed?.Invoke();
        }
    }
}
