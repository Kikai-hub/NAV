using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.AI;
using NAV.Gameplay.Items;

namespace NAV.Editor
{
    /// <summary>
    /// Builds the creature loot resources - Hide, Bone, Fang (Stage B; the pack weapon recipes
    /// of Stage C need them): placeholder pickup models made from Unity primitives plus a
    /// procedural curved fang mesh (no such models in the project yet), their materials,
    /// ItemDefinitions, icons, DefaultItemDatabase entries, and the Forest Wolf / Deer loot
    /// tables. Re-run to rebuild in place (existing assets keep their GUIDs).
    /// </summary>
    internal static class CreatureLootSetup
    {
        private const string ItemFolder = "Assets/NAV/ScriptableObjects/Items";
        private const string PrefabFolder = "Assets/NAV/Prefabs/Item";
        private const string IconFolder = "Assets/NAV/Art/Icons";
        private const string MaterialFolder = "Assets/NAV/Materials/Items";
        private const string FangMeshPath = "Assets/NAV/Art/Models/FangMesh.asset";
        private const string DatabasePath = "Assets/NAV/ScriptableObjects/Core/DefaultItemDatabase.asset";
        private const string WolfPath = "Assets/NAV/ScriptableObjects/Creatures/ForestWolfCreature.asset";
        private const string DeerPath = "Assets/NAV/ScriptableObjects/Creatures/DeerCreature.asset";

        [MenuItem("NAV/Items/Build Creature Loot (Hide, Bone, Fang)")]
        private static void Build()
        {
            EnsureFolder(MaterialFolder);

            Material fur = GetOrCreateMaterial("Hide_material", new Color(0.33f, 0.26f, 0.2f), 0.05f);
            Material leather = GetOrCreateMaterial("HideStrap_material", new Color(0.55f, 0.4f, 0.24f), 0.3f);
            Material bone = GetOrCreateMaterial("Bone_material", new Color(0.88f, 0.84f, 0.72f), 0.3f);

            ItemDefinition hide = BuildItem("Hide", "Hide", "A rolled animal hide. Tanned into leather for armour and grips.",
                40, 0.5f, root => BuildHideModel(root, fur, leather), Quaternion.Euler(-25f, 0f, 0f) * Quaternion.Euler(0f, 30f, 0f));
            ItemDefinition boneItem = BuildItem("Bone", "Bone", "A sturdy animal bone. Carved into tools, needles and weapon parts.",
                40, 0.3f, root => BuildBoneModel(root, bone), Quaternion.Euler(0f, 0f, 35f));
            ItemDefinition fang = BuildItem("Fang", "Fang", "A sharp predator's fang. Prized for spear points and charms.",
                50, 0.1f, root => BuildFangModel(root, bone), Quaternion.Euler(0f, 0f, -20f));

            var database = new SerializedObject(AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath));
            AddToList(database, "_items", hide);
            AddToList(database, "_items", boneItem);
            AddToList(database, "_items", fang);

            // Wolf: the predator - always a hide, often fangs. Deer: hide and bone, no fangs.
            SetLoot(WolfPath, (hide, 1, 1f), (boneItem, 1, 0.6f), (fang, 2, 0.5f));
            SetLoot(DeerPath, (hide, 1, 1f), (boneItem, 1, 0.75f));

            AssetDatabase.SaveAssets();
            Debug.Log("CreatureLootSetup: built Hide, Bone, Fang items, pickups and icons; wolf and deer loot updated.");
        }

        private static ItemDefinition BuildItem(string id, string displayName, string description, int maxStack, float weight,
            System.Action<Transform> buildModel, Quaternion iconPose)
        {
            string itemPath = $"{ItemFolder}/{id}.asset";
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(item, itemPath);
            }

            GameObject prefab = BuildPickup(id, item, buildModel);

            string iconPath = $"{IconFolder}/Icon_{id}.png";
            File.WriteAllBytes(iconPath, ItemIconRenderer.RenderIcon(prefab, iconPose));
            AssetDatabase.ImportAsset(iconPath);
            ItemIconRenderer.ConfigureSprite(iconPath);

            var serialized = new SerializedObject(item);
            serialized.FindProperty("_id").stringValue = id;
            serialized.FindProperty("_displayName").stringValue = displayName;
            serialized.FindProperty("_description").stringValue = description;
            serialized.FindProperty("_icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            serialized.FindProperty("_category").enumValueIndex = (int)ItemCategory.Resource;
            serialized.FindProperty("_maxStackSize").intValue = maxStack;
            serialized.FindProperty("_weight").floatValue = weight;
            serialized.FindProperty("_worldPrefab").objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return item;
        }

