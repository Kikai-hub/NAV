using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.Building;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Items;
using NAV.World.Biomes;
using NAV.World.Generation.Hybrid;

namespace NAV.Editor
{
    /// <summary>
    /// Tier 4 - the Swamp, part 2 (PROGRESSION_TIERS_v0.1.md), NAV > Build > Build Swamp Iron Age
    /// (Tier 4):
    ///   - Iron Hinge (x2 from an iron bar at the Forge level 3);
    ///   - Wild Rosemary (багульник) growing in the swamp (a pickup, like shellfish on the islands)
    ///     -> Antidote (x2 from 3 rosemary + resin at the Workbench): drunk from the inventory, it
    ///     ends poison and keeps it off for 90 s (PlayerPoison) - no food slot;
    ///   - Iron-Bound Gate: the oak gate's double leaves in black bog wood, banded and studded with
    ///     iron, on iron hinges (Workbench level 3, the joiner's bench);
    ///   - Stake Trap: bog-wood stakes with iron points that wound creatures walking over it
    ///     (SpikeTrap), never the player; wears out (Workbench level 2);
    ///   - the boss 4 arena slot: a point of interest in the swamp - an empty slot, no boss.
    /// Models are primitives (placeholders - MODELS_FOR_3D_ARTIST_v0.1.md). Re-runnable; saves the scene.
    /// </summary>
    internal static class SwampCraftSetup
    {
        private const string ItemFolder = "Assets/NAV/ScriptableObjects/Items/";
        private const string DatabasePath = "Assets/NAV/ScriptableObjects/Core/DefaultItemDatabase.asset";
        private const string PieceDatabasePath = "Assets/NAV/ScriptableObjects/Core/BuildingPieceDatabase.asset";
        private const string WorkbenchPath = "Assets/NAV/ScriptableObjects/Workbenches/BasicWorkbench.asset";
        private const string ForgePath = "Assets/NAV/ScriptableObjects/Workbenches/Forge.asset";
        private const string IronMaterialPath = "Assets/NAV/Materials/Items/Iron_material.mat";
        private const string SwampBiomePath = "Assets/NAV/ScriptableObjects/Biomes/SwampBiome.asset";
        private const string HybridSettingsPath = "Assets/NAV/ScriptableObjects/World/HybridWorldSettings.asset";
        private const string PoiFolder = "Assets/NAV/ScriptableObjects/World/Poi/";
        private const string ArenaSlotPrefabPath = "Assets/NAV/Prefabs/World/Poi/Poi_BossArenaSlot.prefab";
        // Same as the iron tools and armour (the anvil); the gate needs the joiner's bench like the oak gate.
        private const int IronForgeLevel = 3;
        private const int GateWorkbenchLevel = 3;
        private const int TrapWorkbenchLevel = 2;

        private static Material _bogWood;
        private static Material _iron;

        [MenuItem("NAV/Build/Build Swamp Iron Age (Tier 4)")]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("SwampCraftSetup: exit Play Mode first.");
                return;
            }

            ItemDefinition bogWood = Item("BogWood");
            ItemDefinition ironBar = Item("IronBar");
            ItemDefinition resin = Item("Resin");
            var workbench = AssetDatabase.LoadAssetAtPath<WorkbenchDefinition>(WorkbenchPath);
            var forge = AssetDatabase.LoadAssetAtPath<WorkbenchDefinition>(ForgePath);
            var swamp = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(SwampBiomePath);
            _iron = AssetDatabase.LoadAssetAtPath<Material>(IronMaterialPath);
            if (bogWood == null || ironBar == null || resin == null || workbench == null || forge == null || swamp == null || _iron == null)
            {
                Debug.LogError("SwampCraftSetup: run the swamp (Bog Wood), smithing (Iron Bar, Forge) and oak setups first.");
                return;
            }

