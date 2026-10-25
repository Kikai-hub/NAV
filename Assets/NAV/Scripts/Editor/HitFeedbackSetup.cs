using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Gameplay.Combat;
using NAV.Presentation.Combat;

namespace NAV.Editor
{
    /// <summary>
    /// One-click scene setup for HitFeedback: creates Materials/Combat/HitParticle.mat (a copy
    /// of the weather Rain.mat - URP Particles/Unlit, transparent, white base colour, so the
    /// particle colour comes from each particle) if it's missing, then adds a "HitFeedback"
    /// object to the open scene wired to its PlayerCombat. Safe to run again - an existing
    /// HitFeedback is re-wired instead of duplicated. Save the scene afterwards.
    /// </summary>
    internal static class HitFeedbackSetup
    {
        private const string MaterialFolder = "Assets/NAV/Materials/Combat";
        private const string MaterialPath = MaterialFolder + "/HitParticle.mat";
        private const string SourceMaterialPath = "Assets/NAV/Materials/Weather/Rain.mat";

        [MenuItem("NAV/Setup/Add Hit Feedback To Scene")]
        private static void Setup()
        {
            PlayerCombat playerCombat = Object.FindAnyObjectByType<PlayerCombat>(FindObjectsInactive.Include);
            if (playerCombat == null)
            {
                Debug.LogError("HitFeedbackSetup: no PlayerCombat in the open scene - open WorldGenTest first.");
                return;
            }

            Material material = GetOrCreateMaterial();
            if (material == null)
            {
                return;
            }

            HitFeedback feedback = Object.FindAnyObjectByType<HitFeedback>(FindObjectsInactive.Include);
            if (feedback == null)
            {
                var feedbackObject = new GameObject("HitFeedback");
                Undo.RegisterCreatedObjectUndo(feedbackObject, "Add Hit Feedback");
                feedback = feedbackObject.AddComponent<HitFeedback>();
            }

            ImpactEffects impacts = feedback.GetComponent<ImpactEffects>() ?? feedback.gameObject.AddComponent<ImpactEffects>();
            SetUpImpactEffects(impacts, material);

            var serialized = new SerializedObject(feedback);
            serialized.FindProperty("_playerCombat").objectReferenceValue = playerCombat;
            serialized.FindProperty("_particleMaterialTemplate").objectReferenceValue = material;
            serialized.FindProperty("_impacts").objectReferenceValue = impacts;
            serialized.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(feedback.gameObject.scene);
            Selection.activeGameObject = feedback.gameObject;
            Debug.Log($"HitFeedbackSetup: '{feedback.name}' wired to '{playerCombat.name}' with {MaterialPath}. Save the scene.");
        }

        // ---- Impact effects (wood chips, shards, blood, sparks, dust) ----

        private const string TextureFolder = "Assets/NAV/Art/Textures/Impact";
        private const string ShardMeshPath = "Assets/NAV/Art/Models/ImpactShard.asset";

