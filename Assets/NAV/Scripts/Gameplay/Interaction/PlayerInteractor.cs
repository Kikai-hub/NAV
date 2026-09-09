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

        public IInteractable CurrentInteractable { get; private set; }
        public string CurrentPrompt => CurrentInteractable?.InteractionPrompt;

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
            if (!Physics.Raycast(_cameraTransform.position, _cameraTransform.forward, out RaycastHit hit, _maxAimDistance, _interactableLayers, QueryTriggerInteraction.Collide))
            {
                return null;
            }

            if (Vector3.Distance(transform.position, hit.point) > _interactRange)
            {
                return null;
            }

            var interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null && interactable.CanInteract(gameObject))
            {
                return interactable;
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
