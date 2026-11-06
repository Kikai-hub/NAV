using UnityEngine;
using NAV.Gameplay.Player;

namespace NAV.Presentation.Animation
{
    /// <summary>
    /// The player's corpse (CLAUDE.md "Death Rule"): on PlayerDeath.Died the character's Ragdoll
    /// takes over - keeping the speed the player had and pushed a little back, as if by the blow
    /// that killed them; on Respawned it hands the body back to the Animator. Presentation only -
    /// PlayerDeath still owns control, the gravestone and the respawn.
    /// </summary>
    public class PlayerRagdoll : MonoBehaviour
    {
        [SerializeField] private Ragdoll _ragdoll;
        [SerializeField] private PlayerDeath _death;
        [SerializeField] private PlayerMotor _motor;
        [Tooltip("Speed (m/s) of the push back the body falls with.")]
        [SerializeField, Min(0f)] private float _deathPush = 1.5f;

        private void Awake()
        {
            if (_ragdoll == null || _death == null || _motor == null)
            {
                Debug.LogError($"{nameof(PlayerRagdoll)} on '{name}' is missing its Ragdoll, PlayerDeath or PlayerMotor - run NAV > Animation > Build Ragdolls.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_death != null)
            {
                _death.Died += HandleDied;
                _death.Respawned += HandleRespawned;
            }
        }

        private void OnDisable()
        {
            if (_death != null)
            {
                _death.Died -= HandleDied;
                _death.Respawned -= HandleRespawned;
            }
        }

        private void HandleDied()
        {
            Vector3 velocity = _motor.MoveDirection * _motor.CurrentSpeed;
            _ragdoll.Activate(velocity, -_motor.transform.forward * _deathPush);
        }

        private void HandleRespawned()
        {
            _ragdoll.Deactivate();
        }
    }
}
