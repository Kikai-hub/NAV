using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Gameplay.Combat;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Items;

namespace NAV.Editor
{
    /// <summary>
    /// Stage C - pack weapons become craftable: a curated set of LowPolyWeapons models (chosen
    /// by look: plain wooden ones early, copper-coloured for copper, plain steel for iron; the
    /// glowing and ornate ones are kept for later tiers) gets a material tier, a name, real
    /// stats (class base x tier multiplier) and a recipe at the Workbench or the Forge. The
    /// temporary dev weapon racks are removed from the open scene. Everything not in Table stays
    /// in the item database (old saves keep loading) but can't be obtained for now.
    ///
    /// Run after WeaponPackGenerator (it rewrites names/stats - re-run this afterwards), and
    /// after MetallurgySetup + SmithingSetup (bars, leather, the Forge). Save the scene.
    /// </summary>
    internal static class WeaponRecipesSetup
    {
        private const string LowPolyItems = "Assets/NAV/ScriptableObjects/Items/LowPoly/";
        private const string Items = "Assets/NAV/ScriptableObjects/Items/";
        private const string Recipes = "Assets/NAV/ScriptableObjects/Recipes/Weapons/";
        private const string Workbenches = "Assets/NAV/ScriptableObjects/Workbenches/";

        private enum Tier { Wood, Bone, Copper, Bronze, Iron, Silver, BlackSteel }

        private sealed class Entry
        {
            public string Model;
            public string Name;
            public Tier Tier;
            public bool Forge;
            public int Level;
            public string Description;
        }

