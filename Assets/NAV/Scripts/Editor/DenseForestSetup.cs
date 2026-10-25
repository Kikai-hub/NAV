using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using NAV.Gameplay.Items;
using NAV.World.Biomes;
using NAV.World.Generation;

namespace NAV.Editor
{
    /// <summary>
    /// The dense forest (дремучий лес) - the tier 2 biome of PROGRESSION_TIERS_v0.1.md, the wettest
    /// heart of every forest (WorldGenerator.GetBiome). NAV > World > Build Dense Forest (Stage C):
    ///   - ancient trees: dark-needled copies of the pack spruce, larch and cedar, spawned 1.5-2.4x
    ///     their size (18-27 m - 10-15 times the player's height) so their crowns close over the
    ///     ground; felled with a copper axe in 8 hits, their trunk and logs scale with them
    ///     (ResourceNode.InheritScale);
    ///   - the biome asset: dark mossy ground, steep hills, ancient trees over young spruce, old oaks
    ///     and copper ore (moved here - no longer on the plains or in the ordinary forest), big
    ///     boulders, the forest's decor with more ferns, little grass; many wolves, few deer;
    ///   - twice-and-a-half the trees in the ordinary forest and the birch groves (developer,
    ///     2026-10-24).
    /// Densities are written as absolute values, so re-running doesn't keep multiplying. The
    /// biome's gloom (fog/light under the crowns) is not here - it needs per-biome atmosphere.
    /// </summary>
    internal static class DenseForestSetup
    {
        private const string BiomeFolder = "Assets/NAV/ScriptableObjects/Biomes/";
        private const string VegetationFolder = "Assets/NAV/Prefabs/World/Vegetation/";
        private const string MaterialFolder = "Assets/NAV/Materials/Vegetation/Ancient";
        private const string DenseBiomePath = BiomeFolder + "DenseForestBiome.asset";
        private const string DenseLayerPath = "Assets/NAV/Art/Terrain/DenseForestLayer.terrainlayer";
        private const string TreeNodePath = "Assets/NAV/Scripts/Gameplay/Items/TreeWoodNode.asset";
        private const string AncientNodePath = "Assets/NAV/ScriptableObjects/World/Felling/AncientTreeNode.asset";
        private const string SettingsPath = "Assets/NAV/ScriptableObjects/World/DefaultWorldGenerationSettings.asset";

        // Darker, bluer needles and bark: old trees in deep shade.
        private static readonly Color AncientTint = new(0.6f, 0.68f, 0.62f);

        [MenuItem("NAV/World/Build Dense Forest (Stage C)")]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("DenseForestSetup: exit Play Mode first.");
                return;
            }

            GameObject spruce = Load("Assets/NAV/Prefabs/World/UNS_Spruce_WoodNode.prefab");
            GameObject oak = Load(VegetationFolder + "OldOak_WoodNode.prefab");
            GameObject copper = Load("Assets/NAV/Prefabs/World/CopperOre_Rock.prefab");
            GameObject bogIron = Load("Assets/NAV/Prefabs/World/BogIron_Rock.prefab");
            GameObject boulder = Load("Assets/NAV/Prefabs/World/SM_Rocks_03.prefab");
            GameObject branch = Load("Assets/NAV/Prefabs/World/LooseBranch.prefab");
            GameObject stone = Load("Assets/NAV/Prefabs/World/LooseStone.prefab");
            var forest = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(BiomeFolder + "ForestBiome.asset");
            if (spruce == null || oak == null || copper == null || forest == null)
            {
                Debug.LogError("DenseForestSetup: run the vegetation, metallurgy and oak setups first (needs the spruce, Old Oak, Copper Ore rock and the Forest biome).");
                return;
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/NAV/Materials/Vegetation", "Ancient");
            }

            // ---- Ancient trees ----
            ResourceNodeDefinition ancientNode = AncientTreeDefinition();
            GameObject ancientSpruce = AncientTree(spruce, "AncientSpruce_WoodNode", ancientNode);
            GameObject ancientLarch = AncientTree(Load(VegetationFolder + "HP_Larch_Tree_WoodNode.prefab"), "AncientLarch_WoodNode", ancientNode);
            GameObject ancientCedar = AncientTree(Load(VegetationFolder + "HP_Cedar_Tree_03_WoodNode.prefab"), "AncientCedar_WoodNode", ancientNode);

