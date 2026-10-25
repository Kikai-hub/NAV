using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.Building;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Items;
using NAV.Gameplay.Survival;
using NAV.World.Biomes;

namespace NAV.Editor
{
    /// <summary>
    /// Stage C, first step - raw materials and their processing:
    ///   - items: Copper Ore, Bog Iron Ore, Charcoal, Copper Bar, Iron Bar, Leather, Bone Glue,
    ///     Tin Ore, Tin Bar, Bronze Bar (placeholder primitive pickups + icons, registered in
    ///     DefaultItemDatabase; bronze is alloyed at the forge - SmithingSetup);
    ///   - ore in the world: CopperOre_Rock (rocks with copper veins on plains and forest hills),
    ///     TinOre_Rock (coastal rocks, copper pickaxe) and BogIron_Rock (rusty lumps on shores,
    ///     just above the water), as biome resources;
    ///   - three building pieces (need a workbench nearby), each a ProcessingStation:
    ///     Charcoal Kiln (wood -> charcoal), Bloomery (ore + charcoal -> bars) and Tanning Rack
    ///     (hide -> leather);
    ///   - bone glue boiled on the campfire (Campfire's CookingStation gets Bone -> Bone Glue).
    /// Re-run to rebuild in place (assets keep their GUIDs). Save the scene afterwards (the
    /// open scene's PlayerBuilding learns the new pieces).
    /// </summary>
    internal static class MetallurgySetup
    {
        private const string ItemFolder = "Assets/NAV/ScriptableObjects/Items";
        private const string BuildPrefabFolder = "Assets/NAV/Prefabs/Build";
        private const string WorldPrefabFolder = "Assets/NAV/Prefabs/World";
        private const string PieceFolder = "Assets/NAV/ScriptableObjects/Building";
        private const string NodeFolder = "Assets/NAV/ScriptableObjects/World";
        private const string MaterialFolder = "Assets/NAV/Materials/Items";
        private const string IconFolder = "Assets/NAV/Art/Icons";
        private const string ItemDatabasePath = "Assets/NAV/ScriptableObjects/Core/DefaultItemDatabase.asset";
        private const string PieceDatabasePath = "Assets/NAV/ScriptableObjects/Core/BuildingPieceDatabase.asset";
        private const string FlintRockPath = "Assets/NAV/Prefabs/World/Flint_Ore_Rock_01.prefab";
        private const string FlintDepositPath = "Assets/NAV/Scripts/Gameplay/Items/FlintDeposit.asset";
        private const string ShardMeshPath = "Assets/NAV/Art/Models/ImpactShard.asset";
        private const string CampfirePrefabPath = "Assets/NAV/Prefabs/Build/Campfire.prefab";

        private static Mesh _shard;

