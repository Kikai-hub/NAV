using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.Building;
using NAV.Gameplay.Combat;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Items;

namespace NAV.Editor
{
    /// <summary>
    /// Stage C, second step - station levels and metal tools:
    ///   - a second crafting station kind, the Forge (WorkbenchDefinition + building piece with a
    ///     Workbench on it; needs a roof like the workbench), upgraded by the Casting Mould, the
    ///     Anvil and the Bellows (WorkbenchExtension, +1 level each - their costs of copper,
    ///     bronze and iron set the order); the workbench is upgraded by the tanning rack
    ///     (MetallurgySetup);
    ///   - the existing recipes are tied to the Workbench;
    ///   - Bronze Bar = 2 copper + 1 tin bar at Forge level 2;
    ///   - Copper Axe / Pickaxe (tool tier 2, Forge level 1), Bronze (tier 3, level 2) and Iron
    ///     (tier 4, level 3): placeholder looks - the stone tools' models with tinted materials;
    ///   - bog iron needs a bronze pickaxe (tool tier 3) - iron is tier 4 since bronze came in
    ///     (PROGRESSION_TIERS_v0.1.md).
    /// Run MetallurgySetup first (bars, leather, glue). Re-run to rebuild in place; save the
    /// scene afterwards (PlayerBuilding/PlayerCrafting learn the new pieces and recipes).
    /// </summary>
    internal static class SmithingSetup
    {
        private const string Items = "Assets/NAV/ScriptableObjects/Items/";
        private const string Recipes = "Assets/NAV/ScriptableObjects/Recipes/";
        private const string Weapons = "Assets/NAV/ScriptableObjects/Weapons/";
        private const string Workbenches = "Assets/NAV/ScriptableObjects/Workbenches/";
        private const string PieceFolder = "Assets/NAV/ScriptableObjects/Building/";
        private const string BuildPrefabs = "Assets/NAV/Prefabs/Build/";
        private const string ItemPrefabs = "Assets/NAV/Prefabs/Item/";
        private const string MaterialFolder = "Assets/NAV/Materials/Items/";
        private const string IconFolder = "Assets/NAV/Art/Icons/";

