using System;
using UnityEngine;
using NAV.Gameplay.Player;

namespace NAV.Gameplay.Interaction
{
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private float _interactRange = 3f;
        [SerializeField] private float _maxAimDistance = 15f;
        [SerializeField] private LayerMask _interactableLayers = ~0;

        private static readonly System.Collections.Generic.IComparer<RaycastHit> HitDistanceComparer =
            System.Collections.Generic.Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];

        public IInteractable CurrentInteractable { get; private set; }
        public string CurrentPrompt => CurrentInteractable is IContextualPrompt contextual
            ? contextual.GetPrompt(gameObject)
            : CurrentInteractable?.InteractionPrompt;

        /// <summary>Fires right after Interact() is called on whatever was just interacted
        /// with - lets UI (e.g. GravestoneUIController) react to a specific interaction
        /// without this class knowing anything about UI itself.</summary>
        public event Action<IInteractable> Interacted;

        private void Awake()
        {
            if (_inputHandler == null || _cameraTransform == null)
            {
                Debug.LogError($"{nameof(PlayerInteractor)} on '{name}' is missing a required reference (InputHandler/CameraTransform).", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.InteractPerformed += HandleInteractPerformed;
            }
        }

        private void OnDisable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.InteractPerformed -= HandleInteractPerformed;
            }
        }

        private void Update()
        {
            CurrentInteractable = FindInteractable();
        }

        private IInteractable FindInteractable()
        {
            // Aim from the camera (avoids the player's own collider blocking the ray), but
            // measure range from the player's position, not the camera's. In third person the
            // camera sits well behind/above the player, so a range check on ray length would
            // consume most of the budget just reaching the character instead of the target.
            //
            // Triggers are still queried (an interactable may use a trigger collider), but only a
            // trigger that carries an IInteractable on its own GameObject can stop the ray - large
            // proximity volumes (Workbench range, Creature detection radius) are triggers too, and
            // used to swallow every E press made from inside or through them. Parent lookup is
            // deliberately skipped for triggers: a placed workbench's range sphere is a child of
            // its BuildingPiece, and must not turn the whole 4 m radius into a "Demolish" target.
            int hitCount = Physics.RaycastNonAlloc(_cameraTransform.position, _cameraTransform.forward, _hitBuffer, _maxAimDistance, _interactableLayers, QueryTriggerInteraction.Collide);
            if (hitCount == 0)
            {
                return null;
            }

            Array.Sort(_hitBuffer, 0, hitCount, HitDistanceComparer);

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                var interactable = hit.collider.isTrigger
                    ? hit.collider.GetComponent<IInteractable>()
                    : hit.collider.GetComponentInParent<IInteractable>();

                if (interactable == null && hit.collider.isTrigger)
                {
                    continue;
                }

                // First solid surface (or interactable trigger) along the ray decides the result.
                if (interactable == null || Vector3.Distance(transform.position, hit.point) > _interactRange)
                {
                    return null;
                }

                return interactable.CanInteract(gameObject) ? interactable : null;
            }

            return null;
        }

        private void HandleInteractPerformed()
        {
            IInteractable interactable = CurrentInteractable;
            if (interactable == null)
            {
                return;
            }

            interactable.Interact(gameObject);
            Interacted?.Invoke(interactable);
        }
    }
}
