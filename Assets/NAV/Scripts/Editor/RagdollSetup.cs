using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Gameplay.AI;
using NAV.Gameplay.Interaction;
using NAV.Gameplay.Player;
using NAV.Presentation.Animation;

namespace NAV.Editor
{
    /// <summary>
    /// NAV > Animation > Build Ragdolls - the death rule (CLAUDE.md "Death Rule"): every creature
    /// prefab and the player's character get a Ragdoll.
    ///   - Parts: the humanoid hero by its avatar (hips, chest, head, upper/lower arms and legs);
    ///     the ithappy animals (rigify-style names) by their skeleton - pelvis (the thighs'
    ///     parent), a middle back, the chest (where the front legs hang), neck, head (the jaw's
    ///     parent / the skull), the first tail bone, thigh + shin of every leg. Bones without a
    ///     part (shoulders, feet, toes, ears) ride rigidly on the part above them.
    ///   - Shapes: each part's capsule is fitted to the skinned vertices that bone (and its
    ///     part-less children) carries - principal axis, the 5-95% span along it, the 70th
    ///     percentile radius - so a leg is as thick as the leg. Mass is split by capsule volume.
    ///   - Joints: CharacterJoints with limits by role (spine stiff, knees/elbows bend far).
    ///   - Wiring: CreatureAnimator._ragdoll; for the player a PlayerRagdoll on the character
    ///     (PlayerDeath -> ragdoll, respawn -> back). The "Ragdoll" layer is created and left out
    ///     of the interaction ray (a corpse never hides loot); corpses stay 20 s.
    /// New creature prefabs go into Creatures below. Re-runnable; saves the scene.
    /// </summary>
    internal static class RagdollSetup
    {
        private const string CreatureFolder = "Assets/NAV/Prefabs/Creatures/";
        private const string MaterialPath = "Assets/NAV/Materials/RagdollFlesh.physicMaterial";
        private const string LayerName = "Ragdoll";
        private const float CorpseSeconds = 20f;

        // Prefab -> total body mass (kg).
        private static readonly (string prefab, float mass)[] Creatures =
        {
            ("ForestWolf", 45f),
            ("Deer", 70f),
            ("BogLurker", 35f),
            ("SnowLeopard", 50f),
            ("Volkolak", 95f),
        };

        // Bipeds on a Mixamo skeleton read by bone names (Generic rigs - no humanoid avatar):
        // prefab path -> total body mass (kg). The model is the root's "Model" child.
        private static readonly (string prefab, float mass)[] MixamoBipeds =
        {
            ("Assets/NAV/Prefabs/Bosses/Leshy.prefab", 600f),
        };

        private const float HeroMass = 75f;

        private enum Role { Root, Spine, Neck, Head, Tail, UpperLimb, LowerLimb }

        private static readonly Dictionary<Role, (float twist, float swing)> Limits = new()
        {
            { Role.Root, (0f, 0f) },
            { Role.Spine, (12f, 18f) },
            { Role.Neck, (20f, 30f) },
            { Role.Head, (25f, 30f) },
            { Role.Tail, (25f, 40f) },
            { Role.UpperLimb, (25f, 60f) },
            { Role.LowerLimb, (5f, 75f) },
        };

        [MenuItem("NAV/Animation/Build Ragdolls")]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("RagdollSetup: exit Play Mode first.");
                return;
            }

            int layer = EnsureLayer(LayerName);
            PhysicsMaterial material = FleshMaterial();

            int built = 0;
            foreach ((string prefabName, float mass) in Creatures)
            {
                built += BuildCreature(CreatureFolder + prefabName + ".prefab", mass, material) ? 1 : 0;
            }

            foreach ((string prefabPath, float mass) in MixamoBipeds)
            {
                built += BuildMixamoBiped(prefabPath, mass, material) ? 1 : 0;
            }

