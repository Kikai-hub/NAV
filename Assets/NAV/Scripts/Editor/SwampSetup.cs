using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.AI;
using NAV.Gameplay.Items;
using NAV.World.Biomes;

namespace NAV.Editor
{
    /// <summary>
    /// Tier 4 - the Swamp (Топь, PROGRESSION_TIERS_v0.1.md), NAV > World > Build Swamp (Tier 4).
    /// The hybrid world already makes the Swamp in island lowlands (HybridWorldSettings height
    /// rule); this fills it:
    ///   - bare dead trees without leaves (сухостой, primitives, 3 shapes - any axe breaks them into
    ///     wood, a stump stays), sunken black logs - Bog Wood, the resinous
    ///     swamp timber that does not rot (a bronze axe at least), some resin;
    ///   - bog iron lives only here now (taken off the plains, forest, dense forest and shallows);
    ///   - reeds with cattails, mossy hummocks, mushrooms; thick green-grey fog; water trickling by
    ///     day, crickets at night;
    ///   - the Bog Lurker: a placeholder poisonous swamp beast (the ithappy dog, tinted) whose bite
    ///     poisons (PlayerPoison) - more of them at night.
    /// Models are placeholders (primitives / tinted pack models) - see MODELS_FOR_3D_ARTIST_v0.1.md.
    /// Safe to re-run: densities are absolute, assets are rebuilt in place.
    /// </summary>
    internal static class SwampSetup
    {
        private const string BiomeFolder = "Assets/NAV/ScriptableObjects/Biomes/";
        private const string SwampBiomePath = BiomeFolder + "SwampBiome.asset";
        private const string WorldPrefabs = "Assets/NAV/Prefabs/World/";
        private const string VegetationFolder = WorldPrefabs + "Vegetation/";
        private const string SwampPrefabFolder = WorldPrefabs + "Swamp";
        private const string MaterialFolder = "Assets/NAV/Materials/Swamp";
        private const string ItemFolder = "Assets/NAV/ScriptableObjects/Items/";
        private const string DatabasePath = "Assets/NAV/ScriptableObjects/Core/DefaultItemDatabase.asset";
        private const string TreeNodePath = "Assets/NAV/Scripts/Gameplay/Items/TreeWoodNode.asset";
        private const string CreaturePrefabs = "Assets/NAV/Prefabs/Creatures/";
        private const string CreatureDefinitions = "Assets/NAV/ScriptableObjects/Creatures/";
        private const string AudioFolder = "Assets/Nature - Essentials/";


        [MenuItem("NAV/World/Build Swamp (Tier 4)")]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("SwampSetup: exit Play Mode first.");
                return;
            }

            var swamp = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(SwampBiomePath);
            GameObject spruce = Load(WorldPrefabs + "UNS_Spruce_WoodNode.prefab");
            GameObject larch = Load(VegetationFolder + "HP_Larch_Tree_WoodNode.prefab");
            GameObject bogIron = Load(WorldPrefabs + "BogIron_Rock.prefab");
            GameObject branch = Load(WorldPrefabs + "LooseBranch.prefab");
            GameObject stone = Load(WorldPrefabs + "LooseStone.prefab");
            GameObject mushrooms = Load("Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Vegetation/Mushrooms/Prefabs/UNS_Mushroom_Patch.prefab");
            if (swamp == null || spruce == null || larch == null || bogIron == null)
            {
                Debug.LogError("SwampSetup: run NAV > World > Hybrid World > Create Assets, the vegetation and metallurgy setups first (needs SwampBiome, the spruce and larch nodes, BogIron_Rock).");
                return;
            }

            EnsureFolder(SwampPrefabFolder);
            EnsureFolder(MaterialFolder);

            // ---- Trees, timber, plants ----
            RemoveOldDeadTrees(swamp);
            ResourceNodeDefinition deadNode = BuildDeadTreeDefinition();
            GameObject deadTreeA = BuildBareTree("DeadTree_A", deadNode, 1, 7.5f, 0.24f, 10);
            GameObject deadTreeB = BuildBareTree("DeadTree_B", deadNode, 2, 9f, 0.28f, 8);
            GameObject deadTreeC = BuildBareTree("DeadTree_C", deadNode, 3, 5.5f, 0.2f, 11);
            ItemDefinition bogWood = BuildBogWoodItem();
            GameObject sunkenLog = BuildSunkenLog(bogWood);
            GameObject reeds = BuildReeds();
            GameObject hummock = BuildHummock();

