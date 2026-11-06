using UnityEngine;

namespace NAV.Gameplay.Environment
{
    /// <summary>
    /// Makes a Rigidbody behave in water (any WaterBody - the sea with its waves): the part of it
    /// under the surface is pushed up (Archimedes: weight of the displaced water), and water drag
    /// slows it. Density is relative to water: below 1 it floats and bobs on the waves (wood, a
    /// felled trunk), above 1 it sinks - slowly, not like a stone through air.
    /// The body is treated as a box the height of its colliders' bounds - enough for loose items
    /// and logs. Sleeps on its own when out of the water.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class WaterBuoyancy : MonoBehaviour
    {
        [Tooltip("Relative to water: < 1 floats (wood 0.5-0.8), > 1 sinks (stone 2.5, metal 7).")]
        [SerializeField, Min(0.05f)] private float _density = 0.6f;
        [Tooltip("Drag (1/s) on movement while in water.")]
        [SerializeField, Min(0f)] private float _waterDrag = 1.5f;
        [SerializeField, Min(0f)] private float _waterAngularDrag = 1f;

        private Rigidbody _body;
        private Collider[] _colliders;

        public float Density
        {
            get => _density;
            set => _density = Mathf.Max(0.05f, value);
        }

        /// <summary>True when part of the body was under water in the last physics step.</summary>
        public bool InWater { get; private set; }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>();
        }

        private void FixedUpdate()
        {
            InWater = false;
            if (_body.isKinematic || !TryGetBounds(out Bounds bounds))
            {
                return;
            }

            Vector3 centre = bounds.center;
            if (!WaterBody.TryGetSurface(centre, out float surface) || bounds.min.y >= surface)
            {
                return;
            }

            float height = Mathf.Max(0.05f, bounds.size.y);
            float submerged = Mathf.Clamp01((surface - bounds.min.y) / height);
            InWater = true;

            // Up by the displaced water's weight, relative to the body's own weight.
            float lift = -Physics.gravity.y * submerged / _density;
            _body.AddForce(Vector3.up * lift, ForceMode.Acceleration);
            _body.AddForce(-_body.linearVelocity * (_waterDrag * submerged), ForceMode.Acceleration);
            _body.AddTorque(-_body.angularVelocity * (_waterAngularDrag * submerged), ForceMode.Acceleration);
        }

        private bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Collider collider in _colliders)
            {
                if (collider == null || !collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                if (found)
                {
                    bounds.Encapsulate(collider.bounds);
                }
                else
                {
                    bounds = collider.bounds;
                    found = true;
                }
            }

            return found;
        }
    }
}
