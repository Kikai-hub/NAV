using NAV.Gameplay.Combat;
using UnityEngine;

namespace NAV.Presentation.Equipment
{
    /// <summary>
    /// On a held bow model (the Human Archer Animations bow): draws the string (LineRenderer, top
    /// tip -> nock -> bottom tip) and puts an arrow in the string hand while drawing.
    /// The nock is pulled from its rest point to the Anchor (full draw) on the Load clip's own
    /// timing - the hand takes the string Pull Start seconds into the clip and has it back at the
    /// cheek Pull Duration later (the pack's values) - and snaps back on the loose. Draw Progress
    /// (PlayerArchery) runs over the bow's Draw Seconds, the Load clip is played to last exactly
    /// that (PlayerAnimator), so clip time = progress x Load Clip Seconds.
    /// Runs in LateUpdate, after the animation has posed the hands.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class BowString : MonoBehaviour
    {
        // How fast the string snaps back to rest after a loose or a cancel (per second).
        private const float SnapBackSpeed = 25f;

        [SerializeField] private Transform _top;
        [SerializeField] private Transform _bottom;
        [Tooltip("The nock at rest (B-nockPoint of the pack's bow).")]
        [SerializeField] private Transform _rest;
        [Tooltip("The nock at full draw (the pack's AnchorPoint).")]
        [SerializeField] private Transform _anchor;
        [Tooltip("Arrow model put in the string hand (B-handProp.R) while drawing.")]
        [SerializeField] private GameObject _arrowInHandPrefab;
        [SerializeField] private Vector3 _arrowInHandPosition = new(0.005f, -0.002f, -0.008f);
        [SerializeField] private Vector3 _arrowInHandRotation = new(359.7f, 0.1f, 0.1f);

        [Header("Load clip timing (s)")]
        [SerializeField, Min(0.1f)] private float _loadClipSeconds = 0.83f;
        [SerializeField, Min(0f)] private float _pullStart = 0.2f;
        [SerializeField, Min(0.01f)] private float _pullDuration = 0.15f;

        private LineRenderer _line;
        private PlayerArchery _archery;
        private GameObject _arrowInHand;
        private float _pull;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.positionCount = 3;
            _line.useWorldSpace = true;
            _archery = GetComponentInParent<PlayerArchery>();
            var animator = GetComponentInParent<Animator>();
            Transform hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            Transform prop = hand != null ? hand.Find("B-handProp.R") : null;
            if (prop != null && _arrowInHandPrefab != null)
            {
                _arrowInHand = Instantiate(_arrowInHandPrefab, prop, false);
                _arrowInHand.transform.localPosition = _arrowInHandPosition;
                _arrowInHand.transform.localRotation = Quaternion.Euler(_arrowInHandRotation);
                _arrowInHand.SetActive(false);
            }
        }

        // The hand arrow lives on the character's hand, not under this bow - it goes with it.
        private void OnDestroy()
        {
            if (_arrowInHand != null)
            {
                Destroy(_arrowInHand);
            }
        }

        private void LateUpdate()
        {
            if (_top == null || _bottom == null || _rest == null)
            {
                return;
            }

            bool drawing = _archery != null && _archery.IsDrawing;
            if (drawing)
            {
                float clipTime = _archery.DrawProgress * _loadClipSeconds;
                _pull = Mathf.Clamp01((clipTime - _pullStart) / _pullDuration);
            }
            else
            {
                _pull = Mathf.MoveTowards(_pull, 0f, SnapBackSpeed * Time.deltaTime);
            }

            Vector3 nock = _anchor != null ? Vector3.Lerp(_rest.position, _anchor.position, _pull) : _rest.position;
            _line.SetPosition(0, _top.position);
            _line.SetPosition(1, nock);
            _line.SetPosition(2, _bottom.position);

            if (_arrowInHand != null && _arrowInHand.activeSelf != drawing)
            {
                _arrowInHand.SetActive(drawing);
            }
        }
    }
}