            bool hero = BuildHero(material);
            ExcludeFromInteraction(layer);
            AssetDatabase.SaveAssets();
            Debug.Log($"RagdollSetup: {built}/{Creatures.Length + MixamoBipeds.Length} creature ragdolls, hero ragdoll {(hero ? "built" : "NOT built")}, layer '{LayerName}' = {layer}.");
        }

        // ---- Creatures ----

        private static bool BuildCreature(string path, float mass, PhysicsMaterial material)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning($"RagdollSetup: {path} not found - skipped.");
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var animatorComponent = root.GetComponent<CreatureAnimator>();
                Transform model = root.transform.Find("Model");
                Animator animator = model != null ? model.GetComponent<Animator>() : null;
                SkinnedMeshRenderer skin = model != null ? model.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                if (animatorComponent == null || animator == null || skin == null)
                {
                    Debug.LogWarning($"RagdollSetup: {path} has no CreatureAnimator / Model Animator / skinned mesh - skipped.");
                    return false;
                }

                List<(Transform bone, Role role)> picked = PickQuadrupedParts(skin);
                if (picked == null)
                {
                    Debug.LogWarning($"RagdollSetup: couldn't read {path}'s skeleton (no thigh.L/front_thigh.L) - skipped.");
                    return false;
                }

                List<RagdollPart> parts = Fit(skin, picked, mass);
                Ragdoll ragdoll = model.GetComponent<Ragdoll>() ?? model.gameObject.AddComponent<Ragdoll>();
                Collider hitBox = root.GetComponent<BoxCollider>();
                ragdoll.EditorConfigure(animator, parts, new Behaviour[0], hitBox != null ? new[] { hitBox } : new Collider[0], material);

                var serialized = new SerializedObject(animatorComponent);
                serialized.FindProperty("_ragdoll").objectReferenceValue = ragdoll;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var creature = new SerializedObject(root.GetComponent<Creature>());
                creature.FindProperty("_despawnDelaySeconds").floatValue = CorpseSeconds;
                creature.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"RagdollSetup: {System.IO.Path.GetFileNameWithoutExtension(path)} - {parts.Count} parts: {string.Join(", ", parts.Select(p => p.Bone.name))}.");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The ithappy (rigify-style) animal skeleton read by names and structure.</summary>
        private static List<(Transform, Role)> PickQuadrupedParts(SkinnedMeshRenderer skin)
        {
            Transform rigRoot = skin.rootBone != null ? skin.rootBone : skin.transform;
            while (rigRoot.parent != null && rigRoot.parent != skin.transform.parent)
            {
                rigRoot = rigRoot.parent;
            }

            Transform Find(string name) => rigRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            Transform thighL = Find("thigh.L");
            Transform frontThighL = Find("front_thigh.L");
            if (thighL == null || frontThighL == null)
            {
                return null;
            }

            // Pelvis: the first spine bone above the hind leg (the dog rig hangs it from a
            // "shoulder" bone first).
            Transform pelvis = thighL.parent;
            while (pelvis != null && !pelvis.name.StartsWith("spine"))
            {
                pelvis = pelvis.parent;
            }

            if (pelvis == null)
            {
                return null;
            }

            // Chest: the first ancestor of the front leg that is itself under the pelvis.
            Transform chest = frontThighL.parent;
            while (chest != null && !chest.IsChildOf(pelvis))
            {
                chest = chest.parent;
            }

            while (chest != null && chest.parent != null && !chest.name.StartsWith("spine"))
            {
                chest = chest.parent;
            }

            if (chest == null || chest == pelvis)
            {
                return null;
            }

            Transform jaw = Find("jaw");
            Transform head = Find("scull") ?? Find("skull") ?? Find("head") ?? (jaw != null ? jaw.parent : null);

            var parts = new List<(Transform, Role)> { (pelvis, Role.Root) };
            Transform mid = Middle(PathBetween(pelvis, chest));
            if (mid != null)
            {
                parts.Add((mid, Role.Spine));
            }

            parts.Add((chest, Role.Spine));
            if (head != null && head.IsChildOf(chest) && head != chest)
            {
                Transform neck = Middle(PathBetween(chest, head));
                if (neck != null)
                {
                    parts.Add((neck, Role.Neck));
                }

                parts.Add((head, Role.Head));
            }

            // Tail: the pelvis's child that leads neither to the chest nor to a leg.
            foreach (Transform child in pelvis)
            {
                if (chest.IsChildOf(child) || child.childCount == 0
                    || child.GetComponentsInChildren<Transform>(true).Any(t => t.name.Contains("thigh")))
                {
                    continue;
                }

                parts.Add((child, Role.Tail));
                break;
            }

            foreach (string side in new[] { "L", "R" })
            {
                foreach (string prefix in new[] { "", "front_" })
                {
                    Transform thigh = Find($"{prefix}thigh.{side}");
                    Transform shin = Find($"{prefix}shin.{side}");
                    if (thigh != null)
                    {
                        parts.Add((thigh, Role.UpperLimb));
                    }

                    if (shin != null)
                    {
                        parts.Add((shin, Role.LowerLimb));
                    }
                }
            }

            return parts;
        }

        /// <summary>The bones strictly between an ancestor and a descendant, top first.</summary>
        private static List<Transform> PathBetween(Transform ancestor, Transform descendant)
        {
            var path = new List<Transform>();
            for (Transform t = descendant.parent; t != null && t != ancestor; t = t.parent)
            {
                path.Insert(0, t);
            }

            return path;
        }

        private static Transform Middle(List<Transform> bones) => bones.Count == 0 ? null : bones[(bones.Count - 1) / 2];

        /// <summary>A biped with a Mixamo skeleton ("mixamorig:Hips" ...) on a Generic rig - the
        /// Leshy. Same eleven parts as the hero. Any component on the prefab with a "_ragdoll"
        /// field (LeshyAnimator) gets the ragdoll.</summary>
        internal static bool BuildMixamoBiped(string path, float mass, PhysicsMaterial material)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning($"RagdollSetup: {path} not found - skipped.");
                return false;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform model = root.transform.Find("Model");
                Animator animator = model != null ? model.GetComponentInChildren<Animator>() : null;
                SkinnedMeshRenderer skin = model != null ? model.GetComponentInChildren<SkinnedMeshRenderer>() : null;
                if (animator == null || skin == null)
                {
                    Debug.LogWarning($"RagdollSetup: {path} has no Model with an Animator and a skinned mesh - skipped.");
                    return false;
                }

                Transform Find(string bone) => model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "mixamorig:" + bone);
                var bones = new (string bone, Role role)[]
                {
                    ("Hips", Role.Root), ("Spine2", Role.Spine), ("Head", Role.Head),
                    ("LeftArm", Role.UpperLimb), ("LeftForeArm", Role.LowerLimb), ("RightArm", Role.UpperLimb), ("RightForeArm", Role.LowerLimb),
                    ("LeftUpLeg", Role.UpperLimb), ("LeftLeg", Role.LowerLimb), ("RightUpLeg", Role.UpperLimb), ("RightLeg", Role.LowerLimb),
                };
                var picked = new List<(Transform, Role)>();
                foreach ((string bone, Role role) in bones)
                {
                    Transform t = Find(bone);
                    if (t == null)
                    {
                        Debug.LogWarning($"RagdollSetup: {path} has no mixamorig:{bone} - skipped.");
                        return false;
                    }

                    picked.Add((t, role));
                }

                // Fitted in the bind pose (the prefab's own T-pose).
                List<RagdollPart> parts = Fit(skin, picked, mass);
                Ragdoll ragdoll = animator.GetComponent<Ragdoll>() ?? animator.gameObject.AddComponent<Ragdoll>();
                Collider[] bodyColliders = root.GetComponents<Collider>().Where(c => !c.isTrigger).ToArray();
                ragdoll.EditorConfigure(animator, parts, new Behaviour[0], bodyColliders, material);

                foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    var serialized = new SerializedObject(behaviour);
                    SerializedProperty field = serialized.FindProperty("_ragdoll");
                    if (field != null && field.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        field.objectReferenceValue = ragdoll;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                Creature creature = root.GetComponent<Creature>();
                if (creature != null)
                {
                    var serialized = new SerializedObject(creature);
                    serialized.FindProperty("_despawnDelaySeconds").floatValue = CorpseSeconds * 2f; // a boss lies longer
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"RagdollSetup: {System.IO.Path.GetFileNameWithoutExtension(path)} - {parts.Count} parts, {mass} kg.");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>For other setups: the shared ragdoll material.</summary>
        internal static PhysicsMaterial SharedFleshMaterial()
        {
            EnsureLayer(LayerName);
            return FleshMaterial();
        }

        // ---- The hero ----

        private static bool BuildHero(PhysicsMaterial material)
        {
            PlayerDeath death = Object.FindAnyObjectByType<PlayerDeath>(FindObjectsInactive.Include);
            Animator character = null;
            if (death != null)
            {
                foreach (Animator candidate in death.GetComponentsInChildren<Animator>(true))
                {
                    if (candidate.isHuman)
                    {
                        character = candidate;
                    }
                }
            }

            GameObject source = character != null ? PrefabUtility.GetCorrespondingObjectFromSource(character.gameObject) : null;
            if (character == null || source == null)
            {
                Debug.LogWarning("RagdollSetup: no player (PlayerDeath + a humanoid model instance) in the open scene - hero ragdoll not built.");
                return false;
            }

            // Fitted on the model asset in its bind pose (the scene pose can be any frame), then
            // mapped onto the scene character's bones by path.
            GameObject model = Object.Instantiate(source);
            model.hideFlags = HideFlags.HideAndDontSave;
            List<RagdollPart> parts;
            try
            {
                var animator = model.GetComponent<Animator>();
                var bones = new (HumanBodyBones bone, Role role)[]
                {
                    (HumanBodyBones.Hips, Role.Root), (HumanBodyBones.Chest, Role.Spine), (HumanBodyBones.Head, Role.Head),
                    (HumanBodyBones.LeftUpperArm, Role.UpperLimb), (HumanBodyBones.LeftLowerArm, Role.LowerLimb),
                    (HumanBodyBones.RightUpperArm, Role.UpperLimb), (HumanBodyBones.RightLowerArm, Role.LowerLimb),
                    (HumanBodyBones.LeftUpperLeg, Role.UpperLimb), (HumanBodyBones.LeftLowerLeg, Role.LowerLimb),
                    (HumanBodyBones.RightUpperLeg, Role.UpperLimb), (HumanBodyBones.RightLowerLeg, Role.LowerLimb),
                };
                var picked = new List<(Transform, Role)>();
                foreach ((HumanBodyBones bone, Role role) in bones)
                {
                    Transform t = animator.GetBoneTransform(bone);
                    if (t == null && bone == HumanBodyBones.Chest)
                    {
                        t = animator.GetBoneTransform(HumanBodyBones.Spine);
                    }

                    if (t == null)
                    {
                        Debug.LogWarning($"RagdollSetup: the hero model has no {bone} - hero ragdoll not built.");
                        return false;
                    }

                    picked.Add((t, role));
                }

                parts = Fit(model.GetComponentInChildren<SkinnedMeshRenderer>(), picked, HeroMass);
                foreach (RagdollPart part in parts)
                {
                    string path = AnimationUtility.CalculateTransformPath(part.Bone, model.transform);
                    part.Bone = character.transform.Find(path);
                    if (part.Bone == null)
                    {
                        Debug.LogWarning($"RagdollSetup: the scene hero has no bone '{path}' - hero ragdoll not built.");
                        return false;
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(model);
            }

            Ragdoll ragdoll = character.GetComponent<Ragdoll>() ?? character.gameObject.AddComponent<Ragdoll>();
            var posers = new List<Behaviour>();
            foreach (Behaviour behaviour in new Behaviour[] { character.GetComponent<PlayerAnimator>(), character.GetComponent<HandGrip>(), character.GetComponent<FootGrounding>() })
            {
                if (behaviour != null)
                {
                    posers.Add(behaviour);
                }
            }

            var controller = death.GetComponent<CharacterController>();
            Undo.RecordObject(ragdoll, "Build Ragdoll");
            ragdoll.EditorConfigure(character, parts, posers.ToArray(), controller != null ? new Collider[] { controller } : new Collider[0], material);
            EditorUtility.SetDirty(ragdoll);

            PlayerRagdoll driver = character.GetComponent<PlayerRagdoll>() ?? character.gameObject.AddComponent<PlayerRagdoll>();
            var serialized = new SerializedObject(driver);
            serialized.FindProperty("_ragdoll").objectReferenceValue = ragdoll;
            serialized.FindProperty("_death").objectReferenceValue = death;
            serialized.FindProperty("_motor").objectReferenceValue = death.GetComponent<PlayerMotor>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(character.gameObject.scene);
            EditorSceneManager.SaveScene(character.gameObject.scene);
            return true;
        }

        // ---- Fitting ----

        /// <summary>Capsules fitted to each part's share of the skinned mesh (in the bone's own
        /// space), parents linked (nearest part above), mass split by volume; root first.</summary>
        private static List<RagdollPart> Fit(SkinnedMeshRenderer skin, List<(Transform bone, Role role)> picked, float totalMass)
        {
            // Parents before children.
            picked = picked.OrderBy(p => Depth(p.bone)).ToList();
            var partOf = new Dictionary<Transform, int>();
            for (int i = 0; i < picked.Count; i++)
            {
                partOf[picked[i].bone] = i;
            }

            var baked = new Mesh();
            skin.BakeMesh(baked, false);
            Vector3[] vertices = baked.vertices;
            BoneWeight[] weights = skin.sharedMesh.boneWeights;
            Transform[] bones = skin.bones;
            var points = new List<Vector3>[picked.Count];
            for (int i = 0; i < picked.Count; i++)
            {
                points[i] = new List<Vector3>();
            }

            var ownerCache = new Dictionary<Transform, int>();
            for (int v = 0; v < vertices.Length && v < weights.Length; v++)
            {
                BoneWeight w = weights[v];
                int boneIndex = w.boneIndex0;
                float best = w.weight0;
                if (w.weight1 > best) { best = w.weight1; boneIndex = w.boneIndex1; }
                if (w.weight2 > best) { best = w.weight2; boneIndex = w.boneIndex2; }
                if (w.weight3 > best) { boneIndex = w.boneIndex3; }
                Transform bone = boneIndex < bones.Length ? bones[boneIndex] : null;
                if (bone == null)
                {
                    continue;
                }

                if (!ownerCache.TryGetValue(bone, out int owner))
                {
                    owner = 0;
                    for (Transform t = bone; t != null; t = t.parent)
                    {
                        if (partOf.TryGetValue(t, out int index))
                        {
                            owner = index;
                            break;
                        }
                    }

                    ownerCache[bone] = owner;
                }

                points[owner].Add(skin.transform.TransformPoint(vertices[v]));
            }

            Object.DestroyImmediate(baked);

            var parts = new List<RagdollPart>();
            var volumes = new List<float>();
            for (int i = 0; i < picked.Count; i++)
            {
                (Transform bone, Role role) = picked[i];
                var part = new RagdollPart { Bone = bone, Parent = -1 };
                for (Transform t = bone.parent; t != null; t = t.parent)
                {
                    if (partOf.TryGetValue(t, out int index))
                    {
                        part.Parent = index;
                        break;
                    }
                }

                FitCapsule(part, points[i]);
                (part.TwistLimit, part.SwingLimit) = Limits[role];
                float scale = bone.lossyScale.x;
                float radius = part.Radius * scale;
                float height = part.Height * scale;
                volumes.Add(Mathf.PI * radius * radius * Mathf.Max(height - 2f * radius, 0f) + 4f / 3f * Mathf.PI * radius * radius * radius);
                parts.Add(part);
            }

            float total = Mathf.Max(1e-6f, volumes.Sum());
            for (int i = 0; i < parts.Count; i++)
            {
                parts[i].Mass = Mathf.Max(0.5f, totalMass * volumes[i] / total);
            }

            return parts;
        }

        private static int Depth(Transform t)
        {
            int depth = 0;
            for (; t.parent != null; t = t.parent)
            {
                depth++;
            }

            return depth;
        }

        /// <summary>Capsule in the bone's space around world points: the bone axis nearest the
        /// points' principal direction, 5-95% span along it, 70th-percentile radius. Too few
        /// points: a capsule from the bone to its first child.</summary>
        private static void FitCapsule(RagdollPart part, List<Vector3> worldPoints)
        {
            Transform bone = part.Bone;
            if (worldPoints.Count < 12)
            {
                Vector3 toChild = bone.childCount > 0 ? bone.GetChild(0).localPosition : Vector3.up * 0.1f;
                int axis = DominantAxis(toChild);
                float length = Mathf.Max(0.02f, toChild.magnitude);
                part.Direction = axis;
                part.Center = toChild * 0.5f;
                part.Radius = length * 0.18f;
                part.Height = length;
                return;
            }

            var local = worldPoints.Select(p => bone.InverseTransformPoint(p)).ToList();
            Vector3 mean = local.Aggregate(Vector3.zero, (a, b) => a + b) / local.Count;
            // Principal direction by power iteration on the covariance.
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (Vector3 p in local)
            {
                Vector3 d = p - mean;
                xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z; yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
            }

            Vector3 principal = Vector3.one.normalized;
            for (int i = 0; i < 24; i++)
            {
                principal = new Vector3(
                    xx * principal.x + xy * principal.y + xz * principal.z,
                    xy * principal.x + yy * principal.y + yz * principal.z,
                    xz * principal.x + yz * principal.y + zz * principal.z).normalized;
            }

            int dir = DominantAxis(principal);
            int a1 = (dir + 1) % 3;
            int a2 = (dir + 2) % 3;
            List<float> along = local.Select(p => p[dir]).OrderBy(x => x).ToList();
            float lo = along[(int)(along.Count * 0.05f)];
            float hi = along[Mathf.Min(along.Count - 1, (int)(along.Count * 0.95f))];
            float c1 = local.Average(p => p[a1]);
            float c2 = local.Average(p => p[a2]);
            List<float> radial = local.Select(p => Mathf.Sqrt((p[a1] - c1) * (p[a1] - c1) + (p[a2] - c2) * (p[a2] - c2))).OrderBy(x => x).ToList();
            float radius = radial[(int)(radial.Count * 0.7f)];
            float height = Mathf.Max(hi - lo, 0.01f);
            radius = Mathf.Clamp(radius, 0.005f, Mathf.Max(0.005f, height * 0.5f));

            var center = new Vector3();
            center[dir] = (lo + hi) * 0.5f;
            center[a1] = c1;
            center[a2] = c2;
            part.Direction = dir;
            part.Center = center;
            part.Radius = radius;
            part.Height = Mathf.Max(height, radius * 2f);
        }

        private static int DominantAxis(Vector3 v)
        {
            float x = Mathf.Abs(v.x), y = Mathf.Abs(v.y), z = Mathf.Abs(v.z);
            return x >= y && x >= z ? 0 : y >= z ? 1 : 2;
        }

        // ---- Project settings ----

        private static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0)
            {
                return existing;
            }

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = name;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            }

            Debug.LogError("RagdollSetup: no free layer slot for 'Ragdoll' - corpses stay on their own layers.");
            return -1;
        }

        private static PhysicsMaterial FleshMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MaterialPath);
            if (material == null)
            {
                material = new PhysicsMaterial("RagdollFlesh");
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            // A body doesn't bounce and doesn't slide far.
            material.dynamicFriction = 0.7f;
            material.staticFriction = 0.9f;
            material.bounciness = 0f;
            material.frictionCombine = PhysicsMaterialCombine.Average;
            material.bounceCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The interaction ray passes through corpses (the loot lies under them).</summary>
        private static void ExcludeFromInteraction(int layer)
        {
            if (layer < 0)
            {
                return;
            }

            PlayerInteractor interactor = Object.FindAnyObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
            if (interactor == null)
            {
                return;
            }

            var serialized = new SerializedObject(interactor);
            SerializedProperty mask = serialized.FindProperty("_interactableLayers");
            if (mask == null)
            {
                return;
            }

            mask.intValue &= ~(1 << layer);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(interactor.gameObject.scene);
            EditorSceneManager.SaveScene(interactor.gameObject.scene);
        }
    }
}
