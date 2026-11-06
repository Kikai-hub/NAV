#if UNITY_EDITOR
using UnityEngine;

namespace NAV.Dev
{
    /// <summary>
    /// Video capture aid (editor only): the hero squats, takes a ball in both hands and presses
    /// it overhead. The game has no pick-up animation (E just moves the item into the
    /// inventory), so the reconstruction shot is staged here with Animator IK. VideoCapture adds
    /// this to the character's Animator object (OnAnimatorIK only runs there); Base Layer has
    /// IK Pass on already (FootGrounding).
    /// </summary>
    public class VideoCaptureLift : MonoBehaviour
    {
        private const float StandStillSeconds = 0.3f;
        private const float SquatSeconds = 0.8f;
        private const float RiseSeconds = 0.9f;
        private const float BraceSeconds = 0.2f;
        private const float PressSeconds = 0.7f;
        private const float SquatDepth = 0.25f; // of the head's height
        private const float HipsBack = 0.12f;
        private const float LeanDegrees = 50f;

        private Animator _animator;
        private Transform _ball;
        private float _radius;
        private float _startTime;
        private bool _pinned;
        private Vector3 _leftFoot;
        private Vector3 _rightFoot;
        private Vector3 _rest;
        private Vector3 _chest;
        private Vector3 _overhead;

        public void Begin(Transform ball, float radius)
        {
            _animator = GetComponent<Animator>();
            _ball = ball;
            _radius = radius;
            _startTime = Time.time + StandStillSeconds;
        }

        private static float Ease(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        private void OnAnimatorIK(int layerIndex)
        {
            float t = Time.time - _startTime;
            if (layerIndex != 0 || _ball == null || t < 0f)
            {
                return;
            }

            if (!_pinned)
            {
                Pin();
            }

            float grip = Ease(t / SquatSeconds);
            float rise = Ease((t - SquatSeconds) / RiseSeconds);
            float press = Ease((t - SquatSeconds - RiseSeconds - BraceSeconds) / PressSeconds);
            float squat = grip * (1f - rise);

            float head = _animator.GetBoneTransform(HumanBodyBones.Head).position.y - transform.position.y;
            _animator.bodyPosition += (Vector3.down * (SquatDepth * head) - transform.forward * HipsBack) * squat;
            _animator.bodyRotation = Quaternion.AngleAxis(LeanDegrees * squat, transform.right) * _animator.bodyRotation;

            _ball.position = Vector3.Lerp(Vector3.Lerp(_rest, _chest, rise), _overhead, press);

            Hold(AvatarIKGoal.LeftHand, _ball.position - transform.right * _radius, grip);
            Hold(AvatarIKGoal.RightHand, _ball.position + transform.right * _radius, grip);
            Hold(AvatarIKGoal.LeftFoot, _leftFoot, squat);
            Hold(AvatarIKGoal.RightFoot, _rightFoot, squat);
        }

        // Feet stay where they stood, the ball's path is measured from the standing body.
        private void Pin()
        {
            _pinned = true;
            _leftFoot = _animator.GetIKPosition(AvatarIKGoal.LeftFoot);
            _rightFoot = _animator.GetIKPosition(AvatarIKGoal.RightFoot);
            float head = _animator.GetBoneTransform(HumanBodyBones.Head).position.y - transform.position.y;
            _rest = _ball.position;
            _chest = transform.position + Vector3.up * (head * 0.65f) + transform.forward * (_radius + 0.1f);
            _overhead = transform.position + Vector3.up * (head + _radius + 0.2f) + transform.forward * 0.05f;
        }

        private void Hold(AvatarIKGoal goal, Vector3 position, float weight)
        {
            _animator.SetIKPosition(goal, position);
            _animator.SetIKPositionWeight(goal, weight);
        }
    }
}
#endif
