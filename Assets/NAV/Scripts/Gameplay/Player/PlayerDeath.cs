using System;
using UnityEngine;
using NAV.Gameplay.Building;
using NAV.Gameplay.Combat;
using NAV.Gameplay.Interaction;

namespace NAV.Gameplay.Player
{
    /// <summary>
    /// Reacts to PlayerHealth.Died: freezes player control (movement/combat/interaction/
    /// building) and raises Died for DeathUIController to show a death screen from. Respawn
    /// teleports back to wherever the player started this scene (captured once in Awake - no
    /// world/save system yet to pick a smarter point) and revives PlayerHealth to full.
    /// Deliberately minimal: this is Roadmap Phase 7's "Death" checklist item (combat needs
    /// *something* coherent to happen at 0 HP instead of silently staying there), not Phase 6's
    /// "Gravestone"/"Respawn" - no gravestone, no inventory loss/recovery loop yet (see GDD_v0.1
    /// section 9 for that future design). Flagged, not implemented, per CLAUDE.md's scope rule.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerDeath : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private PlayerCombat _combat;
        [SerializeField] private PlayerInteractor _interactor;

        [Tooltip("Optional - only present once build mode exists on this player.")]
        [SerializeField] private PlayerBuilding _building;

        private PlayerHealth _health;
        private Vector3 _respawnPosition;
        private Quaternion _respawnRotation;

        public bool IsDead { get; private set; }

        public event Action Died;
        public event Action Respawned;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _respawnPosition = transform.position;
            _respawnRotation = transform.rotation;

            if (_inputHandler == null || _motor == null || _combat == null || _interactor == null)
            {
                Debug.LogError($"{nameof(PlayerDeath)} on '{name}' is missing a required reference (InputHandler/Motor/Combat/Interactor).", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _health.Died += HandleHealthDied;
        }

        private void OnDisable()
        {
            _health.Died -= HandleHealthDied;
        }

        private void HandleHealthDied()
        {
            if (IsDead)
            {
                return;
            }

            IsDead = true;

            _motor.enabled = false;
            _combat.enabled = false;
            _interactor.enabled = false;
            if (_building != null)
            {
                _building.enabled = false;
            }

            _inputHandler.SetMenuOpen(true);
            Debug.Log($"{name} died.", this);
            Died?.Invoke();
        }

        /// <summary>Called by DeathUIController's Respawn button.</summary>
        public void Respawn()
        {
            if (!IsDead)
            {
                return;
            }

            _motor.Teleport(_respawnPosition, _respawnRotation);
            _health.Revive();

            _motor.enabled = true;
            _combat.enabled = true;
            _interactor.enabled = true;
            if (_building != null)
            {
                _building.enabled = true;
            }

            _inputHandler.SetMenuOpen(false);
            IsDead = false;
            Respawned?.Invoke();
        }
    }
}
