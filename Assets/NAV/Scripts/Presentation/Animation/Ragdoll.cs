using System;
using System.Collections.Generic;
using UnityEngine;

namespace NAV.Presentation.Animation
{
    /// <summary>
    /// One body of a ragdoll: a skeleton bone that gets a Rigidbody, a capsule and (unless it
    /// is the root) a CharacterJoint to the nearest part above it. Shapes are in the bone's
    /// local space; filled by NAV > Animation > Build Ragdolls from the skinned mesh.
    /// </summary>
    [Serializable]
    public class RagdollPart
    {
        public Transform Bone;
        [Tooltip("Index of the part this one hangs from (-1 = the root body, the pelvis).")]
        public int Parent = -1;
        public Vector3 Center;
        public float Radius = 0.05f;
        public float Height = 0.2f;
        [Tooltip("Capsule axis in the bone's space: 0 = X, 1 = Y, 2 = Z (also the joint's twist axis).")]
        public int Direction = 1;
        public float Mass = 1f;
        [Tooltip("Twist about the capsule axis allowed either way (degrees).")]
        public float TwistLimit = 20f;
        [Tooltip("Swing away from the capsule axis allowed (degrees).")]
        public float SwingLimit = 30f;
    }

    /// <summary>
    /// The death rule (CLAUDE.md "Death Rule"): every creature and the player die into a physics
    /// ragdoll. The parts are only described while the body lives - no rigidbodies or colliders
    /// cost anything until Activate, which turns the Animator (and whatever else poses the bones)
    /// off, builds the bodies in the pose of that very frame, gives them the body's momentum plus
    /// the killing push, and lets gravity have them. Deactivate (the player's respawn) removes the
    /// bodies again and hands the bones back to the Animator.
    /// </summary>
    public class Ragdoll : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private List<RagdollPart> _parts = new();
        [Tooltip("Scripts that pose the bones after the Animator (spine twist, hand grip, foot IK...) - off while the body is a ragdoll.")]
        [SerializeField] private Behaviour[] _disableWhileActive = Array.Empty<Behaviour>();
        [Tooltip("The living body's own colliders (the creature's hit box, the player's CharacterController) - off while the ragdoll lies, so the bones don't spawn inside them.")]
        [SerializeField] private Collider[] _collidersOffWhileActive = Array.Empty<Collider>();
        [SerializeField] private PhysicsMaterial _material;
        [SerializeField] private string _layerName = "Ragdoll";
        [Tooltip("Damping keeps a heavy corpse from rolling on forever after it lands.")]
        [SerializeField, Min(0f)] private float _linearDamping = 0.1f;
        [SerializeField, Min(0f)] private float _angularDamping = 0.8f;

        private readonly List<Rigidbody> _bodies = new();
        private readonly List<int> _savedLayers = new();

        public bool IsActive { get; private set; }
        public IReadOnlyList<RagdollPart> Parts => _parts;

        /// <summary>The root body (the pelvis) while active, else null.</summary>
        public Rigidbody RootBody => IsActive && _bodies.Count > 0 ? _bodies[0] : null;

        private void Awake()
        {
            if (_animator == null || _parts.Count == 0)
            {
                Debug.LogError($"{nameof(Ragdoll)} on '{name}' has no Animator or no parts - run NAV > Animation > Build Ragdolls.", this);
                enabled = false;
            }
        }

