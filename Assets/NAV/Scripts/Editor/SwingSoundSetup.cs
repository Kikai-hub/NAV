using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Gameplay.Combat;
using NAV.Presentation.Combat;

namespace NAV.Editor
{
    /// <summary>
    /// Builds ScriptableObjects/Audio/SwingSoundSet.asset from the "Whooshes SFX Pack LITE"
    /// and assigns it to the open scene's HitFeedback. The pack has 4 lengths (Extra Fast,
    /// Short, Medium, Long) x Standard/Heavy, 3 clips each; measured: audible whoosh ~0.1 s /
    /// 0.15 s / 0.3 s / 0.7 s, Heavy = darker. Matching rule: the whoosh length follows how long
    /// the weapon is actually moving fast (measured strike timings), Heavy for blunt/heavy heads,
    /// pitch separates weapons sharing a clip family. The two-handed chop strikes at a fixed
    /// 0.445 s (at animation speed 1 - measured for every two-handed class), so its whoosh
    /// starts during the backswing; other styles play on the real strike.
    /// Also sets the clips to mono (they're played as 3D sounds). Re-run to re-apply.
    /// </summary>
    internal static class SwingSoundSetup
    {
        private const string PackFolder = "Assets/Whooshes SFX Pack LITE Edition Unity";
        private const string AssetFolder = "Assets/NAV/ScriptableObjects/Audio";
        private const string AssetPath = AssetFolder + "/SwingSoundSet.asset";
        private const float TwoHandedStrikeTime = 0.445f;

        // Class -> (pack sub-folder, volume, pitch).
        private static readonly (WeaponClass weaponClass, string folder, float volume, float pitch)[] Mapping =
        {
            (WeaponClass.Unarmed,    "Extra Fast/Light",    0.45f, 1.05f), // quick jab, light swish
            (WeaponClass.Dagger,     "Extra Fast/Light",    0.5f,  1.12f), // tiny blade, brightest and fastest
            (WeaponClass.Spear,      "Extra Fast/Standard", 0.55f, 0.95f), // short sharp thrust
            (WeaponClass.Sword,      "Short/Standard",      0.6f,  1f),    // slash, strike lasts ~0.1 s
            (WeaponClass.Mace,       "Short/Heavy",         0.65f, 0.95f), // blunt one-hander
            (WeaponClass.Shield,     "Short/Heavy",         0.6f,  0.85f), // broad bash
            (WeaponClass.HandAxe,    "Medium/Standard",     0.6f,  1f),    // light overhead chop
            (WeaponClass.Tool,       "Medium/Heavy",        0.6f,  1f),    // pickaxe: heavy head, light haft
            (WeaponClass.Greatsword, "Long/Standard",       0.7f,  1f),    // big blade, long arc
            (WeaponClass.BattleAxe,  "Long/Heavy",          0.7f,  1f),    // heavy two-handed axe
            (WeaponClass.Warhammer,  "Long/Heavy",          0.75f, 0.88f), // heaviest - same family, lower
        };

        [MenuItem("NAV/Setup/Build Swing Sounds")]
        private static void Build()
        {
            if (!AssetDatabase.IsValidFolder(AssetFolder))
            {
                AssetDatabase.CreateFolder("Assets/NAV/ScriptableObjects", "Audio");
            }

            var set = AssetDatabase.LoadAssetAtPath<SwingSoundSet>(AssetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<SwingSoundSet>();
                AssetDatabase.CreateAsset(set, AssetPath);
            }

            var serialized = new SerializedObject(set);
            SerializedProperty entries = serialized.FindProperty("_entries");
            entries.ClearArray();
            foreach (var row in Mapping)
            {
                List<AudioClip> clips = LoadClips($"{PackFolder}/{row.folder}");
                if (clips.Count == 0)
                {
                    Debug.LogError($"SwingSoundSetup: no clips in '{PackFolder}/{row.folder}'.");
                    continue;
                }

                entries.arraySize++;
                SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                entry.FindPropertyRelative("WeaponClass").intValue = (int)row.weaponClass;
                entry.FindPropertyRelative("Volume").floatValue = row.volume;
                entry.FindPropertyRelative("Pitch").floatValue = row.pitch;
                SerializedProperty clipArray = entry.FindPropertyRelative("Clips");
                clipArray.arraySize = clips.Count;
                for (int i = 0; i < clips.Count; i++)
                {
                    clipArray.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
                }
            }

            SerializedProperty timings = serialized.FindProperty("_styleTimings");
            timings.arraySize = 1;
            timings.GetArrayElementAtIndex(0).FindPropertyRelative("Style").intValue = (int)WeaponAttackStyle.TwoHanded;
            timings.GetArrayElementAtIndex(0).FindPropertyRelative("StrikeTime").floatValue = TwoHandedStrikeTime;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();

            HitFeedback feedback = Object.FindAnyObjectByType<HitFeedback>(FindObjectsInactive.Include);
            if (feedback != null)
            {
                var feedbackSerialized = new SerializedObject(feedback);
                feedbackSerialized.FindProperty("_swingSounds").objectReferenceValue = set;
                feedbackSerialized.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(feedback.gameObject.scene);
            }

            Debug.Log($"SwingSoundSetup: {entries.arraySize} weapon classes mapped in {AssetPath}" + (feedback != null ? $", assigned to '{feedback.name}' - save the scene." : " (no HitFeedback in the open scene)."));
        }

        /// <summary>The folder's clips, set to mono + decompress-on-load (3D sounds whose
        /// samples HitFeedback reads to find the peak).</summary>
        private static List<AudioClip> LoadClips(string folder)
        {
            var clips = new List<AudioClip>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                if (!importer.forceToMono || settings.loadType != AudioClipLoadType.DecompressOnLoad)
                {
                    importer.forceToMono = true;
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    importer.defaultSampleSettings = settings;
                    importer.SaveAndReimport();
                }

                clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }

            return clips;
        }
    }
}
