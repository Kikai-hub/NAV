using UnityEngine;

namespace NAV.Presentation.Animation
{
    /// <summary>
    /// Turns an upper body about world up, spread up the spine (Spine 25%, Chest 35%, UpperChest
    /// 40%; two bones 45/55; one bone all of it) - the attack clips' hip yaw (AttackTwist) put
    /// back after the Animator, so a swing lands where the character faces. Shared by the local
    /// player (PlayerAnimator) and network avatars (NAV.Network.NetworkAvatar). All of the turn
    /// on the lowest spine bone wrung the hero's waist (2026-11-06).
    /// </summary>
    public static class SpineTwist
    {
        public static void Apply(Transform spine, Transform chest, Transform upperChest, float twist)
        {
            if (spine == null)
            {
                return;
            }

            if (upperChest != null && chest != null)
            {
                Turn(spine, twist * 0.25f);
                Turn(chest, twist * 0.35f);
                Turn(upperChest, twist * 0.4f);
            }
            else if (chest != null)
            {
                Turn(spine, twist * 0.45f);
                Turn(chest, twist * 0.55f);
            }
            else
            {
                Turn(spine, twist);
            }
        }

        private static void Turn(Transform bone, float degrees)
        {
            bone.rotation = Quaternion.AngleAxis(degrees, Vector3.up) * bone.rotation;
        }
    }
}
