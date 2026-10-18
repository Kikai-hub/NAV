using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using NAV.Gameplay.Combat;
using NAV.Presentation.Animation;

namespace NAV.Editor
{
    /// <summary>
    /// Builds the combat part of PlayerAnimatorController - menu NAV > Animation > Setup Player
    /// Combat Layers (re-runnable: it replaces its own layers, leaves Base Layer locomotion
    /// alone apart from turning its IK Pass on for FootGrounding). Layers, in order:
    ///   "Full Stance" (override, full body, weight driven by PlayerAnimator - 1 only while
    ///               standing still with a weapon): Kevin's full-body combat idle per HoldStyle,
    ///               so the legs take the stance too.
    ///   "Upper Body" (override, UpperBodyMask = torso/head/arms, legs keep walking): rest poses
    ///               picked by HoldStyle + IsSprinting - Empty, Stance 1H / 2H / Polearm (combat
    ///               idles), Carry 2H / Polearm (masked hold poses while sprinting) - and every
    ///               attack, entered from Any State on the Attack trigger by AttackStyle +
    ///               AttackVariant (the combo step, AttackCombo), played at AttackSpeed, then
    ///               straight back into the fitting rest pose. Plus "Build Strike" (BuildStrike
    ///               trigger): an overhead blow played when a building piece is placed.
    ///   "Right Hand" / "Left Hand" (hand masks): fist while holding something.
    ///
    /// Every clip used is baked into a copy (Animations/Player/Combat/*.anim) with Animator
    /// curves measured by sampling it on the player's rig:
    ///   AttackTwist     - the clip's hip yaw (+ = right). The attacks/stances are authored from
    ///                     a stance with the hips turned, but the upper-body mask keeps the
    ///                     locomotion hips, so the torso would aim off to the left; PlayerAnimator
    ///                     turns the spine by this curve.
    ///   FullStanceTwist - same, on the full-body stance clips: there the hips do come from the
    ///                     clip, so PlayerAnimator takes it off AttackTwist again.
    ///   HitActive       - 1 while the striking point (weapon tip / fist) moves fast around its
    ///                     peak, else 0: the contact-hit window of that exact attack variant
    ///                     (HeldItemView -> PlayerCombat).
    /// Mirrored variants (the same clip swung from the other side, for two-handed grips and
    /// punches) are a state's Mirror flag; their curves are the mirror of the sampled ones
    /// (yaw negated, left and right swapped) - SampleAnimation can't mirror.
    /// </summary>
    internal static class PlayerCombatAnimatorSetup
    {
        private const string ControllerPath = "Assets/NAV/Animations/Player/PlayerAnimatorController.controller";
        private const string UpperBodyMaskPath = "Assets/NAV/Animations/Player/UpperBodyMask.mask";
        private const string BakedClipFolder = "Assets/NAV/Animations/Player/Combat";
        // Same rig as the player model - sampled to measure each clip.
        private const string BakeModelPath = "Assets/Kevin Iglesias/Human Character Dummy/Models/HumanCharacterDummy_M.fbx";
        private const float BakeFrameRate = 60f;
        // Where along a held weapon the striking point is measured (B-handProp +Y).
        private const float WeaponReach = 0.8f;
        // HitActive covers the frames around the speed peak above this fraction of the peak.
        private const float StrikeSpeedFraction = 0.25f;
        // Frames of lead-in before that stretch (the blade is already moving fast).
        private const int StrikeLeadFrames = 3;

        private const string KevinRoot = "Assets/Kevin Iglesias/Human Animations/";
        private const string CombatClips = KevinRoot + "Animations/Male/Combat/";
        private const string MaskedPoses = KevinRoot + "Animations/Masked Poses/";
        private const string BrawlerClips = "Assets/Brawler Animations Sample/Animations/";
        private const string RightHandMaskPath = KevinRoot + "Models/Avatar Masks/Hands/Human Hand Right Mask.mask";
        private const string LeftHandMaskPath = KevinRoot + "Models/Avatar Masks/Hands/Human Hand Left Mask.mask";

