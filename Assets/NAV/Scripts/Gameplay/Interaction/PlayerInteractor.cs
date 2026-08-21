using UnityEngine;
using NAV.Gameplay.Player;

namespace NAV.Gameplay.Interaction
{
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private float _interactRange = 3f;
        [SerializeField] private LayerMask _interactableLayers = ~0;

        public IInteractable CurrentInteractable { get; private set; }
        public string CurrentPrompt => CurrentInteractable?.InteractionPrompt;

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
            if (Physics.Raycast(_cameraTransform.position, _cameraTransform.forward, out RaycastHit hit, _interactRange, _interactableLayers, QueryTriggerInteraction.Collide))
            {
                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null && interactable.CanInteract(gameObject))
                {
                    return interactable;
                }
            }

            return null;
        }

        private void HandleInteractPerformed()
        {
            CurrentInteractable?.Interact(gameObject);
        }
    }
}
