using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Gameplay.Items;
using NAV.World.Biomes;
using NAV.World.Generation;

namespace NAV.Editor
{
    /// <summary>
    /// NAV > World > Bake Tree Impostors: for every tree any biome spawns (a resource prefab
    /// felled with an axe), renders it from 8 sides (orthographic, evenly lit, transparent
    /// background) into one atlas row (Art/Impostors/Imp_{tree}.png), makes a NAV/Impostor
    /// material with its size, collects them into ScriptableObjects/World/TreeImpostors and
    /// assigns that to the scene's WorldGenerator (scene saved). Frame k is the tree seen from
    /// k * 45 degrees around its forward axis - the angle the shader picks by. Re-run after
    /// adding or changing trees.
    /// </summary>
    internal static class ImpostorBaker
    {
        private const string AtlasFolder = "Assets/NAV/Art/Impostors";
        private const string MaterialFolder = "Assets/NAV/Materials/Impostors";
        private const string SetPath = "Assets/NAV/ScriptableObjects/World/TreeImpostors.asset";
        private const int Frames = 8;
        private const int FrameHeight = 256;
        private const int BakeLayer = 31;

        [MenuItem("NAV/World/Bake Tree Impostors")]
        private static void Bake()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("ImpostorBaker: exit Play Mode first.");
                return;
            }

            Shader shader = Shader.Find("NAV/Impostor");
            if (shader == null)
            {
                Debug.LogError("ImpostorBaker: shader NAV/Impostor not found.");
                return;
            }

            Directory.CreateDirectory(AtlasFolder);
            Directory.CreateDirectory(MaterialFolder);

            var trees = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:BiomeDefinition"))
            {
                var biome = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (BiomeResourceSpawn spawn in biome.ResourceSpawns)
                {
                    var node = spawn.Prefab != null ? spawn.Prefab.GetComponent<ResourceNode>() : null;
                    if (node != null && node.Definition != null && node.Definition.RequiredTool == ToolType.Axe && !trees.Contains(spawn.Prefab))
                    {
                        trees.Add(spawn.Prefab);
                    }
                }
            }

