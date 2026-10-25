using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.Building;

namespace NAV.Editor
{
    /// <summary>
    /// Gives the starting building pieces an Old Rus look (developer, 2026-10-24 - they were grey
    /// test cubes): the foundation, wall, roof and palisade prefabs are rebuilt in place (same
    /// GUIDs - pieces in saves load with the new look), and the door via WoodDoorSetup.
    ///   - wall: a сруб - five round pine logs with dark moss caulking between them, the ends
    ///     sticking out past the corner (рубка «в обло»), so two walls meeting cross like an izba;
    ///   - foundation: a plank floor on a log sill (окладной венец) with stones under the corners;
    ///   - roof: тёс - boards running down the slope, a carved valance (подзор) at the eave;
    ///   - gable (фронтон, new piece): a log triangle closing the end wall under a slope;
    ///   - palisade: a sharpened log (частокол).
    /// Wall, foundation and palisade keep their gameplay: the old prefabs were scaled cubes, now
    /// the root is unscaled and the same box collider and snap points sit at the same places in
    /// metres; the new parts are visual only. The roof was rebuilt as a real slope (2 m run, 2 m
    /// rise, eave hanging past the wall) with a sloped collider and new snap points - roofs placed
    /// before that sit wrong and must be rebuilt. Placeholder primitives. Safe to re-run.
    /// </summary>
    internal static class RusBuildingStyleSetup
    {
        private const string BuildPrefabs = "Assets/NAV/Prefabs/Build/";
        private const string MaterialFolder = "Assets/NAV/Materials/Building/";
        private const string ConeMeshPath = "Assets/NAV/Art/Models/LogTip.asset";
        private const string GableLogsMeshPath = "Assets/NAV/Art/Models/GableLogs.asset";
        private const string GableSolidMeshPath = "Assets/NAV/Art/Models/GableSolid.asset";
        private const string PieceFolder = "Assets/NAV/ScriptableObjects/Building/";
        private const string GableDefinitionPath = PieceFolder + "WoodGablePiece.asset";

        private static Material _log;
        private static Material _logEnd;
        private static Material _moss;
        private static Material _plank;
        private static Material _roof;
        private static Material _carving;
        private static Material _stone;
        private static Mesh _cone;

        [MenuItem("NAV/Build/Restyle Starting Buildings (Old Rus)")]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("RusBuildingStyleSetup: exit Play Mode first.");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/NAV/Materials/Building"))
            {
                AssetDatabase.CreateFolder("Assets/NAV/Materials", "Building");
            }

            _log = Mat("PineLog_material", new Color(0.6f, 0.43f, 0.27f), 0.15f);
            _logEnd = Mat("LogEnd_material", new Color(0.8f, 0.64f, 0.43f), 0.1f);
            _moss = Mat("Moss_material", new Color(0.24f, 0.24f, 0.14f), 0.05f);
            _plank = Mat("Plank_material", new Color(0.55f, 0.4f, 0.25f), 0.15f);
            _roof = Mat("RoofTes_material", new Color(0.46f, 0.39f, 0.31f), 0.1f);
            _carving = Mat("Carving_material", new Color(0.86f, 0.78f, 0.62f), 0.2f);
            _stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/NAV/Materials/CampfireStone_material.mat");
            _cone = BuildConeMesh();

            Restyle("WoodWall", BuildWall);
            Restyle("WoodFoundation", BuildFoundation);
            Restyle("WoodPalisade", BuildPalisade);
            WoodDoorSetup.BuildRus(_log, _logEnd, _moss, _plank, _carving);
            BuildShaped("WoodRoof", BuildRoof, null);
            BuildingPieceDefinition gable = GableDefinition();
            BuildShaped("WoodGable", BuildGable, gable);

            foreach (string id in new[] { "WoodWall", "WoodFoundation", "WoodRoof", "WoodGable", "WoodPalisade" })
            {
                RenderPieceIcon(id);
            }

            FillGableDefinition(gable);
            SetDirectional("WoodRoofPiece");
            SetDirectional("WoodDoorPiece");
            AssetDatabase.SaveAssets();
            Debug.Log("RusBuildingStyleSetup: wall, foundation, palisade, door, roof slope and gable built in the Old Rus style.");
        }