        [MenuItem("NAV/Build/Build Ore, Smelting and Tanning (Stage C)")]
        private static void Build()
        {
            _shard = AssetDatabase.LoadAssetAtPath<Mesh>(ShardMeshPath);
            if (_shard == null)
            {
                Debug.LogError("MetallurgySetup: run NAV/Setup/Add Hit Feedback To Scene first (it builds the shard mesh this uses).");
                return;
            }

            Material copper = Mat("Copper_material", new Color(0.78f, 0.45f, 0.24f), 0.55f, 0.75f);
            Material patina = Mat("CopperPatina_material", new Color(0.32f, 0.62f, 0.52f), 0.2f, 0.1f);
            Material rust = Mat("BogIron_material", new Color(0.52f, 0.27f, 0.16f), 0.15f, 0.2f);
            Material iron = Mat("Iron_material", new Color(0.36f, 0.36f, 0.38f), 0.45f, 0.8f);
            Material charcoalMat = Mat("Charcoal_material", new Color(0.08f, 0.08f, 0.08f), 0.25f, 0f);
            Material leatherMat = Mat("Leather_material", new Color(0.55f, 0.36f, 0.2f), 0.3f, 0f);
            Material clay = Mat("Clay_material", new Color(0.62f, 0.42f, 0.3f), 0.05f, 0f);
            Material glue = Mat("BoneGlue_material", new Color(0.85f, 0.62f, 0.22f), 0.7f, 0f);
            Material tin = Mat("Tin_material", new Color(0.74f, 0.75f, 0.77f), 0.6f, 0.75f);
            Material cassiterite = Mat("Cassiterite_material", new Color(0.2f, 0.15f, 0.12f), 0.3f, 0.2f);
            Material bronze = Mat("Bronze_material", new Color(0.72f, 0.5f, 0.24f), 0.6f, 0.8f);
            Material hide = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/Hide_material.mat");
            Material woodMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/NAV/Materials/Workbench_material.mat");
            Material stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/NAV/Materials/CampfireStone_material.mat");
            Material fire = AssetDatabase.LoadAssetAtPath<Material>("Assets/NAV/Materials/Fire_material.mat");
            Material fireCore = AssetDatabase.LoadAssetAtPath<Material>("Assets/NAV/Materials/FireCore_material.mat");

            // ---- Items ----
            Quaternion lumpPose = Quaternion.Euler(-20f, 30f, 0f);
            ItemDefinition copperOre = CreatureLootSetup.BuildItem("CopperOre", "Copper Ore", "Rock streaked with green and copper. Smelt it in a bloomery with charcoal.",
                30, 1f, root =>
                {
                    Shard(root, stone, new Vector3(0f, 0.08f, 0f), new Vector3(0.3f, 0.24f, 0.28f), 0f);
                    Shard(root, copper, new Vector3(0.07f, 0.12f, 0.06f), new Vector3(0.12f, 0.1f, 0.12f), 40f);
                    Shard(root, patina, new Vector3(-0.08f, 0.11f, -0.04f), new Vector3(0.1f, 0.08f, 0.1f), 110f);
                }, lumpPose);
            ItemDefinition bogIronOre = CreatureLootSetup.BuildItem("BogIronOre", "Bog Iron Ore", "Rusty lumps dug from wet shores - iron for the bloomery.",
                30, 1f, root =>
                {
                    Shard(root, rust, new Vector3(0f, 0.08f, 0f), new Vector3(0.28f, 0.2f, 0.26f), 15f);
                    Shard(root, rust, new Vector3(0.1f, 0.06f, 0.05f), new Vector3(0.14f, 0.12f, 0.14f), 80f);
                }, lumpPose);
            ItemDefinition charcoal = CreatureLootSetup.BuildItem("Charcoal", "Charcoal", "Wood burned slowly without air. Hot fuel for smelting.",
                50, 0.3f, root =>
                {
                    Shard(root, charcoalMat, new Vector3(0f, 0.05f, 0f), new Vector3(0.2f, 0.12f, 0.14f), 0f);
                    Shard(root, charcoalMat, new Vector3(0.08f, 0.05f, 0.05f), new Vector3(0.12f, 0.09f, 0.1f), 70f);
                }, lumpPose);
            ItemDefinition copperBar = CreatureLootSetup.BuildItem("CopperBar", "Copper Bar", "Smelted copper, ready for the smith.",
                30, 1f, root => Ingot(root, copper), Quaternion.Euler(-25f, 30f, 0f));
            ItemDefinition ironBar = CreatureLootSetup.BuildItem("IronBar", "Iron Bar", "A worked bloom of iron - stronger than bronze.",
                30, 1.2f, root => Ingot(root, iron), Quaternion.Euler(-25f, 30f, 0f));
            // Tier 3 (bronze): tin from coastal rocks, alloyed with copper at the forge (SmithingSetup).
            ItemDefinition tinOre = CreatureLootSetup.BuildItem("TinOre", "Tin Ore", "Dark tinstone in pale rock. Smelt it into tin - copper's partner in bronze.",
                30, 1f, root =>
                {
                    Shard(root, stone, new Vector3(0f, 0.08f, 0f), new Vector3(0.3f, 0.22f, 0.28f), 20f);
                    Shard(root, cassiterite, new Vector3(0.07f, 0.12f, 0.05f), new Vector3(0.12f, 0.1f, 0.12f), 60f);
                    Shard(root, tin, new Vector3(-0.07f, 0.11f, -0.05f), new Vector3(0.09f, 0.07f, 0.09f), 130f);
                }, lumpPose);
            ItemDefinition tinBar = CreatureLootSetup.BuildItem("TinBar", "Tin Bar", "Soft white tin. Two parts copper to one of tin make bronze.",
                30, 1f, root => Ingot(root, tin), Quaternion.Euler(-25f, 30f, 0f));
            ItemDefinition bronzeBar = CreatureLootSetup.BuildItem("BronzeBar", "Bronze Bar", "Copper hardened with tin - the smith's first true alloy.",
                30, 1.1f, root => Ingot(root, bronze), Quaternion.Euler(-25f, 30f, 0f));
            ItemDefinition leather = CreatureLootSetup.BuildItem("Leather", "Leather", "A tanned hide, soft and tough. For armour, grips and straps.",
                30, 0.4f, root =>
                {
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Sheet", leatherMat, new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(0.36f, 0.03f, 0.28f));
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Fold", leatherMat, new Vector3(0f, 0.05f, 0.07f), new Vector3(-12f, 0f, 0f), new Vector3(0.34f, 0.03f, 0.14f));
                }, Quaternion.Euler(-35f, 30f, 0f));
            ItemDefinition boneGlue = CreatureLootSetup.BuildItem("BoneGlue", "Bone Glue", "Bones boiled down to a strong glue. Binds bows and layered armour.",
                30, 0.3f, root =>
                {
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Pot", clay, new Vector3(0f, 0.08f, 0f), Vector3.zero, new Vector3(0.16f, 0.08f, 0.16f));
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Glue", glue, new Vector3(0f, 0.155f, 0f), Vector3.zero, new Vector3(0.13f, 0.01f, 0.13f));
                }, Quaternion.Euler(-25f, 0f, 0f));

            var database = new SerializedObject(AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath));
            foreach (ItemDefinition item in new[] { copperOre, bogIronOre, charcoal, copperBar, ironBar, leather, boneGlue, tinOre, tinBar, bronzeBar })
            {
                CreatureLootSetup.AddToList(database, "_items", item);
            }

