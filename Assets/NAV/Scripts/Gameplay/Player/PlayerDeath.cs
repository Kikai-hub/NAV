using System;
using UnityEngine;
using NAV.Gameplay.Building;
using NAV.Gameplay.Combat;
using NAV.Gameplay.Interaction;
using NAV.Gameplay.Inventory;

namespace NAV.Gameplay.Player
{
    /// <summary>
    /// Reacts to PlayerHealth.Died: freezes player control (movement/combat/interaction/
    /// building), spawns a Gravestone at the death location holding the player's whole
    /// Inventory (GDD_v0.1 section 9 "Death"), and raises Died for DeathUIController to show a
    /// death screen from. Respawn teleports back to wherever the player started this scene
    /// (captured once in Awake - no world/save system yet to pick a smarter point) and revives
    /// PlayerHealth to full; the inventory itself is not restored on respawn - it stays in the
    /// gravestone until the player walks back and recovers it via GravestoneUIController's panel.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerDeath : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private PlayerCombat _combat;
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private PlayerInventory _inventory;
        [SerializeField] private Gravestone _gravestonePrefab;

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

            if (_inputHandler == null || _motor == null || _combat == null || _interactor == null || _inventory == null || _gravestonePrefab == null)
            {
                Debug.LogError($"{nameof(PlayerDeath)} on '{name}' is missing a required reference (InputHandler/Motor/Combat/Interactor/Inventory/GravestonePrefab).", this);
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
            SpawnGravestone();
            Debug.Log($"{name} died.", this);
            Died?.Invoke();
        }

        private void SpawnGravestone()
        {
            // Fully-qualified, not the bare "Inventory" type: this namespace (NAV.Gameplay.Player)
            // is a sibling of NAV.Gameplay.Inventory under the shared NAV.Gameplay parent, so the
            // bare name would resolve to that sibling *namespace* instead of the class (CS0118) -
            // see the identical note in Gravestone.cs.
            NAV.Gameplay.Inventory.Inventory gravestoneContents = _inventory.Inventory.ExtractAll();
            if (gravestoneContents.IsEmpty)
            {
                // Nothing to protect - skip spawning so an empty, permanently un-interactable
                // marker (Gravestone only self-destroys once its Contents.Changed fires, which
                // never happens if it started empty) doesn't litter the world.
                return;
            }

            Gravestone.Spawn(_gravestonePrefab, transform.position, transform.rotation, gravestoneContents);
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
