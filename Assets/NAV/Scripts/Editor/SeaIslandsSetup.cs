using UnityEditor;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.Items;
using NAV.Gameplay.Survival;
using NAV.World.Biomes;
using NAV.World.Generation;

namespace NAV.Editor
{
    /// <summary>
    /// Tier 3 - the sea islands (PROGRESSION_TIERS_v0.1.md), NAV > World > Build Sea Islands (Tier 3):
    ///   - SeaIslandsBiome: rocky islands out in the open sea around the round world
    ///     (WorldGenerator.ShapeCoast places them) - sandy-stony ground, wind-bent spruces and
    ///     cedars, boulders, bushes, and on the shore driftwood and shellfish;
    ///   - tin ore lives only here now (taken off the plains, forest and lake shores);
    ///   - Driftwood: a bleached log lying on the beach, chopped with any axe for wood;
    ///   - Shellfish (pickup on the shore) -> Cooked Shellfish on the campfire spit (a food with
    ///     more stamina than meat);
    ///   - sound: the sea day and night, stronger wind; a light sea haze.
    /// Safe to re-run.
    /// </summary>
    internal static class SeaIslandsSetup
    {
        private const string BiomeFolder = "Assets/NAV/ScriptableObjects/Biomes/";
        private const string IslandBiomePath = BiomeFolder + "SeaIslandsBiome.asset";
        private const string IslandLayerPath = "Assets/NAV/Art/Terrain/SeaIslandsLayer.terrainlayer";
        private const string SettingsPath = "Assets/NAV/ScriptableObjects/World/DefaultWorldGenerationSettings.asset";
        private const string FoodFolder = "Assets/NAV/ScriptableObjects/Food/";
        private const string CampfirePath = "Assets/NAV/Prefabs/Build/Campfire.prefab";

        [MenuItem("NAV/World/Build Sea Islands (Tier 3)")]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("SeaIslandsSetup: exit Play Mode first.");
                return;
            }

            GameObject spruce = Load("Assets/NAV/Prefabs/World/UNS_Spruce_WoodNode.prefab");
            GameObject cedar = Load("Assets/NAV/Prefabs/World/Vegetation/HP_Cedar_Tree_03_WoodNode.prefab");
            GameObject boulder = Load("Assets/NAV/Prefabs/World/SM_Rocks_03.prefab");
            GameObject stone = Load("Assets/NAV/Prefabs/World/LooseStone.prefab");
            GameObject branch = Load("Assets/NAV/Prefabs/World/LooseBranch.prefab");
            GameObject tin = Load("Assets/NAV/Prefabs/World/TinOre_Rock.prefab");
            GameObject bushA = Load("Assets/NAV/Prefabs/World/Vegetation/NSK_bush04_WoodNode.prefab");
            GameObject bushB = Load("Assets/NAV/Prefabs/World/Vegetation/NSK_bush05_WoodNode.prefab");
            var plains = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(BiomeFolder + "PlainsBiome.asset");
            if (spruce == null || tin == null || plains == null)
            {
                Debug.LogError("SeaIslandsSetup: run the vegetation and metallurgy setups first.");
                return;
            }

            GameObject driftwood = BuildDriftwood();
            (ItemDefinition shellfish, ItemDefinition cookedShellfish) = BuildShellfish();
            AddCampfireRecipe(shellfish, cookedShellfish);