            // ---- The biome ----
            BiomeDefinition dense = DenseBiome(forest);
            var anyHeight = new Vector2(-1000f, 1000f);
            var dryLand = new Vector2(1.5f, 1000f);
            // Close enough (a tree every ~7 m) for the 14-20 m crowns to meet overhead.
            Upsert(dense, ancientSpruce, 0.8f, 7f, new Vector2(1.5f, 2.1f), dryLand);
            Upsert(dense, ancientLarch, 0.22f, 12f, new Vector2(1.45f, 2f), dryLand);
            Upsert(dense, ancientCedar, 0.18f, 12f, new Vector2(1.7f, 2.3f), dryLand);
            Upsert(dense, spruce, 0.25f, 8f, new Vector2(0.5f, 0.9f), dryLand);
            Upsert(dense, oak, 0.12f, 18f, new Vector2(1.6f, 1.95f), dryLand);
            Upsert(dense, copper, 0.14f, 16f, new Vector2(0.9f, 1.4f), dryLand);
            Upsert(dense, boulder, 0.12f, 16f, new Vector2(1.4f, 2.4f), anyHeight);
            Upsert(dense, bogIron, 0.1f, 14f, new Vector2(0.7f, 1.1f), new Vector2(0.1f, 1.5f));
            Upsert(dense, branch, 0.12f, 14f, Vector2.one, anyHeight);
            Upsert(dense, stone, 0.05f, 18f, Vector2.one, anyHeight);

            // Copper and old oaks live only here now.
            Remove("PlainsBiome", copper);
            Remove("ForestBiome", copper);
            Remove("ForestBiome", oak);

            // ---- More trees in the forest and the birch groves (x2.5) ----
            SetDensity("ForestBiome", "UNS_Spruce_WoodNode", 0.65f);
            SetDensity("ForestBiome", "HP_Larch_Tree_WoodNode", 0.11f);
            SetDensity("ForestBiome", "HP_Cedar_Tree_03_WoodNode", 0.09f);
            foreach (string tree in new[] { "NSK_tree01_WoodNode", "NSK_tree02_WoodNode", "NSK_tree03_WoodNode", "NSK_tree04_WoodNode" })
            {
                SetDensity("ForestBiome", tree, 0.06f);
            }

            foreach (string birch in new[] { "Birch_A_WoodNode", "Birch_B_WoodNode", "Birch_C_WoodNode" })
            {
                SetDensity("BirchGroveBiome", birch, 0.4f);
            }

            SetDensity("BirchGroveBiome", "UNS_Spruce_WoodNode", 0.12f);

