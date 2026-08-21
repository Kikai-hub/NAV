using UnityEngine;

namespace NAV.Gameplay.Player
{
    [CreateAssetMenu(fileName = "PlayerStaminaStats", menuName = "NAV/Player/Stamina Stats")]
    public class PlayerStaminaStats : ScriptableObject
    {
        [Header("Capacity")]
        [SerializeField] private float _maxStamina = 100f;

        [Header("Sprint Drain")]
        [SerializeField] private float _sprintDrainPerSecond = 20f;

        [Header("Regeneration")]
        [SerializeField] private float _regenPerSecond = 15f;
        [SerializeField] private float _regenDelay = 1f;
        [SerializeField] private float _minStaminaToResumeSprint = 15f;

        public float MaxStamina => _maxStamina;
        public float SprintDrainPerSecond => _sprintDrainPerSecond;
        public float RegenPerSecond => _regenPerSecond;
        public float RegenDelay => _regenDelay;
        public float MinStaminaToResumeSprint => _minStaminaToResumeSprint;

        private void OnValidate()
        {
            _maxStamina = Mathf.Max(1f, _maxStamina);
            _sprintDrainPerSecond = Mathf.Max(0f, _sprintDrainPerSecond);
            _regenPerSecond = Mathf.Max(0f, _regenPerSecond);
            _regenDelay = Mathf.Max(0f, _regenDelay);
            _minStaminaToResumeSprint = Mathf.Clamp(_minStaminaToResumeSprint, 0f, _maxStamina);
        }
    }
}