        /// <summary>Makes the body a ragdoll from its current pose. Velocity: how the body was
        /// moving (it keeps going); push: the killing blow, added on top.</summary>
        public void Activate(Vector3 velocity, Vector3 push)
        {
            if (IsActive || !enabled)
            {
                return;
            }

            IsActive = true;
            _animator.enabled = false;
            foreach (Behaviour behaviour in _disableWhileActive)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = false;
                }
            }

            foreach (Collider body in _collidersOffWhileActive)
            {
                if (body != null)
                {
                    body.enabled = false;
                }
            }

            int layer = LayerMask.NameToLayer(_layerName);
            _bodies.Clear();
            _savedLayers.Clear();
            foreach (RagdollPart part in _parts)
            {
                _savedLayers.Add(part.Bone.gameObject.layer);
                if (layer >= 0)
                {
                    part.Bone.gameObject.layer = layer;
                }

                var body = part.Bone.gameObject.AddComponent<Rigidbody>();
                body.mass = Mathf.Max(0.1f, part.Mass);
                body.linearDamping = _linearDamping;
                body.angularDamping = _angularDamping;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.solverIterations = 12;
                body.solverVelocityIterations = 4;
                body.maxDepenetrationVelocity = 2f;

                var capsule = part.Bone.gameObject.AddComponent<CapsuleCollider>();
                capsule.center = part.Center;
                capsule.radius = part.Radius;
                capsule.height = Mathf.Max(part.Height, part.Radius * 2f);
                capsule.direction = part.Direction;
                capsule.sharedMaterial = _material;
                _bodies.Add(body);
            }

            for (int i = 0; i < _parts.Count; i++)
            {
                RagdollPart part = _parts[i];
                if (part.Parent < 0 || part.Parent >= _bodies.Count)
                {
                    continue;
                }

                var joint = part.Bone.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = _bodies[part.Parent];
                joint.axis = Axis(part.Direction);
                joint.swingAxis = Axis((part.Direction + 1) % 3);
                joint.lowTwistLimit = new SoftJointLimit { limit = -part.TwistLimit };
                joint.highTwistLimit = new SoftJointLimit { limit = part.TwistLimit };
                joint.swing1Limit = new SoftJointLimit { limit = part.SwingLimit };
                joint.swing2Limit = new SoftJointLimit { limit = part.SwingLimit * 0.7f };
                joint.enableProjection = true;
                joint.projectionDistance = 0.02f;
                joint.projectionAngle = 10f;
                joint.enablePreprocessing = false;
            }

            // The body keeps going; the blow lands on its highest part (the shoulders / the back),
            // so it topples over away from the attacker instead of sliding off whole.
            Rigidbody highest = _bodies[0];
            foreach (Rigidbody body in _bodies)
            {
                body.linearVelocity = velocity + push * 0.4f;
                if (body.worldCenterOfMass.y > highest.worldCenterOfMass.y)
                {
                    highest = body;
                }
            }

            highest.AddForce(push * 1.5f, ForceMode.VelocityChange);
        }

        /// <summary>Back to the animated body (the player's respawn): the bodies go, the bones
        /// return to their layers and the Animator poses them again.</summary>
        public void Deactivate()
        {
            if (!IsActive)
            {
                return;
            }

            for (int i = 0; i < _parts.Count; i++)
            {
                GameObject bone = _parts[i].Bone.gameObject;
                Destroy(bone.GetComponent<CharacterJoint>());
                Destroy(bone.GetComponent<CapsuleCollider>());
            }

            // Joints must be gone before their bodies.
            foreach (Rigidbody body in _bodies)
            {
                if (body != null)
                {
                    Destroy(body);
                }
            }

            for (int i = 0; i < _parts.Count && i < _savedLayers.Count; i++)
            {
                _parts[i].Bone.gameObject.layer = _savedLayers[i];
            }

            _bodies.Clear();
            foreach (Collider body in _collidersOffWhileActive)
            {
                if (body != null)
                {
                    body.enabled = true;
                }
            }

            foreach (Behaviour behaviour in _disableWhileActive)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = true;
                }
            }

            _animator.enabled = true;
            _animator.Rebind();
            IsActive = false;
        }

        private static Vector3 Axis(int direction) => direction == 0 ? Vector3.right : direction == 1 ? Vector3.up : Vector3.forward;

#if UNITY_EDITOR
        /// <summary>Editor setup (NAV > Animation > Build Ragdolls).</summary>
        public void EditorConfigure(Animator animator, List<RagdollPart> parts, Behaviour[] disableWhileActive, Collider[] collidersOff, PhysicsMaterial material)
        {
            _animator = animator;
            _parts = parts;
            _disableWhileActive = disableWhileActive;
            _collidersOffWhileActive = collidersOff;
            _material = material;
        }
#endif
    }
}
