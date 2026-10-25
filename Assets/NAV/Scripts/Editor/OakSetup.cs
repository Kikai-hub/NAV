using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.Building;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Items;

namespace NAV.Editor
{
    /// <summary>
    /// Tier 2 oak (PROGRESSION_TIERS_v0.1.md - "extended building"):
    ///   - Old Oak (OldOak_WoodNode): Nature Starter Kit tree03 (broad trunk, round crown) with
    ///     darker bark and leaves, spawned big (scale 1.6-1.95) in the forest - temporarily, until
    ///     the dense forest biome exists. Needs a copper axe (tool tier 2). Its own felling chain
    ///     like the spruce's (TreeFellingSetup): standing -> OakFallen trunk -> two OakLogs, which
    ///     drop Oak Wood (felled straight as wood - no separate beam step);
    ///   - Joiner's Bench: workbench upgrade (WorkbenchExtension) - with the tanning rack it
    ///     makes the workbench level 3;
    ///   - the oak building set, all needing the workbench at level 3 nearby
    ///     (BuildingPieceDefinition.RequiredStation): log wall, window wall with a carved frame
    ///     (наличник), post, beam, upper floor, stairs, a double gate that opens (BuildingDoor)
    ///     and a carved roof ridge (конёк).
    /// Looks are PLACEHOLDERS from primitives. Re-run to rebuild in place; saves the scene.
    /// </summary>
    internal static class OakSetup
    {
        private const string Items = "Assets/NAV/ScriptableObjects/Items/";
        private const string PieceFolder = "Assets/NAV/ScriptableObjects/Building/";
        private const string BuildPrefabs = "Assets/NAV/Prefabs/Build/";
        private const string VegetationFolder = "Assets/NAV/Prefabs/World/Vegetation/";
        private const string MaterialFolder = "Assets/NAV/Materials/Items/";
        private const string IconFolder = "Assets/NAV/Art/Icons/";
        private const string NskTreePath = "Assets/NAV/Prefabs/World/Vegetation/NSK_tree03_WoodNode.prefab";
        private const int RequiredWorkbenchLevel = 3;

        // tree03 at scale 1: 8.6 m tall, trunk ~0.45 m radius with a flared base. Everything
        // below is at scale 1 - ResourceNode.InheritScale carries the spawn scale to the stump,
        // trunk and logs.
        private const float TreeHeight = 8.6f;
        private const float StumpScale = 1.3f;
        private const float StumpSink = 0.3f;
        private const float CutHeight = 0.8f;
        private const float TrunkLength = 5.6f;
        private const float TrunkRadius = 0.42f;
        private const float LogLength = 2.6f;
        private const float LogRadius = 0.4f;
        private static readonly float[] LogCentres = { 1.4f, 4.2f };

        private static Material _oak;
        private static Material _carving;
        private static Material _iron;

        [MenuItem("NAV/Build/Build Oak and Joinery (Stage C)")]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("OakSetup: exit Play Mode first.");
                return;
            }

            ItemDefinition wood = Item("Wood");
            ItemDefinition copperBar = Item("CopperBar");
            var workbench = AssetDatabase.LoadAssetAtPath<WorkbenchDefinition>("Assets/NAV/ScriptableObjects/Workbenches/BasicWorkbench.asset");
            if (wood == null || copperBar == null || workbench == null || AssetDatabase.LoadAssetAtPath<GameObject>(NskTreePath) == null)
            {
                Debug.LogError("OakSetup: run the vegetation and Stage C setups first (needs Wood, Copper Bar, the workbench and NSK_tree03_WoodNode).");
                return;
            }

