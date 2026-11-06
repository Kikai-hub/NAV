using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NAV.Editor
{
    /// <summary>
    /// Builds the Leshy (boss 1) model from the developer's two files:
    ///  - Leshy.fbx: the Mixamo skeleton only (the rigged export carries no mesh);
    ///  - the textured mesh (LeshySource.fbx, same coordinates as the skeleton before scaling).
    /// The mesh is skinned to the skeleton here: each vertex goes to the nearest bone segment
    /// its body region allows (arms only past the shoulders, legs only below the spine), then
    /// the weights are diffused over the welded surface so joints bend softly, top 4 kept.
    /// The importer scale makes him 2.5x the hero (about 4.6 m). Outputs, next to Leshy.fbx:
    /// Leshy_Body.asset (skinned mesh), Leshy_Body.mat (URP Lit, PBR maps), Leshy_Model.prefab.
    /// Needs the source mesh; once built the source can go - the clips are rebuilt by
    /// LeshyAnimationSetup on their own.
    /// </summary>
    public static class LeshyModelSetup
    {
        public const string Folder = "Assets/NAV/Art/Models/Characters/Leshy/";
        public const string SkeletonPath = Folder + "Leshy.fbx";
        public const string ModelPrefabPath = Folder + "Leshy_Model.prefab";
        private const string SourcePath = "Assets/_TempLeshySource/LeshySource.fbx";
        private const string MeshPath = Folder + "Leshy_Body.asset";
        private const string MaterialPath = Folder + "Leshy_Body.mat";
        private const string TextureFolder = Folder + "Textures/";
        private const string TextureBase = TextureFolder + "texture_pbr_20250901";

        // Raw file units -> metres: the mesh is 11.46 raw units tall, the hero ~1.85 m; 2.5x = 4.6 m.
        private const float ImportScale = 400f;
        private const int SmoothingPasses = 6;

        [MenuItem("NAV/Characters/Build Leshy Model")]
        private static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null)
            {
                Debug.LogError($"Leshy: source mesh '{SourcePath}' is gone - the model is already built. To rebuild, copy the developer's textured FBX (20261106211902_cffca832.fbx) to that path, run this again, then delete the copy.");
                return;
            }

            SetImportScale();
            Mesh sourceMesh = source.GetComponentInChildren<MeshFilter>().sharedMesh;
            var skeletonAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonPath);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(skeletonAsset);
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Transform hips = FindBone(instance.transform, "Hips");
                Transform[] bones = hips.GetComponentsInChildren<Transform>(true);

                Mesh mesh = BuildSkinnedMesh(sourceMesh, bones);
                Material material = BuildMaterial();

                var body = new GameObject("Body");
                body.transform.SetParent(instance.transform, false);
                var skin = body.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                skin.bones = bones;
                skin.rootBone = hips;
                skin.sharedMaterial = material;
                skin.updateWhenOffscreen = false;
                skin.localBounds = mesh.bounds;

                var animator = instance.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = instance.AddComponent<Animator>();
                }

                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                PrefabUtility.SaveAsPrefabAsset(instance, ModelPrefabPath);
                Debug.Log($"Leshy: model built - {mesh.vertexCount} verts skinned to {bones.Length} bones, height {mesh.bounds.size.y:F2} m -> {ModelPrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            LeshyAnimationSetup.Build();
        }

        private static void SetImportScale()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(SkeletonPath);
            if (Mathf.Approximately(importer.globalScale, ImportScale) && importer.animationType == ModelImporterAnimationType.Generic && !importer.importAnimation)
            {
                return;
            }

            importer.globalScale = ImportScale;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false; // the file's only clip is the T-pose
            importer.SaveAndReimport();
        }

        public static Transform FindBone(Transform root, string shortName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (ShortName(t) == shortName)
                {
                    return t;
                }
            }

            throw new System.InvalidOperationException($"Leshy: bone '{shortName}' not found under '{root.name}'.");
        }

        public static string ShortName(Transform t)
        {
            int colon = t.name.IndexOf(':');
            return colon >= 0 ? t.name.Substring(colon + 1) : t.name;
        }

        // ---------------------------------------------------------------- skinning

        private enum Region { Center, Head, Shoulder, Arm, Leg }

        private struct Segment
        {
            public int Bone;
            public Vector3 A;
            public Vector3 B;
            public Region Region;
            public float Side; // -1 left, +1 right, 0 centre
        }

        private static Mesh BuildSkinnedMesh(Mesh source, Transform[] bones)
        {
            Vector3[] vertices = source.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] *= ImportScale;
            }

            var index = new Dictionary<string, int>();
            for (int i = 0; i < bones.Length; i++)
            {
                index[ShortName(bones[i])] = i;
            }

            List<Segment> segments = BuildSegments(bones, index);
            float armX = Mathf.Abs(bones[index["LeftArm"]].position.x);
            float spineY = bones[index["Spine"]].position.y;
            float neckY = bones[index["Spine2"]].position.y;
            float hipHalf = Mathf.Abs(bones[index["LeftUpLeg"]].position.x);

            int n = vertices.Length;
            int boneCount = bones.Length;
            var weights = new float[n, boneCount];
            for (int v = 0; v < n; v++)
            {
                Vector3 p = vertices[v];
                int best = -1;
                float bestDistance = float.MaxValue;
                int fallback = -1;
                float fallbackDistance = float.MaxValue;
                foreach (Segment s in segments)
                {
                    float d = DistanceToSegment(p, s.A, s.B);
                    if (d < fallbackDistance)
                    {
                        fallbackDistance = d;
                        fallback = s.Bone;
                    }

                    if (d < bestDistance && Allowed(s, p, armX, spineY, neckY, hipHalf))
                    {
                        bestDistance = d;
                        best = s.Bone;
                    }
                }

                weights[v, best >= 0 ? best : fallback] = 1f;
            }

            Smooth(weights, vertices, source.triangles, boneCount);

            var boneWeights = new BoneWeight[n];
            for (int v = 0; v < n; v++)
            {
                boneWeights[v] = TopFour(weights, v, boneCount);
            }

            var mesh = new Mesh { name = "Leshy_Body", indexFormat = source.indexFormat };
            mesh.vertices = vertices;
            mesh.normals = source.normals;
            mesh.uv = source.uv;
            mesh.triangles = source.triangles;
            if (source.tangents != null && source.tangents.Length == n)
            {
                mesh.tangents = source.tangents;
            }
            else
            {
                mesh.RecalculateTangents();
            }

            mesh.boneWeights = boneWeights;
            var bindposes = new Matrix4x4[boneCount];
            for (int i = 0; i < boneCount; i++)
            {
                bindposes[i] = bones[i].worldToLocalMatrix; // the body sits at the root's origin
            }

            mesh.bindposes = bindposes;
            mesh.RecalculateBounds();

            AssetDatabase.DeleteAsset(MeshPath);
            AssetDatabase.CreateAsset(mesh, MeshPath);
            return mesh;
        }

        private static List<Segment> BuildSegments(Transform[] bones, Dictionary<string, int> index)
        {
            var pairs = new List<(string from, string to, Region region)>
            {
                ("Hips", "Spine", Region.Center), ("Spine", "Spine1", Region.Center),
                ("Spine1", "Spine2", Region.Center), ("Spine2", "Neck", Region.Center),
                ("Neck", "Head", Region.Head), ("Head", "HeadTop_End", Region.Head),
            };

            foreach (string side in new[] { "Left", "Right" })
            {
                pairs.Add((side + "Shoulder", side + "Arm", Region.Shoulder));
                pairs.Add((side + "Arm", side + "ForeArm", Region.Arm));
                pairs.Add((side + "ForeArm", side + "Hand", Region.Arm));
                pairs.Add((side + "Hand", side + "HandMiddle1", Region.Arm));
                foreach (string finger in new[] { "Index", "Middle", "Thumb" })
                {
                    for (int k = 1; k <= 3; k++)
                    {
                        pairs.Add((side + "Hand" + finger + k, side + "Hand" + finger + (k + 1), Region.Arm));
                    }
                }

                pairs.Add((side + "UpLeg", side + "Leg", Region.Leg));
                pairs.Add((side + "Leg", side + "Foot", Region.Leg));
                pairs.Add((side + "Foot", side + "ToeBase", Region.Leg));
                pairs.Add((side + "ToeBase", side + "Toe_End", Region.Leg));
            }

            var segments = new List<Segment>();
            foreach ((string from, string to, Region region) in pairs)
            {
                if (!index.TryGetValue(from, out int a) || !index.TryGetValue(to, out int b))
                {
                    continue;
                }

                segments.Add(new Segment
                {
                    Bone = a,
                    A = bones[a].position,
                    B = bones[b].position,
                    Region = region,
                    Side = from.StartsWith("Left") ? -1f : from.StartsWith("Right") ? 1f : 0f,
                });
            }

            return segments;
        }

        private static bool Allowed(Segment s, Vector3 p, float armX, float spineY, float neckY, float hipHalf)
        {
            float outward = p.x * s.Side; // distance out on the segment's own side
            switch (s.Region)
            {
                case Region.Arm: return outward > armX * 0.75f;
                case Region.Shoulder: return outward > armX * 0.25f && p.y > spineY;
                case Region.Leg: return p.y < spineY && outward > -hipHalf * 0.3f;
                case Region.Head: return p.y > neckY;
                default: return Mathf.Abs(p.x) < armX * 1.1f;
            }
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>Diffuses the weights over the surface (UV seams welded by position).</summary>
        private static void Smooth(float[,] weights, Vector3[] vertices, int[] triangles, int boneCount)
        {
            int n = vertices.Length;
            var group = new int[n];
            var keys = new Dictionary<Vector3Int, int>();
            for (int v = 0; v < n; v++)
            {
                Vector3Int key = Vector3Int.RoundToInt(vertices[v] * 1000f); // 1 mm
                if (!keys.TryGetValue(key, out int g))
                {
                    g = keys.Count;
                    keys[key] = g;
                }

                group[v] = g;
            }

            int groups = keys.Count;
            var neighbours = new HashSet<int>[groups];
            for (int g = 0; g < groups; g++)
            {
                neighbours[g] = new HashSet<int>();
            }

            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = group[triangles[t]], b = group[triangles[t + 1]], c = group[triangles[t + 2]];
                neighbours[a].Add(b); neighbours[a].Add(c);
                neighbours[b].Add(a); neighbours[b].Add(c);
                neighbours[c].Add(a); neighbours[c].Add(b);
            }

            var current = new float[groups, boneCount];
            var counts = new int[groups];
            for (int v = 0; v < n; v++)
            {
                counts[group[v]]++;
                for (int b = 0; b < boneCount; b++)
                {
                    current[group[v], b] += weights[v, b];
                }
            }

            for (int g = 0; g < groups; g++)
            {
                for (int b = 0; b < boneCount; b++)
                {
                    current[g, b] /= counts[g];
                }
            }

            var next = new float[groups, boneCount];
            for (int pass = 0; pass < SmoothingPasses; pass++)
            {
                for (int g = 0; g < groups; g++)
                {
                    int k = neighbours[g].Count;
                    for (int b = 0; b < boneCount; b++)
                    {
                        float sum = 0f;
                        foreach (int o in neighbours[g])
                        {
                            sum += current[o, b];
                        }

                        next[g, b] = k > 0 ? 0.5f * current[g, b] + 0.5f * sum / k : current[g, b];
                    }
                }

                (current, next) = (next, current);
            }

            for (int v = 0; v < n; v++)
            {
                for (int b = 0; b < boneCount; b++)
                {
                    weights[v, b] = current[group[v], b];
                }
            }
        }

        private static BoneWeight TopFour(float[,] weights, int v, int boneCount)
        {
            var index = new int[4] { 0, 0, 0, 0 };
            var value = new float[4];
            for (int b = 0; b < boneCount; b++)
            {
                float w = weights[v, b];
                for (int slot = 0; slot < 4; slot++)
                {
                    if (w <= value[slot])
                    {
                        continue;
                    }

                    for (int move = 3; move > slot; move--)
                    {
                        value[move] = value[move - 1];
                        index[move] = index[move - 1];
                    }

                    value[slot] = w;
                    index[slot] = b;
                    break;
                }
            }

            float total = value[0] + value[1] + value[2] + value[3];
            if (total <= 0f)
            {
                value[0] = total = 1f;
            }

            return new BoneWeight
            {
                boneIndex0 = index[0], weight0 = value[0] / total,
                boneIndex1 = index[1], weight1 = value[1] / total,
                boneIndex2 = index[2], weight2 = value[2] / total,
                boneIndex3 = index[3], weight3 = value[3] / total,
            };
        }

        // ---------------------------------------------------------------- material

        private static Material BuildMaterial()
        {
            string normalPath = TextureBase + "_normal.png";
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            if (normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }

            string packedPath = TextureFolder + "Leshy_MetallicSmoothness.png";
            PackMetallicSmoothness(TextureBase + "_metallic.png", TextureBase + "_roughness.png", packedPath);

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TextureBase + ".png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));
            material.SetFloat("_Smoothness", 1f); // scale of the packed map's alpha
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Cull", 0f); // the bark rags and leaves are single sheets
            material.doubleSidedGI = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>URP wants metallic in R and smoothness (1 - roughness) in A.</summary>
        private static void PackMetallicSmoothness(string metallicPath, string roughnessPath, string outPath)
        {
            var metallic = new Texture2D(2, 2);
            var roughness = new Texture2D(2, 2);
            metallic.LoadImage(File.ReadAllBytes(metallicPath));
            roughness.LoadImage(File.ReadAllBytes(roughnessPath));
            Color32[] m = metallic.GetPixels32();
            Color32[] r = roughness.GetPixels32();
            var packed = new Color32[m.Length];
            for (int i = 0; i < m.Length; i++)
            {
                packed[i] = new Color32(m[i].r, m[i].r, m[i].r, (byte)(255 - r[i].r));
            }

            var output = new Texture2D(metallic.width, metallic.height, TextureFormat.RGBA32, false);
            output.SetPixels32(packed);
            File.WriteAllBytes(outPath, output.EncodeToPNG());
            Object.DestroyImmediate(metallic);
            Object.DestroyImmediate(roughness);
            Object.DestroyImmediate(output);

            AssetDatabase.ImportAsset(outPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(outPath);
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
        }
    }
}
