using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using NAV.Core.Save;
using NAV.Dev;
using NAV.Gameplay.Combat;
using NAV.Gameplay.Items;

namespace NAV.Editor
{
    /// <summary>
    /// Turns every model of the LowPoly Weapons Pack (PolySoft3D) into a full game weapon -
    /// menu NAV > Items > Generate Weapons From LowPoly Pack. Re-runnable: it updates the assets
    /// it made before in place (same paths, so GUIDs and references survive).
    ///
    /// Each pack folder is one WeaponClass with one attack style (the class table below), and
    /// per model it writes: a WeaponDefinition (class stats), an ItemDefinition (category
    /// Weapon, axes are also tier-1 Axe tools), a held prefab (model scaled/turned/offset so
    /// its grip sits on the Kevin rig's B-handProp bone, plus a MeleeHitbox over the blade /
    /// head / tip / shield face), a world drop prefab (model lying flat - same components as
    /// StoneSword_item), its icon (ItemIconRenderer), its entry in DefaultItemDatabase (save
    /// lookup) and in the dev WeaponArsenal racks. "_001" models are the pack's recoloured
    /// variants ("b"). sword1H10 / axe1H9 are skipped - they are the Stone Sword / Stone Axe.
    ///
    /// The stats are placeholders per class (no materials/tiers yet) and there are no recipes:
    /// see PROJECT_STATE.md - resources and workbench recipes for these weapons are TODO.
    /// </summary>
    internal static class WeaponPackGenerator
    {
        private const string PackPrefabs = "Assets/LowPolyWeapons/Prefabs/";
        private const string ItemFolder = "Assets/NAV/ScriptableObjects/Items/LowPoly";
        private const string WeaponFolder = "Assets/NAV/ScriptableObjects/Weapons/LowPoly";
        private const string PrefabFolder = "Assets/NAV/Prefabs/Item/LowPoly";
        private const string WorldTemplatePath = "Assets/NAV/Prefabs/Item/StoneSword_item.prefab";
        private const string DatabasePath = "Assets/NAV/ScriptableObjects/Core/DefaultItemDatabase.asset";
        private const string ArsenalPath = "Assets/NAV/ScriptableObjects/Dev/WeaponArsenal.asset";
        private const int RackSize = 20;

        // Kevin's shield sits 8 cm in front of B-handProp.L (measured from his demo prefab).
        private const float ShieldPlateOffset = -0.081f;

        private static readonly HashSet<string> Skipped = new() { "sword1H10", "axe1H9" };

        private enum Grip
        {
            /// <summary>Pivot is at the guard; grip = that fraction of the way down the handle.</summary>
            BelowGuard,

            /// <summary>Grip = that fraction of the total length up from the bottom end.</summary>
            FromBottom,

            /// <summary>Shield: centred on the hand, plate just in front of it.</summary>
            Shield,
        }

        private enum Hitbox
        {
            Blade,
            Head,
            Tip,
            ShieldFace,
        }

        private sealed class ClassSpec
        {
            public WeaponClass Class;
            public string Folder;
            public string Name;
            public WeaponAttackStyle Style;
            public float Size;          // target length in metres (shields: width)
            public Grip Grip;
            public float GripParam;
            public float Yaw;           // model turn around the handle (axes: edge leads the chop)
            public Hitbox Hitbox;
            public float HitRadius;
            public float Damage, Range, Cooldown, Stamina, AnimSpeed, WindowStart, WindowEnd;
            public float Block, BlockStamina, Parry, Weight;
            public ToolType Tool;
            public bool LeftHand;
        }