        // Model id (LPW_ prefix added) -> what it becomes.
        private static readonly Entry[] Table =
        {
            new() { Model = "hammer1H1", Name = "Wooden Club", Tier = Tier.Wood, Level = 1, Description = "A knotty club. Better than bare fists." },
            new() { Model = "shield1", Name = "Wooden Shield", Tier = Tier.Wood, Level = 1, Description = "Planks and a grip - stops a wolf's bite." },
            new() { Model = "dagger9", Name = "Bone Knife", Tier = Tier.Bone, Level = 2, Description = "A curved blade carved from bone." },
            new() { Model = "spear1", Name = "Fang Spear", Tier = Tier.Bone, Level = 2, Description = "A hunting spear tipped with wolf fangs." },
            new() { Model = "hammer2H1", Name = "Great Club", Tier = Tier.Wood, Level = 2, Description = "A heavy two-handed club." },

            new() { Model = "dagger7_001", Name = "Copper Dagger", Tier = Tier.Copper, Forge = true, Level = 1, Description = "A short copper blade." },
            new() { Model = "sword1H2", Name = "Copper Sword", Tier = Tier.Copper, Forge = true, Level = 1, Description = "A one-handed copper sword." },
            new() { Model = "hammer1H2", Name = "Copper Mace", Tier = Tier.Copper, Forge = true, Level = 1, Description = "A copper-headed mace." },
            new() { Model = "spear2", Name = "Copper Spear", Tier = Tier.Copper, Forge = true, Level = 1, Description = "A spear with a copper head." },
            new() { Model = "shield11", Name = "Copper Shield", Tier = Tier.Copper, Forge = true, Level = 1, Description = "A round shield faced with copper." },
            new() { Model = "axe1H1", Name = "Copper Hand Axe", Tier = Tier.Copper, Forge = true, Level = 1, Description = "A light copper axe - for battle and for trees." },

            // Bronze (tier 3, Forge level 2 - the casting mould): models with golden fittings.
            new() { Model = "dagger2", Name = "Bronze Dagger", Tier = Tier.Bronze, Forge = true, Level = 2, Description = "A cast bronze dagger with a golden guard." },
            new() { Model = "sword1H6", Name = "Bronze Sword", Tier = Tier.Bronze, Forge = true, Level = 2, Description = "A cast bronze blade - harder than copper." },
            new() { Model = "hammer1H9", Name = "Bronze Mace", Tier = Tier.Bronze, Forge = true, Level = 2, Description = "A spiked bronze mace." },
            new() { Model = "spear4", Name = "Bronze Spear", Tier = Tier.Bronze, Forge = true, Level = 2, Description = "A spear with a bronze head and butt." },
            new() { Model = "shield14", Name = "Bronze Shield", Tier = Tier.Bronze, Forge = true, Level = 2, Description = "A kite shield with a bronze rim." },
            new() { Model = "axe1H3", Name = "Bronze Hand Axe", Tier = Tier.Bronze, Forge = true, Level = 2, Description = "A bronze hand axe - for battle and for trees." },

            new() { Model = "dagger3", Name = "Iron Dagger", Tier = Tier.Iron, Forge = true, Level = 3, Description = "A keen iron dagger." },
            new() { Model = "sword1H1", Name = "Iron Sword", Tier = Tier.Iron, Forge = true, Level = 3, Description = "A well-balanced iron sword." },
            new() { Model = "hammer1H4", Name = "Iron Mace", Tier = Tier.Iron, Forge = true, Level = 3, Description = "A flanged iron mace." },
            new() { Model = "spear3", Name = "Iron Spear", Tier = Tier.Iron, Forge = true, Level = 3, Description = "A long iron-tipped spear." },
            new() { Model = "shield13", Name = "Banded Shield", Tier = Tier.Iron, Forge = true, Level = 3, Description = "Oak boards bound with iron bands." },
            new() { Model = "axe1H2", Name = "Iron Hand Axe", Tier = Tier.Iron, Forge = true, Level = 3, Description = "An iron hand axe - for battle and for trees." },

            new() { Model = "sword2H1", Name = "Iron Greatsword", Tier = Tier.Iron, Forge = true, Level = 4, Description = "A two-handed iron sword." },
            new() { Model = "axe2H1", Name = "Iron Battle Axe", Tier = Tier.Iron, Forge = true, Level = 4, Description = "A great two-handed axe." },
            new() { Model = "hammer2H4", Name = "Iron Warhammer", Tier = Tier.Iron, Forge = true, Level = 4, Description = "A crushing two-handed hammer." },
            // Silver (tier 5, Forge level 5 - the smelting furnace): the bright, white-steel models;
            // silver bites the unclean (x1.5 against нечисть - ApplyWeapon).
            new() { Model = "sword1H4", Name = "Silver Sword", Tier = Tier.Silver, Forge = true, Level = 5, Description = "A bright silver-faced blade. Нечисть fears it." },
            new() { Model = "hammer1H8", Name = "Silver Mace", Tier = Tier.Silver, Forge = true, Level = 5, Description = "A mace with a silver head - it breaks the unclean." },
            new() { Model = "spear8", Name = "Silver Spear", Tier = Tier.Silver, Forge = true, Level = 5, Description = "A spear with a long silver blade." },
            new() { Model = "shield16", Name = "Silver Shield", Tier = Tier.Silver, Forge = true, Level = 5, Description = "A kite shield rimmed with silver, a charm-stone at its heart." },
            new() { Model = "axe1H4", Name = "Silver Hand Axe", Tier = Tier.Silver, Forge = true, Level = 5, Description = "A double-bitted silver hand axe - for battle and for trees." },
            new() { Model = "sword2H2", Name = "Silver Greatsword", Tier = Tier.Silver, Forge = true, Level = 5, Description = "A two-handed silver sword." },
            new() { Model = "axe2H5", Name = "Silver Battle Axe", Tier = Tier.Silver, Forge = true, Level = 5, Description = "A great silver battle axe." },
            new() { Model = "hammer2H6", Name = "Silver Warhammer", Tier = Tier.Silver, Forge = true, Level = 5, Description = "A two-handed hammer with a silver head." },
            // Black steel (tier 6, Forge level 6 - the quench tub): the dark, heavy models, hafts
            // of bog oak (BlackForestSetup makes the bars and the wood first).
            new() { Model = "dagger6", Name = "Black Steel Dagger", Tier = Tier.BlackSteel, Forge = true, Level = 6, Description = "A dark, thin blade quenched in the black forest's water." },
            new() { Model = "sword1H9", Name = "Black Steel Sword", Tier = Tier.BlackSteel, Forge = true, Level = 6, Description = "A black steel sword on a bog-oak grip." },
            new() { Model = "hammer1H10", Name = "Black Steel Mace", Tier = Tier.BlackSteel, Forge = true, Level = 6, Description = "A heavy black steel head - it dents what silver only scratches." },
            new() { Model = "spear6", Name = "Black Steel Spear", Tier = Tier.BlackSteel, Forge = true, Level = 6, Description = "A long bog-oak shaft with a black steel head." },
            new() { Model = "shield18", Name = "Black Steel Shield", Tier = Tier.BlackSteel, Forge = true, Level = 6, Description = "Bog-oak boards under rows of black steel studs." },
            new() { Model = "axe1H5", Name = "Black Steel Hand Axe", Tier = Tier.BlackSteel, Forge = true, Level = 6, Description = "A broad black steel axe - for battle, and it bites the bog oaks." },
            new() { Model = "sword2H5", Name = "Black Steel Greatsword", Tier = Tier.BlackSteel, Forge = true, Level = 6, Description = "A wide two-handed blade of black steel." },
            new() { Model = "axe2H3", Name = "Black Steel Battle Axe", Tier = Tier.BlackSteel, Forge = true, Level = 6, Description = "A great black steel battle axe." },
            new() { Model = "hammer2H8", Name = "Black Steel Warhammer", Tier = Tier.BlackSteel, Forge = true, Level = 6, Description = "A two-handed black steel hammer." },
        };