        [MenuItem("NAV/Build/Build Forge and Metal Tools (Stage C)")]
        private static void Build()
        {
            var workbench = AssetDatabase.LoadAssetAtPath<WorkbenchDefinition>(Workbenches + "BasicWorkbench.asset");
            WorkbenchDefinition forge = GetOrCreateForgeDefinition(workbench);

            ItemDefinition wood = Item("Wood");
            ItemDefinition rock = Item("Rock");
            ItemDefinition copperBar = Item("CopperBar");
            ItemDefinition ironBar = Item("IronBar");
            ItemDefinition tinBar = Item("TinBar");
            ItemDefinition bronzeBar = Item("BronzeBar");
            ItemDefinition leather = Item("Leather");
            ItemDefinition charcoal = Item("Charcoal");
            ItemDefinition boneGlue = Item("BoneGlue");
            if (copperBar == null || leather == null || bronzeBar == null)
            {
                Debug.LogError("SmithingSetup: run NAV/Build/Build Ore, Smelting and Tanning (Stage C) first.");
                return;
            }

            // Existing recipes are workbench recipes.
            foreach (string guid in AssetDatabase.FindAssets("t:RecipeDefinition", new[] { "Assets/NAV/ScriptableObjects/Recipes" }))
            {
                var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                var serialized = new SerializedObject(recipe);
                if (serialized.FindProperty("_station").objectReferenceValue == null)
                {
                    serialized.FindProperty("_station").objectReferenceValue = workbench;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(recipe);
                }
            }

            Material stone = Load<Material>("Assets/NAV/Materials/CampfireStone_material.mat");
            Material woodMat = Load<Material>("Assets/NAV/Materials/Workbench_material.mat");
            Material fire = Load<Material>("Assets/NAV/Materials/Fire_material.mat");
            Material iron = Load<Material>(MaterialFolder + "Iron_material.mat");
            Material leatherMat = Load<Material>(MaterialFolder + "Leather_material.mat");

            // ---- Pieces ----
            BuildingPieceDefinition forgePiece = BuildPiece("Forge", "Forge",
                new[] { (rock, 15), (wood, 6), (copperBar, 4), (charcoal, 4) },
                root => BuildForgeModel(root, stone, fire, woodMat),
                root =>
                {
                    var range = new GameObject("ForgeRange");
                    range.transform.SetParent(root.transform, false);
                    range.AddComponent<SphereCollider>().isTrigger = true;
                    var bench = new SerializedObject(range.AddComponent<Workbench>());
                    bench.FindProperty("_definition").objectReferenceValue = forge;
                    bench.ApplyModifiedPropertiesWithoutUndo();
                    root.AddComponent<CraftingStationAccess>();
                });
            // Forge levels = 1 + each distinct upgrade nearby. The costs set the order: the casting
            // mould needs copper (level 2 - bronze), the anvil bronze (level 3 - iron), the bellows
            // iron (level 4 - iron two-handers). PROGRESSION_TIERS_v0.1.md, tiers 2-4.
            Material bronze = Load<Material>(MaterialFolder + "Bronze_material.mat");
            BuildingPieceDefinition mouldPiece = BuildPiece("CastingMould", "Casting Mould",
                new[] { (rock, 8), (copperBar, 2), (wood, 2) },
                root => BuildCastingMouldModel(root, stone, bronze, fire),
                root => AddExtension(root, forge, "CastingMould"));
            BuildingPieceDefinition anvilPiece = BuildPiece("Anvil", "Anvil",
                new[] { (bronzeBar, 5), (wood, 4) },
                root => BuildAnvilModel(root, iron, woodMat),
                root => AddExtension(root, forge, "Anvil"));
            BuildingPieceDefinition bellowsPiece = BuildPiece("Bellows", "Bellows",
                new[] { (leather, 4), (wood, 5), (boneGlue, 1), (ironBar, 2) },
                root => BuildBellowsModel(root, leatherMat, woodMat),
                root => AddExtension(root, forge, "Bellows"));

            var pieceDatabase = new SerializedObject(Load<BuildingPieceDatabase>("Assets/NAV/ScriptableObjects/Core/BuildingPieceDatabase.asset"));
            PlayerBuilding building = Object.FindAnyObjectByType<PlayerBuilding>(FindObjectsInactive.Include);
            foreach (BuildingPieceDefinition piece in new[] { forgePiece, mouldPiece, anvilPiece, bellowsPiece })
            {
                CreatureLootSetup.AddToList(pieceDatabase, "_pieces", piece);
                if (building != null)
                {
                    CreatureLootSetup.AddToList(new SerializedObject(building), "_knownPieces", piece);
                }
            }

            // ---- Tools ----
            MetalTools tools = BuildMetalTools();

            var itemDatabase = new SerializedObject(Load<ItemDatabase>("Assets/NAV/ScriptableObjects/Core/DefaultItemDatabase.asset"));
            foreach (ItemDefinition tool in new[] { tools.CopperAxe, tools.CopperPick, tools.BronzeAxe, tools.BronzePick, tools.IronAxe, tools.IronPick })
            {
                CreatureLootSetup.AddToList(itemDatabase, "_items", tool);
            }

            RecipeDefinition[] recipes =
            {
                // Bronze is alloyed at the forge once the casting mould stands by it.
                BuildRecipe("BronzeBarRecipe", bronzeBar, forge, 2, (copperBar, 2), (tinBar, 1)),
                BuildRecipe("CopperAxeRecipe", tools.CopperAxe, forge, 1, (copperBar, 3), (wood, 2), (leather, 1)),
                BuildRecipe("CopperPickaxeRecipe", tools.CopperPick, forge, 1, (copperBar, 3), (wood, 2), (leather, 1)),
                BuildRecipe("BronzeAxeRecipe", tools.BronzeAxe, forge, 2, (bronzeBar, 3), (wood, 2), (leather, 1)),
                BuildRecipe("BronzePickaxeRecipe", tools.BronzePick, forge, 2, (bronzeBar, 3), (wood, 2), (leather, 1)),
                BuildRecipe("IronAxeRecipe", tools.IronAxe, forge, 3, (ironBar, 3), (wood, 2), (leather, 1)),
                BuildRecipe("IronPickaxeRecipe", tools.IronPick, forge, 3, (ironBar, 3), (wood, 2), (leather, 2)),
            };

            PlayerCrafting crafting = Object.FindAnyObjectByType<PlayerCrafting>(FindObjectsInactive.Include);
            if (crafting != null)
            {
                foreach (RecipeDefinition recipe in recipes)
                {
                    CreatureLootSetup.AddToList(new SerializedObject(crafting), "_knownRecipes", recipe);
                }
            }

            // E on the workbench opens its crafting (the forge got it above); the panel and the
            // save system need the matching references in the scene.
            EnsureStationAccess(BuildPrefabs + "WoodWorkbench.prefab");
            WireCraftingScene(crafting);

            // Iron is tier 4 now - bog iron needs a bronze pickaxe (tool tier 3).
            var bogIron = Load<ResourceNodeDefinition>("Assets/NAV/ScriptableObjects/World/BogIronDeposit.asset");
            var bogSerialized = new SerializedObject(bogIron);
            bogSerialized.FindProperty("_minToolTier").intValue = 3;
            bogSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bogIron);