        // Impact times of the clips at speed 1 (tip-speed peaks): 1H_R 0.30 s, 1H_L ~0.07 s,
        // 2H 0.56 s, polearm 0.39 s, shield bash 0.25 s. Windows below are those / AnimSpeed.
        private static readonly ClassSpec[] Classes =
        {
            new() { Class = WeaponClass.Dagger, Folder = "Daggers", Name = "Dagger", Style = WeaponAttackStyle.OneHanded,
                Size = 0.45f, Grip = Grip.BelowGuard, GripParam = 0.5f, Hitbox = Hitbox.Blade, HitRadius = 0.05f,
                Damage = 9f, Range = 1.7f, Cooldown = 0.45f, Stamina = 6f, AnimSpeed = 1.9f, WindowStart = 0.03f, WindowEnd = 0.24f,
                Block = 0.25f, BlockStamina = 12f, Parry = 0.3f, Weight = 1f },
            new() { Class = WeaponClass.Sword, Folder = "Swords1H", Name = "Sword", Style = WeaponAttackStyle.OneHanded,
                Size = 1.0f, Grip = Grip.BelowGuard, GripParam = 0.5f, Hitbox = Hitbox.Blade, HitRadius = 0.07f,
                Damage = 15f, Range = 2.4f, Cooldown = 0.6f, Stamina = 10f, AnimSpeed = 1.5f, WindowStart = 0.04f, WindowEnd = 0.32f,
                Block = 0.5f, BlockStamina = 10f, Parry = 0.25f, Weight = 3f },
            new() { Class = WeaponClass.Greatsword, Folder = "Swords2H", Name = "Greatsword", Style = WeaponAttackStyle.TwoHanded,
                Size = 1.65f, Grip = Grip.BelowGuard, GripParam = 0.35f, Hitbox = Hitbox.Blade, HitRadius = 0.09f,
                Damage = 25f, Range = 2.9f, Cooldown = 1.0f, Stamina = 18f, AnimSpeed = 1.45f, WindowStart = 0.26f, WindowEnd = 0.5f,
                Block = 0.6f, BlockStamina = 12f, Parry = 0.2f, Weight = 7f },
            new() { Class = WeaponClass.HandAxe, Folder = "Axes1H", Name = "Hand Axe", Style = WeaponAttackStyle.TwoHanded,
                Size = 0.75f, Grip = Grip.FromBottom, GripParam = 0.08f, Yaw = 180f, Hitbox = Hitbox.Head, HitRadius = 0.1f,
                Damage = 13f, Range = 2.3f, Cooldown = 0.8f, Stamina = 12f, AnimSpeed = 1.8f, WindowStart = 0.2f, WindowEnd = 0.42f,
                Block = 0.35f, BlockStamina = 12f, Parry = 0.2f, Weight = 3f, Tool = ToolType.Axe },
            new() { Class = WeaponClass.BattleAxe, Folder = "Axes2H", Name = "Battle Axe", Style = WeaponAttackStyle.TwoHanded,
                Size = 1.35f, Grip = Grip.FromBottom, GripParam = 0.12f, Yaw = 180f, Hitbox = Hitbox.Head, HitRadius = 0.15f,
                Damage = 27f, Range = 2.8f, Cooldown = 1.1f, Stamina = 20f, AnimSpeed = 1.35f, WindowStart = 0.3f, WindowEnd = 0.55f,
                Block = 0.5f, BlockStamina = 12f, Parry = 0.2f, Weight = 8f, Tool = ToolType.Axe },
            new() { Class = WeaponClass.Mace, Folder = "Hammers1H", Name = "Mace", Style = WeaponAttackStyle.OneHanded,
                Size = 0.75f, Grip = Grip.FromBottom, GripParam = 0.1f, Hitbox = Hitbox.Head, HitRadius = 0.12f,
                Damage = 17f, Range = 2.2f, Cooldown = 0.75f, Stamina = 12f, AnimSpeed = 1.3f, WindowStart = 0.05f, WindowEnd = 0.38f,
                Block = 0.4f, BlockStamina = 11f, Parry = 0.2f, Weight = 4f },
            new() { Class = WeaponClass.Warhammer, Folder = "Hammers2H", Name = "Warhammer", Style = WeaponAttackStyle.TwoHanded,
                Size = 1.4f, Grip = Grip.FromBottom, GripParam = 0.12f, Hitbox = Hitbox.Head, HitRadius = 0.16f,
                Damage = 30f, Range = 2.8f, Cooldown = 1.2f, Stamina = 22f, AnimSpeed = 1.3f, WindowStart = 0.32f, WindowEnd = 0.58f,
                Block = 0.5f, BlockStamina = 12f, Parry = 0.2f, Weight = 9f },
            new() { Class = WeaponClass.Spear, Folder = "Spears", Name = "Spear", Style = WeaponAttackStyle.Polearm,
                Size = 2.3f, Grip = Grip.FromBottom, GripParam = 0.14f, Hitbox = Hitbox.Tip, HitRadius = 0.07f,
                Damage = 18f, Range = 3.2f, Cooldown = 0.85f, Stamina = 12f, AnimSpeed = 1.5f, WindowStart = 0.14f, WindowEnd = 0.38f,
                Block = 0.35f, BlockStamina = 12f, Parry = 0.2f, Weight = 4f },
            new() { Class = WeaponClass.Shield, Folder = "Shields", Name = "Shield", Style = WeaponAttackStyle.Shield,
                Size = 0.62f, Grip = Grip.Shield, Hitbox = Hitbox.ShieldFace,
                Damage = 6f, Range = 1.6f, Cooldown = 0.8f, Stamina = 10f, AnimSpeed = 1.2f, WindowStart = 0.08f, WindowEnd = 0.3f,
                Block = 0.85f, BlockStamina = 6f, Parry = 0.35f, Weight = 6f, LeftHand = true },
        };