        // Class base stats - the same numbers WeaponPackGenerator writes (copper = 1x).
        private static readonly Dictionary<WeaponClass, (float damage, float block)> ClassBase = new()
        {
            { WeaponClass.Dagger, (9f, 0.25f) },
            { WeaponClass.Sword, (15f, 0.5f) },
            { WeaponClass.Greatsword, (25f, 0.6f) },
            { WeaponClass.HandAxe, (13f, 0.35f) },
            { WeaponClass.BattleAxe, (27f, 0.5f) },
            { WeaponClass.Mace, (17f, 0.4f) },
            { WeaponClass.Warhammer, (30f, 0.5f) },
            { WeaponClass.Spear, (18f, 0.35f) },
            { WeaponClass.Shield, (6f, 0.85f) },
        };

        private static float DamageScale(Tier tier) => tier switch
        {
            Tier.Wood => 0.7f,
            Tier.Bone => 0.85f,
            Tier.Copper => 1f,
            Tier.Bronze => 1.15f,
            Tier.Iron => 1.35f,
            Tier.Silver => 1.55f,
            _ => 1.8f,
        };

        // Shields: better tiers block more (capped below 100%).
        private static float BlockBonus(Tier tier) => tier switch
        {
            Tier.Wood => -0.15f,
            Tier.Bone => -0.1f,
            Tier.Copper => 0f,
            Tier.Bronze => 0.04f,
            Tier.Iron => 0.07f,
            Tier.Silver => 0.1f,
            _ => 0.13f,
        };

        [MenuItem("NAV/Build/Build Pack Weapon Recipes (Stage C)")]
        private static void Build()
        {
            var workbench = AssetDatabase.LoadAssetAtPath<WorkbenchDefinition>(Workbenches + "BasicWorkbench.asset");
            var forge = AssetDatabase.LoadAssetAtPath<WorkbenchDefinition>(Workbenches + "Forge.asset");
            ItemDefinition wood = Item("Wood");
            ItemDefinition leather = Item("Leather");
            ItemDefinition copperBar = Item("CopperBar");
            ItemDefinition ironBar = Item("IronBar");
            ItemDefinition bronzeBar = Item("BronzeBar");
            // Optional until the mountains exist (MountainSetup) - the silver entries are skipped without it.
            ItemDefinition silverBar = Item("SilverBar");
            // Optional until the black forest exists (BlackForestSetup) - those entries are skipped without them.
            ItemDefinition blackSteelBar = Item("BlackSteelBar");
            ItemDefinition bogOak = Item("BogOakWood");
            ItemDefinition bone = Item("Bone");
            ItemDefinition fang = Item("Fang");
            if (forge == null || copperBar == null || leather == null)
            {
                Debug.LogError("WeaponRecipesSetup: run the Stage C setups first (Ore/Smelting/Tanning, then Forge/Metal Tools).");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/NAV/ScriptableObjects/Recipes/Weapons"))
            {
                AssetDatabase.CreateFolder("Assets/NAV/ScriptableObjects/Recipes", "Weapons");
            }

            // The generator keeps them in per-class subfolders - look them up by id.
            var byId = new Dictionary<string, ItemDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { LowPolyItems.TrimEnd('/') }))
            {
                var candidate = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (candidate != null)
                {
                    byId[candidate.Id] = candidate;
                }
            }

            PlayerCrafting crafting = Object.FindAnyObjectByType<PlayerCrafting>(FindObjectsInactive.Include);
            int built = 0;
            foreach (Entry entry in Table)
            {
                byId.TryGetValue($"LPW_{entry.Model}", out ItemDefinition item);
                if (item == null || item.Weapon == null)
                {
                    Debug.LogError($"WeaponRecipesSetup: LPW_{entry.Model} (or its weapon) not found - run NAV/Items/Generate Weapons From LowPoly Pack.");
                    continue;
                }

                WeaponClass weaponClass = item.Weapon.WeaponClass;
                ApplyItem(item, entry);
                ApplyWeapon(item.Weapon, entry, weaponClass);

                if ((entry.Tier == Tier.Silver && silverBar == null) || (entry.Tier == Tier.BlackSteel && (blackSteelBar == null || bogOak == null)))
                {
                    continue;
                }

                ItemDefinition bar = entry.Tier switch
                {
                    Tier.BlackSteel => blackSteelBar,
                    Tier.Silver => silverBar,
                    Tier.Iron => ironBar,
                    Tier.Bronze => bronzeBar,
                    _ => copperBar,
                };
                // Tier 6+ hafts and grips are bog oak (PROGRESSION_TIERS_v0.1.md).
                ItemDefinition haft = entry.Tier == Tier.BlackSteel ? bogOak : wood;
                (ItemDefinition, int)[] cost = Cost(weaponClass, entry.Tier, bar, haft, leather, bone, fang);
                RecipeDefinition recipe = BuildRecipe($"{entry.Model}Recipe", item, entry.Forge ? forge : workbench, entry.Level, cost);
                if (crafting != null)
                {
                    CreatureLootSetup.AddToList(new SerializedObject(crafting), "_knownRecipes", recipe);
                }

                built++;
            }

