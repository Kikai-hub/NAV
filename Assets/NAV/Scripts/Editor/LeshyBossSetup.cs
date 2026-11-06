using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using NAV.Core.Save;
using NAV.Gameplay.AI;
using NAV.Gameplay.Bosses;
using NAV.Gameplay.Combat;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Items;
using NAV.Presentation.Animation;
using NAV.UI;
using NAV.World.Biomes;
using NAV.World.Encounters;
using NAV.World.Generation;
using NAV.World.TimeOfDay;

namespace NAV.Editor
{
    /// <summary>
    /// NAV > Bosses > Build Leshy Boss - boss 1 as the developer designed it (2026-11-06):
    ///  - Leshy Heartwood (item): his drop; the Stone Pickaxe - the first one - now needs it
    ///    for its haft, so killing him is the way to ore and metal;
    ///  - LeshyCreature (definition): a slow, heavy, wooden hostile creature, no stagger;
    ///  - Prefabs/Bosses/Leshy.prefab: Creature + LeshyBoss + BossActor + LeshyAnimator on
    ///    the Leshy_Model (LeshyModelSetup) - and its ragdoll (Death Rule);
    ///  - ForestWrathSettings: felling trees in the plains/forests angers the forest, he comes by
    ///    chance in the forests and for sure at full wrath;
    ///  - WorldGenTest: a ForestWrath object, SaveManager and the HUD (BossHudController) wired.
    /// Re-runnable (assets keep their GUIDs). Needs the open scene to be WorldGenTest.
    /// </summary>
    internal static class LeshyBossSetup
    {
        private const string ItemFolder = "Assets/NAV/ScriptableObjects/Items/";
        private const string RecipeFolder = "Assets/NAV/ScriptableObjects/Recipes/";
        private const string CreatureFolder = "Assets/NAV/ScriptableObjects/Creatures/";
        private const string BiomeFolder = "Assets/NAV/ScriptableObjects/Biomes/";
        private const string MaterialFolder = "Assets/NAV/Materials/Items/";
        private const string BossFolder = "Assets/NAV/Prefabs/Bosses";
        private const string BossPrefabPath = BossFolder + "/Leshy.prefab";
        private const string DefinitionPath = CreatureFolder + "LeshyCreature.asset";
        private const string SettingsPath = "Assets/NAV/ScriptableObjects/World/ForestWrathSettings.asset";
        private const string DatabasePath = "Assets/NAV/ScriptableObjects/Core/DefaultItemDatabase.asset";
        private const float BossMass = 600f;

        [MenuItem("NAV/Bosses/Build Leshy Boss")]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("LeshyBossSetup: exit Play Mode first.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(LeshyModelSetup.ModelPrefabPath) == null)
            {
                Debug.LogError("LeshyBossSetup: build the model first (NAV > Characters > Build Leshy Model).");
                return;
            }

            // New material variants compile asynchronously - the icon would render empty.
            ItemDefinition heartwood;
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                heartwood = BuildHeartwood();
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncShaders;
            }

            GateStonePickaxe(heartwood);
            CreatureDefinition definition = BuildDefinition(heartwood);
            BuildPrefab(definition);
            RagdollSetup.BuildMixamoBiped(BossPrefabPath, BossMass, RagdollSetup.SharedFleshMaterial());
            ForestWrathSettings settings = BuildSettings();
            WireScene(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("LeshyBossSetup: heartwood, stone pickaxe gate, Leshy prefab, ragdoll, forest wrath and scene wiring done.");
        }

        // ---------------------------------------------------------------- the heartwood

