using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using NAV.Gameplay.Items;
using NAV.World.Biomes;
using NAV.World.Generation;

namespace NAV.Editor
{
    /// <summary>
    /// Early visuals - forest and vegetation (roadmap Phase 9 "Trees and vegetation in the
    /// forest; Birch grove"):
    ///   - three placeholder birch trees, generated here (no birch model in the project yet):
    ///     a slightly bent white trunk with black lenticels (procedural bark texture), a few
    ///     branches and a faceted light-green crown; each is a gatherable tree (Birch_*_WoodNode,
    ///     definition BirchWoodNode - a copy of the spruce's TreeWoodNode, so it fells the same
    ///     way). Swap the Model child for a real birch later;
    ///   - grass and flower terrain-detail prefabs (Ultimate Nature meshes, MeshFilter on the
    ///     root as terrain details need);
    ///   - the Birch Grove biome (its own grassy layer, birches, a few spruces, deer) that
    ///     WorldGenerator places inside forests by a grove noise;
    ///   - decor lists and grass/flower densities for Forest, Plains, Ocean and the grove.
    /// Re-run to rebuild (assets keep their GUIDs).
    /// </summary>
    internal static class VegetationSetup
    {
        private const string Uns = "Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/";
        private const string PrefabFolder = "Assets/NAV/Prefabs/World/Vegetation";
        private const string ModelFolder = "Assets/NAV/Art/Models/Birch";
        private const string MaterialFolder = "Assets/NAV/Materials/Vegetation";
        private const string BiomeFolder = "Assets/NAV/ScriptableObjects/Biomes";
        private const string TreeDefinitionPath = "Assets/NAV/Scripts/Gameplay/Items/TreeWoodNode.asset";
        private const string BirchDefinitionPath = "Assets/NAV/ScriptableObjects/World/BirchWoodNode.asset";
        private const string GroveLayerPath = "Assets/NAV/Art/Terrain/GroveLayer.terrainlayer";
        private const string PlainsLayerPath = "Assets/NAV/Art/Terrain/PlainsLayer.terrainlayer";
        private const string ForestLayerPath = "Assets/NAV/Art/Terrain/ForestLayer.terrainlayer";
        private const string Nsk = "Assets/NatureStarterKit2/";
        private const string HandPainted = "Assets/Silver_Cats/Hand_Painted_Nature_Kit_LITE/Prefabs/";

