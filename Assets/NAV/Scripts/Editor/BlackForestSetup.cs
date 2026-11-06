using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.AI;
using NAV.Gameplay.Building;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Items;
using NAV.Presentation.Lighting;
using NAV.World.Biomes;
using NAV.World.Generation.Hybrid;

namespace NAV.Editor
{
    /// <summary>
    /// Tier 6 - the Black Forest (Чернолесье, PROGRESSION_TIERS_v0.1.md), NAV > World > Build Black
    /// Forest (Tier 6):
    ///   - the biome: a climate band of its own between the dense forest and the sea islands (the
    ///     dense forest's band now ends at 0.5 of the world radius, the black forest takes 0.5-0.6);
    ///     cursed, ever-dusky (BiomeDefinition.Twilight - AtmosphereFog dims the daylight,
    ///     DayNightGrading leans to the night look), dark violet fog, night sounds by day;
    ///   - bog oaks (морёный дуб): blackened old oaks, felled only with a black steel axe (tool
    ///     tier 6) -> Bog Oak Wood (the top building timber, hafts of tier 6+ weapons);
    ///   - bog-oak deadfall: fallen black boughs any silver axe (tier 5) can break -> Bog Oak Knots,
    ///     so the first black steel needs no black steel axe;
    ///   - black ore veins (a silver pickaxe at least) -> the Smelting Furnace makes Black Iron
    ///     (a bloom); the Charcoal Kiln burns knots or bog oak into Bog Oak Charcoal; at the Forge
    ///     level 6 black iron + bog oak charcoal -> Black Steel Bar;
    ///   - the Quench Tub (закалочный чан): the Forge's 5th upgrade (level 6); the Carved Table
    ///     (резной стол): the Workbench's 4th upgrade (level 5) - the bog-oak building set;
    ///   - Black Steel Axe / Pickaxe (tool tier 6);
    ///   - the Volkolak (волколак): a black, ember-marked placeholder beast (the ithappy tiger),
    ///     нечисть (silver x1.5), in packs, many at night; drops Volkolak Pelt (cloak, cuirass);
    ///   - the bog-oak set: wall, carved pillar (two storeys), carved arch, tent roof (шатёр),
    ///     black gate;
    ///   - the boss 6 arena slot (no boss - Boss Status: not designed).
    /// Then re-run Build Armour (black steel set, volkolak cloak) and Build Pack Weapon Recipes
    /// (black steel weapons). Models are PLACEHOLDERS. Re-runnable; saves the scene.
    /// </summary>
    internal static class BlackForestSetup
    {
        private const string ItemFolder = "Assets/NAV/ScriptableObjects/Items/";
        private const string BiomeFolder = "Assets/NAV/ScriptableObjects/Biomes/";
        private const string BlackBiomePath = BiomeFolder + "BlackForestBiome.asset";
        private const string DenseLayerPath = "Assets/NAV/Art/Terrain/DenseForestLayer.terrainlayer";
        private const string BlackLayerPath = "Assets/NAV/Art/Terrain/BlackForestLayer.terrainlayer";
        private const string WorldPrefabs = "Assets/NAV/Prefabs/World/";
        private const string VegetationFolder = WorldPrefabs + "Vegetation/";
        private const string BlackForestPrefabs = WorldPrefabs + "BlackForest";
        private const string BuildPrefabs = "Assets/NAV/Prefabs/Build/";
        private const string CreaturePrefabs = "Assets/NAV/Prefabs/Creatures/";
        private const string CreatureDefinitions = "Assets/NAV/ScriptableObjects/Creatures/";
        private const string MaterialFolder = "Assets/NAV/Materials/Items/";
        private const string MeshFolder = "Assets/NAV/Art/Models/";
        private const string DatabasePath = "Assets/NAV/ScriptableObjects/Core/DefaultItemDatabase.asset";
        private const string PieceDatabasePath = "Assets/NAV/ScriptableObjects/Core/BuildingPieceDatabase.asset";
        private const string WorkbenchPath = "Assets/NAV/ScriptableObjects/Workbenches/BasicWorkbench.asset";
        private const string ForgePath = "Assets/NAV/ScriptableObjects/Workbenches/Forge.asset";
        private const string HybridSettingsPath = "Assets/NAV/ScriptableObjects/World/HybridWorldSettings.asset";
        private const string PoiFolder = "Assets/NAV/ScriptableObjects/World/Poi/";
        private const string ArenaSlotPrefabPath = "Assets/NAV/Prefabs/World/Poi/Poi_BossArenaSlot.prefab";
        private const string NskTreePath = VegetationFolder + "NSK_tree03_WoodNode.prefab";
        private const string AudioFolder = "Assets/Nature - Essentials/";

        // The climate band: the dense forest ends here, the black forest runs on to the islands.
        private const float DenseForestUpTo = 0.5f;
        private const float BlackForestUpTo = 0.6f;
        private const int SteelForgeLevel = 6;
        private const int CarvedTableLevel = 5;
        private const int BlackSteelToolTier = 6;

        // NSK tree03 at scale 1 (as OakSetup): 8.6 m tall, trunk ~0.45 m radius.
        private const float TreeHeight = 8.6f;
        private const float CutHeight = 0.8f;
        private const float TrunkLength = 5.6f;
        private const float TrunkRadius = 0.42f;
        private const float LogLength = 2.6f;
        private const float LogRadius = 0.4f;
        private static readonly float[] LogCentres = { 1.4f, 4.2f };

        private static Material _bogOak;
        private static Material _carving;
        private static Material _blackSteel;

        [MenuItem("NAV/World/Build Black Forest (Tier 6)")]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("BlackForestSetup: exit Play Mode first.");
                return;
            }

            ItemDefinition rock = Item("Rock");
            ItemDefinition wood = Item("Wood");
            ItemDefinition leather = Item("Leather");
            ItemDefinition bone = Item("Bone");
            ItemDefinition fang = Item("Fang");
            ItemDefinition ironBar = Item("IronBar");
            ItemDefinition silverBar = Item("SilverBar");
            var workbench = AssetDatabase.LoadAssetAtPath<WorkbenchDefinition>(WorkbenchPath);
            var forge = AssetDatabase.LoadAssetAtPath<WorkbenchDefinition>(ForgePath);
            var dense = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(BiomeFolder + "DenseForestBiome.asset");
            var settings = AssetDatabase.LoadAssetAtPath<HybridWorldSettings>(HybridSettingsPath);
            if (rock == null || wood == null || leather == null || ironBar == null || silverBar == null || workbench == null || forge == null
                || dense == null || settings == null || Load(BuildPrefabs + "SmeltingFurnace.prefab") == null || Load(BuildPrefabs + "CharcoalKiln.prefab") == null)
            {
                Debug.LogError("BlackForestSetup: run the hybrid world, metallurgy, smithing, oak, dense forest and mountain setups first (needs Silver Bar, the Smelting Furnace and the Dense Forest).");
                return;
            }

