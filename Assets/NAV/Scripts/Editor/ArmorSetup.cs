using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Equipment;
using NAV.Gameplay.Items;
using NAV.Presentation.Equipment;

namespace NAV.Editor
{
    /// <summary>
    /// Stage C - armour. Sets of head / chest / legs, one per tier built so far
    /// (PROGRESSION_TIERS_v0.1.md):
    ///   - Leather (tier 1, Workbench level 2 - the tanning rack): cap, tunic, trousers;
    ///   - Copper (tier 2, Forge level 1): helm, scale shirt, greaves;
    ///   - Bronze (tier 3, Forge level 2 - the casting mould): helm, cuirass, greaves;
    ///   - Iron (tier 4, Forge level 3 - the anvil): shelom (the Rus pointed helmet), chainmail,
    ///     greaves.
    /// Armour values follow ArmorMath (Valheim's curve) and the plan's scale: leather 4 in
    /// total, copper 8, bronze 11, iron 15 - a wolf's 12-damage bite then does 8, 4.5, 3.3, 2.4.
    ///
    /// Looks are PLACEHOLDERS built from primitives and a procedural dome, sized to the Kevin
    /// Iglesias dummy (measured from its mesh): each worn part is a prefab in character space
    /// with its origin at a bone's pivot (see ArmorView). Also adds ArmorView to the character
    /// and saves the scene. Run after MetallurgySetup and SmithingSetup; re-run to rebuild.
    /// </summary>
    internal static class ArmorSetup
    {
        private const string ArmorFolder = "Assets/NAV/ScriptableObjects/Armor";
        private const string WornFolder = "Assets/NAV/Prefabs/Armor";
        private const string MaterialFolder = "Assets/NAV/Materials/Items/";
        private const string DomeMeshPath = "Assets/NAV/Art/Models/ArmorDome.asset";
        private const string Items = "Assets/NAV/ScriptableObjects/Items/";
        private const string Workbenches = "Assets/NAV/ScriptableObjects/Workbenches/";

        // Bone pivots of the dummy in its bind pose, relative to the character's feet - used only
        // to lay the parts out together for the pickup model and icon.
        private static readonly Dictionary<ArmorBone, Vector3> RestPivots = new()
        {
            { ArmorBone.Head, new Vector3(0f, 1.607f, 0.017f) },
            { ArmorBone.Chest, new Vector3(0f, 1.264f, 0.025f) },
            { ArmorBone.ThighLeft, new Vector3(-0.087f, 0.925f, 0.013f) },
            { ArmorBone.ThighRight, new Vector3(0.087f, 0.925f, 0.013f) },
            { ArmorBone.ShinLeft, new Vector3(-0.118f, 0.515f, 0.027f) },
            { ArmorBone.ShinRight, new Vector3(0.118f, 0.515f, 0.027f) },
        };

        private enum Metal { Leather, Copper, Bronze, Iron, Silver, Fur, BlackSteel, BlackFur }

        private struct Mats
        {
            public Material Leather;
            public Material Strap;
            public Material Copper;
            public Material Bronze;
            public Material Iron;
            public Material Mail;
            public Material Silver;
            public Material Fur;
            public Material BlackSteel;
            public Material BlackFur;
        }

        private static Mesh _dome;
        private static Mats _mats;

        [MenuItem("NAV/Build/Build Armour (Stage C)")]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("ArmorSetup: exit Play Mode first.");
                return;
            }

            ItemDefinition leather = Item("Leather");
            ItemDefinition hide = Item("Hide");
            ItemDefinition boneGlue = Item("BoneGlue");
            ItemDefinition copperBar = Item("CopperBar");
            ItemDefinition ironBar = Item("IronBar");
            ItemDefinition bronzeBar = Item("BronzeBar");
            var workbench = AssetDatabase.LoadAssetAtPath<WorkbenchDefinition>(Workbenches + "BasicWorkbench.asset");
            var forge = AssetDatabase.LoadAssetAtPath<WorkbenchDefinition>(Workbenches + "Forge.asset");
            if (leather == null || copperBar == null || bronzeBar == null || ironBar == null || forge == null)
            {
                Debug.LogError("ArmorSetup: run NAV/Build/Build Ore, Smelting and Tanning and NAV/Build/Build Forge and Metal Tools first.");
                return;
            }