        private static ItemDefinition BuildHeartwood()
        {
            Material wood = Material("LeshyHeartwood_material", new Color(0.62f, 0.43f, 0.2f), 0.25f, Color.black);
            Material bark = Material("LeshyHeartwoodBark_material", new Color(0.3f, 0.24f, 0.16f), 0.1f, Color.black);
            Material sap = Material("LeshyHeartwoodSap_material", new Color(0.45f, 0.95f, 0.35f), 0.7f, new Color(0.35f, 1.1f, 0.25f));

            ItemDefinition item = CreatureLootSetup.BuildItem("LeshyHeartwood", "Leshy Heartwood",
                "Living wood from the Leshy's heart - it still beats with green sap. Ordinary wood splits on stone; " +
                "only a haft of heartwood holds a stone pickaxe head.", 10, 1.2f,
                root =>
                {
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Core", wood, new Vector3(0f, 0.09f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.14f, 0.2f, 0.14f));
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "BarkL", bark, new Vector3(-0.065f, 0.09f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.05f, 0.21f, 0.15f));
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "BarkR", bark, new Vector3(0.065f, 0.09f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.05f, 0.21f, 0.15f));
                    CreatureLootSetup.AddPart(root, PrimitiveType.Sphere, "SapA", sap, new Vector3(0f, 0.17f, 0.12f), Vector3.zero, new Vector3(0.05f, 0.04f, 0.05f));
                    CreatureLootSetup.AddPart(root, PrimitiveType.Sphere, "SapB", sap, new Vector3(0.02f, 0.16f, -0.08f), Vector3.zero, new Vector3(0.035f, 0.03f, 0.035f));
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Twig", bark, new Vector3(0.06f, 0.15f, 0.1f), new Vector3(30f, 0f, -40f), new Vector3(0.02f, 0.07f, 0.02f));
                }, Quaternion.Euler(-25f, 35f, 0f));

