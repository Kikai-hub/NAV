using UnityEngine;
using UnityEngine.InputSystem;
using NAV.Presentation.Camera;
using NAV.Gameplay.Interaction;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Building;
using NAV.Gameplay.Combat;

namespace NAV.Gameplay.Player
{
    public class PlayerDebugHud : MonoBehaviour
    {
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private ThirdPersonCameraController _cameraController;
        [SerializeField] private PlayerStamina _stamina;
        [SerializeField] private PlayerHealth _health;
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private PlayerInventory _inventory;
        [SerializeField] private PlayerCrafting _crafting;
        [SerializeField] private PlayerBuilding _building;
        [SerializeField] private PlayerCombat _combat;

        private bool _visible = true;
        private GUIStyle _style;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                _visible = !_visible;
            }
        }

        private void OnGUI()
        {
            if (!_visible || _motor == null || _inputHandler == null)
            {
                return;
            }

            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 14 };

            string cameraLine = _cameraController != null
                ? $"Cam Yaw/Pitch: {_cameraController.Yaw:F1} / {_cameraController.Pitch:F1}\n"
                : string.Empty;

            string staminaLine = _stamina != null
                ? $"Stamina: {_stamina.CurrentStamina:F0}/{_stamina.MaxStamina:F0} (CanSprint: {_stamina.CanSprint}, Draining: {_stamina.IsDraining})\n"
                : string.Empty;

            string healthLine = _health != null
                ? $"Health: {_health.CurrentHealth:F0}/{_health.MaxHealth:F0}\n"
                : string.Empty;

            string interactLine = _interactor != null
                ? $"Interact: {(_interactor.CurrentInteractable != null ? _interactor.CurrentPrompt : "-")}\n"
                : string.Empty;

            string inventoryLine = _inventory != null && _inventory.Inventory != null
                ? $"Inventory: {UsedSlotCount()}/{_inventory.Inventory.Capacity} slots used\n" +
                  $"Weight: {_inventory.Inventory.TotalWeight:F1}/{_inventory.Inventory.MaxWeight:F0} kg{(_inventory.Inventory.IsOverloaded ? " (OVERLOADED)" : string.Empty)}\n"
                : string.Empty;

            string workbenchLine = _crafting != null
                ? _crafting.NearbyWorkbenchTier > 0
                    ? $"Workbench: Tier {_crafting.NearbyWorkbenchTier} nearby\n"
                    : "Workbench: none nearby\n"
                : string.Empty;

            string buildingLine = _building != null
                ? _building.IsBuildModeActive
                    ? $"Build: {_building.SelectedPiece?.DisplayName ?? "-"} (valid: {_building.IsPlacementValid}, afford: {_building.CanAffordSelected}, snapped: {_building.IsSnapped})\n"
                    : "Build: off\n"
                : string.Empty;

            string combatLine = _combat != null
                ? $"Combat: {(_combat.EquippedWeapon != null ? _combat.EquippedWeapon.DisplayName : "no weapon")} (blocking: {_combat.IsBlocking})\n"
                : string.Empty;

            GUI.Box(new Rect(10, 10, 300, 290), GUIContent.none);
            GUI.Label(
                new Rect(20, 15, 280, 280),
                $"Grounded: {_motor.IsGrounded}\n" +
                $"Speed: {_motor.CurrentSpeed:F2} m/s\n" +
                $"Sprinting: {_motor.IsSprinting}\n" +
                $"Move Input: {_inputHandler.MoveInput}\n" +
                $"Look Input: {_inputHandler.LookInput}\n" +
                cameraLine +
                healthLine +
                staminaLine +
                interactLine +
                inventoryLine +
                workbenchLine +
                buildingLine +
                combatLine,
                _style);
        }

        private int UsedSlotCount()
        {
            int used = 0;
            foreach (var slot in _inventory.Inventory.Slots)
            {
                if (!slot.IsEmpty)
                {
                    used++;
                }
            }

            return used;
        }
    }
}
