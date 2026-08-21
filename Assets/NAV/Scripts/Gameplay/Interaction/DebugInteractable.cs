using UnityEngine;

namespace NAV.Gameplay.Interaction
{
    /// <summary>
    /// Minimal IInteractable used to manually verify PlayerInteractor before real
    /// interactable systems (items, resource nodes, containers) exist.
    /// </summary>
    public class DebugInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _prompt = "Interact";
        [SerializeField] private Color _interactedColor = Color.green;

        public string InteractionPrompt => _prompt;

        private Renderer _renderer;
        private Color _originalColor;
        private bool _toggled;

        private void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null)
            {
                _originalColor = _renderer.material.color;
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            return true;
        }

        public void Interact(GameObject interactor)
        {
            Debug.Log($"{name} was interacted with by {interactor.name}.", this);

            if (_renderer != null)
            {
                _toggled = !_toggled;
                _renderer.material.color = _toggled ? _interactedColor : _originalColor;
            }
        }
    }
}
