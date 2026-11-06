using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.AI;
using NAV.Gameplay.Building;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Environment;
using NAV.Gameplay.Equipment;
using NAV.Gameplay.Items;
using NAV.Gameplay.Player;
using NAV.World.Biomes;
using NAV.World.Generation.Hybrid;

namespace NAV.Editor
{
    /// <summary>
    /// Tier 5 - the Mountains (Каменный пояс, PROGRESSION_TIERS_v0.1.md), NAV > World > Build
    /// Mountains (Tier 5):
    ///   - Silver: veins in the mountains and on the snowy peaks (an iron pickaxe at least) -> Silver
    ///     Ore -> Silver Bar in the Smelting Furnace (плавильный горн, with charcoal), which is also
    ///     the Forge's 4th upgrade (level 5 - silver tools, weapons, armour);
    ///   - the Loom (ткацкий станок): the Workbench's 3rd upgrade (level 4 - fur hood and cloak, ArmorSetup);
    ///   - the Snow Leopard (the ithappy tiger, pale): a fast mountain hunter, drops Fur;
    ///   - Snowy Peaks: a new biome above 175 m (a height rule before the mountains), bitter cold;
    ///     the mountains are cold, permafrost bitter (BiomeDefinition Cold - PlayerCold);
    ///   - warmth: campfire, forge, bloomery, kiln and furnace are HeatSources; PlayerCold on the player;
    ///   - Silver Axe / Pickaxe (tool tier 5), the boss 5 arena slot (no boss);
    ///   - the Bog Lurker becomes нечисть (silver x1.5).
    /// Then run Build Armour (silver set, fur hood/cloak) and Build Pack Weapon Recipes (silver
    /// weapons). Models are placeholders. Re-runnable; saves the scene.
    /// </summary>
    internal static class MountainSetup
    {
        private const string ItemFolder = "Assets/NAV/ScriptableObjects/Items/";
        private const string BiomeFolder = "Assets/NAV/ScriptableObjects/Biomes/";
        private const string LayerFolder = "Assets/NAV/Art/Terrain/";
        private const string WorldPrefabs = "Assets/NAV/Prefabs/World/";
        private const string BuildPrefabs = "Assets/NAV/Prefabs/Build/";
        private const string CreaturePrefabs = "Assets/NAV/Prefabs/Creatures/";
        private const string CreatureDefinitions = "Assets/NAV/ScriptableObjects/Creatures/";
        private const string DatabasePath = "Assets/NAV/ScriptableObjects/Core/DefaultItemDatabase.asset";
        private const string PieceDatabasePath = "Assets/NAV/ScriptableObjects/Core/BuildingPieceDatabase.asset";
        private const string WorkbenchPath = "Assets/NAV/ScriptableObjects/Workbenches/BasicWorkbench.asset";
        private const string ForgePath = "Assets/NAV/ScriptableObjects/Workbenches/Forge.asset";
        private const string HybridSettingsPath = "Assets/NAV/ScriptableObjects/World/HybridWorldSettings.asset";
        private const string PoiFolder = "Assets/NAV/ScriptableObjects/World/Poi/";
        private const string ArenaSlotPrefabPath = "Assets/NAV/Prefabs/World/Poi/Poi_BossArenaSlot.prefab";
        private const string AudioFolder = "Assets/Nature - Essentials/";
        private const string SnowTexture = "Assets/ADG_Textures/ground_vol1/ground3/ground3_Diffuse.tga";
        private const float SnowLine = 175f;
        private const int SilverForgeLevel = 5;

        [MenuItem("NAV/World/Build Mountains (Tier 5)")]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("MountainSetup: exit Play Mode first.");
                return;
            }

            ItemDefinition rock = Item("Rock");
            ItemDefinition wood = Item("Wood");
            ItemDefinition leather = Item("Leather");
            ItemDefinition charcoal = Item("Charcoal");
            ItemDefinition bone = Item("Bone");
            ItemDefinition ironBar = Item("IronBar");
            var workbench = AssetDatabase.LoadAssetAtPath<WorkbenchDefinition>(WorkbenchPath);
            var forge = AssetDatabase.LoadAssetAtPath<WorkbenchDefinition>(ForgePath);
            var mountains = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(BiomeFolder + "MountainsBiome.asset");
            var permafrost = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(BiomeFolder + "PermafrostBiome.asset");
            var settings = AssetDatabase.LoadAssetAtPath<HybridWorldSettings>(HybridSettingsPath);
            if (rock == null || wood == null || leather == null || charcoal == null || ironBar == null || workbench == null || forge == null || mountains == null || settings == null)
            {
                Debug.LogError("MountainSetup: run the hybrid world, metallurgy and smithing setups first.");
                return;
            }