        [MenuItem("NAV/World/Build Vegetation and Birch Grove")]
        private static void Build()
        {
            foreach (string folder in new[] { PrefabFolder, ModelFolder, MaterialFolder, "Assets/NAV/ScriptableObjects/World" })
            {
                EnsureFolder(folder);
            }

            Material bark = GetOrCreateMaterial("BirchBark", Color.white, 0.15f, BuildBarkTexture());
            Material leaves = GetOrCreateMaterial("BirchLeaves", new Color(0.42f, 0.6f, 0.22f), 0.1f, null);
            ResourceNodeDefinition birchDefinition = GetOrCreateBirchDefinition();

            var birches = new List<GameObject>();
            string[] variants = { "A", "B", "C" };
            for (int i = 0; i < variants.Length; i++)
            {
                Mesh mesh = BuildBirchMesh(variants[i], 1000 + i * 77, out float height);
                birches.Add(BuildBirchPrefab(variants[i], mesh, height, bark, leaves, birchDefinition));
            }

            Material flowerMaterial = GetOrCreateFlowerMaterial();
            GameObject chamomile = BuildFlowerPrefab("ChamomileDetail", flowerMaterial, false, 31);
            GameObject cornflower = BuildFlowerPrefab("CornflowerDetail", flowerMaterial, true, 57);

            // Nature Starter Kit 2 (added by the developer for bushes, grass etc.): its Tree
            // Creator materials don't render in URP, so each prefab is rebuilt with URP Lit.
            var nskBushes = new List<GameObject>();
            // bush01 is skipped: its leaves vanish under alpha clipping, only twigs remain.
            for (int i = 2; i <= 6; i++)
            {
                nskBushes.Add(ConvertNsk($"bush0{i}", null));
            }

            var treeDefinition = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(TreeDefinitionPath);
            var nskTrees = new List<GameObject>();
            for (int i = 1; i <= 4; i++)
            {
                nskTrees.Add(ConvertNsk($"tree0{i}", treeDefinition));
            }

            // Hand Painted Nature Kit Lite (added by the developer): already URP Lit with LOD
            // billboards. Its trees are huge (pine 28 m), so they're scaled down and wrapped as
            // gatherable trees; the small plants are used as they are.
            // Pine_Tree is not used: the Lite atlas has no texture where its needles map (that
            // part is in the paid version), so it renders as a bare black pole.
            GameObject hpLarch = WrapHandPaintedTree("Larch_Tree", 0.7f, treeDefinition);
            GameObject hpCedar = WrapHandPaintedTree("Cedar_Tree_03", 0.7f, treeDefinition);
            GameObject fern = Load(HandPainted + "Fern.prefab");
            GameObject boletus = Load(HandPainted + "Boletus_Edulis.prefab");
            GameObject dandelion = Load(HandPainted + "Dandelion_02.prefab");
            GameObject plantain = Load(HandPainted + "Plantain.prefab");
            GameObject hpStump = Load(HandPainted + "Stump.prefab");

            GameObject mushrooms = Load(Uns + "Vegetation/Mushrooms/Prefabs/UNS_Mushroom_Patch.prefab");
            GameObject stump = Load(Uns + "Props/Logs/Prefabs/UNS_Stump.prefab");
            GameObject log = Load(Uns + "Props/Logs/Prefabs/UNS_Log.prefab");
            GameObject pebble = Load(Uns + "Rocks/River/Prefabs/UNS_Tiny_Rock_01.prefab");
            GameObject spruce = Load("Assets/NAV/Prefabs/World/UNS_Spruce_WoodNode.prefab");
            GameObject looseBranch = Load("Assets/NAV/Prefabs/World/LooseBranch.prefab");
            GameObject looseStone = Load("Assets/NAV/Prefabs/World/LooseStone.prefab");
            GameObject deer = Load("Assets/NAV/Prefabs/Creatures/Deer.prefab");

            var forest = new SerializedObject(LoadBiome("ForestBiome"));
            // Mostly spruce, with larches, cedars and the odd broadleaf tree.
            SetResources(forest,
                (spruce, 0.27f, 9f, 0.6f, 1.35f),
                (hpLarch, 0.045f, 16f, 0.65f, 1.3f),
                (hpCedar, 0.035f, 18f, 0.65f, 1.3f),
                (nskTrees[0], 0.025f, 16f, 0.7f, 1.3f),
                (nskTrees[1], 0.025f, 16f, 0.7f, 1.3f),
                (nskTrees[2], 0.025f, 16f, 0.7f, 1.3f),
                (nskTrees[3], 0.025f, 16f, 0.7f, 1.3f),
                (looseBranch, 0.12f, 14f, 1f, 1f),
                (looseStone, 0.04f, 18f, 1f, 1f));
            SetDecor(forest, 0.45f, 0.02f,
                (nskBushes[0], 0.08f, 10f, 0.7f, 1.1f, false),
                (nskBushes[1], 0.08f, 10f, 0.7f, 1.1f, false),
                (nskBushes[2], 0.06f, 12f, 0.7f, 1.1f, false),
                (nskBushes[3], 0.06f, 12f, 0.7f, 1.1f, false),
                (fern, 0.3f, 6f, 0.9f, 1.6f, false),
                (mushrooms, 0.1f, 10f, 0.8f, 1.3f, false),
                (boletus, 0.06f, 12f, 0.8f, 1.3f, false),
                (pebble, 0.08f, 14f, 0.6f, 1.4f, false),
                (stump, 0.04f, 26f, 0.8f, 1.1f, true),
                (hpStump, 0.03f, 28f, 0.5f, 0.7f, true),
                (log, 0.04f, 26f, 0.8f, 1.2f, true));
            forest.FindProperty("_terrainLayer").objectReferenceValue = GetOrCreateForestLayer();
            forest.ApplyModifiedPropertiesWithoutUndo();

            var plains = new SerializedObject(LoadBiome("PlainsBiome"));
            SetDecor(plains, 0.7f, 0.07f,
                (nskBushes[3], 0.03f, 18f, 0.6f, 1f, false),
                (nskBushes[4], 0.03f, 18f, 0.6f, 1f, false),
                (dandelion, 0.14f, 7f, 0.9f, 1.5f, false),
                (plantain, 0.1f, 8f, 0.9f, 1.4f, false),
                (pebble, 0.04f, 16f, 0.6f, 1.2f, false));

            var ocean = new SerializedObject(LoadBiome("OceanBiome"));
            SetDecor(ocean, 0f, 0f,
                (pebble, 0.04f, 14f, 0.6f, 1.5f, false));

            BiomeDefinition grove = GetOrCreateGrove();
            var groveSerialized = new SerializedObject(grove);
            groveSerialized.FindProperty("_displayName").stringValue = "Birch Grove";
            groveSerialized.FindProperty("_terrainLayer").objectReferenceValue = GetOrCreateGroveLayer();
            groveSerialized.FindProperty("_heightVariation").floatValue = 0.45f;
            SetResources(groveSerialized,
                (birches[0], 0.16f, 8f, 0.65f, 1.3f),
                (birches[1], 0.16f, 8f, 0.65f, 1.3f),
                (birches[2], 0.16f, 8f, 0.65f, 1.3f),
                (spruce, 0.05f, 18f, 0.6f, 1.3f),
                (looseBranch, 0.1f, 14f, 1f, 1f));
            SetDecor(groveSerialized, 0.85f, 0.14f,
                (nskBushes[0], 0.05f, 14f, 0.6f, 1f, false),
                (nskBushes[4], 0.05f, 14f, 0.6f, 1f, false),
                (fern, 0.12f, 9f, 0.8f, 1.3f, false),
                (dandelion, 0.1f, 8f, 0.9f, 1.4f, false),
                (plantain, 0.06f, 10f, 0.9f, 1.3f, false),
                // Ceps under birches - a proper Russian birch wood.
                (boletus, 0.1f, 10f, 0.8f, 1.4f, false),
                (mushrooms, 0.06f, 12f, 0.8f, 1.2f, false),
                (log, 0.03f, 30f, 0.8f, 1.1f, true));
            SetDeer(groveSerialized, deer);

            WorldGenerationSettings settings = Object.FindAnyObjectByType<WorldGenerator>() != null
                ? GetSettings(Object.FindAnyObjectByType<WorldGenerator>())
                : null;
            if (settings == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:WorldGenerationSettings");
                settings = guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<WorldGenerationSettings>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
            }

            if (settings != null)
            {
                var serialized = new SerializedObject(settings);
                serialized.FindProperty("_groveBiome").objectReferenceValue = grove;
                // URP's terrain grass barely applies the healthy/dry tint to these white blade
                // masks (they came out straw-beige), so the colour is baked into green copies and
                // the tint only adds a slight variation.
                serialized.FindProperty("_grassHealthyColor").colorValue = Color.white;
                serialized.FindProperty("_grassDryColor").colorValue = new Color(0.92f, 0.95f, 0.8f);
                SerializedProperty grass = serialized.FindProperty("_grassTextures");
                grass.arraySize = 2;
                grass.GetArrayElementAtIndex(0).objectReferenceValue = BuildGreenGrass("grass01", new Color(0.36f, 0.56f, 0.2f));
                grass.GetArrayElementAtIndex(1).objectReferenceValue = BuildGreenGrass("grass02", new Color(0.46f, 0.6f, 0.24f));
                SerializedProperty flowers = serialized.FindProperty("_flowerDetailPrefabs");
                flowers.arraySize = 2;
                flowers.GetArrayElementAtIndex(0).objectReferenceValue = chamomile;
                flowers.GetArrayElementAtIndex(1).objectReferenceValue = cornflower;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"VegetationSetup: built 3 birches, grass/flower details, Birch Grove biome and decor lists{(settings != null ? $", wired into '{settings.name}'" : " (no WorldGenerationSettings found)")}.");
        }