            // ---- The biome ----
            ConfigureBiome(swamp);
            var anyHeight = new Vector2(-1000f, 1000f);
            var wetGround = new Vector2(-0.4f, 3f);
            MetallurgySetup.UpsertResource(swamp.name, deadTreeA, 0.18f, 9f, new Vector2(0.8f, 1.25f), new Vector2(0.2f, 1000f));
            MetallurgySetup.UpsertResource(swamp.name, deadTreeB, 0.12f, 11f, new Vector2(0.8f, 1.2f), new Vector2(0.2f, 1000f));
            MetallurgySetup.UpsertResource(swamp.name, deadTreeC, 0.16f, 8f, new Vector2(0.7f, 1.3f), new Vector2(-0.2f, 1000f));
            MetallurgySetup.UpsertResource(swamp.name, spruce, 0.06f, 12f, new Vector2(0.5f, 0.8f), new Vector2(1f, 1000f));
            MetallurgySetup.UpsertResource(swamp.name, sunkenLog, 0.14f, 14f, new Vector2(0.9f, 1.3f), wetGround);
            MetallurgySetup.UpsertResource(swamp.name, bogIron, 0.2f, 12f, new Vector2(0.7f, 1.15f), wetGround);
            if (branch != null)
            {
                MetallurgySetup.UpsertResource(swamp.name, branch, 0.12f, 12f, Vector2.one, anyHeight);
            }

            if (stone != null)
            {
                MetallurgySetup.UpsertResource(swamp.name, stone, 0.03f, 20f, Vector2.one, anyHeight);
            }

            var decor = new SerializedObject(swamp);
            UpsertDecor(decor, reeds, 0.55f, 4f, new Vector2(0.8f, 1.3f));
            UpsertDecor(decor, hummock, 0.3f, 5f, new Vector2(0.7f, 1.4f));
            if (mushrooms != null)
            {
                UpsertDecor(decor, mushrooms, 0.08f, 12f, new Vector2(0.8f, 1.2f));
            }

            decor.ApplyModifiedPropertiesWithoutUndo();

            // Bog iron lives only in the swamp now.
            foreach (string biome in new[] { "PlainsBiome", "ForestBiome", "DenseForestBiome", "OceanBiome" })
            {
                RemoveResource(biome, bogIron);
            }

            // ---- The Bog Lurker ----
            GameObject lurker = BuildBogLurker();
            SetCreatures(swamp, lurker);

