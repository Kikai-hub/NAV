using System;
using UnityEngine;

namespace NAV.Gameplay.Items
{
    /// <summary>
    /// A felled tree's trunk on its way down (the next stage of a chopped standing tree - see
    /// ResourceNodeDefinition.BecomesPrefab). The standing tree is replaced by the bare trunk the
    /// moment it's felled; this component tells when that trunk has landed - its upper part
    /// touching the ground once tipped past Min Land Tilt Degrees, or tipped past Land Tilt
    /// Degrees, or Max Fall Seconds passed - and then raises its damping so it settles instead of
    /// sliding down a slope. AnyFelled / AnyLanded let Presentation add the needle burst and
    /// the thud without this class knowing about effects. The trunk's chopping is this same
    /// object's ResourceNode.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FallingTree : MonoBehaviour
    {
        [Tooltip("Lands on the first ground contact of the upper trunk once tipped at least this far...")]
        [SerializeField, Range(10f, 90f)] private float _minLandTiltDegrees = 55f;
        [Tooltip("...or when tipped this far even without such a contact (e.g. hanging off a slope).")]
        [SerializeField, Range(10f, 120f)] private float _landTiltDegrees = 88f;
        [SerializeField, Min(0.5f)] private float _maxFallSeconds = 6f;
        [Tooltip("A contact counts as the upper trunk touching down only this far (m) up from its base - the base rests on the stump/ground the whole time it falls.")]
        [SerializeField, Min(0f)] private float _upperContactHeight = 3f;
        [Tooltip("Height (m) of the standing tree this trunk replaced - where Presentation puts the needle burst of the vanished crown.")]
        [SerializeField, Min(0f)] private float _treeHeight = 12.5f;

        [Header("After Landing")]
        [Tooltip("Damping applied once down, so a trunk lying across a slope settles instead of sliding down it.")]
        [SerializeField, Min(0f)] private float _landedLinearDamping = 1f;
        [SerializeField, Min(0f)] private float _landedAngularDamping = 4f;

        private float _age;
        private bool _touching;

        public bool HasLanded { get; private set; }
        public float TreeHeight => _treeHeight;

        /// <summary>Fires when the trunk appears in place of the felled tree.</summary>
        public static event Action<FallingTree> AnyFelled;

        /// <summary>Fires once per trunk, the moment it lands.</summary>
        public static event Action<FallingTree> AnyLanded;

        private void Start()
        {
            AnyFelled?.Invoke(this);
        }

        private void Update()
        {
            if (HasLanded)
            {
                return;
            }

            _age += Time.deltaTime;
            float tilt = Vector3.Angle(transform.up, Vector3.up);
            if ((_touching && tilt >= _minLandTiltDegrees) || tilt >= _landTiltDegrees || _age >= _maxFallSeconds)
            {
                Land();
            }

            _touching = false;
        }

        private void OnCollisionStay(Collision collision)
        {
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (Vector3.Dot(collision.GetContact(i).point - transform.position, transform.up) > _upperContactHeight)
                {
                    _touching = true;
                    return;
                }
            }
        }

        private void Land()
        {
            HasLanded = true;
            enabled = false;

            var body = GetComponent<Rigidbody>();
            body.linearDamping = _landedLinearDamping;
            body.angularDamping = _landedAngularDamping;

            AnyLanded?.Invoke(this);
        }
    }
}
