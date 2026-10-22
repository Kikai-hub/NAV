using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using NAV.Gameplay.AI;
using NAV.Presentation.Animation;

namespace NAV.Editor
{
    /// <summary>
    /// Puts the ithappy "Animals FREE" models into the creature prefabs in place of the
    /// placeholder capsules: the Tiger stands in for the Forest Wolf (for now), the Deer replaces
    /// the Wild Boar (the boar prefab/definition are renamed to Deer - GUIDs, and so the biome
    /// spawn lists, are kept). Per creature: the root keeps its Creature, NavMeshAgent and
    /// detection trigger (a body hit box replaces the capsule; agent base offset 0, root at the feet), the
    /// capsule mesh is removed, the pack prefab is added as the "Model" child without its own
    /// CharacterController / CreatureMover / MovePlayerInput (the NavMeshAgent moves the
    /// creature), and a CreatureAnimator drives its Animator. Also sets walk/chase speeds that
    /// suit the walk/run clips. Re-run to rebuild in place.
    /// </summary>
    internal static class CreatureModelSetup
    {
        private const string PackPrefabs = "Assets/ithappy/Animals_FREE/Prefabs/";
        private const string CreaturePrefabs = "Assets/NAV/Prefabs/Creatures/";
        private const string CreatureDefinitions = "Assets/NAV/ScriptableObjects/Creatures/";

        private struct Animal
        {
            public string Prefab;
            public string Definition;
            public string DisplayName;
            public string Model;
            public float Scale;
            // Body hit box in root space (root at the feet): half width, length along the forward
            // axis, and top - up to the withers, so a chest-high swing still connects (a capsule
            // around the torso alone sat under the player's punches).
            public float BodyHalfWidth;
            public float BodyLength;
            public float BodyTop;
            // Ground speed (m/s) the walk/run clips show at this scale - the pack's own mover uses
            // 1 and 4 m/s at scale 1.
            public float WalkClipSpeed;
            public float RunClipSpeed;
            public float MoveSpeed;
            public float ChaseMultiplier;
            // Attack reach from the root (the body centre). Its muzzle must end up within the
            // player's weapon reach - at 2.6 the tiger bit from 1.5 m (out of a sword's reach), at
            // 2.0 its muzzle stopped 0.95 m away (a fist reaches ~0.75 m).
            public float AttackRange;
        }

        private static readonly Animal[] Animals =
        {
            new Animal
            {
                Prefab = "ForestWolf", Definition = "ForestWolfCreature", DisplayName = null, Model = "Tiger_001",
                Scale = 0.7f, BodyHalfWidth = 0.35f, BodyLength = 2f, BodyTop = 1.3f,
                WalkClipSpeed = 0.7f, RunClipSpeed = 2.8f,
                MoveSpeed = 1.8f, ChaseMultiplier = 3.5f, AttackRange = 1.7f, // patrol walk, chase ~6.3 m/s (as before)
            },
            new Animal
            {
                Prefab = "WildBoar", Definition = "WildBoarCreature", DisplayName = "Deer", Model = "Deer_001",
                Scale = 1f, BodyHalfWidth = 0.32f, BodyLength = 1.4f, BodyTop = 1.6f,
                WalkClipSpeed = 1f, RunClipSpeed = 4f,
                MoveSpeed = 1.2f, ChaseMultiplier = 5f, AttackRange = 2f, // grazing walk, flees at 6 m/s
            },
        };