            // Dark, but not black - the iron bands must read against it.
            _bogWood = OakSetup.Mat("BogWoodPiece_material", new Color(0.3f, 0.21f, 0.14f), 0.3f);
            var database = new SerializedObject(AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath));
            var recipes = new List<RecipeDefinition>();

            // ---- Iron hinge ----
            ItemDefinition hinge = CreatureLootSetup.BuildItem("IronHinge", "Iron Hinge",
                "A forged strap hinge with its pin - for gates and doors that have to stand a beating.", 50, 0.4f,
                root =>
                {
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Strap", _iron, new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(0.28f, 0.015f, 0.05f));
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Leaf", _iron, new Vector3(-0.16f, 0.02f, 0f), Vector3.zero, new Vector3(0.04f, 0.015f, 0.12f));
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Pin", _iron, new Vector3(-0.19f, 0.04f, 0f), Vector3.zero, new Vector3(0.025f, 0.05f, 0.025f));
                }, Quaternion.Euler(-30f, 20f, 0f));
            CreatureLootSetup.AddToList(database, "_items", hinge);
            recipes.Add(Recipe("IronHingeRecipe", hinge, 2, forge, IronForgeLevel, (ironBar, 1)));

            // ---- Wild rosemary and the antidote ----
            Material leaf = OakSetup.Mat("WildRosemary_material", new Color(0.24f, 0.36f, 0.18f), 0.15f);
            Material blossom = OakSetup.Mat("WildRosemaryBlossom_material", new Color(0.92f, 0.9f, 0.84f), 0.1f);
            ItemDefinition rosemary = CreatureLootSetup.BuildItem("WildRosemary", "Wild Rosemary",
                "Bitter-smelling marsh shrub (багульник). Brewed with resin it draws the swamp's poison out.", 50, 0.05f,
                root =>
                {
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = i * 60f * Mathf.Deg2Rad;
                        var lean = new Vector3(Mathf.Sin(angle) * 18f, 0f, Mathf.Cos(angle) * 18f);
                        var stem = new Vector3(Mathf.Cos(angle) * 0.03f, 0.1f, Mathf.Sin(angle) * 0.03f);
                        CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Stem", leaf, stem, lean, new Vector3(0.012f, 0.1f, 0.012f));
                        CreatureLootSetup.AddPart(root, PrimitiveType.Sphere, "Blossom", blossom, stem + Vector3.up * 0.1f + stem * 0.6f, Vector3.zero, Vector3.one * 0.045f);
                    }
                }, Quaternion.Euler(-20f, 0f, 0f));
            CreatureLootSetup.AddToList(database, "_items", rosemary);
            MetallurgySetup.UpsertResource(swamp.name, rosemary.WorldPrefab, 0.22f, 8f, Vector2.one, new Vector2(0.1f, 4f));

            Material clay = OakSetup.Mat("AntidoteFlask_material", new Color(0.45f, 0.3f, 0.2f), 0.4f);
            Material cork = OakSetup.Mat("AntidoteCork_material", new Color(0.62f, 0.5f, 0.32f), 0.1f);
            ItemDefinition antidote = CreatureLootSetup.BuildItem("Antidote", "Antidote",
                "A clay flask of wild-rosemary brew. Ends poisoning and keeps the swamp's venom off for a while.", 10, 0.3f,
                root =>
                {
                    CreatureLootSetup.AddPart(root, PrimitiveType.Sphere, "Flask", clay, new Vector3(0f, 0.07f, 0f), Vector3.zero, new Vector3(0.12f, 0.13f, 0.12f));
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Neck", clay, new Vector3(0f, 0.15f, 0f), Vector3.zero, new Vector3(0.045f, 0.03f, 0.045f));
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Cork", cork, new Vector3(0f, 0.19f, 0f), Vector3.zero, new Vector3(0.04f, 0.015f, 0.04f));
                }, Quaternion.Euler(-15f, 0f, 0f));
            var antidoteSerialized = new SerializedObject(antidote);
            antidoteSerialized.FindProperty("_category").enumValueIndex = (int)ItemCategory.Misc;
            antidoteSerialized.FindProperty("_curesPoison").boolValue = true;
            antidoteSerialized.FindProperty("_poisonImmunitySeconds").floatValue = 90f;
            antidoteSerialized.ApplyModifiedPropertiesWithoutUndo();
            CreatureLootSetup.AddToList(database, "_items", antidote);
            recipes.Add(Recipe("AntidoteRecipe", antidote, 2, workbench, 1, (rosemary, 3), (resin, 1)));

            // ---- Pieces ----
            var pieces = new List<BuildingPieceDefinition>
            {
                OakSetup.Piece("IronBoundGate", "Iron-Bound Gate", new[] { (bogWood, 8), (hinge, 4) }, workbench, GateWorkbenchLevel, BuildIronBoundGate, OakSetup.AddGateDoor),
                OakSetup.Piece("StakeTrap", "Stake Trap", new[] { (bogWood, 3), (ironBar, 1) }, workbench, TrapWorkbenchLevel, BuildStakeTrap, root => root.AddComponent<SpikeTrap>()),
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

            // ---- Boss 4 arena slot ----
            AddSwampBossSlot(swamp);

            PlayerCrafting crafting = Object.FindAnyObjectByType<PlayerCrafting>(FindObjectsInactive.Include);
            if (crafting != null)
            {
                var craftingSerialized = new SerializedObject(crafting);
                foreach (RecipeDefinition recipe in recipes)
                {
                    CreatureLootSetup.AddToList(craftingSerialized, "_knownRecipes", recipe);
                }
            }

            EditorUtility.SetDirty(swamp);
            AssetDatabase.SaveAssets();
            if (building != null)
            {
                EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
                EditorSceneManager.SaveScene(building.gameObject.scene);
            }

            Debug.Log("SwampCraftSetup: Iron Hinge, Wild Rosemary (swamp) -> Antidote, Iron-Bound Gate, Stake Trap, boss 4 arena slot. Scene saved.");
        }

        private static ItemDefinition Item(string id) => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}{id}.asset");

        private static RecipeDefinition Recipe(string name, ItemDefinition output, int amount, WorkbenchDefinition station, int level, params (ItemDefinition item, int amount)[] ingredients)
        {
            RecipeDefinition recipe = SmithingSetup.BuildRecipe(name, output, station, level, ingredients);
            var serialized = new SerializedObject(recipe);
            serialized.FindProperty("_displayName").stringValue = amount > 1 ? $"{output.DisplayName} x{amount}" : output.DisplayName;
            serialized.FindProperty("_outputAmount").intValue = amount;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return recipe;
        }

        /// <summary>The oak gate's layout (posts, lintel, two leaves on hinge pivots - OakSetup's
        /// AddGateDoor opens them) in black bog wood, the leaves banded top, middle and bottom with
        /// iron, iron studs and big strap hinges.</summary>
        private static void BuildIronBoundGate(Transform root)
        {
            foreach (float x in new[] { -2.15f, 2.15f })
            {
                OakSetup.Box(root, x < 0f ? "PostL" : "PostR", _bogWood, new Vector3(x, 1.3f, 0f), new Vector3(0.34f, 2.6f, 0.34f));
                OakSetup.Box(root, "PostCap", _iron, new Vector3(x, 2.62f, 0f), new Vector3(0.38f, 0.06f, 0.38f));
            }

            OakSetup.Box(root, "Lintel", _bogWood, new Vector3(0f, 2.7f, 0f), new Vector3(4.7f, 0.28f, 0.36f));
            foreach (float side in new[] { -1f, 1f })
            {
                var hingeRoot = new GameObject(side < 0f ? "HingeL" : "HingeR").transform;
                hingeRoot.SetParent(root, false);
                hingeRoot.localPosition = new Vector3(2f * side, 0f, 0f);
                OakSetup.Box(hingeRoot, "Leaf", _bogWood, new Vector3(-1f * side, 1.2f, 0f), new Vector3(1.98f, 2.3f, 0.14f));
                foreach (float y in new[] { 0.35f, 1.2f, 2.05f })
                {
                    Object.DestroyImmediate(OakSetup.Box(hingeRoot, "Band", _iron, new Vector3(-1f * side, y, 0.08f), new Vector3(1.9f, 0.12f, 0.02f)).GetComponent<Collider>());
                    // A long strap hinge from the post side into the leaf.
                    Object.DestroyImmediate(OakSetup.Box(hingeRoot, "Strap", _iron, new Vector3(-0.35f * side, y, -0.08f), new Vector3(0.7f, 0.1f, 0.02f)).GetComponent<Collider>());
                    for (int stud = 0; stud < 4; stud++)
                    {
                        float x = -side * (0.3f + stud * 0.45f);
                        Object.DestroyImmediate(OakSetup.Box(hingeRoot, "Stud", _iron, new Vector3(x, y, 0.1f), Vector3.one * 0.05f).GetComponent<Collider>());
                    }
                }
            }

            OakSetup.Snap(root, "SnapPoint_BottomL", new Vector3(-1f, 0f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
            OakSetup.Snap(root, "SnapPoint_BottomR", new Vector3(1f, 0f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
        }

        /// <summary>A low square frame of bog-wood logs bristling with sharpened stakes, iron-tipped,
        /// leaning out. Nothing in it is solid - creatures walk (and the NavMesh runs) straight over
        /// it; the root gets a flat trigger the size of the trap so it can be aimed at.</summary>
        private static void BuildStakeTrap(Transform root)
        {
            foreach (float s in new[] { -0.9f, 0.9f })
            {
                Object.DestroyImmediate(OakSetup.Box(root, "Frame", _bogWood, new Vector3(s, 0.08f, 0f), new Vector3(0.16f, 0.16f, 1.96f)).GetComponent<Collider>());
                Object.DestroyImmediate(OakSetup.Box(root, "Frame", _bogWood, new Vector3(0f, 0.08f, s), new Vector3(1.96f, 0.16f, 0.16f)).GetComponent<Collider>());
            }

            for (int ix = -1; ix <= 1; ix++)
            {
                for (int iz = -1; iz <= 1; iz++)
                {
                    var foot = new Vector3(ix * 0.6f, 0f, iz * 0.6f);
                    Vector3 lean = new Vector3(ix * 14f + 4f * iz, 0f, -iz * 14f + 3f * ix);
                    Quaternion tilt = Quaternion.Euler(lean.z, 0f, lean.x);
                    Vector3 up = tilt * Vector3.up;
                    Object.DestroyImmediate(OakSetup.Box(root, "Stake", _bogWood, foot + up * 0.35f, new Vector3(0.07f, 0.7f, 0.07f), tilt.eulerAngles).GetComponent<Collider>());
                    Object.DestroyImmediate(OakSetup.Box(root, "Point", _iron, foot + up * 0.74f, new Vector3(0.05f, 0.12f, 0.05f), (tilt * Quaternion.Euler(0f, 45f, 0f)).eulerAngles).GetComponent<Collider>());
                }
            }

            var zone = root.gameObject.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.center = new Vector3(0f, 0.2f, 0f);
            zone.size = new Vector3(1.96f, 0.4f, 1.96f);
        }

        /// <summary>A point of interest in the swamp for the tier 4 boss's arena - the generic
        /// menhir ring placeholder; the boss itself is not designed yet (Boss Status).</summary>
        private static void AddSwampBossSlot(BiomeDefinition swamp)
        {
            string path = PoiFolder + "SwampBossSlot.asset";
            var poi = AssetDatabase.LoadAssetAtPath<PoiDefinition>(path);
            if (poi == null)
            {
                poi = ScriptableObject.CreateInstance<PoiDefinition>();
                AssetDatabase.CreateAsset(poi, path);
            }

            poi.EditorConfigure("SwampBossSlot", "Boss arena slot - tier 4 (Swamp)", new Color(0.4f, 0.9f, 0.3f), 1, 20,
                new List<BiomeDefinition> { swamp }, true, new Vector2(0.2f, 6f), new Vector2(0.05f, 0.85f), 600f, 0f);
            var serialized = new SerializedObject(poi);
            serialized.FindProperty("_prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaSlotPrefabPath);
            serialized.FindProperty("_clearRadius").floatValue = 16f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(poi);

            var settings = AssetDatabase.LoadAssetAtPath<HybridWorldSettings>(HybridSettingsPath);
            if (settings != null)
            {
                CreatureLootSetup.AddToList(new SerializedObject(settings), "_pointsOfInterest", poi);
            }
        }
    }
}