        [MenuItem("NAV/Items/Generate Weapons From LowPoly Pack")]
        private static void Generate()
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(WorldTemplatePath);
            var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
            if (template == null || database == null)
            {
                Debug.LogError($"{nameof(WeaponPackGenerator)}: missing {WorldTemplatePath} or {DatabasePath}.");
                return;
            }

            var allItems = new List<ItemDefinition>();
            var racks = new List<WeaponArsenal.Rack>();

            foreach (ClassSpec spec in Classes)
            {
                List<(string path, GameObject prefab, Bounds bounds)> models = LoadModels(spec.Folder);
                if (models.Count == 0)
                {
                    Debug.LogWarning($"{nameof(WeaponPackGenerator)}: no models in {spec.Folder}.");
                    continue;
                }

                // One scale per class (from its average size) keeps the variants' relative sizes.
                float average = spec.Grip == Grip.Shield
                    ? models.Average(m => m.bounds.size.x)
                    : models.Average(m => m.bounds.size.y);
                float scale = spec.Size / average;

                var classItems = new List<ItemDefinition>();
                foreach (var (_, prefab, bounds) in models)
                {
                    classItems.Add(BuildWeapon(spec, prefab, bounds, scale, template));
                }

                allItems.AddRange(classItems);
                for (int start = 0; start < classItems.Count; start += RackSize)
                {
                    int part = start / RackSize + 1;
                    int parts = (classItems.Count + RackSize - 1) / RackSize;
                    racks.Add(new WeaponArsenal.Rack
                    {
                        Title = parts > 1 ? $"{spec.Name}s ({part}/{parts})" : $"{spec.Name}s",
                        Items = classItems.Skip(start).Take(RackSize).ToList(),
                    });
                }
            }

            AddToDatabase(database, allItems);
            SaveArsenal(racks);
            AssetDatabase.SaveAssets();
            ItemIconRenderer.RenderIcons(allItems);
            AssetDatabase.SaveAssets();
            Debug.Log($"{nameof(WeaponPackGenerator)}: generated {allItems.Count} weapons in {Classes.Length} classes, {racks.Count} arsenal racks.");
        }

        private static List<(string, GameObject, Bounds)> LoadModels(string folder)
        {
            var models = new List<(string, GameObject, Bounds)>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PackPrefabs + folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (Skipped.Contains(name))
                {
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                models.Add((path, prefab, GetLocalBounds(prefab)));
            }

            // Natural order: 1, 1b, 2, 2b ... 10.
            return models.OrderBy(m => VariantNumber(Path.GetFileNameWithoutExtension(m.Item1)))
                .ThenBy(m => m.Item1.Contains("_001") ? 1 : 0)
                .ToList();
        }

