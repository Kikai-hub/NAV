using UnityEngine;

namespace NAV.Gameplay.Player
{
    /// <summary>
    /// Base health data for PlayerHealth. Food (PlayerFood) adds max-health and regeneration
    /// bonuses on top of these at runtime - this asset is the "nothing eaten" baseline.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerHealthStats", menuName = "NAV/Player/Health Stats")]
    public class PlayerHealthStats : ScriptableObject
    {
        [SerializeField] private float _maxHealth = 100f;

        [Tooltip("Health regenerated per second with no food active. 0 = health only comes back by eating (Valheim-style).")]
        [SerializeField] private float _baseRegenPerSecond;

        [Tooltip("Seconds after taking damage before any regeneration (base or food) resumes.")]
        [SerializeField] private float _regenDelayAfterDamage = 3f;

        public float MaxHealth => _maxHealth;
        public float BaseRegenPerSecond => _baseRegenPerSecond;
        public float RegenDelayAfterDamage => _regenDelayAfterDamage;

        private void OnValidate()
        {
            _maxHealth = Mathf.Max(1f, _maxHealth);
            _baseRegenPerSecond = Mathf.Max(0f, _baseRegenPerSecond);
            _regenDelayAfterDamage = Mathf.Max(0f, _regenDelayAfterDamage);
        }
    }
}