            // ---- World generation ----
            var settings = new SerializedObject(AssetDatabase.LoadAssetAtPath<WorldGenerationSettings>(SettingsPath));
            settings.FindProperty("_denseForestBiome").objectReferenceValue = dense;
            settings.FindProperty("_denseForestMoistureThreshold").floatValue = 0.68f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings.targetObject);

            AssetDatabase.SaveAssets();
            Debug.Log("DenseForestSetup: Dense Forest biome (ancient trees, old oaks, copper), forest and grove trees x2.5. WorldGenerationVersion 6.");
        }

        private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        /// <summary>The ancient trees' node: the ordinary tree (same trunk/stump/log chain and
        /// drops) but 8 hits and a copper axe at least.</summary>
        private static ResourceNodeDefinition AncientTreeDefinition()
        {
            if (AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(AncientNodePath) == null)
            {
                AssetDatabase.CopyAsset(TreeNodePath, AncientNodePath);
            }

            var definition = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(AncientNodePath);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_displayName").stringValue = "Ancient Tree";
            serialized.FindProperty("_hitsToDeplete").intValue = 8;
            serialized.FindProperty("_minToolTier").intValue = 2;
            serialized.FindProperty("_fallImpulse").floatValue = 900f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        /// <summary>A copy of a pack tree node with darkened materials and the ancient node -
        /// its size comes from the biome's spawn scale.</summary>
        private static GameObject AncientTree(GameObject source, string name, ResourceNodeDefinition node)
        {
            GameObject tree = TreeFellingSetup.InstantiateUnpacked(AssetDatabase.GetAssetPath(source));
            tree.name = name;
            foreach (Renderer renderer in tree.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = Darkened(materials[i]);
                }

                renderer.sharedMaterials = materials;
            }

            var serialized = new SerializedObject(tree.GetComponent<ResourceNode>());
            serialized.FindProperty("_definition").objectReferenceValue = node;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tree, $"{VegetationFolder}{name}.prefab");
            Object.DestroyImmediate(tree);
            return prefab;
        }

        private static Material Darkened(Material source)
        {
            if (source == null)
            {
                return null;
            }

            string path = $"{MaterialFolder}/Ancient_{source.name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = $"Ancient_{source.name}" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.CopyPropertiesFromMaterial(source);
            if (source.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", source.GetColor("_BaseColor") * AncientTint);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static BiomeDefinition DenseBiome(BiomeDefinition forest)
        {
            // Ground: the forest floor, darker and greener (moss under the crowns).
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(DenseLayerPath);
            if (layer == null)
            {
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(forest.TerrainLayer), DenseLayerPath);
                layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(DenseLayerPath);
            }

            layer.diffuseRemapMax = new Vector4(0.36f, 0.5f, 0.28f, 1f);
            EditorUtility.SetDirty(layer);

            var biome = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(DenseBiomePath);
            if (biome == null)
            {
                biome = ScriptableObject.CreateInstance<BiomeDefinition>();
                AssetDatabase.CreateAsset(biome, DenseBiomePath);
            }

            var serialized = new SerializedObject(biome);
            var source = new SerializedObject(forest);
            serialized.FindProperty("_displayName").stringValue = "Dense Forest";
            serialized.FindProperty("_terrainLayer").objectReferenceValue = layer;
            serialized.FindProperty("_isWater").boolValue = false;
            serialized.FindProperty("_heightVariation").floatValue = 0.8f;
            serialized.FindProperty("_grassDensity").floatValue = 0.12f;
            serialized.FindProperty("_flowerDensity").floatValue = 0f;
            // Gloom under the crowns (AtmosphereFog): you see ~110 m, the air greener and darker.
            serialized.FindProperty("_fogDensity").floatValue = 0.018f;
            serialized.FindProperty("_fogColor").colorValue = new Color(0.3f, 0.42f, 0.3f, 0.85f);

            // Decor: the forest's, with more ferns and fallen logs.
            serialized.CopyFromSerializedProperty(source.FindProperty("_decorSpawns"));
            SerializedProperty decor = serialized.FindProperty("_decorSpawns");
            for (int i = 0; i < decor.arraySize; i++)
            {
                SerializedProperty entry = decor.GetArrayElementAtIndex(i);
                Object prefab = entry.FindPropertyRelative("_prefab").objectReferenceValue;
                string prefabName = prefab != null ? prefab.name : "";
                if (prefabName == "Fern")
                {
                    entry.FindPropertyRelative("_density").floatValue = 0.5f;
                    entry.FindPropertyRelative("_scale").vector2Value = new Vector2(1f, 1.6f);
                }
                else if (prefabName == "UNS_Log" || prefabName.Contains("Stump"))
                {
                    entry.FindPropertyRelative("_density").floatValue = 0.08f;
                }
            }

            // Creatures: wolves, a few deer.
            serialized.CopyFromSerializedProperty(source.FindProperty("_creatureSpawns"));
            SerializedProperty creatures = serialized.FindProperty("_creatureSpawns");
            for (int i = 0; i < creatures.arraySize; i++)
            {
                SerializedProperty entry = creatures.GetArrayElementAtIndex(i);
                Object prefab = entry.FindPropertyRelative("_prefab").objectReferenceValue;
                bool wolf = prefab != null && prefab.name.Contains("Wolf");
                entry.FindPropertyRelative("_count").intValue = wolf ? 5 : 1;
                entry.FindPropertyRelative("_nightCount").intValue = wolf ? 9 : 0;
            }

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

        private static void Upsert(BiomeDefinition biome, GameObject prefab, float density, float spacing, Vector2 scale, Vector2 heightAboveWater)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"DenseForestSetup: a resource prefab for {biome.name} is missing - skipped.");
                return;
            }

            MetallurgySetup.UpsertResource(biome.name, prefab, density, spacing, scale, heightAboveWater);
        }

        private static void Remove(string biomeName, GameObject prefab)
        {
            var biome = AssetDatabase.LoadAssetAtPath<BiomeDefinition>($"{BiomeFolder}{biomeName}.asset");
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

        private static void SetDensity(string biomeName, string prefabName, float density)
        {
            var biome = AssetDatabase.LoadAssetAtPath<BiomeDefinition>($"{BiomeFolder}{biomeName}.asset");
            var serialized = new SerializedObject(biome);
            SerializedProperty list = serialized.FindProperty("_resourceSpawns");
            var found = new List<int>();
            for (int i = 0; i < list.arraySize; i++)
            {
                Object prefab = list.GetArrayElementAtIndex(i).FindPropertyRelative("_prefab").objectReferenceValue;
                if (prefab != null && prefab.name == prefabName)
                {
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("_density").floatValue = density;
                    found.Add(i);
                }
            }

            if (found.Count == 0)
            {
                Debug.LogWarning($"DenseForestSetup: {prefabName} isn't in {biomeName} - its density was not changed.");
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(biome);
        }
    }
}