            var database = new SerializedObject(AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath));
            Material silver = MetallurgySetup.Mat("Silver_material", new Color(0.82f, 0.84f, 0.88f), 0.75f, 0.9f);
            Material silverDark = MetallurgySetup.Mat("SilverOre_material", new Color(0.55f, 0.57f, 0.6f), 0.5f, 0.6f);
            Material stone = MetallurgySetup.Mat("OreStone_material", new Color(0.45f, 0.44f, 0.42f), 0.1f, 0f);
            Material furPale = MetallurgySetup.Mat("SnowFur_material", new Color(0.86f, 0.85f, 0.8f), 0.05f, 0f);
            Material furSpot = MetallurgySetup.Mat("SnowFurSpot_material", new Color(0.3f, 0.29f, 0.28f), 0.05f, 0f);

            // ---- Items ----
            ItemDefinition silverOre = CreatureLootSetup.BuildItem("SilverOre", "Silver Ore",
                "Grey rock shot through with silver. Smelt it in the furnace with charcoal.", 30, 1.2f,
                root =>
                {
                    MetallurgySetup.Shard(root, stone, new Vector3(0f, 0.08f, 0f), new Vector3(0.3f, 0.22f, 0.28f), 10f);
                    MetallurgySetup.Shard(root, silver, new Vector3(0.07f, 0.12f, 0.05f), new Vector3(0.12f, 0.1f, 0.12f), 50f);
                    MetallurgySetup.Shard(root, silverDark, new Vector3(-0.07f, 0.11f, -0.05f), new Vector3(0.1f, 0.08f, 0.1f), 120f);
                }, Quaternion.Euler(-30f, 30f, 0f));
            ItemDefinition silverBar = CreatureLootSetup.BuildItem("SilverBar", "Silver Bar",
                "A bright bar of silver. Нечисть can't abide it.", 30, 1f, root => MetallurgySetup.Ingot(root, silver), Quaternion.Euler(-30f, 30f, 0f));
            ItemDefinition fur = CreatureLootSetup.BuildItem("Fur", "Fur",
                "A thick snow-leopard pelt. Woven into hoods and cloaks on the loom, it keeps the mountain cold off.", 30, 0.6f,
                root =>
                {
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Pelt", furPale, new Vector3(0f, 0.04f, 0f), new Vector3(0f, 15f, 0f), new Vector3(0.34f, 0.08f, 0.26f));
                    for (int i = 0; i < 5; i++)
                    {
                        var spot = new Vector3(-0.12f + i * 0.06f, 0.081f, (i % 2 == 0 ? -0.05f : 0.05f));
                        CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Spot", furSpot, spot, Vector3.zero, new Vector3(0.04f, 0.002f, 0.04f));
                    }
                }, Quaternion.Euler(-30f, 20f, 0f));
            foreach (ItemDefinition item in new[] { silverOre, silverBar, fur })
            {
                CreatureLootSetup.AddToList(database, "_items", item);
            }

            // ---- Silver veins (an iron pickaxe - tier 4) ----
            GameObject silverRock = MetallurgySetup.BuildOreRock("SilverOre_Rock", "SilverDeposit", "Silver Vein", 6,
                new[] { (silverOre, 2, 1f), (silverOre, 1, 0.5f), (rock, 2, 1f) }, null, new[] { silver, silverDark });
            MetallurgySetup.SetMinToolTier("SilverDeposit", 4);

            // ---- Biomes ----
            BiomeDefinition snowPeaks = SnowPeaksBiome();
            ConfigureMountains(mountains);
            SetCold(mountains, 1f);
            SetCold(snowPeaks, 1.5f);
            if (permafrost != null)
            {
                SetCold(permafrost, 1.5f);
            }

            GameObject boulder = Load(WorldPrefabs + "SM_Rocks_03.prefab");
            GameObject looseStone = Load(WorldPrefabs + "LooseStone.prefab");
            GameObject spruce = Load(WorldPrefabs + "UNS_Spruce_WoodNode.prefab");
            GameObject deadTree = Load(WorldPrefabs + "Swamp/DeadTree_C.prefab");
            var anyHeight = new Vector2(-1000f, 1000f);
            MetallurgySetup.UpsertResource(mountains.name, silverRock, 0.12f, 16f, new Vector2(0.9f, 1.4f), anyHeight);
            MetallurgySetup.UpsertResource(mountains.name, boulder, 0.2f, 14f, new Vector2(1.2f, 2.6f), anyHeight);
            MetallurgySetup.UpsertResource(mountains.name, looseStone, 0.06f, 16f, Vector2.one, anyHeight);
            MetallurgySetup.UpsertResource(mountains.name, spruce, 0.08f, 12f, new Vector2(0.45f, 0.75f), new Vector2(0f, 150f));
            if (deadTree != null)
            {
                MetallurgySetup.UpsertResource(mountains.name, deadTree, 0.03f, 18f, new Vector2(0.6f, 0.9f), anyHeight);
            }

            MetallurgySetup.UpsertResource(snowPeaks.name, silverRock, 0.15f, 14f, new Vector2(0.9f, 1.4f), anyHeight);
            MetallurgySetup.UpsertResource(snowPeaks.name, boulder, 0.12f, 16f, new Vector2(1.2f, 2.4f), anyHeight);

            AddSnowLine(settings, snowPeaks);

            // ---- The snow leopard ----
            GameObject leopard = BuildSnowLeopard(fur, bone);
            SetCreatures(mountains, leopard, 2, 3);
            SetCreatures(snowPeaks, leopard, 1, 2);

            // ---- Stations ----
            BuildingPieceDefinition furnace = MetallurgySetup.BuildStation("SmeltingFurnace", "Smelting Furnace",
                new[] { (rock, 20), (ironBar, 4), (wood, 4) },
                new[] { MetallurgySetup.Recipe(silverOre, 1, silverBar, 1, 40f) }, charcoal, 2, 10, 20,
                BuildFurnaceModel);
            MetallurgySetup.MakeExtension(furnace.Prefab, forge, "SmeltingFurnace");
            SetUnlock(furnace, silverOre);

            BuildingPieceDefinition loom = SmithingSetup.BuildPiece("Loom", "Loom",
                new[] { (wood, 10), (leather, 2), (ironBar, 1) }, BuildLoomModel,
                root => SmithingSetup.AddExtension(root, workbench, "Loom"));
            SetUnlock(loom, fur);

            var pieceDatabase = new SerializedObject(AssetDatabase.LoadAssetAtPath<BuildingPieceDatabase>(PieceDatabasePath));
            PlayerBuilding building = Object.FindAnyObjectByType<PlayerBuilding>(FindObjectsInactive.Include);
            foreach (BuildingPieceDefinition piece in new[] { furnace, loom })
            {
                CreatureLootSetup.AddToList(pieceDatabase, "_pieces", piece);
                if (building != null)
                {
                    CreatureLootSetup.AddToList(new SerializedObject(building), "_knownPieces", piece);
                }
            }

            // ---- Warmth: fires and furnaces ----
            AddHeat(BuildPrefabs + "Campfire.prefab", 5f);
            AddHeat(BuildPrefabs + "Forge.prefab", 4f);
            AddHeat(BuildPrefabs + "Bloomery.prefab", 4f);
            AddHeat(BuildPrefabs + "CharcoalKiln.prefab", 4f);
            AddHeat(BuildPrefabs + "SmeltingFurnace.prefab", 5f);

            // ---- Silver tools ----
            SmithingSetup.Metal silverTint = SmithingSetup.TintedToolMaterial("Silver", new Color(1.45f, 1.5f, 1.6f), new Color(0.8f, 0.82f, 0.86f));
            ItemDefinition silverAxe = SmithingSetup.BuildTool("StoneAxe", "SilverAxe", "Silver Axe", "A silver-edged axe - fells the mountain's old pines.", 5, 2.7f, silverTint);
            ItemDefinition silverPick = SmithingSetup.BuildTool("StonePickaxe", "SilverPickaxe", "Silver Pickaxe", "A silver pickaxe for what lies deeper.", 5, 2.7f, silverTint);
            var recipes = new List<RecipeDefinition>
            {
                SmithingSetup.BuildRecipe("SilverAxeRecipe", silverAxe, forge, SilverForgeLevel, (silverBar, 3), (wood, 2), (leather, 1)),
                SmithingSetup.BuildRecipe("SilverPickaxeRecipe", silverPick, forge, SilverForgeLevel, (silverBar, 3), (wood, 2), (leather, 2)),
            };
            CreatureLootSetup.AddToList(database, "_items", silverAxe);
            CreatureLootSetup.AddToList(database, "_items", silverPick);

            // ---- Boss 5 arena slot ----
            AddBossSlot(settings, mountains, snowPeaks);

            // ---- The player feels the cold; the bog lurker is нечисть ----
            WirePlayer(recipes);
            SetUnclean("BogLurkerCreature");

            AssetDatabase.SaveAssets();
            if (building != null)
            {
                EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
                EditorSceneManager.SaveScene(building.gameObject.scene);
            }

            Debug.Log("MountainSetup: silver (veins, ore, bars, furnace), loom, fur, snow leopard, snowy peaks, cold and warmth, silver tools, boss 5 slot. Now run Build Armour and Build Pack Weapon Recipes for the silver and fur gear.");
        }

        private static ItemDefinition Item(string id) => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}{id}.asset");

        private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        private static BiomeDefinition SnowPeaksBiome()
        {
            string layerPath = LayerFolder + "SnowPeaksLayer.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }

            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SnowTexture);
            layer.tileSize = new Vector2(4f, 4f);
            // The frosty ground pushed to near white - snow over rock.
            layer.diffuseRemapMax = new Vector4(1.5f, 1.52f, 1.58f, 1f);
            EditorUtility.SetDirty(layer);

            string path = BiomeFolder + "SnowPeaksBiome.asset";
            var biome = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(path);
            if (biome == null)
            {
                biome = ScriptableObject.CreateInstance<BiomeDefinition>();
                AssetDatabase.CreateAsset(biome, path);
            }

            var serialized = new SerializedObject(biome);
            serialized.FindProperty("_displayName").stringValue = "Snowy Peaks";
            serialized.FindProperty("_terrainLayer").objectReferenceValue = layer;
            serialized.FindProperty("_mapColor").colorValue = new Color(0.95f, 0.96f, 0.98f);
            serialized.FindProperty("_heightVariation").floatValue = 1f;
            serialized.FindProperty("_grassDensity").floatValue = 0f;
            serialized.FindProperty("_flowerDensity").floatValue = 0f;
            serialized.FindProperty("_fogDensity").floatValue = 0.01f;
            serialized.FindProperty("_fogColor").colorValue = new Color(0.85f, 0.88f, 0.92f, 0.6f);
            AudioClip wind = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "Ambiance_Wind_Calm_Loop_Stereo.wav");
            serialized.FindProperty("_dayAmbience").objectReferenceValue = wind;
            serialized.FindProperty("_nightAmbience").objectReferenceValue = wind;
            serialized.FindProperty("_ambienceVolume").floatValue = 0.35f;
            serialized.FindProperty("_windAmbience").objectReferenceValue = wind;
            serialized.FindProperty("_windVolume").floatValue = 0.7f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(biome);
            return biome;
        }

        private static void ConfigureMountains(BiomeDefinition mountains)
        {
            var serialized = new SerializedObject(mountains);
            serialized.FindProperty("_displayName").stringValue = "Mountains";
            serialized.FindProperty("_grassDensity").floatValue = 0.12f;
            serialized.FindProperty("_fogDensity").floatValue = 0.004f;
            serialized.FindProperty("_fogColor").colorValue = new Color(0.7f, 0.74f, 0.8f, 0.4f);
            AudioClip wind = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "Ambiance_Wind_Calm_Loop_Stereo.wav");
            serialized.FindProperty("_dayAmbience").objectReferenceValue = wind;
            serialized.FindProperty("_nightAmbience").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "Ambiance_Night_Loop_Stereo.wav");
            serialized.FindProperty("_ambienceVolume").floatValue = 0.3f;
            serialized.FindProperty("_windAmbience").objectReferenceValue = wind;
            serialized.FindProperty("_windVolume").floatValue = 0.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mountains);
        }

        private static void SetCold(BiomeDefinition biome, float cold)
        {
            var serialized = new SerializedObject(biome);
            serialized.FindProperty("_cold").floatValue = cold;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(biome);
        }

        /// <summary>The snowy peaks take the ground above the snow line - a height rule checked
        /// before the mountains' own.</summary>
        private static void AddSnowLine(HybridWorldSettings settings, BiomeDefinition snowPeaks)
        {
            var serialized = new SerializedObject(settings);
            SerializedProperty rules = serialized.FindProperty("_heightRules");
            for (int i = rules.arraySize - 1; i >= 0; i--)
            {
                if (rules.GetArrayElementAtIndex(i).FindPropertyRelative("_biome").objectReferenceValue == snowPeaks)
                {
                    rules.DeleteArrayElementAtIndex(i);
                }
            }

            rules.InsertArrayElementAtIndex(0);
            SerializedProperty rule = rules.GetArrayElementAtIndex(0);
            rule.FindPropertyRelative("_biome").objectReferenceValue = snowPeaks;
            rule.FindPropertyRelative("_minHeight").floatValue = SnowLine;
            rule.FindPropertyRelative("_maxHeight").floatValue = 10000f;
            rule.FindPropertyRelative("_landmass").enumValueIndex = (int)LandmassFilter.Any;
            rule.FindPropertyRelative("_coverage").floatValue = 1f;
            rule.FindPropertyRelative("_excludePolar").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        /// <summary>The snow leopard: the wolf's prefab and definition copied, the ithappy tiger as
        /// its model, pale with dark spots; a fast lone hunter that drops fur.</summary>
        private static GameObject BuildSnowLeopard(ItemDefinition fur, ItemDefinition bone)
        {
            string prefabPath = CreaturePrefabs + "SnowLeopard.prefab";
            string definitionPath = CreatureDefinitions + "SnowLeopardCreature.asset";
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
                Prefab = "SnowLeopard", Definition = "SnowLeopardCreature", DisplayName = null, Model = "Tiger_001",
                Scale = 0.62f, BodyHalfWidth = 0.3f, BodyLength = 1.8f, BodyTop = 1.15f,
                WalkClipSpeed = 0.62f, RunClipSpeed = 2.5f,
                MoveSpeed = 1.6f, ChaseMultiplier = 4.2f, AttackRange = 1.6f, // stalks slowly, charges ~6.7 m/s
            };
            CreatureModelSetup.ConfigureDefinition(definitionPath, animal);
            CreatureModelSetup.BuildPrefab(prefabPath, animal);

            var definition = AssetDatabase.LoadAssetAtPath<CreatureDefinition>(definitionPath);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_displayName").stringValue = "Snow Leopard";
            serialized.FindProperty("_isHostile").boolValue = true;
            serialized.FindProperty("_isUnclean").boolValue = false;
            serialized.FindProperty("_maxHealth").floatValue = 90f;
            serialized.FindProperty("_attackDamage").floatValue = 16f;
            serialized.FindProperty("_attackCooldown").floatValue = 1.3f;
            serialized.FindProperty("_poisonDamage").floatValue = 0f;
            serialized.FindProperty("_detectionRadius").floatValue = 14f;
            serialized.FindProperty("_packAlertRadius").floatValue = 0f;
            SerializedProperty loot = serialized.FindProperty("_lootDrops");
            loot.arraySize = 2;
            SetDrop(loot, 0, fur, 2, 1f);
            SetDrop(loot, 1, bone, 1, 0.6f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                root.name = "SnowLeopard";
                var creatureSerialized = new SerializedObject(root.GetComponent<Creature>());
                creatureSerialized.FindProperty("_definition").objectReferenceValue = definition;
                creatureSerialized.ApplyModifiedPropertiesWithoutUndo();
                foreach (Renderer renderer in root.transform.Find("Model").GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i] = PaleCopy(materials[i]);
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

        // The tiger's own texture (its stripes become the leopard's markings), washed pale grey.
        private static Material PaleCopy(Material source)
        {
            if (source == null)
            {
                return null;
            }

            string path = $"Assets/NAV/Materials/Items/SnowLeopard_{source.name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = $"SnowLeopard_{source.name}" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.CopyPropertiesFromMaterial(source);
            string colour = source.HasProperty("_BaseColor") ? "_BaseColor" : source.HasProperty("_Color") ? "_Color" : null;
            if (colour != null)
            {
                material.SetColor(colour, Color.white);
            }

            // The model is coloured by a palette texture, so a tint alone leaves it orange: bake a
            // pale copy of the texture (light fur, the dark stripes kept as grey markings).
            string textureProperty = source.HasProperty("_BaseMap") ? "_BaseMap" : source.HasProperty("_MainTex") ? "_MainTex" : null;
            Texture texture = textureProperty != null ? source.GetTexture(textureProperty) : null;
            if (texture != null)
            {
                Texture2D pale = PaleTexture(texture, $"Assets/NAV/Materials/Items/SnowLeopard_{texture.name}.png");
                material.SetTexture(textureProperty, pale);
                if (textureProperty == "_BaseMap" && material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", pale);
                }
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D PaleTexture(Texture source, string path)
        {
            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, target);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);

            Color[] pixels = readable.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float grey = pixels[i].grayscale;
                float value = Mathf.Lerp(0.22f, 0.9f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.4f, grey)));
                pixels[i] = new Color(value, value * 0.98f, value * 0.94f, pixels[i].a);
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

        private static void SetDrop(SerializedProperty list, int index, ItemDefinition item, int amount, float chance)
        {
            SerializedProperty drop = list.GetArrayElementAtIndex(index);
            drop.FindPropertyRelative("_item").objectReferenceValue = item;
            drop.FindPropertyRelative("_amount").intValue = amount;
            drop.FindPropertyRelative("_chance").floatValue = chance;
        }

        private static void SetCreatures(BiomeDefinition biome, GameObject prefab, int day, int night)
        {
            var serialized = new SerializedObject(biome);
            SerializedProperty list = serialized.FindProperty("_creatureSpawns");
            list.arraySize = 1;
            SerializedProperty entry = list.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("_prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("_count").intValue = day;
            entry.FindPropertyRelative("_nightCount").intValue = night;
            entry.FindPropertyRelative("_groupSize").vector2IntValue = new Vector2Int(1, 1);
            entry.FindPropertyRelative("_respawnSeconds").floatValue = 150f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(biome);
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

        /// <summary>A tall stone furnace (плавильный горн): a round stone base, a tapering shaft,
        /// a chimney, a stoke-hole that glows while it works.</summary>
        private static void BuildFurnaceModel(Transform root, GameObject working, GameObject loaded)
        {
            Material stone = MetallurgySetup.Mat("FurnaceStone_material", new Color(0.42f, 0.4f, 0.38f), 0.1f, 0f);
            Material clay = MetallurgySetup.Mat("FurnaceClay_material", new Color(0.55f, 0.36f, 0.24f), 0.15f, 0f);
            Material fire = MetallurgySetup.Mat("FurnaceFire_material", new Color(1f, 0.55f, 0.15f), 0.6f, 0f);
            fire.EnableKeyword("_EMISSION");
            fire.SetColor("_EmissionColor", new Color(3f, 1.4f, 0.3f));
            Material silver = MetallurgySetup.Mat("Silver_material", new Color(0.82f, 0.84f, 0.88f), 0.75f, 0.9f);

            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Base", stone, new Vector3(0f, 0.4f, 0f), Vector3.zero, new Vector3(1.6f, 0.4f, 1.6f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Shaft", clay, new Vector3(0f, 1.25f, 0f), Vector3.zero, new Vector3(1.2f, 0.5f, 1.2f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Neck", clay, new Vector3(0f, 1.95f, 0f), Vector3.zero, new Vector3(0.75f, 0.25f, 0.75f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Chimney", stone, new Vector3(0f, 2.55f, 0f), Vector3.zero, new Vector3(0.4f, 0.4f, 0.4f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "StokeHole", stone, new Vector3(0f, 0.45f, 0.76f), Vector3.zero, new Vector3(0.5f, 0.4f, 0.1f));

            working.transform.SetParent(root, false);
            CreatureLootSetup.AddPart(working.transform, PrimitiveType.Cube, "Glow", fire, new Vector3(0f, 0.45f, 0.8f), Vector3.zero, new Vector3(0.36f, 0.28f, 0.04f));
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(working.transform, false);
            light.transform.localPosition = new Vector3(0f, 0.6f, 1.1f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.25f);
            light.range = 5f;
            light.intensity = 2.5f;

            loaded.transform.SetParent(root, false);
            CreatureLootSetup.AddPart(loaded.transform, PrimitiveType.Cube, "Bar", silver, new Vector3(0.55f, 0.85f, 0.45f), new Vector3(0f, 30f, 0f), new Vector3(0.24f, 0.06f, 0.1f));
        }

        /// <summary>A simple upright loom: two posts, beams top and bottom, a warp of threads and a
        /// half-woven fur-and-wool cloth.</summary>
        private static void BuildLoomModel(Transform root)
        {
            Material woodMat = MetallurgySetup.Mat("LoomWood_material", new Color(0.45f, 0.32f, 0.2f), 0.2f, 0f);
            Material thread = MetallurgySetup.Mat("LoomThread_material", new Color(0.86f, 0.82f, 0.72f), 0.1f, 0f);
            Material cloth = MetallurgySetup.Mat("LoomCloth_material", new Color(0.6f, 0.5f, 0.38f), 0.05f, 0f);
            foreach (float x in new[] { -0.75f, 0.75f })
            {
                CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Post", woodMat, new Vector3(x, 0.95f, 0f), Vector3.zero, new Vector3(0.1f, 1.9f, 0.12f));
                CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Foot", woodMat, new Vector3(x, 0.04f, 0f), Vector3.zero, new Vector3(0.12f, 0.08f, 0.6f));
            }

            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "TopBeam", woodMat, new Vector3(0f, 1.8f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.09f, 0.78f, 0.09f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "ClothBeam", woodMat, new Vector3(0f, 0.6f, 0.05f), new Vector3(0f, 0f, 90f), new Vector3(0.09f, 0.78f, 0.09f));
            for (int i = 0; i < 13; i++)
            {
                float x = -0.6f + i * 0.1f;
                CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Warp", thread, new Vector3(x, 1.4f, 0f), Vector3.zero, new Vector3(0.008f, 0.4f, 0.008f));
            }

            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Cloth", cloth, new Vector3(0f, 0.82f, 0.02f), Vector3.zero, new Vector3(1.3f, 0.4f, 0.02f));
        }

        private static void AddHeat(string prefabPath, float radius)
        {
            if (Load(prefabPath) == null)
            {
                Debug.LogWarning($"MountainSetup: {prefabPath} not found - no warmth from it.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var heat = root.GetComponent<HeatSource>();
                if (heat == null)
                {
                    heat = root.AddComponent<HeatSource>();
                }

                var serialized = new SerializedObject(heat);
                serialized.FindProperty("_radius").floatValue = radius;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddBossSlot(HybridWorldSettings settings, BiomeDefinition mountains, BiomeDefinition snowPeaks)
        {
            string path = PoiFolder + "MountainBossSlot.asset";
            var poi = AssetDatabase.LoadAssetAtPath<PoiDefinition>(path);
            if (poi == null)
            {
                poi = ScriptableObject.CreateInstance<PoiDefinition>();
                AssetDatabase.CreateAsset(poi, path);
            }

            poi.EditorConfigure("MountainBossSlot", "Boss arena slot - tier 5 (Mountains)", new Color(0.7f, 0.85f, 1f), 1, 20,
                new List<BiomeDefinition> { mountains, snowPeaks }, true, new Vector2(100f, 10000f), new Vector2(0f, 0.85f), 600f, 0f);
            var serialized = new SerializedObject(poi);
            serialized.FindProperty("_prefab").objectReferenceValue = Load(ArenaSlotPrefabPath);
            serialized.FindProperty("_clearRadius").floatValue = 16f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(poi);
            CreatureLootSetup.AddToList(new SerializedObject(settings), "_pointsOfInterest", poi);
        }

        private static void WirePlayer(List<RecipeDefinition> recipes)
        {
            var health = Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (health != null)
            {
                var cold = health.GetComponent<PlayerCold>();
                if (cold == null)
                {
                    cold = health.gameObject.AddComponent<PlayerCold>();
                }

                var serialized = new SerializedObject(cold);
                serialized.FindProperty("_equipment").objectReferenceValue = health.GetComponent<PlayerEquipment>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("MountainSetup: no PlayerHealth in the open scene - add PlayerCold to the player by hand.");
            }

            PlayerCrafting crafting = Object.FindAnyObjectByType<PlayerCrafting>(FindObjectsInactive.Include);
            if (crafting != null)
            {
                var craftingSerialized = new SerializedObject(crafting);
                foreach (RecipeDefinition recipe in recipes)
                {
                    CreatureLootSetup.AddToList(craftingSerialized, "_knownRecipes", recipe);
                }
            }
        }

        private static void SetUnclean(string definitionName)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CreatureDefinition>($"{CreatureDefinitions}{definitionName}.asset");
            if (definition == null)
            {
                return;
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_isUnclean").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }
    }
}
