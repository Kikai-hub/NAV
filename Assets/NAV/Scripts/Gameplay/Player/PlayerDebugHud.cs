using UnityEngine;
using UnityEngine.InputSystem;
using NAV.Presentation.Camera;

namespace NAV.Gameplay.Player
{
    public class PlayerDebugHud : MonoBehaviour
    {
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private ThirdPersonCameraController _cameraController;
        [SerializeField] private PlayerStamina _stamina;

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
                ? $"Stamina: {_stamina.CurrentStamina:F0}/{_stamina.MaxStamina:F0} (CanSprint: {_stamina.CanSprint})\n"
                : string.Empty;

            GUI.Box(new Rect(10, 10, 300, 150), GUIContent.none);
            GUI.Label(
                new Rect(20, 15, 280, 140),
                $"Grounded: {_motor.IsGrounded}\n" +
                $"Speed: {_motor.CurrentSpeed:F2} m/s\n" +
                $"Sprinting: {_motor.IsSprinting}\n" +
                $"Move Input: {_inputHandler.MoveInput}\n" +
                $"Look Input: {_inputHandler.LookInput}\n" +
                cameraLine +
                staminaLine,
                _style);
        }
    }
}