            ItemDefinition wood = Item("Wood");
            ItemDefinition rock = Item("Rock");
            ItemDefinition flint = Item("Flint");
            ItemDefinition hideItem = Item("Hide");
            ItemDefinition bone = Item("Bone");

            // ---- Ore rocks ----
            GameObject copperRock = BuildOreRock("CopperOre_Rock", "CopperDeposit", "Copper Vein", 5,
                new[] { (copperOre, 3, 1f), (copperOre, 1, 0.5f), (rock, 2, 1f) }, null, new[] { copper, patina });
            GameObject bogIronRock = BuildOreRock("BogIron_Rock", "BogIronDeposit", "Bog Iron", 4,
                new[] { (bogIronOre, 2, 1f), (bogIronOre, 1, 0.5f), (rock, 1, 1f) }, rust, new[] { rust });

            // Tin: rocks on the coast, a little above the water. Its own biome (sea islands, tier 3 -
            // PROGRESSION_TIERS_v0.1.md) doesn't exist yet, so for now it sits on the shores of the
            // plains, forests and ocean flats. Needs a copper pickaxe.
            GameObject tinRock = BuildOreRock("TinOre_Rock", "TinDeposit", "Tin Vein", 5,
                new[] { (tinOre, 2, 1f), (tinOre, 1, 0.5f), (rock, 2, 1f) }, null, new[] { cassiterite, tin });
            SetMinToolTier("TinDeposit", 2);
            UpsertResource("PlainsBiome", tinRock, 0.04f, 20f, new Vector2(0.8f, 1.15f), new Vector2(0.8f, 4f));
            UpsertResource("ForestBiome", tinRock, 0.04f, 20f, new Vector2(0.8f, 1.15f), new Vector2(0.8f, 4f));
            UpsertResource("OceanBiome", tinRock, 0.08f, 16f, new Vector2(0.8f, 1.15f), new Vector2(0.6f, 3f));

