using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.Building;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;
using NAV.Gameplay.Vehicles;

namespace NAV.Editor
{
    /// <summary>
    /// NAV > Build > Build Ladya (Boat) - the tier 3 boat as a building piece: built at the
    /// joiner's bench (workbench level 3) from oak, wood, resin and leather, placed on water with
    /// the hammer (Floats On Water). A placeholder ladya from primitives (keel, planked sides, a
    /// horse-head prow, mast with a striped sail, oars, a hold at the stern) - 6 x 1.8 m; the
    /// pivot is the waterline. Rigidbody + Boat (6 float points, seat amidships) + a
    /// StorageContainer hold (12 slots) on its own child, so aiming at it opens the hold and
    /// aiming at the hull boards. Registered in the piece database and the player's pieces
    /// (scene saved). Safe to re-run.
    /// </summary>
    internal static class BoatSetup
    {
        private const string PrefabPath = "Assets/NAV/Prefabs/Build/Ladya.prefab";
        private const string DefinitionPath = "Assets/NAV/ScriptableObjects/Building/LadyaPiece.asset";
        private const string Items = "Assets/NAV/ScriptableObjects/Items/";

        [MenuItem("NAV/Build/Build Ladya (Boat)")]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("BoatSetup: exit Play Mode first.");
                return;
            }