        private const string TwistParameter = "AttackTwist";
        private const string FullStanceTwistParameter = "FullStanceTwist";
        private const string HitActiveParameter = "HitActive";
        private const string HoldStyleParameter = "HoldStyle";
        private const string SprintParameter = "IsSprinting";
        private const string BuildStrikeParameter = "BuildStrike";
        // Placing a building piece: Kevin's overhead 2H blow read as a hammer strike.
        private const string BuildStrikeSource = CombatClips + "2H/HumanM@Attack2H01.fbx";
        private const float BuildStrikeSpeed = 1.8f;

        private const string FullStanceLayer = "Full Stance";
        private const string UpperBodyLayer = "Upper Body";
        private const string RightHandLayer = "Right Hand";
        private const string LeftHandLayer = "Left Hand";

        private enum Strike
        {
            None,
            Weapon,
            Fists,
        }

        private sealed class Attack
        {
            public string Name;
            public string Source;
            public bool Mirror;
            public Strike Strike;
            public WeaponAttackStyle Style;
            public int Variant;
        }

        // The combo table (variant order = combo order). Must match AttackCombo.VariantCount.
        private static readonly Attack[] Attacks =
        {
            new() { Name = "Unarmed Jab Left", Source = BrawlerClips + "Atk_P_1.fbx", Strike = Strike.Fists, Style = WeaponAttackStyle.Unarmed, Variant = 0 },
            new() { Name = "Unarmed Jab Right", Source = BrawlerClips + "Atk_P_1.fbx", Mirror = true, Strike = Strike.Fists, Style = WeaponAttackStyle.Unarmed, Variant = 1 },
            new() { Name = "Unarmed Hook", Source = BrawlerClips + "Atk_P_2.fbx", Strike = Strike.Fists, Style = WeaponAttackStyle.Unarmed, Variant = 2 },

            new() { Name = "1H Slash Right", Source = CombatClips + "1H/HumanM@Attack1H01_R.fbx", Strike = Strike.Weapon, Style = WeaponAttackStyle.OneHanded, Variant = 0 },
            new() { Name = "1H Slash Left", Source = CombatClips + "1H/HumanM@Attack1H01_L.fbx", Strike = Strike.Weapon, Style = WeaponAttackStyle.OneHanded, Variant = 1 },
            new() { Name = "1H Overhead", Source = CombatClips + "2H/HumanM@Attack2H01.fbx", Strike = Strike.Weapon, Style = WeaponAttackStyle.OneHanded, Variant = 2 },

            new() { Name = "2H Chop", Source = CombatClips + "2H/HumanM@Attack2H01.fbx", Strike = Strike.Weapon, Style = WeaponAttackStyle.TwoHanded, Variant = 0 },
            new() { Name = "2H Chop Other Side", Source = CombatClips + "2H/HumanM@Attack2H01.fbx", Mirror = true, Strike = Strike.Weapon, Style = WeaponAttackStyle.TwoHanded, Variant = 1 },

            new() { Name = "Polearm Thrust", Source = CombatClips + "Polearm/HumanM@AttackPolearm01.fbx", Strike = Strike.Weapon, Style = WeaponAttackStyle.Polearm, Variant = 0 },
            new() { Name = "Polearm Thrust Other Side", Source = CombatClips + "Polearm/HumanM@AttackPolearm01.fbx", Mirror = true, Strike = Strike.Weapon, Style = WeaponAttackStyle.Polearm, Variant = 1 },
            new() { Name = "Polearm Slam", Source = CombatClips + "2H/HumanM@Attack2H01.fbx", Strike = Strike.Weapon, Style = WeaponAttackStyle.Polearm, Variant = 2 },

            new() { Name = "Shield Bash", Source = CombatClips + "Shield/HumanM@AttackShield01.fbx", Strike = Strike.Fists, Style = WeaponAttackStyle.Shield, Variant = 0 },
            new() { Name = "Shield Punch", Source = BrawlerClips + "Atk_P_1.fbx", Mirror = true, Strike = Strike.Fists, Style = WeaponAttackStyle.Shield, Variant = 1 },
        };

