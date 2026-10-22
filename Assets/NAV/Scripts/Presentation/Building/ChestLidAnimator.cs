using UnityEngine;
using NAV.Gameplay.Inventory;

namespace NAV.Presentation.Building
{
    /// <summary>
    /// Swings a chest's lid open while its StorageContainer is open (the container panel shows
    /// it) and shut again afterwards. The Lid transform's pivot is the hinge at the back edge;
    /// it rotates about its local X axis. Purely visual - the lid has no collider of its own.
    /// </summary>
    public class ChestLidAnimator : MonoBehaviour
    {
        [SerializeField] private StorageContainer _container;
        [SerializeField] private Transform _lid;
        [Tooltip("Lid angle when open (degrees, negative = backwards).")]
        [SerializeField] private float _openAngle = -100f;
        [Tooltip("Degrees per second.")]
        [SerializeField, Min(1f)] private float _speed = 360f;

        private Quaternion _closedRotation;
        private float _angle;

        private void Awake()
        {
            if (_container == null || _lid == null)
            {
                Debug.LogError($"{nameof(ChestLidAnimator)} on '{name}' is missing its Container or Lid.", this);
                enabled = false;
                return;
            }

            _closedRotation = _lid.localRotation;
        }

        private void Update()
        {
            float target = _container.IsOpen ? _openAngle : 0f;
            if (Mathf.Approximately(_angle, target))
            {
                return;
            }

            _angle = Mathf.MoveTowards(_angle, target, _speed * Time.deltaTime);
            _lid.localRotation = _closedRotation * Quaternion.Euler(_angle, 0f, 0f);
        }
    }
}