            ItemDefinition oak = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + "OakWood.asset");
            ItemDefinition wood = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + "Wood.asset");
            ItemDefinition resin = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + "Resin.asset");
            ItemDefinition leather = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + "Leather.asset");
            var workbench = AssetDatabase.LoadAssetAtPath<WorkbenchDefinition>("Assets/NAV/ScriptableObjects/Workbenches/BasicWorkbench.asset");
            if (oak == null || wood == null || resin == null || leather == null || workbench == null)
            {
                Debug.LogError("BoatSetup: needs Oak Wood, Wood, Resin, Leather and the workbench (run the oak and leather setups first).");
                return;
            }

            var definition = AssetDatabase.LoadAssetAtPath<BuildingPieceDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BuildingPieceDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            GameObject prefab = BuildPrefab(definition);

            const string iconPath = "Assets/NAV/Art/Icons/Icon_Ladya.png";
            System.IO.File.WriteAllBytes(iconPath, ItemIconRenderer.RenderIcon(prefab, Quaternion.Euler(-20f, 0f, 0f) * Quaternion.Euler(0f, 135f, 0f)));
            AssetDatabase.ImportAsset(iconPath);
            ItemIconRenderer.ConfigureSprite(iconPath);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = "LadyaPiece";
            serialized.FindProperty("_displayName").stringValue = "Ladya";
            serialized.FindProperty("_prefab").objectReferenceValue = prefab;
            serialized.FindProperty("_icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            serialized.FindProperty("_requiresWorkbench").boolValue = true;
            serialized.FindProperty("_requiredStation").objectReferenceValue = workbench;
            serialized.FindProperty("_requiredStationLevel").intValue = 3;
            serialized.FindProperty("_floatsOnWater").boolValue = true;
            SerializedProperty cost = serialized.FindProperty("_cost");
            (ItemDefinition item, int amount)[] parts = { (oak, 10), (wood, 10), (resin, 6), (leather, 4) };
            cost.arraySize = parts.Length;
            for (int i = 0; i < parts.Length; i++)
            {
                cost.GetArrayElementAtIndex(i).FindPropertyRelative("_item").objectReferenceValue = parts[i].item;
                cost.GetArrayElementAtIndex(i).FindPropertyRelative("_amount").intValue = parts[i].amount;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            CreatureLootSetup.AddToList(new SerializedObject(AssetDatabase.LoadAssetAtPath<BuildingPieceDatabase>("Assets/NAV/ScriptableObjects/Core/BuildingPieceDatabase.asset")), "_pieces", definition);
            PlayerBuilding building = Object.FindAnyObjectByType<PlayerBuilding>(FindObjectsInactive.Include);
            if (building != null)
            {
                CreatureLootSetup.AddToList(new SerializedObject(building), "_knownPieces", definition);
                EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
                EditorSceneManager.SaveScene(building.gameObject.scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("BoatSetup: Ladya built (joiner's bench, placed on water).");
        }

        private static GameObject BuildPrefab(BuildingPieceDefinition definition)
        {
            Material hull = Mat("LadyaHull_material", new Color(0.5f, 0.36f, 0.22f));
            Material plank = Mat("LadyaPlank_material", new Color(0.62f, 0.46f, 0.28f));
            Material sailRed = Mat("LadyaSailRed_material", new Color(0.72f, 0.18f, 0.14f));
            Material sailWhite = Mat("LadyaSailWhite_material", new Color(0.9f, 0.86f, 0.76f));
            Material carving = AssetDatabase.LoadAssetAtPath<Material>("Assets/NAV/Materials/Building/Carving_material.mat") ?? plank;

            var root = new GameObject("Ladya");
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            // Hull: a flat bottom, sides flaring out, bow and stern rising.
            Part(visual, PrimitiveType.Cube, "Bottom", hull, new Vector3(0f, -0.35f, 0f), Vector3.zero, new Vector3(1.1f, 0.12f, 5.2f));
            foreach (float side in new[] { -1f, 1f })
            {
                Part(visual, PrimitiveType.Cube, "SideLow", hull, new Vector3(side * 0.62f, -0.18f, 0f), new Vector3(0f, 0f, side * -28f), new Vector3(0.08f, 0.42f, 5.4f));
                Part(visual, PrimitiveType.Cube, "SideHigh", plank, new Vector3(side * 0.82f, 0.12f, 0f), new Vector3(0f, 0f, side * -12f), new Vector3(0.07f, 0.34f, 5.6f));
                Part(visual, PrimitiveType.Cube, "Gunwale", hull, new Vector3(side * 0.86f, 0.3f, 0f), Vector3.zero, new Vector3(0.12f, 0.06f, 5.5f));
            }

            foreach (float end in new[] { -1f, 1f })
            {
                Part(visual, PrimitiveType.Cube, "EndPlank", hull, new Vector3(0f, -0.02f, end * 2.75f), new Vector3(end * -35f, 0f, 0f), new Vector3(1.2f, 0.7f, 0.08f));
                Part(visual, PrimitiveType.Cylinder, "Stem", hull, new Vector3(0f, 0.35f, end * 2.95f), new Vector3(end * 30f, 0f, 0f), new Vector3(0.14f, 0.55f, 0.14f));
            }

            // Horse-head prow, carved.
            Part(visual, PrimitiveType.Cube, "ProwNeck", carving, new Vector3(0f, 0.95f, 3.2f), new Vector3(20f, 0f, 0f), new Vector3(0.14f, 0.5f, 0.16f));
            Part(visual, PrimitiveType.Cube, "ProwHead", carving, new Vector3(0f, 1.18f, 3.36f), new Vector3(60f, 0f, 0f), new Vector3(0.13f, 0.34f, 0.16f));

            // Benches, mast, striped sail, oars.
            foreach (float z in new[] { -1.4f, 0.4f, 1.7f })
            {
                Part(visual, PrimitiveType.Cube, "Bench", plank, new Vector3(0f, 0.02f, z), Vector3.zero, new Vector3(1.55f, 0.06f, 0.26f));
            }

            Part(visual, PrimitiveType.Cylinder, "Mast", hull, new Vector3(0f, 2.0f, 0.9f), Vector3.zero, new Vector3(0.12f, 2.3f, 0.12f));
            Part(visual, PrimitiveType.Cylinder, "Yard", hull, new Vector3(0f, 3.7f, 0.95f), new Vector3(0f, 0f, 90f), new Vector3(0.07f, 1.35f, 0.07f));
            for (int i = 0; i < 5; i++)
            {
                Part(visual, PrimitiveType.Cube, "Sail", i % 2 == 0 ? sailRed : sailWhite, new Vector3(-1.1f + 0.55f * i, 2.45f, 1.0f), Vector3.zero, new Vector3(0.55f, 2.4f, 0.03f));
            }

            foreach (float side in new[] { -1f, 1f })
            {
                Part(visual, PrimitiveType.Cylinder, "Oar", plank, new Vector3(side * 1.3f, 0.05f, -0.2f), new Vector3(0f, side * 10f, side * 70f), new Vector3(0.06f, 1.1f, 0.06f));
                Part(visual, PrimitiveType.Cube, "OarBlade", plank, new Vector3(side * 2.25f, -0.28f, -0.2f), new Vector3(0f, side * 10f, side * 70f), new Vector3(0.05f, 0.45f, 0.18f));
            }

            // Hull collider on the root (BuildingPiece needs one; aiming at it boards).
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, -0.05f, 0f);
            collider.size = new Vector3(1.75f, 0.8f, 6f);

            // The hold at the stern: its own collider and storage.
            var hold = new GameObject("Hold");
            hold.transform.SetParent(root.transform, false);
            hold.transform.localPosition = new Vector3(0f, 0.12f, -2.15f);
            Part(hold.transform, PrimitiveType.Cube, "Chest", plank, Vector3.zero, Vector3.zero, new Vector3(0.8f, 0.4f, 0.5f));
            Part(hold.transform, PrimitiveType.Cube, "Lid", hull, new Vector3(0f, 0.22f, 0f), Vector3.zero, new Vector3(0.84f, 0.06f, 0.54f));
            var holdCollider = hold.AddComponent<BoxCollider>();
            holdCollider.size = new Vector3(0.84f, 0.5f, 0.54f);
            holdCollider.center = new Vector3(0f, 0.04f, 0f);
            var storage = new SerializedObject(hold.AddComponent<StorageContainer>());
            storage.FindProperty("_title").stringValue = "Ship's Hold";
            storage.FindProperty("_capacity").intValue = 12;
            storage.ApplyModifiedPropertiesWithoutUndo();

            var seat = new GameObject("Seat").transform;
            seat.SetParent(root.transform, false);
            seat.localPosition = new Vector3(0f, -0.29f, -0.6f);

            var points = new Transform[6];
            int n = 0;
            foreach (float z in new[] { -2.3f, 0f, 2.3f })
            {
                foreach (float x in new[] { -0.7f, 0.7f })
                {
                    var point = new GameObject($"Float{n}").transform;
                    point.SetParent(root.transform, false);
                    point.localPosition = new Vector3(x, -0.3f, z);
                    points[n++] = point;
                }
            }

            var body = root.AddComponent<Rigidbody>();
            body.mass = 400f;
            body.linearDamping = 0.15f;
            body.angularDamping = 1.2f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var piece = new SerializedObject(root.AddComponent<BuildingPiece>());
            piece.FindProperty("_definition").objectReferenceValue = definition;
            piece.ApplyModifiedPropertiesWithoutUndo();

            var boat = new SerializedObject(root.AddComponent<Boat>());
            boat.FindProperty("_seat").objectReferenceValue = seat;
            SerializedProperty floats = boat.FindProperty("_floatPoints");
            floats.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++)
            {
                floats.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            }

            boat.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void Part(Transform parent, PrimitiveType type, string name, Material material, Vector3 position, Vector3 euler, Vector3 scale)
        {
            RusBuildingStyleSetup.Part(parent, type, name, material, position, euler, scale);
        }

        private static Material Mat(string name, Color color)
        {
            string path = $"Assets/NAV/Materials/Building/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
