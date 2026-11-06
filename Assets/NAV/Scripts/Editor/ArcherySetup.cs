using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.Combat;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;
using NAV.Presentation.Animation;
using NAV.Presentation.Equipment;

namespace NAV.Editor
{
    /// <summary>
    /// Tier 4 - the bow (PROGRESSION_TIERS_v0.1.md), NAV > Combat > Build Bow And Arrows. Uses the
    /// Kevin Iglesias "Human Archer Animations FREE" pack (bow and arrow models; its clips are
    /// wired by NAV > Animation > Setup Player Combat Layers):
    ///   - Hunting Bow (Охотничий лук): bog wood, leather, resin at the Workbench level 2; held in
    ///     the left hand (B-handProp.L, as in the pack's demo), drawn with LMB (PlayerArchery); the
    ///     string and the arrow in the string hand by BowString;
    ///   - arrows (10 per craft): wood (Workbench), flint (Workbench), iron (Forge) - each an
    ///     ItemDefinition with an AmmoDefinition and its flying model (ArrowProjectile);
    ///   - wires PlayerArchery onto the scene's Player and PlayerAnimator to it, adds the recipes
    ///     to PlayerCrafting's known recipes and the items to DefaultItemDatabase.
    /// Re-runnable; saves the scene.
    /// </summary>
    internal static class ArcherySetup
    {
        private const string ItemFolder = "Assets/NAV/ScriptableObjects/Items/";
        private const string WeaponFolder = "Assets/NAV/ScriptableObjects/Weapons/";
        private const string AmmoFolder = "Assets/NAV/ScriptableObjects/Weapons/Ammo";
        private const string PrefabFolder = "Assets/NAV/Prefabs/Archery";
        private const string MaterialFolder = "Assets/NAV/Materials/Archery";
        private const string DatabasePath = "Assets/NAV/ScriptableObjects/Core/DefaultItemDatabase.asset";
        private const string WorkbenchPath = "Assets/NAV/ScriptableObjects/Workbenches/BasicWorkbench.asset";
        private const string ForgePath = "Assets/NAV/ScriptableObjects/Workbenches/Forge.asset";
        private const string PackDemo = "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Archer Animations/";
        private const string PackBowPath = PackDemo + "Prefabs/HumanArcher_Bow.prefab";
        private const string PackArrowPath = PackDemo + "Models/HumanArcher_Arrow.fbx";
        private const int ArrowsPerCraft = 10;
        // The pack's arrow: 1 m along +Y, tail at 0, tip at the top. Ours are a little shorter.
        private const float ArrowScale = 0.8f;
        // Where the pack's demo holds the bow in B-handProp.L.
        private static readonly Vector3 BowInHandOffset = new(0.01f, 0f, 0f);

        private struct ArrowKind
        {
            public string Id;
            public string Name;
            public string Description;
            public Color Tint;
            public float Damage;
            public float Recover;
        }

        [MenuItem("NAV/Combat/Build Bow And Arrows")]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("ArcherySetup: exit Play Mode first.");
                return;
            }

            ItemDefinition wood = Item("Wood");
            ItemDefinition bogWood = Item("BogWood");
            ItemDefinition leather = Item("Leather");
            ItemDefinition resin = Item("Resin");
            ItemDefinition flint = Item("Flint");
            ItemDefinition ironBar = Item("IronBar");
            var workbench = AssetDatabase.LoadAssetAtPath<WorkbenchDefinition>(WorkbenchPath);
            var forge = AssetDatabase.LoadAssetAtPath<WorkbenchDefinition>(ForgePath);
            var packBow = AssetDatabase.LoadAssetAtPath<GameObject>(PackBowPath);
            var packArrow = AssetDatabase.LoadAssetAtPath<GameObject>(PackArrowPath);
            if (wood == null || bogWood == null || leather == null || resin == null || flint == null || ironBar == null || workbench == null || forge == null)
            {
                Debug.LogError("ArcherySetup: run the swamp (Bog Wood), tanning (Leather) and smithing (Iron Bar, Forge) setups first.");
                return;
            }