            // Copper lives only in the dense forest (tier 2) - DenseForestSetup places it there
            // (this used to put it on the plains and forest hills).
            UpsertResource("OceanBiome", bogIronRock, 0.18f, 12f, new Vector2(0.7f, 1.1f), new Vector2(0.1f, 1.8f));
            UpsertResource("PlainsBiome", bogIronRock, 0.12f, 14f, new Vector2(0.7f, 1.1f), new Vector2(0.1f, 1.5f));
            UpsertResource("ForestBiome", bogIronRock, 0.12f, 14f, new Vector2(0.7f, 1.1f), new Vector2(0.1f, 1.5f));

            // ---- Stations ----
            BuildingPieceDefinition kiln = BuildStation("CharcoalKiln", "Charcoal Kiln",
                new[] { (rock, 10), (wood, 6) },
                new[] { Recipe(wood, 1, charcoal, 1, 10f) }, null, 0, 20, 0,
                (root, working, loaded) => BuildKiln(root, working, loaded, clay, stone, woodMaterial, fire, fireCore));
            BuildingPieceDefinition bloomery = BuildStation("Bloomery", "Bloomery",
                new[] { (rock, 20), (wood, 4), (flint, 2) },
                new[] { Recipe(copperOre, 1, copperBar, 1, 20f), Recipe(tinOre, 1, tinBar, 1, 20f), Recipe(bogIronOre, 1, ironBar, 1, 30f) }, charcoal, 2, 10, 20,
                (root, working, loaded) => BuildBloomery(root, working, loaded, clay, stone, fire, fireCore, rust));
            BuildingPieceDefinition tanning = BuildStation("TanningRack", "Tanning Rack",
                new[] { (wood, 8), (flint, 1) },
                new[] { Recipe(hideItem, 1, leather, 1, 30f) }, null, 0, 5, 0,
                (root, working, loaded) => BuildTanningRack(root, loaded, woodMaterial, hide != null ? hide : leatherMat, working));

            // The kiln and the bloomery are made of starting materials (rock, wood, flint), so they
            // also wait for the first ore - nothing to smelt before it.
            foreach (BuildingPieceDefinition furnace in new[] { kiln, bloomery })
            {
                var furnaceSerialized = new SerializedObject(furnace);
                SerializedProperty unlock = furnaceSerialized.FindProperty("_unlockItems");
                unlock.arraySize = 1;
                unlock.GetArrayElementAtIndex(0).objectReferenceValue = copperOre;
                furnaceSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(furnace);
            }

            // The tanning rack is also the workbench's first upgrade (level 2), as in Valheim.
            MakeExtension(tanning.Prefab, AssetDatabase.LoadAssetAtPath<WorkbenchDefinition>("Assets/NAV/ScriptableObjects/Workbenches/BasicWorkbench.asset"), "TanningRack");

            var pieces = new SerializedObject(AssetDatabase.LoadAssetAtPath<BuildingPieceDatabase>(PieceDatabasePath));
            PlayerBuilding building = Object.FindAnyObjectByType<PlayerBuilding>(FindObjectsInactive.Include);
            foreach (BuildingPieceDefinition piece in new[] { kiln, bloomery, tanning })
            {
                CreatureLootSetup.AddToList(pieces, "_pieces", piece);
                if (building != null)
                {
                    CreatureLootSetup.AddToList(new SerializedObject(building), "_knownPieces", piece);
                }
            }

            if (building != null)
            {
                EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
            }

            AddBoneGlueToCampfire(bone, boneGlue);

            AssetDatabase.SaveAssets();
            Debug.Log($"MetallurgySetup: ore, charcoal, bars, leather, bone glue, ore rocks, kiln/bloomery/tanning rack built{(building != null ? $", added to '{building.name}' - save the scene." : ".")}");
        }

        // ---- Items ----

        private static void Shard(Transform root, Material material, Vector3 position, Vector3 size, float yaw)
        {
            var part = new GameObject("Lump");
            part.transform.SetParent(root, false);
            part.transform.localPosition = position;
            part.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
            part.transform.localScale = size;
            part.AddComponent<MeshFilter>().sharedMesh = _shard;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void Ingot(Transform root, Material material)
        {
            // A rough bar: a long block with a narrower top.
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Base", material, new Vector3(0f, 0.035f, 0f), Vector3.zero, new Vector3(0.26f, 0.07f, 0.11f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Top", material, new Vector3(0f, 0.08f, 0f), Vector3.zero, new Vector3(0.21f, 0.03f, 0.08f));
        }

