using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Presentation.Animation;
using NAV.Presentation.Equipment;

namespace NAV.Editor
{
    /// <summary>
    /// NAV > Animation > Setup Hero Hand Grip: adds HandGrip to the character (the object with
    /// HeldItemView), stores the open-hand rest rotations of both index chains from the model
    /// asset, and moves the B-handProp.R/.L sockets into the middle of the ring the curled
    /// fingers make (a circle fitted through the second and third finger joints and the tip, in
    /// the hand's own up/forward plane - the grip runs along the hand's right axis). Re-run
    /// after changing HandGrip's curl. Saves the scene.
    /// </summary>
    internal static class HandGripSetup
    {
        [MenuItem("NAV/Animation/Setup Hero Hand Grip")]
        private static void Setup()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("HandGripSetup: exit Play Mode first.");
                return;
            }

            HeldItemView view = Object.FindAnyObjectByType<HeldItemView>(FindObjectsInactive.Include);
            Animator character = view != null ? view.GetComponent<Animator>() : null;
            GameObject source = character != null ? PrefabUtility.GetCorrespondingObjectFromSource(character.gameObject) : null;
            if (character == null || !character.isHuman || source == null)
            {
                Debug.LogError("HandGripSetup: no humanoid character with HeldItemView (an instance of its model) in the open scene.");
                return;
            }

            HandGrip grip = character.GetComponent<HandGrip>() ?? character.gameObject.AddComponent<HandGrip>();
            Vector3 curl = new SerializedObject(grip).FindProperty("_curl").vector3Value;

            // The model asset in its own (open, T-pose) pose: rest rotations and the ring centres.
            GameObject model = Object.Instantiate(source);
            model.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var animator = model.GetComponent<Animator>();
                Quaternion[] rightRest = Rest(animator, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal);
                Quaternion[] leftRest = Rest(animator, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal);
                if (rightRest == null || leftRest == null)
                {
                    Debug.LogError("HandGripSetup: the model has no index finger chains (Proximal/Intermediate/Distal) in its avatar.");
                    return;
                }

                Undo.RecordObject(grip, "Setup Hand Grip");
                grip.EditorSetRest(rightRest, leftRest);
                EditorUtility.SetDirty(grip);

                PlaceSocket(character, animator, true, curl);
                PlaceSocket(character, animator, false, curl);
                grip.EditorSetSockets(
                    character.GetBoneTransform(HumanBodyBones.RightHand).Find("B-handProp.R"),
                    character.GetBoneTransform(HumanBodyBones.LeftHand).Find("B-handProp.L"));
            }
            finally
            {
                Object.DestroyImmediate(model);
            }

            EditorSceneManager.MarkSceneDirty(character.gameObject.scene);
            EditorSceneManager.SaveScene(character.gameObject.scene);
            Debug.Log($"HandGripSetup: HandGrip on '{character.name}' (curl {curl}), hand sockets moved into the grip. Scene saved.");
        }

        private static Quaternion[] Rest(Animator animator, HumanBodyBones a, HumanBodyBones b, HumanBodyBones c)
        {
            Transform first = animator.GetBoneTransform(a);
            Transform second = animator.GetBoneTransform(b);
            Transform third = animator.GetBoneTransform(c);
            return first != null && second != null && third != null
                ? new[] { first.localRotation, second.localRotation, third.localRotation }
                : null;
        }

        /// <summary>Curls the model's chain as HandGrip does, fits the ring and moves the scene
        /// character's socket (B-handProp.R/.L under that hand) to its centre - its position
        /// across the hand (x) and its rotation stay.</summary>
        private static void PlaceSocket(Animator character, Animator model, bool right, Vector3 curl)
        {
            Transform hand = model.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Transform b1 = model.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            Transform b2 = model.GetBoneTransform(right ? HumanBodyBones.RightIndexIntermediate : HumanBodyBones.LeftIndexIntermediate);
            Transform b3 = model.GetBoneTransform(right ? HumanBodyBones.RightIndexDistal : HumanBodyBones.LeftIndexDistal);
            if (b3.childCount == 0)
            {
                Debug.LogWarning($"HandGripSetup: the {(right ? "right" : "left")} finger chain has no end bone - socket left where it was.");
                return;
            }

            Vector3 axis = hand.right;
            b1.rotation = Quaternion.AngleAxis(curl.x, axis) * b1.rotation;
            b2.rotation = Quaternion.AngleAxis(curl.y, axis) * b2.rotation;
            b3.rotation = Quaternion.AngleAxis(curl.z, axis) * b3.rotation;
            Vector3 p1 = hand.InverseTransformPoint(b2.position);
            Vector3 p2 = hand.InverseTransformPoint(b3.position);
            Vector3 p3 = hand.InverseTransformPoint(b3.GetChild(0).position);
            Vector2 centre = Circumcentre(new Vector2(p1.y, p1.z), new Vector2(p2.y, p2.z), new Vector2(p3.y, p3.z));

            Transform sceneHand = character.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Transform socket = sceneHand.Find(right ? "B-handProp.R" : "B-handProp.L");
            if (socket == null)
            {
                Debug.LogWarning($"HandGripSetup: no {(right ? "B-handProp.R" : "B-handProp.L")} under {sceneHand.name} - socket not moved.");
                return;
            }

            Undo.RecordObject(socket, "Setup Hand Grip");
            socket.localPosition = new Vector3(socket.localPosition.x, centre.x, centre.y);
        }

        private static Vector2 Circumcentre(Vector2 a, Vector2 b, Vector2 c)
        {
            float d = 2f * (a.x * (b.y - c.y) + b.x * (c.y - a.y) + c.x * (a.y - b.y));
            if (Mathf.Abs(d) < 1e-8f)
            {
                return (a + b + c) / 3f;
            }

            float a2 = a.sqrMagnitude;
            float b2 = b.sqrMagnitude;
            float c2 = c.sqrMagnitude;
            return new Vector2(
                (a2 * (b.y - c.y) + b2 * (c.y - a.y) + c2 * (a.y - b.y)) / d,
                (a2 * (c.x - b.x) + b2 * (a.x - c.x) + c2 * (b.x - a.x)) / d);
        }
    }
}
