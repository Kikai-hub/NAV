using UnityEngine;
using NAV.Core;
using NAV.Gameplay.Building;
using NAV.Gameplay.Interaction;
using NAV.Gameplay.Player;

namespace NAV.Gameplay.Vehicles
{
    /// <summary>
    /// A boat (the ladya, tier 3): floats, carries the player, sails and rows.
    ///  - Floating: each Float Point under the sea surface (WorldWater + the waves of WaveField)
    ///    pushes up in proportion to how deep it is, with damping - the hull settles at its
    ///    waterline, pitches and rolls on the waves and rides up on a beach. A keel force kills
    ///    sideways drift.
    ///  - E on the hull boards: the player is parented to the Seat, their movement and collider
    ///    switched off; W/S row forward/back, A/D turn, Sprint (hold) sets the sail - faster. E
    ///    again (anywhere) gets off: onto land beside the boat if there is any, else into the water.
    ///  - The open sea is endless to look at, but a boat more than World Limit Margin past the
    ///    round world's edge (WorldWater) is gently pushed back.
    /// It is a building piece (placed with the hammer, on water - BuildingPieceDefinition Floats On
    /// Water) and is saved where it floats; its hold is a StorageContainer on a child.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Boat : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform _seat;
        [Tooltip("Hull points that float: corners and middle of the hull at the waterline.")]
        [SerializeField] private Transform[] _floatPoints = new Transform[0];

        [Header("Floating")]
        [Tooltip("Upward acceleration (m/s^2) per metre a float point is under water.")]
        [SerializeField, Min(0f)] private float _buoyancy = 30f;
        [SerializeField, Min(0f)] private float _waterDamping = 3f;
        [SerializeField, Min(0f)] private float _keelGrip = 2.5f;

        // Forward drag (1/s): a boat left alone glides to a stop over a few seconds.
        private const float WaterDrag = 0.25f;

        [Header("Sailing")]
        [SerializeField, Min(0f)] private float _rowAcceleration = 2.2f;
        [SerializeField, Min(0f)] private float _sailAcceleration = 4.5f;
        [SerializeField, Min(0f)] private float _rowMaxSpeed = 3.5f;
        [SerializeField, Min(0f)] private float _sailMaxSpeed = 8f;
        [SerializeField, Min(0f)] private float _turnAcceleration = 1.2f;

        [Header("World Limit")]
        [Tooltip("Metres past the world's round edge where the sea starts pushing a boat back.")]
        [SerializeField, Min(0f)] private float _worldLimitMargin = 250f;

        private Rigidbody _body;
        private BuildingPiece _piece;
        private GameObject _driver;
        private PlayerInputHandler _input;
        private PlayerMotor _motor;
        private CharacterController _controller;
        private PlayerInteractor _interactor;
        private float _boardedAt;

        public bool HasDriver => _driver != null;
        public bool SailSet { get; private set; }

        public string InteractionPrompt
        {
            get
            {
                string name = _piece != null && _piece.Definition != null ? _piece.Definition.DisplayName : "boat";
                return $"Board {name}";
            }
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _piece = GetComponent<BuildingPiece>();
        }

        public bool CanInteract(GameObject interactor) => enabled && _driver == null && (_piece == null || _piece.IsPlaced);

        public void Interact(GameObject interactor)
        {
            if (_driver != null || interactor == null)
            {
                return;
            }

            _input = interactor.GetComponent<PlayerInputHandler>();
            _motor = interactor.GetComponent<PlayerMotor>();
            if (_input == null || _motor == null)
            {
                return;
            }

            _driver = interactor;
            _controller = interactor.GetComponent<CharacterController>();
            _interactor = interactor.GetComponent<PlayerInteractor>();
            _motor.enabled = false;
            if (_controller != null)
            {
                _controller.enabled = false;
            }

            // Its E would also reach this hull again - the boat takes E while aboard.
            if (_interactor != null)
            {
                _interactor.enabled = false;
            }

            interactor.transform.SetParent(_seat != null ? _seat : transform, false);
            interactor.transform.localPosition = Vector3.zero;
            interactor.transform.localRotation = Quaternion.identity;
            _boardedAt = Time.time;
            _input.InteractPerformed += Leave;
        }