            if (packBow == null || packArrow == null)
            {
                Debug.LogError("ArcherySetup: import Kevin Iglesias 'Human Archer Animations FREE' (its demo bow and arrow are used).");
                return;
            }

            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(AmmoFolder);

            var database = new SerializedObject(AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath));
            var recipes = new List<RecipeDefinition>();

            // ---- Arrows ----
            var kinds = new[]
            {
                new ArrowKind { Id = "WoodArrow", Name = "Wood Arrow", Description = "A fire-hardened wooden point. Cheap, light, breaks often.", Tint = new Color(1f, 0.92f, 0.82f), Damage = 8f, Recover = 0.5f },
                new ArrowKind { Id = "FlintArrow", Name = "Flint Arrow", Description = "A knapped flint head - cuts deep into hide.", Tint = new Color(0.78f, 0.78f, 0.8f), Damage = 14f, Recover = 0.6f },
                new ArrowKind { Id = "IronArrow", Name = "Iron Arrow", Description = "A forged iron bodkin - goes through mail and bone.", Tint = new Color(0.82f, 0.88f, 1f), Damage = 24f, Recover = 0.75f },
            };

            var arrowItems = new Dictionary<string, ItemDefinition>();
            foreach (ArrowKind kind in kinds)
            {
                Material material = TintedCopy(packArrow, $"{kind.Id}_material", kind.Tint);
                GameObject projectile = BuildArrowProjectile(kind.Id, packArrow, material);
                ItemDefinition item = CreatureLootSetup.BuildItem(kind.Id, kind.Name, kind.Description, 100, 0.02f,
                    root => AddArrow(root, packArrow, material, new Vector3(0f, 0.03f, 0.4f), Quaternion.identity),
                    Quaternion.Euler(0f, 45f, 0f));
                AmmoDefinition ammo = Ammo(kind, projectile);
                var serialized = new SerializedObject(item);
                serialized.FindProperty("_category").enumValueIndex = (int)ItemCategory.Weapon;
                serialized.FindProperty("_ammo").objectReferenceValue = ammo;
                serialized.FindProperty("_density").floatValue = 0.6f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                CreatureLootSetup.AddToList(database, "_items", item);
                arrowItems[kind.Id] = item;
            }

            recipes.Add(Recipe("WoodArrowRecipe", arrowItems["WoodArrow"], ArrowsPerCraft, workbench, 1, (wood, 2)));
            recipes.Add(Recipe("FlintArrowRecipe", arrowItems["FlintArrow"], ArrowsPerCraft, workbench, 1, (wood, 2), (flint, 2)));
            recipes.Add(Recipe("IronArrowRecipe", arrowItems["IronArrow"], ArrowsPerCraft, forge, 1, (wood, 2), (ironBar, 1)));

            // ---- The bow ----
            GameObject held = BuildHeldBow(packBow, packArrow);
            WeaponDefinition weapon = BowWeapon();
            ItemDefinition bow = CreatureLootSetup.BuildItem("HuntingBow", "Hunting Bow",
                "A long bow of black bog wood with a leather grip. Hold LMB to draw, release to loose an arrow; a full draw flies farthest and hits hardest.",
                1, 1.5f, root => AddBowModel(root, packBow), Quaternion.Euler(0f, 0f, 45f));
            var bowSerialized = new SerializedObject(bow);
            bowSerialized.FindProperty("_category").enumValueIndex = (int)ItemCategory.Weapon;
            bowSerialized.FindProperty("_weapon").objectReferenceValue = weapon;
            bowSerialized.FindProperty("_heldPrefab").objectReferenceValue = held;
            bowSerialized.FindProperty("_heldInLeftHand").boolValue = true;
            bowSerialized.FindProperty("_density").floatValue = 0.7f;
            bowSerialized.ApplyModifiedPropertiesWithoutUndo();
            CreatureLootSetup.AddToList(database, "_items", bow);
            recipes.Add(Recipe("HuntingBowRecipe", bow, 1, workbench, 2, (bogWood, 4), (leather, 2), (resin, 2)));

            WirePlayer(recipes);
            AssetDatabase.SaveAssets();
            Debug.Log("ArcherySetup: Hunting Bow and Wood/Flint/Iron arrows (Human Archer pack models), recipes, PlayerArchery wired. Scene saved. Run NAV > Animation > Setup Player Combat Layers for the bow clips.");
        }

        private static ItemDefinition Item(string id) => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}{id}.asset");

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        /// <summary>A copy of the pack model's material with its colour multiplied - the three
        /// arrows look apart.</summary>
        private static Material TintedCopy(GameObject model, string name, Color tint)
        {
            Material source = model.GetComponentInChildren<Renderer>().sharedMaterial;
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.CopyPropertiesFromMaterial(source);
            string colour = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            if (material.HasProperty(colour))
            {
                material.SetColor(colour, source.GetColor(colour) * tint);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The pack's arrow turned to our convention: the tip at the holder's origin, the
        /// shaft back along -Z.</summary>
        private static void AddArrow(Transform parent, GameObject packArrow, Material material, Vector3 offset, Quaternion rotation)
        {
            var holder = new GameObject("Arrow").transform;
            holder.SetParent(parent, false);
            holder.localPosition = offset;
            holder.localRotation = rotation;
            var model = (GameObject)Object.Instantiate(packArrow, holder);
            model.name = "Model";
            // +Y (tail -> tip) onto +Z, then back by its length so the tip sits at the origin.
            model.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            model.transform.localScale = Vector3.one * ArrowScale;
            model.transform.localPosition = new Vector3(0f, 0f, -ArrowScale);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial = material;
            }
        }

        private static GameObject BuildArrowProjectile(string id, GameObject packArrow, Material material)
        {
            var root = new GameObject($"{id}_Projectile");
            AddArrow(root.transform, packArrow, material, Vector3.zero, Quaternion.identity);
            root.AddComponent<ArrowProjectile>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{root.name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static AmmoDefinition Ammo(ArrowKind kind, GameObject projectile)
        {
            string path = $"{AmmoFolder}/{kind.Id}Ammo.asset";
            var ammo = AssetDatabase.LoadAssetAtPath<AmmoDefinition>(path);
            if (ammo == null)
            {
                ammo = ScriptableObject.CreateInstance<AmmoDefinition>();
                AssetDatabase.CreateAsset(ammo, path);
            }

            var serialized = new SerializedObject(ammo);
            serialized.FindProperty("_damage").floatValue = kind.Damage;
            serialized.FindProperty("_projectilePrefab").objectReferenceValue = projectile;
            serialized.FindProperty("_recoverChance").floatValue = kind.Recover;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ammo);
            return ammo;
        }

        /// <summary>The pack's bow lying on the ground (the pickup) - the model only.</summary>
        private static void AddBowModel(Transform parent, GameObject packBow)
        {
            var model = (GameObject)Object.Instantiate(packBow, parent);
            model.name = "Bow";
            model.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            model.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            foreach (LineRenderer line in model.GetComponentsInChildren<LineRenderer>())
            {
                Object.DestroyImmediate(line);
            }
        }

        /// <summary>The pack's bow as held: where the demo holds it in B-handProp.L, its own string
        /// LineRenderer driven by BowString (tips, rest nock, anchor at full draw, the pack's arrow
        /// for the string hand).</summary>
        private static GameObject BuildHeldBow(GameObject packBow, GameObject packArrow)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(packBow);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "HuntingBow_Held";
            root.transform.localPosition = BowInHandOffset;
            root.transform.localRotation = Quaternion.identity;

            var bowString = root.GetComponent<BowString>();
            if (bowString == null)
            {
                bowString = root.AddComponent<BowString>();
            }

            var serialized = new SerializedObject(bowString);
            serialized.FindProperty("_top").objectReferenceValue = Find(root.transform, "B-bowTip01");
            serialized.FindProperty("_bottom").objectReferenceValue = Find(root.transform, "B-bowTip02");
            serialized.FindProperty("_rest").objectReferenceValue = Find(root.transform, "B-nockPoint");
            serialized.FindProperty("_anchor").objectReferenceValue = Find(root.transform, "AnchorPoint");
            serialized.FindProperty("_arrowInHandPrefab").objectReferenceValue = packArrow;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/HuntingBow_Held.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            Debug.LogError($"ArcherySetup: '{name}' not found in the pack's bow.");
            return null;
        }

        private static WeaponDefinition BowWeapon()
        {
            string path = WeaponFolder + "HuntingBowWeapon.asset";
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (weapon == null)
            {
                weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(weapon, path);
            }

            var serialized = new SerializedObject(weapon);
            serialized.FindProperty("_displayName").stringValue = "Hunting Bow";
            serialized.FindProperty("_weaponClass").intValue = (int)WeaponClass.Bow;
            serialized.FindProperty("_attackStyle").intValue = (int)WeaponAttackStyle.Bow;
            serialized.FindProperty("_damage").floatValue = 14f;
            // The crosshair's "target in sight" reach (a bow's real reach is its arrow's flight).
            serialized.FindProperty("_range").floatValue = 60f;
            serialized.FindProperty("_attackCooldown").floatValue = 0f;
            serialized.FindProperty("_attackStaminaCost").floatValue = 8f;
            serialized.FindProperty("_drawSeconds").floatValue = 0.9f;
            serialized.FindProperty("_projectileSpeed").floatValue = 50f;
            serialized.FindProperty("_blockDamageReduction").floatValue = 0.25f;
            serialized.FindProperty("_blockStaminaCostPerHit").floatValue = 12f;
            serialized.FindProperty("_parryWindowSeconds").floatValue = 0.1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weapon);
            return weapon;
        }

        private static RecipeDefinition Recipe(string name, ItemDefinition output, int amount, WorkbenchDefinition station, int level, params (ItemDefinition item, int amount)[] ingredients)
        {
            RecipeDefinition recipe = SmithingSetup.BuildRecipe(name, output, station, level, ingredients);
            var serialized = new SerializedObject(recipe);
            serialized.FindProperty("_displayName").stringValue = amount > 1 ? $"{output.DisplayName} x{amount}" : output.DisplayName;
            serialized.FindProperty("_outputAmount").intValue = amount;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return recipe;
        }

        private static void WirePlayer(List<RecipeDefinition> recipes)
        {
            var combat = Object.FindAnyObjectByType<PlayerCombat>(FindObjectsInactive.Include);
            if (combat == null)
            {
                Debug.LogWarning("ArcherySetup: no PlayerCombat in the open scene - add PlayerArchery by hand.");
                return;
            }

            GameObject player = combat.gameObject;
            var combatSerialized = new SerializedObject(combat);
            var archery = player.GetComponent<PlayerArchery>();
            if (archery == null)
            {
                archery = player.AddComponent<PlayerArchery>();
            }

            var serialized = new SerializedObject(archery);
            serialized.FindProperty("_inputHandler").objectReferenceValue = combatSerialized.FindProperty("_inputHandler").objectReferenceValue;
            serialized.FindProperty("_combat").objectReferenceValue = combat;
            serialized.FindProperty("_equipment").objectReferenceValue = combatSerialized.FindProperty("_equipment").objectReferenceValue;
            serialized.FindProperty("_inventory").objectReferenceValue = player.GetComponent<PlayerInventory>();
            serialized.FindProperty("_stamina").objectReferenceValue = combatSerialized.FindProperty("_stamina").objectReferenceValue;
            serialized.FindProperty("_cameraTransform").objectReferenceValue = combatSerialized.FindProperty("_cameraTransform").objectReferenceValue;
            serialized.FindProperty("_building").objectReferenceValue = combatSerialized.FindProperty("_building").objectReferenceValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var animator = player.GetComponentInChildren<PlayerAnimator>(true);
            if (animator != null)
            {
                var animatorSerialized = new SerializedObject(animator);
                animatorSerialized.FindProperty("_archery").objectReferenceValue = archery;
                animatorSerialized.ApplyModifiedPropertiesWithoutUndo();
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

            EditorSceneManager.MarkSceneDirty(player.scene);
            EditorSceneManager.SaveScene(player.scene);
        }
    }
}