            _oak = Mat("OakWood_material", new Color(0.36f, 0.24f, 0.15f), 0.2f);
            _carving = Mat("OakCarving_material", new Color(0.8f, 0.68f, 0.5f), 0.25f);
            _iron = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "Iron_material.mat");

            // ---- Item ----
            ItemDefinition oakWood = CreatureLootSetup.BuildItem("OakWood", "Oak Wood", "Heavy, dark oak from an old tree. For beams, upper floors, gates and carved work.",
                50, 1.2f, root =>
                {
                    for (int i = 0; i < 3; i++)
                    {
                        CreatureLootSetup.AddPart(root, PrimitiveType.Cylinder, "Log", _oak,
                            new Vector3(-0.09f + i * 0.09f, 0.05f + (i == 1 ? 0.07f : 0f), 0f), new Vector3(90f, 0f, 0f), new Vector3(0.09f, 0.22f, 0.09f));
                    }
                }, Quaternion.Euler(-25f, 30f, 0f));
            CreatureLootSetup.AddToList(new SerializedObject(AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/NAV/ScriptableObjects/Core/DefaultItemDatabase.asset")), "_items", oakWood);

            // ---- Tree and its felling chain ----
            BuildOakTree(oakWood);
            // Old oaks grow only in the dense forest - DenseForestSetup places them there.

            // ---- Pieces ----
            var pieces = new List<BuildingPieceDefinition>
            {
                Piece("JoinersBench", "Joiner's Bench", new[] { (oakWood, 6), (wood, 6), (copperBar, 1) }, null, 0,
                    BuildJoinersBench, root => AddJoinersBenchExtension(root, workbench)),
                Piece("OakWall", "Oak Log Wall", new[] { (oakWood, 3) }, workbench, RequiredWorkbenchLevel, BuildOakWall, null),
                Piece("OakWindowWall", "Oak Window Wall", new[] { (oakWood, 3), (wood, 1) }, workbench, RequiredWorkbenchLevel, BuildOakWindowWall, null),
                Piece("OakPost", "Oak Post", new[] { (oakWood, 2) }, workbench, RequiredWorkbenchLevel, BuildOakPost, null),
                Piece("OakBeam", "Oak Beam", new[] { (oakWood, 2) }, workbench, RequiredWorkbenchLevel, BuildOakBeam, null),
                Piece("OakFloor", "Oak Floor", new[] { (oakWood, 4) }, workbench, RequiredWorkbenchLevel, BuildOakFloor, null),
                Piece("OakStairs", "Oak Stairs", new[] { (oakWood, 6) }, workbench, RequiredWorkbenchLevel, BuildOakStairs, null),
                Piece("OakGate", "Oak Gate", new[] { (oakWood, 8), (copperBar, 2) }, workbench, RequiredWorkbenchLevel, BuildOakGate, AddGateDoor),
                Piece("OakRidge", "Carved Ridge", new[] { (oakWood, 2), (wood, 1) }, workbench, RequiredWorkbenchLevel, BuildOakRidge, null),
            };

            var pieceDatabase = new SerializedObject(AssetDatabase.LoadAssetAtPath<BuildingPieceDatabase>("Assets/NAV/ScriptableObjects/Core/BuildingPieceDatabase.asset"));
            PlayerBuilding building = Object.FindAnyObjectByType<PlayerBuilding>(FindObjectsInactive.Include);
            foreach (BuildingPieceDefinition piece in pieces)
            {
                CreatureLootSetup.AddToList(pieceDatabase, "_pieces", piece);
                if (building != null)
                {
                    CreatureLootSetup.AddToList(new SerializedObject(building), "_knownPieces", piece);
                }
            }

            AssetDatabase.SaveAssets();
            if (building != null)
            {
                EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
                EditorSceneManager.SaveScene(building.gameObject.scene);
            }

            Debug.Log($"OakSetup: Old Oak (+ felling chain, Oak Wood), Joiner's Bench and {pieces.Count - 1} oak pieces built. Scene saved.");
        }

        // The joiner's bench is the workbench's second upgrade (WorkbenchExtension).
        private static void AddJoinersBenchExtension(GameObject root, WorkbenchDefinition workbench)
        {
            var extension = new SerializedObject(root.AddComponent<WorkbenchExtension>());
            extension.FindProperty("_upgrades").objectReferenceValue = workbench;
            extension.FindProperty("_extensionId").stringValue = "JoinersBench";
            extension.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Tree ----

        private static GameObject BuildOakTree(ItemDefinition oakWood)
        {
            ResourceNodeDefinition logDefinition = TreeFellingSetup.GetOrCreateDefinition("OakLogNode");
            ResourceNodeDefinition fallenDefinition = TreeFellingSetup.GetOrCreateDefinition("OakFallenNode");
            ResourceNodeDefinition treeDefinition = TreeFellingSetup.GetOrCreateDefinition("OakTreeNode");
            Material bark = TintedCopy(AssetDatabase.LoadAssetAtPath<Material>("Assets/NAV/Materials/Vegetation/NSK_tree03_Bark.mat"), "OakBark", new Color(0.55f, 0.45f, 0.38f));
            Material leaves = TintedCopy(AssetDatabase.LoadAssetAtPath<Material>("Assets/NAV/Materials/Vegetation/NSK_tree03_Leaves.mat"), "OakLeaves", new Color(0.72f, 0.82f, 0.62f));

            GameObject stump = BuildStump();
            GameObject log = BuildLog(logDefinition);
            GameObject fallen = BuildFallen(fallenDefinition);

            var logSerialized = new SerializedObject(logDefinition);
            SerializedProperty drops = logSerialized.FindProperty("_drops");
            drops.arraySize = 1;
            drops.GetArrayElementAtIndex(0).FindPropertyRelative("_item").objectReferenceValue = oakWood;
            drops.GetArrayElementAtIndex(0).FindPropertyRelative("_amount").intValue = 3;
            drops.GetArrayElementAtIndex(0).FindPropertyRelative("_chance").floatValue = 1f;
            logSerialized.ApplyModifiedPropertiesWithoutUndo();

            // Tool tier 2 (copper axe) for the tree, the trunk and the logs.
            TreeFellingSetup.Configure(logSerialized, "Oak Log", 3, null, null, 0f);
            TreeFellingSetup.Configure(new SerializedObject(fallenDefinition), "Fallen Oak", 4, log, null, 0f);
            TreeFellingSetup.Configure(new SerializedObject(treeDefinition), "Old Oak", 6, fallen, stump, 450f);
            foreach (ResourceNodeDefinition definition in new[] { logDefinition, fallenDefinition, treeDefinition })
            {
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("_minToolTier").intValue = 2;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
            }

            GameObject tree = TreeFellingSetup.InstantiateUnpacked(NskTreePath);
            tree.name = "OldOak_WoodNode";
            foreach (Renderer renderer in tree.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null)
                    {
                        continue;
                    }

                    materials[i] = materials[i].name.Contains("Leaves") ? (leaves != null ? leaves : materials[i]) : (bark != null ? bark : materials[i]);
                }

                renderer.sharedMaterials = materials;
            }

            var node = new SerializedObject(tree.GetComponent<ResourceNode>());
            node.FindProperty("_definition").objectReferenceValue = treeDefinition;
            node.ApplyModifiedPropertiesWithoutUndo();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tree, $"{VegetationFolder}OldOak_WoodNode.prefab");
            Object.DestroyImmediate(tree);
            return prefab;
        }

        private static GameObject BuildStump()
        {
            var root = new GameObject("OakStump");
            GameObject model = TreeFellingSetup.InstantiateUnpacked(TreeFellingSetup.StumpPath);
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = Vector3.one * StumpScale;
            model.transform.localPosition = new Vector3(0f, -StumpSink, 0f);
            DarkenWood(root);
            return TreeFellingSetup.Save(root, "OakStump");
        }

        private static GameObject BuildLog(ResourceNodeDefinition definition)
        {
            var root = new GameObject("OakLog");
            TreeFellingSetup.AddLogVisual(root.transform, "Visual", LogLength, LogRadius, Vector3.zero, Quaternion.identity);
            DarkenWood(root);
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(LogLength, LogRadius * 2f, LogRadius * 2f);
            box.sharedMaterial = TreeFellingSetup.GetOrCreateFrictionMaterial();
            TreeFellingSetup.AddBody(root, 110f, 4f);
            TreeFellingSetup.AddNode(root, definition, null);
            return TreeFellingSetup.Save(root, "OakLog");
        }

        private static GameObject BuildFallen(ResourceNodeDefinition definition)
        {
            var root = new GameObject("OakFallen");
            TreeFellingSetup.AddLogVisual(root.transform, "TrunkVisual", TrunkLength, TrunkRadius,
                new Vector3(0f, CutHeight + TrunkLength * 0.5f, 0f), Quaternion.Euler(0f, 0f, 90f));
            DarkenWood(root);
            var trunk = root.AddComponent<BoxCollider>();
            trunk.size = new Vector3(TrunkRadius * 2f, TrunkLength, TrunkRadius * 2f);
            trunk.center = new Vector3(0f, CutHeight + TrunkLength * 0.5f, 0f);
            trunk.sharedMaterial = TreeFellingSetup.GetOrCreateFrictionMaterial();

            var points = new Transform[LogCentres.Length];
            for (int i = 0; i < LogCentres.Length; i++)
            {
                var point = new GameObject($"LogPoint_{i + 1}").transform;
                point.SetParent(root.transform, false);
                point.localPosition = new Vector3(0f, CutHeight + LogCentres[i], 0f);
                point.localRotation = Quaternion.Euler(0f, 0f, 90f);
                points[i] = point;
            }

            TreeFellingSetup.AddBody(root, 600f, 0.5f);
            TreeFellingSetup.AddNode(root, definition, points);
            var falling = new SerializedObject(root.AddComponent<FallingTree>());
            falling.FindProperty("_treeHeight").floatValue = TreeHeight;
            falling.FindProperty("_upperContactHeight").floatValue = 2.2f;
            falling.ApplyModifiedPropertiesWithoutUndo();
            return TreeFellingSetup.Save(root, "OakFallen");
        }

        // Oak is darker than spruce - tinted copies of the log/stump pack materials.
        private static void DarkenWood(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = TintedCopy(materials[i], $"{(materials[i] != null ? materials[i].name : "Wood")}_Oak", new Color(0.62f, 0.5f, 0.42f));
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static Material TintedCopy(Material source, string name, Color tint)
        {
            if (source == null)
            {
                return null;
            }

            string path = $"{MaterialFolder}{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.CopyPropertiesFromMaterial(source);
            Color baseColor = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white;
            material.SetColor("_BaseColor", baseColor * tint);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---- Pieces ----

        private static BuildingPieceDefinition Piece(string id, string displayName, (ItemDefinition item, int amount)[] cost,
            WorkbenchDefinition requiredStation, int requiredLevel, System.Action<Transform> buildModel, System.Action<GameObject> addComponents)
        {
            string definitionPath = $"{PieceFolder}{id}Piece.asset";
            var definition = AssetDatabase.LoadAssetAtPath<BuildingPieceDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BuildingPieceDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }

            var root = new GameObject(id);
            buildModel(root.transform);
            // BuildingPiece requires a collider on the root. Pieces whose solid parts are children
            // (post, beam, floor, stairs, gate) get a tiny trigger there - a full box would, say,
            // close the gate's opening.
            if (root.GetComponent<Collider>() == null)
            {
                var marker = root.AddComponent<BoxCollider>();
                marker.isTrigger = true;
                marker.size = Vector3.one * 0.05f;
            }

            var piece = new SerializedObject(root.AddComponent<BuildingPiece>());
            piece.FindProperty("_definition").objectReferenceValue = definition;
            piece.ApplyModifiedPropertiesWithoutUndo();
            addComponents?.Invoke(root);

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
            serialized.FindProperty("_requiredStation").objectReferenceValue = requiredStation;
            serialized.FindProperty("_requiredStationLevel").intValue = requiredLevel;
            serialized.FindProperty("_icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
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

        /// <summary>A visible box with its own collider (pieces are solid).</summary>
        private static GameObject Box(Transform parent, string name, Material material, Vector3 center, Vector3 size, Vector3 euler = default)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = center;
            part.transform.localEulerAngles = euler;
            part.transform.localScale = size;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        /// <summary>A round log along X with a box collider.</summary>
        private static void Log(Transform parent, string name, Vector3 center, float length, float radius)
        {
            CreatureLootSetup.AddPart(parent, PrimitiveType.Cylinder, name, _oak, center, new Vector3(0f, 0f, 90f), new Vector3(radius * 2f, length * 0.5f, radius * 2f));
        }

        private static void Snap(Transform parent, string name, Vector3 position, float yaw, BuildingSnapPoint.SnapKind kind)
        {
            var point = new GameObject(name);
            point.transform.SetParent(parent, false);
            point.transform.localPosition = position;
            point.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
            var serialized = new SerializedObject(point.AddComponent<BuildingSnapPoint>());
            serialized.FindProperty("_kind").enumValueIndex = (int)kind;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Wall-like pieces: root at the wall's centre, 2 x 2 m, the same snap points as WoodWall.
        private static void WallSnaps(Transform root)
        {
            Snap(root, "SnapPoint_Bottom", new Vector3(0f, -1f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
            Snap(root, "SnapPoint_BottomLeft", new Vector3(-1f, -1f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
            Snap(root, "SnapPoint_BottomRight", new Vector3(1f, -1f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
            Snap(root, "SnapPoint_TopLeft", new Vector3(-1f, 1f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
            Snap(root, "SnapPoint_TopRight", new Vector3(1f, 1f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
        }

        /// <summary>Five stacked logs; one collider for the whole wall.</summary>
        private static void BuildOakWall(Transform root)
        {
            for (int i = 0; i < 5; i++)
            {
                Log(root, $"Log{i}", new Vector3(0f, -0.8f + i * 0.4f, 0f), 2.1f, 0.2f);
            }

            var box = root.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(2f, 2f, 0.3f);
            WallSnaps(root);
        }

        /// <summary>Logs below and above a window, posts at its sides and a carved frame
        /// (наличник): side boards, a sill and a peaked top with a sun rosette.</summary>
        private static void BuildOakWindowWall(Transform root)
        {
            const float sill = -0.25f;
            const float top = 0.55f;
            for (int i = 0; i < 2; i++)
            {
                Log(root, $"LogLow{i}", new Vector3(0f, -0.8f + i * 0.4f, 0f), 2.1f, 0.2f);
            }

            Log(root, "LogHigh", new Vector3(0f, 0.8f, 0f), 2.1f, 0.2f);
            foreach (float x in new[] { -0.7f, 0.7f })
            {
                Box(root, x < 0f ? "SideL" : "SideR", _oak, new Vector3(x, (sill + top) * 0.5f, 0f), new Vector3(0.6f, top - sill, 0.3f));
            }

            Box(root, "Lintel", _oak, new Vector3(0f, (top + 0.6f) * 0.5f, 0f), new Vector3(0.8f, 0.6f - top, 0.3f));
            // Carved frame on the outside (+Z).
            const float z = 0.19f;
            foreach (float x in new[] { -0.47f, 0.47f })
            {
                Box(root, "FrameSide", _carving, new Vector3(x, (sill + top) * 0.5f, z), new Vector3(0.1f, top - sill + 0.1f, 0.06f));
            }

            Box(root, "FrameSill", _carving, new Vector3(0f, sill - 0.06f, z + 0.02f), new Vector3(1.1f, 0.08f, 0.1f));
            Box(root, "FramePeakL", _carving, new Vector3(-0.28f, top + 0.16f, z), new Vector3(0.62f, 0.08f, 0.06f), new Vector3(0f, 0f, 24f));
            Box(root, "FramePeakR", _carving, new Vector3(0.28f, top + 0.16f, z), new Vector3(0.62f, 0.08f, 0.06f), new Vector3(0f, 0f, -24f));
            Box(root, "Rosette", _carving, new Vector3(0f, top + 0.12f, z + 0.01f), new Vector3(0.12f, 0.12f, 0.05f), new Vector3(0f, 0f, 45f));
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            {
                if (collider.name.StartsWith("Frame") || collider.name == "Rosette")
                {
                    Object.DestroyImmediate(collider);
                }
            }

            // Collider for the low and high logs (the cylinders have none).
            var low = root.gameObject.AddComponent<BoxCollider>();
            low.center = new Vector3(0f, -0.6f, 0f);
            low.size = new Vector3(2f, 0.8f, 0.3f);
            var high = root.gameObject.AddComponent<BoxCollider>();
            high.center = new Vector3(0f, 0.8f, 0f);
            high.size = new Vector3(2f, 0.4f, 0.3f);
            WallSnaps(root);
        }

        /// <summary>A square post, 2 m: bottom to a foundation corner, top for beams.</summary>
        private static void BuildOakPost(Transform root)
        {
            Box(root, "Post", _oak, Vector3.zero, new Vector3(0.3f, 2f, 0.3f));
            Box(root, "Cap", _carving, new Vector3(0f, 0.95f, 0f), new Vector3(0.36f, 0.1f, 0.36f));
            Snap(root, "SnapPoint_Bottom", new Vector3(0f, -1f, 0f), 0f, BuildingSnapPoint.SnapKind.Corner);
            Snap(root, "SnapPoint_Top", new Vector3(0f, 1f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
        }

        /// <summary>A 2 m beam that sits on wall tops or post tops by its ends.</summary>
        private static void BuildOakBeam(Transform root)
        {
            Box(root, "Beam", _oak, Vector3.zero, new Vector3(2f, 0.26f, 0.26f));
            Snap(root, "SnapPoint_EndL", new Vector3(-1f, -0.13f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
            Snap(root, "SnapPoint_EndR", new Vector3(1f, -0.13f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
        }

        /// <summary>A 2 x 2 m plank floor for upper storeys: rests on wall tops by its bottom
        /// corners; its top edges take walls like a foundation's.</summary>
        private static void BuildOakFloor(Transform root)
        {
            Box(root, "Floor", _oak, Vector3.zero, new Vector3(2f, 0.2f, 2f));
            for (int i = 0; i < 4; i++)
            {
                Object.DestroyImmediate(Box(root, $"Seam{i}", _carving, new Vector3(-0.75f + i * 0.5f, 0.101f, 0f), new Vector3(0.02f, 0.002f, 2f)).GetComponent<Collider>());
            }

            Snap(root, "SnapPoint_North", new Vector3(0f, 0.1f, 1f), 0f, BuildingSnapPoint.SnapKind.Edge);
            Snap(root, "SnapPoint_South", new Vector3(0f, 0.1f, -1f), 0f, BuildingSnapPoint.SnapKind.Edge);
            Snap(root, "SnapPoint_East", new Vector3(1f, 0.1f, 0f), 90f, BuildingSnapPoint.SnapKind.Edge);
            Snap(root, "SnapPoint_West", new Vector3(-1f, 0.1f, 0f), 90f, BuildingSnapPoint.SnapKind.Edge);
            foreach (Vector2 corner in new[] { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f) })
            {
                Snap(root, "SnapPoint_Corner", new Vector3(corner.x, 0.1f, corner.y), 0f, BuildingSnapPoint.SnapKind.Corner);
                Snap(root, "SnapPoint_Under", new Vector3(corner.x, -0.1f, corner.y), 0f, BuildingSnapPoint.SnapKind.Edge);
            }
        }

        /// <summary>Eight 25 cm steps up 2 m over 2 m, 2 m wide; root at the bottom front edge.</summary>
        private static void BuildOakStairs(Transform root)
        {
            for (int i = 0; i < 8; i++)
            {
                float depth = 2f - i * 0.25f;
                Box(root, $"Step{i}", _oak, new Vector3(0f, 0.125f + i * 0.25f, i * 0.25f + depth * 0.5f), new Vector3(2f, 0.25f, depth));
            }

            foreach (float x in new[] { -1f, 1f })
            {
                Box(root, "String", _carving, new Vector3(x, 1f, 1f), new Vector3(0.08f, 0.25f, 2.85f), new Vector3(-45f, 0f, 0f));
            }

            Snap(root, "SnapPoint_Bottom", Vector3.zero, 0f, BuildingSnapPoint.SnapKind.Edge);
            Snap(root, "SnapPoint_Top", new Vector3(0f, 2f, 2f), 0f, BuildingSnapPoint.SnapKind.Edge);
        }

        /// <summary>A 4 m double gate between two posts under a lintel; the leaves hang on hinge
        /// pivots at the posts (BuildingDoor). Root at the bottom centre; snaps onto two
        /// neighbouring foundation edges.</summary>
        private static void BuildOakGate(Transform root)
        {
            foreach (float x in new[] { -2.15f, 2.15f })
            {
                Box(root, x < 0f ? "PostL" : "PostR", _oak, new Vector3(x, 1.25f, 0f), new Vector3(0.3f, 2.5f, 0.3f));
            }

            Box(root, "Lintel", _oak, new Vector3(0f, 2.6f, 0f), new Vector3(4.6f, 0.25f, 0.32f));
            Box(root, "Gable", _carving, new Vector3(0f, 2.82f, 0.02f), new Vector3(1.2f, 0.2f, 0.2f));
            foreach (float side in new[] { -1f, 1f })
            {
                var hinge = new GameObject(side < 0f ? "HingeL" : "HingeR").transform;
                hinge.SetParent(root, false);
                hinge.localPosition = new Vector3(2f * side, 0f, 0f);
                Box(hinge, "Leaf", _oak, new Vector3(-1f * side, 1.15f, 0f), new Vector3(1.98f, 2.2f, 0.12f));
                foreach (float y in new[] { 0.45f, 1.85f })
                {
                    Object.DestroyImmediate(Box(hinge, "Band", _iron, new Vector3(-0.9f * side, y, 0.07f), new Vector3(1.7f, 0.1f, 0.02f)).GetComponent<Collider>());
                }
            }

            Snap(root, "SnapPoint_BottomL", new Vector3(-1f, 0f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
            Snap(root, "SnapPoint_BottomR", new Vector3(1f, 0f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
        }

        private static void AddGateDoor(GameObject root)
        {
            var door = new SerializedObject(root.AddComponent<NAV.Gameplay.Building.BuildingDoor>());
            SerializedProperty leaves = door.FindProperty("_leaves");
            leaves.arraySize = 2;
            leaves.GetArrayElementAtIndex(0).FindPropertyRelative("Hinge").objectReferenceValue = root.transform.Find("HingeL");
            leaves.GetArrayElementAtIndex(0).FindPropertyRelative("OpenAngle").floatValue = 95f;
            leaves.GetArrayElementAtIndex(1).FindPropertyRelative("Hinge").objectReferenceValue = root.transform.Find("HingeR");
            leaves.GetArrayElementAtIndex(1).FindPropertyRelative("OpenAngle").floatValue = -95f;
            door.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A carved roof ridge (конёк): a 2 m ridge board with a horse head at one end.</summary>
        private static void BuildOakRidge(Transform root)
        {
            Box(root, "Ridge", _oak, Vector3.zero, new Vector3(2f, 0.2f, 0.2f));
            Box(root, "Neck", _carving, new Vector3(1.05f, 0.22f, 0f), new Vector3(0.18f, 0.5f, 0.14f), new Vector3(0f, 0f, -20f));
            Box(root, "Head", _carving, new Vector3(1.22f, 0.48f, 0f), new Vector3(0.36f, 0.16f, 0.13f), new Vector3(0f, 0f, -10f));
            Box(root, "Mane", _oak, new Vector3(1f, 0.42f, 0f), new Vector3(0.08f, 0.3f, 0.16f), new Vector3(0f, 0f, -25f));
            Snap(root, "SnapPoint_EndL", new Vector3(-1f, -0.1f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
            Snap(root, "SnapPoint_EndR", new Vector3(1f, -0.1f, 0f), 0f, BuildingSnapPoint.SnapKind.Edge);
        }

        /// <summary>A sturdy bench with a vise, a saw and a plane; the workbench's upgrade.</summary>
        private static void BuildJoinersBench(Transform root)
        {
            Box(root, "Top", _oak, new Vector3(0f, 0.85f, 0f), new Vector3(1.8f, 0.12f, 0.7f));
            foreach (float x in new[] { -0.75f, 0.75f })
            {
                foreach (float z in new[] { -0.25f, 0.25f })
                {
                    Box(root, "Leg", _oak, new Vector3(x, 0.4f, z), new Vector3(0.12f, 0.8f, 0.12f));
                }
            }

            Box(root, "Vise", _oak, new Vector3(-0.95f, 0.8f, 0.2f), new Vector3(0.12f, 0.25f, 0.3f));
            Box(root, "ViseScrew", _iron, new Vector3(-1.05f, 0.8f, 0.2f), new Vector3(0.18f, 0.05f, 0.05f));
            Box(root, "SawBlade", _iron, new Vector3(0.3f, 0.93f, 0.1f), new Vector3(0.7f, 0.02f, 0.16f), new Vector3(0f, 25f, 0f));
            Box(root, "SawHandle", _carving, new Vector3(-0.05f, 0.95f, 0.25f), new Vector3(0.14f, 0.06f, 0.1f), new Vector3(0f, 25f, 0f));
            Box(root, "Plane", _carving, new Vector3(0.55f, 0.95f, -0.18f), new Vector3(0.3f, 0.08f, 0.1f));
            Box(root, "Board", _carving, new Vector3(-0.3f, 0.93f, -0.15f), new Vector3(0.9f, 0.04f, 0.22f), new Vector3(0f, -8f, 0f));
            foreach (Collider collider in root.GetComponentsInChildren<Collider>())
            {
                Object.DestroyImmediate(collider);
            }

            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.5f, 0f);
            box.size = new Vector3(2f, 1f, 0.75f);
        }

        private static Material Mat(string name, Color color, float smoothness)
        {
            string path = $"{MaterialFolder}{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static ItemDefinition Item(string id) => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{Items}{id}.asset");
    }
}