        // ---- Birch model ----

        /// <summary>One mesh, two submeshes (0 bark, 1 leaves). Trunk/branches smooth, crown
        /// faceted (one normal per triangle) for the low-poly look.</summary>
        private static Mesh BuildBirchMesh(string variant, int seed, out float height)
        {
            var rng = new System.Random(seed);
            float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);

            height = Rand(9.5f, 11.5f);
            float trunkHeight = height;
            Vector3 bendDirection = Quaternion.Euler(0f, Rand(0f, 360f), 0f) * Vector3.forward;
            float bend = Rand(0.25f, 0.6f);
            Vector3 TrunkCentre(float t) => bendDirection * (bend * t * t) + Vector3.up * (t * trunkHeight);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var bark = new List<int>();
            var leaves = new List<int>();

            // Trunk: uv.v runs 0..1 over the whole height (the bark texture's dark base is at v = 0).
            AddTube(vertices, normals, uvs, bark, 12, 10, t => TrunkCentre(t), t => Mathf.Lerp(0.2f, 0.045f, t), 0f, 1f);

            // Branches spiral up the upper trunk; each ends in a clump of leaves, so the crown is
            // an airy oval of clumps rather than one ball.
            int branchCount = rng.Next(7, 10);
            float yaw = Rand(0f, 360f);
            for (int i = 0; i < branchCount; i++)
            {
                float t = Mathf.Lerp(0.4f, 0.85f, (i + Rand(0f, 0.6f)) / branchCount);
                yaw += Rand(110f, 160f);
                Vector3 start = TrunkCentre(t);
                Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                direction = (direction + Vector3.up * Rand(0.5f, 1f)).normalized;
                // Longest branches in the middle of the crown.
                float length = Mathf.Lerp(2.2f, 1.1f, Mathf.Abs(t - 0.58f) / 0.3f) * Rand(0.85f, 1.15f);
                AddTube(vertices, normals, uvs, bark, 6, 2, s => start + direction * (s * length), s => Mathf.Lerp(0.05f, 0.015f, s), t, t + 0.05f);

                float size = Rand(1.7f, 2.4f);
                // Hanging a little below the tip - birch twigs droop.
                AddBlob(vertices, normals, uvs, leaves, start + direction * length + Vector3.down * 0.3f, new Vector3(size, size * 1.2f, size), rng);
            }

