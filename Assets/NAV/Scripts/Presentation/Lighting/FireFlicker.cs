using UnityEngine;

namespace NAV.Presentation.Lighting
{
    /// <summary>
    /// A fire's light that lives like a flame (2026-11-13): intensity, colour and range flicker on
    /// layered noise, and the light itself wanders a few centimetres so the shadows it casts sway
    /// the way they do round a real campfire. The light casts soft shadows while the camera is
    /// near and drops them further away (several campfires in a base would otherwise each render
    /// six shadow faces every frame).
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FireFlicker : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _baseIntensity = 2.5f;
        [SerializeField, Range(0f, 1f)] private float _flicker = 0.35f;
        [SerializeField, Min(0f)] private float _speed = 7f;
        [SerializeField] private Color _hot = new Color(1f, 0.62f, 0.26f);
        [SerializeField] private Color _cool = new Color(1f, 0.42f, 0.12f);
        [SerializeField, Min(0f)] private float _wanderMetres = 0.06f;
        [SerializeField, Min(0f)] private float _shadowDistance = 35f;
        [SerializeField] private LightShadows _shadows = LightShadows.Soft;

        private Light _light;
        private Vector3 _home;
        private float _baseRange;
        private float _seed;
        private float _nextShadowCheck;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _home = transform.localPosition;
            _baseRange = _light.range;
            _seed = Random.Range(0f, 100f);
            _light.shadowStrength = 0.9f;
            _light.shadowNearPlane = 0.1f;
        }

        private void OnDisable()
        {
            if (_light != null)
            {
                transform.localPosition = _home;
            }
        }

        private void Update()
        {
            float t = Time.time * _speed + _seed;
            // Slow breathing + quick licks of flame, roughly 0..1.
            float slow = Mathf.PerlinNoise(t * 0.25f, _seed);
            float fast = Mathf.PerlinNoise(t, _seed + 3.7f);
            float n = Mathf.Clamp01(slow * 0.6f + fast * 0.55f - 0.08f);

            _light.intensity = _baseIntensity * (1f - _flicker + _flicker * 2f * n);
            _light.range = _baseRange * (0.94f + 0.12f * slow);
            _light.color = Color.Lerp(_cool, _hot, n);
            transform.localPosition = _home + new Vector3(
                (Mathf.PerlinNoise(t * 0.6f, _seed + 9f) - 0.5f) * 2f * _wanderMetres,
                (fast - 0.5f) * _wanderMetres,
                (Mathf.PerlinNoise(_seed + 5f, t * 0.6f) - 0.5f) * 2f * _wanderMetres);

            if (Time.time >= _nextShadowCheck)
            {
                _nextShadowCheck = Time.time + 0.5f;
                UnityEngine.Camera camera = UnityEngine.Camera.main;
                bool near = camera != null && (camera.transform.position - transform.position).sqrMagnitude < _shadowDistance * _shadowDistance;
                _light.shadows = near ? _shadows : LightShadows.None;
            }
        }
    }
}
