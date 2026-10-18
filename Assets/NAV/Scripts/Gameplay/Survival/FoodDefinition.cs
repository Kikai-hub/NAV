using UnityEngine;

namespace NAV.Gameplay.Survival
{
    /// <summary>
    /// What eating one item does (GDD 16: Valheim-inspired - food temporarily raises the
    /// player's attributes, it is not a hunger meter). Referenced from ItemDefinition.Food; an
    /// item with no FoodDefinition simply isn't edible. While active, each eaten food adds its
    /// bonuses on top of the player's base stats for DurationSeconds; PlayerFood owns the
    /// timers and pushes the totals into PlayerHealth/PlayerStamina. Exact formulas are "not
    /// finalized" per the GDD - every number here is placeholder balance.
    /// </summary>
    [CreateAssetMenu(fileName = "FoodDefinition", menuName = "NAV/Survival/Food Definition")]
    public class FoodDefinition : ScriptableObject
    {
        [Tooltip("Added to the player's maximum health while this food is active.")]
        [SerializeField] private float _healthBonus = 20f;

        [Tooltip("Added to the player's maximum stamina while this food is active.")]
        [SerializeField] private float _staminaBonus = 10f;

        [Tooltip("Health regenerated per second while this food is active (the player has no other health regeneration).")]
        [SerializeField] private float _healthRegenPerSecond = 1f;

        [SerializeField] private float _durationSeconds = 600f;

        public float HealthBonus => _healthBonus;
        public float StaminaBonus => _staminaBonus;
        public float HealthRegenPerSecond => _healthRegenPerSecond;
        public float DurationSeconds => _durationSeconds;

        private void OnValidate()
        {
            _healthBonus = Mathf.Max(0f, _healthBonus);
            _staminaBonus = Mathf.Max(0f, _staminaBonus);
            _healthRegenPerSecond = Mathf.Max(0f, _healthRegenPerSecond);
            _durationSeconds = Mathf.Max(1f, _durationSeconds);
        }
    }
}
