using System.IO;
using UnityEditor;
using UnityEngine;
using NAV.Gameplay.Items;

namespace NAV.Editor
{
    /// <summary>
    /// Builds the Valheim-style felling chain for the spruce:
    ///   UNS_Spruce_WoodNode (standing, TreeWoodNode) - no shrink, no drops; when felled it
    ///     leaves SpruceStump (Ultimate Nature stump) and becomes SpruceFallen, pushed over away
    ///     from the player;
    ///   SpruceFallen (SpruceFallenNode) - the bare trunk (Wood Pack "Log_1" stretched) that
    ///     replaces the tree the moment it's felled and topples (FallingTree); chopped, it splits
    ///     into two SpruceLogs along its length;
    ///   SpruceLog (SpruceLogNode) - a shorter Wood Pack log; chopped, it drops the wood (the
    ///     drops the standing tree used to have).
    /// Re-run after changing the numbers below - prefabs and definitions are overwritten in
    /// place (same GUIDs), TreeWoodNode is only re-pointed.
    /// </summary>
    internal static class TreeFellingSetup
    {
        private const string Folder = "Assets/NAV/Prefabs/World/Felling";
        private const string DefinitionFolder = "Assets/NAV/ScriptableObjects/World/Felling";
        private const string TreeDefinitionPath = "Assets/NAV/Scripts/Gameplay/Items/TreeWoodNode.asset";
        private const string StumpPath = "Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Props/Logs/Prefabs/UNS_Stump.prefab";
        private const string LogModelPath = "Assets/Static Soul Studio/Wood Pack/Built-in/Prefabs/Log_1.prefab";

        // Spruce model: ~12.5 m tall, trunk radius ~0.23 m.
        private const float TreeHeight = 12.5f;
        private const float StumpScale = 1.4f;   // UNS_Stump at 1: ~1 m tall, ~1.1 m wide, collider top 0.85 m
        private const float StumpSink = 0.35f;   // roots go into the ground, like the standing tree's
        private const float CutHeight = 0.9f;    // trunk starts just above the stump's collider (0.85 * 1.4 - 0.35 = 0.84)
        private const float TreeMass = 400f;
        private const float FallImpulse = 300f;

        // Wood Pack Log_1: lies along X, 2.2 m long, 0.25 m thick, pivot under its middle.
        private const float ModelLogLength = 2.2f;
        private const float ModelLogThickness = 0.25f;
        private const float ModelLogCentreY = 0.17f;

        private const float TrunkLength = 10f;
        private const float TrunkRadius = 0.32f;
        private const float LogLength = 4.6f;
        private const float LogRadius = 0.3f;
        private const float LogMass = 80f;
        // Log centres along the trunk, measured from the cut.
        private static readonly float[] LogCentres = { 2.5f, 7.4f };

        [MenuItem("NAV/World/Build Spruce Felling")]
        private static void Build()
        {
            EnsureFolder(Folder);
            EnsureFolder(DefinitionFolder);

            var treeDefinition = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(TreeDefinitionPath);
            if (treeDefinition == null || AssetDatabase.LoadAssetAtPath<GameObject>(LogModelPath) == null)
            {
                Debug.LogError($"TreeFellingSetup: '{TreeDefinitionPath}' or '{LogModelPath}' not found.");
                return;
            }

            ResourceNodeDefinition logDefinition = GetOrCreateDefinition("SpruceLogNode");
            ResourceNodeDefinition fallenDefinition = GetOrCreateDefinition("SpruceFallenNode");

            GameObject stump = BuildStump();
            GameObject log = BuildLog(logDefinition);
            GameObject fallen = BuildFallen(fallenDefinition);

            var treeSerialized = new SerializedObject(treeDefinition);

            // The wood moves from the standing tree to the logs (first run only - afterwards the
            // tree has no drops left to move).
            var logSerialized = new SerializedObject(logDefinition);
            if (treeSerialized.FindProperty("_drops").arraySize > 0)
            {
                logSerialized.CopyFromSerializedProperty(treeSerialized.FindProperty("_drops"));
                treeSerialized.FindProperty("_drops").ClearArray();
            }
            Configure(logSerialized, "Spruce Log", 2, null, null, 0f);
            Configure(new SerializedObject(fallenDefinition), "Fallen Spruce", 3, log, null, 0f);
            Configure(treeSerialized, null, -1, fallen, stump, FallImpulse);

            AssetDatabase.SaveAssets();
            Debug.Log($"TreeFellingSetup: built {Folder}/(SpruceStump, SpruceFallen, SpruceLog) and re-pointed {treeDefinition.name}. Log drops: {logDefinition.Drops.Count} entries.");
        }

        private static void Configure(SerializedObject definition, string displayName, int hits, GameObject becomes, GameObject leavesBehind, float impulse)
        {
            if (displayName != null)
            {
                definition.FindProperty("_displayName").stringValue = displayName;
            }

            if (hits > 0)
            {
                definition.FindProperty("_hitsToDeplete").intValue = hits;
            }

            definition.FindProperty("_requiredTool").intValue = (int)ToolType.Axe;
            definition.FindProperty("_shrinkOnHit").boolValue = false;
            definition.FindProperty("_becomesPrefab").objectReferenceValue = becomes;
            definition.FindProperty("_leavesBehindPrefab").objectReferenceValue = leavesBehind;
            definition.FindProperty("_fallImpulse").floatValue = impulse;
            definition.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition.targetObject);
        }

