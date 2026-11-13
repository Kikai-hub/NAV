using UnityEngine;

namespace NAV.Presentation.Camera
{
    /// <summary>
    /// The main menu camera breathes a little (2026-11-13): a slow sway and drift round where it was
    /// placed, so the diorama behind the menu feels alive without pulling the eye from the buttons.
    /// </summary>
    public class MenuCameraDrift : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _moveMetres = 0.12f;
        [SerializeField, Min(0f)] private float _turnDegrees = 0.8f;
        [SerializeField, Min(0.01f)] private float _speed = 0.12f;

        private Vector3 _position;
        private Quaternion _rotation;

        private void Awake()
        {
            _position = transform.position;
            _rotation = transform.rotation;
        }

        private void LateUpdate()
        {
            float t = Time.unscaledTime * _speed;
            var offset = new Vector3(Mathf.Sin(t * 1.3f), Mathf.Sin(t * 0.9f + 1f) * 0.5f, Mathf.Sin(t * 0.7f + 2f) * 0.6f) * _moveMetres;
            transform.position = _position + _rotation * offset;
            transform.rotation = _rotation * Quaternion.Euler(Mathf.Sin(t * 0.8f + 0.5f) * _turnDegrees * 0.5f, Mathf.Sin(t * 0.6f) * _turnDegrees, 0f);
        }
    }
}
