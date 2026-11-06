using System;
using NAV.Gameplay.Building;
using NAV.Gameplay.Equipment;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;
using NAV.Gameplay.Player;
using UnityEngine;

namespace NAV.Gameplay.Combat
{
    /// <summary>
    /// The bow (tier 4): with a ranged weapon in hand (WeaponDefinition.IsRanged), holding Attack
    /// draws it - Draw Progress fills over the bow's Draw Seconds - and letting go looses an arrow
    /// toward the crosshair: the first arrow stack in the inventory (ItemDefinition.Ammo) loses
    /// one, an ArrowProjectile flies at Projectile Speed and hits for (bow + arrow damage), both
    /// scaled down for a short draw. Block cancels a draw. Stamina (the bow's Attack Stamina Cost)
    /// is spent on the loose; a draw doesn't start without stamina or arrows (NoAmmo fires for the
    /// HUD). The body faces the aim while drawing. PlayerCombat leaves Attack alone for bows.
    /// </summary>
    public class PlayerArchery : MonoBehaviour
    {
        // A loose this early is a fumble: nothing flies, the arrow stays in the quiver.
        private const float MinLooseDraw = 0.15f;
        private const float MinSpeedFactor = 0.35f;
        private const float MinDamageFactor = 0.3f;

        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private PlayerCombat _combat;
        [SerializeField] private PlayerEquipment _equipment;
        [SerializeField] private PlayerInventory _inventory;
        [SerializeField] private PlayerStamina _stamina;
        [SerializeField] private Transform _cameraTransform;
        [Tooltip("Optional - no drawing while build mode is active.")]
        [SerializeField] private PlayerBuilding _building;

        [Header("Shot")]
        [Tooltip("What arrows hit (and the aim ray).")]
        [SerializeField] private LayerMask _hitLayers = ~0;
        [Tooltip("Aim ray length (m) - the arrow flies at what the crosshair is on, or this far ahead.")]
        [SerializeField, Min(10f)] private float _maxAimDistance = 250f;
        [Tooltip("Where the arrow leaves, relative to the player's feet (up) and toward the aim (forward).")]
        [SerializeField] private Vector2 _launchOffset = new(1.45f, 0.7f);

        private PlayerMotor _motor;

        public bool IsDrawing { get; private set; }
        /// <summary>0 at the start of a draw, 1 at full draw.</summary>
        public float DrawProgress { get; private set; }
        /// <summary>The arrow that will be loosed (the first in the inventory), or null.</summary>
        public ItemDefinition CurrentAmmo { get; private set; }
        public int AmmoCount { get; private set; }
        public bool HasBow => Bow != null;

        public event Action DrawStarted;
        public event Action<WeaponDefinition> Loosed;
        /// <summary>Tried to draw with no arrows in the inventory.</summary>
        public event Action NoAmmo;

        private WeaponDefinition Bow
        {
            get
            {
                WeaponDefinition weapon = _equipment != null ? _equipment.MainHandWeapon : null;
                return weapon != null && weapon.IsRanged ? weapon : null;
            }
        }

        private void Awake()
        {
            if (_inputHandler == null || _combat == null || _equipment == null || _inventory == null || _stamina == null || _cameraTransform == null)
            {
                Debug.LogError($"{nameof(PlayerArchery)} on '{name}' is missing a required reference (InputHandler/Combat/Equipment/Inventory/Stamina/CameraTransform).", this);
                enabled = false;
                return;
            }

            _motor = GetComponent<PlayerMotor>();
        }

        private void Update()
        {
            WeaponDefinition bow = Bow;
            RefreshAmmo(bow);
            bool busy = bow == null || !_combat.IsAlive || (_building != null && _building.IsBuildModeActive);
            if (busy)
            {
                CancelDraw();
                return;
            }

            if (!IsDrawing)
            {
                if (_inputHandler.AttackHeld && !_inputHandler.BlockHeld)
                {
                    TryStartDraw(bow);
                }

                return;
            }

            // Block lowers the bow without loosing.
            if (_inputHandler.BlockHeld)
            {
                CancelDraw();
                return;
            }

            if (_motor != null)
            {
                _motor.FaceAim(0.2f);
            }

            DrawProgress = Mathf.Min(1f, DrawProgress + Time.deltaTime / bow.DrawSeconds);
            if (!_inputHandler.AttackHeld)
            {
                Loose(bow);
            }
        }

        private void RefreshAmmo(WeaponDefinition bow)
        {
            CurrentAmmo = null;
            AmmoCount = 0;
            if (bow == null)
            {
                return;
            }

            foreach (ItemStack stack in _inventory.Inventory.Slots)
            {
                if (stack == null || stack.IsEmpty || !stack.Definition.IsAmmo)
                {
                    continue;
                }

                CurrentAmmo ??= stack.Definition;
                if (stack.Definition == CurrentAmmo)
                {
                    AmmoCount += stack.Quantity;
                }
            }
        }

        // Only on a fresh press - holding Attack after a loose doesn't redraw on its own.
        private bool _waitForRelease;

        private void TryStartDraw(WeaponDefinition bow)
        {
            if (_waitForRelease)
            {
                return;
            }

            _waitForRelease = true;
            if (CurrentAmmo == null)
            {
                NoAmmo?.Invoke();
                return;
            }

            if (_stamina.CurrentStamina < bow.AttackStaminaCost)
            {
                return;
            }

            IsDrawing = true;
            DrawProgress = 0f;
            DrawStarted?.Invoke();
        }

        private void LateUpdate()
        {
            if (!_inputHandler.AttackHeld)
            {
                _waitForRelease = false;
            }
        }

        private void CancelDraw()
        {
            IsDrawing = false;
            DrawProgress = 0f;
        }

        private void Loose(WeaponDefinition bow)
        {
            float draw = DrawProgress;
            CancelDraw();
            if (draw < MinLooseDraw || CurrentAmmo == null || CurrentAmmo.Ammo.ProjectilePrefab == null)
            {
                return;
            }

            ItemDefinition arrowItem = CurrentAmmo;
            if (_inventory.Inventory.RemoveItem(arrowItem, 1) < 1)
            {
                return;
            }

            _stamina.Spend(bow.AttackStaminaCost);

            Vector3 aimForward = _cameraTransform.forward;
            Vector3 flatForward = new Vector3(aimForward.x, 0f, aimForward.z).normalized;
            Vector3 start = transform.position + Vector3.up * _launchOffset.x + flatForward * _launchOffset.y;

            // Toward what the crosshair is on (the camera sits behind the player, so the arrow
            // must converge on the camera ray's hit, not fly parallel to it).
            Vector3 target = _cameraTransform.position + aimForward * _maxAimDistance;
            if (Physics.Raycast(_cameraTransform.position, aimForward, out RaycastHit hit, _maxAimDistance, _hitLayers, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform))
            {
                target = hit.point;
            }

            Vector3 direction = (target - start).normalized;
            float speed = bow.ProjectileSpeed * Mathf.Lerp(MinSpeedFactor, 1f, draw);
            float damage = (bow.Damage + CurrentAmmo.Ammo.Damage) * Mathf.Lerp(MinDamageFactor, 1f, draw);

            GameObject arrow = Instantiate(arrowItem.Ammo.ProjectilePrefab, start, Quaternion.LookRotation(direction));
            var projectile = arrow.GetComponent<ArrowProjectile>();
            if (projectile == null)
            {
                projectile = arrow.AddComponent<ArrowProjectile>();
            }

            projectile.Launch(_combat, bow, arrowItem, damage, direction * speed, _hitLayers);
            Loosed?.Invoke(bow);
        }
    }
}