            TreeFellingSetup.EnsureFolder(BlackForestPrefabs);
            _bogOak = OakSetup.Mat("BogOak_material", new Color(0.12f, 0.1f, 0.1f), 0.3f);
            _carving = OakSetup.Mat("BogOakCarving_material", new Color(0.55f, 0.44f, 0.28f), 0.35f);
            _blackSteel = MetallurgySetup.Mat("BlackSteel_material", new Color(0.12f, 0.12f, 0.14f), 0.7f, 0.9f);
            Material oreStone = MetallurgySetup.Mat("BlackOreStone_material", new Color(0.2f, 0.19f, 0.2f), 0.15f, 0f);
            Material oreGlint = MetallurgySetup.Mat("BlackOreGlint_material", new Color(0.3f, 0.08f, 0.06f), 0.6f, 0.7f);
            Material bloom = MetallurgySetup.Mat("BlackIron_material", new Color(0.22f, 0.21f, 0.22f), 0.35f, 0.75f);
            Material charcoalMat = MetallurgySetup.Mat("BogOakCharcoal_material", new Color(0.06f, 0.05f, 0.07f), 0.45f, 0f);
            Material peltMat = MetallurgySetup.Mat("VolkolakPelt_material", new Color(0.09f, 0.08f, 0.08f), 0.05f, 0f);
            Material peltMark = MetallurgySetup.Mat("VolkolakMark_material", new Color(0.45f, 0.08f, 0.04f), 0.1f, 0f);

