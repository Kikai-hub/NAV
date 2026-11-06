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

        [Header("Collision")]
        [Tooltip("What the camera can't pass through (terrain, trees, rocks, buildings). Characters, creatures, items and triggers are always ignored.")]
        [SerializeField] private LayerMask _collisionLayers = ~0;
        [Tooltip("Radius of the camera's collision sphere (m) - keeps the near plane out of walls.")]
        [SerializeField, Min(0.05f)] private float _collisionRadius = 0.3f;
        [Tooltip("Closest the camera comes to the target when pushed in (m).")]
        [SerializeField, Min(0.1f)] private float _minDistance = 0.6f;
        [Tooltip("How fast (m/s) the camera eases back out once nothing is in the way.")]
        [SerializeField, Min(0.1f)] private float _returnSpeed = 6f;
        [Tooltip("Height (m) the camera keeps above the ground under it.")]
        [SerializeField, Min(0f)] private float _groundClearance = 0.35f;

        private const float WaterClearance = 0.25f;
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private float _currentDistance = -1f;

        public float Yaw => _yaw;
        public float Pitch => _pitch;

        private float _yaw;
        private float _pitch;

        // Camera shake (a boss's slam, a heavy fall): strongest shake wins, fading out.
        private float _shakeDegrees;
        private float _shakeEnd;
        private float _shakeLength = 1f;

        /// <summary>Shakes the view for a moment - degrees of wobble at the start, fading to
        /// nothing over seconds. A stronger shake replaces a weaker one still running.</summary>
        public void AddShake(float degrees, float seconds)
        {
            float remaining = CurrentShake();
            if (degrees < remaining)
            {
                return;
            }

            _shakeDegrees = degrees;
            _shakeLength = Mathf.Max(0.05f, seconds);
            _shakeEnd = Time.time + _shakeLength;
        }

        private float CurrentShake()
        {
            float left = _shakeEnd - Time.time;
            return left > 0f ? _shakeDegrees * (left / _shakeLength) : 0f;
        }

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

            // Nothing is drawn past the view distance - AtmosphereFog closes the fog in fully
            // there, so the edge is never seen. A little extra so the clip sits inside full fog.
            float view = GameSettings.Current.EffectiveViewDistance;
            camera.farClipPlane = view * 1.05f;

            // Trees/rocks/ore (the WorldResource layer WorldGenerator streams) exist only in the
            // chunks loaded around the player - the Object Draw Distance setting decides how far
            // that is - and are drawn as far as any loaded chunk reaches; past it the far-plane
            // impostors take over. Spherical, so turning the camera doesn't make them pop.
            int resourceLayer = LayerMask.NameToLayer(_resourceLayerName);
            if (resourceLayer >= 0)
            {
                float[] distances = camera.layerCullDistances;
                distances[resourceLayer] = Mathf.Min(view, NAV.World.Generation.WorldGenerator.ResourceDrawDistance(GameSettings.Current));
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
            Vector3 position = _target.position - rotation * Vector3.forward * CollideDistance(rotation);

            float shake = CurrentShake();
            if (shake > 0f)
            {
                // Rough, quick wobble on top of where the player looks (the orbit stays put).
                float t = Time.time * 28f;
                rotation *= Quaternion.Euler((Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * shake,
                    (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * shake,
                    (Mathf.PerlinNoise(t, 5.1f) - 0.5f) * shake);
            }

            // Never below the water surface (no underwater view yet) - a swimmer looking up keeps
            // the camera on the surface instead of seeing the lake bed from below.
            if (WaterBody.TryGetSurface(position, out float waterHeight) && position.y < waterHeight + WaterClearance)
            {
                position.y = waterHeight + WaterClearance;
            }

            transform.position = position;
            transform.rotation = rotation;
        }

        /// <summary>How far behind the target the camera can be: pulled in at once in front of
        /// the ground or anything solid between it and the target (a sphere cast), eased back
        /// out when the way is clear. Never under the terrain either.</summary>
        private float CollideDistance(Quaternion rotation)
        {
            Vector3 origin = _target.position;
            Vector3 back = -(rotation * Vector3.forward);
            float allowed = _distance;
            int count = Physics.SphereCastNonAlloc(origin, _collisionRadius, back, _hits, _distance, _collisionLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.distance <= 0f || Ignored(hit.collider))
                {
                    continue; // starting inside a collider (hugging a wall) - judged by the others
                }

                allowed = Mathf.Min(allowed, hit.distance);
            }

            // The terrain under the camera point (a sphere cast can slip through a terrain seam).
            for (float d = allowed; d > _minDistance; d -= 0.25f)
            {
                Vector3 point = origin + back * d;
                Terrain terrain = TerrainAt(point);
                if (terrain == null || point.y >= terrain.SampleHeight(point) + terrain.transform.position.y + _groundClearance)
                {
                    break;
                }

                allowed = d - 0.25f;
            }

            allowed = Mathf.Max(_minDistance, allowed);
            _currentDistance = _currentDistance < 0f || allowed < _currentDistance
                ? allowed
                : Mathf.MoveTowards(_currentDistance, allowed, _returnSpeed * Time.deltaTime);
            return _currentDistance;
        }

        /// <summary>The player, creatures, loose items and anything moving don't push the camera.</summary>
        private bool Ignored(Collider collider)
        {
            if (collider.transform.IsChildOf(_target.root) || collider is CharacterController)
            {
                return true;
            }

            if (collider.attachedRigidbody != null && !collider.attachedRigidbody.isKinematic)
            {
                return true;
            }

            return collider.GetComponentInParent<NAV.Gameplay.Combat.IDamageable>() != null;
        }

        private static Terrain TerrainAt(Vector3 point)
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (point.x >= origin.x && point.z >= origin.z && point.x < origin.x + size.x && point.z < origin.z + size.z)
                {
                    return terrain;
                }
            }

            return null;
        }

        private static float NormalizePitch(float pitch)
        {
            return pitch > 180f ? pitch - 360f : pitch;
        }
    }
}