        [MenuItem("NAV/Creatures/Apply Animal Models")]
        private static void Apply()
        {
            foreach (Animal animal in Animals)
            {
                string prefabPath = CreaturePrefabs + animal.Prefab + ".prefab";
                string definitionPath = CreatureDefinitions + animal.Definition + ".asset";

                // The boar becomes a deer: rename its files too (same GUIDs, references survive).
                if (animal.DisplayName != null && AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                {
                    AssetDatabase.RenameAsset(prefabPath, animal.DisplayName);
                    AssetDatabase.RenameAsset(definitionPath, animal.DisplayName + "Creature");
                }

                if (animal.DisplayName != null)
                {
                    prefabPath = CreaturePrefabs + animal.DisplayName + ".prefab";
                    definitionPath = CreatureDefinitions + animal.DisplayName + "Creature.asset";
                }

                ConfigureDefinition(definitionPath, animal);
                BuildPrefab(prefabPath, animal);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("CreatureModelSetup: Tiger (Forest Wolf) and Deer models applied.");
        }

        private static void ConfigureDefinition(string path, Animal animal)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CreatureDefinition>(path);
            if (definition == null)
            {
                Debug.LogError($"CreatureModelSetup: no CreatureDefinition at '{path}'.");
                return;
            }

            var serialized = new SerializedObject(definition);
            if (animal.DisplayName != null)
            {
                serialized.FindProperty("_displayName").stringValue = animal.DisplayName;
            }

            serialized.FindProperty("_moveSpeed").floatValue = animal.MoveSpeed;
            serialized.FindProperty("_chaseSpeedMultiplier").floatValue = animal.ChaseMultiplier;
            serialized.FindProperty("_attackRange").floatValue = animal.AttackRange;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void BuildPrefab(string path, Animal animal)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Object.DestroyImmediate(root.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(root.GetComponent<MeshFilter>());
                Transform oldModel = root.transform.Find("Model");
                if (oldModel != null)
                {
                    Object.DestroyImmediate(oldModel.gameObject);
                }

                var source = AssetDatabase.LoadAssetAtPath<GameObject>(PackPrefabs + animal.Model + ".prefab");
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                // The pack's own controller scripts, dependents first: MovePlayerInput requires
                // CreatureMover, which requires the CharacterController.
                Object.DestroyImmediate(model.GetComponent<ithappy.Animals_FREE.MovePlayerInput>());
                Object.DestroyImmediate(model.GetComponent<ithappy.Animals_FREE.CreatureMover>());
                Object.DestroyImmediate(model.GetComponent<CharacterController>());
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * animal.Scale;
                SetLayerRecursively(model.transform, root.layer);

                var animator = model.GetComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                // The placeholder's capsule is replaced by the body box.
                Object.DestroyImmediate(root.GetComponent<CapsuleCollider>());
                var body = root.GetComponent<BoxCollider>();
                if (body == null)
                {
                    body = root.AddComponent<BoxCollider>();
                }

                const float bodyBottom = 0.15f;
                body.size = new Vector3(animal.BodyHalfWidth * 2f, animal.BodyTop - bodyBottom, animal.BodyLength);
                body.center = new Vector3(0f, (animal.BodyTop + bodyBottom) * 0.5f, 0f);

                var agent = root.GetComponent<NavMeshAgent>();
                agent.baseOffset = 0f;
                agent.height = animal.BodyTop;
                // Brakes hard (6 m/s at the old 8 m/s² needed ~2.5 m) and eases in so it stops just
                // inside its attack range instead of sliding into the target.
                agent.acceleration = 20f;
                agent.stoppingDistance = animal.AttackRange * 0.9f;

                var creatureAnimator = root.GetComponent<CreatureAnimator>();
                if (creatureAnimator == null)
                {
                    creatureAnimator = root.AddComponent<CreatureAnimator>();
                }

                var serialized = new SerializedObject(creatureAnimator);
                serialized.FindProperty("_creature").objectReferenceValue = root.GetComponent<Creature>();
                serialized.FindProperty("_animator").objectReferenceValue = animator;
                serialized.FindProperty("_model").objectReferenceValue = model.transform;
                serialized.FindProperty("_walkClipSpeed").floatValue = animal.WalkClipSpeed;
                serialized.FindProperty("_runClipSpeed").floatValue = animal.RunClipSpeed;
                serialized.FindProperty("_playbackSpeedRange").vector2Value = new Vector2(0.6f, 2.4f);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
            {
                SetLayerRecursively(child, layer);
            }
        }
    }
}