            int topClumps = rng.Next(3, 5);
            for (int i = 0; i < topClumps; i++)
            {
                float t = Mathf.Lerp(0.82f, 1f, (float)i / topClumps);
                Vector3 offset = Quaternion.Euler(0f, Rand(0f, 360f), 0f) * Vector3.forward * Rand(0.1f, 0.6f);
                float size = Mathf.Lerp(1.8f, 1.1f, (float)i / topClumps);
                AddBlob(vertices, normals, uvs, leaves, TrunkCentre(Mathf.Min(t, 0.98f)) + offset, new Vector3(size, size * 1.3f, size), rng);
            }

            string path = $"{ModelFolder}/Birch_{variant}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.Clear();
            mesh.name = $"Birch_{variant}";
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(bark, 0);
            mesh.SetTriangles(leaves, 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void AddTube(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
            int sides, int rings, System.Func<float, Vector3> centre, System.Func<float, float> radius, float vStart, float vEnd)
        {
            int start = vertices.Count;
            for (int r = 0; r <= rings; r++)
            {
                float t = (float)r / rings;
                Vector3 c = centre(t);
                Vector3 axis = (centre(Mathf.Min(1f, t + 0.01f)) - centre(Mathf.Max(0f, t - 0.01f))).normalized;
                Vector3 side = Vector3.Cross(axis, Vector3.forward).sqrMagnitude > 0.01f ? Vector3.Cross(axis, Vector3.forward).normalized : Vector3.right;
                Vector3 other = Vector3.Cross(axis, side);
                for (int s = 0; s <= sides; s++)
                {
                    float angle = s * Mathf.PI * 2f / sides;
                    Vector3 normal = side * Mathf.Cos(angle) + other * Mathf.Sin(angle);
                    vertices.Add(c + normal * radius(t));
                    normals.Add(normal);
                    uvs.Add(new Vector2((float)s / sides, Mathf.Lerp(vStart, vEnd, t)));
                }
            }

            int stride = sides + 1;

            // Unity draws clockwise front faces: pick the winding whose face normal agrees with
            // the outward vertex normal, or the bark is culled from outside and the trunk looks
            // like a hollow half-pipe.
            Vector3 v0 = vertices[start];
            Vector3 v1 = vertices[start + 1];
            Vector3 v2 = vertices[start + stride];
            bool flip = Vector3.Dot(Vector3.Cross(v2 - v0, v1 - v0), normals[start]) < 0f;

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = start + r * stride + s;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    triangles.AddRange(flip ? new[] { a, b, c, b, d, c } : new[] { a, c, b, b, c, d });
                }
            }
        }