            var database = new SerializedObject(AssetDatabase.LoadAssetAtPath<NAV.Core.Save.ItemDatabase>(DatabasePath));
            CreatureLootSetup.AddToList(database, "_items", item);
            return item;
        }

        private static Material Material(string name, Color colour, float smoothness, Color emission)
        {
            string path = MaterialFolder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The Stone Pickaxe - the first pickaxe, the way to every ore - now needs the
        /// heartwood for its haft instead of ordinary wood (developer, 2026-11-06): 3 heartwood +
        /// 3 rock (was 3 wood + 3 rock).</summary>
        private static void GateStonePickaxe(ItemDefinition heartwood)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(RecipeFolder + "StonePickaxeRecipe.asset");
            var wood = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemFolder + "Wood.asset");
            var rock = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemFolder + "Rock.asset");
            if (recipe == null || wood == null || rock == null)
            {
                Debug.LogError("LeshyBossSetup: StonePickaxeRecipe / Wood / Rock not found - the pickaxe is NOT gated.");
                return;
            }

            var serialized = new SerializedObject(recipe);
            SerializedProperty ingredients = serialized.FindProperty("_ingredients");
            ingredients.arraySize = 2;
            SetIngredient(ingredients, 0, heartwood, 3);
            SetIngredient(ingredients, 1, rock, 3);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(recipe);

            var pickaxe = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemFolder + "StonePickaxe.asset");
            if (pickaxe != null)
            {
                var item = new SerializedObject(pickaxe);
                item.FindProperty("_description").stringValue =
                    "A stone pickaxe head on a haft of the Leshy's heartwood - ordinary wood splits on stone. The first tool for ore.";
                item.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pickaxe);
            }
        }

        private static void SetIngredient(SerializedProperty list, int index, ItemDefinition item, int amount)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("_item").objectReferenceValue = item;
            entry.FindPropertyRelative("_amount").intValue = amount;
        }

        // ---------------------------------------------------------------- the creature

        private static CreatureDefinition BuildDefinition(ItemDefinition heartwood)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CreatureDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<CreatureDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            var wood = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemFolder + "Wood.asset");
            var s = new SerializedObject(definition);
            s.FindProperty("_displayName").stringValue = "Leshy";
            s.FindProperty("_isHostile").boolValue = true;
            s.FindProperty("_isUnclean").boolValue = false;
            s.FindProperty("_surface").enumValueIndex = (int)HitSurface.Wood;
            s.FindProperty("_maxHealth").floatValue = 650f;
            s.FindProperty("_moveSpeed").floatValue = 1.1f;       // the walk clip shows ~1.05 m/s
            s.FindProperty("_chaseSpeedMultiplier").floatValue = 1.5f; // 1.65 m/s, 2.1 enraged - a walking player keeps away, not a lazy one
            s.FindProperty("_turnSpeed").floatValue = 55f;        // swings round slowly - get behind him
            s.FindProperty("_detectionRadius").floatValue = 30f;
            s.FindProperty("_viewAngle").floatValue = 200f;
            s.FindProperty("_alwaysNoticeDistance").floatValue = 10f;
            s.FindProperty("_eyeHeight").floatValue = 4f;
            s.FindProperty("_packAlertRadius").floatValue = 0f;
            s.FindProperty("_attackDamage").floatValue = 0f;      // his blows are LeshyBoss's
            s.FindProperty("_attackRange").floatValue = 3f;   // steps in close before he swings
            s.FindProperty("_attackCooldown").floatValue = 1.2f;
            s.FindProperty("_poisonDamage").floatValue = 0f;
            s.FindProperty("_staggerSeconds").floatValue = 0f;    // a tree doesn't stagger
            s.FindProperty("_knockbackDistance").floatValue = 0f;
            s.FindProperty("_patrolRadius").floatValue = 18f;
            s.FindProperty("_patrolWaitSeconds").floatValue = 6f;
            s.FindProperty("_searchWaitSeconds").floatValue = 10f;
            SerializedProperty loot = s.FindProperty("_lootDrops");
            // 3-5 heartwood (developer, 2026-11-06): 3 always, +1 and +1 at even odds - one
            // kill always makes a pickaxe. Plus ordinary wood from his body.
            loot.arraySize = 4;
            SetDrop(loot, 0, heartwood, 3, 1f);
            SetDrop(loot, 1, heartwood, 1, 0.5f);
            SetDrop(loot, 2, heartwood, 1, 0.5f);
            SetDrop(loot, 3, wood, 10, 1f);
            s.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void SetDrop(SerializedProperty drops, int index, ItemDefinition item, int amount, float chance)
        {
            SerializedProperty entry = drops.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("_item").objectReferenceValue = item;
            entry.FindPropertyRelative("_amount").intValue = amount;
            entry.FindPropertyRelative("_chance").floatValue = chance;
        }

        private static void BuildPrefab(CreatureDefinition definition)
        {
            if (!AssetDatabase.IsValidFolder(BossFolder))
            {
                AssetDatabase.CreateFolder("Assets/NAV/Prefabs", "Bosses");
            }

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            GameObject root = existing != null ? PrefabUtility.LoadPrefabContents(BossPrefabPath) : new GameObject("Leshy");
            try
            {
                var agent = GetOrAdd<NavMeshAgent>(root);
                agent.radius = 0.9f;
                agent.height = 4.5f;
                agent.stoppingDistance = 2.4f;
                agent.acceleration = 6f;    // heavy to get going
                agent.angularSpeed = 70f;
                agent.autoBraking = true;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

                GetOrAdd<SphereCollider>(root).isTrigger = true; // detection, sized by Creature
                var creature = GetOrAdd<Creature>(root);
                var leshy = GetOrAdd<LeshyBoss>(root);
                var boss = GetOrAdd<BossActor>(root);
                var body = GetOrAdd<CapsuleCollider>(root);
                body.isTrigger = false;
                body.radius = 0.85f;
                body.height = 4.4f;
                body.center = new Vector3(0f, 2.2f, 0f);

                Set(creature, "_definition", definition);
                SetString(boss, "_title", "Leshy");

                Transform model = root.transform.Find("Model");
                if (model == null)
                {
                    var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LeshyModelSetup.ModelPrefabPath);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, root.transform);
                    instance.name = "Model";
                    model = instance.transform;
                }

                model.localPosition = Vector3.zero;
                model.localRotation = Quaternion.identity;
                var animator = model.GetComponent<Animator>();
                // Set here too: an older null override on the nested model would hide the model's own.
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LeshyModelSetup.Folder + "Leshy.controller");
                animator.applyRootMotion = false;
                var leshyAnimator = GetOrAdd<LeshyAnimator>(root);
                Set(leshyAnimator, "_creature", creature);
                Set(leshyAnimator, "_boss", leshy);
                Set(leshyAnimator, "_animator", animator);
                var speed = new SerializedObject(leshyAnimator);
                speed.FindProperty("_walkClipSpeed").floatValue = LeshyAnimationSetup.WalkSpeed;
                speed.ApplyModifiedPropertiesWithoutUndo();

                // Feet on the real ground (Generic rig - no humanoid foot IK), read in the bind pose.
                var grounding = GetOrAdd<LegGrounding>(root);
                Transform Bone(string bone) => LeshyModelSetup.FindBone(model, bone);
                grounding.EditorConfigure(animator,
                    new[] { Bone("LeftUpLeg"), Bone("LeftLeg"), Bone("LeftFoot") },
                    new[] { Bone("RightUpLeg"), Bone("RightLeg"), Bone("RightFoot") });
                EditorUtility.SetDirty(grounding);

                // Areas where his hands actually land (developer, 2026-11-06: "too long a reach"):
                // the slam's hands come down ~2 m in front, the sweeping arm reaches ~3 m round him.
                var blows = new SerializedObject(leshy);
                SerializedProperty slam = blows.FindProperty("_slam");
                slam.FindPropertyRelative("HitDelay").floatValue = LeshyAnimationSetup.SlamHitTime;
                slam.FindPropertyRelative("Reach").floatValue = 2f;
                slam.FindPropertyRelative("Radius").floatValue = 1.7f;
                SerializedProperty sweep = blows.FindProperty("_sweep");
                sweep.FindPropertyRelative("HitDelay").floatValue = LeshyAnimationSetup.SweepHitTime;
                sweep.FindPropertyRelative("Reach").floatValue = 0f;
                sweep.FindPropertyRelative("Radius").floatValue = 3.1f;
                sweep.FindPropertyRelative("Arc").floatValue = 150f;
                blows.FindProperty("_sweepMaxDistance").floatValue = 2.8f;
                blows.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            }
            finally
            {
                if (existing != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent(out T component) ? component : go.AddComponent<T>();
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(Object target, string field, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- the forest's wrath

        private static ForestWrathSettings BuildSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ForestWrathSettings>(SettingsPath);
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/NAV/ScriptableObjects/World"))
                {
                    AssetDatabase.CreateFolder("Assets/NAV/ScriptableObjects", "World");
                }

                settings = ScriptableObject.CreateInstance<ForestWrathSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            var s = new SerializedObject(settings);
            s.FindProperty("_bossPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            SetBiomes(s.FindProperty("_wrathBiomes"), "PlainsBiome", "ForestBiome", "BirchGroveBiome", "DenseForestBiome");
            SetBiomes(s.FindProperty("_chanceBiomes"), "ForestBiome", "BirchGroveBiome", "DenseForestBiome");
            s.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static void SetBiomes(SerializedProperty list, params string[] names)
        {
            list.arraySize = 0;
            foreach (string biomeName in names)
            {
                var biome = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(BiomeFolder + biomeName + ".asset");
                if (biome == null)
                {
                    Debug.LogWarning($"LeshyBossSetup: biome {biomeName} not found.");
                    continue;
                }

                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = biome;
            }
        }

        private static void WireScene(ForestWrathSettings settings)
        {
            var generator = Object.FindAnyObjectByType<WorldGenerator>(FindObjectsInactive.Include);
            var saveManager = Object.FindAnyObjectByType<SaveManager>(FindObjectsInactive.Include);
            var hud = Object.FindAnyObjectByType<PlayerHudController>(FindObjectsInactive.Include);
            if (generator == null || saveManager == null || hud == null)
            {
                Debug.LogError("LeshyBossSetup: open WorldGenTest (WorldGenerator, SaveManager, PlayerHudController) - the scene is NOT wired.");
                return;
            }

            var wrath = Object.FindAnyObjectByType<ForestWrath>(FindObjectsInactive.Include);
            if (wrath == null)
            {
                var population = Object.FindAnyObjectByType<CreaturePopulation>(FindObjectsInactive.Include);
                var go = new GameObject("ForestWrath");
                go.transform.SetParent(population != null ? population.transform.parent : generator.transform.parent, false);
                wrath = go.AddComponent<ForestWrath>();
            }

            Set(wrath, "_settings", settings);
            Set(wrath, "_worldGenerator", generator);
            Set(wrath, "_clock", Object.FindAnyObjectByType<WorldClock>(FindObjectsInactive.Include));
            Set(saveManager, "_forestWrath", wrath);

            var bossHud = GetOrAdd<BossHudController>(hud.gameObject);
            Set(bossHud, "_forestWrath", wrath);

            EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            EditorSceneManager.SaveScene(generator.gameObject.scene);
        }
    }
}