            var set = AssetDatabase.LoadAssetAtPath<ImpostorSet>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<ImpostorSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }

            var serializedSet = new SerializedObject(set);
            SerializedProperty entries = serializedSet.FindProperty("_entries");
            entries.arraySize = trees.Count;
            for (int i = 0; i < trees.Count; i++)
            {
                Material material = BakeTree(trees[i], shader);
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Prefab").objectReferenceValue = trees[i];
                entry.FindPropertyRelative("Material").objectReferenceValue = material;
            }

            serializedSet.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();

            WorldGenerator generator = Object.FindAnyObjectByType<WorldGenerator>(FindObjectsInactive.Include);
            if (generator != null)
            {
                var serialized = new SerializedObject(generator);
                serialized.FindProperty("_impostors").objectReferenceValue = set;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
                EditorSceneManager.SaveScene(generator.gameObject.scene);
            }
            else
            {
                Debug.LogWarning("ImpostorBaker: no WorldGenerator in the open scene - assign TreeImpostors to its Impostors field by hand.");
            }

            Debug.Log($"ImpostorBaker: baked {trees.Count} tree impostors into {AtlasFolder}.");
        }

        private static Material BakeTree(GameObject prefab, Shader shader)
        {
            GameObject instance = Object.Instantiate(prefab);
            var cameraObject = new GameObject("ImpostorCamera");
            var keyObject = new GameObject("ImpostorKey");
            var fillObject = new GameObject("ImpostorFill");
            bool fog = RenderSettings.fog;
            var ambientMode = RenderSettings.ambientMode;
            Color ambient = RenderSettings.ambientLight;
            try
            {
                RenderSettings.fog = false;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.45f);

                Vector3 pivot = new Vector3(0f, -10000f, 0f);
                instance.transform.SetPositionAndRotation(pivot, Quaternion.identity);
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = BakeLayer;
                }

                foreach (LODGroup group in instance.GetComponentsInChildren<LODGroup>())
                {
                    group.ForceLOD(0);
                }

                Bounds bounds = default;
                bool any = false;
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    if (renderer is BillboardRenderer)
                    {
                        continue;
                    }

                    if (!any)
                    {
                        bounds = renderer.bounds;
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }

                float yMin = bounds.min.y - pivot.y;
                float yMax = bounds.max.y - pivot.y;
                float radius = Mathf.Max(Mathf.Abs(bounds.min.x - pivot.x), Mathf.Abs(bounds.max.x - pivot.x), Mathf.Abs(bounds.min.z - pivot.z), Mathf.Abs(bounds.max.z - pivot.z));
                float height = yMax - yMin;
                int frameWidth = Mathf.Clamp(Mathf.RoundToInt(FrameHeight * 2f * radius / height), 64, 256);

                var target = new RenderTexture(frameWidth, FrameHeight, 24, RenderTextureFormat.ARGB32);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.cullingMask = 1 << BakeLayer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.orthographic = true;
                camera.orthographicSize = height * 0.5f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = radius * 4f + height * 2f + 20f;
                camera.targetTexture = target;
                camera.aspect = (float)frameWidth / FrameHeight;

                var key = keyObject.AddComponent<Light>();
                key.type = LightType.Directional;
                key.intensity = 0.9f;
                key.cullingMask = 1 << BakeLayer;
                var fill = fillObject.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.intensity = 0.3f;
                fill.cullingMask = 1 << BakeLayer;

                var atlas = new Texture2D(frameWidth * Frames, FrameHeight, TextureFormat.RGBA32, false);
                Vector3 centre = pivot + Vector3.up * (yMin + yMax) * 0.5f;
                var read = new Texture2D(frameWidth, FrameHeight, TextureFormat.RGBA32, false);
                // Warm-up: the first render of a fresh instance can come out empty (the
                // procedural birches did) - render once and throw it away.
                camera.transform.position = centre + Vector3.forward * (radius * 2f + height + 10f);
                camera.transform.LookAt(centre);
                camera.Render();
                for (int k = 0; k < Frames; k++)
                {
                    float angle = k * Mathf.PI * 2f / Frames;
                    var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                    camera.transform.position = centre + direction * (radius * 2f + height + 10f);
                    camera.transform.LookAt(centre);
                    // Light from over the viewer's shoulder, fill from the other side.
                    keyObject.transform.rotation = Quaternion.LookRotation(-direction + Vector3.down * 0.8f);
                    fillObject.transform.rotation = Quaternion.LookRotation(direction + Vector3.down * 0.3f);
                    camera.Render();

                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = target;
                    read.ReadPixels(new Rect(0, 0, frameWidth, FrameHeight), 0, 0);
                    read.Apply();
                    RenderTexture.active = previous;
                    atlas.SetPixels(k * frameWidth, 0, frameWidth, FrameHeight, read.GetPixels());
                }

                Object.DestroyImmediate(read);
                target.Release();
                Object.DestroyImmediate(target);

                Bleed(atlas, 4);
                atlas.Apply();
                string atlasPath = $"{AtlasFolder}/Imp_{prefab.name}.png";
                File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());
                Object.DestroyImmediate(atlas);
                AssetDatabase.ImportAsset(atlasPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                // Keeps the leaves' alpha coverage in the smaller mips - without it thin leaves
                // drop under the cutoff at a distance and only the trunk is left.
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.4f;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();

                string materialPath = $"{MaterialFolder}/Imp_{prefab.name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, materialPath);
                }

                material.shader = shader;
                material.SetTexture("_Atlas", AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath));
                material.SetFloat("_Frames", Frames);
                material.SetVector("_Size", new Vector4(radius * 2f, height, 0f, 0f));
                material.SetFloat("_BaseY", yMin);
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                return material;
            }
            finally
            {
                RenderSettings.fog = fog;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambient;
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(keyObject);
                Object.DestroyImmediate(fillObject);
            }
        }

        /// <summary>Spreads the colour of opaque pixels into the transparent ones around them
        /// (alpha untouched), so mipmapped edges don't fade to the black of the empty background.</summary>
        private static void Bleed(Texture2D texture, int passes)
        {
            int width = texture.width;
            int height = texture.height;
            Color[] pixels = texture.GetPixels();
            var filled = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                filled[i] = pixels[i].a > 0.1f;
            }

            for (int pass = 0; pass < passes; pass++)
            {
                var next = (bool[])filled.Clone();
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * width + x;
                        if (filled[i])
                        {
                            continue;
                        }

                        Color sum = Color.clear;
                        int count = 0;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= width || ny >= height || !filled[ny * width + nx])
                                {
                                    continue;
                                }

                                sum += pixels[ny * width + nx];
                                count++;
                            }
                        }

                        if (count > 0)
                        {
                            Color colour = sum / count;
                            colour.a = pixels[i].a;
                            pixels[i] = colour;
                            next[i] = true;
                        }
                    }
                }

                filled = next;
            }

            texture.SetPixels(pixels);
        }
    }
}
