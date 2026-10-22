using System.IO;
using UnityEditor;
using UnityEngine;
using NAV.Gameplay.Items;

namespace NAV.Editor
{
    /// <summary>
    /// Renders inventory icons from 3D models: every ItemDefinition with a HeldPrefab gets a
    /// transparent 256 px PNG of that model (diagonal, blade/head up-right, the usual
    /// survival-game icon pose) at Art/Icons/Rendered/Icon_{Id}.png, imported as a Sprite like
    /// the hand-made icons, and assigned as the item's Icon (and its weapon's, if any).
    /// Re-run after changing a model; items without a HeldPrefab keep their current icon.
    /// The model is rendered on its own layer, so nothing else in the open scene shows up.
    /// </summary>
    internal static class ItemIconRenderer
    {
        private const string OutputFolder = "Assets/NAV/Art/Icons/Rendered";
        private const int Size = 256;
        private const int IconLayer = 31;
        private const float Padding = 1.12f;

        [MenuItem("NAV/Items/Render Icons From Held Models")]
        private static void RenderAll()
        {
            var items = new System.Collections.Generic.List<ItemDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
            {
                items.Add(AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
            }

            RenderIcons(items);
        }

        /// <summary>Renders and assigns icons for the given items (those with a HeldPrefab).</summary>
        internal static void RenderIcons(System.Collections.Generic.IEnumerable<ItemDefinition> items)
        {
            Directory.CreateDirectory(OutputFolder);
            int rendered = 0;
            foreach (ItemDefinition item in items)
            {
                if (item == null || item.HeldPrefab == null)
                {
                    continue;
                }

                string path = $"{OutputFolder}/Icon_{item.Id}.png";
                File.WriteAllBytes(path, RenderIcon(item.HeldPrefab));
                AssetDatabase.ImportAsset(path);
                ConfigureSprite(path);

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                AssignIcon(item, sprite);
                if (item.Weapon != null)
                {
                    AssignIcon(item.Weapon, sprite);
                }

                rendered++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"{nameof(ItemIconRenderer)}: rendered {rendered} icon(s) into {OutputFolder}.");
        }

        /// <summary>Renders prefab to a transparent PNG. pose = its rotation in front of the camera
        /// (default: a held item tilted handle-down-left).</summary>
        internal static byte[] RenderIcon(GameObject prefab, Quaternion? pose = null)
        {
            var instance = Object.Instantiate(prefab);
            var cameraObject = new GameObject("IconCamera");
            var lightObject = new GameObject("IconLight");
            var fillObject = new GameObject("IconFill");
            var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            bool fog = RenderSettings.fog;
            try
            {
                RenderSettings.fog = false;
                instance.transform.position = new Vector3(0f, -10000f, 0f);
                // Handle (+Y) pointing up-right, so the blade/head sits in the top-right corner.
                instance.transform.rotation = pose ?? Quaternion.Euler(0f, 0f, -45f);
                foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = IconLayer;
                }

                Bounds bounds = GetBounds(instance);

                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.cullingMask = 1 << IconLayer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * Padding;
                camera.transform.position = bounds.center + Vector3.back * (bounds.extents.magnitude + 5f);
                camera.transform.rotation = Quaternion.identity;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = bounds.extents.magnitude * 2f + 10f;
                camera.targetTexture = target;

                var key = lightObject.AddComponent<Light>();
                key.type = LightType.Directional;
                key.intensity = 1.4f;
                key.cullingMask = 1 << IconLayer;
                lightObject.transform.rotation = Quaternion.Euler(35f, 25f, 0f);

                var fill = fillObject.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.intensity = 0.6f;
                fill.cullingMask = 1 << IconLayer;
                fillObject.transform.rotation = Quaternion.Euler(-20f, -150f, 0f);

                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                byte[] png = texture.EncodeToPNG();
                Object.DestroyImmediate(texture);
                return png;
            }
            finally
            {
                RenderSettings.fog = fog;
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(lightObject);
                Object.DestroyImmediate(fillObject);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static Bounds GetBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(root.transform.position, Vector3.one);
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        internal static void ConfigureSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void AssignIcon(Object asset, Sprite sprite)
        {
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("_icon").objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }
    }
}