            EnsureFolder(ArmorFolder);
            EnsureFolder(WornFolder);
            _dome = BuildDomeMesh();
            _mats = new Mats
            {
                Leather = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "Leather_material.mat"),
                Strap = GetOrCreateMaterial("ArmorStrap_material", new Color(0.28f, 0.18f, 0.11f), 0.25f, 0f),
                Copper = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "Copper_material.mat"),
                Bronze = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "Bronze_material.mat"),
                Iron = GetOrCreateMaterial("ArmorIron_material", new Color(0.55f, 0.56f, 0.6f), 0.6f, 0.85f),
                Mail = GetOrCreateMaterial("Chainmail_material", new Color(0.42f, 0.43f, 0.46f), 0.3f, 0.75f),
                Silver = GetOrCreateMaterial("ArmorSilver_material", new Color(0.82f, 0.84f, 0.88f), 0.75f, 0.9f),
                Fur = GetOrCreateMaterial("ArmorFur_material", new Color(0.52f, 0.44f, 0.36f), 0.05f, 0f),
                BlackSteel = GetOrCreateMaterial("ArmorBlackSteel_material", new Color(0.13f, 0.13f, 0.15f), 0.65f, 0.9f),
                BlackFur = GetOrCreateMaterial("ArmorBlackFur_material", new Color(0.1f, 0.09f, 0.09f), 0.05f, 0f),
            };

            var pieces = new List<(ItemDefinition item, RecipeDefinition recipe)>
            {
                Piece("LeatherCap", "Leather Cap", "A snug cap of tanned leather.", ArmorSlot.Head, 1f, 1f, Metal.Leather,
                    workbench, 2, (leather, 2), (hide, 1)),
                Piece("LeatherTunic", "Leather Tunic", "A thick leather tunic, stitched and glued in layers.", ArmorSlot.Chest, 2f, 3f, Metal.Leather,
                    workbench, 2, (leather, 5), (boneGlue, 1)),
                Piece("LeatherTrousers", "Leather Trousers", "Hard-wearing leather trousers.", ArmorSlot.Legs, 1f, 2f, Metal.Leather,
                    workbench, 2, (leather, 4)),

                Piece("CopperHelm", "Copper Helm", "A rounded copper helm with a nose guard.", ArmorSlot.Head, 2f, 2f, Metal.Copper,
                    forge, 1, (copperBar, 3), (leather, 1)),
                Piece("CopperScaleShirt", "Copper Scale Shirt", "Copper scales riveted onto leather.", ArmorSlot.Chest, 4f, 6f, Metal.Copper,
                    forge, 1, (copperBar, 6), (leather, 3)),
                Piece("CopperGreaves", "Copper Greaves", "Copper plates strapped over the shins.", ArmorSlot.Legs, 2f, 4f, Metal.Copper,
                    forge, 1, (copperBar, 4), (leather, 2)),

                // Bronze (tier 3, Forge level 2 - the casting mould).
                Piece("BronzeHelm", "Bronze Helm", "A cast bronze helm with cheek guards and a nose guard.", ArmorSlot.Head, 3f, 2.5f, Metal.Bronze,
                    forge, 2, (bronzeBar, 3), (leather, 1)),
                Piece("BronzeCuirass", "Bronze Cuirass", "Hammered bronze plates over a leather jerkin.", ArmorSlot.Chest, 5f, 7f, Metal.Bronze,
                    forge, 2, (bronzeBar, 6), (leather, 3)),
                Piece("BronzeGreaves", "Bronze Greaves", "Bronze shin and knee guards.", ArmorSlot.Legs, 3f, 4.5f, Metal.Bronze,
                    forge, 2, (bronzeBar, 4), (leather, 2)),

                Piece("IronShelom", "Iron Shelom", "A tall pointed iron helmet, as the old warriors wore.", ArmorSlot.Head, 4f, 3f, Metal.Iron,
                    forge, 3, (ironBar, 3), (leather, 1)),
                Piece("IronChainmail", "Iron Chainmail", "A long shirt of iron rings - heavy, but a wolf's teeth can't get through.", ArmorSlot.Chest, 7f, 9f, Metal.Iron,
                    forge, 3, (ironBar, 8), (leather, 2)),
                Piece("IronGreaves", "Iron Greaves", "Iron shin guards over mail leggings.", ArmorSlot.Legs, 4f, 5f, Metal.Iron,
                    forge, 3, (ironBar, 4), (leather, 2)),
            };

            // Tier 5 (the mountains - MountainSetup makes silver and fur first): silver ~5/10/5 at
            // the Forge level 5 (the smelting furnace); fur hood and cloak at the Workbench level 4
            // (the loom) - they keep the cold off (warmth: hood 0.5, cloak 1).
            ItemDefinition silverBar = Item("SilverBar");
            ItemDefinition fur = Item("Fur");
            if (silverBar != null && fur != null)
            {
                pieces.Add(Piece("SilverHelm", "Silver Helm", "A tall silver helm with a nose guard and a mail aventail - bright, and hated by the unclean.", ArmorSlot.Head, 5f, 3f, Metal.Silver,
                    forge, 5, (silverBar, 3), (leather, 1)));
                pieces.Add(Piece("SilverCuirass", "Silver Cuirass", "Silver-faced plates over mail.", ArmorSlot.Chest, 10f, 10f, Metal.Silver,
                    forge, 5, (silverBar, 8), (leather, 2)));
                pieces.Add(Piece("SilverGreaves", "Silver Greaves", "Silver shin and knee guards over mail leggings.", ArmorSlot.Legs, 5f, 6f, Metal.Silver,
                    forge, 5, (silverBar, 4), (leather, 2)));
                pieces.Add(WarmPiece(Piece("FurHood", "Fur Hood", "A deep hood of snow-leopard fur. Warm.", ArmorSlot.Head, 2f, 1f, Metal.Fur,
                    workbench, 4, (fur, 2), (leather, 1)), "FurHood", 0.5f));
                pieces.Add(WarmPiece(Piece("FurCloak", "Fur Cloak", "A heavy cloak of snow-leopard fur - keeps the mountain cold off.", ArmorSlot.Cloak, 1f, 2f, Metal.Fur,
                    workbench, 4, (fur, 4), (leather, 2)), "FurCloak", 1f));
            }
            else
            {
                Debug.LogWarning("ArmorSetup: no Silver Bar / Fur yet - run NAV > World > Build Mountains (Tier 5), then this again for the silver and fur pieces.");
            }

            // Tier 6 (the black forest - BlackForestSetup makes black steel and the volkolak pelt
            // first): black steel 7/12/7 at the Forge level 6 (the quench tub); the volkolak cloak
            // at the Workbench level 4 (the loom) - a little more armour than the fur one, as warm.
            ItemDefinition blackSteelBar = Item("BlackSteelBar");
            ItemDefinition pelt = Item("VolkolakPelt");
            if (blackSteelBar != null && pelt != null)
            {
                pieces.Add(Piece("BlackSteelHelm", "Black Steel Helm", "A tall black steel shelom with a mask over the eyes.", ArmorSlot.Head, 7f, 3.5f, Metal.BlackSteel,
                    forge, 6, (blackSteelBar, 4), (leather, 1)));
                pieces.Add(Piece("BlackSteelCuirass", "Black Steel Cuirass", "Black steel plates over mail, lined with volkolak pelt.", ArmorSlot.Chest, 12f, 11f, Metal.BlackSteel,
                    forge, 6, (blackSteelBar, 9), (pelt, 1), (leather, 2)));
                pieces.Add(Piece("BlackSteelGreaves", "Black Steel Greaves", "Black steel shin and knee guards over mail leggings.", ArmorSlot.Legs, 7f, 6.5f, Metal.BlackSteel,
                    forge, 6, (blackSteelBar, 5), (leather, 2)));
                pieces.Add(WarmPiece(Piece("VolkolakCloak", "Volkolak Cloak", "A cloak of black volkolak pelt. Warm, and the forest's нечисть smells its own kind on it.", ArmorSlot.Cloak, 2f, 2f, Metal.BlackFur,
                    workbench, 4, (pelt, 3), (leather, 2)), "VolkolakCloak", 1f));
            }
            else
            {
                Debug.LogWarning("ArmorSetup: no Black Steel Bar / Volkolak Pelt yet - run NAV > World > Build Black Forest (Tier 6), then this again for the black steel pieces.");
            }

            var itemDatabase = new SerializedObject(AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/NAV/ScriptableObjects/Core/DefaultItemDatabase.asset"));
            PlayerCrafting crafting = Object.FindAnyObjectByType<PlayerCrafting>(FindObjectsInactive.Include);
            foreach ((ItemDefinition item, RecipeDefinition recipe) in pieces)
            {
                CreatureLootSetup.AddToList(itemDatabase, "_items", item);
                if (crafting != null)
                {
                    CreatureLootSetup.AddToList(new SerializedObject(crafting), "_knownRecipes", recipe);
                }
            }

            WireArmorView();
            AssetDatabase.SaveAssets();
            if (crafting != null)
            {
                EditorSceneManager.SaveScene(crafting.gameObject.scene);
            }

            Debug.Log($"ArmorSetup: built {pieces.Count} armour pieces with recipes; ArmorView on the character. Scene saved.");
        }

        private static (ItemDefinition, RecipeDefinition) Piece(string id, string displayName, string description, ArmorSlot slot,
            float armor, float weight, Metal metal, WorkbenchDefinition station, int level, params (ItemDefinition item, int amount)[] cost)
        {
            List<(ArmorBone bone, System.Action<Transform> build)> parts = Parts(slot, metal);

            // Worn models, one prefab per bone.
            var worn = new List<(ArmorBone bone, GameObject prefab)>();
            foreach ((ArmorBone bone, System.Action<Transform> build) in parts)
            {
                var root = new GameObject($"{id}_{bone}");
                build(root.transform);
                string path = $"{WornFolder}/{id}_{bone}.prefab";
                worn.Add((bone, PrefabUtility.SaveAsPrefabAsset(root, path)));
                Object.DestroyImmediate(root);
            }

            // Pickup + icon: the same parts laid out as on the body, centred.
            Vector3 centre = Vector3.zero;
            foreach ((ArmorBone bone, _) in parts)
            {
                centre += RestPivots[bone];
            }

            centre /= parts.Count;
            ItemDefinition item = CreatureLootSetup.BuildItem(id, displayName, description, 1, weight, root =>
            {
                foreach ((ArmorBone bone, System.Action<Transform> build) in parts)
                {
                    var group = new GameObject(bone.ToString()).transform;
                    group.SetParent(root, false);
                    group.localPosition = RestPivots[bone] - centre;
                    build(group);
                }
            }, Quaternion.Euler(12f, 200f, 0f));

            string armorPath = $"{ArmorFolder}/{id}Armor.asset";
            var definition = AssetDatabase.LoadAssetAtPath<ArmorDefinition>(armorPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ArmorDefinition>();
                AssetDatabase.CreateAsset(definition, armorPath);
            }

            var armorSerialized = new SerializedObject(definition);
            armorSerialized.FindProperty("_slot").enumValueIndex = (int)slot;
            armorSerialized.FindProperty("_armor").floatValue = armor;
            SerializedProperty list = armorSerialized.FindProperty("_wornParts");
            list.arraySize = worn.Count;
            for (int i = 0; i < worn.Count; i++)
            {
                list.GetArrayElementAtIndex(i).FindPropertyRelative("_bone").enumValueIndex = (int)worn[i].bone;
                list.GetArrayElementAtIndex(i).FindPropertyRelative("_prefab").objectReferenceValue = worn[i].prefab;
            }

            armorSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            var itemSerialized = new SerializedObject(item);
            itemSerialized.FindProperty("_category").enumValueIndex = (int)ItemCategory.Armor;
            itemSerialized.FindProperty("_armor").objectReferenceValue = definition;
            itemSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);

            RecipeDefinition recipe = SmithingSetup.BuildRecipe($"{id}Recipe", item, station, level, cost);
            return (item, recipe);
        }

        /// <summary>Sets the warmth (PlayerCold) on a piece's ArmorDefinition.</summary>
        private static (ItemDefinition, RecipeDefinition) WarmPiece((ItemDefinition, RecipeDefinition) piece, string id, float warmth)
        {
            var definition = AssetDatabase.LoadAssetAtPath<ArmorDefinition>($"{ArmorFolder}/{id}Armor.asset");
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_warmth").floatValue = warmth;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return piece;
        }

        // ---- Shapes (character space, origin = bone pivot; Unity cylinder = 1 wide, 2 tall) ----

        private static List<(ArmorBone, System.Action<Transform>)> Parts(ArmorSlot slot, Metal metal)
        {
            switch (slot)
            {
                case ArmorSlot.Head:
                    return new List<(ArmorBone, System.Action<Transform>)> { (ArmorBone.Head, t => BuildHelmet(t, metal)) };
                case ArmorSlot.Chest:
                    return new List<(ArmorBone, System.Action<Transform>)> { (ArmorBone.Chest, t => BuildBody(t, metal)) };
                case ArmorSlot.Cloak:
                    return new List<(ArmorBone, System.Action<Transform>)> { (ArmorBone.Chest, t => BuildCloak(t, metal)) };
                default:
                    return new List<(ArmorBone, System.Action<Transform>)>
                    {
                        (ArmorBone.ThighLeft, t => BuildThigh(t, metal, -1f)),
                        (ArmorBone.ThighRight, t => BuildThigh(t, metal, 1f)),
                        (ArmorBone.ShinLeft, t => BuildShin(t, metal, -1f)),
                        (ArmorBone.ShinRight, t => BuildShin(t, metal, 1f)),
                    };
            }
        }

        private static void BuildHelmet(Transform root, Metal metal)
        {
            Material shell = Shell(metal);
            float height = metal == Metal.Leather ? 0.22f : metal == Metal.Copper ? 0.25f : metal == Metal.Bronze ? 0.27f : 0.3f;
            AddDome(root, "Dome", shell, new Vector3(0f, 0.1f, 0.025f), new Vector3(0.285f, height, 0.305f));
            Material rim = metal == Metal.Leather ? _mats.Strap : shell;
            if (metal == Metal.Fur || metal == Metal.BlackFur)
            {
                // A deep, shaggy hood: a fat fur roll round the face and down the nape.
                CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Rim", shell, new Vector3(0f, 0.09f, 0.025f), Vector3.zero, new Vector3(0.33f, 0.04f, 0.35f));
                CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Nape", shell, new Vector3(0f, 0.0f, -0.07f), new Vector3(-15f, 0f, 0f), new Vector3(0.3f, 0.08f, 0.2f));
                return;
            }

            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Rim", rim, new Vector3(0f, 0.105f, 0.025f), Vector3.zero, new Vector3(0.3f, 0.014f, 0.32f));
            if (metal == Metal.Leather)
            {
                return;
            }

            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Nasal", shell, new Vector3(0f, 0.06f, 0.168f), Vector3.zero, new Vector3(0.026f, 0.1f, 0.014f));
            if (metal == Metal.Bronze)
            {
                // Cheek guards - tells the bronze helm from the copper one.
                foreach (float side in new[] { -1f, 1f })
                {
                    CreatureLootSetup.AddPart(root, PrimitiveType.Cube, side < 0f ? "CheekL" : "CheekR", shell,
                        new Vector3(0.132f * side, 0.035f, 0.07f), new Vector3(0f, 18f * side, 0f), new Vector3(0.02f, 0.11f, 0.1f));
                }
            }

            if (metal == Metal.BlackSteel)
            {
                // A half-mask over the eyes - tells the black helm from the iron shelom.
                CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Mask", shell, new Vector3(0f, 0.075f, 0.155f), Vector3.zero, new Vector3(0.2f, 0.05f, 0.03f));
            }

            if (metal == Metal.Iron || metal == Metal.Silver || metal == Metal.BlackSteel)
            {
                // The shelom's spike and a mail curtain over the nape.
                CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Spike", shell, new Vector3(0f, 0.27f, 0.025f), Vector3.zero, new Vector3(0.03f, 0.05f, 0.03f));
                CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Aventail", _mats.Mail, new Vector3(0f, 0.03f, -0.03f), new Vector3(-12f, 0f, 0f), new Vector3(0.27f, 0.07f, 0.22f));
            }
        }

        private static void BuildBody(Transform root, Metal metal)
        {
            // Bottom edge: tunic at the hips, scale shirt a little higher, chainmail to mid-thigh.
            float bottom = metal == Metal.Leather ? 0.9f : metal == Metal.Copper ? 0.96f : metal == Metal.Bronze ? 0.92f : 0.84f;
            const float top = 1.42f;
            const float chestPivot = 1.264f;
            float halfHeight = (top - bottom) * 0.5f;
            float centre = (top + bottom) * 0.5f - chestPivot;
            Material body = metal == Metal.Iron ? _mats.Mail : Shell(metal);
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Body", body, new Vector3(0f, centre, 0.02f), Vector3.zero, new Vector3(0.43f, halfHeight, 0.32f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Belt", _mats.Strap, new Vector3(0f, 1.0f - chestPivot, 0.02f), Vector3.zero, new Vector3(0.45f, 0.025f, 0.34f));
            if (metal == Metal.Leather)
            {
                CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Collar", _mats.Strap, new Vector3(0f, top - chestPivot, 0.02f), Vector3.zero, new Vector3(0.36f, 0.02f, 0.28f));
                return;
            }

            Material pad = Shell(metal);
            foreach (float side in new[] { -1f, 1f })
            {
                CreatureLootSetup.AddPart(root, PrimitiveType.Sphere, side < 0f ? "PauldronL" : "PauldronR", pad,
                    new Vector3(0.19f * side, 0.16f, 0.015f), Vector3.zero, new Vector3(0.17f, 0.12f, 0.19f));
            }
        }

        /// <summary>A fur cloak on the chest bone: a broad mantle over the shoulders, a long back
        /// panel down to the knees, a shaggy collar.</summary>
        private static void BuildCloak(Transform root, Metal metal)
        {
            const float chestPivot = 1.264f;
            Material fur = Shell(metal);
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Mantle", fur, new Vector3(0f, 0.14f, -0.01f), Vector3.zero, new Vector3(0.5f, 0.05f, 0.36f));
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Collar", fur, new Vector3(0f, 0.2f, -0.02f), Vector3.zero, new Vector3(0.3f, 0.05f, 0.26f));
            // Back panel: from the shoulders (1.42 m) to the knees (~0.55 m), a little behind the back.
            float top = 1.4f - chestPivot;
            float bottom = 0.55f - chestPivot;
            CreatureLootSetup.AddPart(root, PrimitiveType.Cube, "Back", fur, new Vector3(0f, (top + bottom) * 0.5f, -0.2f), new Vector3(-4f, 0f, 0f), new Vector3(0.5f, top - bottom, 0.04f));
        }

        private static void BuildThigh(Transform root, Metal metal, float side)
        {
            Material material = metal == Metal.Iron || metal == Metal.Silver || metal == Metal.BlackSteel ? _mats.Mail : _mats.Leather;
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Thigh", material, new Vector3(0.022f * side, -0.19f, 0.01f), Vector3.zero, new Vector3(0.22f, 0.18f, 0.22f));
        }

        private static void BuildShin(Transform root, Metal metal, float side)
        {
            Material material = Shell(metal);
            CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Shin", material, new Vector3(-0.005f * side, -0.17f, -0.025f), Vector3.zero, new Vector3(0.175f, 0.15f, 0.18f));
            if (metal != Metal.Leather)
            {
                CreatureLootSetup.AddPart(root, PrimitiveType.Sphere, "Knee", material, new Vector3(0f, 0f, 0.035f), Vector3.zero, new Vector3(0.12f, 0.12f, 0.1f));
            }
        }

        // The set's main metal (leather for the leather set).
        private static Material Shell(Metal metal) => metal switch
        {
            Metal.Leather => _mats.Leather,
            Metal.Copper => _mats.Copper,
            Metal.Bronze => _mats.Bronze,
            Metal.Silver => _mats.Silver,
            Metal.Fur => _mats.Fur,
            Metal.BlackSteel => _mats.BlackSteel,
            Metal.BlackFur => _mats.BlackFur,
            _ => _mats.Iron,
        };

        private static void AddDome(Transform parent, string name, Material material, Vector3 position, Vector3 scale)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = _dome;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>Unit half-sphere (diameter 1, height 0.5, open at the bottom), faces out.</summary>
        private static Mesh BuildDomeMesh()
        {
            const int segments = 24;
            const int rings = 8;
            var vertices = new List<Vector3>();
            for (int r = 0; r <= rings; r++)
            {
                float phi = Mathf.PI * 0.5f * r / rings;
                for (int s = 0; s <= segments; s++)
                {
                    float theta = Mathf.PI * 2f * s / segments;
                    vertices.Add(new Vector3(Mathf.Cos(theta) * Mathf.Cos(phi), Mathf.Sin(phi), Mathf.Sin(theta) * Mathf.Cos(phi)) * 0.5f);
                }
            }

            var triangles = new List<int>();
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * (segments + 1) + s;
                    int b = a + 1;
                    int c = a + segments + 1;
                    int d = c + 1;
                    AddOutward(vertices, triangles, a, c, b);
                    AddOutward(vertices, triangles, b, c, d);
                }
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(DomeMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "ArmorDome" };
                AssetDatabase.CreateAsset(mesh, DomeMeshPath);
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // A front face's Cross(b-a, c-a) points at the viewer (Unity winds front faces clockwise),
        // so keep the order when that points away from the centre, flip it otherwise. Degenerate
        // triangles at the pole are dropped.
        private static void AddOutward(List<Vector3> vertices, List<int> triangles, int a, int b, int c)
        {
            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            Vector3 centroid = (vertices[a] + vertices[b] + vertices[c]) / 3f;
            if (normal.sqrMagnitude < 1e-12f)
            {
                return;
            }

            if (Vector3.Dot(normal, centroid) > 0f)
            {
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }
            else
            {
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
            }
        }

        private static void WireArmorView()
        {
            HeldItemView held = Object.FindAnyObjectByType<HeldItemView>(FindObjectsInactive.Include);
            if (held == null)
            {
                Debug.LogError("ArmorSetup: no HeldItemView (the character) in the open scene - ArmorView not added.");
                return;
            }

            ArmorView view = held.GetComponent<ArmorView>();
            if (view == null)
            {
                view = held.gameObject.AddComponent<ArmorView>();
            }

            var serialized = new SerializedObject(view);
            serialized.FindProperty("_equipment").objectReferenceValue = new SerializedObject(held).FindProperty("_equipment").objectReferenceValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        private static Material GetOrCreateMaterial(string name, Color color, float smoothness, float metallic)
        {
            string path = MaterialFolder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "Leather_material.mat")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static ItemDefinition Item(string id) => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{Items}{id}.asset");

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
            }
        }
    }
}