        /// <summary>Gets off: onto land beside the boat if there is any, else into the water.</summary>
        public void Leave()
        {
            if (_driver == null || Time.time - _boardedAt < 0.3f)
            {
                return;
            }

            GameObject driver = _driver;
            _input.InteractPerformed -= Leave;
            _driver = null;
            SailSet = false;
            driver.transform.SetParent(null, true);

            Vector3 exit = FindExit();
            Quaternion facing = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            if (_controller != null)
            {
                _controller.enabled = true;
            }

            _motor.enabled = true;
            _motor.Teleport(exit, facing);
            if (_interactor != null)
            {
                _interactor.enabled = true;
            }
        }

        private Vector3 FindExit()
        {
            Vector3 centre = _seat != null ? _seat.position : transform.position;
            Vector3[] directions = { transform.right, -transform.right, transform.forward, -transform.forward };
            foreach (Vector3 direction in directions)
            {
                for (float distance = 2f; distance <= 5f; distance += 1.5f)
                {
                    Vector3 probe = centre + direction * distance + Vector3.up * 20f;
                    if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore)
                        && !hit.collider.transform.IsChildOf(transform)
                        && (!WorldWater.HasSea || hit.point.y > WorldWater.SeaHeight + 0.1f))
                    {
                        return hit.point + Vector3.up * 0.1f;
                    }
                }
            }

            // No land near: over the side into the water.
            return centre + transform.right * 1.8f;
        }

        private void OnDisable()
        {
            if (_driver != null)
            {
                _boardedAt = -10f;
                Leave();
            }
        }

        private void FixedUpdate()
        {
            if (!WorldWater.HasSea || _body.isKinematic || (_piece != null && !_piece.IsPlaced))
            {
                return;
            }

            float sea = WorldWater.SeaHeight;
            int submerged = 0;
            float share = 1f / Mathf.Max(1, _floatPoints.Length);
            foreach (Transform point in _floatPoints)
            {
                if (point == null)
                {
                    continue;
                }

                Vector3 position = point.position;
                // The drawn waves (WaveField) - each float point rides its own part of a wave, so
                // the hull pitches and rolls with them.
                float depth = sea + WaveField.HeightAt(position.x, position.z) - position.y;
                if (depth <= 0f)
                {
                    continue;
                }

                submerged++;
                // Damping only on the bobbing (vertical) motion - on all of it, the water braked
                // the boat to a crawl.
                float bob = _body.GetPointVelocity(position).y;
                Vector3 lift = Vector3.up * (_buoyancy * Mathf.Min(depth, 1f) - bob * _waterDamping);
                _body.AddForceAtPosition(lift * share, position, ForceMode.Acceleration);
            }

            if (submerged == 0)
            {
                return;
            }

            // Keel: water resists sideways motion far more than forward; a little drag on the rest.
            Vector3 right = transform.right;
            Vector3 flat = _body.linearVelocity;
            flat.y = 0f;
            float sideways = Vector3.Dot(flat, right);
            _body.AddForce(-right * sideways * _keelGrip - flat * WaterDrag, ForceMode.Acceleration);
            _body.angularVelocity *= 1f - Mathf.Clamp01(0.8f * Time.fixedDeltaTime);

            Steer();
            KeepInsideWorld();
        }

        private void Steer()
        {
            if (_driver == null || _input == null)
            {
                return;
            }

            Vector2 move = _input.MoveInput;
            SailSet = _input.SprintHeld && move.y > 0.1f;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            float speed = Vector3.Dot(_body.linearVelocity, forward);
            float maxSpeed = SailSet ? _sailMaxSpeed : _rowMaxSpeed;
            float acceleration = SailSet ? _sailAcceleration : _rowAcceleration;
            if ((move.y > 0f && speed < maxSpeed) || (move.y < 0f && speed > -_rowMaxSpeed * 0.5f))
            {
                _body.AddForce(forward * move.y * acceleration, ForceMode.Acceleration);
            }

            // Turning bites harder the faster the boat moves, but a boat at rest still turns.
            float steer = move.x * _turnAcceleration * Mathf.Lerp(0.5f, 1.2f, Mathf.Clamp01(Mathf.Abs(speed) / _sailMaxSpeed));
            _body.AddTorque(Vector3.up * steer, ForceMode.Acceleration);
        }

        private void KeepInsideWorld()
        {
            if (WorldWater.WorldRadius <= 0f)
            {
                return;
            }

            Vector3 offset = transform.position - WorldWater.WorldCentre;
            offset.y = 0f;
            float beyond = offset.magnitude - (WorldWater.WorldRadius + _worldLimitMargin);
            if (beyond > 0f)
            {
                _body.AddForce(-offset.normalized * Mathf.Min(beyond, 20f) * 0.5f, ForceMode.Acceleration);
            }
        }
    }
}