        private static GameObject BuildStump()
        {
            // Root pivot stays on the ground (where the tree stood); the model is scaled and sunk.
            var root = new GameObject("SpruceStump");
            GameObject model = InstantiateUnpacked(StumpPath);
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = Vector3.one * StumpScale;
            model.transform.localPosition = new Vector3(0f, -StumpSink, 0f);
            return Save(root, "SpruceStump");
        }

        private static GameObject BuildLog(ResourceNodeDefinition definition)
        {
            var root = new GameObject("SpruceLog");
            AddLogVisual(root.transform, "Visual", LogLength, LogRadius, Vector3.zero, Quaternion.identity);

            // A box, not a capsule: a round collider rolls down every slope (and into the lake).
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(LogLength, LogRadius * 2f, LogRadius * 2f);
            box.sharedMaterial = GetOrCreateFrictionMaterial();

            AddBody(root, LogMass, 4f);
            AddNode(root, definition, null);
            return Save(root, "SpruceLog");
        }

        private static GameObject BuildFallen(ResourceNodeDefinition definition)
        {
            // Root pivot = the standing tree's pivot (ground), so it spawns exactly where the tree
            // stood; everything solid starts at the cut, just above the stump's collider.
            var root = new GameObject("SpruceFallen");

            // The bare trunk (the felled tree is replaced by it at once), standing along the
            // root's Y - the model's X turned up.
            AddLogVisual(root.transform, "TrunkVisual", TrunkLength, TrunkRadius,
                new Vector3(0f, CutHeight + TrunkLength * 0.5f, 0f), Quaternion.Euler(0f, 0f, 90f));

            // A box, not a capsule: tips over its base edge while falling and doesn't roll once down.
            var trunk = root.AddComponent<BoxCollider>();
            trunk.size = new Vector3(TrunkRadius * 2f, TrunkLength, TrunkRadius * 2f);
            trunk.center = new Vector3(0f, CutHeight + TrunkLength * 0.5f, 0f);
            trunk.sharedMaterial = GetOrCreateFrictionMaterial();

            var points = new Transform[LogCentres.Length];
            for (int i = 0; i < LogCentres.Length; i++)
            {
                var point = new GameObject($"LogPoint_{i + 1}").transform;
                point.SetParent(root.transform, false);
                point.localPosition = new Vector3(0f, CutHeight + LogCentres[i], 0f);
                // The log's long axis (X) along the trunk (Y).
                point.localRotation = Quaternion.Euler(0f, 0f, 90f);
                points[i] = point;
            }

            AddBody(root, TreeMass, 0.5f);
            AddNode(root, definition, points);

            var falling = new SerializedObject(root.AddComponent<FallingTree>());
            falling.FindProperty("_treeHeight").floatValue = TreeHeight;
            falling.ApplyModifiedPropertiesWithoutUndo();

            return Save(root, "SpruceFallen");
        }

        /// <summary>A Wood Pack log stretched to length x radius, centred on localPosition with its
        /// long axis along the parent's X (rotated by localRotation).</summary>
        private static GameObject AddLogVisual(Transform parent, string name, float length, float radius, Vector3 localPosition, Quaternion localRotation)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.localPosition = localPosition;
            holder.localRotation = localRotation;

            GameObject model = InstantiateUnpacked(LogModelPath);
            StripColliders(model);
            model.transform.SetParent(holder, false);
            float thicknessScale = radius * 2f / ModelLogThickness;
            model.transform.localScale = new Vector3(length / ModelLogLength, thicknessScale, thicknessScale);
            model.transform.localPosition = new Vector3(0f, -ModelLogCentreY * thicknessScale, 0f);
            return holder.gameObject;
        }

        private static void AddBody(GameObject root, float mass, float angularDamping)
        {
            var body = root.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.linearDamping = 0.1f;
            // Damping on top of the friction, so a log knocked loose settles quickly.
            body.angularDamping = angularDamping;
        }

        private static void AddNode(GameObject root, ResourceNodeDefinition definition, Transform[] spawnPoints)
        {
            var node = root.AddComponent<ResourceNode>();
            var serialized = new SerializedObject(node);
            serialized.FindProperty("_definition").objectReferenceValue = definition;
            var points = serialized.FindProperty("_becomesSpawnPoints");
            points.arraySize = spawnPoints != null ? spawnPoints.Length : 0;
            for (int i = 0; i < points.arraySize; i++)
            {
                points.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject InstantiateUnpacked(string path)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            return instance;
        }

        /// <summary>Visual-only copies: the packs' own colliders (incl. non-convex MeshColliders,
        /// which a Rigidbody can't use) are replaced by the root's own colliders.</summary>
        private static void StripColliders(GameObject visual)
        {
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static GameObject Save(GameObject root, string name)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Grippy wood: full friction, no bounce, Maximum combine so even a smooth surface
        /// (the water plane, building floors) holds a log.</summary>
        private static PhysicsMaterial GetOrCreateFrictionMaterial()
        {
            const string path = Folder + "/LogFriction.asset";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material == null)
            {
                material = new PhysicsMaterial("LogFriction");
                AssetDatabase.CreateAsset(material, path);
            }

            material.dynamicFriction = 1f;
            material.staticFriction = 1f;
            material.bounciness = 0f;
            material.frictionCombine = PhysicsMaterialCombine.Maximum;
            material.bounceCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static ResourceNodeDefinition GetOrCreateDefinition(string name)
        {
            string path = $"{DefinitionFolder}/{name}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            return definition;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
