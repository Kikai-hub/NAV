using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NAV.World.Biomes;
using NAV.World.Generation;
using NAV.World.Generation.Hybrid;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NAV.Editor
{
    /// <summary>
    /// The hybrid world (continents + archipelagos + deep ocean, radius 15 km):
    ///   NAV > World > Hybrid World > Create Assets - HybridWorldSettings with climate bands,
    ///     height rules and polar caps; placeholder biomes Mountains, Swamp, Permafrost, Desert
    ///     (ground and map colour only - their content comes with their tiers); placeholder POI
    ///     kinds (ruins, shrine, shipwreck, boss arena slot - slots, no bosses) with placeholder
    ///     prefabs from primitives; wires the settings into the open scene's WorldGenerator. Safe to re-run:
    ///     existing assets are kept, only the map colours and settings lists are refreshed.
    ///   NAV > World > Hybrid World > Create Tile Terrain Base - a tiny TerrainData asset, trimmed
    ///     from the scene's template terrain, that every terrain tile is cloned from, wired into
    ///     the WorldGenerationSettings. Grass on a new TerrainData() is white quads in builds.
    ///   NAV > World > Hybrid World > Render Preview Map - draws the whole world from the
    ///     settings (biomes, hill shading, sea depth, zone rings, POI dots) into
    ///     ProjectTitle/HybridWorld_Preview.png and logs land/biome/POI statistics.
    /// </summary>
    internal static class HybridWorldSetup
    {
        private const string BiomeFolder = "Assets/NAV/ScriptableObjects/Biomes/";
        private const string PoiFolder = "Assets/NAV/ScriptableObjects/World/Poi/";
        private const string LayerFolder = "Assets/NAV/Art/Terrain/";
        private const string GroundTextureFolder = "Assets/ADG_Textures/ground_vol1/";
        private const string SettingsPath = "Assets/NAV/ScriptableObjects/World/HybridWorldSettings.asset";
        private const string PoiPrefabFolder = "Assets/NAV/Prefabs/World/Poi/";
        private const string PoiMaterialFolder = "Assets/NAV/Materials/Poi/";
        private const string PreviewPath = "ProjectTitle/HybridWorld_Preview.png";
        private const int PreviewSeed = 1337;
        private const int PreviewSize = 1024;
        private const string TileBasePath = LayerFolder + "TileTerrainBase.asset";

        [MenuItem("NAV/World/Hybrid World/Create Tile Terrain Base")]
        private static void CreateTileTerrainBase()
        {
            var generator = Object.FindAnyObjectByType<WorldGenerator>(FindObjectsInactive.Include);
            if (generator == null)
            {
                Debug.LogError("HybridWorldSetup: no WorldGenerator in the open scene.");
                return;
            }

            var serialized = new SerializedObject(generator);
            var template = (Terrain)serialized.FindProperty("_terrain").objectReferenceValue;
            var settings = (WorldGenerationSettings)serialized.FindProperty("_settings").objectReferenceValue;
            if (template == null || template.terrainData == null || settings == null)
            {
                Debug.LogError("HybridWorldSetup: the WorldGenerator needs its Terrain (with data) and Settings assigned.", generator);
                return;
            }

            TerrainData data = Object.Instantiate(template.terrainData);
            data.heightmapResolution = 33;
            data.alphamapResolution = 16;
            data.baseMapResolution = 16;
            data.SetDetailResolution(16, 8);
            data.detailPrototypes = new DetailPrototype[0];
            data.treePrototypes = new TreePrototype[0];
            data.terrainLayers = new TerrainLayer[0];
            AssetDatabase.DeleteAsset(TileBasePath);
            AssetDatabase.CreateAsset(data, TileBasePath);

            var settingsObject = new SerializedObject(settings);
            settingsObject.FindProperty("_tileBaseData").objectReferenceValue = data;
            settingsObject.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"HybridWorldSetup: created {TileBasePath}, wired into '{settings.name}'.", data);
        }

        [MenuItem("NAV/World/Hybrid World/Create Assets")]
        private static void CreateAssets()
        {
            BiomeDefinition plains = LoadBiome("PlainsBiome");
            BiomeDefinition grove = LoadBiome("BirchGroveBiome");
            BiomeDefinition forest = LoadBiome("ForestBiome");
            BiomeDefinition dense = LoadBiome("DenseForestBiome");
            BiomeDefinition islands = LoadBiome("SeaIslandsBiome");
            BiomeDefinition ocean = LoadBiome("OceanBiome");
            if (plains == null || grove == null || forest == null || dense == null || islands == null || ocean == null)
            {
                Debug.LogError("HybridWorldSetup: a biome asset is missing - run the vegetation, dense forest and sea islands setups first.");
                return;
            }

            SetMapColor(plains, new Color(0.62f, 0.74f, 0.38f));
            SetMapColor(grove, new Color(0.55f, 0.78f, 0.45f));
            SetMapColor(forest, new Color(0.24f, 0.52f, 0.24f));
            SetMapColor(dense, new Color(0.1f, 0.33f, 0.17f));
            SetMapColor(islands, new Color(0.78f, 0.74f, 0.55f));
            SetMapColor(ocean, new Color(0.12f, 0.3f, 0.52f));

            BiomeDefinition mountains = PlaceholderBiome("MountainsBiome", "Mountains", "MountainsLayer", new Color(0.55f, 0.53f, 0.5f), "ground7", new Vector4(0.85f, 0.85f, 0.88f, 1f));
            BiomeDefinition swamp = PlaceholderBiome("SwampBiome", "Swamp", "SwampLayer", new Color(0.33f, 0.4f, 0.25f), "ground5", new Vector4(0.9f, 0.9f, 0.85f, 1f));
            BiomeDefinition permafrost = PlaceholderBiome("PermafrostBiome", "Permafrost", "PermafrostLayer", new Color(0.88f, 0.92f, 0.95f), "ground3", new Vector4(1.15f, 1.18f, 1.2f, 1f));
            BiomeDefinition desert = PlaceholderBiome("DesertBiome", "Desert", "DesertLayer", new Color(0.9f, 0.78f, 0.5f), "ground2", Vector4.one);

            // Mountains: the roughest local relief; swamps: almost flat.
            SetHeightVariation(mountains, 1f);
            SetHeightVariation(swamp, 0.1f);

            // From the safe centre to the dangerous rim; tier 4+ biomes (black forest, steppe...)
            // get their own bands when they exist.
            var bands = new List<ClimateBand>
            {
                // Plains with copses of ordinary forest, one part in four - not an empty field.
                new(plains, 0.15f, forest, 0.25f),
                new(grove, 0.27f),
                new(forest, 0.42f),
                new(dense, 0.6f),
                new(islands, 1.2f)
            };

            var heightRules = new List<HeightBiomeRule>
            {
                new(mountains, 95f, 10000f, LandmassFilter.Any, 1f, false),
                new(swamp, 0f, 4f, LandmassFilter.Island, 0.5f, true)
            };

            // Snowy peaks above the mountains (tier 5 - MountainSetup makes the biome); checked first.
            BiomeDefinition snowPeaks = LoadBiome("SnowPeaksBiome");
            if (snowPeaks != null)
            {
                heightRules.Insert(0, new HeightBiomeRule(snowPeaks, 175f, 10000f, LandmassFilter.Any, 1f, false));
                SetMapColor(snowPeaks, new Color(0.95f, 0.96f, 0.98f));
            }

            var landBiomes = new List<BiomeDefinition> { plains, grove, forest, dense, islands, mountains, swamp, permafrost, desert };
            var pois = new List<PoiDefinition>
            {
                Poi("BossArenaSlot", "Boss arena (slot)", new Color(0.9f, 0.1f, 0.1f), 8, 10,
                    new List<BiomeDefinition>(landBiomes), new Vector2(2f, 10000f), new Vector2(0.15f, 0.8f), 1500f, 4000f),
                Poi("Shrine", "Shrine (kapishche)", new Color(1f, 0.85f, 0.2f), 25, 5,
                    new List<BiomeDefinition> { plains, grove, forest }, new Vector2(1f, 10000f), new Vector2(0f, 0.45f), 400f, 1500f),
                Poi("Shipwreck", "Shipwreck", new Color(0.55f, 0.3f, 0.12f), 15, 3,
                    new List<BiomeDefinition>(), new Vector2(0.3f, 6f), new Vector2(0.35f, 0.85f), 300f, 1500f),
                Poi("Ruins", "Ruins", new Color(0.75f, 0.2f, 0.9f), 60, 0,
                    new List<BiomeDefinition>(landBiomes), new Vector2(1f, 10000f), new Vector2(0f, 0.85f), 400f, 1200f)
            };

            var settings = AssetDatabase.LoadAssetAtPath<HybridWorldSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<HybridWorldSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            settings.EditorAssign(ocean, permafrost, desert, bands, heightRules, pois);
            EditorUtility.SetDirty(settings);
            foreach (PoiDefinition poi in pois)
            {
                AssignPlaceholderPrefab(poi);
            }

            AssetDatabase.SaveAssets();
            WireScene(settings);
            Debug.Log("HybridWorldSetup: assets ready - " + SettingsPath);
        }

        // The open scene's WorldGenerator gets the hybrid settings.
        private static void WireScene(HybridWorldSettings settings)
        {
            var generator = Object.FindAnyObjectByType<WorldGenerator>(FindObjectsInactive.Include);
            if (generator == null)
            {
                Debug.LogWarning("HybridWorldSetup: no WorldGenerator in the open scene - assign HybridWorldSettings to its World Shape field by hand.");
                return;
            }

            var serialized = new SerializedObject(generator);
            serialized.FindProperty("_worldShape").objectReferenceValue = settings;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
        }

        // Placeholder landmarks from primitives until the 3D artist's models exist
        // (MODELS_FOR_3D_ARTIST_v0.1.md); a POI that already has a prefab keeps it.
        private static void AssignPlaceholderPrefab(PoiDefinition poi)
        {
            var serialized = new SerializedObject(poi);
            SerializedProperty prefabProperty = serialized.FindProperty("_prefab");
            if (prefabProperty.objectReferenceValue != null)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(PoiPrefabFolder.TrimEnd('/')))
            {
                AssetDatabase.CreateFolder("Assets/NAV/Prefabs/World", "Poi");
            }

            if (!AssetDatabase.IsValidFolder(PoiMaterialFolder.TrimEnd('/')))
            {
                AssetDatabase.CreateFolder("Assets/NAV/Materials", "Poi");
            }

            Material stone = PlaceholderMaterial("PoiStone", new Color(0.52f, 0.5f, 0.47f));
            Material darkStone = PlaceholderMaterial("PoiDarkStone", new Color(0.22f, 0.21f, 0.23f));
            Material wood = PlaceholderMaterial("PoiWood", new Color(0.42f, 0.28f, 0.16f));

            var root = new GameObject("Poi_" + poi.Id);
            switch (poi.Id)
            {
                case "Ruins":
                    // Broken pillars round a fallen block.
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = i * Mathf.PI / 3f;
                        float height = 1.2f + (i * 0.7f % 2.3f);
                        Part(root, PrimitiveType.Cube, stone, new Vector3(Mathf.Cos(angle) * 5f, height * 0.5f, Mathf.Sin(angle) * 5f), new Vector3(0.9f, height, 0.9f), i * 11f);
                    }

                    Part(root, PrimitiveType.Cube, stone, new Vector3(0.5f, 0.35f, -0.4f), new Vector3(3.2f, 0.7f, 1.1f), 25f);
                    break;
                case "Shrine":
                    // A carved idol in a ring of stones.
                    Part(root, PrimitiveType.Cylinder, wood, new Vector3(0f, 1.6f, 0f), new Vector3(0.6f, 1.6f, 0.6f), 0f);
                    Part(root, PrimitiveType.Cube, wood, new Vector3(0f, 2.9f, 0f), new Vector3(0.9f, 0.5f, 0.9f), 45f);
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * Mathf.PI / 4f;
                        Part(root, PrimitiveType.Sphere, stone, new Vector3(Mathf.Cos(angle) * 3.5f, 0.15f, Mathf.Sin(angle) * 3.5f), new Vector3(0.8f, 0.5f, 0.7f), i * 23f);
                    }

                    break;
                case "Shipwreck":
                    // A hull on its side and a broken mast.
                    Part(root, PrimitiveType.Capsule, wood, new Vector3(0f, 0.7f, 0f), new Vector3(2.6f, 4.5f, 1.6f), 0f, new Vector3(80f, 0f, 20f));
                    Part(root, PrimitiveType.Cylinder, wood, new Vector3(1.8f, 1.2f, 1.5f), new Vector3(0.25f, 2.5f, 0.25f), 0f, new Vector3(0f, 0f, 55f));
                    break;
                default:
                    // Boss arena slot: a ring of tall dark menhirs - the arena itself comes with its boss.
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * Mathf.PI / 4f;
                        Part(root, PrimitiveType.Cube, darkStone, new Vector3(Mathf.Cos(angle) * 12f, 2.5f, Mathf.Sin(angle) * 12f), new Vector3(1.2f, 5f, 0.8f), -angle * Mathf.Rad2Deg);
                    }

                    break;
            }

            string path = PoiPrefabFolder + root.name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            prefabProperty.objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Part(GameObject root, PrimitiveType type, Material material, Vector3 position, Vector3 scale, float yaw, Vector3 tilt = default)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(tilt.x, yaw + tilt.y, tilt.z);
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material PlaceholderMaterial(string name, Color colour)
        {
            string path = PoiMaterialFolder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = colour };
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
        }

        [MenuItem("NAV/World/Hybrid World/Render Preview Map")]
        private static void RenderPreviewMenu()
        {
            RenderPreview(PreviewSeed, PreviewSize, PreviewPath);
        }

        /// <summary>Draws the world for a seed into a PNG and returns the statistics text.</summary>
        public static string RenderPreview(int seed, int size, string path)
        {
            var settings = AssetDatabase.LoadAssetAtPath<HybridWorldSettings>(SettingsPath);
            if (settings == null)
            {
                Debug.LogError("HybridWorldSetup: no settings - run Create Assets first.");
                return null;
            }

            Stopwatch timer = Stopwatch.StartNew();
            var shape = new HybridWorldShape(settings, seed);
            float radius = shape.Radius;
            float extent = radius * 1.05f;
            float metresPerPixel = extent * 2f / size;

            var heights = new float[size * size];
            var biomes = new BiomeDefinition[size * size];
            var biomeCounts = new Dictionary<BiomeDefinition, int>();
            int[] zoneLand = new int[3];
            int[] zoneTotal = new int[3];
            float highest = 0f;

            for (int py = 0; py < size; py++)
            {
                float z = -extent + (py + 0.5f) * metresPerPixel;
                for (int px = 0; px < size; px++)
                {
                    float x = -extent + (px + 0.5f) * metresPerPixel;
                    WorldPoint point = shape.Sample(x, z);
                    int i = py * size + px;
                    heights[i] = point.Height;
                    biomes[i] = point.Biome;
                    if (point.Radius01 > 1f)
                    {
                        continue;
                    }

                    int zone = (int)point.Zone;
                    zoneTotal[zone]++;
                    if (!point.IsLand)
                    {
                        continue;
                    }

                    zoneLand[zone]++;
                    highest = Mathf.Max(highest, point.Height);
                    if (point.Biome != null)
                    {
                        biomeCounts.TryGetValue(point.Biome, out int count);
                        biomeCounts[point.Biome] = count + 1;
                    }
                }
            }

            long sampleMs = timer.ElapsedMilliseconds;
            var placer = new PoiPlacer(shape, seed);
            List<PoiPlacement> pois = placer.PlaceAll(settings.PointsOfInterest, settings.PoiAttemptsPerPoint);
            long poiMs = timer.ElapsedMilliseconds - sampleMs;

            Color[] pixels = Paint(heights, biomes, size, metresPerPixel, extent, radius, settings);
            DrawPois(pixels, size, extent, metresPerPixel, pois);

            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            texture.SetPixels(pixels);
            texture.Apply();
            string fullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", path);
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            string report = Report(seed, size, sampleMs, poiMs, zoneLand, zoneTotal, highest, biomeCounts, pois, placer, fullPath);
            Debug.Log(report);
            return report;
        }

        private static Color[] Paint(float[] heights, BiomeDefinition[] biomes, int size, float metresPerPixel, float extent, float radius, HybridWorldSettings settings)
        {
            var pixels = new Color[size * size];
            Color shallow = new(0.3f, 0.56f, 0.72f);
            Color deep = new(0.05f, 0.14f, 0.3f);
            Vector3 light = new Vector3(-1f, 1.4f, 1f).normalized;
            float deepest = settings.SeaDepthScale * -settings.DeepOceanElevation;

            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    int i = py * size + px;
                    float height = heights[i];
                    Color colour;
                    if (height <= 0f || biomes[i] == null)
                    {
                        colour = Color.Lerp(shallow, deep, Mathf.Clamp01(-height / deepest));
                    }
                    else
                    {
                        // Hill shading from the neighbouring heights.
                        float dx = heights[py * size + Mathf.Min(px + 1, size - 1)] - heights[py * size + Mathf.Max(px - 1, 0)];
                        float dz = heights[Mathf.Min(py + 1, size - 1) * size + px] - heights[Mathf.Max(py - 1, 0) * size + px];
                        Vector3 normal = new Vector3(-dx, metresPerPixel * 2f, -dz).normalized;
                        float shade = Mathf.Lerp(0.6f, 1.15f, Mathf.Clamp01(Vector3.Dot(normal, light)));
                        colour = biomes[i].MapColor * shade;
                    }

                    // Zone rings: continent edge, archipelago edge, world rim.
                    float x = -extent + (px + 0.5f) * metresPerPixel;
                    float z = -extent + (py + 0.5f) * metresPerPixel;
                    float r = Mathf.Sqrt(x * x + z * z) / radius;
                    float ringWidth = metresPerPixel / radius * 0.7f;
                    if (IsOnRing(r, settings.ContinentZoneEnd, ringWidth) || IsOnRing(r, settings.ArchipelagoZoneEnd, ringWidth) || IsOnRing(r, 1f, ringWidth))
                    {
                        colour = Color.Lerp(colour, Color.white, ((px + py) / 6) % 2 == 0 ? 0.55f : 0f);
                    }

                    colour.a = 1f;
                    pixels[i] = colour;
                }
            }

            return pixels;
        }

        private static bool IsOnRing(float r, float ring, float width) => Mathf.Abs(r - ring) < width;

        private static void DrawPois(Color[] pixels, int size, float extent, float metresPerPixel, List<PoiPlacement> pois)
        {
            foreach (PoiPlacement poi in pois)
            {
                int cx = Mathf.FloorToInt((poi.Position.x + extent) / metresPerPixel);
                int cy = Mathf.FloorToInt((poi.Position.z + extent) / metresPerPixel);
                for (int dy = -3; dy <= 3; dy++)
                {
                    for (int dx = -3; dx <= 3; dx++)
                    {
                        int x = cx + dx;
                        int y = cy + dy;
                        if (x < 0 || y < 0 || x >= size || y >= size)
                        {
                            continue;
                        }

                        bool border = Mathf.Abs(dx) == 3 || Mathf.Abs(dy) == 3;
                        pixels[y * size + x] = border ? Color.black : poi.Definition.MapColor;
                    }
                }
            }
        }

        private static string Report(int seed, int size, long sampleMs, long poiMs, int[] zoneLand, int[] zoneTotal, float highest,
            Dictionary<BiomeDefinition, int> biomeCounts, List<PoiPlacement> pois, PoiPlacer placer, string path)
        {
            var text = new StringBuilder();
            text.AppendLine($"Hybrid world preview, seed {seed}, {size}x{size}: sampling {sampleMs} ms, POIs {poiMs} ms -> {path}");
            string[] zones = { "Continent zone", "Archipelago zone", "Ocean zone" };
            for (int z = 0; z < 3; z++)
            {
                text.AppendLine($"  {zones[z]}: land {100f * zoneLand[z] / Mathf.Max(1, zoneTotal[z]):F1}%");
            }

            text.AppendLine($"  Highest point: {highest:F0} m");
            int land = 0;
            foreach (int count in biomeCounts.Values)
            {
                land += count;
            }

            foreach (KeyValuePair<BiomeDefinition, int> pair in biomeCounts)
            {
                text.AppendLine($"  {pair.Key.DisplayName}: {100f * pair.Value / Mathf.Max(1, land):F1}% of land");
            }

            var perKind = new Dictionary<PoiDefinition, int>();
            foreach (PoiPlacement poi in pois)
            {
                perKind.TryGetValue(poi.Definition, out int count);
                perKind[poi.Definition] = count + 1;
            }

            foreach (KeyValuePair<PoiDefinition, int> pair in perKind)
            {
                placer.Shortfalls.TryGetValue(pair.Key, out int missing);
                text.AppendLine($"  POI {pair.Key.DisplayName}: {pair.Value}" + (missing > 0 ? $" ({missing} did not fit)" : string.Empty));
            }

            foreach (KeyValuePair<PoiDefinition, int> pair in placer.Shortfalls)
            {
                if (!perKind.ContainsKey(pair.Key))
                {
                    text.AppendLine($"  POI {pair.Key.DisplayName}: 0 ({pair.Value} did not fit)");
                }
            }

            return text.ToString();
        }

        private static BiomeDefinition LoadBiome(string name) => AssetDatabase.LoadAssetAtPath<BiomeDefinition>(BiomeFolder + name + ".asset");

        private static void SetHeightVariation(BiomeDefinition biome, float variation)
        {
            var serialized = new SerializedObject(biome);
            serialized.FindProperty("_heightVariation").floatValue = variation;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetMapColor(BiomeDefinition biome, Color colour)
        {
            var serialized = new SerializedObject(biome);
            serialized.FindProperty("_mapColor").colorValue = colour;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Ground layer (the plains texture tinted) and map colour; content comes with the biome's tier.
        // texture: an ADG ground set ("ground7" = grey stone, "ground5" = marsh, "ground3" = frost, "ground2" = sand).
        private static BiomeDefinition PlaceholderBiome(string assetName, string displayName, string layerName, Color mapColour, string texture, Vector4 tint)
        {
            string layerPath = LayerFolder + layerName + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }

            string textureFolder = $"{GroundTextureFolder}{texture}/{texture}";
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureFolder + "_Diffuse.tga");
            if (layer.diffuseTexture == null)
            {
                Debug.LogError($"HybridWorldSetup: ground texture '{texture}' not found under {GroundTextureFolder}.");
            }

            layer.tileSize = new Vector2(4f, 4f);
            layer.diffuseRemapMax = tint;
            EditorUtility.SetDirty(layer);

            string path = BiomeFolder + assetName + ".asset";
            var biome = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(path);
            if (biome == null)
            {
                biome = ScriptableObject.CreateInstance<BiomeDefinition>();
                AssetDatabase.CreateAsset(biome, path);
                var serialized = new SerializedObject(biome);
                serialized.FindProperty("_displayName").stringValue = displayName;
                serialized.FindProperty("_terrainLayer").objectReferenceValue = layer;
                serialized.FindProperty("_heightVariation").floatValue = 0.4f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            SetMapColor(biome, mapColour);
            return biome;
        }

        private static PoiDefinition Poi(string id, string displayName, Color colour, int count, int priority,
            List<BiomeDefinition> biomes, Vector2 heightRange, Vector2 radialRange, float exclusion, float sameKind)
        {
            if (!AssetDatabase.IsValidFolder(PoiFolder.TrimEnd('/')))
            {
                AssetDatabase.CreateFolder("Assets/NAV/ScriptableObjects/World", "Poi");
            }

            string path = PoiFolder + id + ".asset";
            var poi = AssetDatabase.LoadAssetAtPath<PoiDefinition>(path);
            if (poi == null)
            {
                poi = ScriptableObject.CreateInstance<PoiDefinition>();
                AssetDatabase.CreateAsset(poi, path);
                poi.EditorConfigure(id, displayName, colour, count, priority, biomes, true, heightRange, radialRange, exclusion, sameKind);
                EditorUtility.SetDirty(poi);
            }

            return poi;
        }
    }
}
