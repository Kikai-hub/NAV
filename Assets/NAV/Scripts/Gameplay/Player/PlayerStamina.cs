using UnityEngine;

namespace NAV.Gameplay.Player
{
    public class PlayerStamina : MonoBehaviour
    {
        [SerializeField] private PlayerStaminaStats _stats;

        public float CurrentStamina { get; private set; }
        public float MaxStamina => _stats.MaxStamina;
        public bool CanSprint { get; private set; }

        private float _regenDelayTimer;

        private void Awake()
        {
            if (_stats == null)
            {
                Debug.LogError($"{nameof(PlayerStamina)} on '{name}' is missing its Stats reference.", this);
                enabled = false;
                return;
            }

            CurrentStamina = _stats.MaxStamina;
            CanSprint = true;
        }

        /// <summary>
        /// Advances stamina drain/regen by one frame and returns whether sprint is actually
        /// active. isOverloaded (carried weight at/over MaxWeight) blocks sprint outright
        /// (an overloaded character can only walk) and drains stamina passively while moving
        /// - standing still overloaded costs nothing and still regenerates normally.
        /// </summary>
        public bool TickSprint(bool wantsToSprint, bool isMoving, bool isOverloaded, float deltaTime)
        {
            bool sprinting = !isOverloaded && wantsToSprint && isMoving && CanSprint && CurrentStamina > 0f;

            if (sprinting)
            {
                CurrentStamina = Mathf.Max(0f, CurrentStamina - _stats.SprintDrainPerSecond * deltaTime);
                _regenDelayTimer = _stats.RegenDelay;

                if (CurrentStamina <= 0f)
                {
                    CanSprint = false;
                }
            }
            else if (isOverloaded && isMoving)
            {
                CurrentStamina = Mathf.Max(0f, CurrentStamina - _stats.OverloadDrainPerSecond * deltaTime);
                _regenDelayTimer = _stats.RegenDelay;

                if (CurrentStamina <= 0f)
                {
                    CanSprint = false;
                }
            }
            else if (_regenDelayTimer > 0f)
            {
                _regenDelayTimer -= deltaTime;
            }
            else
            {
                CurrentStamina = Mathf.Min(_stats.MaxStamina, CurrentStamina + _stats.RegenPerSecond * deltaTime);

                if (!CanSprint && CurrentStamina >= _stats.MinStaminaToResumeSprint)
                {
                    CanSprint = true;
                }
            }

            return sprinting;
        }
    }
}