        /// <summary>Same layout as the other pickups (RawMeat_item): Rigidbody + one BoxCollider
        /// + ItemPickup on the root; the visual parts are collider-less children.</summary>
        private static GameObject BuildPickup(string id, ItemDefinition item, System.Action<Transform> buildModel)
        {
            var root = new GameObject($"{id}_item");
            buildModel(root.transform);

            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            var collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;

            var body = root.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.angularDamping = 2f;
            // Small and thrown on drop - discrete checks let them tunnel through the terrain.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var pickup = new SerializedObject(root.AddComponent<ItemPickup>());
            pickup.FindProperty("_definition").objectReferenceValue = item;
            pickup.FindProperty("_quantity").intValue = 1;
            pickup.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{id}_item.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---- Placeholder models (sizes in metres) ----

        private static void BuildHideModel(Transform root, Material fur, Material leather)
        {
            // A rolled hide lying along X, tied with two straps, the loose edge flapping over.
            AddPart(root, PrimitiveType.Cylinder, "Roll", fur, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.28f, 0.24f, 0.28f));
            foreach (float x in new[] { -0.17f, 0.17f })
            {
                AddPart(root, PrimitiveType.Cylinder, "Strap", leather, new Vector3(x, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.295f, 0.025f, 0.295f));
            }

            AddPart(root, PrimitiveType.Cube, "Flap", fur, new Vector3(0f, 0.1f, 0.15f), new Vector3(-50f, 0f, 0f), new Vector3(0.42f, 0.03f, 0.2f));
        }

        private static void BuildBoneModel(Transform root, Material bone)
        {
            // Shaft along X with two knuckles at each end.
            AddPart(root, PrimitiveType.Cylinder, "Shaft", bone, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.065f, 0.17f, 0.065f));
            foreach (float x in new[] { -0.17f, 0.17f })
            {
                foreach (float y in new[] { -0.035f, 0.035f })
                {
                    AddPart(root, PrimitiveType.Sphere, "Knuckle", bone, new Vector3(x, y, 0f), Vector3.zero, Vector3.one * 0.095f);
                }
            }
        }

        private static void BuildFangModel(Transform root, Material bone)
        {
            var fang = new GameObject("Fang");
            fang.transform.SetParent(root, false);
            fang.AddComponent<MeshFilter>().sharedMesh = GetOrCreateFangMesh();
            fang.AddComponent<MeshRenderer>().sharedMaterial = bone;
        }

        /// <summary>A tapered, curved cone: base ring at y = 0, tip bent towards +X.</summary>
        private static Mesh GetOrCreateFangMesh()
        {
            const int Rings = 8;
            const int Sides = 10;
            const float Length = 0.16f;
            const float BaseRadius = 0.04f;
            const float Bend = 0.05f;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            for (int r = 0; r <= Rings; r++)
            {
                float t = (float)r / Rings;
                float radius = BaseRadius * (1f - t) + 0.002f;
                Vector3 centre = new Vector3(Bend * t * t, Length * t, 0f);
                for (int s = 0; s < Sides; s++)
                {
                    float angle = s * Mathf.PI * 2f / Sides;
                    vertices.Add(centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
                }
            }

            for (int r = 0; r < Rings; r++)
            {
                for (int s = 0; s < Sides; s++)
                {
                    int a = r * Sides + s;
                    int b = r * Sides + (s + 1) % Sides;
                    int c = a + Sides;
                    int d = b + Sides;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            }

            // Flat base cap.
            int capCentre = vertices.Count;
            vertices.Add(Vector3.zero);
            for (int s = 0; s < Sides; s++)
            {
                triangles.AddRange(new[] { capCentre, s, (s + 1) % Sides });
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(FangMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "FangMesh" };
                AssetDatabase.CreateAsset(mesh, FangMeshPath);
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void AddPart(Transform parent, PrimitiveType type, string name, Material material, Vector3 position, Vector3 euler, Vector3 scale)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localEulerAngles = euler;
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material GetOrCreateMaterial(string name, Color color, float smoothness)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/NAV/Materials/RawMeat_material.mat")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", null);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Keeps the definition's other drops (meat) and adds/updates these.</summary>
        private static void SetLoot(string definitionPath, params (ItemDefinition item, int amount, float chance)[] drops)
        {
            var definition = AssetDatabase.LoadAssetAtPath<CreatureDefinition>(definitionPath);
            var serialized = new SerializedObject(definition);
            SerializedProperty list = serialized.FindProperty("_lootDrops");

            foreach ((ItemDefinition item, int amount, float chance) in drops)
            {
                SerializedProperty entry = null;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("_item").objectReferenceValue == item)
                    {
                        entry = list.GetArrayElementAtIndex(i);
                    }
                }

                if (entry == null)
                {
                    list.arraySize++;
                    entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                }

                entry.FindPropertyRelative("_item").objectReferenceValue = item;
                entry.FindPropertyRelative("_amount").intValue = amount;
                entry.FindPropertyRelative("_chance").floatValue = chance;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void AddToList(SerializedObject owner, string listName, Object item)
        {
            SerializedProperty list = owner.FindProperty(listName);
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == item)
                {
                    return;
                }
            }

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
            owner.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner.targetObject);
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
            }
        }
    }
}
