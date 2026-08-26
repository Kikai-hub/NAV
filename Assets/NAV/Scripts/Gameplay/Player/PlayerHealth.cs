using System;
using UnityEngine;

namespace NAV.Gameplay.Player
{
    /// <summary>
    /// Holds current/max HP and exposes TakeDamage/Heal for Combat to call once it exists.
    /// No death handling yet - reaching 0 just stays at 0 (Death/Gravestone are their own
    /// separate Roadmap Phase 6/8 items). Mirrors PlayerStamina's shape (stats SO + a Changed
    /// event for UI to observe, per ARCHITECTURE_v0.1.md's "UI observes gameplay state" rule).
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private PlayerHealthStats _stats;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => _stats.MaxHealth;

        public event Action Changed;

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
            if (amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            Changed?.Invoke();
        }

        public void Heal(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Min(_stats.MaxHealth, CurrentHealth + amount);
            Changed?.Invoke();
        }
    }
}