            BiomeDefinition islands = IslandBiome(plains);
            var anyHeight = new Vector2(-1000f, 1000f);
            var dryLand = new Vector2(0.8f, 1000f);
            var beach = new Vector2(-0.2f, 1.4f);
            Upsert(islands, spruce, 0.22f, 11f, new Vector2(0.6f, 1.05f), dryLand);
            Upsert(islands, cedar, 0.12f, 14f, new Vector2(0.6f, 1f), dryLand);
            Upsert(islands, boulder, 0.16f, 14f, new Vector2(0.8f, 1.8f), anyHeight);
            Upsert(islands, tin, 0.16f, 15f, new Vector2(0.85f, 1.2f), new Vector2(0.3f, 1000f));
            Upsert(islands, driftwood, 0.16f, 12f, new Vector2(0.8f, 1.2f), beach);
            Upsert(islands, shellfish.WorldPrefab, 0.22f, 9f, Vector2.one, beach);
            Upsert(islands, bushA, 0.05f, 14f, new Vector2(0.7f, 1.1f), dryLand);
            Upsert(islands, bushB, 0.05f, 14f, new Vector2(0.7f, 1.1f), dryLand);
            Upsert(islands, stone, 0.08f, 14f, Vector2.one, anyHeight);
            Upsert(islands, branch, 0.06f, 16f, Vector2.one, dryLand);

            // Tin only on the islands now.
            foreach (string biome in new[] { "PlainsBiome", "ForestBiome", "OceanBiome" })
            {
                Remove(biome, tin);
            }

