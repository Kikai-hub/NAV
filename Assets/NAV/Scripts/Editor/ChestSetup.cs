using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.Building;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;
using NAV.Presentation.Building;

namespace NAV.Editor
{
    /// <summary>
    /// Builds the wooden chest building piece, in the same style as the workbench (Unity cubes
    /// with the workbench's wood material, plus dark iron bands): Prefabs/Build/WoodChest
    /// (BoxCollider + BuildingPiece + StorageContainer with 10 slots + ChestLidAnimator; the Lid
    /// child's pivot is the back hinge), Materials/Building/ChestIron, the definition
    /// ScriptableObjects/Building/WoodChestPiece (10 wood, needs a workbench nearby), its icon
    /// Art/Icons/Icon_WoodChest.png, and registers the piece in BuildingPieceDatabase and the
    /// open scene's PlayerBuilding (save the scene afterwards). Re-run to rebuild in place.
    /// </summary>
    internal static class ChestSetup
    {
        private const string PrefabPath = "Assets/NAV/Prefabs/Build/WoodChest.prefab";
        private const string WorkbenchPrefabPath = "Assets/NAV/Prefabs/Build/WoodWorkbench.prefab";
        private const string IronMaterialPath = "Assets/NAV/Materials/Building/ChestIron.mat";
        private const string DefinitionPath = "Assets/NAV/ScriptableObjects/Building/WoodChestPiece.asset";
        private const string WorkbenchDefinitionPath = "Assets/NAV/ScriptableObjects/Building/WoodWorkbenchPiece.asset";
        private const string DatabasePath = "Assets/NAV/ScriptableObjects/Core/BuildingPieceDatabase.asset";
        private const string IconPath = "Assets/NAV/Art/Icons/Icon_WoodChest.png";
        private const int WoodCost = 10;

        // Outer size of the closed chest (m); the root pivot is its centre, like the workbench.
        private const float Width = 1f;
        private const float Height = 0.62f;
        private const float Depth = 0.6f;
        private const float LidHeight = 0.2f;

        [MenuItem("NAV/Build/Build Wood Chest")]
        private static void Build()
        {
            Material wood = AssetDatabase.LoadAssetAtPath<GameObject>(WorkbenchPrefabPath).GetComponentInChildren<Renderer>().sharedMaterial;
            Material iron = GetOrCreateIron(wood);

            var definition = AssetDatabase.LoadAssetAtPath<BuildingPieceDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BuildingPieceDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            GameObject prefab = BuildPrefab(wood, iron, definition);

            // Same wood item as the workbench's cost.
            var workbenchDefinition = new SerializedObject(AssetDatabase.LoadAssetAtPath<BuildingPieceDefinition>(WorkbenchDefinitionPath));
            Object woodItem = workbenchDefinition.FindProperty("_cost").GetArrayElementAtIndex(0).FindPropertyRelative("_item").objectReferenceValue;

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_id").stringValue = "WoodChestPiece";
            serialized.FindProperty("_displayName").stringValue = "Chest";
            serialized.FindProperty("_prefab").objectReferenceValue = prefab;
            serialized.FindProperty("_requiresWorkbench").boolValue = true;
            SerializedProperty cost = serialized.FindProperty("_cost");
            cost.arraySize = 1;
            cost.GetArrayElementAtIndex(0).FindPropertyRelative("_item").objectReferenceValue = woodItem;
            cost.GetArrayElementAtIndex(0).FindPropertyRelative("_amount").intValue = WoodCost;
            serialized.FindProperty("_icon").objectReferenceValue = RenderIcon(prefab);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);

            AddToList(new SerializedObject(AssetDatabase.LoadAssetAtPath<BuildingPieceDatabase>(DatabasePath)), "_pieces", definition);

