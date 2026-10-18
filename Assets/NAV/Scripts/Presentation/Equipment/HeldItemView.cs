using UnityEngine;
using NAV.Gameplay.Combat;
using NAV.Gameplay.Equipment;
using NAV.Gameplay.Items;

namespace NAV.Presentation.Equipment
{
    /// <summary>
    /// Shows the main-hand item in the character's hand: whenever PlayerEquipment changes, the
    /// held item's HeldPrefab is spawned under the hand socket (the rig's B-handProp.R bone -
    /// the attack animations are authored for props attached there at zero offset) and the
    /// previous one is removed. Purely visual - observes PlayerEquipment, owns no state, and
    /// strips colliders/rigidbodies from the spawned model so it can never block the aim rays
    /// PlayerCombat/PlayerInteractor cast or push the character around.
    ///
    /// Also PlayerCombat's IMeleeHitboxSource. Striking surfaces: the held model's MeleeHitbox
    /// capsule, plus a sphere around every free fist - both fists with empty hands (or a held
    /// item without a hitbox, e.g. the hammer), the right fist while a shield is in the left
    /// hand (its combo has a right punch). Strike timing comes from the attack animation's
    /// HitActive curve (an Animator float, baked by the combat layer setup), so each attack
    /// variant hits exactly during its own swing.
    /// </summary>
    public class HeldItemView : MonoBehaviour, IMeleeHitboxSource
    {
        private static readonly int HitActiveHash = Animator.StringToHash("HitActive");

        [SerializeField] private PlayerEquipment _equipment;
        [Tooltip("Bone the held model is parented to - B-handProp.R on the Kevin Iglesias rig.")]
        [SerializeField] private Transform _handSocket;
        [Tooltip("Bone for items held in the left hand (ItemDefinition.HeldInLeftHand - shields) - B-handProp.L.")]
        [SerializeField] private Transform _leftHandSocket;
        [Tooltip("Radius of the fist hit spheres around the hand sockets.")]
        [SerializeField, Min(0.01f)] private float _fistRadius = 0.15f;

        private GameObject _heldInstance;
        private MeleeHitbox _heldHitbox;
        private ItemDefinition _shownItem;
        private Animator _animator;
        private bool _hasHitActive;

        public bool HasStrikeTiming => _hasHitActive;

        public bool IsStriking => _hasHitActive && _animator.GetFloat(HitActiveHash) > 0.5f;

        public int GetHitCapsules(MeleeCapsule[] buffer)
        {
            int count = 0;
            if (_heldHitbox != null && count < buffer.Length)
            {
                _heldHitbox.GetWorldCapsule(out Vector3 start, out Vector3 end, out float radius);
                buffer[count++] = new MeleeCapsule(start, end, radius);
            }

            bool weaponInRight = _heldHitbox != null && _shownItem != null && !_shownItem.HeldInLeftHand;
            bool weaponInLeft = _heldHitbox != null && _shownItem != null && _shownItem.HeldInLeftHand;
            if (!weaponInRight && _handSocket != null && count < buffer.Length)
            {
                buffer[count++] = new MeleeCapsule(_handSocket.position, _handSocket.position, _fistRadius);
            }

            if (!weaponInLeft && !weaponInRight && _leftHandSocket != null && count < buffer.Length)
            {
                buffer[count++] = new MeleeCapsule(_leftHandSocket.position, _leftHandSocket.position, _fistRadius);
            }

            return count;
        }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            if (_animator != null)
            {
                foreach (AnimatorControllerParameter parameter in _animator.parameters)
                {
                    _hasHitActive |= parameter.nameHash == HitActiveHash;
                }
            }

            if (_equipment == null || _handSocket == null)
            {
                Debug.LogError($"{nameof(HeldItemView)} on '{name}' is missing a required reference (Equipment/Hand Socket).", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_equipment != null)
            {
                _equipment.Changed += Refresh;
                Refresh();
            }
        }

        private void OnDisable()
        {
            if (_equipment != null)
            {
                _equipment.Changed -= Refresh;
            }
        }

        private void Refresh()
        {
            ItemDefinition item = _equipment.MainHand;
            if (item == _shownItem && (_heldInstance != null || item == null || item.HeldPrefab == null))
            {
                return;
            }

            if (_heldInstance != null)
            {
                Destroy(_heldInstance);
                _heldInstance = null;
                _heldHitbox = null;
            }

            _shownItem = item;
            if (item == null || item.HeldPrefab == null)
            {
                return;
            }

            Transform socket = item.HeldInLeftHand && _leftHandSocket != null ? _leftHandSocket : _handSocket;
            _heldInstance = Instantiate(item.HeldPrefab, socket, false);
            _heldInstance.name = item.HeldPrefab.name;
            foreach (Collider collider in _heldInstance.GetComponentsInChildren<Collider>(true))
            {
                Destroy(collider);
            }

            foreach (Rigidbody body in _heldInstance.GetComponentsInChildren<Rigidbody>(true))
            {
                Destroy(body);
            }

            _heldHitbox = _heldInstance.GetComponentInChildren<MeleeHitbox>();
        }
    }
}