            var settings = new SerializedObject(AssetDatabase.LoadAssetAtPath<WorldGenerationSettings>(SettingsPath));
            settings.FindProperty("_islandBiome").objectReferenceValue = islands;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings.targetObject);

            AssetDatabase.SaveAssets();
            Debug.Log("SeaIslandsSetup: Sea Islands biome (driftwood, shellfish, tin), cooked shellfish on the campfire.");
        }

        private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        private static void Upsert(BiomeDefinition biome, GameObject prefab, float density, float spacing, Vector2 scale, Vector2 heightAboveWater)
        {
            if (prefab != null)
            {
                MetallurgySetup.UpsertResource(biome.name, prefab, density, spacing, scale, heightAboveWater);
            }
        }

        private static void Remove(string biomeName, GameObject prefab)
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

        /// <summary>A bleached log lying on the sand: any axe, 2 hits, 2-3 wood.</summary>
        private static GameObject BuildDriftwood()
        {
            ResourceNodeDefinition definition = TreeFellingSetup.GetOrCreateDefinition("DriftwoodNode");
            ItemDefinition wood = null;
            foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null && item.Id == "wood")
                {
                    wood = item;
                }
            }

            var serialized = new SerializedObject(definition);
            TreeFellingSetup.Configure(serialized, "Driftwood", 2, null, null, 0f);
            serialized = new SerializedObject(definition);
            serialized.FindProperty("_shrinkOnHit").boolValue = true;
            SerializedProperty drops = serialized.FindProperty("_drops");
            drops.arraySize = 2;
            drops.GetArrayElementAtIndex(0).FindPropertyRelative("_item").objectReferenceValue = wood;
            drops.GetArrayElementAtIndex(0).FindPropertyRelative("_amount").intValue = 2;
            drops.GetArrayElementAtIndex(0).FindPropertyRelative("_chance").floatValue = 1f;
            drops.GetArrayElementAtIndex(1).FindPropertyRelative("_item").objectReferenceValue = wood;
            drops.GetArrayElementAtIndex(1).FindPropertyRelative("_amount").intValue = 1;
            drops.GetArrayElementAtIndex(1).FindPropertyRelative("_chance").floatValue = 0.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            var root = new GameObject("Driftwood");
            // Half sunk in the sand, a little askew.
            GameObject log = TreeFellingSetup.AddLogVisual(root.transform, "Log", 2.6f, 0.2f, new Vector3(0f, 0.08f, 0f), Quaternion.Euler(0f, 0f, 4f));
            Material bleached = BleachedMaterial(log.GetComponentInChildren<Renderer>().sharedMaterial);
            foreach (Renderer renderer in log.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial = bleached;
            }

            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 0;
            collider.radius = 0.2f;
            collider.height = 2.6f;
            collider.center = new Vector3(0f, 0.12f, 0f);
            TreeFellingSetup.AddNode(root, definition, null);
            return TreeFellingSetup.Save(root, "Driftwood");
        }

        private static Material BleachedMaterial(Material source)
        {
            const string path = "Assets/NAV/Materials/Vegetation/Driftwood.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = "Driftwood" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.CopyPropertiesFromMaterial(source);
            material.SetColor("_BaseColor", new Color(0.86f, 0.82f, 0.74f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static (ItemDefinition raw, ItemDefinition cooked) BuildShellfish()
        {
            Material shell = Mat("Shellfish_material", new Color(0.24f, 0.26f, 0.34f));
            Material pearl = Mat("ShellInside_material", new Color(0.86f, 0.82f, 0.78f));
            Material cookedMaterial = Mat("CookedShellfish_material", new Color(0.92f, 0.56f, 0.32f));

            ItemDefinition raw = CreatureLootSetup.BuildItem("Shellfish", "Shellfish",
                "Mussels from the island shore. Cook them on a campfire.", 30, 0.2f,
                root =>
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var position = new Vector3(-0.08f + i * 0.08f, 0.03f, (i % 2) * 0.05f);
                        RusBuildingStyleSetup.Part(root, PrimitiveType.Sphere, "Shell", shell, position, new Vector3(0f, i * 50f, 0f), new Vector3(0.09f, 0.04f, 0.05f));
                        RusBuildingStyleSetup.Part(root, PrimitiveType.Sphere, "Lip", pearl, position + new Vector3(0f, 0.012f, 0f), new Vector3(0f, i * 50f, 0f), new Vector3(0.07f, 0.01f, 0.035f));
                    }
                }, Quaternion.Euler(-35f, 20f, 0f));

            ItemDefinition cooked = CreatureLootSetup.BuildItem("CookedShellfish", "Cooked Shellfish",
                "Mussels cooked on the fire - light, salty food that keeps you going.", 20, 0.2f,
                root =>
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var position = new Vector3(-0.08f + i * 0.08f, 0.03f, (i % 2) * 0.05f);
                        RusBuildingStyleSetup.Part(root, PrimitiveType.Sphere, "Shell", shell, position, new Vector3(0f, i * 50f, 0f), new Vector3(0.09f, 0.03f, 0.05f));
                        RusBuildingStyleSetup.Part(root, PrimitiveType.Sphere, "Meat", cookedMaterial, position + new Vector3(0f, 0.018f, 0f), new Vector3(0f, i * 50f, 0f), new Vector3(0.06f, 0.025f, 0.035f));
                    }
                }, Quaternion.Euler(-35f, 20f, 0f));

            // Food: less health than meat, more stamina - mixing foods pays, as with meat.
            string foodPath = FoodFolder + "CookedShellfishFood.asset";
            var food = AssetDatabase.LoadAssetAtPath<FoodDefinition>(foodPath);
            if (food == null)
            {
                food = ScriptableObject.CreateInstance<FoodDefinition>();
                AssetDatabase.CreateAsset(food, foodPath);
            }

            var foodSerialized = new SerializedObject(food);
            SetIfExists(foodSerialized, "_healthBonus", 14f);
            SetIfExists(foodSerialized, "_staminaBonus", 28f);
            SetIfExists(foodSerialized, "_healthRegenPerSecond", 0.8f);
            SetIfExists(foodSerialized, "_durationSeconds", 720f);
            foodSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(food);

            var cookedSerialized = new SerializedObject(cooked);
            cookedSerialized.FindProperty("_food").objectReferenceValue = food;
            cookedSerialized.FindProperty("_category").enumValueIndex = (int)ItemCategory.Food;
            cookedSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cooked);

            var database = new SerializedObject(AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/NAV/ScriptableObjects/Core/DefaultItemDatabase.asset"));
            CreatureLootSetup.AddToList(database, "_items", raw);
            CreatureLootSetup.AddToList(database, "_items", cooked);
            return (raw, cooked);
        }

        private static void SetIfExists(SerializedObject serialized, string property, float value)
        {
            SerializedProperty found = serialized.FindProperty(property);
            if (found != null)
            {
                found.floatValue = value;
            }
        }

        private static Material Mat(string name, Color color)
        {
            string path = $"Assets/NAV/Materials/Items/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The campfire spit also cooks shellfish.</summary>
        private static void AddCampfireRecipe(ItemDefinition raw, ItemDefinition cooked)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(CampfirePath);
            try
            {
                CookingStation station = root.GetComponentInChildren<CookingStation>(true);
                if (station == null)
                {
                    Debug.LogWarning("SeaIslandsSetup: the campfire has no CookingStation - shellfish recipe not added.");
                    return;
                }

                var serialized = new SerializedObject(station);
                SerializedProperty recipes = serialized.FindProperty("_recipes");
                for (int i = 0; i < recipes.arraySize; i++)
                {
                    if (recipes.GetArrayElementAtIndex(i).FindPropertyRelative("_input").objectReferenceValue == raw)
                    {
                        recipes.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }

                recipes.arraySize++;
                SerializedProperty recipe = recipes.GetArrayElementAtIndex(recipes.arraySize - 1);
                recipe.FindPropertyRelative("_input").objectReferenceValue = raw;
                recipe.FindPropertyRelative("_output").objectReferenceValue = cooked;
                recipe.FindPropertyRelative("_cookSeconds").floatValue = 8f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, CampfirePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static BiomeDefinition IslandBiome(BiomeDefinition plains)
        {
            // Ground: the dirt texture remapped sandy-grey - stony coastal ground.
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(IslandLayerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, IslandLayerPath);
            }

            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Terrain/Textures/UNS_Terrain_Dirt.png");
            layer.tileSize = new Vector2(3f, 3f);
            layer.diffuseRemapMax = new Vector4(0.74f, 0.76f, 0.64f, 1f);
            EditorUtility.SetDirty(layer);

            var biome = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(IslandBiomePath);
            if (biome == null)
            {
                biome = ScriptableObject.CreateInstance<BiomeDefinition>();
                AssetDatabase.CreateAsset(biome, IslandBiomePath);
            }

            var serialized = new SerializedObject(biome);
            serialized.FindProperty("_displayName").stringValue = "Sea Islands";
            serialized.FindProperty("_terrainLayer").objectReferenceValue = layer;
            serialized.FindProperty("_isWater").boolValue = false;
            // Same as the sea's - a different value would step the ground at the shoreline.
            serialized.FindProperty("_heightVariation").floatValue = 0.15f;
            serialized.FindProperty("_grassDensity").floatValue = 0.35f;
            serialized.FindProperty("_flowerDensity").floatValue = 0.02f;
            serialized.FindProperty("_fogDensity").floatValue = 0.004f;
            serialized.FindProperty("_fogColor").colorValue = new Color(0.46f, 0.5f, 0.56f, 0.35f);

            // Decor: the plains' pebbles and plants, no creatures.
            serialized.CopyFromSerializedProperty(new SerializedObject(plains).FindProperty("_decorSpawns"));
            serialized.FindProperty("_creatureSpawns").ClearArray();

            AudioClip sea = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Nature - Essentials/Ambiance_Sea_Loop_Stereo.wav");
            AudioClip wind = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Nature - Essentials/Ambiance_Wind_Calm_Loop_Stereo.wav");
            serialized.FindProperty("_dayAmbience").objectReferenceValue = sea;
            serialized.FindProperty("_nightAmbience").objectReferenceValue = sea;
            serialized.FindProperty("_ambienceVolume").floatValue = 0.6f;
            serialized.FindProperty("_windAmbience").objectReferenceValue = wind;
            serialized.FindProperty("_windVolume").floatValue = 0.55f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(biome);
            return biome;
        }
    }
}
