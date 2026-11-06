using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Gameplay.Player;
using NAV.Presentation.Audio;
using NAV.World.Biomes;
using NAV.World.Generation;
using NAV.World.TimeOfDay;
using NAV.World.Weather;

namespace NAV.Editor
{
    /// <summary>
    /// NAV > Audio > Set Up Ambience and Footsteps - wires the two sound packs the developer
    /// imported (2026-10-25):
    ///   - "Nature - Essentials" loops -> each biome's day/night/wind ambience (plains: cicadas and
    ///     calm wind; forest and birch grove: birds; dense forest: fewer birds, wind in the trees;
    ///     water: the sea; night: crickets), the rain (calm/strong) -> AmbienceController in the
    ///     scene, and fire loops on the campfire, forge, charcoal kiln and bloomery (AmbientEmitter);
    ///   - "Footsteps Volume 02" -> FootstepProfile (grass on the plains and groves, the
    ///     alternative grass on the forest floor, soft ground in the dense forest, sand on the
    ///     shore, wood on buildings, stone on rocks, mud when wading) -> FootstepAudio on the player.
    /// Import settings: long loops stream (Vorbis), footsteps are mono and decompressed on load.
    /// Cave, river, stream and waterfall loops wait for caves and rivers. Safe to re-run.
    /// </summary>
    internal static class AudioSetup
    {
        private const string NatureFolder = "Assets/Nature - Essentials/";
        private const string StepsFolder = "Assets/The_Sound_Guild_FREE_PACK_Footsteps_Volume_02/WAV - 44100 Hz - 16 Bit/";
        private const string BiomeFolder = "Assets/NAV/ScriptableObjects/Biomes/";
        private const string ProfilePath = "Assets/NAV/ScriptableObjects/Audio/FootstepProfile.asset";

        [MenuItem("NAV/Audio/Set Up Ambience and Footsteps")]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("AudioSetup: exit Play Mode first.");
                return;
            }

            ConfigureImports();

            AudioClip birds = Nature("Ambiance_Forest_Birds_Loop_Stereo");
            AudioClip cicadas = Nature("Ambiance_Cicadas_Loop_Stereo");
            AudioClip night = Nature("Ambiance_Night_Loop_Stereo");
            AudioClip sea = Nature("Ambiance_Sea_Loop_Stereo");
            AudioClip windCalm = Nature("Ambiance_Wind_Calm_Loop_Stereo");
            AudioClip windForest = Nature("Ambiance_Wind_Forest_Loop_Stereo");

            SetBiome("PlainsBiome", cicadas, night, 0.3f, windCalm, 0.3f);
            SetBiome("ForestBiome", birds, night, 0.55f, windForest, 0.3f);
            SetBiome("BirchGroveBiome", birds, night, 0.6f, windCalm, 0.25f);
            SetBiome("DenseForestBiome", birds, night, 0.25f, windForest, 0.5f);
            SetBiome("OceanBiome", sea, sea, 0.7f, windCalm, 0.35f);

            FootstepProfile profile = BuildFootstepProfile();

            AddEmitter("Campfire", Nature("Ambiance_Firecamp_Small_Loop_Mono"), 0.55f, 18f);
            AddEmitter("Forge", Nature("Ambiance_Firecamp_Medium_Loop_Mono"), 0.6f, 22f);
            AddEmitter("CharcoalKiln", Nature("Ambiance_Firecamp_Medium_Loop_Mono"), 0.5f, 20f);
            AddEmitter("Bloomery", Nature("Ambiance_Fire_Big_Loop_Mono"), 0.55f, 25f);

