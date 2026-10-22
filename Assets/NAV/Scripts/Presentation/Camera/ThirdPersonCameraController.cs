using UnityEngine;
using NAV.Gameplay.Player;
using NAV.Gameplay.Environment;
using NAV.Core.Settings;

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
        [Tooltip("Layer of the world's trees/rocks/ore - culled at the player's Object Draw Distance setting.")]
        [SerializeField] private string _resourceLayerName = "WorldResource";

        private const float WaterClearance = 0.25f;

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

        private void Start()
        {
            ApplySettings();
            GameSettings.Changed += ApplySettings;
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= ApplySettings;
        }

        /// <summary>Field of view and object draw distance from the player's settings
        /// (sensitivity and invert are read every frame in LateUpdate).</summary>
        private void ApplySettings()
        {
            var camera = GetComponent<UnityEngine.Camera>();
            if (camera == null)
            {
                return;
            }

            camera.fieldOfView = Mathf.Clamp(GameSettings.Current.FieldOfView, 40f, 100f);

            // Trees/rocks/ore (the WorldResource layer WorldGenerator spawns them on) stop being
            // drawn past this distance; 0 = the camera's far clip. Spherical, so turning the
            // camera doesn't make them pop at the screen edges.
            int resourceLayer = LayerMask.NameToLayer(_resourceLayerName);
            if (resourceLayer >= 0)
            {
                float[] distances = camera.layerCullDistances;
                distances[resourceLayer] = Mathf.Max(0f, GameSettings.Current.ObjectDrawDistance);
                camera.layerCullDistances = distances;
                camera.layerCullSpherical = true;
            }
        }

        private void LateUpdate()
        {
            Vector2 look = _inputHandler.LookInput;
            float sensitivity = _sensitivity * Mathf.Max(0.05f, GameSettings.Current.MouseSensitivity);
            float invert = GameSettings.Current.InvertY ? -1f : 1f;
            _yaw += look.x * sensitivity;
            _pitch = Mathf.Clamp(_pitch - look.y * sensitivity * invert, _minPitch, _maxPitch);

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 position = _target.position - rotation * Vector3.forward * _distance;

            // Never below the water surface (no underwater view yet) - a swimmer looking up keeps
            // the camera on the surface instead of seeing the lake bed from below.
            if (WaterBody.TryGetSurface(position, out float waterHeight) && position.y < waterHeight + WaterClearance)
            {
                position.y = waterHeight + WaterClearance;
            }

            transform.position = position;
            transform.rotation = rotation;
        }

        private static float NormalizePitch(float pitch)
        {
            return pitch > 180f ? pitch - 360f : pitch;
        }
    }
}