            // ---- Items ----
            var database = new SerializedObject(AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath));
            Quaternion lumpPose = Quaternion.Euler(-30f, 30f, 0f);
            ItemDefinition blackOre = CreatureLootSetup.BuildItem("BlackOre", "Black Ore",
                "Heavy black rock with a dull red glint, dug in the black forest. The smelting furnace makes a bloom of it.", 30, 1.3f,
                root =>
                {
                    MetallurgySetup.Shard(root, oreStone, new Vector3(0f, 0.08f, 0f), new Vector3(0.3f, 0.22f, 0.28f), 10f);
                    MetallurgySetup.Shard(root, _blackSteel, new Vector3(0.07f, 0.12f, 0.05f), new Vector3(0.12f, 0.1f, 0.12f), 50f);
                    MetallurgySetup.Shard(root, oreGlint, new Vector3(-0.07f, 0.11f, -0.05f), new Vector3(0.09f, 0.07f, 0.09f), 120f);
                }, lumpPose);
            ItemDefinition blackIron = CreatureLootSetup.BuildItem("BlackIron", "Black Iron",
                "A spongy bloom of black iron. Forged with bog oak charcoal and quenched, it becomes black steel.", 30, 1.3f,
                root =>
                {
                    MetallurgySetup.Shard(root, bloom, new Vector3(0f, 0.08f, 0f), new Vector3(0.28f, 0.2f, 0.26f), 25f);
                    MetallurgySetup.Shard(root, bloom, new Vector3(0.09f, 0.06f, 0.06f), new Vector3(0.14f, 0.12f, 0.14f), 95f);
                }, lumpPose);
            ItemDefinition bogOakWood = CreatureLootSetup.BuildItem("BogOakWood", "Bog Oak Wood",
                "Black, iron-hard oak from the cursed forest. For the highest buildings and the hafts of the best weapons.", 50, 1.4f,
                root =>
                {
                    for (int i = 0; i < 3; i++)
                    {
                        CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Log", _bogOak,
                            new Vector3(-0.09f + i * 0.09f, 0.05f + (i == 1 ? 0.07f : 0f), 0f), new Vector3(90f, 0f, 0f), new Vector3(0.09f, 0.22f, 0.09f));
                    }
                }, Quaternion.Euler(-25f, 30f, 0f));
            ItemDefinition knots = CreatureLootSetup.BuildItem("BogOakKnots", "Bog Oak Knots",
                "Black boughs and knots broken off fallen bog oaks. Too twisted to build with - burn them into charcoal.", 50, 0.6f,
                root =>
                {
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Bough", _bogOak, new Vector3(0f, 0.04f, 0f), new Vector3(90f, 20f, 0f), new Vector3(0.05f, 0.18f, 0.05f));
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Twig", _bogOak, new Vector3(0.06f, 0.05f, 0.05f), new Vector3(90f, -35f, 0f), new Vector3(0.035f, 0.1f, 0.035f));
                    CreatureLootSetup.AddPart(root, PrimitiveType.Sphere, "Knot", _bogOak, new Vector3(-0.07f, 0.05f, -0.03f), Vector3.zero, new Vector3(0.09f, 0.08f, 0.09f));
                }, Quaternion.Euler(-30f, 20f, 0f));
            ItemDefinition bogCharcoal = CreatureLootSetup.BuildItem("BogOakCharcoal", "Bog Oak Charcoal",
                "Charcoal burned from bog oak - it burns hot and clean enough to turn black iron into steel.", 50, 0.3f,
                root =>
                {
                    MetallurgySetup.Shard(root, charcoalMat, new Vector3(0f, 0.05f, 0f), new Vector3(0.2f, 0.12f, 0.14f), 0f);
                    MetallurgySetup.Shard(root, charcoalMat, new Vector3(0.08f, 0.05f, 0.05f), new Vector3(0.12f, 0.09f, 0.1f), 70f);
                }, lumpPose);
            ItemDefinition blackSteelBar = CreatureLootSetup.BuildItem("BlackSteelBar", "Black Steel Bar",
                "A bar of black steel - harder than silver, if not as hated by the unclean.", 30, 1.2f,
                root => MetallurgySetup.Ingot(root, _blackSteel), Quaternion.Euler(-25f, 30f, 0f));
            ItemDefinition pelt = CreatureLootSetup.BuildItem("VolkolakPelt", "Volkolak Pelt",
                "The coarse black pelt of a volkolak, still smelling of the forest. For cloaks and armour linings.", 20, 0.8f,
                root =>
                {
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Pelt", peltMat, new Vector3(0f, 0.04f, 0f), new Vector3(0f, 15f, 0f), new Vector3(0.38f, 0.08f, 0.28f));
                    for (int i = 0; i < 3; i++)
                    {
                        CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Mark", peltMark, new Vector3(-0.1f + i * 0.1f, 0.081f, 0.02f), new Vector3(0f, 15f + i * 10f, 0f), new Vector3(0.025f, 0.002f, 0.16f));
                    }
                }, Quaternion.Euler(-30f, 20f, 0f));
            foreach (ItemDefinition item in new[] { blackOre, blackIron, bogOakWood, knots, bogCharcoal, blackSteelBar, pelt })
            {
                CreatureLootSetup.AddToList(database, "_items", item);
            }

            // ---- Black ore veins (a silver pickaxe - tier 5) ----
            GameObject blackOreRock = MetallurgySetup.BuildOreRock("BlackOre_Rock", "BlackOreDeposit", "Black Ore Vein", 7,
                new[] { (blackOre, 2, 1f), (blackOre, 1, 0.5f), (rock, 2, 1f) }, oreStone, new[] { _blackSteel, oreGlint });
            MetallurgySetup.SetMinToolTier("BlackOreDeposit", 5);

            // ---- Bog oaks (a black steel axe) and their deadfall (a silver axe) ----
            GameObject bogOakTree = BuildBogOakTree(bogOakWood, knots);
            GameObject deadfall = BuildDeadfall(knots);

            // ---- The biome ----
            BiomeDefinition black = BlackBiome(dense);
            var anyHeight = new Vector2(-1000f, 1000f);
            var dryLand = new Vector2(1.5f, 1000f);
            Upsert(black, bogOakTree, 0.5f, 8f, new Vector2(1.5f, 2.1f), dryLand);
            Upsert(black, Load(VegetationFolder + "AncientSpruce_WoodNode.prefab"), 0.22f, 10f, new Vector2(1.3f, 1.9f), dryLand);
            Upsert(black, Load(WorldPrefabs + "Swamp/DeadTree_A.prefab"), 0.07f, 12f, new Vector2(0.9f, 1.3f), dryLand);
            Upsert(black, Load(WorldPrefabs + "Swamp/DeadTree_B.prefab"), 0.05f, 14f, new Vector2(0.9f, 1.3f), dryLand);
            Upsert(black, deadfall, 0.16f, 12f, new Vector2(0.9f, 1.3f), dryLand);
            Upsert(black, blackOreRock, 0.12f, 16f, new Vector2(0.9f, 1.4f), anyHeight);
            Upsert(black, Load(WorldPrefabs + "SM_Rocks_03.prefab"), 0.1f, 16f, new Vector2(1.4f, 2.4f), anyHeight);
            Upsert(black, Load(WorldPrefabs + "LooseBranch.prefab"), 0.12f, 14f, Vector2.one, anyHeight);
            Upsert(black, Load(WorldPrefabs + "LooseStone.prefab"), 0.05f, 18f, Vector2.one, anyHeight);
            AddClimateBand(settings, dense, black);

            // ---- The volkolak ----
            GameObject volkolak = BuildVolkolak(pelt, fang, bone);
            SetCreatures(black, volkolak);

            // ---- Stations: charcoal from bog oak, black iron from the ore ----
            AddStationRecipe(BuildPrefabs + "CharcoalKiln.prefab", knots, 2, bogCharcoal, 1, 20f);
            AddStationRecipe(BuildPrefabs + "CharcoalKiln.prefab", bogOakWood, 1, bogCharcoal, 2, 25f);
            AddStationRecipe(BuildPrefabs + "SmeltingFurnace.prefab", blackOre, 1, blackIron, 1, 50f);

            BuildingPieceDefinition quenchTub = SmithingSetup.BuildPiece("QuenchTub", "Quench Tub",
                new[] { (bogOakWood, 4), (ironBar, 4), (rock, 6) }, BuildQuenchTubModel,
                root => SmithingSetup.AddExtension(root, forge, "QuenchTub"));
            SetUnlock(quenchTub, blackIron);
            BuildingPieceDefinition carvedTable = SmithingSetup.BuildPiece("CarvedTable", "Carved Table",
                new[] { (bogOakWood, 8), (silverBar, 1), (ironBar, 2) }, BuildCarvedTableModel,
                root => SmithingSetup.AddExtension(root, workbench, "CarvedTable"));

            // ---- Black steel and its tools ----
            SmithingSetup.Metal steelTint = SmithingSetup.TintedToolMaterial("BlackSteel", new Color(0.32f, 0.32f, 0.36f), new Color(0.12f, 0.12f, 0.14f));
            ItemDefinition steelAxe = SmithingSetup.BuildTool("StoneAxe", "BlackSteelAxe", "Black Steel Axe", "A black steel axe - the only edge a bog oak gives way to.", BlackSteelToolTier, 3.2f, steelTint);
            ItemDefinition steelPick = SmithingSetup.BuildTool("StonePickaxe", "BlackSteelPickaxe", "Black Steel Pickaxe", "A black steel pickaxe for what lies deeper still.", BlackSteelToolTier, 3.2f, steelTint);
            CreatureLootSetup.AddToList(database, "_items", steelAxe);
            CreatureLootSetup.AddToList(database, "_items", steelPick);
            var recipes = new List<RecipeDefinition>
            {
                SmithingSetup.BuildRecipe("BlackSteelBarRecipe", blackSteelBar, forge, SteelForgeLevel, (blackIron, 1), (bogCharcoal, 2)),
                SmithingSetup.BuildRecipe("BlackSteelAxeRecipe", steelAxe, forge, SteelForgeLevel, (blackSteelBar, 3), (bogOakWood, 2), (leather, 1)),
                SmithingSetup.BuildRecipe("BlackSteelPickaxeRecipe", steelPick, forge, SteelForgeLevel, (blackSteelBar, 3), (bogOakWood, 2), (leather, 2)),
            };

            // ---- The bog-oak building set (the carved table - Workbench level 5) ----
            var pieces = new List<BuildingPieceDefinition>
            {
                quenchTub,
                carvedTable,
                OakSetup.Piece("BogOakWall", "Bog Oak Wall", new[] { (bogOakWood, 3) }, workbench, CarvedTableLevel, BuildBogOakWall, null),
                OakSetup.Piece("CarvedPillar", "Carved Pillar", new[] { (bogOakWood, 4) }, workbench, CarvedTableLevel, BuildCarvedPillar, null),
                OakSetup.Piece("CarvedArch", "Carved Arch", new[] { (bogOakWood, 3) }, workbench, CarvedTableLevel, BuildCarvedArch, null),
                OakSetup.Piece("TentRoof", "Tent Roof", new[] { (bogOakWood, 10), (wood, 6) }, workbench, CarvedTableLevel, BuildTentRoof, null),
                OakSetup.Piece("BlackGate", "Black Gate", new[] { (bogOakWood, 10), (blackSteelBar, 2) }, workbench, CarvedTableLevel, BuildBlackGate, OakSetup.AddGateDoor),
            };

            var pieceDatabase = new SerializedObject(AssetDatabase.LoadAssetAtPath<BuildingPieceDatabase>(PieceDatabasePath));
            PlayerBuilding building = Object.FindAnyObjectByType<PlayerBuilding>(FindObjectsInactive.Include);
            foreach (BuildingPieceDefinition piece in pieces)
            {
                CreatureLootSetup.AddToList(pieceDatabase, "_pieces", piece);
                if (building != null)
                {
                    CreatureLootSetup.AddToList(new SerializedObject(building), "_knownPieces", piece);
                }
            }

            // ---- Boss 6 arena slot ----
            AddBossSlot(settings, black);

            // ---- Scene: known recipes, the dusk in the grading ----
            WireScene(recipes);

            AssetDatabase.SaveAssets();
            if (building != null)
            {
                EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
                EditorSceneManager.SaveScene(building.gameObject.scene);
            }

            Debug.Log("BlackForestSetup: black forest band (0.5-0.6), bog oaks + deadfall, black ore, black iron -> black steel, quench tub, carved table, black steel tools, volkolak, bog-oak set, boss 6 slot. Now run Build Armour and Build Pack Weapon Recipes for the black steel gear.");
        }

        private static ItemDefinition Item(string id) => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}{id}.asset");

        private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        private static void Upsert(BiomeDefinition biome, GameObject prefab, float density, float spacing, Vector2 scale, Vector2 heightAboveWater)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"BlackForestSetup: a resource prefab for {biome.name} is missing - skipped.");
                return;
            }

            MetallurgySetup.UpsertResource(biome.name, prefab, density, spacing, scale, heightAboveWater);
        }

        // ---- Bog oak ----

        /// <summary>An old oak (NSK tree03, as OakSetup) gone black - bark near black, the crown
        /// a dead olive-brown - with its own felling chain: tree -> fallen trunk -> two logs of
        /// Bog Oak Wood (+ knots). Everything needs tool tier 6 (a black steel axe).</summary>
        private static GameObject BuildBogOakTree(ItemDefinition bogOakWood, ItemDefinition knots)
        {
            ResourceNodeDefinition logDefinition = TreeFellingSetup.GetOrCreateDefinition("BogOakLogNode");
            ResourceNodeDefinition fallenDefinition = TreeFellingSetup.GetOrCreateDefinition("BogOakFallenNode");
            ResourceNodeDefinition treeDefinition = TreeFellingSetup.GetOrCreateDefinition("BogOakTreeNode");
            Material bark = TintedCopy(AssetDatabase.LoadAssetAtPath<Material>("Assets/NAV/Materials/Vegetation/NSK_tree03_Bark.mat"), "BogOakBark", new Color(0.22f, 0.2f, 0.2f));
            Material leaves = TintedCopy(AssetDatabase.LoadAssetAtPath<Material>("Assets/NAV/Materials/Vegetation/NSK_tree03_Leaves.mat"), "BogOakLeaves", new Color(0.42f, 0.38f, 0.3f));

            GameObject stump = BuildStump();
            GameObject log = BuildLog(logDefinition);
            GameObject fallen = BuildFallen(fallenDefinition);

            var logSerialized = new SerializedObject(logDefinition);
            SerializedProperty drops = logSerialized.FindProperty("_drops");
            drops.arraySize = 2;
            SetDrop(drops, 0, bogOakWood, 3, 1f);
            SetDrop(drops, 1, knots, 1, 0.5f);
            logSerialized.ApplyModifiedPropertiesWithoutUndo();

            TreeFellingSetup.Configure(logSerialized, "Bog Oak Log", 4, null, null, 0f);
            TreeFellingSetup.Configure(new SerializedObject(fallenDefinition), "Fallen Bog Oak", 5, log, null, 0f);
            TreeFellingSetup.Configure(new SerializedObject(treeDefinition), "Bog Oak", 8, fallen, stump, 500f);
            foreach (ResourceNodeDefinition definition in new[] { logDefinition, fallenDefinition, treeDefinition })
            {
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("_minToolTier").intValue = BlackSteelToolTier;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
            }

            GameObject tree = TreeFellingSetup.InstantiateUnpacked(NskTreePath);
            tree.name = "BogOak_WoodNode";
            foreach (Renderer renderer in tree.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null)
                    {
                        continue;
                    }

                    materials[i] = materials[i].name.Contains("Leaves") ? (leaves != null ? leaves : materials[i]) : (bark != null ? bark : materials[i]);
                }

                renderer.sharedMaterials = materials;
            }

            var node = new SerializedObject(tree.GetComponent<ResourceNode>());
            node.FindProperty("_definition").objectReferenceValue = treeDefinition;
            node.ApplyModifiedPropertiesWithoutUndo();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tree, $"{VegetationFolder}BogOak_WoodNode.prefab");
            Object.DestroyImmediate(tree);
            return prefab;
        }

        private static GameObject BuildStump()
        {
            var root = new GameObject("BogOakStump");
            GameObject model = TreeFellingSetup.InstantiateUnpacked(TreeFellingSetup.StumpPath);
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = Vector3.one * 1.3f;
            model.transform.localPosition = new Vector3(0f, -0.3f, 0f);
            BlackenWood(root);
            return TreeFellingSetup.Save(root, "BogOakStump");
        }

        private static GameObject BuildLog(ResourceNodeDefinition definition)
        {
            var root = new GameObject("BogOakLog");
            TreeFellingSetup.AddLogVisual(root.transform, "Visual", LogLength, LogRadius, Vector3.zero, Quaternion.identity);
            BlackenWood(root);
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(LogLength, LogRadius * 2f, LogRadius * 2f);
            box.sharedMaterial = TreeFellingSetup.GetOrCreateFrictionMaterial();
            TreeFellingSetup.AddBody(root, 130f, 4f);
            TreeFellingSetup.AddNode(root, definition, null);
            return TreeFellingSetup.Save(root, "BogOakLog");
        }

        private static GameObject BuildFallen(ResourceNodeDefinition definition)
        {
            var root = new GameObject("BogOakFallen");
            TreeFellingSetup.AddLogVisual(root.transform, "TrunkVisual", TrunkLength, TrunkRadius,
                new Vector3(0f, CutHeight + TrunkLength * 0.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
            BlackenWood(root);
            var trunk = root.AddComponent<BoxCollider>();
            trunk.size = new Vector3(TrunkRadius * 2f, TrunkLength, TrunkRadius * 2f);
            trunk.center = new Vector3(0f, CutHeight + TrunkLength * 0.5f, 0f);
            trunk.sharedMaterial = TreeFellingSetup.GetOrCreateFrictionMaterial();

            var points = new Transform[LogCentres.Length];
            for (int i = 0; i < LogCentres.Length; i++)
            {
                var point = new GameObject($"LogPoint_{i + 1}").transform;
                point.SetParent(root.transform, false);
                point.localPosition = new Vector3(0f, CutHeight + LogCentres[i], 0f);
                point.localRotation = Quaternion.Euler(0f, 0f, 90f);
                points[i] = point;
            }

            TreeFellingSetup.AddBody(root, 700f, 0.5f);
            TreeFellingSetup.AddNode(root, definition, points);
            var falling = new SerializedObject(root.AddComponent<FallingTree>());
            falling.FindProperty("_treeHeight").floatValue = TreeHeight;
            falling.FindProperty("_upperContactHeight").floatValue = 2.2f;
            falling.ApplyModifiedPropertiesWithoutUndo();
            return TreeFellingSetup.Save(root, "BogOakFallen");
        }

        /// <summary>Fallen black boughs lying in the moss: static (no physics), any silver axe
        /// breaks them into Bog Oak Knots - the first bog oak charcoal before black steel.</summary>
        private static GameObject BuildDeadfall(ItemDefinition knots)
        {
            ResourceNodeDefinition definition = TreeFellingSetup.GetOrCreateDefinition("BogOakDeadfallNode");
            TreeFellingSetup.Configure(new SerializedObject(definition), "Bog Oak Deadfall", 4, null, null, 0f);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_shrinkOnHit").boolValue = true;
            serialized.FindProperty("_minToolTier").intValue = 5;
            SerializedProperty drops = serialized.FindProperty("_drops");
            drops.arraySize = 2;
            SetDrop(drops, 0, knots, 2, 1f);
            SetDrop(drops, 1, knots, 1, 0.5f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            var root = new GameObject("BogOakDeadfall");
            GameObject trunk = TreeFellingSetup.AddLogVisual(root.transform, "Bough", 3.4f, 0.26f, new Vector3(0f, 0.18f, 0f), Quaternion.Euler(0f, 0f, 4f));
            GameObject branch = TreeFellingSetup.AddLogVisual(root.transform, "Branch", 1.6f, 0.12f, new Vector3(0.6f, 0.3f, 0.45f), Quaternion.Euler(0f, -40f, 12f));
            GameObject twig = TreeFellingSetup.AddLogVisual(root.transform, "Twig", 1f, 0.07f, new Vector3(-0.9f, 0.35f, -0.3f), Quaternion.Euler(0f, 55f, 25f));
            foreach (GameObject part in new[] { trunk, branch, twig })
            {
                foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = _bogOak;
                }
            }

            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 0;
            collider.radius = 0.3f;
            collider.height = 3.4f;
            collider.center = new Vector3(0f, 0.2f, 0f);
            TreeFellingSetup.AddNode(root, definition, null);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{BlackForestPrefabs}/BogOakDeadfall.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BlackenWood(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = TintedCopy(materials[i], $"{(materials[i] != null ? materials[i].name : "Wood")}_BogOak", new Color(0.3f, 0.27f, 0.26f));
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static Material TintedCopy(Material source, string name, Color tint)
        {
            if (source == null)
            {
                return null;
            }

            string path = $"{MaterialFolder}{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.CopyPropertiesFromMaterial(source);
            Color baseColor = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white;
            material.SetColor("_BaseColor", baseColor * tint);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetDrop(SerializedProperty drops, int index, ItemDefinition item, int amount, float chance)
        {
            SerializedProperty drop = drops.GetArrayElementAtIndex(index);
            drop.FindPropertyRelative("_item").objectReferenceValue = item;
            drop.FindPropertyRelative("_amount").intValue = amount;
            drop.FindPropertyRelative("_chance").floatValue = chance;
        }

        // ---- The biome ----

        private static BiomeDefinition BlackBiome(BiomeDefinition dense)
        {
            // Ground: the dense forest's mossy floor, drained to a grey-black humus.
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(BlackLayerPath);
            if (layer == null)
            {
                AssetDatabase.CopyAsset(DenseLayerPath, BlackLayerPath);
                layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(BlackLayerPath);
            }

            layer.diffuseRemapMax = new Vector4(0.2f, 0.21f, 0.19f, 1f);
            EditorUtility.SetDirty(layer);

            var biome = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(BlackBiomePath);
            if (biome == null)
            {
                biome = ScriptableObject.CreateInstance<BiomeDefinition>();
                AssetDatabase.CreateAsset(biome, BlackBiomePath);
            }

            var serialized = new SerializedObject(biome);
            var source = new SerializedObject(dense);
            serialized.FindProperty("_displayName").stringValue = "Black Forest";
            serialized.FindProperty("_terrainLayer").objectReferenceValue = layer;
            serialized.FindProperty("_isWater").boolValue = false;
            serialized.FindProperty("_mapColor").colorValue = new Color(0.16f, 0.13f, 0.18f);
            serialized.FindProperty("_heightVariation").floatValue = 0.8f;
            serialized.FindProperty("_grassDensity").floatValue = 0.04f;
            serialized.FindProperty("_flowerDensity").floatValue = 0f;
            // Eternal dusk: you see ~65 m through a dark violet haze, the daylight mostly gone.
            serialized.FindProperty("_fogDensity").floatValue = 0.03f;
            serialized.FindProperty("_fogColor").colorValue = new Color(0.22f, 0.2f, 0.27f, 0.9f);
            serialized.FindProperty("_twilight").floatValue = 0.6f;
            serialized.FindProperty("_cold").floatValue = 0f;

            // Decor: the dense forest's (ferns, logs, stumps, mushrooms).
            serialized.CopyFromSerializedProperty(source.FindProperty("_decorSpawns"));
            // Night sounds by day too; the forest's wind.
            AudioClip night = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "Ambiance_Night_Loop_Stereo.wav");
            serialized.FindProperty("_dayAmbience").objectReferenceValue = night;
            serialized.FindProperty("_nightAmbience").objectReferenceValue = night;
            serialized.FindProperty("_ambienceVolume").floatValue = 0.45f;
            serialized.FindProperty("_windAmbience").objectReferenceValue = source.FindProperty("_windAmbience").objectReferenceValue;
            serialized.FindProperty("_windVolume").floatValue = 0.2f;

            // Resources are filled by Upsert; drop entries whose prefab is gone.
            SerializedProperty resources = serialized.FindProperty("_resourceSpawns");
            for (int i = resources.arraySize - 1; i >= 0; i--)
            {
                if (resources.GetArrayElementAtIndex(i).FindPropertyRelative("_prefab").objectReferenceValue == null)
                {
                    resources.DeleteArrayElementAtIndex(i);
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(biome);
            return biome;
        }

        /// <summary>The dense forest's band ends at DenseForestUpTo; the black forest's band
        /// follows it up to BlackForestUpTo (then the sea islands).</summary>
        private static void AddClimateBand(HybridWorldSettings settings, BiomeDefinition dense, BiomeDefinition black)
        {
            var serialized = new SerializedObject(settings);
            SerializedProperty bands = serialized.FindProperty("_climateBands");
            for (int i = bands.arraySize - 1; i >= 0; i--)
            {
                if (bands.GetArrayElementAtIndex(i).FindPropertyRelative("_biome").objectReferenceValue == black)
                {
                    bands.DeleteArrayElementAtIndex(i);
                }
            }

            int denseIndex = -1;
            for (int i = 0; i < bands.arraySize; i++)
            {
                if (bands.GetArrayElementAtIndex(i).FindPropertyRelative("_biome").objectReferenceValue == dense)
                {
                    denseIndex = i;
                }
            }

            if (denseIndex < 0)
            {
                Debug.LogError("BlackForestSetup: the Dense Forest has no climate band in HybridWorldSettings - the black forest band was not added.");
                return;
            }

            bands.GetArrayElementAtIndex(denseIndex).FindPropertyRelative("_upTo").floatValue = DenseForestUpTo;
            bands.InsertArrayElementAtIndex(denseIndex + 1);
            SerializedProperty band = bands.GetArrayElementAtIndex(denseIndex + 1);
            band.FindPropertyRelative("_biome").objectReferenceValue = black;
            band.FindPropertyRelative("_upTo").floatValue = BlackForestUpTo;
            band.FindPropertyRelative("_mixBiome").objectReferenceValue = null;
            band.FindPropertyRelative("_mixShare").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        // ---- The volkolak ----

        /// <summary>The volkolak (волколак): the wolf's prefab and definition copied, the ithappy
        /// tiger bigger, its texture baked black with ember-red markings. Нечисть, in packs.</summary>
        private static GameObject BuildVolkolak(ItemDefinition pelt, ItemDefinition fang, ItemDefinition bone)
        {
            string prefabPath = CreaturePrefabs + "Volkolak.prefab";
            string definitionPath = CreatureDefinitions + "VolkolakCreature.asset";
            if (Load(prefabPath) == null)
            {
                AssetDatabase.CopyAsset(CreaturePrefabs + "ForestWolf.prefab", prefabPath);
            }

            if (AssetDatabase.LoadAssetAtPath<CreatureDefinition>(definitionPath) == null)
            {
                AssetDatabase.CopyAsset(CreatureDefinitions + "ForestWolfCreature.asset", definitionPath);
            }

            var animal = new CreatureModelSetup.Animal
            {
                Prefab = "Volkolak", Definition = "VolkolakCreature", DisplayName = null, Model = "Tiger_001",
                Scale = 0.85f, BodyHalfWidth = 0.42f, BodyLength = 2.4f, BodyTop = 1.55f,
                WalkClipSpeed = 0.85f, RunClipSpeed = 3.4f,
                MoveSpeed = 1.9f, ChaseMultiplier = 3.6f, AttackRange = 1.9f, // prowls, charges ~6.8 m/s
            };
            CreatureModelSetup.ConfigureDefinition(definitionPath, animal);
            CreatureModelSetup.BuildPrefab(prefabPath, animal);

            var definition = AssetDatabase.LoadAssetAtPath<CreatureDefinition>(definitionPath);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_displayName").stringValue = "Volkolak";
            serialized.FindProperty("_isHostile").boolValue = true;
            serialized.FindProperty("_isUnclean").boolValue = true;
            serialized.FindProperty("_maxHealth").floatValue = 160f;
            serialized.FindProperty("_attackDamage").floatValue = 24f;
            serialized.FindProperty("_attackCooldown").floatValue = 1.4f;
            serialized.FindProperty("_poisonDamage").floatValue = 0f;
            serialized.FindProperty("_detectionRadius").floatValue = 18f;
            serialized.FindProperty("_packAlertRadius").floatValue = 20f;
            SerializedProperty loot = serialized.FindProperty("_lootDrops");
            loot.arraySize = 4;
            SetDrop(loot, 0, pelt, 1, 1f);
            SetDrop(loot, 1, pelt, 1, 0.4f);
            SetDrop(loot, 2, fang, 2, 0.6f);
            SetDrop(loot, 3, bone, 1, 0.5f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                root.name = "Volkolak";
                var creatureSerialized = new SerializedObject(root.GetComponent<Creature>());
                creatureSerialized.FindProperty("_definition").objectReferenceValue = definition;
                creatureSerialized.ApplyModifiedPropertiesWithoutUndo();
                foreach (Renderer renderer in root.transform.Find("Model").GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i] = BlackCopy(materials[i]);
                    }

                    renderer.sharedMaterials = materials;
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return Load(prefabPath);
        }

        // The tiger's palette texture baked black: its light fur -> charcoal, its dark stripes ->
        // smouldering red markings.
        private static Material BlackCopy(Material source)
        {
            if (source == null)
            {
                return null;
            }

            string path = $"{MaterialFolder}Volkolak_{source.name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = $"Volkolak_{source.name}" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.CopyPropertiesFromMaterial(source);
            string colour = source.HasProperty("_BaseColor") ? "_BaseColor" : source.HasProperty("_Color") ? "_Color" : null;
            if (colour != null)
            {
                material.SetColor(colour, Color.white);
            }

            string textureProperty = source.HasProperty("_BaseMap") ? "_BaseMap" : source.HasProperty("_MainTex") ? "_MainTex" : null;
            Texture texture = textureProperty != null ? source.GetTexture(textureProperty) : null;
            if (texture != null)
            {
                Texture2D black = BlackTexture(texture, $"{MaterialFolder}Volkolak_{texture.name}.png");
                material.SetTexture(textureProperty, black);
                if (textureProperty == "_BaseMap" && material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", black);
                }
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D BlackTexture(Texture source, string path)
        {
            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, target);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);

            var fur = new Color(0.13f, 0.12f, 0.12f);
            var marking = new Color(0.36f, 0.07f, 0.04f);
            Color[] pixels = readable.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float light = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.4f, pixels[i].grayscale));
                Color result = Color.Lerp(marking, fur, light);
                result.a = pixels[i].a;
                pixels[i] = result;
            }

            readable.SetPixels(pixels);
            System.IO.File.WriteAllBytes(path, readable.EncodeToPNG());
            Object.DestroyImmediate(readable);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (source is Texture2D && importer != null)
            {
                importer.filterMode = source.filterMode;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void SetCreatures(BiomeDefinition biome, GameObject volkolak)
        {
            var serialized = new SerializedObject(biome);
            SerializedProperty list = serialized.FindProperty("_creatureSpawns");
            list.arraySize = 1;
            SerializedProperty entry = list.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("_prefab").objectReferenceValue = volkolak;
            entry.FindPropertyRelative("_count").intValue = 2;
            entry.FindPropertyRelative("_nightCount").intValue = 6;
            entry.FindPropertyRelative("_groupSize").vector2IntValue = new Vector2Int(2, 3);
            entry.FindPropertyRelative("_respawnSeconds").floatValue = 140f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(biome);
        }

        // ---- Stations ----

        /// <summary>Adds (or updates) one recipe in a built station prefab's ProcessingStation -
        /// the station itself stays as its own setup made it.</summary>
        private static void AddStationRecipe(string prefabPath, ItemDefinition input, int inputAmount, ItemDefinition output, int outputAmount, float seconds)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var station = root.GetComponentInChildren<ProcessingStation>(true);
                var serialized = new SerializedObject(station);
                SerializedProperty list = serialized.FindProperty("_recipes");
                SerializedProperty entry = null;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("_input").objectReferenceValue == input)
                    {
                        entry = list.GetArrayElementAtIndex(i);
                    }
                }

                if (entry == null)
                {
                    list.arraySize++;
                    entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                }

                entry.FindPropertyRelative("_input").objectReferenceValue = input;
                entry.FindPropertyRelative("_inputAmount").intValue = inputAmount;
                entry.FindPropertyRelative("_output").objectReferenceValue = output;
                entry.FindPropertyRelative("_outputAmount").intValue = outputAmount;
                entry.FindPropertyRelative("_seconds").floatValue = seconds;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetUnlock(BuildingPieceDefinition piece, ItemDefinition item)
        {
            var serialized = new SerializedObject(piece);
            SerializedProperty unlock = serialized.FindProperty("_unlockItems");
            unlock.arraySize = 1;
            unlock.GetArrayElementAtIndex(0).objectReferenceValue = item;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(piece);
        }

        /// <summary>A bog-oak barrel of black water bound with iron hoops, tongs over its rim.</summary>
        private static void BuildQuenchTubModel(Transform root)
        {
            Material iron = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "Iron_material.mat");
            Material water = MetallurgySetup.Mat("QuenchWater_material", new Color(0.05f, 0.06f, 0.08f), 0.95f, 0f);
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Barrel", _bogOak, new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(1f, 0.45f, 1f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Water", water, new Vector3(0f, 0.86f, 0f), Vector3.zero, new Vector3(0.9f, 0.01f, 0.9f));
            foreach (float y in new[] { 0.15f, 0.5f, 0.82f })
            {
                CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Hoop", iron, new Vector3(0f, y, 0f), Vector3.zero, new Vector3(1.04f, 0.03f, 1.04f));
            }

            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "TongL", iron, new Vector3(0.3f, 0.95f, 0.05f), new Vector3(0f, 20f, 30f), new Vector3(0.6f, 0.03f, 0.03f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "TongR", iron, new Vector3(0.3f, 0.95f, -0.05f), new Vector3(0f, -20f, 30f), new Vector3(0.6f, 0.03f, 0.03f));
            StripChildColliders(root);
        }

        /// <summary>A heavy bog-oak table with carved legs, a half-carved panel, chisels and a mallet.</summary>
        private static void BuildCarvedTableModel(Transform root)
        {
            Material iron = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "Iron_material.mat");
            OakSetup.Box(root, "Top", _bogOak, new Vector3(0f, 0.86f, 0f), new Vector3(1.8f, 0.12f, 0.8f));
            foreach (float x in new[] { -0.75f, 0.75f })
            {
                foreach (float z in new[] { -0.28f, 0.28f })
                {
                    OakSetup.Box(root, "Leg", _bogOak, new Vector3(x, 0.4f, z), new Vector3(0.14f, 0.8f, 0.14f));
                    OakSetup.Box(root, "LegCarving", _carving, new Vector3(x, 0.55f, z), new Vector3(0.17f, 0.06f, 0.17f), new Vector3(0f, 45f, 0f));
                }
            }

            OakSetup.Box(root, "Panel", _bogOak, new Vector3(-0.2f, 0.94f, 0f), new Vector3(0.9f, 0.04f, 0.5f));
            OakSetup.Box(root, "PanelSun", _carving, new Vector3(-0.2f, 0.965f, 0f), new Vector3(0.24f, 0.012f, 0.24f), new Vector3(0f, 45f, 0f));
            OakSetup.Box(root, "PanelBorder", _carving, new Vector3(-0.2f, 0.963f, 0.22f), new Vector3(0.86f, 0.01f, 0.03f));
            OakSetup.Box(root, "Chisel", iron, new Vector3(0.55f, 0.94f, 0.15f), new Vector3(0.26f, 0.02f, 0.03f), new Vector3(0f, 20f, 0f));
            OakSetup.Box(root, "Gouge", iron, new Vector3(0.6f, 0.94f, -0.05f), new Vector3(0.24f, 0.02f, 0.03f), new Vector3(0f, -10f, 0f));
            OakSetup.Box(root, "Mallet", _carving, new Vector3(0.55f, 0.97f, -0.25f), new Vector3(0.16f, 0.1f, 0.1f));
            StripChildColliders(root);
        }

        // SmithingSetup.BuildPiece puts one box over the whole model.
        private static void StripChildColliders(Transform root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            {
                Object.DestroyImmediate(collider);
            }
        }

        // ---- The bog-oak set ----

        /// <summary>A round bog-oak log along X (visual only).</summary>
        private static void Log(Transform parent, string name, Vector3 center, float length, float radius)
        {
            CreatureLootSetup.AddPart(parent, PrimitiveType.Cylinder, name, _bogOak, center, new Vector3(0f, 0f, 90f), new Vector3(radius * 2f, length * 0.5f, radius * 2f));
        }

        private static void WallSnaps(Transform root)
        {
            OakSetup.Snap(root, "SnapPoint_Bottom", new Vector3(0f, -1f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
            OakSetup.Snap(root, "SnapPoint_BottomLeft", new Vector3(-1f, -1f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
            OakSetup.Snap(root, "SnapPoint_BottomRight", new Vector3(1f, -1f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
            OakSetup.Snap(root, "SnapPoint_TopLeft", new Vector3(-1f, 1f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
            OakSetup.Snap(root, "SnapPoint_TopRight", new Vector3(1f, 1f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
        }

        /// <summary>Five black logs with carved ends sticking out (as a terem's corners).</summary>
        private static void BuildBogOakWall(Transform root)
        {
            for (int i = 0; i < 5; i++)
            {
                Log(root, $"Log{i}", new Vector3(0f, -0.8f + i * 0.4f, 0f), 2.2f, 0.2f);
            }

            foreach (float x in new[] { -1.1f, 1.1f })
            {
                Object.DestroyImmediate(OakSetup.Box(root, "EndCarving", _carving, new Vector3(x, 0f, 0f), new Vector3(0.04f, 1.9f, 0.3f)).GetComponent<Collider>());
            }

            var box = root.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(2f, 2f, 0.3f);
            WallSnaps(root);
        }

        /// <summary>A 4 m pillar (two storeys) carved with bands and a capital; root at its
        /// centre, bottom to a foundation corner, top for beams.</summary>
        private static void BuildCarvedPillar(Transform root)
        {
            OakSetup.Box(root, "Pillar", _bogOak, Vector3.zero, new Vector3(0.36f, 4f, 0.36f));
            foreach (float y in new[] { -1.4f, -0.4f, 0.6f, 1.5f })
            {
                Object.DestroyImmediate(OakSetup.Box(root, "Band", _carving, new Vector3(0f, y, 0f), new Vector3(0.4f, 0.08f, 0.4f), new Vector3(0f, 45f, 0f)).GetComponent<Collider>());
            }

            OakSetup.Box(root, "Capital", _carving, new Vector3(0f, 1.94f, 0f), new Vector3(0.5f, 0.12f, 0.5f));
            OakSetup.Box(root, "Base", _bogOak, new Vector3(0f, -1.93f, 0f), new Vector3(0.48f, 0.14f, 0.48f));
            OakSetup.Snap(root, "SnapPoint_Bottom", new Vector3(0f, -2f, 0f), 0f, BuildingSnapPoint.SnapKind.Corner);
            OakSetup.Snap(root, "SnapPoint_Middle", new Vector3(0f, 0f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
            OakSetup.Snap(root, "SnapPoint_Top", new Vector3(0f, 2f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
        }

        /// <summary>A 2 m beam with a carved half-round arch hanging under it - sits on two
        /// posts or pillars by its ends, like the oak beam.</summary>
        private static void BuildCarvedArch(Transform root)
        {
            OakSetup.Box(root, "Beam", _bogOak, Vector3.zero, new Vector3(2f, 0.26f, 0.26f));
            // The arch's curve: low at the ends (0.85 m from the middle, 0.8 m under the beam),
            // touching the beam's underside in the middle - boxes laid chord to chord along it.
            const int segments = 8;
            for (int i = 0; i < segments; i++)
            {
                Vector3 a = ArchPoint((float)i / segments);
                Vector3 b = ArchPoint((float)(i + 1) / segments);
                float tilt = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                Object.DestroyImmediate(OakSetup.Box(root, $"Arc{i}", _carving, (a + b) * 0.5f, new Vector3((b - a).magnitude + 0.04f, 0.1f, 0.18f), new Vector3(0f, 0f, tilt)).GetComponent<Collider>());
            }

            Object.DestroyImmediate(OakSetup.Box(root, "Keystone", _bogOak, new Vector3(0f, -0.24f, 0f), new Vector3(0.16f, 0.16f, 0.22f), new Vector3(0f, 0f, 45f)).GetComponent<Collider>());
            foreach (float x in new[] { -0.85f, 0.85f })
            {
                Object.DestroyImmediate(OakSetup.Box(root, "Spandrel", _bogOak, new Vector3(x, -0.5f, 0f), new Vector3(0.12f, 0.75f, 0.16f)).GetComponent<Collider>());
            }
            OakSetup.Snap(root, "SnapPoint_EndL", new Vector3(-1f, -0.13f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
            OakSetup.Snap(root, "SnapPoint_EndR", new Vector3(1f, -0.13f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
        }

        // t 0..1 from the left end to the right one; a half-ellipse 0.85 m wide each way, 0.62 m tall.
        private static Vector3 ArchPoint(float t)
        {
            float angle = Mathf.PI * (1f - t);
            return new Vector3(Mathf.Cos(angle) * 0.85f, -0.83f + Mathf.Sin(angle) * 0.62f, 0f);
        }

        /// <summary>A tent roof (шатёр) over a 4 x 4 m room: a tall four-sided pyramid of bog-oak
        /// shingles with eaves, carved ribs on its edges and a finial. Root at the middle of the
        /// wall tops; its corners and side middles sit on wall tops.</summary>
        private static void BuildTentRoof(Transform root)
        {
            var shell = new GameObject("Shell");
            shell.transform.SetParent(root, false);
            Mesh mesh = TentRoofMesh();
            shell.AddComponent<MeshFilter>().sharedMesh = mesh;
            shell.AddComponent<MeshRenderer>().sharedMaterial = _bogOak;
            var collider = shell.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;

            const float apex = 3.4f;
            const float half = 2.25f;
            foreach (Vector2 corner in new[] { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f) })
            {
                var from = new Vector3(corner.x * half, -0.15f, corner.y * half);
                var to = new Vector3(0f, apex, 0f);
                Vector3 middle = (from + to) * 0.5f;
                var rib = OakSetup.Box(root, "Rib", _carving, middle, new Vector3(0.1f, 0.08f, (to - from).magnitude));
                rib.transform.localRotation = Quaternion.LookRotation(to - from);
                Object.DestroyImmediate(rib.GetComponent<Collider>());
            }

            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "FinialPost", _carving, new Vector3(0f, apex + 0.25f, 0f), Vector3.zero, new Vector3(0.12f, 0.3f, 0.12f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Sphere, "FinialBall", _carving, new Vector3(0f, apex + 0.6f, 0f), Vector3.zero, new Vector3(0.24f, 0.24f, 0.24f));
            foreach (Collider part in root.GetComponentsInChildren<Collider>())
            {
                if (part.gameObject != shell)
                {
                    Object.DestroyImmediate(part);
                }
            }

            foreach (Vector2 corner in new[] { new Vector2(2f, 2f), new Vector2(-2f, 2f), new Vector2(2f, -2f), new Vector2(-2f, -2f) })
            {
                OakSetup.Snap(root, "SnapPoint_Corner", new Vector3(corner.x, 0f, corner.y), 0f, BuildingSnapPoint.SnapKind.Edge);
            }

            OakSetup.Snap(root, "SnapPoint_North", new Vector3(0f, 0f, 2f), 0f, BuildingSnapPoint.SnapKind.Edge);
            OakSetup.Snap(root, "SnapPoint_South", new Vector3(0f, 0f, -2f), 0f, BuildingSnapPoint.SnapKind.Edge);
            OakSetup.Snap(root, "SnapPoint_East", new Vector3(2f, 0f, 0f), 90f, BuildingSnapPoint.SnapKind.Edge);
            OakSetup.Snap(root, "SnapPoint_West", new Vector3(-2f, 0f, 0f), 90f, BuildingSnapPoint.SnapKind.Edge);
        }

        /// <summary>Square pyramid, base 4.5 m (0.25 m eaves over a 4 m room) at y -0.15, apex
        /// 3.4 m; flat-shaded faces and an underside, saved as an asset.</summary>
        private static Mesh TentRoofMesh()
        {
            string path = MeshFolder + "TentRoof.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "TentRoof" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            const float half = 2.25f;
            const float eave = -0.15f;
            var apex = new Vector3(0f, 3.4f, 0f);
            Vector3[] corners =
            {
                new(-half, eave, -half), new(half, eave, -half), new(half, eave, half), new(-half, eave, half),
            };
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = corners[i];
                Vector3 b = corners[(i + 1) % 4];
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(apex);
                vertices.Add(b);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(0.5f, 1f));
                uvs.Add(new Vector2(1f, 0f));
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }

            // Underside (seen from inside the room).
            int under = vertices.Count;
            foreach (Vector3 corner in corners)
            {
                vertices.Add(corner);
                uvs.Add(new Vector2(corner.x, corner.z));
            }

            triangles.AddRange(new[] { under, under + 1, under + 2, under, under + 2, under + 3 });

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>The oak gate's layout in bog oak with black steel bands and studs - a 4 m
        /// double gate between two posts under a carved lintel; opens (BuildingDoor).</summary>
        private static void BuildBlackGate(Transform root)
        {
            foreach (float x in new[] { -2.15f, 2.15f })
            {
                OakSetup.Box(root, x < 0f ? "PostL" : "PostR", _bogOak, new Vector3(x, 1.4f, 0f), new Vector3(0.34f, 2.8f, 0.34f));
                Object.DestroyImmediate(OakSetup.Box(root, "PostCap", _carving, new Vector3(x, 2.86f, 0f), new Vector3(0.42f, 0.12f, 0.42f)).GetComponent<Collider>());
            }

            OakSetup.Box(root, "Lintel", _bogOak, new Vector3(0f, 2.65f, 0f), new Vector3(4.7f, 0.28f, 0.34f));
            Object.DestroyImmediate(OakSetup.Box(root, "Gable", _carving, new Vector3(0f, 2.95f, 0.02f), new Vector3(1.6f, 0.3f, 0.22f)).GetComponent<Collider>());
            foreach (float side in new[] { -1f, 1f })
            {
                var hinge = new GameObject(side < 0f ? "HingeL" : "HingeR").transform;
                hinge.SetParent(root, false);
                hinge.localPosition = new Vector3(2f * side, 0f, 0f);
                OakSetup.Box(hinge, "Leaf", _bogOak, new Vector3(-1f * side, 1.2f, 0f), new Vector3(1.98f, 2.3f, 0.14f));
                foreach (float y in new[] { 0.4f, 1.2f, 2f })
                {
                    Object.DestroyImmediate(OakSetup.Box(hinge, "Band", _blackSteel, new Vector3(-0.9f * side, y, 0.08f), new Vector3(1.75f, 0.12f, 0.02f)).GetComponent<Collider>());
                    for (int s = 0; s < 4; s++)
                    {
                        Object.DestroyImmediate(OakSetup.Box(hinge, "Stud", _blackSteel, new Vector3((-0.3f - s * 0.45f) * side, y, 0.1f), new Vector3(0.06f, 0.06f, 0.03f)).GetComponent<Collider>());
                    }
                }
            }

            OakSetup.Snap(root, "SnapPoint_BottomL", new Vector3(-1f, 0f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
            OakSetup.Snap(root, "SnapPoint_BottomR", new Vector3(1f, 0f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
        }

        // ---- Boss slot, scene ----

        private static void AddBossSlot(HybridWorldSettings settings, BiomeDefinition black)
        {
            string path = PoiFolder + "BlackForestBossSlot.asset";
            var poi = AssetDatabase.LoadAssetAtPath<PoiDefinition>(path);
            if (poi == null)
            {
                poi = ScriptableObject.CreateInstance<PoiDefinition>();
                AssetDatabase.CreateAsset(poi, path);
            }

            poi.EditorConfigure("BlackForestBossSlot", "Boss arena slot - tier 6 (Black Forest)", new Color(0.45f, 0.2f, 0.5f), 1, 20,
                new List<BiomeDefinition> { black }, true, new Vector2(2f, 10000f), new Vector2(0f, 0.85f), 600f, 0f);
            var serialized = new SerializedObject(poi);
            serialized.FindProperty("_prefab").objectReferenceValue = Load(ArenaSlotPrefabPath);
            serialized.FindProperty("_clearRadius").floatValue = 16f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(poi);
            CreatureLootSetup.AddToList(new SerializedObject(settings), "_pointsOfInterest", poi);
        }

        private static void WireScene(List<RecipeDefinition> recipes)
        {
            PlayerCrafting crafting = Object.FindAnyObjectByType<PlayerCrafting>(FindObjectsInactive.Include);
            if (crafting != null)
            {
                var craftingSerialized = new SerializedObject(crafting);
                foreach (RecipeDefinition recipe in recipes)
                {
                    CreatureLootSetup.AddToList(craftingSerialized, "_knownRecipes", recipe);
                }
            }
            else
            {
                Debug.LogWarning("BlackForestSetup: no PlayerCrafting in the open scene - the black steel recipes are not known to the player.");
            }

            DayNightGrading grading = Object.FindAnyObjectByType<DayNightGrading>(FindObjectsInactive.Include);
            AtmosphereFog atmosphere = Object.FindAnyObjectByType<AtmosphereFog>(FindObjectsInactive.Include);
            if (grading != null && atmosphere != null)
            {
                var serialized = new SerializedObject(grading);
                serialized.FindProperty("_atmosphere").objectReferenceValue = atmosphere;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("BlackForestSetup: DayNightGrading or AtmosphereFog not in the open scene - the black forest's dusk won't darken the colour grading.");
            }
        }
    }
}