        private static ItemDefinition Item(string id)
        {
            return AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{id}.asset");
        }

        private static Material Mat(string name, Color color, float smoothness, float metallic)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---- Ore rocks ----

        /// <summary>A flint-rock copy (same rock model, collider, LODs) with its own deposit
        /// definition, an optional tint and metal lumps stuck on its surface.</summary>
        private static GameObject BuildOreRock(string prefabName, string definitionName, string displayName, int hits,
            (ItemDefinition item, int amount, float chance)[] drops, Material tint, Material[] veins)
        {
            string definitionPath = $"{NodeFolder}/{definitionName}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(definitionPath);
            if (definition == null)
            {
                AssetDatabase.CopyAsset(FlintDepositPath, definitionPath);
                definition = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(definitionPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_displayName").stringValue = displayName;
            serialized.FindProperty("_hitsToDeplete").intValue = hits;
            SerializedProperty list = serialized.FindProperty("_drops");
            list.arraySize = drops.Length;
            for (int i = 0; i < drops.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_item").objectReferenceValue = drops[i].item;
                entry.FindPropertyRelative("_amount").intValue = drops[i].amount;
                entry.FindPropertyRelative("_chance").floatValue = drops[i].chance;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FlintRockPath));
            root.name = prefabName;
            root.transform.position = Vector3.zero;
            var node = new SerializedObject(root.GetComponent<ResourceNode>());
            node.FindProperty("_definition").objectReferenceValue = definition;
            node.ApplyModifiedPropertiesWithoutUndo();

            if (tint != null)
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i] = TintedRockMaterial(materials[i], tint.GetColor("_BaseColor"));
                    }