            WireScene(profile, Nature("Ambiance_Rain_Calm_Loop_Stereo"), Nature("Ambiance_Rain_Strong_Loop_Stereo"));
            AssetDatabase.SaveAssets();
            Debug.Log("AudioSetup: biome ambience, rain, fire loops and footsteps set up.");
        }

        private static AudioClip Nature(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{NatureFolder}{name}.wav");

        private static AudioClip[] Steps(string prefix)
        {
            var clips = new List<AudioClip>();
            foreach (string path in Directory.GetFiles(StepsFolder, prefix + "*.wav"))
            {
                // "Grass_" must not also pick up "Grass_Alternative_".
                string rest = Path.GetFileNameWithoutExtension(path).Substring(prefix.Length);
                if (int.TryParse(rest, out _))
                {
                    clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path.Replace('\\', '/')));
                }
            }

            return clips.ToArray();
        }

        private static void ConfigureImports()
        {
            foreach (string path in Directory.GetFiles(NatureFolder, "*.wav"))
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
                importer.defaultSampleSettings = settings;
                importer.loadInBackground = true;
                importer.SaveAndReimport();
            }

            foreach (string path in Directory.GetFiles(StepsFolder, "*.wav"))
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.ADPCM;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
        }

        private static void SetBiome(string biomeName, AudioClip day, AudioClip nightClip, float volume, AudioClip wind, float windVolume)
        {
            var biome = AssetDatabase.LoadAssetAtPath<BiomeDefinition>($"{BiomeFolder}{biomeName}.asset");
            if (biome == null)
            {
                Debug.LogWarning($"AudioSetup: biome {biomeName} not found - skipped.");
                return;
            }

            var serialized = new SerializedObject(biome);
            serialized.FindProperty("_dayAmbience").objectReferenceValue = day;
            serialized.FindProperty("_nightAmbience").objectReferenceValue = nightClip;
            serialized.FindProperty("_ambienceVolume").floatValue = volume;
            serialized.FindProperty("_windAmbience").objectReferenceValue = wind;
            serialized.FindProperty("_windVolume").floatValue = windVolume;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(biome);
        }

        private static FootstepProfile BuildFootstepProfile()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath));
            var profile = AssetDatabase.LoadAssetAtPath<FootstepProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<FootstepProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            AudioClip[] grass = Steps("Foostep_Grass_");
            var terrain = new List<(string biome, AudioClip[] clips)>
            {
                ("PlainsBiome", grass),
                ("BirchGroveBiome", grass),
                ("ForestBiome", Steps("Footstep_Grass_Alternative_")),
                ("DenseForestBiome", Steps("Footstep_Soft_Surface_")),
                ("OceanBiome", Steps("Footstep_Sand_")),
                ("SeaIslandsBiome", Steps("Footstep_Sand_")),
            };

            var serialized = new SerializedObject(profile);
            SerializedProperty list = serialized.FindProperty("_terrain");
            list.arraySize = terrain.Count;
            for (int i = 0; i < terrain.Count; i++)
            {
                var biome = AssetDatabase.LoadAssetAtPath<BiomeDefinition>($"{BiomeFolder}{terrain[i].biome}.asset");
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Layer").objectReferenceValue = biome != null ? biome.TerrainLayer : null;
                SetClips(entry.FindPropertyRelative("Clips"), terrain[i].clips);
            }

            SetClips(serialized.FindProperty("_wood"), Steps("Footstep_Wood_"));
            SetClips(serialized.FindProperty("_stone"), Steps("Footstep_Shoe_On_Street_"));
            SetClips(serialized.FindProperty("_water"), Steps("Footstep_Mud_"));
            SetClips(serialized.FindProperty("_default"), grass);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void SetClips(SerializedProperty array, AudioClip[] clips)
        {
            array.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }
        }

        private static void AddEmitter(string prefabName, AudioClip clip, float volume, float maxDistance)
        {
            string path = $"Assets/NAV/Prefabs/Build/{prefabName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null || clip == null)
            {
                Debug.LogWarning($"AudioSetup: {prefabName} or its fire loop not found - skipped.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!root.TryGetComponent(out AmbientEmitter emitter))
                {
                    emitter = root.AddComponent<AmbientEmitter>();
                }

                var serialized = new SerializedObject(emitter);
                serialized.FindProperty("_clip").objectReferenceValue = clip;
                serialized.FindProperty("_volume").floatValue = volume;
                serialized.FindProperty("_maxDistance").floatValue = maxDistance;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WireScene(FootstepProfile profile, AudioClip lightRain, AudioClip heavyRain)
        {
            WorldGenerator generator = Object.FindAnyObjectByType<WorldGenerator>(FindObjectsInactive.Include);
            WorldClock clock = Object.FindAnyObjectByType<WorldClock>(FindObjectsInactive.Include);
            WeatherSystem weather = Object.FindAnyObjectByType<WeatherSystem>(FindObjectsInactive.Include);
            PlayerMotor player = Object.FindAnyObjectByType<PlayerMotor>(FindObjectsInactive.Include);
            if (clock == null || weather == null)
            {
                Debug.LogWarning("AudioSetup: no WorldClock/WeatherSystem in the open scene - add AmbienceController by hand.");
                return;
            }

            AmbienceController ambience = Object.FindAnyObjectByType<AmbienceController>(FindObjectsInactive.Include);
            if (ambience == null)
            {
                ambience = new GameObject("Ambience").AddComponent<AmbienceController>();
            }

            var serialized = new SerializedObject(ambience);
            serialized.FindProperty("_worldGenerator").objectReferenceValue = generator;
            serialized.FindProperty("_clock").objectReferenceValue = clock;
            serialized.FindProperty("_weather").objectReferenceValue = weather;
            serialized.FindProperty("_lightRain").objectReferenceValue = lightRain;
            serialized.FindProperty("_heavyRain").objectReferenceValue = heavyRain;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (player != null)
            {
                if (!player.TryGetComponent(out FootstepAudio footsteps))
                {
                    footsteps = player.gameObject.AddComponent<FootstepAudio>();
                }

                var steps = new SerializedObject(footsteps);
                steps.FindProperty("_profile").objectReferenceValue = profile;
                steps.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(clock.gameObject.scene);
            EditorSceneManager.SaveScene(clock.gameObject.scene);
        }
    }
}