        /// <summary>A lumpy icosphere (one subdivision), flat-shaded.</summary>
        private static void AddBlob(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
            Vector3 centre, Vector3 size, System.Random rng)
        {
            Icosphere(out List<Vector3> points, out List<int> faces);
            for (int i = 0; i < points.Count; i++)
            {
                float lump = 0.8f + (float)rng.NextDouble() * 0.4f;
                points[i] = Vector3.Scale(points[i] * lump, size * 0.5f) + centre;
            }

            for (int i = 0; i < faces.Count; i += 3)
            {
                Vector3 a = points[faces[i]];
                Vector3 b = points[faces[i + 1]];
                Vector3 c = points[faces[i + 2]];
                // Unity draws clockwise front faces: wind every face so it faces away from the
                // clump's centre, or the crown is culled from outside and looks hollow.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - centre) < 0f)
                {
                    (b, c) = (c, b);
                }

                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                int index = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);
                uvs.Add(Vector2.zero);
                uvs.Add(Vector2.zero);
                uvs.Add(Vector2.zero);
                triangles.AddRange(new[] { index, index + 1, index + 2 });
            }
        }

        private static void Icosphere(out List<Vector3> points, out List<int> faces)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            points = new List<Vector3>
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };
            for (int i = 0; i < points.Count; i++)
            {
                points[i] = points[i].normalized;
            }

            var baseFaces = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            faces = new List<int>();
            var midpoints = new Dictionary<long, int>();
            List<Vector3> pts = points;
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
                if (!midpoints.TryGetValue(key, out int index))
                {
                    index = pts.Count;
                    pts.Add(((pts[a] + pts[b]) * 0.5f).normalized);
                    midpoints[key] = index;
                }

                return index;
            }

            for (int i = 0; i < baseFaces.Count; i += 3)
            {
                int a = baseFaces[i], b = baseFaces[i + 1], c = baseFaces[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                faces.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
        }

        /// <summary>Creamy white bark with short black horizontal lenticels, darker and rougher
        /// towards the base (v = 0), as on real birches.</summary>
        private static Texture2D BuildBarkTexture()
        {
            const int Width = 128;
            const int Height = 1024;
            string path = $"{ModelFolder}/BirchBark.png";
            var rng = new System.Random(4242);
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var pixels = new Color[Width * Height];

            var white = new Color(0.9f, 0.89f, 0.84f);
            var dark = new Color(0.13f, 0.12f, 0.11f);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float grain = Mathf.PerlinNoise(x * 0.08f, y * 0.01f) * 0.08f - 0.04f;
                    pixels[y * Width + x] = white + new Color(grain, grain, grain);
                }
            }

            // Lenticels: short, wrapped horizontal dashes.
            for (int i = 0; i < 260; i++)
            {
                int cx = rng.Next(Width);
                int cy = rng.Next(Height);
                int length = rng.Next(8, 40);
                int thickness = rng.Next(2, 5);
                for (int dy = 0; dy < thickness; dy++)
                {
                    for (int dx = 0; dx < length; dx++)
                    {
                        int x = (cx + dx) % Width;
                        int y = Mathf.Clamp(cy + dy, 0, Height - 1);
                        pixels[y * Width + x] = Color.Lerp(pixels[y * Width + x], dark, 0.85f);
                    }
                }
            }

            // Dark, cracked base.
            for (int y = 0; y < Height / 9; y++)
            {
                float fade = 1f - (float)y / (Height / 9);
                for (int x = 0; x < Width; x++)
                {
                    float crack = Mathf.PerlinNoise(x * 0.15f, y * 0.04f);
                    pixels[y * Width + x] = Color.Lerp(pixels[y * Width + x], dark * (0.8f + crack * 0.6f), Mathf.Clamp01(fade * 1.4f * crack + fade * 0.3f));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static GameObject BuildBirchPrefab(string variant, Mesh mesh, float height, Material bark, Material leaves, ResourceNodeDefinition definition)
        {
            var root = new GameObject($"Birch_{variant}_WoodNode");
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            model.AddComponent<MeshRenderer>().sharedMaterials = new[] { bark, leaves };

            var collider = root.AddComponent<CapsuleCollider>();
            collider.radius = 0.2f;
            collider.height = height;
            collider.center = new Vector3(0f, height * 0.5f, 0f);

            var node = new SerializedObject(root.AddComponent<ResourceNode>());
            node.FindProperty("_definition").objectReferenceValue = definition;
            node.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/Birch_{variant}_WoodNode.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static ResourceNodeDefinition GetOrCreateBirchDefinition()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(BirchDefinitionPath);
            if (definition == null)
            {
                AssetDatabase.CopyAsset(TreeDefinitionPath, BirchDefinitionPath);
                definition = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(BirchDefinitionPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_displayName").stringValue = "Birch";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        // ---- Nature Starter Kit 2 ----

        /// <summary>Rebuilds a Nature Starter Kit 2 Tree Creator prefab for URP: its baked mesh
        /// (submesh 0 bark, 1 leaves, both on the prefab's own diffuse atlas) with URP Lit
        /// materials - opaque bark, alpha-clipped double-sided leaves. With a definition it
        /// becomes a gatherable tree (capsule collider copied from the source + ResourceNode);
        /// without one, a walk-through bush.</summary>
        private static GameObject ConvertNsk(string name, ResourceNodeDefinition definition)
        {
            string sourcePath = $"{Nsk}Nature/{name}.prefab";
            Mesh mesh = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(sourcePath))
            {
                if (asset is Mesh found)
                {
                    mesh = found;
                    break;
                }
            }

            if (mesh == null)
            {
                Debug.LogError($"VegetationSetup: no mesh in '{sourcePath}'.");
                return null;
            }

            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Nsk}Nature/{name}_Textures/diffuse.png");
            Material bark = GetOrCreateMaterial($"NSK_{name}_Bark", Color.white, 0.1f, atlas);
            Material leaves = GetOrCreateMaterial($"NSK_{name}_Leaves", Color.white, 0.1f, atlas);
            leaves.SetFloat("_AlphaClip", 1f);
            leaves.SetFloat("_Cutoff", 0.45f);
            leaves.EnableKeyword("_ALPHATEST_ON");
            leaves.SetFloat("_Cull", 0f);
            leaves.doubleSidedGI = true;
            leaves.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;

            var root = new GameObject(definition != null ? $"NSK_{name}_WoodNode" : $"NSK_{name}");
            GameObject model = root;
            if (definition != null)
            {
                model = new GameObject("Model");
                model.transform.SetParent(root.transform, false);

                var source = Load(sourcePath).GetComponent<CapsuleCollider>();
                var collider = root.AddComponent<CapsuleCollider>();
                collider.radius = source != null ? source.radius : 0.25f;
                collider.height = source != null ? source.height : mesh.bounds.size.y;
                collider.center = source != null ? source.center : new Vector3(0f, mesh.bounds.size.y * 0.5f, 0f);

                var node = new SerializedObject(root.AddComponent<ResourceNode>());
                node.FindProperty("_definition").objectReferenceValue = definition;
                node.ApplyModifiedPropertiesWithoutUndo();
            }

            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            model.AddComponent<MeshRenderer>().sharedMaterials = new[] { bark, leaves };

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{root.name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Wraps a Hand Painted Nature Kit tree as a gatherable tree: an unlinked copy
        /// of the prefab (LODs, billboard) scaled down under a root that carries the trunk
        /// collider (the source's, scaled) and a ResourceNode.</summary>
        private static GameObject WrapHandPaintedTree(string name, float scale, ResourceNodeDefinition definition)
        {
            GameObject source = Load(HandPainted + name + ".prefab");
            var root = new GameObject($"HP_{name}_WoodNode");
            GameObject model = Object.Instantiate(source, root.transform);
            model.name = "Model";
            model.transform.localScale = Vector3.one * scale;

            var collider = root.AddComponent<CapsuleCollider>();
            if (model.GetComponentInChildren<Collider>() is CapsuleCollider sourceCapsule)
            {
                collider.radius = sourceCapsule.radius * scale;
                collider.height = sourceCapsule.height * scale;
                collider.center = sourceCapsule.center * scale;
                collider.direction = sourceCapsule.direction;
            }
            else
            {
                collider.radius = 0.3f;
                collider.height = 10f * scale;
                collider.center = new Vector3(0f, collider.height * 0.5f, 0f);
            }

            foreach (Collider modelCollider in model.GetComponentsInChildren<Collider>())
            {
                Object.DestroyImmediate(modelCollider);
            }

            var node = new SerializedObject(root.AddComponent<ResourceNode>());
            node.FindProperty("_definition").objectReferenceValue = definition;
            node.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{root.name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>A green copy of a Nature Starter Kit 2 grass mask (colour = mask * green,
        /// darker towards the root, alpha kept) in the birch model folder.</summary>
        private static Texture2D BuildGreenGrass(string name, Color green)
        {
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Nsk}Textures/{name}.tga");
            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, target);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);

            Color[] pixels = texture.GetPixels();
            for (int y = 0; y < source.height; y++)
            {
                float root = Mathf.Lerp(0.7f, 1.1f, (float)y / source.height);
                for (int x = 0; x < source.width; x++)
                {
                    Color p = pixels[y * source.width + x];
                    pixels[y * source.width + x] = new Color(p.r * green.r * root, p.g * green.g * root, p.b * green.b * root, p.a);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            string path = $"{ModelFolder}/Grass_{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Forest floor: Nature Starter Kit 2's olive ground instead of bare dirt.</summary>
        private static TerrainLayer GetOrCreateForestLayer()
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(ForestLayerPath);
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Nsk}Textures/ground03.tga");
            layer.tileSize = new Vector2(4f, 4f);
            layer.diffuseRemapMax = new Vector4(0.85f, 0.95f, 0.7f, 1f);
            EditorUtility.SetDirty(layer);
            return layer;
        }

        // ---- Flowers ----

        // Palette texels (4 x 1, point-filtered): stem green, white petal, yellow centre, blue petal.
        private const int PaletteStem = 0;
        private const int PaletteWhite = 1;
        private const int PaletteYellow = 2;
        private const int PaletteBlue = 3;

        private static Material GetOrCreateFlowerMaterial()
        {
            string texturePath = $"{ModelFolder}/FlowerPalette.png";
            var palette = new Texture2D(4, 1, TextureFormat.RGB24, false);
            palette.SetPixels(new[]
            {
                new Color(0.3f, 0.5f, 0.18f),
                new Color(0.95f, 0.95f, 0.9f),
                new Color(0.98f, 0.78f, 0.18f),
                new Color(0.28f, 0.42f, 0.95f),
            });
            palette.Apply();
            File.WriteAllBytes(texturePath, palette.EncodeToPNG());
            Object.DestroyImmediate(palette);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            Material material = GetOrCreateMaterial("FlowerPalette", Color.white, 0.1f, AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            // Petals are single quads - visible from below too.
            material.SetFloat("_Cull", 0f);
            material.doubleSidedGI = true;
            return material;
        }

        /// <summary>A little clump of 3-5 flowers (one terrain detail): a chamomile (white
        /// petals, yellow centre) or a cornflower (blue cup of petals) - meadow flowers of the
        /// Russian countryside. One mesh, one material (terrain details allow only one).</summary>
        private static GameObject BuildFlowerPrefab(string name, Material material, bool cornflower, int seed)
        {
            var rng = new System.Random(seed);
            float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            Vector2 Palette(int index) => new((index + 0.5f) / 4f, 0.5f);

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int colour)
            {
                Vector3 normal = Vector3.Cross(b - a, d - a).normalized;
                // Double-sided, so the winding doesn't matter for culling - but a petal lit from
                // below looked dark from above.
                if (normal.y < 0f)
                {
                    normal = -normal;
                }

                int start = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                for (int i = 0; i < 4; i++)
                {
                    normals.Add(normal);
                    uvs.Add(Palette(colour));
                }

                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }

            int count = rng.Next(3, 6);
            for (int f = 0; f < count; f++)
            {
                Vector3 stemBase = f == 0 ? Vector3.zero : Quaternion.Euler(0f, Rand(0f, 360f), 0f) * Vector3.forward * Rand(0.08f, 0.3f);
                float height = Rand(0.16f, 0.28f);
                Vector3 lean = new Vector3(Rand(-0.05f, 0.05f), 0f, Rand(-0.05f, 0.05f));
                Vector3 head = stemBase + Vector3.up * height + lean;

                // Stem: two crossed thin strips, and one small leaf.
                const float StemWidth = 0.007f;
                Quad(stemBase + Vector3.left * StemWidth, head + Vector3.left * StemWidth, head + Vector3.right * StemWidth, stemBase + Vector3.right * StemWidth, PaletteStem);
                Quad(stemBase + Vector3.back * StemWidth, head + Vector3.back * StemWidth, head + Vector3.forward * StemWidth, stemBase + Vector3.forward * StemWidth, PaletteStem);
                Vector3 leafBase = Vector3.Lerp(stemBase, head, 0.35f);
                Vector3 leafDir = Quaternion.Euler(0f, Rand(0f, 360f), 0f) * Vector3.forward;
                Quad(leafBase, leafBase + leafDir * 0.06f + Vector3.up * 0.05f, leafBase + leafDir * 0.12f + Vector3.up * 0.03f, leafBase + leafDir * 0.06f + Vector3.up * 0.01f, PaletteStem);

                int petals = cornflower ? 9 : 10;
                float inner = cornflower ? 0.015f : 0.028f;
                float outer = cornflower ? 0.075f : 0.095f;
                float rise = cornflower ? 0.04f : 0.008f;   // cornflower petals cup upward
                float halfWidth = cornflower ? 0.25f : 0.17f;
                float spin = Rand(0f, Mathf.PI * 2f);
                for (int p = 0; p < petals; p++)
                {
                    float angle = spin + p * Mathf.PI * 2f / petals;
                    Vector3 Dir(float a) => new(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Quad(head + Dir(angle - halfWidth * 0.5f) * inner,
                        head + Dir(angle - halfWidth) * outer + Vector3.up * rise,
                        head + Dir(angle + halfWidth) * outer + Vector3.up * rise,
                        head + Dir(angle + halfWidth * 0.5f) * inner,
                        cornflower ? PaletteBlue : PaletteWhite);
                }

                // Centre: a small raised square, yellow for chamomile, deep blue for cornflower.
                float c = inner * 1.1f;
                Vector3 top = head + Vector3.up * 0.012f;
                Quad(top + new Vector3(-c, 0f, -c), top + new Vector3(-c, 0f, c), top + new Vector3(c, 0f, c), top + new Vector3(c, 0f, -c),
                    cornflower ? PaletteBlue : PaletteYellow);
            }

            string meshPath = $"{ModelFolder}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, meshPath);
            }

            mesh.Clear();
            mesh.name = name;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            var root = new GameObject(name);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---- Biomes ----

        private static BiomeDefinition GetOrCreateGrove()
        {
            string path = $"{BiomeFolder}/BirchGroveBiome.asset";
            var grove = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(path);
            if (grove == null)
            {
                grove = ScriptableObject.CreateInstance<BiomeDefinition>();
                AssetDatabase.CreateAsset(grove, path);
            }

            return grove;
        }

        private static TerrainLayer GetOrCreateGroveLayer()
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(GroveLayerPath);
            if (layer == null)
            {
                AssetDatabase.CopyAsset(PlainsLayerPath, GroveLayerPath);
                layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(GroveLayerPath);
            }

            // Sunnier, yellower than the meadow - a birch grove is light.
            layer.diffuseRemapMax = new Vector4(0.86f, 0.88f, 0.6f, 1f);
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static void SetDecor(SerializedObject biome, float grass, float flowers,
            params (GameObject prefab, float density, float spacing, float minScale, float maxScale, bool keepColliders)[] entries)
        {
            SerializedProperty list = biome.FindProperty("_decorSpawns");
            list.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_prefab").objectReferenceValue = entries[i].prefab;
                entry.FindPropertyRelative("_density").floatValue = entries[i].density;
                entry.FindPropertyRelative("_minSpacing").floatValue = entries[i].spacing;
                entry.FindPropertyRelative("_scale").vector2Value = new Vector2(entries[i].minScale, entries[i].maxScale);
                entry.FindPropertyRelative("_keepColliders").boolValue = entries[i].keepColliders;
            }

            biome.FindProperty("_grassDensity").floatValue = grass;
            biome.FindProperty("_flowerDensity").floatValue = flowers;
            biome.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(biome.targetObject);
        }

        private static void SetResources(SerializedObject biome, params (GameObject prefab, float density, float spacing, float minScale, float maxScale)[] entries)
        {
            SerializedProperty list = biome.FindProperty("_resourceSpawns");
            list.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_prefab").objectReferenceValue = entries[i].prefab;
                entry.FindPropertyRelative("_density").floatValue = entries[i].density;
                entry.FindPropertyRelative("_minSpacing").floatValue = entries[i].spacing;
                entry.FindPropertyRelative("_scale").vector2Value = new Vector2(entries[i].minScale, entries[i].maxScale);
            }

            biome.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetDeer(SerializedObject biome, GameObject deer)
        {
            SerializedProperty list = biome.FindProperty("_creatureSpawns");
            list.arraySize = deer != null ? 1 : 0;
            if (deer != null)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("_prefab").objectReferenceValue = deer;
                entry.FindPropertyRelative("_count").intValue = 3;
                entry.FindPropertyRelative("_nightCount").intValue = 1;
                entry.FindPropertyRelative("_groupSize").vector2IntValue = new Vector2Int(1, 3);
                entry.FindPropertyRelative("_respawnSeconds").floatValue = 150f;
            }

            biome.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(biome.targetObject);
        }

        // ---- Helpers ----

        private static WorldGenerationSettings GetSettings(WorldGenerator generator)
        {
            return new SerializedObject(generator).FindProperty("_settings").objectReferenceValue as WorldGenerationSettings;
        }

        private static BiomeDefinition LoadBiome(string name)
        {
            return AssetDatabase.LoadAssetAtPath<BiomeDefinition>($"{BiomeFolder}/{name}.asset");
        }

        private static GameObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"VegetationSetup: '{path}' not found.");
            }

            return prefab;
        }

        private static Material GetOrCreateMaterial(string name, Color color, float smoothness, Texture2D texture)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            }
        }
    }
}