        private static ItemDefinition BuildWeapon(ClassSpec spec, GameObject model, Bounds bounds, float scale, GameObject worldTemplate)
        {
            string modelName = model.name;
            bool variant = modelName.EndsWith("_001");
            int number = VariantNumber(modelName);
            string id = $"LPW_{modelName}";
            string displayName = $"{spec.Name} {number}{(variant ? "b" : string.Empty)}";
            string classFolder = spec.Class.ToString();

            WeaponDefinition weapon = LoadOrCreate<WeaponDefinition>($"{WeaponFolder}/{classFolder}/{id}_Weapon.asset");
            var w = new SerializedObject(weapon);
            w.FindProperty("_displayName").stringValue = displayName;
            w.FindProperty("_weaponClass").intValue = (int)spec.Class;
            w.FindProperty("_damage").floatValue = spec.Damage;
            w.FindProperty("_range").floatValue = spec.Range;
            w.FindProperty("_attackCooldown").floatValue = spec.Cooldown;
            w.FindProperty("_attackStaminaCost").floatValue = spec.Stamina;
            w.FindProperty("_attackStyle").intValue = (int)spec.Style;
            w.FindProperty("_attackAnimationSpeed").floatValue = spec.AnimSpeed;
            w.FindProperty("_hitWindowStart").floatValue = spec.WindowStart;
            w.FindProperty("_hitWindowEnd").floatValue = spec.WindowEnd;
            w.FindProperty("_blockDamageReduction").floatValue = spec.Block;
            w.FindProperty("_blockStaminaCostPerHit").floatValue = spec.BlockStamina;
            w.FindProperty("_parryWindowSeconds").floatValue = spec.Parry;
            w.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weapon);

            GameObject held = BuildHeldPrefab(spec, model, bounds, scale, $"{PrefabFolder}/{classFolder}/{id}_held.prefab");

            ItemDefinition item = LoadOrCreate<ItemDefinition>($"{ItemFolder}/{classFolder}/{id}.asset");
            GameObject world = BuildWorldPrefab(model, scale, item, worldTemplate, $"{PrefabFolder}/{classFolder}/{id}_item.prefab");