            EditorUtility.SetDirty(swamp);
            AssetDatabase.SaveAssets();
            Debug.Log("SwampSetup: Swamp biome filled - dead trees, sunken logs (Bog Wood), reeds, hummocks, bog iron (moved here), fog and sound, Bog Lurker (poison).");
        }

        private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private static Material Mat(string name, Color colour, float smoothness = 0.1f)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Drops the first pass's recoloured pack trees (needles and all) from the swamp
        /// and deletes them - the bare dead trees below replace them.</summary>
        private static void RemoveOldDeadTrees(BiomeDefinition swamp)
        {
            foreach (string name in new[] { "DeadSpruce_WoodNode", "DeadLarch_WoodNode" })
            {
                string path = $"{SwampPrefabFolder}/{name}.prefab";
                GameObject old = Load(path);
                if (old == null)
                {
                    continue;
                }

                RemoveResource(swamp.name, old);
                AssetDatabase.DeleteAsset(path);
            }

            foreach (string guid in AssetDatabase.FindAssets("Dead_ t:Material", new[] { MaterialFolder }))
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            }
        }

        /// <summary>Standing dead wood (сухостой): no falling trunk - a few axe hits (any axe)
        /// and it breaks down into wood, leaving a stump.</summary>
        private static ResourceNodeDefinition BuildDeadTreeDefinition()
        {
            ResourceNodeDefinition definition = TreeFellingSetup.GetOrCreateDefinition("DeadTreeNode");
            var serialized = new SerializedObject(definition);
            TreeFellingSetup.Configure(serialized, "Dead Tree", 4, null, Load(WorldPrefabs + "Felling/SpruceStump.prefab"), 0f);
            serialized = new SerializedObject(definition);
            serialized.FindProperty("_minToolTier").intValue = 1;
            ItemDefinition wood = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemFolder + "Wood.asset");
            SerializedProperty drops = serialized.FindProperty("_drops");
            drops.arraySize = 2;
            SetDrop(drops, 0, wood, 3, 1f);
            SetDrop(drops, 1, wood, 2, 0.5f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        /// <summary>A leafless dead tree from primitives: a leaning, tapering grey trunk in a few
        /// crooked segments with a snapped-off top, and bare branches (some forked) reaching up and
        /// out. seed varies the shape; height and girth are the base size (the biome scales it).</summary>
        private static GameObject BuildBareTree(string name, ResourceNodeDefinition node, int seed, float height, float radius, int branches)
        {
            Material bark = Mat("DeadBark_material", new Color(0.4f, 0.37f, 0.33f));
            Material wood = Mat("DeadWood_material", new Color(0.58f, 0.54f, 0.47f));
            var random = new System.Random(seed * 7919);
            var root = new GameObject(name);

            // Trunk: 3 segments, each leaning a little more, thinner towards the top.
            const int segments = 3;
            Vector3 start = Vector3.zero;
            Vector3 direction = Vector3.up;
            float segmentLength = height / segments;
            var trunkPoints = new List<(Vector3 point, Vector3 direction, float radius)>();
            for (int i = 0; i < segments; i++)
            {
                direction = (direction + new Vector3(Signed(random) * 0.18f, 0f, Signed(random) * 0.18f)).normalized;
                float r = radius * Mathf.Lerp(1f, 0.35f, (float)i / segments);
                Vector3 end = start + direction * segmentLength;
                Limb(root.transform, "Trunk", bark, start, end, r, r * 0.8f);
                trunkPoints.Add((start, direction, r));
                start = end;
            }

            // A snapped-off top: a short pale splinter.
            Limb(root.transform, "BrokenTop", wood, start, start + direction * 0.35f + new Vector3(0.08f, 0f, 0f), radius * 0.28f, radius * 0.05f);

            // Bare branches from the upper two thirds of the trunk.
            for (int b = 0; b < branches; b++)
            {
                float along = Mathf.Lerp(0.3f, 0.95f, (float)random.NextDouble());
                int segment = Mathf.Min(segments - 1, Mathf.FloorToInt(along * segments));
                (Vector3 point, Vector3 dir, float r) = trunkPoints[segment];
                Vector3 from = point + dir * (along * segments - segment) * segmentLength;
                float angle = (b * 137.5f + seed * 40f) * Mathf.Deg2Rad;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 branchDirection = (outward + Vector3.up * Mathf.Lerp(0.3f, 1.1f, (float)random.NextDouble())).normalized;
                float length = height * Mathf.Lerp(0.28f, 0.48f, (float)random.NextDouble()) * (1.25f - along * 0.55f);
                float branchRadius = r * 0.42f;
                Vector3 to = from + branchDirection * length;
                Limb(root.transform, "Branch", bark, from, to, branchRadius, branchRadius * 0.3f);

                // Most branches fork once near the end.
                if (random.NextDouble() < 0.85)
                {
                    Vector3 forkFrom = Vector3.Lerp(from, to, 0.6f);
                    Vector3 forkDirection = (branchDirection + new Vector3(Signed(random) * 0.8f, 0.4f, Signed(random) * 0.8f)).normalized;
                    Limb(root.transform, "Twig", bark, forkFrom, forkFrom + forkDirection * length * 0.45f, branchRadius * 0.45f, branchRadius * 0.12f);
                }
            }

            // Only the trunk's foot blocks: branches overhead never stop the player.
            var collider = root.AddComponent<CapsuleCollider>();
            collider.radius = radius * 1.1f;
            collider.height = height * 0.7f;
            collider.center = new Vector3(0f, height * 0.35f, 0f);
            TreeFellingSetup.AddNode(root, node, null);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{SwampPrefabFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static float Signed(System.Random random) => (float)random.NextDouble() * 2f - 1f;

        /// <summary>A tapering limb between two points: a cylinder from a to b (radius at a), with
        /// a thinner cylinder over its outer half so it narrows towards b.</summary>
        private static void Limb(Transform parent, string name, Material material, Vector3 a, Vector3 b, float radiusA, float radiusB)
        {
            Vector3 middle = Vector3.Lerp(a, b, 0.5f);
            Part(parent, name, material, a, middle, radiusA);
            Part(parent, name, material, middle, b, Mathf.Lerp(radiusA, radiusB, 0.6f));
        }

        private static void Part(Transform parent, string name, Material material, Vector3 a, Vector3 b, float radius)
        {
            Vector3 offset = b - a;
            if (offset.sqrMagnitude < 0.0001f)
            {
                return;
            }

            CreatureLootSetup.AddPart(parent, PrimitiveType.Cylinder, name, material, (a + b) * 0.5f,
                Quaternion.FromToRotation(Vector3.up, offset.normalized).eulerAngles,
                new Vector3(radius * 2f, offset.magnitude * 0.5f, radius * 2f));
        }

        private static ItemDefinition BuildBogWoodItem()
        {
            Material bark = Mat("BogWood_material", new Color(0.16f, 0.12f, 0.09f), 0.35f);
            Material core = Mat("BogWoodCore_material", new Color(0.32f, 0.2f, 0.1f), 0.5f);
            ItemDefinition item = CreatureLootSetup.BuildItem("BogWood", "Bog Wood",
                "Black, resin-soaked timber from logs sunk in the bog - hard and it never rots. For iron-age building and hafts.",
                50, 1.2f,
                root =>
                {
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Log", bark, new Vector3(0f, 0.08f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.16f, 0.25f, 0.16f));
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Cut", core, new Vector3(0.251f, 0.08f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.14f, 0.002f, 0.14f));
                },
                Quaternion.Euler(0f, 30f, 10f));

            var serialized = new SerializedObject(item);
            // Resinous wood still floats, low in the water.
            serialized.FindProperty("_density").floatValue = 0.9f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var database = new SerializedObject(AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath));
            CreatureLootSetup.AddToList(database, "_items", item);
            return item;
        }

        /// <summary>A black log half sunk in the bog: chopped (bronze axe at least) for Bog Wood and
        /// a little resin.</summary>
        private static GameObject BuildSunkenLog(ItemDefinition bogWood)
        {
            ResourceNodeDefinition definition = TreeFellingSetup.GetOrCreateDefinition("SunkenLogNode");
            var serialized = new SerializedObject(definition);
            TreeFellingSetup.Configure(serialized, "Sunken Log", 4, null, null, 0f);
            serialized = new SerializedObject(definition);
            serialized.FindProperty("_shrinkOnHit").boolValue = true;
            serialized.FindProperty("_minToolTier").intValue = 3;
            ItemDefinition resin = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemFolder + "Resin.asset");
            SerializedProperty drops = serialized.FindProperty("_drops");
            drops.arraySize = resin != null ? 3 : 2;
            SetDrop(drops, 0, bogWood, 2, 1f);
            SetDrop(drops, 1, bogWood, 1, 0.5f);
            if (resin != null)
            {
                SetDrop(drops, 2, resin, 1, 0.35f);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            var root = new GameObject("SunkenLog");
            // Low and askew, one end deeper in the mud.
            GameObject log = TreeFellingSetup.AddLogVisual(root.transform, "Log", 3.2f, 0.28f, new Vector3(0f, 0.05f, 0f), Quaternion.Euler(0f, 0f, 6f));
            Material black = Mat("SunkenLog_material", new Color(0.13f, 0.11f, 0.09f), 0.4f);
            foreach (Renderer renderer in log.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial = black;
            }

            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 0;
            collider.radius = 0.28f;
            collider.height = 3.2f;
            collider.center = new Vector3(0f, 0.1f, 0f);
            TreeFellingSetup.AddNode(root, definition, null);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{SwampPrefabFolder}/SunkenLog.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void SetDrop(SerializedProperty drops, int index, ItemDefinition item, int amount, float chance)
        {
            SerializedProperty drop = drops.GetArrayElementAtIndex(index);
            drop.FindPropertyRelative("_item").objectReferenceValue = item;
            drop.FindPropertyRelative("_amount").intValue = amount;
            drop.FindPropertyRelative("_chance").floatValue = chance;
        }

        /// <summary>A clump of reeds with a few cattail heads (decor, walk-through).</summary>
        private static GameObject BuildReeds()
        {
            Material stem = Mat("Reed_material", new Color(0.42f, 0.46f, 0.24f));
            Material dry = Mat("ReedDry_material", new Color(0.58f, 0.52f, 0.32f));
            Material cattail = Mat("Cattail_material", new Color(0.3f, 0.18f, 0.1f));
            var root = new GameObject("Reeds");
            var random = new System.Random(7);
            for (int i = 0; i < 14; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = (float)random.NextDouble() * 0.5f;
                float height = 1.1f + (float)random.NextDouble() * 0.9f;
                var position = new Vector3(Mathf.Cos(angle) * distance, height * 0.5f, Mathf.Sin(angle) * distance);
                var lean = new Vector3((float)random.NextDouble() * 12f - 6f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 12f - 6f);
                CreatureLootSetup.AddPart(root.transform, PrimitiveType.Cylinder, "Stem", i % 4 == 0 ? dry : stem, position, lean, new Vector3(0.025f, height * 0.5f, 0.025f));
                if (i % 3 == 0)
                {
                    var head = position + Vector3.up * (height * 0.42f);
                    CreatureLootSetup.AddPart(root.transform, PrimitiveType.Capsule, "Cattail", cattail, head, lean, new Vector3(0.06f, 0.12f, 0.06f));
                }
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{SwampPrefabFolder}/Reeds.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>A mossy tussock (кочка) with a tuft of sedge on top (decor).</summary>
        private static GameObject BuildHummock()
        {
            Material moss = Mat("Hummock_material", new Color(0.3f, 0.36f, 0.16f));
            Material sedge = Mat("Sedge_material", new Color(0.5f, 0.52f, 0.26f));
            var root = new GameObject("Hummock");
            CreatureLootSetup.AddPart(root.transform, PrimitiveType.Sphere, "Mound", moss, new Vector3(0f, 0.05f, 0f), Vector3.zero, new Vector3(0.9f, 0.4f, 0.8f));
            for (int i = 0; i < 7; i++)
            {
                float angle = i * Mathf.PI * 2f / 7f;
                var position = new Vector3(Mathf.Cos(angle) * 0.15f, 0.42f, Mathf.Sin(angle) * 0.15f);
                CreatureLootSetup.AddPart(root.transform, PrimitiveType.Cylinder, "Sedge", sedge, position, new Vector3(Mathf.Sin(angle) * 20f, 0f, Mathf.Cos(angle) * 20f), new Vector3(0.02f, 0.22f, 0.02f));
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{SwampPrefabFolder}/Hummock.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void ConfigureBiome(BiomeDefinition swamp)
        {
            var serialized = new SerializedObject(swamp);
            serialized.FindProperty("_displayName").stringValue = "Swamp";
            serialized.FindProperty("_heightVariation").floatValue = 0.1f;
            serialized.FindProperty("_grassDensity").floatValue = 0.55f;
            serialized.FindProperty("_flowerDensity").floatValue = 0f;
            // Thick green-grey murk: you see ~80 m.
            serialized.FindProperty("_fogDensity").floatValue = 0.026f;
            serialized.FindProperty("_fogColor").colorValue = new Color(0.36f, 0.42f, 0.3f, 0.85f);
            serialized.FindProperty("_dayAmbience").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "Ambiance_Stream_Calm_Loop_Stereo.wav");
            serialized.FindProperty("_nightAmbience").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "Ambiance_Night_Loop_Stereo.wav");
            serialized.FindProperty("_ambienceVolume").floatValue = 0.45f;
            serialized.FindProperty("_windAmbience").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "Ambiance_Wind_Calm_Loop_Stereo.wav");
            serialized.FindProperty("_windVolume").floatValue = 0.15f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpsertDecor(SerializedObject biome, GameObject prefab, float density, float spacing, Vector2 scale)
        {
            SerializedProperty list = biome.FindProperty("_decorSpawns");
            SerializedProperty entry = null;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("_prefab").objectReferenceValue == prefab)
                {
                    entry = list.GetArrayElementAtIndex(i);
                }
            }

            if (entry == null)
            {
                list.arraySize++;
                entry = list.GetArrayElementAtIndex(list.arraySize - 1);
            }

            entry.FindPropertyRelative("_prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("_density").floatValue = density;
            entry.FindPropertyRelative("_minSpacing").floatValue = spacing;
            entry.FindPropertyRelative("_scale").vector2Value = scale;
            entry.FindPropertyRelative("_keepColliders").boolValue = false;
        }

        private static void RemoveResource(string biomeName, GameObject prefab)
        {
            var biome = AssetDatabase.LoadAssetAtPath<BiomeDefinition>($"{BiomeFolder}{biomeName}.asset");
            if (biome == null)
            {
                return;
            }

            var serialized = new SerializedObject(biome);
            SerializedProperty list = serialized.FindProperty("_resourceSpawns");
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("_prefab").objectReferenceValue == prefab)
                {
                    list.DeleteArrayElementAtIndex(i);
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(biome);
        }

        /// <summary>The Bog Lurker: a copy of the wolf's prefab and definition with the ithappy dog
        /// as its (tinted) model; slower than a wolf, weaker bite, but a poisonous one.</summary>
        private static GameObject BuildBogLurker()
        {
            string prefabPath = CreaturePrefabs + "BogLurker.prefab";
            string definitionPath = CreatureDefinitions + "BogLurkerCreature.asset";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                AssetDatabase.CopyAsset(CreaturePrefabs + "ForestWolf.prefab", prefabPath);
            }

            if (AssetDatabase.LoadAssetAtPath<CreatureDefinition>(definitionPath) == null)
            {
                AssetDatabase.CopyAsset(CreatureDefinitions + "ForestWolfCreature.asset", definitionPath);
            }

            var animal = new CreatureModelSetup.Animal
            {
                Prefab = "BogLurker", Definition = "BogLurkerCreature", DisplayName = null, Model = "Dog_001",
                Scale = 1.05f, BodyHalfWidth = 0.3f, BodyLength = 1.3f, BodyTop = 1f,
                WalkClipSpeed = 1.05f, RunClipSpeed = 4.2f,
                MoveSpeed = 1.4f, ChaseMultiplier = 3.4f, AttackRange = 1.5f, // creeps, chases ~4.8 m/s (a wolf ~6.3)
            };
            CreatureModelSetup.ConfigureDefinition(definitionPath, animal);
            CreatureModelSetup.BuildPrefab(prefabPath, animal);

            var definition = AssetDatabase.LoadAssetAtPath<CreatureDefinition>(definitionPath);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_displayName").stringValue = "Bog Lurker";
            serialized.FindProperty("_isHostile").boolValue = true;
            // Нечисть: silver hurts it more.
            serialized.FindProperty("_isUnclean").boolValue = true;
            serialized.FindProperty("_maxHealth").floatValue = 50f;
            serialized.FindProperty("_attackDamage").floatValue = 8f;
            serialized.FindProperty("_attackCooldown").floatValue = 1.7f;
            serialized.FindProperty("_poisonDamage").floatValue = 24f;
            serialized.FindProperty("_poisonSeconds").floatValue = 8f;
            serialized.FindProperty("_detectionRadius").floatValue = 10f;
            serialized.FindProperty("_packAlertRadius").floatValue = 12f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            // Murky green-brown hide, set on the prefab's model.
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                root.name = "BogLurker";
                var creature = root.GetComponent<Creature>();
                var creatureSerialized = new SerializedObject(creature);
                creatureSerialized.FindProperty("_definition").objectReferenceValue = definition;
                creatureSerialized.ApplyModifiedPropertiesWithoutUndo();

                Transform model = root.transform.Find("Model");
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i] = LurkerSkin(materials[i]);
                    }

                    renderer.sharedMaterials = materials;
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static Material LurkerSkin(Material source)
        {
            if (source == null)
            {
                return null;
            }

            string path = $"{MaterialFolder}/Lurker_{source.name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = $"Lurker_{source.name}" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.CopyPropertiesFromMaterial(source);
            string colourProperty = source.HasProperty("_BaseColor") ? "_BaseColor" : source.HasProperty("_Color") ? "_Color" : null;
            if (colourProperty != null)
            {
                material.SetColor(colourProperty, new Color(0.34f, 0.4f, 0.22f));
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetCreatures(BiomeDefinition swamp, GameObject lurker)
        {
            var serialized = new SerializedObject(swamp);
            SerializedProperty list = serialized.FindProperty("_creatureSpawns");
            list.arraySize = 1;
            SerializedProperty entry = list.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("_prefab").objectReferenceValue = lurker;
            entry.FindPropertyRelative("_count").intValue = 3;
            entry.FindPropertyRelative("_nightCount").intValue = 6;
            entry.FindPropertyRelative("_groupSize").vector2IntValue = new Vector2Int(1, 2);
            entry.FindPropertyRelative("_respawnSeconds").floatValue = 120f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
