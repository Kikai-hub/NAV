using System.Linq;
using UnityEditor;
using UnityEngine;
using NAV.Gameplay.Building;

namespace NAV.Editor
{
    /// <summary>
    /// Rebuilds the Wood Door piece so it opens (BuildingDoor, like the oak gate) and looks Old
    /// Rus - called by RusBuildingStyleSetup (NAV > Build > Restyle Starting Buildings). It used
    /// to be one scaled cube; now an unscaled root at the same pivot (the wall's centre) with the
    /// same snap points in metres. Colliders: two jambs and a lintel filling the 2 x 2 m wall slot
    /// around a 1.2 x 1.8 m opening, and the leaf on a hinge. Visuals (no colliders): сруб logs
    /// beside and above the opening, a carved frame (наличник) and a plank leaf with forged strap
    /// hinges and a ring. Saved over the same prefab (same GUID) - doors in saves load as the new
    /// one. Safe to re-run.
    /// </summary>
    internal static class WoodDoorSetup
    {
        private const string PrefabPath = "Assets/NAV/Prefabs/Build/WoodDoor.prefab";
        private const float LeafWidth = 1.2f;
        // Taller than the player's capsule (1.8 m + skin) - at 1.8 the player stuck in the opening.
        private const float LeafHeight = 1.9f;

        internal static void BuildRus(Material log, Material logEnd, Material moss, Material plank, Material carving)
        {
            Material iron = AssetDatabase.LoadAssetAtPath<Material>("Assets/NAV/Materials/Items/Iron_material.mat");
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // First run converts the old scaled cube; snap points keep their world positions.
                Vector3 oldScale = root.transform.localScale;
                if (oldScale != Vector3.one)
                {
                    foreach (BuildingSnapPoint point in root.GetComponentsInChildren<BuildingSnapPoint>(true))
                    {
                        point.transform.localPosition = Vector3.Scale(point.transform.localPosition, oldScale);
                        point.transform.localScale = Vector3.one;
                    }

                    root.transform.localScale = Vector3.one;
                }

                if (root.TryGetComponent(out MeshRenderer oldRenderer))
                {
                    Object.DestroyImmediate(oldRenderer);
                }

                if (root.TryGetComponent(out MeshFilter oldFilter))
                {
                    Object.DestroyImmediate(oldFilter);
                }

                foreach (Transform child in root.transform.Cast<Transform>().ToList())
                {
                    if (child.GetComponent<BuildingSnapPoint>() == null)
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }
                }

                // BuildingPiece requires a collider on the root - a tiny trigger; the frame and
                // the leaf have their own.
                var marker = root.GetComponent<BoxCollider>();
                marker.isTrigger = true;
                marker.center = Vector3.zero;
                marker.size = Vector3.one * 0.05f;

                // Colliders (wall centre = 0: floor at y = -1, top at y = +1).
                float jambWidth = (2f - LeafWidth) * 0.5f;
                foreach (float side in new[] { -1f, 1f })
                {
                    Solid(root.transform, side < 0f ? "JambL" : "JambR", new Vector3(side * (LeafWidth * 0.5f + jambWidth * 0.5f), 0f, 0f), new Vector3(jambWidth, 2f, 0.2f));
                }

                float lintelHeight = 2f - LeafHeight;
                Solid(root.transform, "Lintel", new Vector3(0f, -1f + LeafHeight + lintelHeight * 0.5f, 0f), new Vector3(LeafWidth, lintelHeight, 0.2f));

                // Visual: short сруб logs beside the opening (ends out past the wall's outer
                // edges like the wall's), a log above it.
                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                const int logs = 5;
                const float diameter = 0.4f;
                for (int i = 0; i < logs; i++)
                {
                    float y = -1f + diameter * (i + 0.5f);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float inner = LeafWidth * 0.5f;
                        float outer = 1f + 0.18f;
                        float length = outer - inner;
                        RusBuildingStyleSetup.LogAlongX(visual, "Log", new Vector3(side * (inner + length * 0.5f), y, 0f), length, diameter * 0.5f, 0.8f);
                    }
                }

                RusBuildingStyleSetup.Part(visual, PrimitiveType.Cube, "LintelBeam", log, new Vector3(0f, -1f + LeafHeight + lintelHeight * 0.5f, 0f), Vector3.zero, new Vector3(LeafWidth + 0.1f, lintelHeight, 0.3f));