        /// <summary>Builds a piece from scratch over its prefab (or a new one): unscaled root with
        /// a tiny trigger (BuildingPiece needs a root collider - the solid parts are children)
        /// and a BuildingPiece; everything under it, snap points included, comes from build.</summary>
        private static void BuildShaped(string id, System.Action<Transform> build, BuildingPieceDefinition definition)
        {
            string path = $"{BuildPrefabs}{id}.prefab";
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(id);
            try
            {
                root.transform.localScale = Vector3.one;
                foreach (Transform child in root.transform.Cast<Transform>().ToList())
                {
                    Object.DestroyImmediate(child.gameObject);
                }

                if (root.TryGetComponent(out MeshRenderer oldRenderer))
                {
                    Object.DestroyImmediate(oldRenderer);
                }

                if (root.TryGetComponent(out MeshFilter oldFilter))
                {
                    Object.DestroyImmediate(oldFilter);
                }

                if (!root.TryGetComponent(out BoxCollider marker))
                {
                    marker = root.AddComponent<BoxCollider>();
                }

                marker.isTrigger = true;
                marker.center = Vector3.zero;
                marker.size = Vector3.one * 0.05f;

                if (!root.TryGetComponent(out BuildingPiece piece))
                {
                    piece = root.AddComponent<BuildingPiece>();
                }

                if (definition != null)
                {
                    var serialized = new SerializedObject(piece);
                    serialized.FindProperty("_definition").objectReferenceValue = definition;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                build(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (exists)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        /// <summary>Unscales the prefab's root (moving the snap points and the root collider to
        /// the same places in metres), drops the old cube mesh and any previous visual, and adds
        /// the new visual parts under "Visual".</summary>
        private static void Restyle(string id, System.Action<Transform, Vector3> buildVisual)
        {
            string path = $"{BuildPrefabs}{id}.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Vector3 scale = root.transform.localScale;
                var box = root.GetComponent<BoxCollider>();
                if (scale != Vector3.one)
                {
                    foreach (BuildingSnapPoint point in root.GetComponentsInChildren<BuildingSnapPoint>(true))
                    {
                        point.transform.localPosition = Vector3.Scale(point.transform.localPosition, scale);
                        point.transform.localScale = Vector3.one;
                    }

                    box.center = Vector3.Scale(box.center, scale);
                    box.size = Vector3.Scale(box.size, scale);
                    root.transform.localScale = Vector3.one;
                }

                // The collider's size in metres is the piece's footprint the visuals fill.
                Vector3 size = box.size;
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

                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                buildVisual(visual, size);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---- Pieces (centred on the piece's collider, size = its size in metres) ----

        /// <summary>Сруб: 5 logs 0.4 m thick filling 2 m, ends 0.18 m past each side.</summary>
        private static void BuildWall(Transform visual, Vector3 size)
        {
            const int logs = 5;
            float diameter = size.y / logs;
            float length = size.x + 0.36f;
            for (int i = 0; i < logs; i++)
            {
                float y = -size.y * 0.5f + diameter * (i + 0.5f);
                LogAlongX(visual, $"Log{i}", new Vector3(0f, y, 0f), length, diameter * 0.5f, 0.8f);
                if (i > 0)
                {
                    // Moss caulking in the seam below this log.
                    Part(visual, PrimitiveType.Cube, "Moss", _moss, new Vector3(0f, y - diameter * 0.5f, 0f), Vector3.zero, new Vector3(size.x, 0.05f, 0.24f));
                }
            }
        }

        /// <summary>A plank floor on a log sill, stones under the corners.</summary>
        private static void BuildFoundation(Transform visual, Vector3 size)
        {
            float top = size.y * 0.5f;
            const int boards = 6;
            float boardWidth = size.z / boards;
            for (int i = 0; i < boards; i++)
            {
                float z = -size.z * 0.5f + boardWidth * (i + 0.5f);
                Part(visual, PrimitiveType.Cube, $"Board{i}", i % 2 == 0 ? _plank : _log, new Vector3(0f, top - 0.04f, z), Vector3.zero, new Vector3(size.x, 0.08f, boardWidth - 0.02f));
            }

            // The sill: two logs along X under the edges, two along Z, ends crossing.
            foreach (float z in new[] { -size.z * 0.5f + 0.12f, size.z * 0.5f - 0.12f })
            {
                LogAlongX(visual, "SillX", new Vector3(0f, top - 0.2f, z), size.x + 0.2f, 0.13f, 0.75f);
            }

            foreach (float x in new[] { -size.x * 0.5f + 0.12f, size.x * 0.5f - 0.12f })
            {
                LogAlongZ(visual, "SillZ", new Vector3(x, top - 0.24f, 0f), size.z + 0.2f, 0.12f);
            }

            foreach (float x in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -1f, 1f })
                {
                    Part(visual, PrimitiveType.Sphere, "Stone", _stone, new Vector3(x * (size.x * 0.5f - 0.12f), top - 0.45f, z * (size.z * 0.5f - 0.12f)),
                        new Vector3(0f, 30f * x * z, 0f), new Vector3(0.42f, 0.3f, 0.38f));
                }
            }
        }

        // A roof slope spans one foundation (2 m) and rises as much - 45 degrees, so two slopes
        // over a 4 m house meet in a ridge 2 m above the wall tops - and runs on past the wall.
        private const float RoofRun = 2f;
        private const float RoofOverhang = 0.5f;
        private const float RoofWidth = 2f;

        /// <summary>Тёсовая кровля, one slope. The root sits on the eave wall's top centre line
        /// (its snap points EaveL/EaveR take a wall's top corners); the slope rises to the ridge
        /// at -Z (RidgeL/RidgeR, turned 180 so the opposite slope snaps on facing the other way)
        /// and runs 0.5 m down past the wall to +Z, so the boards lie on the top log and hang
        /// over it. Boards in two overlapping layers, purlins under them, a carved valance
        /// (подзор) at the eave; the collider is a board along the slope, ridge to wall line.</summary>
        private static void BuildRoof(Transform root)
        {
            float slopeLength = RoofRun * Mathf.Sqrt(2f);
            Vector3 down = new Vector3(0f, -1f, 1f).normalized;
            Vector3 normal = new Vector3(0f, 1f, 1f).normalized;
            Quaternion tilt = Quaternion.Euler(45f, 0f, 0f);

            // Along the slope: 0 at the wall line, -slopeLength at the ridge (a little past it,
            // under the opposite slope's boards), +RoofOverhang at the eave.
            float from = -slopeLength - 0.04f;
            float middle = (from + RoofOverhang) * 0.5f;
            float length = RoofOverhang - from;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            var slope = new GameObject("Slope").transform;
            slope.SetParent(visual, false);
            slope.localPosition = down * middle + normal * 0.08f;
            slope.localRotation = tilt;
            const int boards = 8;
            float width = RoofWidth / boards;
            for (int i = 0; i < boards; i++)
            {
                float x = -RoofWidth * 0.5f + width * (i + 0.5f);
                // The upper layer's boards overlap the lower ones' edges - no light between them.
                Part(slope, PrimitiveType.Cube, $"Board{i}", i % 2 == 0 ? _roof : _plank, new Vector3(x, i % 2 == 0 ? 0f : 0.04f, 0f), Vector3.zero, new Vector3(width + (i % 2 == 0 ? 0.02f : 0.06f), 0.06f, length));
            }

            foreach (float s in new[] { -0.8f, -2.1f })
            {
                Part(slope, PrimitiveType.Cube, "Purlin", _log, new Vector3(0f, -0.09f, s - middle), Vector3.zero, new Vector3(RoofWidth, 0.1f, 0.12f));
            }

            // The valance hangs straight down from the eave, with diamond teeth.
            Vector3 eave = down * RoofOverhang + normal * 0.05f;
            Part(visual, PrimitiveType.Cube, "Valance", _carving, eave + new Vector3(0f, -0.1f, 0.02f), Vector3.zero, new Vector3(RoofWidth, 0.16f, 0.03f));
            const int teeth = 8;
            for (int i = 0; i < teeth; i++)
            {
                float x = -RoofWidth * 0.5f + RoofWidth / teeth * (i + 0.5f);
                Part(visual, PrimitiveType.Cube, "ValanceTooth", _carving, eave + new Vector3(x, -0.2f, 0.02f), new Vector3(0f, 0f, 45f), new Vector3(0.09f, 0.09f, 0.03f));
            }

            // The collider runs from the ridge to the wall line only: the overhang drops below
            // head height in front of the wall (it blocked the player at every door under an
            // eave), so it stays visual.
            var solid = new GameObject("Solid").transform;
            solid.SetParent(root, false);
            solid.localPosition = down * (-slopeLength * 0.5f) + normal * 0.05f;
            solid.localRotation = tilt;
            solid.gameObject.AddComponent<BoxCollider>().size = new Vector3(RoofWidth, 0.1f, slopeLength);

            Snap(root, "EaveL", new Vector3(-RoofWidth * 0.5f, 0f, 0f), 0f);
            Snap(root, "EaveR", new Vector3(RoofWidth * 0.5f, 0f, 0f), 0f);
            Snap(root, "RidgeL", new Vector3(-RoofWidth * 0.5f, RoofRun, -RoofRun), 180f);
            Snap(root, "RidgeR", new Vector3(RoofWidth * 0.5f, RoofRun, -RoofRun), 180f);
        }

        /// <summary>Фронтон: a right triangle of logs closing the end wall under one roof slope -
        /// 2 m along the wall, 2 m high at -X (under the ridge) down to nothing at +X (the eave
        /// corner), each log cut at 45 degrees flush with the roof's underside. Sits on a wall's
        /// top like a wall on a wall; two of them (the second turned with R to rise the other
        /// way) close a 4 m house's end. Collider: a matching triangular prism.</summary>
        private static void BuildGable(Transform root)
        {
            const int logs = 5;
            const float diameter = 0.4f;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            var body = new GameObject("Logs");
            body.transform.SetParent(visual, false);
            body.AddComponent<MeshFilter>().sharedMesh = SaveMesh(BuildGableLogs(logs, diameter * 0.5f, diameter * 0.5f * 0.8f), GableLogsMeshPath);
            body.AddComponent<MeshRenderer>().sharedMaterials = new[] { _log, _logEnd };
            for (int i = 1; i < logs; i++)
            {
                // Moss caulking in the seam below log i, ending under the roof line.
                float y = diameter * i;
                float end = RoofWidth * 0.5f - y;
                Part(visual, PrimitiveType.Cube, "Moss", _moss, new Vector3((end - RoofWidth * 0.5f) * 0.5f, y, 0f), Vector3.zero, new Vector3(end + RoofWidth * 0.5f, 0.05f, 0.24f));
            }

            var solid = new GameObject("Solid");
            solid.transform.SetParent(root, false);
            var collider = solid.AddComponent<MeshCollider>();
            collider.sharedMesh = SaveMesh(BuildGableSolid(0.1f), GableSolidMeshPath);
            collider.convex = true;

            Snap(root, "BaseL", new Vector3(-RoofWidth * 0.5f, 0f, 0f), 0f);
            Snap(root, "BaseR", new Vector3(RoofWidth * 0.5f, 0f, 0f), 0f);
            Snap(root, "Top", new Vector3(-RoofWidth * 0.5f, RoofRun, 0f), 0f);
        }

        /// <summary>The gable's logs along X: octagonal, from x = -1 (the ridge side) to the roof
        /// line x = 1 - y, every vertex cut by that 45-degree plane. Submesh 0 the bark, 1 the
        /// cut ends.</summary>
        private static Mesh BuildGableLogs(int logs, float radius, float depthRadius)
        {
            const int sides = 8;
            float left = -RoofWidth * 0.5f;
            var vertices = new List<Vector3>();
            var bark = new List<int>();
            var ends = new List<int>();
            for (int i = 0; i < logs; i++)
            {
                float centerY = radius * (2 * i + 1);
                var axis = new Vector3(0f, centerY, 0f);
                int ring = vertices.Count;
                for (int k = 0; k < sides; k++)
                {
                    float angle = Mathf.PI * 2f * k / sides;
                    float y = centerY + Mathf.Sin(angle) * radius;
                    float z = Mathf.Cos(angle) * depthRadius;
                    vertices.Add(new Vector3(left, y, z));
                    vertices.Add(new Vector3(Mathf.Max(left, RoofWidth * 0.5f - y), y, z));
                }

                for (int k = 0; k < sides; k++)
                {
                    int a = ring + k * 2;
                    int b = ring + (k + 1) % sides * 2;
                    Vector3 outward = (vertices[a] + vertices[b]) * 0.5f - axis;
                    outward.x = 0f;
                    Tri(bark, vertices, a, b, b + 1, outward);
                    Tri(bark, vertices, a, b + 1, a + 1, outward);
                }

                // Caps: the square end at the ridge side, the 45-degree cut at the roof line.
                foreach (int side in new[] { 0, 1 })
                {
                    int center = vertices.Count;
                    Vector3 sum = Vector3.zero;
                    for (int k = 0; k < sides; k++)
                    {
                        sum += vertices[ring + k * 2 + side];
                    }

                    vertices.Add(sum / sides);
                    int first = vertices.Count;
                    for (int k = 0; k < sides; k++)
                    {
                        vertices.Add(vertices[ring + k * 2 + side]);
                    }

                    Vector3 outward = side == 0 ? Vector3.left : new Vector3(1f, 1f, 0f);
                    for (int k = 0; k < sides; k++)
                    {
                        Tri(ends, vertices, center, first + k, first + (k + 1) % sides, outward);
                    }
                }
            }

            var mesh = new Mesh { name = "GableLogs", subMeshCount = 2 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(bark, 0);
            mesh.SetTriangles(ends, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The gable's collider: the triangle (-1,0), (1,0), (-1,2) as a prism.</summary>
        private static Mesh BuildGableSolid(float halfDepth)
        {
            float left = -RoofWidth * 0.5f;
            float right = RoofWidth * 0.5f;
            var vertices = new List<Vector3>();
            foreach (float z in new[] { -halfDepth, halfDepth })
            {
                vertices.Add(new Vector3(left, 0f, z));
                vertices.Add(new Vector3(right, 0f, z));
                vertices.Add(new Vector3(left, RoofRun, z));
            }

            var triangles = new List<int>();
            Vector3 middle = new Vector3(left + RoofWidth / 3f, RoofRun / 3f, 0f);
            void Face(params int[] corners)
            {
                Vector3 center = Vector3.zero;
                foreach (int corner in corners)
                {
                    center += vertices[corner];
                }

                Vector3 outward = center / corners.Length - middle;
                for (int k = 1; k < corners.Length - 1; k++)
                {
                    Tri(triangles, vertices, corners[0], corners[k], corners[k + 1], outward);
                }
            }

            Face(0, 1, 2);
            Face(3, 4, 5);
            Face(0, 1, 4, 3);
            Face(1, 2, 5, 4);
            Face(2, 0, 3, 5);

            var mesh = new Mesh { name = "GableSolid" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Adds triangle a-b-c facing outward (Unity's front face: Cross(b-a, c-a)).</summary>
        private static void Tri(List<int> triangles, List<Vector3> vertices, int a, int b, int c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]), outward) < 0f)
            {
                (b, c) = (c, b);
            }

            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        /// <summary>Copies a built mesh into its asset (created once - same reference after).</summary>
        private static Mesh SaveMesh(Mesh built, string path)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.Clear();
            mesh.name = built.name;
            mesh.subMeshCount = built.subMeshCount;
            mesh.SetVertices(built.vertices);
            for (int i = 0; i < built.subMeshCount; i++)
            {
                mesh.SetTriangles(built.GetTriangles(i), i);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void Snap(Transform root, string name, Vector3 position, float yaw)
        {
            var point = new GameObject(name);
            point.transform.SetParent(root, false);
            point.transform.localPosition = position;
            point.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
            point.AddComponent<BuildingSnapPoint>();
        }

        private static BuildingPieceDefinition GableDefinition()
        {
            var definition = AssetDatabase.LoadAssetAtPath<BuildingPieceDefinition>(GableDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BuildingPieceDefinition>();
                AssetDatabase.CreateAsset(definition, GableDefinitionPath);
            }

            return definition;
        }

        /// <summary>Wood Gable: 3 wood like a wall (the roof's own material, read from its cost),
        /// needs a workbench, directional; registered in the piece database and the player's
        /// piece list (scene saved).</summary>
        private static void FillGableDefinition(BuildingPieceDefinition gable)
        {
            var roof = AssetDatabase.LoadAssetAtPath<BuildingPieceDefinition>($"{PieceFolder}WoodRoofPiece.asset");
            var serialized = new SerializedObject(gable);
            serialized.FindProperty("_id").stringValue = "WoodGablePiece";
            serialized.FindProperty("_displayName").stringValue = "Wood Gable";
            serialized.FindProperty("_prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>($"{BuildPrefabs}WoodGable.prefab");
            serialized.FindProperty("_icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/NAV/Art/Icons/Icon_WoodGable.png");
            serialized.FindProperty("_requiresWorkbench").boolValue = true;
            serialized.FindProperty("_directional").boolValue = true;
            SerializedProperty cost = serialized.FindProperty("_cost");
            cost.arraySize = 1;
            cost.GetArrayElementAtIndex(0).FindPropertyRelative("_item").objectReferenceValue = roof.Cost[0].Item;
            cost.GetArrayElementAtIndex(0).FindPropertyRelative("_amount").intValue = 3;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gable);

            CreatureLootSetup.AddToList(new SerializedObject(AssetDatabase.LoadAssetAtPath<BuildingPieceDatabase>("Assets/NAV/ScriptableObjects/Core/BuildingPieceDatabase.asset")), "_pieces", gable);
            PlayerBuilding building = Object.FindAnyObjectByType<PlayerBuilding>(FindObjectsInactive.Include);
            if (building == null)
            {
                Debug.LogWarning("RusBuildingStyleSetup: no PlayerBuilding in the open scene - add Wood Gable to its Known Pieces by hand.");
                return;
            }

            CreatureLootSetup.AddToList(new SerializedObject(building), "_knownPieces", gable);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(building.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(building.gameObject.scene);
        }

        private static void SetDirectional(string definitionName)
        {
            var definition = AssetDatabase.LoadAssetAtPath<BuildingPieceDefinition>($"{PieceFolder}{definitionName}.asset");
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_directional").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        /// <summary>Частокол: a log sharpened at the top, standing in its old 2 m slot.</summary>
        private static void BuildPalisade(Transform visual, Vector3 size)
        {
            const float radius = 0.13f;
            float tip = 0.35f;
            float body = size.y - tip;
            Part(visual, PrimitiveType.Cylinder, "Log", _log, new Vector3(0f, -size.y * 0.5f + body * 0.5f, 0f), Vector3.zero, new Vector3(radius * 2f, body * 0.5f, radius * 2f));
            var point = new GameObject("Tip");
            point.transform.SetParent(visual, false);
            point.transform.localPosition = new Vector3(0f, -size.y * 0.5f + body, 0f);
            point.transform.localScale = new Vector3(radius * 2f, tip, radius * 2f);
            point.AddComponent<MeshFilter>().sharedMesh = _cone;
            point.AddComponent<MeshRenderer>().sharedMaterial = _logEnd;
        }

        // ---- Parts ----

        internal static void LogAlongX(Transform parent, string name, Vector3 center, float length, float radius, float depthSquash)
        {
            // Slightly oval logs: the walls' colliders are only 0.2 m thick.
            Part(parent, PrimitiveType.Cylinder, name, _log, center, new Vector3(0f, 0f, 90f), new Vector3(radius * 2f, length * 0.5f, radius * 2f * depthSquash));
            foreach (float side in new[] { -1f, 1f })
            {
                Part(parent, PrimitiveType.Cylinder, name + "End", _logEnd, center + new Vector3(side * length * 0.5f, 0f, 0f), new Vector3(0f, 0f, 90f),
                    new Vector3(radius * 1.9f, 0.006f, radius * 1.9f * depthSquash));
            }
        }

        private static void LogAlongZ(Transform parent, string name, Vector3 center, float length, float radius)
        {
            Part(parent, PrimitiveType.Cylinder, name, _log, center, new Vector3(90f, 0f, 0f), new Vector3(radius * 2f, length * 0.5f, radius * 2f));
        }

        /// <summary>A visual-only primitive (its collider removed).</summary>
        internal static GameObject Part(Transform parent, PrimitiveType type, string name, Material material, Vector3 position, Vector3 euler, Vector3 scale)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localEulerAngles = euler;
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        /// <summary>A unit cone (radius 0.5, height 1, base at y 0) for the palisade tips.</summary>
        private static Mesh BuildConeMesh()
        {
            const int segments = 12;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * 2f * i / segments;
                float a1 = Mathf.PI * 2f * (i + 1) / segments;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
                int start = vertices.Count;
                vertices.Add(p0);
                vertices.Add(Vector3.up);
                vertices.Add(p1);
                // Clockwise seen from outside (Unity's front face).
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ConeMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "LogTip" };
                AssetDatabase.CreateAsset(mesh, ConeMeshPath);
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void RenderPieceIcon(string id)
        {
            string iconPath = $"Assets/NAV/Art/Icons/Icon_{id}.png";
            System.IO.File.WriteAllBytes(iconPath, ItemIconRenderer.RenderIcon(AssetDatabase.LoadAssetAtPath<GameObject>($"{BuildPrefabs}{id}.prefab"),
                Quaternion.Euler(-22f, 0f, 0f) * Quaternion.Euler(0f, 145f, 0f)));
            AssetDatabase.ImportAsset(iconPath);
            ItemIconRenderer.ConfigureSprite(iconPath);
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
    }
}
