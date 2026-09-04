using UnityEngine;
using UnityEngine.UIElements;
using NAV.Gameplay.Player;

namespace NAV.UI
{
    /// <summary>
    /// Centered "You Died" overlay, shown for as long as PlayerDeath.IsDead. Same
    /// UIDocument-per-controller pattern as PauseUIController/PausePanel (same dark-wood/gold
    /// palette too) - the Respawn button just calls PlayerDeath.Respawn() directly, matching how
    /// PauseUIController's Resume/Leave buttons call straight into gameplay rather than owning
    /// any state themselves.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DeathUIController : MonoBehaviour
    {
        [SerializeField] private PlayerDeath _playerDeath;

        private UIDocument _document;
        private VisualElement _root;
        private Button _respawnButton;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();

            if (_playerDeath == null)
            {
                Debug.LogError($"{nameof(DeathUIController)} on '{name}' is missing a required reference (Player Death).", this);
                enabled = false;
            }
        }

        private void Start()
        {
            VisualElement documentRoot = _document.rootVisualElement;
            if (documentRoot == null)
            {
                Debug.LogError($"{nameof(DeathUIController)} on '{name}' has no rootVisualElement - check that its UIDocument has both Panel Settings and a Source Asset assigned.", this);
                enabled = false;
                return;
            }

            _root = documentRoot.Q<VisualElement>("death-root");
            _respawnButton = documentRoot.Q<Button>("respawn-button");

            if (_root == null || _respawnButton == null)
            {
                Debug.LogError($"{nameof(DeathUIController)} on '{name}' could not find 'death-root'/'respawn-button' in its UIDocument's source asset.", this);
                enabled = false;
                return;
            }

            _respawnButton.clicked += HandleRespawnClicked;

            SetVisible(_playerDeath.IsDead);
        }

        private void OnEnable()
        {
            if (_playerDeath != null)
            {
                _playerDeath.Died += HandleDied;
                _playerDeath.Respawned += HandleRespawned;
            }
        }

        private void OnDisable()
        {
            if (_playerDeath != null)
            {
                _playerDeath.Died -= HandleDied;
                _playerDeath.Respawned -= HandleRespawned;
            }
        }

        private void HandleDied()
        {
            SetVisible(true);
        }

        private void HandleRespawned()
        {
            SetVisible(false);
        }

        private void HandleRespawnClicked()
        {
            _playerDeath.Respawn();
        }

        private void SetVisible(bool visible)
        {
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