                // Наличник: side boards, a top board with sun rosettes, on the front. Kept low - a
                // roof slope's underside runs just above the wall's front edge (no peak, it would
                // poke through the eave).
                const float z = 0.16f;
                foreach (float side in new[] { -1f, 1f })
                {
                    RusBuildingStyleSetup.Part(visual, PrimitiveType.Cube, "FrameSide", carving, new Vector3(side * (LeafWidth * 0.5f + 0.05f), -1f + LeafHeight * 0.5f, z), Vector3.zero, new Vector3(0.12f, LeafHeight, 0.05f));
                }

                // Hangs a little into the opening (visual only - the player passes under it).
                float frameTop = -1f + LeafHeight - 0.04f;
                RusBuildingStyleSetup.Part(visual, PrimitiveType.Cube, "FrameTop", carving, new Vector3(0f, frameTop, z), Vector3.zero, new Vector3(LeafWidth + 0.3f, 0.08f, 0.05f));
                foreach (float x in new[] { -0.45f, 0f, 0.45f })
                {
                    RusBuildingStyleSetup.Part(visual, PrimitiveType.Cube, "Rosette", carving, new Vector3(x, frameTop, z + 0.03f), new Vector3(0f, 0f, 45f), new Vector3(0.06f, 0.06f, 0.03f));
                }

                // Leaf on a hinge at the left side; swings away from the front (+Z). Its collider
                // is one box, the look is vertical planks with forged straps and a ring.
                var hinge = new GameObject("Hinge").transform;
                hinge.SetParent(root.transform, false);
                hinge.localPosition = new Vector3(-LeafWidth * 0.5f, -1f, 0f);
                Solid(hinge, "Leaf", new Vector3(LeafWidth * 0.5f, LeafHeight * 0.5f, 0f), new Vector3(LeafWidth - 0.02f, LeafHeight - 0.02f, 0.1f));
                const int boards = 5;
                float boardWidth = (LeafWidth - 0.02f) / boards;
                for (int i = 0; i < boards; i++)
                {
                    RusBuildingStyleSetup.Part(hinge, PrimitiveType.Cube, "Plank", i % 2 == 0 ? plank : log,
                        new Vector3(0.01f + boardWidth * (i + 0.5f), LeafHeight * 0.5f, 0f), Vector3.zero, new Vector3(boardWidth - 0.015f, LeafHeight - 0.02f, 0.1f));
                }

                foreach (float y in new[] { 0.35f, 1.45f })
                {
                    RusBuildingStyleSetup.Part(hinge, PrimitiveType.Cube, "Strap", iron, new Vector3(LeafWidth * 0.36f, y, 0.06f), Vector3.zero, new Vector3(LeafWidth * 0.66f, 0.07f, 0.02f));
                    RusBuildingStyleSetup.Part(hinge, PrimitiveType.Cube, "StrapTip", iron, new Vector3(LeafWidth * 0.7f, y, 0.06f), new Vector3(0f, 0f, 45f), new Vector3(0.09f, 0.09f, 0.02f));
                }

                RusBuildingStyleSetup.Part(hinge, PrimitiveType.Cylinder, "Ring", iron, new Vector3(LeafWidth - 0.16f, 0.95f, 0.07f), new Vector3(90f, 0f, 0f), new Vector3(0.12f, 0.01f, 0.12f));

                BuildingDoor door = root.GetComponent<BuildingDoor>() ?? root.AddComponent<BuildingDoor>();
                var serialized = new SerializedObject(door);
                SerializedProperty leaves = serialized.FindProperty("_leaves");
                leaves.arraySize = 1;
                leaves.GetArrayElementAtIndex(0).FindPropertyRelative("Hinge").objectReferenceValue = hinge;
                leaves.GetArrayElementAtIndex(0).FindPropertyRelative("OpenAngle").floatValue = 100f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // The build menu icon shows the new door (same file, so the piece keeps its sprite).
            const string iconPath = "Assets/NAV/Art/Icons/Icon_WoodDoor.png";
            System.IO.File.WriteAllBytes(iconPath, ItemIconRenderer.RenderIcon(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),
                Quaternion.Euler(-22f, 0f, 0f) * Quaternion.Euler(0f, 145f, 0f)));
            AssetDatabase.ImportAsset(iconPath);
            ItemIconRenderer.ConfigureSprite(iconPath);
        }

        /// <summary>An invisible box collider (the visuals are separate parts).</summary>
        private static void Solid(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = center;
            part.AddComponent<BoxCollider>().size = size;
        }
    }
}