        [MenuItem("NAV/Animation/Setup Player Combat Layers")]
        private static void Setup()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var bakeModelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BakeModelPath);
            AnimationClip fist = LoadClip(MaskedPoses + "Human@HandsClosed01.fbx");
            var rightHandMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(RightHandMaskPath);
            var leftHandMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(LeftHandMaskPath);
            if (controller == null || bakeModelAsset == null || fist == null || rightHandMask == null || leftHandMask == null)
            {
                Debug.LogError($"{nameof(PlayerCombatAnimatorSetup)}: missing the controller, the bake model, the fist pose or a hand mask - are Human Basic Motions/Melee Animations imported?");
                return;
            }

            if (!ValidateComboTable())
            {
                return;
            }

            EnsureParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, BuildStrikeParameter, AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "AttackStyle", AnimatorControllerParameterType.Int);
            EnsureParameter(controller, "AttackVariant", AnimatorControllerParameterType.Int);
            EnsureParameter(controller, "AttackSpeed", AnimatorControllerParameterType.Float, 1f);
            EnsureParameter(controller, "HoldingItem", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "HoldingLeftItem", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, HoldStyleParameter, AnimatorControllerParameterType.Int);
            EnsureParameter(controller, TwistParameter, AnimatorControllerParameterType.Float);
            EnsureParameter(controller, FullStanceTwistParameter, AnimatorControllerParameterType.Float);
            EnsureParameter(controller, HitActiveParameter, AnimatorControllerParameterType.Float);
            RemoveParameter(controller, "AttackMirror");

            var bakeModel = Object.Instantiate(bakeModelAsset);
            var baked = new Dictionary<string, AnimationClip>();
            AnimationClip stance1H, stance2H, stancePolearm, carry2H, carryPolearm, full1H, full2H, fullPolearm;
            try
            {
                foreach (Attack attack in Attacks)
                {
                    string key = attack.Source + (attack.Mirror ? "|M" : string.Empty);
                    if (!baked.ContainsKey(key))
                    {
                        string clipName = ClipName(attack.Source) + (attack.Mirror ? "_Mirror" : string.Empty);
                        baked[key] = Bake(LoadClip(attack.Source), clipName, bakeModel, attack.Mirror, TwistParameter, attack.Strike, 0f);
                    }
                }

                stance1H = Bake(LoadClip(CombatClips + "1H/HumanM@CombatIdle1H01.fbx"), "Stance1H", bakeModel, false, TwistParameter, Strike.None, 0f);
                stance2H = Bake(LoadClip(CombatClips + "2H/HumanM@CombatIdle2H01.fbx"), "Stance2H", bakeModel, false, TwistParameter, Strike.None, 0f);
                // Kevin's polearm guard holds the spear ~65 degrees across the body; turning the
                // upper body a further 45 degrees puts the point roughly at the enemy.
                stancePolearm = Bake(LoadClip(CombatClips + "Polearm/HumanM@CombatIdlePolearm01.fbx"), "StancePolearm", bakeModel, false, TwistParameter, Strike.None, 45f);
                carry2H = Bake(LoadClip(MaskedPoses + "HumanM@WeaponHold2H01.fbx"), "Carry2H", bakeModel, false, TwistParameter, Strike.None, 0f);
                carryPolearm = Bake(LoadClip(MaskedPoses + "HumanM@WeaponHoldPolearm01.fbx"), "CarryPolearm", bakeModel, false, TwistParameter, Strike.None, 0f);
                full1H = Bake(LoadClip(CombatClips + "1H/HumanM@CombatIdle1H01.fbx"), "FullStance1H", bakeModel, false, FullStanceTwistParameter, Strike.None, 0f);
                full2H = Bake(LoadClip(CombatClips + "2H/HumanM@CombatIdle2H01.fbx"), "FullStance2H", bakeModel, false, FullStanceTwistParameter, Strike.None, 0f);
                fullPolearm = Bake(LoadClip(CombatClips + "Polearm/HumanM@CombatIdlePolearm01.fbx"), "FullStancePolearm", bakeModel, false, FullStanceTwistParameter, Strike.None, 0f);
            }
            finally
            {
                Object.DestroyImmediate(bakeModel);
            }

            RemoveLayer(controller, FullStanceLayer);
            RemoveLayer(controller, UpperBodyLayer);
            RemoveLayer(controller, RightHandLayer);
            RemoveLayer(controller, LeftHandLayer);

            // --- Full Stance: full-body combat idle while standing still (weight from PlayerAnimator) ---
            AnimatorStateMachine full = AddLayer(controller, FullStanceLayer, null, defaultWeight: 0f);
            AnimatorState fullEmpty = full.AddState("Empty", new Vector3(300, 0, 0));
            full.defaultState = fullEmpty;
            var fullPoses = new[]
            {
                new Pose(fullEmpty, Set(Style(0))),
                new Pose(AddPoseState(full, "Full 1H", full1H, new Vector3(0, -100, 0)), Set(Style(WeaponAttackStyle.OneHanded)), Set(Style(WeaponAttackStyle.Shield))),
                new Pose(AddPoseState(full, "Full 2H", full2H, new Vector3(0, 0, 0)), Set(Style(WeaponAttackStyle.TwoHanded))),
                new Pose(AddPoseState(full, "Full Polearm", fullPolearm, new Vector3(0, 100, 0)), Set(Style(WeaponAttackStyle.Polearm))),
            };
            ConnectPoses(fullPoses);

            // --- Upper Body: weapon stances (rest poses) + attacks ---
            AnimatorStateMachine upper = AddLayer(controller, UpperBodyLayer, GetOrCreateUpperBodyMask(), defaultWeight: 1f);
            AnimatorState empty = upper.AddState("Empty", new Vector3(300, 0, 0));
            upper.defaultState = empty;

            // Which rest pose: HoldStyle (= held weapon's WeaponAttackStyle, 0 = none) and
            // sprinting. One-handers/shields drop the stance while sprinting (natural arm swing);
            // two-handers and spears switch to a carry pose instead. Each pose lists the
            // condition sets (OR of ANDs) under which it is the right one - exactly one matches.
            var poses = new[]
            {
                new Pose(empty, Set(Style(0)), Set(Style(WeaponAttackStyle.OneHanded), Sprinting(true)), Set(Style(WeaponAttackStyle.Shield), Sprinting(true))),
                new Pose(AddPoseState(upper, "Stance 1H", stance1H, new Vector3(0, -150, 0)),
                    Set(Style(WeaponAttackStyle.OneHanded), Sprinting(false)), Set(Style(WeaponAttackStyle.Shield), Sprinting(false))),
                new Pose(AddPoseState(upper, "Stance 2H", stance2H, new Vector3(0, -75, 0)), Set(Style(WeaponAttackStyle.TwoHanded), Sprinting(false))),
                new Pose(AddPoseState(upper, "Stance Polearm", stancePolearm, new Vector3(0, 0, 0)), Set(Style(WeaponAttackStyle.Polearm), Sprinting(false))),
                new Pose(AddPoseState(upper, "Carry 2H", carry2H, new Vector3(0, 75, 0)), Set(Style(WeaponAttackStyle.TwoHanded), Sprinting(true))),
                new Pose(AddPoseState(upper, "Carry Polearm", carryPolearm, new Vector3(0, 150, 0)), Set(Style(WeaponAttackStyle.Polearm), Sprinting(true))),
            };
            ConnectPoses(poses);

            for (int i = 0; i < Attacks.Length; i++)
            {
                Attack attack = Attacks[i];
                AnimationClip clip = baked[attack.Source + (attack.Mirror ? "|M" : string.Empty)];
                AddAttack(upper, poses, attack, clip, new Vector3(650, -300 + i * 50, 0));
            }

            // Hammer blow when a building piece is placed (PlayerBuilding.PiecePlaced) - its own
            // trigger so it never touches the combat combo.
            AnimatorState buildStrike = upper.AddState("Build Strike", new Vector3(650, -300 + Attacks.Length * 50, 0));
            buildStrike.motion = baked[BuildStrikeSource];
            buildStrike.speed = BuildStrikeSpeed;
            AnimatorStateTransition buildEnter = upper.AddAnyStateTransition(buildStrike);
            buildEnter.hasExitTime = false;
            buildEnter.duration = 0.08f;
            buildEnter.canTransitionToSelf = true;
            buildEnter.AddCondition(AnimatorConditionMode.If, 0f, BuildStrikeParameter);
            foreach (Pose pose in poses)
            {
                AddPoseTransitions(buildStrike, pose, hasExitTime: true, duration: 0.2f);
            }

            // --- Hands: close the fist around a held item ---
            AddGripLayer(controller, RightHandLayer, rightHandMask, fist, "HoldingItem");
            AddGripLayer(controller, LeftHandLayer, leftHandMask, fist, "HoldingLeftItem");

            // --- Base Layer: IK pass for FootGrounding (feet on the terrain) ---
            AnimatorControllerLayer[] layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"{nameof(PlayerCombatAnimatorSetup)}: rebuilt '{FullStanceLayer}', '{UpperBodyLayer}' ({Attacks.Length} attacks), '{RightHandLayer}', '{LeftHandLayer}' on {ControllerPath}.");
        }

        private static bool ValidateComboTable()
        {
            foreach (WeaponAttackStyle style in System.Enum.GetValues(typeof(WeaponAttackStyle)))
            {
                int built = Attacks.Count(a => a.Style == style);
                int expected = AttackCombo.VariantCount(style);
                bool ordered = Attacks.Where(a => a.Style == style).Select(a => a.Variant).OrderBy(v => v).SequenceEqual(Enumerable.Range(0, built));
                if (built != expected || !ordered)
                {
                    Debug.LogError($"{nameof(PlayerCombatAnimatorSetup)}: {style} has {built} attack(s) (variants must be 0..n-1) but AttackCombo.VariantCount says {expected} - keep the two tables in sync.");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Copies a humanoid clip into BakedClipFolder with a yaw curve (twistParameter: the
        /// clip's hip yaw, + = right, from the hip-joint line so it doesn't depend on bone axes,
        /// plus extraYaw) and, for attacks, HitActive (around the striking point's speed peak).
        /// mirror = the curves for the clip played mirrored (yaw negated, hands swapped).
        /// Overwrites a previous bake in place (keeps its GUID, so controller references hold).
        /// </summary>
        private static AnimationClip Bake(AnimationClip source, string clipName, GameObject model, bool mirror,
            string twistParameter, Strike strike, float extraYaw)
        {
            if (source == null)
            {
                throw new System.InvalidOperationException($"{nameof(PlayerCombatAnimatorSetup)}: missing source clip for {clipName}.");
            }

            var animator = model.GetComponent<Animator>();
            Transform leftHip = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform rightHip = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform rightProp = rightHand.Find("B-handProp.R");
            Transform leftProp = leftHand.Find("B-handProp.L");
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            int frames = Mathf.Max(1, Mathf.CeilToInt(source.length * BakeFrameRate));
            var twist = new AnimationCurve();
            var speeds = new float[frames + 1];
            var times = new float[frames + 1];
            Vector3 previous = Vector3.zero;
            Vector3 previousLeft = Vector3.zero;
            for (int frame = 0; frame <= frames; frame++)
            {
                float time = Mathf.Min(source.length, frame / BakeFrameRate);
                times[frame] = time;
                source.SampleAnimation(model, time);

                Vector3 across = model.transform.InverseTransformDirection(rightHip.position - leftHip.position);
                across.y = 0f;
                float yaw = Vector3.SignedAngle(Vector3.right, across, Vector3.up);
                twist.AddKey(time, (mirror ? -yaw : yaw) + extraYaw);

                if (strike == Strike.None)
                {
                    continue;
                }

                // Mirrored playback: the right hand does what the left one did, and vice versa.
                Transform weaponProp = mirror ? leftProp : rightProp;
                Vector3 point = strike == Strike.Weapon
                    ? model.transform.InverseTransformPoint(weaponProp.position + weaponProp.up * WeaponReach)
                    : model.transform.InverseTransformPoint(rightHand.position);
                Vector3 pointLeft = model.transform.InverseTransformPoint(leftHand.position);
                if (frame > 0)
                {
                    float speed = (point - previous).magnitude * BakeFrameRate;
                    if (strike == Strike.Fists)
                    {
                        speed = Mathf.Max(speed, (pointLeft - previousLeft).magnitude * BakeFrameRate);
                    }

                    speeds[frame] = speed;
                }

                previous = point;
                previousLeft = pointLeft;
            }

            for (int i = 0; i < twist.length; i++)
            {
                twist.SmoothTangents(i, 0f);
            }

            var copy = Object.Instantiate(source);
            copy.name = clipName;
            AnimationUtility.SetEditorCurve(copy, EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), twistParameter), twist);
            if (strike != Strike.None)
            {
                AnimationUtility.SetEditorCurve(copy, EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), HitActiveParameter), StrikeCurve(times, speeds, source.length));
            }

            if (!AssetDatabase.IsValidFolder(BakedClipFolder))
            {
                AssetDatabase.CreateFolder("Assets/NAV/Animations/Player", "Combat");
            }

            string path = $"{BakedClipFolder}/{clipName}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(copy, existing);
                Object.DestroyImmediate(copy);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        /// <summary>1 over the contiguous stretch around the fastest frame where the striking
        /// point moves at least StrikeSpeedFraction of that peak (StrikeLeadFrames of lead-in),
        /// else 0 - a near-step curve.</summary>
        private static AnimationCurve StrikeCurve(float[] times, float[] speeds, float length)
        {
            int peak = 0;
            for (int i = 1; i < speeds.Length; i++)
            {
                if (speeds[i] > speeds[peak])
                {
                    peak = i;
                }
            }

            float threshold = speeds[peak] * StrikeSpeedFraction;
            int first = peak;
            int last = peak;
            while (first > 1 && speeds[first - 1] >= threshold)
            {
                first--;
            }

            while (last < speeds.Length - 1 && speeds[last + 1] >= threshold)
            {
                last++;
            }

            float start = times[Mathf.Max(0, first - StrikeLeadFrames)];
            float end = times[last];
            const float edge = 0.001f;
            var curve = new AnimationCurve();
            curve.AddKey(new Keyframe(0f, 0f, 0f, 0f));
            if (start > edge)
            {
                curve.AddKey(new Keyframe(start - edge, 0f, 0f, 0f));
            }

            curve.AddKey(new Keyframe(start, 1f, 0f, 0f));
            curve.AddKey(new Keyframe(end, 1f, 0f, 0f));
            if (end + edge < length)
            {
                curve.AddKey(new Keyframe(end + edge, 0f, 0f, 0f));
                curve.AddKey(new Keyframe(length, 0f, 0f, 0f));
            }

            return curve;
        }

        private readonly struct Condition
        {
            public readonly AnimatorConditionMode Mode;
            public readonly float Threshold;
            public readonly string Parameter;

            public Condition(AnimatorConditionMode mode, float threshold, string parameter)
            {
                Mode = mode;
                Threshold = threshold;
                Parameter = parameter;
            }
        }

        /// <summary>A rest pose and the condition sets (any one of them, all conditions in a
        /// set) under which it is the one to be in.</summary>
        private sealed class Pose
        {
            public readonly AnimatorState State;
            public readonly Condition[][] Sets;

            public Pose(AnimatorState state, params Condition[][] sets)
            {
                State = state;
                Sets = sets;
            }
        }

        private static Condition[] Set(params Condition[] conditions) => conditions;

        private static Condition Style(int value) => new(AnimatorConditionMode.Equals, value, HoldStyleParameter);

        private static Condition Style(WeaponAttackStyle style) => Style((int)style);

        private static Condition Sprinting(bool value) => new(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, SprintParameter);

        private static AnimatorState AddPoseState(AnimatorStateMachine machine, string stateName, AnimationClip clip, Vector3 position)
        {
            AnimatorState state = machine.AddState(stateName, position);
            state.motion = clip;
            return state;
        }

        private static void ConnectPoses(Pose[] poses)
        {
            foreach (Pose from in poses)
            {
                foreach (Pose to in poses)
                {
                    if (to != from)
                    {
                        AddPoseTransitions(from.State, to, hasExitTime: false, duration: 0.25f);
                    }
                }
            }
        }

        /// <summary>One transition from 'from' to the pose per condition set of that pose.</summary>
        private static void AddPoseTransitions(AnimatorState from, Pose to, bool hasExitTime, float duration)
        {
            foreach (Condition[] set in to.Sets)
            {
                AnimatorStateTransition transition = from.AddTransition(to.State);
                transition.hasExitTime = hasExitTime;
                transition.exitTime = 0.8f;
                transition.duration = duration;
                // A combo's next attack (Any State) must cut in while an attack is still
                // easing back to its rest pose, not wait for that blend to finish.
                transition.interruptionSource = TransitionInterruptionSource.Destination;
                foreach (Condition condition in set)
                {
                    transition.AddCondition(condition.Mode, condition.Threshold, condition.Parameter);
                }
            }
        }

        private static void AddAttack(AnimatorStateMachine machine, Pose[] poses, Attack attack, AnimationClip clip, Vector3 position)
        {
            AnimatorState state = machine.AddState(attack.Name, position);
            state.motion = clip;
            state.mirror = attack.Mirror;
            state.speedParameterActive = true;
            state.speedParameter = "AttackSpeed";

            AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.08f;
            // Lets a fast follow-up restart the same swing.
            enter.canTransitionToSelf = true;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            enter.AddCondition(AnimatorConditionMode.Equals, (int)attack.Style, "AttackStyle");
            enter.AddCondition(AnimatorConditionMode.Equals, attack.Variant, "AttackVariant");

            // Straight back into whichever rest pose fits now (stance/carry/empty), not via Empty.
            foreach (Pose pose in poses)
            {
                AddPoseTransitions(state, pose, hasExitTime: true, duration: 0.2f);
            }
        }

        private static void AddGripLayer(AnimatorController controller, string layerName, AvatarMask mask, AnimationClip fist, string parameter)
        {
            AnimatorStateMachine hand = AddLayer(controller, layerName, mask, defaultWeight: 1f);
            AnimatorState open = hand.AddState("Open", new Vector3(300, 0, 0));
            AnimatorState grip = hand.AddState("Grip", new Vector3(300, 100, 0));
            grip.motion = fist;
            hand.defaultState = open;
            AddBoolTransition(open, grip, parameter, true);
            AddBoolTransition(grip, open, parameter, false);
        }

        private static void AddBoolTransition(AnimatorState from, AnimatorState to, string parameter, bool value)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.15f;
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
        }

        private static AnimatorStateMachine AddLayer(AnimatorController controller, string layerName, AvatarMask mask, float defaultWeight)
        {
            var machine = new AnimatorStateMachine { name = layerName, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(machine, controller);
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = layerName,
                stateMachine = machine,
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = defaultWeight,
            });
            return machine;
        }

        private static void RemoveLayer(AnimatorController controller, string layerName)
        {
            for (int i = controller.layers.Length - 1; i >= 0; i--)
            {
                if (controller.layers[i].name == layerName)
                {
                    controller.RemoveLayer(i);
                }
            }
        }

        private static void EnsureParameter(AnimatorController controller, string parameterName, AnimatorControllerParameterType type, float defaultFloat = 0f)
        {
            if (controller.parameters.Any(p => p.name == parameterName))
            {
                return;
            }

            controller.AddParameter(new AnimatorControllerParameter { name = parameterName, type = type, defaultFloat = defaultFloat });
        }

        private static void RemoveParameter(AnimatorController controller, string parameterName)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = parameters.Length - 1; i >= 0; i--)
            {
                if (parameters[i].name == parameterName)
                {
                    controller.RemoveParameter(i);
                }
            }
        }

        private static AvatarMask GetOrCreateUpperBodyMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            }

            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
            {
                bool upper = part is AvatarMaskBodyPart.Body or AvatarMaskBodyPart.Head
                    or AvatarMaskBodyPart.LeftArm or AvatarMaskBodyPart.RightArm
                    or AvatarMaskBodyPart.LeftFingers or AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, upper);
            }

            EditorUtility.SetDirty(mask);
            return mask;
        }

        // "…/HumanM@Attack2H01.fbx" -> "Attack2H01", "…/Atk_P_1.fbx" -> "Atk_P_1".
        private static string ClipName(string path)
        {
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            int at = file.IndexOf('@');
            return at >= 0 ? file.Substring(at + 1) : file;
        }

        private static AnimationClip LoadClip(string fbxPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview"));
        }
    }
}
