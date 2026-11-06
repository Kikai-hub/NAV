using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace NAV.Editor
{
    /// <summary>
    /// Authors the Leshy's own animation clips (no Mixamo/store clip fits a walking tree):
    /// key poses are written as rotations in the character's axes (+X right, +Y up, +Z forward,
    /// as in the T-pose) on top of a hunched "old tree" stance, then baked at 30 fps into
    /// Transform curves of the Generic rig. The heavy, viscous feel comes from:
    ///  - slow timings and eased (smootherstep) moves between attack keys;
    ///  - follow-through: every bone of the upper body lags its parent by a few hundredths of a
    ///    second, so a move travels up the spine and out to the twig fingers like through sap;
    ///  - weight: the hips sink after each step and after the slam - only as far as the bent
    ///    knees allow, so the feet stay on the ground (no foot IK on a 4.6 m tree).
    /// Clips: Idle, Walk (in place, ~WalkSpeed m/s), Attack_Slam, Attack_Sweep, Roar, HitReact.
    /// Death is not a clip: the body goes to its ragdoll (CLAUDE.md "Death Rule").
    /// Leshy.controller: blend tree Locomotion on "Speed", triggers Slam/Sweep/Roar/Hit.
    /// </summary>
    public static class LeshyAnimationSetup
    {
        private const string ClipFolder = LeshyModelSetup.Folder + "Animations/";
        private const string ControllerPath = LeshyModelSetup.Folder + "Leshy.controller";
        private const float Fps = 30f;

        /// <summary>Ground speed (m/s) the walk clip shows: ~1.69 m stride per 1.6 s step.</summary>
        public const float WalkSpeed = 1.05f;

        /// <summary>Seconds into Attack_Slam / Attack_Sweep when the hands strike (incl. the lag).</summary>
        public const float SlamHitTime = 2.15f;
        public const float SweepHitTime = 1.95f;

        private sealed class Pose
        {
            public readonly Dictionary<string, Vector3> Rotations = new();
            public Vector3 Hips;

            public Pose R(string bone, float x, float y, float z)
            {
                Rotations[bone] = new Vector3(x, y, z);
                return this;
            }

            public Pose H(float x, float y, float z)
            {
                Hips = new Vector3(x, y, z);
                return this;
            }

            public Pose Copy()
            {
                var copy = new Pose { Hips = Hips };
                foreach (KeyValuePair<string, Vector3> pair in Rotations)
                {
                    copy.Rotations[pair.Key] = pair.Value;
                }

                return copy;
            }

            /// <summary>Left-right mirror: swap sides, flip yaw and roll, flip the hips sideways.</summary>
            public Pose Mirror()
            {
                var mirror = new Pose { Hips = new Vector3(-Hips.x, Hips.y, Hips.z) };
                foreach (KeyValuePair<string, Vector3> pair in Rotations)
                {
                    string name = pair.Key.StartsWith("Left") ? "Right" + pair.Key.Substring(4)
                        : pair.Key.StartsWith("Right") ? "Left" + pair.Key.Substring(5) : pair.Key;
                    mirror.Rotations[name] = new Vector3(pair.Value.x, -pair.Value.y, -pair.Value.z);
                }

                return mirror;
            }
        }

        private sealed class ClipSpec
        {
            public string Name;
            public float Length;
            public bool Loop;
            public float LagPerLevel;
            public readonly List<(float time, Pose pose)> Keys = new();

            public ClipSpec Key(float time, Pose pose)
            {
                Keys.Add((time, pose));
                return this;
            }
        }

        [MenuItem("NAV/Characters/Build Leshy Animations")]
        public static void Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(LeshyModelSetup.ModelPrefabPath);
            if (model == null)
            {
                Debug.LogError($"Leshy: build the model first ({LeshyModelSetup.ModelPrefabPath} is missing).");
                return;
            }

            if (!AssetDatabase.IsValidFolder(ClipFolder.TrimEnd('/')))
            {
                AssetDatabase.CreateFolder(LeshyModelSetup.Folder.TrimEnd('/'), "Animations");
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            var clips = new Dictionary<string, AnimationClip>();
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var rig = new Rig(instance.transform);
                foreach (ClipSpec spec in Specs())
                {
                    clips[spec.Name] = Bake(spec, rig);
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            AnimatorController controller = BuildController(clips);
            var prefabRoot = PrefabUtility.LoadPrefabContents(LeshyModelSetup.ModelPrefabPath);
            prefabRoot.GetComponent<Animator>().runtimeAnimatorController = controller;
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, LeshyModelSetup.ModelPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            AssetDatabase.SaveAssets();
            Debug.Log($"Leshy: {clips.Count} clips baked -> {ClipFolder}, controller {ControllerPath}");
        }

        // ---------------------------------------------------------------- the poses

        private static Pose Stance()
        {
            var p = new Pose()
                .R("Spine", 8, 0, 0).R("Spine1", 5, 0, 0).R("Spine2", 3, 0, 0)
                .R("Neck", -6, 0, 0).R("Head", -6, 0, 0)
                .R("LeftShoulder", 0, 0, 6).R("LeftArm", 0, 12, 58).R("LeftForeArm", 0, 28, 0).R("LeftHand", 0, 0, 10)
                .R("LeftUpLeg", -8, 0, 0).R("LeftLeg", 14, 0, 0).R("LeftFoot", -6, 0, 0)
                .H(0, -0.02f, 0);
            foreach (string finger in new[] { "Index", "Middle" })
            {
                for (int k = 1; k <= 3; k++)
                {
                    p.R("LeftHand" + finger + k, 0, 0, 14);
                }
            }

            p.R("LeftHandThumb1", 0, 8, 0).R("LeftHandThumb2", 0, 8, 0);
            Pose right = p.Mirror();
            foreach (KeyValuePair<string, Vector3> pair in right.Rotations)
            {
                if (pair.Key.StartsWith("Right"))
                {
                    p.Rotations[pair.Key] = pair.Value;
                }
            }

            return p;
        }

        private static IEnumerable<ClipSpec> Specs()
        {
            // Idle: a slow breath, the trunk sways and the head creaks round to look.
            yield return new ClipSpec { Name = "Idle", Length = 6f, Loop = true, LagPerLevel = 0.045f }
                .Key(0f, new Pose())
                .Key(1.5f, new Pose().R("Spine1", 2, 0, 1).R("Spine2", 2, 0, 0).R("Neck", 0, -4, 0).R("Head", 0, -10, 0)
                    .R("LeftArm", 0, 4, -2).R("RightArm", 0, 2, 2).R("Hips", 0, 0, 1.5f)
                    .R("LeftUpLeg", 0, 0, -1.5f).R("RightUpLeg", 0, 0, -1.5f).H(0, 0.015f, 0))
                .Key(3f, new Pose().R("Spine1", -1, 0, 0).R("Spine2", -1, 0, 0).R("Head", 2, -3, 0).H(0, -0.01f, 0))
                .Key(4.5f, new Pose().R("Spine1", 2, 0, -1).R("Spine2", 2, 0, 0).R("Neck", 0, 4, 0).R("Head", 0, 10, 0)
                    .R("LeftArm", 0, -2, -2).R("RightArm", 0, -4, 2).R("Hips", 0, 0, -1.5f)
                    .R("LeftUpLeg", 0, 0, 1.5f).R("RightUpLeg", 0, 0, 1.5f).H(0, 0.015f, 0));

            // Walk: heavy steps, the hips sink after each foot lands; second half = mirror.
            Pose contact = new Pose().H(0, -0.15f, 0).R("Hips", 0, 6, 0).R("Spine2", 2, -7, 0).R("Head", 0, 3, 0)
                .R("LeftUpLeg", -22, -6, 0).R("LeftLeg", 4, 0, 0).R("LeftFoot", -8, 0, 0)
                .R("RightUpLeg", 16, -6, 0).R("RightLeg", 22, 0, 0).R("RightFoot", 16, 0, 0)
                .R("LeftArm", 0, -14, 0).R("RightArm", 0, -14, 0).R("LeftForeArm", 0, -6, 0).R("RightForeArm", 0, -10, 0);
            Pose settle = new Pose().H(0, -0.17f, 0).R("Hips", 0, 5, 0).R("Spine", 4, 0, 0).R("Spine2", 2, -6, 0).R("Head", 3, 2, 0)
                .R("LeftUpLeg", -24, -5, 0).R("LeftLeg", 30, 0, 0).R("LeftFoot", -6, 0, 0)
                .R("RightUpLeg", 18, -5, 0).R("RightLeg", 34, 0, 0).R("RightFoot", 24, 0, 0)
                .R("LeftArm", 0, -12, 0).R("RightArm", 0, -12, 0);
            Pose passing = new Pose().H(0, -0.03f, 0)
                .R("LeftUpLeg", 2, 0, 0).R("LeftLeg", 8, 0, 0).R("LeftFoot", -8, 0, 0)
                .R("RightUpLeg", -24, 0, 0).R("RightLeg", 55, 0, 0).R("RightFoot", -6, 0, 0);
            Pose reach = new Pose().H(0, -0.09f, 0).R("Hips", 0, -4, 0).R("Spine2", 0, 4, 0)
                .R("LeftUpLeg", 12, 4, 0).R("LeftLeg", 8, 0, 0).R("LeftFoot", 6, 0, 0)
                .R("RightUpLeg", -26, 4, 0).R("RightLeg", 18, 0, 0).R("RightFoot", -10, 0, 0)
                .R("LeftArm", 0, 10, 0).R("RightArm", 0, 10, 0);
            yield return new ClipSpec { Name = "Walk", Length = 3.2f, Loop = true, LagPerLevel = 0.035f }
                .Key(0f, contact).Key(0.2f, settle).Key(0.55f, passing).Key(1.1f, reach)
                .Key(1.6f, contact.Mirror()).Key(1.8f, settle.Mirror()).Key(2.15f, passing.Mirror()).Key(2.7f, reach.Mirror());

            // Slam: both arms slowly up over the head, a shaking hold, then down into the ground.
            Pose windup = new Pose().H(0, 0f, -0.05f)
                .R("Spine", -10, 0, 0).R("Spine1", -8, 0, 0).R("Spine2", -6, 0, 0).R("Neck", -4, 0, 0).R("Head", -8, 0, 0)
                .R("LeftArm", 0, 25, -125).R("RightArm", 0, -25, 125).R("LeftForeArm", 0, 20, 0).R("RightForeArm", 0, -20, 0);
            Pose hold = windup.Copy().R("Spine", -12, 0, 0).R("LeftArm", 0, 25, -130).R("RightArm", 0, -25, 130);
            Pose slam = new Pose().H(0, -0.22f, 0.12f)
                .R("Spine", 28, 0, 0).R("Spine1", 14, 0, 0).R("Spine2", 8, 0, 0).R("Neck", 6, 0, 0).R("Head", 8, 0, 0)
                .R("LeftArm", 0, 62, -20).R("RightArm", 0, -62, 20).R("LeftForeArm", 0, -20, 0).R("RightForeArm", 0, 20, 0)
                .R("LeftUpLeg", -22, 0, 0).R("LeftLeg", 40, 0, 0).R("LeftFoot", -18, 0, 0)
                .R("RightUpLeg", -22, 0, 0).R("RightLeg", 40, 0, 0).R("RightFoot", -18, 0, 0);
            Pose stuck = slam.Copy().H(0, -0.24f, 0.12f).R("Spine", 31, 0, 0);
            yield return new ClipSpec { Name = "Attack_Slam", Length = 3.6f, LagPerLevel = 0.025f }
                .Key(0f, new Pose()).Key(1.3f, windup).Key(1.7f, hold).Key(2f, slam).Key(2.15f, stuck)
                .Key(2.6f, slam.Copy().H(0, -0.23f, 0.1f).R("Spine", 29, 0, 0)).Key(3.6f, new Pose());

            // Sweep: the trunk winds right, the right arm swings across like a falling bough.
            Pose wind = new Pose().H(0, -0.02f, 0).R("Hips", 0, 10, 0).R("LeftUpLeg", 0, -10, 0).R("RightUpLeg", 0, -10, 0)
                .R("Spine", 0, 10, 0).R("Spine1", 0, 10, 0).R("Spine2", 0, 8, 0).R("Head", 0, -12, 0)
                .R("RightArm", 0, 45, 48).R("RightForeArm", 0, 10, 0).R("LeftArm", 0, 20, 0);
            Pose sweep = new Pose().H(0, -0.08f, 0).R("Hips", 0, -10, 0)
                .R("LeftUpLeg", -10, 10, 0).R("LeftLeg", 20, 0, 0).R("LeftFoot", -10, 0, 0)
                .R("RightUpLeg", -10, 10, 0).R("RightLeg", 20, 0, 0).R("RightFoot", -10, 0, 0)
                .R("Spine", 0, -12, 0).R("Spine1", 0, -12, 0).R("Spine2", 0, -12, 0).R("Head", 0, 14, 0)
                .R("RightArm", 0, -75, 38).R("RightForeArm", 0, 25, 0).R("LeftArm", 0, -20, 0);
            yield return new ClipSpec { Name = "Attack_Sweep", Length = 3.2f, LagPerLevel = 0.025f }
                .Key(0f, new Pose()).Key(1.2f, wind).Key(1.45f, wind.Copy().R("Spine2", 0, 11, 0))
                .Key(1.85f, sweep).Key(2.05f, sweep.Copy().R("Spine2", 0, -16, 0).R("RightArm", 0, -90, 36))
                .Key(3.2f, new Pose());

            // Roar: a deep inhale crouch, then the chest opens, arms spread, the body shakes.
            Pose inhale = new Pose().H(0, -0.08f, 0)
                .R("LeftUpLeg", -10, 0, 0).R("LeftLeg", 20, 0, 0).R("LeftFoot", -10, 0, 0)
                .R("RightUpLeg", -10, 0, 0).R("RightLeg", 20, 0, 0).R("RightFoot", -10, 0, 0).R("Spine", 12, 0, 0).R("Spine1", 6, 0, 0).R("Head", 8, 0, 0)
                .R("LeftArm", 0, 20, 10).R("RightArm", 0, -20, -10);
            Pose roar = new Pose().H(0, 0.02f, -0.05f)
                .R("Spine", -14, 0, 0).R("Spine1", -10, 0, 0).R("Spine2", -8, 0, 0).R("Neck", -10, 0, 0).R("Head", -16, 0, 0)
                .R("LeftArm", 0, -25, -60).R("RightArm", 0, 25, 60).R("LeftForeArm", 0, -20, 0).R("RightForeArm", 0, 20, 0)
                .R("LeftHandIndex1", 0, 0, -14).R("LeftHandMiddle1", 0, 0, -14).R("RightHandIndex1", 0, 0, 14).R("RightHandMiddle1", 0, 0, 14);
            var roarClip = new ClipSpec { Name = "Roar", Length = 4.4f, LagPerLevel = 0.04f }
                .Key(0f, new Pose()).Key(0.9f, inhale).Key(1.6f, roar);
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                roarClip.Key(1.9f + i * 0.3f, roar.Copy().R("Head", -17, 2 * side, 0).R("Spine2", -8.5f, 0, side));
            }

            yield return roarClip.Key(3.2f, roar).Key(4.4f, new Pose());

            // Hit: the trunk gives way backwards and the limbs catch up late.
            yield return new ClipSpec { Name = "HitReact", Length = 1.6f, LagPerLevel = 0.06f }
                .Key(0f, new Pose())
                .Key(0.22f, new Pose().H(0, -0.04f, -0.12f).R("Spine", -8, 0, 0).R("Spine1", -6, 0, 0).R("Head", -8, 0, 0)
                    .R("LeftArm", 0, -10, -8).R("RightArm", 0, 10, 8))
                .Key(1.6f, new Pose());
        }

        // ---------------------------------------------------------------- baking

        private sealed class Rig
        {
            public readonly Transform Root;
            public readonly Dictionary<string, Transform> Bones = new();
            public readonly Dictionary<string, int> UpperDepth = new();

            public Rig(Transform root)
            {
                Root = root;
                Transform hips = LeshyModelSetup.FindBone(root, "Hips");
                foreach (Transform t in hips.GetComponentsInChildren<Transform>(true))
                {
                    Bones[LeshyModelSetup.ShortName(t)] = t;
                }

                Transform spine = Bones["Spine"];
                foreach (Transform t in spine.GetComponentsInChildren<Transform>(true))
                {
                    int depth = 1;
                    for (Transform p = t; p != spine; p = p.parent)
                    {
                        depth++;
                    }

                    UpperDepth[LeshyModelSetup.ShortName(t)] = depth;
                }
            }
        }

        private static AnimationClip Bake(ClipSpec spec, Rig rig)
        {
            Pose stance = Stance();
            var bones = new HashSet<string>(stance.Rotations.Keys);
            foreach ((float _, Pose pose) in spec.Keys)
            {
                bones.UnionWith(pose.Rotations.Keys);
            }

            bones.Add("Hips");
            int maxDepth = 0;
            foreach (string bone in bones)
            {
                if (rig.UpperDepth.TryGetValue(bone, out int d))
                {
                    maxDepth = Mathf.Max(maxDepth, d);
                }
            }

            // A one-shot clip runs on until its last-lagging twig has settled.
            float length = spec.Loop ? spec.Length : spec.Length + maxDepth * spec.LagPerLevel;
            int frames = Mathf.RoundToInt(length * Fps);
            var clip = new AnimationClip { name = "Leshy_" + spec.Name, frameRate = Fps };

            foreach (string bone in bones)
            {
                if (!rig.Bones.TryGetValue(bone, out Transform t))
                {
                    Debug.LogWarning($"Leshy: clip {spec.Name} names bone '{bone}' the rig doesn't have.");
                    continue;
                }

                Quaternion restLocal = t.localRotation;
                Quaternion restWorld = t.rotation;
                float lag = rig.UpperDepth.TryGetValue(bone, out int depth) ? depth * spec.LagPerLevel : 0f;
                stance.Rotations.TryGetValue(bone, out Vector3 baseEuler);

                var curves = new AnimationCurve[4];
                var values = new float[4][];
                for (int c = 0; c < 4; c++)
                {
                    values[c] = new float[frames + 1];
                }

                Quaternion previous = restLocal;
                for (int f = 0; f <= frames; f++)
                {
                    float time = f / Fps;
                    Vector3 euler = baseEuler + Sample(spec, time - lag, pose => pose.Rotations.TryGetValue(bone, out Vector3 e) ? e : Vector3.zero);
                    Quaternion delta = Quaternion.Inverse(restWorld) * Quaternion.Euler(euler) * restWorld;
                    Quaternion q = restLocal * delta;
                    if (Quaternion.Dot(q, previous) < 0f)
                    {
                        q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    }

                    previous = q;
                    values[0][f] = q.x; values[1][f] = q.y; values[2][f] = q.z; values[3][f] = q.w;
                }

                string path = AnimationUtility.CalculateTransformPath(t, rig.Root);
                string[] channels = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
                for (int c = 0; c < 4; c++)
                {
                    curves[c] = ToCurve(values[c]);
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), channels[c]), curves[c]);
                }
            }

            // Hips height/sway (metres, model axes = the hips' parent).
            Transform hipsBone = rig.Bones["Hips"];
            Vector3 restPosition = hipsBone.localPosition;
            var position = new float[3][];
            for (int c = 0; c < 3; c++)
            {
                position[c] = new float[frames + 1];
            }

            for (int f = 0; f <= frames; f++)
            {
                Vector3 p = restPosition + stance.Hips + Sample(spec, f / Fps, pose => pose.Hips);
                position[0][f] = p.x; position[1][f] = p.y; position[2][f] = p.z;
            }

            string hipsPath = AnimationUtility.CalculateTransformPath(hipsBone, rig.Root);
            string[] axes = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
            for (int c = 0; c < 3; c++)
            {
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(hipsPath, typeof(Transform), axes[c]), ToCurve(position[c]));
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = spec.Loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.EnsureQuaternionContinuity();

            string clipPath = ClipFolder + clip.name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                Object.DestroyImmediate(clip);
                return existing;
            }

            AssetDatabase.CreateAsset(clip, clipPath);
            return clip;
        }

        /// <summary>
        /// The pose value at a time: loops go through their keys on a Catmull-Rom spline (the
        /// motion never stops), one-shots ease in and out of every key (heavy starts and stops).
        /// </summary>
        private static Vector3 Sample(ClipSpec spec, float time, System.Func<Pose, Vector3> get)
        {
            List<(float time, Pose pose)> keys = spec.Keys;
            int count = keys.Count;
            if (spec.Loop)
            {
                time = Mathf.Repeat(time, spec.Length);
                int i = count - 1;
                for (int k = 0; k < count; k++)
                {
                    if (keys[k].time <= time)
                    {
                        i = k;
                    }
                }

                float t0 = keys[i].time;
                float t1 = i + 1 < count ? keys[i + 1].time : spec.Length + keys[0].time;
                float u = (time - t0) / Mathf.Max(1e-4f, t1 - t0);
                Vector3 p0 = get(keys[(i - 1 + count) % count].pose);
                Vector3 p1 = get(keys[i].pose);
                Vector3 p2 = get(keys[(i + 1) % count].pose);
                Vector3 p3 = get(keys[(i + 2) % count].pose);
                return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u
                    + (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u);
            }

            if (time <= keys[0].time)
            {
                return get(keys[0].pose);
            }

            for (int k = 0; k + 1 < count; k++)
            {
                if (time <= keys[k + 1].time)
                {
                    float u = (time - keys[k].time) / Mathf.Max(1e-4f, keys[k + 1].time - keys[k].time);
                    u = u * u * u * (u * (6f * u - 15f) + 10f); // smootherstep
                    return Vector3.LerpUnclamped(get(keys[k].pose), get(keys[k + 1].pose), u);
                }
            }

            return get(keys[count - 1].pose);
        }

        private static AnimationCurve ToCurve(float[] values)
        {
            int n = values.Length;
            var keys = new Keyframe[n];
            for (int f = 0; f < n; f++)
            {
                float before = values[Mathf.Max(0, f - 1)];
                float after = values[Mathf.Min(n - 1, f + 1)];
                float span = (Mathf.Min(n - 1, f + 1) - Mathf.Max(0, f - 1)) / Fps;
                float slope = span > 0f ? (after - before) / span : 0f;
                keys[f] = new Keyframe(f / Fps, values[f], slope, slope);
            }

            return new AnimationCurve(keys);
        }

        // ---------------------------------------------------------------- controller

        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
        {
            // Rebuilt inside the same asset: deleting and re-creating it would give it a new GUID
            // and every prefab holding it (Leshy_Model, the boss) would lose its controller.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }
            else
            {
                Clear(controller);
            }

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            foreach (string trigger in new[] { "Slam", "Sweep", "Roar", "Hit" })
            {
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(clips["Idle"], 0f);
            tree.AddChild(clips["Walk"], WalkSpeed);
            machine.defaultState = locomotion;

            AddAction(machine, locomotion, clips["Attack_Slam"], "Slam", 0.35f);
            AddAction(machine, locomotion, clips["Attack_Sweep"], "Sweep", 0.35f);
            AddAction(machine, locomotion, clips["Roar"], "Roar", 0.4f);
            AddAction(machine, locomotion, clips["HitReact"], "Hit", 0.15f);
            return controller;
        }

        private static void Clear(AnimatorController controller)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                controller.RemoveParameter(parameter);
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            {
                machine.RemoveAnyStateTransition(transition);
            }

            foreach (ChildAnimatorState child in machine.states)
            {
                machine.RemoveState(child.state);
            }

            // The old blend tree stays in the file as a sub-asset otherwise.
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            {
                if (sub is BlendTree)
                {
                    AssetDatabase.RemoveObjectFromAsset(sub);
                    Object.DestroyImmediate(sub, true);
                }
            }
        }

        private static void AddAction(AnimatorStateMachine machine, AnimatorState locomotion, AnimationClip clip, string trigger, float blendIn)
        {
            AnimatorState state = machine.AddState(clip.name.Replace("Leshy_", ""));
            state.motion = clip;
            // Blows and roars come from any state (a flinch may still be playing when LeshyBoss
            // starts one - its damage timing assumes the clip starts now); the flinch only
            // from locomotion.
            AnimatorStateTransition enter = trigger == "Hit" ? locomotion.AddTransition(state) : machine.AddAnyStateTransition(state);
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            enter.hasExitTime = false;
            enter.duration = blendIn;
            enter.hasFixedDuration = true;

            AnimatorStateTransition back = state.AddTransition(locomotion);
            back.hasExitTime = true;
            back.exitTime = 0.92f;
            back.duration = 0.5f; // a slow return to stance
            back.hasFixedDuration = true;
        }
    }
}