        private static void SetUpImpactEffects(ImpactEffects impacts, Material baseParticle)
        {
            if (!AssetDatabase.IsValidFolder(TextureFolder))
            {
                AssetDatabase.CreateFolder("Assets/NAV/Art/Textures", "Impact");
            }

            Texture2D soft = WriteTexture("ImpactSoft", 64, (u, v, rng) => Mathf.Pow(Mathf.Clamp01(1f - Radius(u, v)), 1.6f));
            Texture2D flash = WriteTexture("ImpactFlash", 64, (u, v, rng) =>
            {
                // A small hot core with four thin rays.
                float x = Mathf.Abs(u - 0.5f) * 2f;
                float y = Mathf.Abs(v - 0.5f) * 2f;
                float core = Mathf.Pow(Mathf.Clamp01(1f - Radius(u, v) * 1.6f), 2f);
                float rays = Mathf.Clamp01(1f - x * 8f) * Mathf.Clamp01(1f - y) + Mathf.Clamp01(1f - y * 8f) * Mathf.Clamp01(1f - x);
                return Mathf.Clamp01(core + rays * 0.8f);
            });
            Texture2D splat = WriteTexture("ImpactSplat", 64, (u, v, rng) =>
            {
                // An irregular blob: radius wobbles with the angle, a few satellite drops.
                float angle = Mathf.Atan2(v - 0.5f, u - 0.5f);
                float edge = 0.55f + 0.12f * Mathf.Sin(angle * 5f) + 0.08f * Mathf.Sin(angle * 9f + 1.3f);
                float blob = Mathf.Clamp01((edge - Radius(u, v)) * 8f);
                float drop = Mathf.Clamp01((0.09f - Vector2.Distance(new Vector2(u, v), new Vector2(0.82f, 0.3f))) * 30f)
                    + Mathf.Clamp01((0.06f - Vector2.Distance(new Vector2(u, v), new Vector2(0.2f, 0.78f))) * 30f);
                return Mathf.Clamp01(blob + drop) * 0.9f;
            });

            Material softMaterial = CopyParticleMaterial(baseParticle, "ImpactSoft", soft, false);
            Material splatMaterial = CopyParticleMaterial(baseParticle, "ImpactSplat", splat, false);
            Material additiveMaterial = CopyParticleMaterial(baseParticle, "ImpactAdditive", flash, true);

            string chunkPath = $"{MaterialFolder}/ImpactChunk.mat";
            var chunk = AssetDatabase.LoadAssetAtPath<Material>(chunkPath);
            if (chunk == null)
            {
                chunk = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit")) { name = "ImpactChunk" };
                AssetDatabase.CreateAsset(chunk, chunkPath);
            }

            // Opaque and lit, coloured per particle - chips and shards darken at night like the world.
            chunk.SetFloat("_Surface", 0f);
            chunk.SetColor("_BaseColor", Color.white);
            chunk.SetFloat("_Smoothness", 0.1f);
            chunk.enableInstancing = true;
            EditorUtility.SetDirty(chunk);

            var serialized = new SerializedObject(impacts);
            serialized.FindProperty("_chunkMaterial").objectReferenceValue = chunk;
            serialized.FindProperty("_softMaterial").objectReferenceValue = softMaterial;
            serialized.FindProperty("_additiveMaterial").objectReferenceValue = additiveMaterial;
            serialized.FindProperty("_splatMaterial").objectReferenceValue = splatMaterial;
            serialized.FindProperty("_chipMesh").objectReferenceValue = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            serialized.FindProperty("_shardMesh").objectReferenceValue = GetOrCreateShardMesh();
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        private static float Radius(float u, float v)
        {
            return Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
        }

        /// <summary>White texture whose alpha is alpha(u, v) - particles tint it per particle.</summary>
        private static Texture2D WriteTexture(string name, int size, System.Func<float, float, System.Random, float> alpha)
        {
            string path = $"{TextureFolder}/{name}.png";
            var rng = new System.Random(name.GetHashCode());
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha((x + 0.5f) / size, (y + 0.5f) / size, rng)));
                }
            }

            texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material CopyParticleMaterial(Material source, string name, Texture2D texture, bool additive)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            if (additive)
            {
                // URP particle blend mode Additive: src alpha, dst one.
                material.SetFloat("_Blend", 2f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>A small irregular octahedron - a rock shard.</summary>
        private static Mesh GetOrCreateShardMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ShardMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "ImpactShard" };
                AssetDatabase.CreateAsset(mesh, ShardMeshPath);
            }

            Vector3[] points =
            {
                new(0f, 0.55f, 0f), new(0f, -0.4f, 0.05f),
                new(0.5f, 0.05f, 0.1f), new(-0.45f, -0.05f, 0f), new(0.05f, 0.1f, 0.5f), new(-0.1f, 0f, -0.45f),
            };
            int[] faces =
            {
                0, 2, 4, 0, 4, 3, 0, 3, 5, 0, 5, 2,
                1, 4, 2, 1, 3, 4, 1, 5, 3, 1, 2, 5,
            };

            // Flat-shaded: own vertices per face.
            var vertices = new Vector3[faces.Length];
            var triangles = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++)
            {
                vertices[i] = points[faces[i]];
                triangles[i] = i;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            // Wind outward (Unity: clockwise front faces).
            Vector3[] normals = mesh.normals;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 centre = (vertices[i] + vertices[i + 1] + vertices[i + 2]) / 3f;
                if (Vector3.Dot(normals[i], centre) < 0f)
                {
                    (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
                }
            }

            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material GetOrCreateMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                return material;
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/NAV/Materials", "Combat");
            }

            if (!AssetDatabase.CopyAsset(SourceMaterialPath, MaterialPath))
            {
                Debug.LogError($"HitFeedbackSetup: couldn't copy '{SourceMaterialPath}' to '{MaterialPath}'.");
                return null;
            }

            material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
