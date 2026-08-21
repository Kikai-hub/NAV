using UnityEngine;
using NAV.Gameplay.Player;

namespace NAV.Presentation.Camera
{
    public class ThirdPersonCameraController : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private Transform _target;
        [SerializeField] private float _distance = 4.5f;
        [SerializeField] private float _minPitch = -60f;
        [SerializeField] private float _maxPitch = 80f;
        [SerializeField] private float _sensitivity = 0.12f;

        public float Yaw => _yaw;
        public float Pitch => _pitch;

        private float _yaw;
        private float _pitch;

        private void Awake()
        {
            if (_inputHandler == null || _target == null)
            {
                Debug.LogError($"{nameof(ThirdPersonCameraController)} on '{name}' is missing a required reference (InputHandler/Target).", this);
                enabled = false;
                return;
            }

            Vector3 offset = transform.position - _target.position;
            if (offset.sqrMagnitude > 0.0001f)
            {
                Quaternion lookRotation = Quaternion.LookRotation(-offset.normalized);
                Vector3 initialEuler = lookRotation.eulerAngles;
                _pitch = NormalizePitch(initialEuler.x);
                _yaw = initialEuler.y;
            }
        }

        private void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void LateUpdate()
        {
            Vector2 look = _inputHandler.LookInput;
            _yaw += look.x * _sensitivity;
            _pitch = Mathf.Clamp(_pitch - look.y * _sensitivity, _minPitch, _maxPitch);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.position = _target.position - rotation * Vector3.forward * _distance;
            transform.rotation = rotation;
        }

        private static float NormalizePitch(float pitch)
        {
            return pitch > 180f ? pitch - 360f : pitch;
        }
    }
}
