using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.AI.Navigation;
using NAV.Gameplay.Environment;
using NAV.Gameplay.Player;
using NAV.Presentation.Environment;
using NAV.World.Generation;

namespace NAV.Editor
{
    /// <summary>
    /// Sets up NAV's URP water (shader NAV/Water): builds Materials/World/NAVWater with SUIMONO's
    /// wave normal maps and foam mask (SUIMONO itself is built-in-pipeline only and isn't used
    /// otherwise), and puts it on the open scene's WorldGenerator water plane in place of the old
    /// flat Lit placeholder (renamed "Water", no shadow casting). Save the scene afterwards.
    /// </summary>
    internal static class WaterSetup
    {
        private const string MaterialPath = "Assets/NAV/Materials/World/NAVWater.mat";
        private const string Suimono = "Assets/SUIMONO - WATER SYSTEM 2/TEXTURES/";
        private const string SuimonoSounds = "Assets/SUIMONO - WATER SYSTEM 2/SOUNDS/";
        private const string ParticleMaterialPath = "Assets/NAV/Materials/Combat/HitParticle.mat";

        [MenuItem("NAV/World/Build Water")]
        private static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/NAV/Materials/World"))
            {
                AssetDatabase.CreateFolder("Assets/NAV/Materials", "World");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("NAV/Water")) { name = "NAVWater" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.shader = Shader.Find("NAV/Water");
            material.SetTexture("_WaveNormalA", AssetDatabase.LoadAssetAtPath<Texture2D>(Suimono + "tex_WaveCalm_normal.psd"));
            material.SetTexture("_WaveNormalB", AssetDatabase.LoadAssetAtPath<Texture2D>(Suimono + "tex_WaveRoll_normal.psd"));
            material.SetTexture("_FoamMask", AssetDatabase.LoadAssetAtPath<Texture2D>(Suimono + "tex_foamMask.psd"));
            // A touch of teal - the first navy deep read as gloomy for fairy-tale lakes.
            material.SetColor("_DeepColor", new Color(0.07f, 0.26f, 0.36f));
            EditorUtility.SetDirty(material);

            WorldGenerator generator = Object.FindAnyObjectByType<WorldGenerator>();
            Transform water = generator != null ? new SerializedObject(generator).FindProperty("_waterPlane").objectReferenceValue as Transform : null;
            if (water == null)
            {
                AssetDatabase.SaveAssets();
                Debug.LogWarning("WaterSetup: material built, but the open scene has no WorldGenerator water plane to put it on.");
                return;
            }

            water.name = "Water";
            var renderer = water.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            EditorUtility.SetDirty(renderer);

            // Water is no longer solid: no collider, a WaterBody instead, and kept out of the
            // NavMesh bake (it bakes render meshes - the surface would become walkable).
            foreach (Collider collider in water.GetComponents<Collider>())
            {
                Object.DestroyImmediate(collider);
            }

            GetOrAdd<WaterBody>(water.gameObject);
            NavMeshModifier modifier = GetOrAdd<NavMeshModifier>(water.gameObject);
            modifier.ignoreFromBuild = true;
            EditorUtility.SetDirty(modifier);

            // Lake beds aren't walkable either - the generator stretches this over them.
            var generatorSerialized = new SerializedObject(generator);
            SerializedProperty blockProperty = generatorSerialized.FindProperty("_underwaterNavBlock");
            var block = blockProperty.objectReferenceValue as NavMeshModifierVolume;
            if (block == null)
            {
                block = new GameObject("Underwater NavMesh Block").AddComponent<NavMeshModifierVolume>();
                blockProperty.objectReferenceValue = block;
                generatorSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            block.area = NavMesh.GetAreaFromName("Not Walkable");
            EditorUtility.SetDirty(block);

            // The reaction to the player: rings, splashes, sloshing.
            PlayerMotor player = Object.FindAnyObjectByType<PlayerMotor>(FindObjectsInactive.Include);
            WaterInteraction interaction = Object.FindAnyObjectByType<WaterInteraction>(FindObjectsInactive.Include);
            if (interaction == null)
            {
                interaction = new GameObject("Water Interaction").AddComponent<WaterInteraction>();
            }

            var interactionSerialized = new SerializedObject(interaction);
            interactionSerialized.FindProperty("_player").objectReferenceValue = player;
            interactionSerialized.FindProperty("_particleMaterialTemplate").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
            interactionSerialized.FindProperty("_bigSplash").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SuimonoSounds + "water_bigsplash.wav");
            SerializedProperty splashes = interactionSerialized.FindProperty("_splashes");
            splashes.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                splashes.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SuimonoSounds}water_splash0{i + 1}.wav");
            }

            interactionSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(water.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"WaterSetup: NAV/Water on '{water.name}', WaterBody, NavMesh exclusions and Water Interaction{(player == null ? " (no PlayerMotor found - assign it)" : "")} - save the scene.");
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            return target.TryGetComponent(out T existing) ? existing : target.AddComponent<T>();
        }
    }
}
