using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NAV.Editor
{
    /// <summary>
    /// Contact sheets of the Leshy clips: each clip is sampled at a few times, every pose baked
    /// into a static mesh and lined up next to the hero for scale, then rendered on its own
    /// layer to ProjectTitle/Screenshots/leshy_&lt;clip&gt;.png. For checking the skinning and the
    /// poses without entering Play Mode.
    /// </summary>
    public static class LeshyPreviewRenderer
    {
        private const int Layer = 31;
        private const string OutFolder = "ProjectTitle/Screenshots/";

        private static readonly (string clip, float[] times, float yaw)[] Sheets =
        {
            ("Idle", new[] { 0f, 1.5f, 3f, 4.5f }, 150f),
            ("Walk", new[] { 0f, 0.2f, 0.55f, 1.1f, 1.6f }, 110f),
            ("Attack_Slam", new[] { 0f, 1.3f, 1.7f, 2.05f, 2.3f, 3.2f }, 120f),
            ("Attack_Sweep", new[] { 0f, 1.2f, 1.6f, 1.95f, 2.2f }, 160f),
            ("Roar", new[] { 0.9f, 1.8f, 2.6f }, 150f),
            ("HitReact", new[] { 0f, 0.3f, 0.6f }, 120f),
        };

        [MenuItem("NAV/Characters/Render Leshy Preview")]
        public static void RenderAll()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(LeshyModelSetup.ModelPrefabPath);
            Directory.CreateDirectory(OutFolder);
            foreach ((string clipName, float[] times, float yaw) in Sheets)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(LeshyModelSetup.Folder + "Animations/Leshy_" + clipName + ".anim");
                string path = OutFolder + "leshy_" + clipName.ToLowerInvariant() + ".png";
                Render(model, clip, times, yaw, path);
            }

            Debug.Log("Leshy: preview sheets -> " + OutFolder + "leshy_*.png");
        }

        private static void Render(GameObject model, AnimationClip clip, float[] times, float yaw, string path)
        {
            var created = new List<Object>();
            const float spacing = 4.2f;
            var hero = GameObject.Find("Player/Hero");
            try
            {
                for (int i = 0; i < times.Length; i++)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                    instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    clip.SampleAnimation(instance, times[i]);
                    var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                    var baked = new Mesh();
                    skin.BakeMesh(baked, true);
                    created.Add(baked);

                    var pose = new GameObject("LeshyPose" + i) { layer = Layer };
                    pose.transform.SetPositionAndRotation(new Vector3(i * spacing, 1000f, 0f), Quaternion.Euler(0f, yaw, 0f));
                    pose.AddComponent<MeshFilter>().sharedMesh = baked;
                    pose.AddComponent<MeshRenderer>().sharedMaterial = skin.sharedMaterial;
                    created.Add(pose);
                    Object.DestroyImmediate(instance);
                }

                if (hero != null)
                {
                    // A copy of the hero in his current pose, next to the first Leshy, for scale.
                    var scale = Object.Instantiate(hero);
                    scale.name = "HeroScale";
                    scale.transform.SetPositionAndRotation(new Vector3(-2.6f, 1000f, 0f), Quaternion.Euler(0f, 160f, 0f));
                    foreach (Transform child in scale.GetComponentsInChildren<Transform>(true))
                    {
                        child.gameObject.layer = Layer;
                    }

                    created.Add(scale);
                }

                float minX = hero != null ? -3.6f : -2.4f;
                float maxX = (times.Length - 1) * spacing + 2.4f;
                const float pixelsPerMetre = 120f;
                int width = Mathf.RoundToInt((maxX - minX) * pixelsPerMetre);
                const int height = 660;
                var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                created.Add(target);

                var cameraObject = new GameObject("LeshyPreviewCamera");
                created.Add(cameraObject);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.cullingMask = 1 << Layer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.55f, 0.6f, 0.58f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = height * 0.5f / pixelsPerMetre;
                camera.aspect = (float)width / height;
                camera.transform.SetPositionAndRotation(new Vector3((minX + maxX) * 0.5f, 1000f + height * 0.5f / pixelsPerMetre - 0.1f, -30f), Quaternion.identity);
                camera.nearClipPlane = 1f;
                camera.farClipPlane = 80f;
                camera.targetTexture = target;

                var keyObject = new GameObject("LeshyKey");
                created.Add(keyObject);
                var key = keyObject.AddComponent<Light>();
                key.type = LightType.Directional;
                key.intensity = 1.5f;
                key.cullingMask = 1 << Layer;
                keyObject.transform.rotation = Quaternion.Euler(35f, 30f, 0f);
                var fillObject = new GameObject("LeshyFill");
                created.Add(fillObject);
                var fill = fillObject.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.intensity = 0.6f;
                fill.cullingMask = 1 << Layer;
                fillObject.transform.rotation = Quaternion.Euler(20f, -140f, 0f);

                camera.Render(); // the first render after setup can come out empty
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                camera.targetTexture = null;
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally
            {
                foreach (Object o in created)
                {
                    if (o != null)
                    {
                        Object.DestroyImmediate(o);
                    }
                }
            }
        }
    }
}