                    renderer.sharedMaterials = materials;
                }
            }

            AddVeins(root, veins);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{WorldPrefabFolder}/{prefabName}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material TintedRockMaterial(Material source, Color tint)
        {
            string path = $"{MaterialFolder}/{source.name}_BogIron.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = $"{source.name}_BogIron" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.CopyPropertiesFromMaterial(source);
            material.SetColor("_BaseColor", Color.Lerp(Color.white, tint * 1.25f, 0.85f));
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Metal lumps set into the rock's surface: rays from around the rock towards
        /// its centre find the surface, a lump goes half into it there.</summary>
        private static void AddVeins(GameObject rock, Material[] materials)
        {
            Renderer main = rock.GetComponentInChildren<Renderer>();
            Bounds bounds = main.bounds;
            var collider = rock.GetComponent<Collider>();
            Physics.SyncTransforms();
            var rng = new System.Random(rock.name.GetHashCode());
            int placed = 0;
            for (int i = 0; i < 40 && placed < 9; i++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float height = Mathf.Lerp(0.25f, 0.95f, (float)rng.NextDouble());
                Vector3 target = bounds.center + Vector3.up * (bounds.extents.y * (height - 0.5f));
                Vector3 from = target + new Vector3(Mathf.Cos(angle), 0.3f, Mathf.Sin(angle)) * (bounds.extents.magnitude * 2f);
                if (!collider.Raycast(new Ray(from, (target - from).normalized), out RaycastHit hit, 100f))
                {
                    continue;
                }

                var lump = new GameObject("Vein");
                lump.transform.SetParent(rock.transform, true);
                lump.transform.position = hit.point - hit.normal * 0.02f;
                lump.transform.rotation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler((float)rng.NextDouble() * 360f, 0f, (float)rng.NextDouble() * 360f);
                lump.transform.localScale = Vector3.one * Mathf.Lerp(0.38f, 0.6f, (float)rng.NextDouble());
                lump.AddComponent<MeshFilter>().sharedMesh = _shard;
                lump.AddComponent<MeshRenderer>().sharedMaterial = materials[placed % materials.Length];
                placed++;
            }
        }

        private static void SetMinToolTier(string definitionName, int tier)
        {
            var definition = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>($"{NodeFolder}/{definitionName}.asset");
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_minToolTier").intValue = tier;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        internal static void UpsertResource(string biomeName, GameObject prefab, float density, float spacing, Vector2 scale, Vector2 heightAboveWater)
        {
            var biome = AssetDatabase.LoadAssetAtPath<BiomeDefinition>($"Assets/NAV/ScriptableObjects/Biomes/{biomeName}.asset");
            var serialized = new SerializedObject(biome);
            SerializedProperty list = serialized.FindProperty("_resourceSpawns");
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
            entry.FindPropertyRelative("_heightAboveWater").vector2Value = heightAboveWater;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(biome);
        }

        // ---- Stations ----

        private struct StationRecipe
        {
            public ItemDefinition Input;
            public int InputAmount;
            public ItemDefinition Output;
            public int OutputAmount;
            public float Seconds;
        }

        private static StationRecipe Recipe(ItemDefinition input, int inputAmount, ItemDefinition output, int outputAmount, float seconds)
        {
            return new StationRecipe { Input = input, InputAmount = inputAmount, Output = output, OutputAmount = outputAmount, Seconds = seconds };
        }

        private static BuildingPieceDefinition BuildStation(string id, string displayName, (ItemDefinition item, int amount)[] cost,
            StationRecipe[] recipes, ItemDefinition fuel, int fuelPerItem, int inputCapacity, int fuelCapacity,
            System.Action<Transform, GameObject, GameObject> buildModel)
        {
            string definitionPath = $"{PieceFolder}/{id}Piece.asset";
            var definition = AssetDatabase.LoadAssetAtPath<BuildingPieceDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BuildingPieceDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }

            var root = new GameObject(id);
            var working = new GameObject("Working");
            var loaded = new GameObject("Loaded");
            buildModel(root.transform, working, loaded);

            // Collider over the whole model; the station's own interact collider is a child.
            Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                bounds.Encapsulate(renderer.bounds);
            }

            var box = root.AddComponent<BoxCollider>();
            box.center = bounds.center - root.transform.position;
            box.size = bounds.size;

            var piece = new SerializedObject(root.AddComponent<BuildingPiece>());
            piece.FindProperty("_definition").objectReferenceValue = definition;
            piece.ApplyModifiedPropertiesWithoutUndo();

            var stationObject = new GameObject("Station");
            stationObject.transform.SetParent(root.transform, false);
            stationObject.transform.localPosition = box.center;
            var interactBox = stationObject.AddComponent<BoxCollider>();
            interactBox.size = box.size * 1.02f;
            var output = new GameObject("Output");
            output.transform.SetParent(root.transform, false);
            output.transform.localPosition = new Vector3(0f, 0.4f, bounds.extents.z + 0.4f);

            ProcessingStation station = stationObject.AddComponent<ProcessingStation>();
            var stationSerialized = new SerializedObject(station);
            stationSerialized.FindProperty("_stationName").stringValue = displayName;
            SerializedProperty recipeList = stationSerialized.FindProperty("_recipes");
            recipeList.arraySize = recipes.Length;
            for (int i = 0; i < recipes.Length; i++)
            {
                SerializedProperty entry = recipeList.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_input").objectReferenceValue = recipes[i].Input;
                entry.FindPropertyRelative("_inputAmount").intValue = recipes[i].InputAmount;
                entry.FindPropertyRelative("_output").objectReferenceValue = recipes[i].Output;
                entry.FindPropertyRelative("_outputAmount").intValue = recipes[i].OutputAmount;
                entry.FindPropertyRelative("_seconds").floatValue = recipes[i].Seconds;
            }

            stationSerialized.FindProperty("_fuel").objectReferenceValue = fuel;
            stationSerialized.FindProperty("_fuelPerItem").intValue = Mathf.Max(1, fuelPerItem);
            stationSerialized.FindProperty("_fuelCapacity").intValue = Mathf.Max(1, fuelCapacity);
            stationSerialized.FindProperty("_inputCapacity").intValue = inputCapacity;
            stationSerialized.FindProperty("_outputPoint").objectReferenceValue = output.transform;
            stationSerialized.FindProperty("_workingVisual").objectReferenceValue = working;
            stationSerialized.FindProperty("_loadedVisual").objectReferenceValue = loaded;
            stationSerialized.ApplyModifiedPropertiesWithoutUndo();
            working.SetActive(false);
            loaded.SetActive(false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{BuildPrefabFolder}/{id}.prefab");
            Object.DestroyImmediate(root);

            string iconPath = $"{IconFolder}/Icon_{id}.png";
            File.WriteAllBytes(iconPath, ItemIconRenderer.RenderIcon(prefab, Quaternion.Euler(-22f, 0f, 0f) * Quaternion.Euler(0f, 145f, 0f)));
            AssetDatabase.ImportAsset(iconPath);
            ItemIconRenderer.ConfigureSprite(iconPath);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = $"{id}Piece";
            serialized.FindProperty("_displayName").stringValue = displayName;
            serialized.FindProperty("_prefab").objectReferenceValue = prefab;
            serialized.FindProperty("_requiresWorkbench").boolValue = true;
            serialized.FindProperty("_icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            SerializedProperty costList = serialized.FindProperty("_cost");
            costList.arraySize = cost.Length;
            for (int i = 0; i < cost.Length; i++)
            {
                costList.GetArrayElementAtIndex(i).FindPropertyRelative("_item").objectReferenceValue = cost[i].item;
                costList.GetArrayElementAtIndex(i).FindPropertyRelative("_amount").intValue = cost[i].amount;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        /// <summary>Makes a built piece prefab an upgrade of a station kind (WorkbenchExtension):
        /// the tanning rack upgrades the workbench. SmithingSetup uses it for the anvil/bellows.</summary>
        internal static void MakeExtension(GameObject prefab, WorkbenchDefinition upgrades, string extensionId)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                WorkbenchExtension extension = root.GetComponent<WorkbenchExtension>() ?? root.AddComponent<WorkbenchExtension>();
                var serialized = new SerializedObject(extension);
                serialized.FindProperty("_upgrades").objectReferenceValue = upgrades;
                serialized.FindProperty("_extensionId").stringValue = extensionId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Each model's pivot is at ground level, centred.

        /// <summary>An earth-covered charcoal pit: a low clay dome ringed with stones, a log
        /// stack peeking out; glowing embers and a light while it burns.</summary>
        private static void BuildKiln(Transform root, GameObject working, GameObject loaded, Material clay, Material stone, Material wood, Material fire, Material fireCore)
        {
            CreatureLootSetup.AddPart(root, PrimitiveType.Sphere, "Mound", clay, new Vector3(0f, 0.2f, 0f), Vector3.zero, new Vector3(2f, 1.1f, 2f));
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI * 2f / 10f;
                CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Stone", stone, new Vector3(Mathf.Cos(angle) * 1.05f, 0.1f, Mathf.Sin(angle) * 1.05f), new Vector3(0f, -angle * Mathf.Rad2Deg, 0f), new Vector3(0.35f, 0.22f, 0.28f));
            }

            loaded.transform.SetParent(root, false);
            for (int i = 0; i < 3; i++)
            {
                CreatureLootSetup.AddPart(loaded.transform, PrimitiveType.Cylinder, "Log", wood, new Vector3(-0.15f + i * 0.15f, 0.72f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.12f, 0.25f, 0.12f));
            }

            working.transform.SetParent(root, false);
            CreatureLootSetup.AddPart(working.transform, PrimitiveType.Sphere, "Embers", fire, new Vector3(0f, 0.55f, 0.82f), Vector3.zero, new Vector3(0.35f, 0.18f, 0.12f));
            AddLight(working.transform, new Vector3(0f, 0.8f, 1f), 1.6f, 4f);
        }

        /// <summary>A clay bloomery tower (домница) on a stone base, with a front mouth; fire in
        /// the mouth and a light while smelting, ore by the mouth while loaded.</summary>
        private static void BuildBloomery(Transform root, GameObject working, GameObject loaded, Material clay, Material stone, Material fire, Material fireCore, Material ore)
        {
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Base", stone, new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(1.4f, 0.15f, 1.4f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Tower", clay, new Vector3(0f, 1.05f, 0f), Vector3.zero, new Vector3(1f, 0.75f, 1f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Neck", clay, new Vector3(0f, 1.95f, 0f), Vector3.zero, new Vector3(0.7f, 0.2f, 0.7f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Mouth", stone, new Vector3(0f, 0.55f, 0.47f), Vector3.zero, new Vector3(0.45f, 0.4f, 0.12f));

            loaded.transform.SetParent(root, false);
            CreatureLootSetup.AddPart(loaded.transform, PrimitiveType.Sphere, "OreHeap", ore, new Vector3(0.45f, 0.35f, 0.65f), Vector3.zero, new Vector3(0.35f, 0.2f, 0.3f));

            working.transform.SetParent(root, false);
            CreatureLootSetup.AddPart(working.transform, PrimitiveType.Sphere, "Flame", fire, new Vector3(0f, 0.55f, 0.52f), Vector3.zero, new Vector3(0.32f, 0.28f, 0.08f));
            CreatureLootSetup.AddPart(working.transform, PrimitiveType.Sphere, "FlameCore", fire, new Vector3(0f, 0.52f, 0.55f), Vector3.zero, new Vector3(0.2f, 0.16f, 0.06f));
            CreatureLootSetup.AddPart(working.transform, PrimitiveType.Sphere, "TopGlow", fire, new Vector3(0f, 2.06f, 0f), Vector3.zero, new Vector3(0.5f, 0.04f, 0.5f));
            AddLight(working.transform, new Vector3(0f, 0.7f, 0.9f), 2.2f, 6f);
        }

        /// <summary>A wooden frame with a hide stretched in it; while tanning, the hide hangs in
        /// the frame (Loaded), the frame alone otherwise.</summary>
        private static void BuildTanningRack(Transform root, GameObject loaded, Material woodMaterial, Material hide, GameObject working)
        {
            foreach (float x in new[] { -0.75f, 0.75f })
            {
                CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Post", woodMaterial, new Vector3(x, 0.9f, 0f), new Vector3(8f, 0f, 0f), new Vector3(0.1f, 0.9f, 0.1f));
            }

            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "TopBar", woodMaterial, new Vector3(0f, 1.65f, 0.1f), new Vector3(0f, 0f, 90f), new Vector3(0.08f, 0.85f, 0.08f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "BottomBar", woodMaterial, new Vector3(0f, 0.35f, -0.1f), new Vector3(0f, 0f, 90f), new Vector3(0.08f, 0.85f, 0.08f));

            loaded.transform.SetParent(root, false);
            CreatureLootSetup.AddPart(loaded.transform, PrimitiveType.Cube, "Hide", hide, new Vector3(0f, 1f, 0f), new Vector3(8f, 0f, 0f), new Vector3(1.25f, 1.15f, 0.03f));

            // Working: nothing extra (tanning is quiet); Loaded already shows the hide.
            working.transform.SetParent(root, false);
        }

        private static void AddLight(Transform parent, Vector3 position, float intensity, float range)
        {
            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.25f);
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        // ---- Campfire ----

        private static void AddBoneGlueToCampfire(ItemDefinition bone, ItemDefinition boneGlue)
        {
            GameObject campfire = PrefabUtility.LoadPrefabContents(CampfirePrefabPath);
            try
            {
                CookingStation station = campfire.GetComponentInChildren<CookingStation>(true);
                var serialized = new SerializedObject(station);
                SerializedProperty recipes = serialized.FindProperty("_recipes");
                for (int i = 0; i < recipes.arraySize; i++)
                {
                    if (recipes.GetArrayElementAtIndex(i).FindPropertyRelative("_input").objectReferenceValue == bone)
                    {
                        return;
                    }
                }

                recipes.arraySize++;
                SerializedProperty entry = recipes.GetArrayElementAtIndex(recipes.arraySize - 1);
                entry.FindPropertyRelative("_input").objectReferenceValue = bone;
                entry.FindPropertyRelative("_output").objectReferenceValue = boneGlue;
                entry.FindPropertyRelative("_cookSeconds").floatValue = 20f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(campfire, CampfirePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(campfire);
            }
        }
    }
}