            if (building != null)
            {
                EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("SmithingSetup: Forge (+Casting Mould, +Anvil, +Bellows), bronze alloy, copper/bronze/iron tools and recipes built; bog iron needs tool tier 3. Save the scene.");
        }

        // ---- Station kind ----

        private static WorkbenchDefinition GetOrCreateForgeDefinition(WorkbenchDefinition workbench)
        {
            string path = Workbenches + "Forge.asset";
            var forge = Load<WorkbenchDefinition>(path);
            if (forge == null)
            {
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(workbench), path);
                forge = Load<WorkbenchDefinition>(path);
            }

            var serialized = new SerializedObject(forge);
            serialized.FindProperty("_displayName").stringValue = "Forge";
            serialized.FindProperty("_tier").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(forge);
            return forge;
        }

        private static void EnsureStationAccess(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (root.GetComponent<CraftingStationAccess>() == null)
                {
                    root.AddComponent<CraftingStationAccess>();
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WireCraftingScene(PlayerCrafting crafting)
        {
            var ui = Object.FindAnyObjectByType<NAV.UI.CraftingUIController>(FindObjectsInactive.Include);
            var interactor = Object.FindAnyObjectByType<NAV.Gameplay.Interaction.PlayerInteractor>(FindObjectsInactive.Include);
            if (ui != null)
            {
                var serialized = new SerializedObject(ui);
                serialized.FindProperty("_interactor").objectReferenceValue = interactor;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var saveManager = Object.FindAnyObjectByType<NAV.Core.Save.SaveManager>(FindObjectsInactive.Include);
            if (saveManager != null)
            {
                var serialized = new SerializedObject(saveManager);
                serialized.FindProperty("_playerCrafting").objectReferenceValue = crafting;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AddExtension(GameObject root, WorkbenchDefinition upgrades, string id)
        {
            var extension = new SerializedObject(root.AddComponent<WorkbenchExtension>());
            extension.FindProperty("_upgrades").objectReferenceValue = upgrades;
            extension.FindProperty("_extensionId").stringValue = id;
            extension.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Pieces ----

        private static BuildingPieceDefinition BuildPiece(string id, string displayName, (ItemDefinition item, int amount)[] cost,
            System.Action<Transform> buildModel, System.Action<GameObject> addComponents)
        {
            string definitionPath = $"{PieceFolder}{id}Piece.asset";
            var definition = Load<BuildingPieceDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BuildingPieceDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }

            var root = new GameObject(id);
            buildModel(root.transform);
            var bounds = new Bounds(root.transform.position, Vector3.zero);
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
            addComponents(root);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{BuildPrefabs}{id}.prefab");
            Object.DestroyImmediate(root);

            string iconPath = $"{IconFolder}Icon_{id}.png";
            File.WriteAllBytes(iconPath, ItemIconRenderer.RenderIcon(prefab, Quaternion.Euler(-22f, 0f, 0f) * Quaternion.Euler(0f, 145f, 0f)));
            AssetDatabase.ImportAsset(iconPath);
            ItemIconRenderer.ConfigureSprite(iconPath);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = $"{id}Piece";
            serialized.FindProperty("_displayName").stringValue = displayName;
            serialized.FindProperty("_prefab").objectReferenceValue = prefab;
            serialized.FindProperty("_requiresWorkbench").boolValue = true;
            serialized.FindProperty("_icon").objectReferenceValue = Load<Sprite>(iconPath);
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

        /// <summary>A stone hearth with a glowing coal bed, a chimney hood and a quench trough.</summary>
        private static void BuildForgeModel(Transform root, Material stone, Material fire, Material wood)
        {
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Hearth", stone, new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(1.4f, 0.9f, 1.1f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Coals", fire, new Vector3(0f, 0.92f, 0.05f), Vector3.zero, new Vector3(0.9f, 0.06f, 0.65f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "HoodBack", stone, new Vector3(0f, 1.7f, -0.42f), Vector3.zero, new Vector3(1.3f, 1.5f, 0.25f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Chimney", stone, new Vector3(0f, 2.75f, -0.35f), Vector3.zero, new Vector3(0.55f, 0.7f, 0.45f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Trough", wood, new Vector3(0.95f, 0.25f, 0.25f), Vector3.zero, new Vector3(0.45f, 0.5f, 0.8f));
            var lightObject = new GameObject("CoalLight");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.3f, 0.3f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.5f, 0.2f);
            light.intensity = 1.8f;
            light.range = 5f;
        }

        /// <summary>A stone casting table: a slab with dark mould cavities (ingot, blade), a clay
        /// crucible with a bronze glow on a small fire.</summary>
        private static void BuildCastingMouldModel(Transform root, Material stone, Material bronze, Material fire)
        {
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Table", stone, new Vector3(0f, 0.35f, 0f), Vector3.zero, new Vector3(1.1f, 0.7f, 0.7f));
            Material cavity = Load<Material>("Assets/NAV/Materials/Items/Charcoal_material.mat");
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "IngotMould", cavity, new Vector3(-0.25f, 0.71f, 0.05f), Vector3.zero, new Vector3(0.3f, 0.03f, 0.14f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "BladeMould", cavity, new Vector3(0.2f, 0.71f, -0.08f), new Vector3(0f, 20f, 0f), new Vector3(0.5f, 0.03f, 0.09f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Cast", bronze, new Vector3(-0.25f, 0.725f, 0.05f), Vector3.zero, new Vector3(0.24f, 0.02f, 0.09f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Crucible", stone, new Vector3(0.62f, 0.25f, 0.3f), Vector3.zero, new Vector3(0.28f, 0.2f, 0.28f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Melt", fire, new Vector3(0.62f, 0.46f, 0.3f), Vector3.zero, new Vector3(0.22f, 0.01f, 0.22f));
        }

        /// <summary>A dark anvil on a log stump.</summary>
        private static void BuildAnvilModel(Transform root, Material iron, Material wood)
        {
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Stump", wood, new Vector3(0f, 0.3f, 0f), Vector3.zero, new Vector3(0.55f, 0.3f, 0.55f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Waist", iron, new Vector3(0f, 0.7f, 0f), Vector3.zero, new Vector3(0.22f, 0.2f, 0.3f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Face", iron, new Vector3(0f, 0.86f, 0f), Vector3.zero, new Vector3(0.3f, 0.12f, 0.7f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Horn", iron, new Vector3(0f, 0.86f, 0.45f), new Vector3(20f, 0f, 0f), new Vector3(0.12f, 0.08f, 0.3f));
        }

        /// <summary>A pair of leather bellows on a wooden frame.</summary>
        private static void BuildBellowsModel(Transform root, Material leather, Material wood)
        {
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Frame", wood, new Vector3(0f, 0.25f, 0f), Vector3.zero, new Vector3(0.7f, 0.5f, 1.2f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Sphere, "Bag", leather, new Vector3(0f, 0.65f, -0.1f), Vector3.zero, new Vector3(0.65f, 0.35f, 0.95f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Board", wood, new Vector3(0f, 0.86f, -0.1f), new Vector3(-6f, 0f, 0f), new Vector3(0.6f, 0.04f, 0.95f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Nozzle", wood, new Vector3(0f, 0.6f, 0.62f), new Vector3(90f, 0f, 0f), new Vector3(0.08f, 0.18f, 0.08f));
        }

        // ---- Tools ----

        /// <summary>Rebuilds only the copper/bronze/iron axes and pickaxes from the current stone tools
        /// (e.g. after a stone tool got a new model) - items, held/world models, icons.</summary>
        [MenuItem("NAV/Build/Rebuild Metal Tool Models")]
        private static void RebuildMetalTools()
        {
            BuildMetalTools();
            AssetDatabase.SaveAssets();
            Debug.Log("SmithingSetup: copper/bronze/iron axes and pickaxes rebuilt from the stone tools.");
        }

        private struct MetalTools
        {
            public ItemDefinition CopperAxe;
            public ItemDefinition CopperPick;
            public ItemDefinition BronzeAxe;
            public ItemDefinition BronzePick;
            public ItemDefinition IronAxe;
            public ItemDefinition IronPick;
        }

        // Tool tier = progression tier (PROGRESSION_TIERS_v0.1.md): copper 2, bronze 3, iron 4.
        private static MetalTools BuildMetalTools()
        {
            Metal copper = TintedToolMaterial("Copper", new Color(1.7f, 1.05f, 0.65f), new Color(0.66f, 0.3f, 0.17f));
            Metal bronze = TintedToolMaterial("Bronze", new Color(1.6f, 1.25f, 0.7f), new Color(0.72f, 0.5f, 0.24f));
            Metal iron = TintedToolMaterial("Iron", new Color(1.2f, 1.25f, 1.35f), new Color(0.55f, 0.56f, 0.6f));
            return new MetalTools
            {
                CopperAxe = BuildTool("StoneAxe", "CopperAxe", "Copper Axe", "A copper-headed axe. Fells trees faster than stone.", 2, 1.6f, copper),
                CopperPick = BuildTool("StonePickaxe", "CopperPickaxe", "Copper Pickaxe", "A copper pickaxe - hard enough for tin.", 2, 1.6f, copper),
                BronzeAxe = BuildTool("StoneAxe", "BronzeAxe", "Bronze Axe", "A bronze axe - keeps its edge longer than copper.", 3, 1.9f, bronze),
                BronzePick = BuildTool("StonePickaxe", "BronzePickaxe", "Bronze Pickaxe", "A bronze pickaxe - hard enough for bog iron.", 3, 1.9f, bronze),
                IronAxe = BuildTool("StoneAxe", "IronAxe", "Iron Axe", "A heavy iron axe.", 4, 2.3f, iron),
                IronPick = BuildTool("StonePickaxe", "IronPickaxe", "Iron Pickaxe", "An iron pickaxe for the hardest ore.", 4, 2.3f, iron),
            };
        }

        /// <summary>The metal's tint (bright - it multiplies textures that are fairly dark), and
        /// the plain metal colour for untextured materials (the developer's own Blender models,
        /// whose flat stone colour would only turn muddy brown when multiplied).</summary>
        private struct Metal
        {
            public string Name;
            public Color Tint;
            public Color Solid;
        }

        // The developer's Blender tool models name their wooden handle part this; it stays wood.
        private const string HandlePartName = "Рукоядь";

        private static Metal TintedToolMaterial(string metal, Color tint, Color solid)
        {
            return new Metal { Name = metal, Tint = tint, Solid = solid };
        }

        /// <summary>A tinted copy of one of the stone tool's own materials (its textures/UVs
        /// stay right - replacing them all with one material striped the pickaxe).</summary>
        private static Material Tinted(Material source, Metal metal)
        {
            string path = $"{MaterialFolder}{source.name}_{metal.Name}.mat";
            var material = Load<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = $"{source.name}_{metal.Name}" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.CopyPropertiesFromMaterial(source);
            Color baseColor = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white;
            bool textured = source.HasProperty("_BaseMap") && source.GetTexture("_BaseMap") != null;
            material.SetColor("_BaseColor", textured ? baseColor * metal.Tint : metal.Solid);
            material.SetFloat("_Metallic", 0.45f);
            material.SetFloat("_Smoothness", 0.5f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>A metal version of a stone tool: copies of its definition, weapon, pickup and
        /// held prefabs, recoloured, with a higher tool tier and damage.</summary>
        private static ItemDefinition BuildTool(string sourceId, string id, string displayName, string description, int tier, float damageScale, Metal tint)
        {
            var source = Load<ItemDefinition>($"{Items}{sourceId}.asset");
            string itemPath = $"{Items}{id}.asset";
            var item = Load<ItemDefinition>(itemPath);
            if (item == null)
            {
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), itemPath);
                item = Load<ItemDefinition>(itemPath);
            }

            WeaponDefinition weapon = source.Weapon != null ? CopyWeapon(source.Weapon, id, displayName, damageScale) : null;
            GameObject world = source.WorldPrefab != null ? CopyTinted(source.WorldPrefab, $"{ItemPrefabs}{id}_item.prefab", tint, item) : null;
            GameObject held = source.HeldPrefab != null ? CopyTinted(source.HeldPrefab, $"{ItemPrefabs}Held/{id}_held.prefab", tint, null) : null;

            string iconPath = $"{IconFolder}Icon_{id}.png";
            GameObject iconModel = held != null ? held : world;
            File.WriteAllBytes(iconPath, ItemIconRenderer.RenderIcon(iconModel, held != null ? (Quaternion?)null : Quaternion.Euler(-30f, 30f, -30f)));
            AssetDatabase.ImportAsset(iconPath);
            ItemIconRenderer.ConfigureSprite(iconPath);

            var serialized = new SerializedObject(item);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_displayName").stringValue = displayName;
            serialized.FindProperty("_description").stringValue = description;
            serialized.FindProperty("_toolTier").intValue = tier;
            serialized.FindProperty("_weapon").objectReferenceValue = weapon;
            serialized.FindProperty("_worldPrefab").objectReferenceValue = world;
            serialized.FindProperty("_heldPrefab").objectReferenceValue = held;
            serialized.FindProperty("_icon").objectReferenceValue = Load<Sprite>(iconPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return item;
        }

        private static WeaponDefinition CopyWeapon(WeaponDefinition source, string id, string displayName, float damageScale)
        {
            string path = $"{Weapons}{id}Weapon.asset";
            var weapon = Load<WeaponDefinition>(path);
            if (weapon == null)
            {
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path);
                weapon = Load<WeaponDefinition>(path);
            }

            var serialized = new SerializedObject(weapon);
            serialized.FindProperty("_displayName").stringValue = displayName;
            serialized.FindProperty("_damage").floatValue = source.Damage * damageScale;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weapon);
            return weapon;
        }

        private static GameObject CopyTinted(GameObject source, string path, Metal tint, ItemDefinition pickupItem)
        {
            GameObject root = Object.Instantiate(source);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name == HandlePartName)
                {
                    continue;
                }

                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = materials[i] != null ? Tinted(materials[i], tint) : null;
                }

                renderer.sharedMaterials = materials;
            }

            if (pickupItem != null && root.TryGetComponent(out ItemPickup pickup))
            {
                var serialized = new SerializedObject(pickup);
                serialized.FindProperty("_definition").objectReferenceValue = pickupItem;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        internal static RecipeDefinition BuildRecipe(string name, ItemDefinition output, WorkbenchDefinition station, int level, params (ItemDefinition item, int amount)[] ingredients)
        {
            string path = $"{Recipes}{name}.asset";
            var recipe = Load<RecipeDefinition>(path);
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
                AssetDatabase.CreateAsset(recipe, path);
            }

            var serialized = new SerializedObject(recipe);
            serialized.FindProperty("_outputItem").objectReferenceValue = output;
            serialized.FindProperty("_outputAmount").intValue = 1;
            serialized.FindProperty("_station").objectReferenceValue = station;
            serialized.FindProperty("_requiredWorkbenchTier").intValue = level;
            SerializedProperty list = serialized.FindProperty("_ingredients");
            list.arraySize = ingredients.Length;
            for (int i = 0; i < ingredients.Length; i++)
            {
                list.GetArrayElementAtIndex(i).FindPropertyRelative("_item").objectReferenceValue = ingredients[i].item;
                list.GetArrayElementAtIndex(i).FindPropertyRelative("_amount").intValue = ingredients[i].amount;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(recipe);
            return recipe;
        }

        private static ItemDefinition Item(string id) => Load<ItemDefinition>($"{Items}{id}.asset");

        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
