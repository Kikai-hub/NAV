using System;
using UnityEngine;

namespace NAV.Gameplay.Player
{
    /// <summary>
    /// Holds current/max HP and exposes TakeDamage/Heal/Revive for Combat to call. Fires Died
    /// once (guarded) the instant CurrentHealth reaches 0; PlayerDeath is the thing that
    /// actually reacts to it (freezing control, showing the death screen). This is still
    /// deliberately not the full Roadmap Phase 6 "Health" system - no regen/food interaction,
    /// no gravestone, no loot-recovery loop; see PROJECT_STATE.md's existing Technical Debt
    /// note. Mirrors PlayerStamina's shape (stats SO + a Changed event for UI to observe, per
    /// ARCHITECTURE_v0.1.md's "UI observes gameplay state" rule).
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private PlayerHealthStats _stats;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => _stats.MaxHealth;
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

            CurrentHealth = Mathf.Min(_stats.MaxHealth, CurrentHealth + amount);
            Changed?.Invoke();
        }

        /// <summary>Resets health to full and clears the dead state - called by PlayerDeath on
        /// respawn. Deliberately unconditional (no IsAlive guard, unlike Heal/TakeDamage) since
        /// this is exactly how a dead character is meant to become alive again.</summary>
        public void Revive()
        {
            CurrentHealth = _stats.MaxHealth;
            Changed?.Invoke();
        }

        /// <summary>Directly sets current health to an arbitrary saved value (clamped to
        /// 0..MaxHealth) - used by SaveManager on load. Deliberately bypasses TakeDamage/Heal's
        /// IsAlive guard and Died-event firing, same "force state directly" precedent as
        /// Revive().</summary>
        public void SetHealth(float value)
        {
            CurrentHealth = Mathf.Clamp(value, 0f, _stats.MaxHealth);
            Changed?.Invoke();
        }
    }
}