            var i = new SerializedObject(item);
            i.FindProperty("_id").stringValue = id;
            i.FindProperty("_displayName").stringValue = displayName;
            i.FindProperty("_description").stringValue = $"{spec.Name} from the LowPoly Weapons Pack. Placeholder stats - no materials, tier or recipe yet.";
            i.FindProperty("_category").intValue = (int)ItemCategory.Weapon;
            i.FindProperty("_maxStackSize").intValue = 1;
            i.FindProperty("_weight").floatValue = spec.Weight;
            i.FindProperty("_worldPrefab").objectReferenceValue = world;
            i.FindProperty("_toolType").intValue = (int)spec.Tool;
            i.FindProperty("_toolTier").intValue = 1;
            i.FindProperty("_weapon").objectReferenceValue = weapon;
            i.FindProperty("_heldPrefab").objectReferenceValue = held;
            i.FindProperty("_heldInLeftHand").boolValue = spec.LeftHand;
            i.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return item;
        }

        /// <summary>Root (MeleeHitbox) + the pack model as a child, placed so the grip is at the
        /// root's origin with the handle along +Y (B-handProp convention).</summary>
        private static GameObject BuildHeldPrefab(ClassSpec spec, GameObject model, Bounds b, float scale, string path)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            var child = (GameObject)PrefabUtility.InstantiatePrefab(model);
            child.name = "Model";
            child.transform.SetParent(root.transform, false);
            child.transform.localScale = Vector3.one * scale;
            float yaw = spec.Grip == Grip.Shield ? 180f : spec.Yaw;
            child.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            StripColliders(child);

            float length = b.size.y;
            Vector3 anchor;
            Vector3 target = Vector3.zero;
            switch (spec.Grip)
            {
                case Grip.BelowGuard:
                    anchor = new Vector3(0f, b.min.y * spec.GripParam, 0f);
                    break;
                case Grip.FromBottom:
                    anchor = new Vector3(0f, b.min.y + length * spec.GripParam, 0f);
                    break;
                default:
                    // Shield: the back of the plate (min Z before the 180 turn) faces the hand.
                    anchor = new Vector3(b.center.x, b.center.y, b.min.z);
                    target = new Vector3(0f, 0f, ShieldPlateOffset);
                    break;
            }

            child.transform.localPosition = Vector3.zero;
            child.transform.localPosition = target - child.transform.localRotation * (anchor * scale);

            Vector3 start, end;
            float radius = spec.HitRadius;
            switch (spec.Hitbox)
            {
                case Hitbox.Blade:
                    start = new Vector3(0f, b.max.y * 0.15f, 0f);
                    end = new Vector3(0f, b.max.y * 0.95f, 0f);
                    break;
                case Hitbox.Head:
                    start = new Vector3(0f, b.min.y + length * 0.72f, 0f);
                    end = new Vector3(0f, b.max.y - length * 0.04f, 0f);
                    float headHalfWidth = Mathf.Max(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x)) * scale;
                    radius = Mathf.Clamp(headHalfWidth * 0.6f, spec.HitRadius, 0.2f);
                    break;
                case Hitbox.Tip:
                    start = new Vector3(0f, b.min.y + length * 0.8f, 0f);
                    end = new Vector3(0f, b.max.y, 0f);
                    break;
                default:
                    start = end = new Vector3(b.center.x, b.center.y, b.max.z);
                    radius = Mathf.Clamp(b.size.x * scale * 0.4f, 0.15f, 0.35f);
                    break;
            }

            var hitbox = root.AddComponent<MeleeHitbox>();
            var h = new SerializedObject(hitbox);
            h.FindProperty("_start").vector3Value = root.transform.InverseTransformPoint(child.transform.TransformPoint(start));
            h.FindProperty("_end").vector3Value = root.transform.InverseTransformPoint(child.transform.TransformPoint(end));
            h.FindProperty("_radius").floatValue = radius;
            h.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(Path.GetDirectoryName(path));
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>Copy of StoneSword_item (collider, rigidbody, ItemPickup) with this model
        /// lying flat, centred, collider fitted.</summary>
        private static GameObject BuildWorldPrefab(GameObject model, float scale, ItemDefinition item, GameObject template, string path)
        {
            GameObject root = Object.Instantiate(template);
            root.name = Path.GetFileNameWithoutExtension(path);
            for (int c = root.transform.childCount - 1; c >= 0; c--)
            {
                Object.DestroyImmediate(root.transform.GetChild(c).gameObject);
            }

            var child = (GameObject)PrefabUtility.InstantiatePrefab(model);
            child.name = "Model";
            child.transform.SetParent(root.transform, false);
            child.transform.localScale = Vector3.one * scale;
            child.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.right);
            StripColliders(child);

            Bounds local = default;
            bool first = true;
            foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>())
            {
                Bounds rb = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = rb.center + Vector3.Scale(rb.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    p = root.transform.InverseTransformPoint(p);
                    if (first)
                    {
                        local = new Bounds(p, Vector3.zero);
                        first = false;
                    }
                    else
                    {
                        local.Encapsulate(p);
                    }
                }
            }

            child.transform.localPosition = -local.center;
            var box = root.GetComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = new Vector3(local.size.x, Mathf.Max(local.size.y, 0.05f), local.size.z);

            var pickup = root.GetComponent<ItemPickup>();
            var p2 = new SerializedObject(pickup);
            p2.FindProperty("_definition").objectReferenceValue = item;
            p2.FindProperty("_quantity").intValue = 1;
            p2.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(Path.GetDirectoryName(path));
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static void AddToDatabase(ItemDatabase database, List<ItemDefinition> items)
        {
            var serialized = new SerializedObject(database);
            SerializedProperty list = serialized.FindProperty("_items");
            var existing = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                existing.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);
            }

            foreach (ItemDefinition item in items)
            {
                if (existing.Contains(item))
                {
                    continue;
                }

                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }

        private static void SaveArsenal(List<WeaponArsenal.Rack> racks)
        {
            WeaponArsenal arsenal = LoadOrCreate<WeaponArsenal>(ArsenalPath);
            arsenal.SetRacks(racks);
            EditorUtility.SetDirty(arsenal);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            EnsureFolder(Path.GetDirectoryName(path));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static void StripColliders(GameObject go)
        {
            foreach (Collider collider in go.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static Bounds GetLocalBounds(GameObject prefab)
        {
            Bounds result = default;
            bool first = true;
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                Bounds mb = filter.sharedMesh.bounds;
                Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = toRoot.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    if (first)
                    {
                        result = new Bounds(p, Vector3.zero);
                        first = false;
                    }
                    else
                    {
                        result.Encapsulate(p);
                    }
                }
            }

            return result;
        }

        // "sword1H3_001" -> 3, "dagger10" -> 10, "spear4" -> 4.
        private static int VariantNumber(string modelName)
        {
            string baseName = modelName.EndsWith("_001") ? modelName.Substring(0, modelName.Length - 4) : modelName;
            Match match = Regex.Match(baseName, @"(\d+)$");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }
    }
}