            int racks = RemoveRacks();
            if (crafting != null)
            {
                EditorSceneManager.MarkSceneDirty(crafting.gameObject.scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"WeaponRecipesSetup: {built} pack weapons got a tier, stats and a recipe; {racks} temporary rack spawner(s) removed from the scene. Save the scene.");
        }

        private static void ApplyItem(ItemDefinition item, Entry entry)
        {
            var serialized = new SerializedObject(item);
            serialized.FindProperty("_displayName").stringValue = entry.Name;
            serialized.FindProperty("_description").stringValue = entry.Description;
            // Hand axes are also axes for trees: tool tier = progression tier (copper 2, bronze 3, iron 4).
            if (item.ToolType != ToolType.None)
            {
                serialized.FindProperty("_toolTier").intValue = entry.Tier switch
                {
                    Tier.BlackSteel => 6,
                    Tier.Silver => 5,
                    Tier.Iron => 4,
                    Tier.Bronze => 3,
                    Tier.Copper => 2,
                    _ => 1,
                };
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }

        private static void ApplyWeapon(WeaponDefinition weapon, Entry entry, WeaponClass weaponClass)
        {
            if (!ClassBase.TryGetValue(weaponClass, out (float damage, float block) stats))
            {
                return;
            }

            var serialized = new SerializedObject(weapon);
            serialized.FindProperty("_displayName").stringValue = entry.Name;
            serialized.FindProperty("_damage").floatValue = Mathf.Round(stats.damage * DamageScale(entry.Tier) * 10f) / 10f;
            serialized.FindProperty("_blockDamageReduction").floatValue = Mathf.Clamp(stats.block + BlockBonus(entry.Tier), 0.1f, 0.95f);
            serialized.FindProperty("_uncleanDamageMultiplier").floatValue = entry.Tier == Tier.Silver ? 1.5f : 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weapon);
        }

        /// <summary>Bigger weapons cost more; metal ones take bars + a wooden haft + a leather
        /// grip, wood/bone ones their own material.</summary>
        private static (ItemDefinition, int)[] Cost(WeaponClass weaponClass, Tier tier, ItemDefinition bar,
            ItemDefinition wood, ItemDefinition leather, ItemDefinition bone, ItemDefinition fang)
        {
            (int metal, int haft, int grip) = weaponClass switch
            {
                WeaponClass.Dagger => (2, 1, 1),
                WeaponClass.Sword => (4, 2, 1),
                WeaponClass.Greatsword => (7, 3, 2),
                WeaponClass.HandAxe => (3, 2, 1),
                WeaponClass.BattleAxe => (6, 4, 2),
                WeaponClass.Mace => (4, 2, 1),
                WeaponClass.Warhammer => (8, 4, 2),
                WeaponClass.Spear => (2, 4, 1),
                WeaponClass.Shield => (3, 6, 2),
                _ => (3, 2, 1),
            };

            switch (tier)
            {
                case Tier.Wood:
                    // Level-1 wood gear needs no leather (the tanning rack is the level-2 upgrade).
                    return weaponClass == WeaponClass.Warhammer
                        ? new[] { (wood, haft * 3), (leather, grip) }
                        : new[] { (wood, haft * 2) };
                case Tier.Bone:
                    return weaponClass == WeaponClass.Spear
                        ? new[] { (wood, haft), (fang, 2), (leather, grip) }
                        : new[] { (bone, 3), (leather, grip) };
                default:
                    return new[] { (bar, metal), (wood, haft), (leather, grip) };
            }
        }

        private static RecipeDefinition BuildRecipe(string name, ItemDefinition output, WorkbenchDefinition station, int level, (ItemDefinition item, int amount)[] ingredients)
        {
            string path = $"{Recipes}{name}.asset";
            var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);
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

        /// <summary>Removes the temporary dev weapon arsenal (WeaponArsenalSpawner) from the open
        /// scene - its racks were how the pack weapons were handed out before recipes existed.</summary>
        private static int RemoveRacks()
        {
            int removed = 0;
            foreach (NAV.Dev.WeaponArsenalSpawner spawner in Object.FindObjectsByType<NAV.Dev.WeaponArsenalSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(spawner.gameObject);
                removed++;
            }

            return removed;
        }

        private static ItemDefinition Item(string id) => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{Items}{id}.asset");
    }
}
