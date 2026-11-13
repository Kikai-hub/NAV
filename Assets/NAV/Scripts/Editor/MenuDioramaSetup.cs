using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace NAV.Editor
{
    /// <summary>
    /// NAV > UI > Build Main Menu Diorama (2026-11-13, the developer's reference: a forest clearing at
    /// sunset - a log house on the right, a campfire with logs to sit on, birches and spruces framing,
    /// a misty valley with a lake and far hills, the menu on the left). Built only from what the game
    /// already has: the world's trees/bushes/flowers/rocks/stumps/logs, the player's own building
    /// pieces (the house is the one from a real test build - its pieces' offsets, mirrored where it
    /// was unfinished), the campfire piece, the Fantasy Skybox sunset sky, the world's terrain layers
    /// and water material. Gameplay components are stripped from every placed copy (no resource
    /// nodes, building logic or cooking in the menu). Rebuilds from scratch each time (the
    /// "MenuDiorama" root in the MainMenu scene); the terrain is Art/Menu/MenuTerrain.asset.
    /// </summary>
    internal static class MenuDioramaSetup
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string TerrainPath = "Assets/NAV/Art/Menu/MenuTerrain.asset";
        private const string RootName = "MenuDiorama";
        private const float TerrainBaseY = -40f;
        private const float TerrainHeight = 80f;
        private const float TerrainSize = 400f;
        private const float WaterY = -12.5f;

        private static Random _rnd;
        private static Terrain _terrain;
        private static Transform _root;

        [MenuItem("NAV/UI/Build Main Menu Diorama")]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("MenuDioramaSetup: leave Play Mode first.");
                return;
            }

            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                EditorSceneManager.OpenScene(ScenePath);
            }

            _rnd = new Random(1112);
            GameObject old = GameObject.Find(RootName);
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            _root = new GameObject(RootName).transform;
            BuildTerrain();
            BuildWater();
            BuildHouse(new Vector3(8.5f, 0f, 14f), -128f);
            BuildCampfire(new Vector3(1.6f, 0f, 9.5f));
            BuildTrees();
            BuildGroundCover();
            SetUpLightSkyFog();
            SetUpCamera();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("MenuDioramaSetup: the main menu diorama is built and the scene saved.");
        }

        // ---- Terrain and water ----

        private static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        /// <summary>World height (m) at x/z: a flat clearing, a valley falling away behind it with
        /// a lake, hills framing left and right, a ridge on the horizon.</summary>
        private static float Height(float x, float z)
        {
            float n = Mathf.PerlinNoise(x * 0.03f + 11f, z * 0.03f + 7f);
            float n2 = Mathf.PerlinNoise(x * 0.008f + 3f, z * 0.008f + 5f);
            float valley = Smooth(22f, 60f, z) * (1f - Smooth(150f, 210f, z));
            float sides = Smooth(28f, 80f, Mathf.Abs(x - 4f));
            float ridge = Smooth(140f, 230f, z);
            float h = 0.25f * (n - 0.5f);
            h -= 17f * valley * (1f - sides * 0.6f);
            h += 10f * sides * (0.5f + n);
            h += 26f * ridge * (0.55f + 0.6f * n2);
            return h;
        }

        private static void BuildTerrain()
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
            if (data == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/NAV/Art/Menu"))
                {
                    AssetDatabase.CreateFolder("Assets/NAV/Art", "Menu");
                }

                data = new TerrainData();
                AssetDatabase.CreateAsset(data, TerrainPath);
            }

            data.heightmapResolution = 257;
            data.alphamapResolution = 256;
            data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
            var origin = new Vector3(-TerrainSize / 2f, TerrainBaseY, -60f);
            int res = data.heightmapResolution;
            var heights = new float[res, res];
            for (int iz = 0; iz < res; iz++)
            {
                for (int ix = 0; ix < res; ix++)
                {
                    float x = origin.x + ix / (float)(res - 1) * TerrainSize;
                    float z = origin.z + iz / (float)(res - 1) * TerrainSize;
                    heights[iz, ix] = Mathf.Clamp01((Height(x, z) - TerrainBaseY) / TerrainHeight);
                }
            }

            data.SetHeights(0, 0, heights);

            var layers = new[]
            {
                AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/NAV/Art/Terrain/PlainsLayer.terrainlayer"),
                AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/NAV/Art/Terrain/ForestLayer.terrainlayer"),
                AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/NAV/Art/Terrain/GroveLayer.terrainlayer"),
            };
            data.terrainLayers = layers;
            int ar = data.alphamapResolution;
            var alpha = new float[ar, ar, layers.Length];
            for (int iz = 0; iz < ar; iz++)
            {
                for (int ix = 0; ix < ar; ix++)
                {
                    float x = origin.x + ix / (float)(ar - 1) * TerrainSize;
                    float z = origin.z + iz / (float)(ar - 1) * TerrainSize;
                    // Trodden earth round the fire and on the way to the house; forest floor on the
                    // slopes and far away; meadow grass elsewhere.
                    float fire = 1f - Smooth(1.5f, 3.6f, Vector2.Distance(new Vector2(x, z), new Vector2(1.6f, 9.5f)));
                    float path = 1f - Smooth(0.6f, 1.6f, DistanceToSegment(new Vector2(x, z), new Vector2(2.5f, 10f), new Vector2(7.5f, 12.5f)));
                    float forest = Mathf.Clamp01(Smooth(30f, 70f, z) * 0.7f + Smooth(25f, 60f, Mathf.Abs(x)) * 0.6f);
                    float earth = Mathf.Max(fire, path * 0.8f);
                    alpha[iz, ix, 1] = Mathf.Max(earth, forest * (1f - earth));
                    alpha[iz, ix, 2] = 0.25f * (1f - alpha[iz, ix, 1]) * Mathf.PerlinNoise(x * 0.1f, z * 0.1f);
                    alpha[iz, ix, 0] = Mathf.Max(0f, 1f - alpha[iz, ix, 1] - alpha[iz, ix, 2]);
                }
            }

            data.SetAlphamaps(0, 0, alpha);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "MenuTerrain";
            go.transform.SetParent(_root, false);
            go.transform.position = origin;
            _terrain = go.GetComponent<Terrain>();
            _terrain.drawInstanced = true;
            _terrain.basemapDistance = 400f;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private static float GroundY(float x, float z) => _terrain.SampleHeight(new Vector3(x, 0f, z)) + _terrain.transform.position.y;

        private static void BuildWater()
        {
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "MenuLake";
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.SetParent(_root, false);
            water.transform.position = new Vector3(0f, WaterY, 110f);
            water.transform.localScale = new Vector3(30f, 1f, 16f);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/NAV/Materials/World/NAVWater.mat");
            if (material != null)
            {
                water.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        // ---- Placing the game's own prefabs ----

        private static readonly string[] StripNamespaces = { "NAV.Gameplay", "NAV.World", "NAV.Network", "Photon" };

        /// <summary>A copy of a game prefab with everything gameplay taken off (it is scenery here).</summary>
        private static GameObject Place(string path, Vector3 position, float yaw, float scale = 1f, Transform parent = null, bool onGround = true)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"MenuDioramaSetup: no prefab at {path}.");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent != null ? parent : _root);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Strip(go);
            if (onGround)
            {
                position.y = GroundY(position.x, position.z) + position.y;
            }

            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale *= scale;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static void Strip(GameObject go)
        {
            // Several passes: a component can be required by another and only goes once that one has.
            for (int pass = 0; pass < 4; pass++)
            {
                foreach (Component component in go.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || component is Transform)
                    {
                        continue;
                    }

                    string full = component.GetType().FullName ?? string.Empty;
                    bool gameplay = Array.Exists(StripNamespaces, ns => full.StartsWith(ns, StringComparison.Ordinal));
                    if (gameplay || (pass > 0 && component is Rigidbody) || component is UnityEngine.AI.NavMeshObstacle)
                    {
                        Object.DestroyImmediate(component);
                    }
                }
            }
        }

        private static float R(float a, float b) => a + (float)_rnd.NextDouble() * (b - a);

        // ---- The house (the pieces' offsets from a real build, 2 x 2 foundations) ----

        private static void BuildHouse(Vector3 centre, float yaw)
        {
            const string B = "Assets/NAV/Prefabs/Build/";
            var house = new GameObject("House").transform;
            house.SetParent(_root, false);
            // Foundations sit on the lowest ground under the house so nothing floats.
            float ground = float.MaxValue;
            for (int i = -1; i <= 1; i += 2)
            {
                for (int j = -1; j <= 1; j += 2)
                {
                    Vector3 corner = centre + Quaternion.Euler(0f, yaw, 0f) * new Vector3(i * 2f, 0f, j * 2f);
                    ground = Mathf.Min(ground, GroundY(corner.x, corner.z));
                }
            }

            house.position = new Vector3(centre.x, ground + 0.25f, centre.z);
            house.rotation = Quaternion.Euler(0f, yaw, 0f);

            void Piece(string name, float x, float y, float z, float pieceYaw)
            {
                GameObject go = Place(B + name + ".prefab", Vector3.zero, 0f, 1f, house, false);
                if (go != null)
                {
                    go.transform.localPosition = new Vector3(x, y, z);
                    go.transform.localRotation = Quaternion.Euler(0f, pieceYaw, 0f);
                }
            }

            foreach (float x in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -1f, 1f })
                {
                    Piece("WoodFoundation", x, 0f, z, 0f);
                }
            }

            Piece("WoodWall", -1f, 1.1f, -2f, 0f);
            Piece("WoodWall", 1f, 1.1f, -2f, 0f);
            Piece("WoodWall", -1f, 1.1f, 2f, 0f);
            Piece("WoodDoor", 1f, 1.1f, 2f, 180f);
            foreach (float x in new[] { -2f, 2f })
            {
                Piece("WoodWall", x, 1.1f, -1f, 90f);
                Piece("WoodWall", x, 1.1f, 1f, 90f);
                Piece("WoodGable", x, 2.1f, -1f, 90f);
                Piece("WoodGable", x, 2.1f, 1f, -90f);
            }

            Piece("WoodRoof", -1f, 2.1f, -2f, 180f);
            Piece("WoodRoof", 1f, 2.1f, -2f, 180f);
            Piece("WoodRoof", -1f, 2.1f, 2f, 0f);
            Piece("WoodRoof", 1f, 2.1f, 2f, 0f);

            // A palisade fence and a woodpile beside the house.
            for (int i = 0; i < 9; i++)
            {
                Piece("WoodPalisade", -4.6f + i * 0.0f, 0.75f, -3.5f + i * 0.9f, 0f);
            }

            for (int i = 0; i < 4; i++)
            {
                GameObject log = Place("Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Props/Logs/Prefabs/UNS_Log.prefab", Vector3.zero, 0f, 0.45f, house, false);
                if (log != null)
                {
                    log.transform.localPosition = new Vector3(-2.9f, 0.05f + (i / 2) * 0.32f, -1.2f + (i % 2) * 0.4f + (i / 2) * 0.2f);
                    log.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                }
            }
        }

        // ---- The campfire and its seats ----

        private static void BuildCampfire(Vector3 at)
        {
            GameObject fire = Place("Assets/NAV/Prefabs/Build/Campfire.prefab", at + new Vector3(0f, 0.2f, 0f), 20f);
            if (fire != null)
            {
                foreach (Transform t in fire.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Meat_Ready")
                    {
                        t.gameObject.SetActive(false);
                    }

                    if (t.name == "Meat_Cooking")
                    {
                        t.gameObject.SetActive(true);
                    }
                }

                Light light = fire.GetComponentInChildren<Light>(true);
                if (light != null)
                {
                    light.enabled = true;
                    light.gameObject.SetActive(true);
                    light.intensity = Mathf.Max(light.intensity, 2.5f);
                    light.range = Mathf.Max(light.range, 7f);
                }
            }

            const string Log = "Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Props/Logs/Prefabs/UNS_Log.prefab";
            Place(Log, at + new Vector3(-2.2f, 0.05f, -0.6f), 75f, 0.8f);
            Place(Log, at + new Vector3(2.0f, 0.05f, 0.9f), 100f, 0.8f);
            Place(Log, at + new Vector3(0.4f, 0.05f, -2.4f), 10f, 0.7f);
            Place("Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Props/Logs/Prefabs/UNS_Stump.prefab", at + new Vector3(-1.4f, 0f, 2.3f), 30f, 0.9f);
            Place("Assets/Silver_Cats/Hand_Painted_Nature_Kit_LITE/Prefabs/Stump.prefab", at + new Vector3(4.6f, 0f, -3.6f), 200f, 1.1f);
        }

        // ---- Trees: framing near, lining the valley, a forest on the far hills ----

        private static readonly string[] Spruces =
        {
            "Assets/NAV/Prefabs/World/UNS_Spruce_WoodNode.prefab",
            "Assets/NAV/Prefabs/World/Vegetation/HP_Larch_Tree_WoodNode.prefab",
        };

        private static readonly string[] Birches =
        {
            "Assets/NAV/Prefabs/World/Vegetation/Birch_A_WoodNode.prefab",
            "Assets/NAV/Prefabs/World/Vegetation/Birch_B_WoodNode.prefab",
            "Assets/NAV/Prefabs/World/Vegetation/Birch_C_WoodNode.prefab",
        };

        private static void BuildTrees()
        {
            var trees = new GameObject("Trees").transform;
            trees.SetParent(_root, false);

            // Framing: big spruces on the left behind the menu, a birch on the right edge.
            var frame = new (string Path, Vector3 At, float Scale)[]
            {
                (Spruces[0], new Vector3(-7.5f, 0f, 8f), 1.25f),
                (Spruces[0], new Vector3(-11f, 0f, 15f), 1.35f),
                (Spruces[1], new Vector3(-5.5f, 0f, 19f), 1.1f),
                (Spruces[0], new Vector3(-14f, 0f, 5f), 1.3f),
                (Birches[0], new Vector3(6.2f, 0f, 3.2f), 1.25f),
                (Birches[1], new Vector3(10.5f, 0f, 6.5f), 1.1f),
                (Spruces[0], new Vector3(14f, 0f, 20f), 1.2f),
                (Birches[2], new Vector3(-3.5f, 0f, 14f), 1f),
                (Spruces[1], new Vector3(-1.5f, 0f, 30f), 1.05f),
            };
            foreach (var (path, at, scale) in frame)
            {
                Place(path, at, R(0f, 360f), scale, trees);
            }

            // Along the valley's shoulders.
            for (int i = 0; i < 40; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var at = new Vector3(side * R(12f, 45f), 0f, R(24f, 70f));
                Place(i % 5 == 0 ? Birches[i % 3] : Spruces[i % 2], at, R(0f, 360f), R(0.9f, 1.4f), trees);
            }

            // The far forest on the hills beyond the lake.
            for (int i = 0; i < 120; i++)
            {
                var at = new Vector3(R(-150f, 150f), 0f, R(120f, 300f));
                if (GroundY(at.x, at.z) < WaterY + 1f)
                {
                    continue;
                }

                Place(Spruces[i % 2], at, R(0f, 360f), R(1.4f, 2.4f), trees);
            }
        }

        // ---- Grass, flowers, ferns and stones in the foreground ----

        private static void BuildGroundCover()
        {
            var cover = new GameObject("GroundCover").transform;
            cover.SetParent(_root, false);
            const string Grass = "Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Vegetation/Grass/Prefabs/UNS_Grass.prefab";
            const string Flower = "Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Vegetation/Flowers/Prefabs/UNS_Flower.prefab";
            string[] flowers =
            {
                Flower,
                "Assets/NAV/Prefabs/World/Vegetation/CornflowerDetail.prefab",
                "Assets/Silver_Cats/Hand_Painted_Nature_Kit_LITE/Prefabs/Dandelion_02.prefab",
                "Assets/Silver_Cats/Hand_Painted_Nature_Kit_LITE/Prefabs/Plantain.prefab",
            };
            string[] bushes =
            {
                "Assets/NAV/Prefabs/World/Vegetation/NSK_bush02.prefab",
                "Assets/NAV/Prefabs/World/Vegetation/NSK_bush03.prefab",
                "Assets/NAV/Prefabs/World/Vegetation/NSK_bush06.prefab",
                "Assets/Silver_Cats/Hand_Painted_Nature_Kit_LITE/Prefabs/Fern.prefab",
            };

            for (int i = 0; i < 170; i++)
            {
                var at = new Vector3(R(-16f, 18f), 0f, R(3f, 24f));
                // Keep the trodden ground round the fire and the path clear.
                if (Vector2.Distance(new Vector2(at.x, at.z), new Vector2(1.6f, 9.5f)) < 3.2f
                    || DistanceToSegment(new Vector2(at.x, at.z), new Vector2(2.5f, 10f), new Vector2(7.5f, 12.5f)) < 1.2f
                    || Vector2.Distance(new Vector2(at.x, at.z), new Vector2(8.5f, 14f)) < 3.6f)
                {
                    continue;
                }

                Place(Grass, at, R(0f, 360f), R(0.6f, 1.0f), cover);
            }

            for (int i = 0; i < 90; i++)
            {
                var at = new Vector3(R(-14f, 16f), 0f, R(0f, 14f));
                if (Vector2.Distance(new Vector2(at.x, at.z), new Vector2(1.6f, 9.5f)) < 3f)
                {
                    continue;
                }

                Place(flowers[i % flowers.Length], at, R(0f, 360f), R(0.9f, 1.3f), cover);
            }

            for (int i = 0; i < 26; i++)
            {
                var at = new Vector3(R(-16f, 18f), 0f, R(2f, 26f));
                if (Vector2.Distance(new Vector2(at.x, at.z), new Vector2(1.6f, 9.5f)) < 4f || Vector2.Distance(new Vector2(at.x, at.z), new Vector2(8.5f, 14f)) < 4.5f)
                {
                    continue;
                }

                Place(bushes[i % bushes.Length], at, R(0f, 360f), R(0.8f, 1.2f), cover);
            }

            for (int i = 0; i < 14; i++)
            {
                var at = new Vector3(R(-12f, 14f), 0f, R(1f, 22f));
                Place("Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Rocks/River/Prefabs/UNS_Tiny_Rock_01.prefab", at, R(0f, 360f), R(0.8f, 1.6f), cover);
            }

            Place("Assets/NAV/Prefabs/World/SM_Rocks_03.prefab", new Vector3(-6f, -0.2f, 12f), 40f, 0.8f, cover);
            Place("Assets/InnerverseInteractive/Ultimate Nature – Starter/Environment/Vegetation/Mushrooms/Prefabs/UNS_Mushroom_Patch.prefab", new Vector3(5.5f, 0f, 4.5f), 0f, 1f, cover);
        }

        // ---- Sunset light, sky and haze ----

        private static void SetUpLightSkyFog()
        {
            Light sun = null;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.type == LightType.Directional)
                {
                    sun = light;
                    break;
                }
            }

            if (sun != null)
            {
                // Low sun ahead and a little right (over the valley), light coming towards the camera.
                sun.transform.rotation = Quaternion.Euler(9f, 192f, 0f);
                sun.color = new Color(1f, 0.74f, 0.48f);
                sun.intensity = 1.6f;
                sun.shadows = LightShadows.Soft;
                RenderSettings.sun = sun;
            }

            var sky = AssetDatabase.LoadAssetAtPath<Material>(FindSky("FS003_Sunset"));
            if (sky != null)
            {
                RenderSettings.skybox = sky;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.86f, 0.72f, 0.58f);
            RenderSettings.fogDensity = 0.0042f;
            DynamicGI.UpdateEnvironment();

            // Golden-hour grading and its strong sun shafts (SunShaftsVolume) suit the sunset scene.
            var volume = Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>();
            var golden = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/NAV/Settings/PostProcess/NAV_GoldenHour.asset");
            if (volume != null && golden != null)
            {
                volume.sharedProfile = golden;
            }
        }

        private static string FindSky(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Material", new[] { "Assets/Fantasy Skybox FREE" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
                {
                    return path;
                }
            }

            return null;
        }

        private static void SetUpCamera()
        {
            Camera camera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                return;
            }

            camera.transform.position = new Vector3(-1.5f, GroundY(-1.5f, -1f) + 2.9f, -1f);
            camera.transform.rotation = Quaternion.Euler(7f, 12f, 0f);
            camera.fieldOfView = 52f;
            camera.farClipPlane = 600f;
            camera.clearFlags = CameraClearFlags.Skybox;
            if (camera.GetComponent<NAV.Presentation.Camera.MenuCameraDrift>() == null)
            {
                camera.gameObject.AddComponent<NAV.Presentation.Camera.MenuCameraDrift>();
            }
        }
    }
}
