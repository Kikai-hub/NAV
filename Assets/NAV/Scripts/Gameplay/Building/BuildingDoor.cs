using UnityEngine;
using NAV.Gameplay.Interaction;

namespace NAV.Gameplay.Building
{
    /// <summary>
    /// A door or gate on a building piece: E opens or closes it. Each leaf is a hinge Transform
    /// (pivot on the hinge edge) that swings by its angle around Y; the leaf's own collider is
    /// below the hinge, so it moves with it and the opening becomes passable. Pure toggle state,
    /// not saved - doors load closed. A build-ghost copy has this behaviour switched off by
    /// PlayerBuilding, and an unplaced piece can't be used.
    /// </summary>
    public class BuildingDoor : MonoBehaviour, IInteractable
    {
        [System.Serializable]
        private class Leaf
        {
            public Transform Hinge;
            [Tooltip("Degrees around the hinge's Y when open (sign = which way it swings).")]
            public float OpenAngle = 95f;
        }

        [SerializeField] private Leaf[] _leaves = new Leaf[0];
        [SerializeField, Min(0.05f)] private float _swingSeconds = 0.6f;

        private BuildingPiece _piece;
        private float _openAmount;

        public bool IsOpen { get; private set; }

        public string InteractionPrompt
        {
            get
            {
                string name = _piece != null && _piece.Definition != null ? _piece.Definition.DisplayName : "door";
                return IsOpen ? $"Close {name}" : $"Open {name}";
            }
        }

        private void Awake()
        {
            _piece = GetComponent<BuildingPiece>();
        }

        public bool CanInteract(GameObject interactor) => enabled && (_piece == null || _piece.IsPlaced);

        /// <summary>Opened or closed by a player here (not by SetOpen) - multiplayer sends it.</summary>
        public event System.Action<BuildingDoor, bool> Toggled;

        public void Interact(GameObject interactor)
        {
            IsOpen = !IsOpen;
            Toggled?.Invoke(this, IsOpen);
        }

        /// <summary>Opens or closes it without announcing it (another player did, online).</summary>
        public void SetOpen(bool open)
        {
            IsOpen = open;
        }

        private void Update()
        {
            float target = IsOpen ? 1f : 0f;
            if (Mathf.Approximately(_openAmount, target))
            {
                return;
            }

            _openAmount = Mathf.MoveTowards(_openAmount, target, Time.deltaTime / _swingSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, _openAmount);
            foreach (Leaf leaf in _leaves)
            {
                if (leaf.Hinge != null)
                {
                    leaf.Hinge.localRotation = Quaternion.Euler(0f, leaf.OpenAngle * eased, 0f);
                }
            }
        }
    }
}