            PlayerBuilding building = Object.FindAnyObjectByType<PlayerBuilding>(FindObjectsInactive.Include);
            if (building != null)
            {
                AddToList(new SerializedObject(building), "_knownPieces", definition);
                EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"ChestSetup: built {PrefabPath} and {DefinitionPath}" + (building != null ? $", added to '{building.name}' - save the scene." : " (no PlayerBuilding in the open scene)."));
        }

        private static GameObject BuildPrefab(Material wood, Material iron, BuildingPieceDefinition definition)
        {
            var root = new GameObject("WoodChest");
            float bodyHeight = Height - LidHeight;
            float bottom = -Height * 0.5f;

            // Body + its two iron bands, a hasp and four little feet.
            AddCube(root.transform, "Body", wood, new Vector3(0f, bottom + bodyHeight * 0.5f, 0f), new Vector3(Width, bodyHeight, Depth));
            foreach (float x in new[] { -0.3f, 0.3f })
            {
                AddCube(root.transform, "Band", iron, new Vector3(x, bottom + bodyHeight * 0.5f, 0f), new Vector3(0.06f, bodyHeight + 0.01f, Depth + 0.02f));
            }

            AddCube(root.transform, "Hasp", iron, new Vector3(0f, bottom + bodyHeight - 0.06f, Depth * 0.5f + 0.015f), new Vector3(0.1f, 0.1f, 0.03f));

            // Lid: pivot on the hinge (top back edge of the body), boards and bands hang off it.
            var lid = new GameObject("Lid").transform;
            lid.SetParent(root.transform, false);
            lid.localPosition = new Vector3(0f, bottom + bodyHeight, -Depth * 0.5f);
            AddCube(lid, "LidBoard", wood, new Vector3(0f, LidHeight * 0.5f, Depth * 0.5f), new Vector3(Width + 0.02f, LidHeight, Depth + 0.02f));
            foreach (float x in new[] { -0.3f, 0.3f })
            {
                AddCube(lid, "LidBand", iron, new Vector3(x, LidHeight * 0.5f, Depth * 0.5f), new Vector3(0.06f, LidHeight + 0.01f, Depth + 0.04f));
            }

            AddCube(lid, "Lock", iron, new Vector3(0f, 0.03f, Depth + 0.02f), new Vector3(0.14f, 0.14f, 0.03f));

            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(Width, Height, Depth);

            var piece = root.AddComponent<BuildingPiece>();
            var pieceSerialized = new SerializedObject(piece);
            pieceSerialized.FindProperty("_definition").objectReferenceValue = definition;
            pieceSerialized.ApplyModifiedPropertiesWithoutUndo();

            var storage = root.AddComponent<StorageContainer>();
            var animator = new SerializedObject(root.AddComponent<ChestLidAnimator>());
            animator.FindProperty("_container").objectReferenceValue = storage;
            animator.FindProperty("_lid").objectReferenceValue = lid;
            animator.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void AddCube(Transform parent, string name, Material material, Vector3 localPosition, Vector3 size)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = size;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material GetOrCreateIron(Material wood)
        {
            var iron = AssetDatabase.LoadAssetAtPath<Material>(IronMaterialPath);
            if (iron == null)
            {
                iron = new Material(wood) { name = "ChestIron" };
                AssetDatabase.CreateAsset(iron, IronMaterialPath);
            }

            iron.SetColor("_BaseColor", new Color(0.2f, 0.2f, 0.22f));
            iron.SetFloat("_Metallic", 0.6f);
            iron.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(iron);
            return iron;
        }

        private static Sprite RenderIcon(GameObject prefab)
        {
            // Front three-quarter view from slightly above (the camera looks along +Z).
            byte[] png = ItemIconRenderer.RenderIcon(prefab, Quaternion.Euler(-22f, 0f, 0f) * Quaternion.Euler(0f, 145f, 0f));
            File.WriteAllBytes(IconPath, png);
            AssetDatabase.ImportAsset(IconPath);
            ItemIconRenderer.ConfigureSprite(IconPath);
            return AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
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
    }
}
