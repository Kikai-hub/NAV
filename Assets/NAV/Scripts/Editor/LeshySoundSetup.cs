using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Gameplay.AI;
using NAV.Gameplay.Bosses;
using NAV.Presentation.Animation;
using NAV.Presentation.Audio;
using NAV.World.Encounters;

namespace NAV.Editor
{
    /// <summary>
    /// NAV > Audio > Build Leshy Sounds - PLACEHOLDER sound design for the Leshy until real
    /// recordings exist: synthesised WAVs in Audio/Leshy/Placeholder (wood creaks from a
    /// stick-slip pulse train through wood resonances, booms, cracks, rustle, a low growl, the
    /// omen whistle) plus the Whooshes pack's heavy whooshes for the sweep, put into
    /// LeshySoundSet. Then wires LeshyAudio on the boss prefab, ForestWrathAudio on the scene's
    /// ForestWrath and the forest's silence into AmbienceController. To use real sounds, drag
    /// them into the LeshySoundSet slots - re-running this keeps slots that hold non-placeholder
    /// clips. Re-runnable.
    /// </summary>
    internal static class LeshySoundSetup
    {
        private const string Folder = "Assets/NAV/Audio/Leshy/Placeholder/";
        private const string SoundSetPath = "Assets/NAV/ScriptableObjects/Audio/LeshySoundSet.asset";
        private const string BossPrefabPath = "Assets/NAV/Prefabs/Bosses/Leshy.prefab";
        private const string WhooshFolder = "Assets/Whooshes SFX Pack LITE Edition Unity/";
        private const int Rate = 44100;

        [MenuItem("NAV/Audio/Build Leshy Sounds")]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("LeshySoundSetup: exit Play Mode first.");
                return;
            }

            Directory.CreateDirectory(Folder);
            var made = new Dictionary<string, AudioClip[]>
            {
                ["Presence"] = Make("Leshy_Presence_Loop", 1, i => Presence(8f, 11)),
                ["Footstep"] = Make("Leshy_Footstep", 3, i => Footstep(100 + i)),
                ["Roar"] = Make("Leshy_Roar", 2, i => Roar(200 + i)),
                ["SlamWindup"] = Make("Leshy_SlamWindup", 2, i => Strain(300 + i)),
                ["SlamImpact"] = Make("Leshy_SlamImpact", 2, i => Impact(400 + i, 1f)),
                ["Hurt"] = Make("Leshy_Hurt", 3, i => Groan(500 + i)),
                ["DeathCreak"] = Make("Leshy_DeathCreak", 1, i => FallingCreak(600)),
                ["DeathImpact"] = Make("Leshy_DeathImpact", 1, i => Impact(700, 1.6f)),
                ["Omen"] = Make("Leshy_Omen_Whistle", 2, i => Whistle(800 + i)),
                ["PeacefulOmen"] = Make("Leshy_Omen_Creak", 2, i => DistantCreak(900 + i)),
            };

            AssetDatabase.Refresh();
            foreach (AudioClip[] clips in made.Values)
            {
                foreach (AudioClip clip in clips)
                {
                    ConfigureImport(AssetDatabase.GetAssetPath(clip), clip.name.Contains("Loop"));
                }
            }

            var whooshes = new List<AudioClip>();
            foreach (string name in new[] { "Medium/Heavy/MOVEActv_MediumHeavy_HA_WHOOSH_04", "Medium/Heavy/MOVEActv_MediumHeavy_HA_WHOOSH_09", "Long/Heavy/MOVEActv_LongHeavy_HA_WHOOSH_07" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(WhooshFolder + name + ".wav");
                if (clip != null)
                {
                    whooshes.Add(clip);
                }
            }

            LeshySoundSet set = BuildSoundSet(made, whooshes.ToArray());
            WirePrefab(set);
            WireScene(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"LeshySoundSetup: {made.Count} placeholder sounds + {whooshes.Count} whooshes -> {SoundSetPath}; boss, ForestWrath and ambience wired.");
        }

        // ---------------------------------------------------------------- wiring

        private static LeshySoundSet BuildSoundSet(Dictionary<string, AudioClip[]> made, AudioClip[] whooshes)
        {
            var set = AssetDatabase.LoadAssetAtPath<LeshySoundSet>(SoundSetPath);
            if (set == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/NAV/ScriptableObjects/Audio"))
                {
                    AssetDatabase.CreateFolder("Assets/NAV/ScriptableObjects", "Audio");
                }

                set = ScriptableObject.CreateInstance<LeshySoundSet>();
                AssetDatabase.CreateAsset(set, SoundSetPath);
            }

            Fill(set.Presence, made["Presence"]);
            Fill(set.Footstep, made["Footstep"]);
            Fill(set.Roar, made["Roar"]);
            Fill(set.SlamWindup, made["SlamWindup"]);
            Fill(set.SlamImpact, made["SlamImpact"]);
            Fill(set.Hurt, made["Hurt"]);
            Fill(set.DeathCreak, made["DeathCreak"]);
            Fill(set.DeathImpact, made["DeathImpact"]);
            Fill(set.Omen, made["Omen"]);
            Fill(set.PeacefulOmen, made["PeacefulOmen"]);
            if (whooshes.Length > 0)
            {
                Fill(set.SweepWhoosh, whooshes);
            }

            EditorUtility.SetDirty(set);
            return set;
        }

        /// <summary>Puts the placeholders in a slot unless the developer already put real clips there.</summary>
        private static void Fill(LeshySoundSet.Slot slot, AudioClip[] clips)
        {
            bool real = false;
            foreach (AudioClip clip in slot.Clips ?? new AudioClip[0])
            {
                if (clip != null && !AssetDatabase.GetAssetPath(clip).StartsWith(Folder) && !AssetDatabase.GetAssetPath(clip).StartsWith(WhooshFolder))
                {
                    real = true;
                }
            }

            if (!real)
            {
                slot.Clips = clips;
            }
        }

        private static void WirePrefab(LeshySoundSet set)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            try
            {
                var audio = root.GetComponent<LeshyAudio>() ?? root.AddComponent<LeshyAudio>();
                var serialized = new SerializedObject(audio);
                serialized.FindProperty("_sounds").objectReferenceValue = set;
                serialized.FindProperty("_creature").objectReferenceValue = root.GetComponent<Creature>();
                serialized.FindProperty("_boss").objectReferenceValue = root.GetComponent<LeshyBoss>();
                serialized.FindProperty("_legs").objectReferenceValue = root.GetComponent<LegGrounding>();
                serialized.ApplyModifiedPropertiesWithoutUndo();

                // Dust, clods and leaves for the same blows (2026-11-13) - wires itself.
                if (root.GetComponent<NAV.Presentation.Combat.LeshyDust>() == null)
                {
                    root.AddComponent<NAV.Presentation.Combat.LeshyDust>();
                }
                PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WireScene(LeshySoundSet set)
        {
            var wrath = UnityEngine.Object.FindAnyObjectByType<ForestWrath>(FindObjectsInactive.Include);
            var ambience = UnityEngine.Object.FindAnyObjectByType<AmbienceController>(FindObjectsInactive.Include);
            if (wrath == null)
            {
                Debug.LogError("LeshySoundSetup: no ForestWrath in the open scene (open WorldGenTest) - omens and silence NOT wired.");
                return;
            }

            var omens = wrath.GetComponent<ForestWrathAudio>() ?? wrath.gameObject.AddComponent<ForestWrathAudio>();
            var serialized = new SerializedObject(omens);
            serialized.FindProperty("_forestWrath").objectReferenceValue = wrath;
            serialized.FindProperty("_sounds").objectReferenceValue = set;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (ambience != null)
            {
                var ambienceSerialized = new SerializedObject(ambience);
                ambienceSerialized.FindProperty("_forestWrath").objectReferenceValue = wrath;
                ambienceSerialized.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("LeshySoundSetup: no AmbienceController in the scene - the forest won't fall silent.");
            }

            EditorSceneManager.MarkSceneDirty(wrath.gameObject.scene);
            EditorSceneManager.SaveScene(wrath.gameObject.scene);
        }

        private static void ConfigureImport(string path, bool loop)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                return;
            }

            importer.forceToMono = true;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        // ---------------------------------------------------------------- files

        private static AudioClip[] Make(string name, int count, Func<int, float[]> synth)
        {
            var clips = new AudioClip[count];
            for (int i = 0; i < count; i++)
            {
                string path = Folder + (count == 1 ? name : $"{name}_{i + 1}") + ".wav";
                float[] samples = synth(i);
                RemoveDc(samples);
                Normalize(samples, 0.9f);
                WriteWav(path, samples);
                AssetDatabase.ImportAsset(path);
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }

            return clips;
        }

        private static void WriteWav(string path, float[] samples)
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);
            int bytes = samples.Length * 2;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + bytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(Rate);
            writer.Write(Rate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(bytes);
            foreach (float s in samples)
            {
                writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
            }
        }

        /// <summary>DC blocker (one-pole high-pass ~7 Hz) - the resonators let the pulses' offset
        /// through, which clicks at a clip's ends.</summary>
        private static void RemoveDc(float[] samples)
        {
            float x1 = 0f;
            float y1 = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float y = samples[i] - x1 + 0.999f * y1;
                x1 = samples[i];
                y1 = y;
                samples[i] = y;
            }
        }

        private static void Normalize(float[] samples, float peak)
        {
            float max = 1e-6f;
            foreach (float s in samples)
            {
                max = Mathf.Max(max, Mathf.Abs(s));
            }

            float gain = peak / max;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= gain;
            }
        }

        // ---------------------------------------------------------------- synthesis

        /// <summary>Two-pole resonator (band-pass) - a body resonance of the wood.</summary>
        private sealed class Resonator
        {
            private readonly float _a1;
            private readonly float _a2;
            private readonly float _gain;
            private float _y1;
            private float _y2;

            public Resonator(float frequency, float bandwidth)
            {
                float r = Mathf.Exp(-Mathf.PI * bandwidth / Rate);
                _a1 = 2f * r * Mathf.Cos(2f * Mathf.PI * frequency / Rate);
                _a2 = -r * r;
                _gain = 1f - r;
            }

            public float Tick(float x)
            {
                float y = _gain * x + _a1 * _y1 + _a2 * _y2;
                _y2 = _y1;
                _y1 = y;
                return y;
            }
        }

        private static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        /// <summary>Wood creak: stick-slip clicks at rate(t) Hz (jittered) ringing three wood
        /// resonances. resonance scales the body (lower = a bigger trunk).</summary>
        private static void AddCreak(float[] buffer, int seed, float start, float seconds, Func<float, float> rate, float gain, float resonance = 1f)
        {
            var random = new System.Random(seed);
            var bodies = new[] { new Resonator(230f * resonance, 35f), new Resonator(560f * resonance, 70f), new Resonator(1350f * resonance, 150f) };
            float[] mix = { 1f, 0.7f, 0.35f };
            int from = Mathf.RoundToInt(start * Rate);
            int length = Mathf.RoundToInt(seconds * Rate);
            float phase = 0f;
            float pulse = 1f;
            for (int i = 0; i < length && from + i < buffer.Length; i++)
            {
                float t = i / (float)Rate;
                float u = t / seconds;
                phase += rate(u) / Rate;
                float x = 0f;
                if (phase >= 1f)
                {
                    phase -= 1f + (float)(random.NextDouble() - 0.5) * 0.45f;
                    pulse = 0.5f + (float)random.NextDouble() * 0.5f;
                    x = pulse * 40f;
                }

                x += ((float)random.NextDouble() - 0.5f) * 0.4f; // grit
                float y = 0f;
                for (int b = 0; b < bodies.Length; b++)
                {
                    y += bodies[b].Tick(x) * mix[b];
                }

                float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u)) * (0.6f + 0.4f * Mathf.Sin(u * 17f + seed));
                buffer[from + i] += y * gain * Mathf.Max(0f, envelope);
            }
        }

        /// <summary>Low boom: a falling sine with a soft noise thump.</summary>
        private static void AddBoom(float[] buffer, int seed, float start, float seconds, float fromHz, float toHz, float gain)
        {
            var random = new System.Random(seed);
            int from = Mathf.RoundToInt(start * Rate);
            float phase = 0f;
            float low = 0f;
            for (int i = 0; from + i < buffer.Length && i < seconds * Rate; i++)
            {
                float t = i / (float)Rate;
                float frequency = Mathf.Lerp(fromHz, toHz, Mathf.Clamp01(t / (seconds * 0.6f)));
                phase += frequency / Rate;
                low += 0.02f * (((float)random.NextDouble() - 0.5f) - low);
                float decay = Mathf.Exp(-t * 4.5f / seconds);
                buffer[from + i] += (Mathf.Sin(2f * Mathf.PI * phase) * 0.9f + low * 6f * Mathf.Exp(-t * 18f)) * decay * gain * Mathf.Clamp01(t * 400f);
            }
        }

        /// <summary>Cracks: short bright bursts (snapping wood, splitting earth).</summary>
        private static void AddCracks(float[] buffer, int seed, float start, float spread, int count, float gain)
        {
            var random = new System.Random(seed);
            for (int c = 0; c < count; c++)
            {
                int from = Mathf.RoundToInt((start + (float)random.NextDouble() * spread) * Rate);
                int length = Mathf.RoundToInt((0.006f + (float)random.NextDouble() * 0.02f) * Rate);
                float amplitude = gain * (0.4f + (float)random.NextDouble() * 0.6f);
                float previous = 0f;
                var ring = new Resonator(1800f + (float)random.NextDouble() * 1800f, 400f);
                for (int i = 0; i < length * 4 && from + i < buffer.Length; i++)
                {
                    float noise = i < length ? (float)random.NextDouble() * 2f - 1f : 0f;
                    float high = noise - previous;
                    previous = noise;
                    buffer[from + i] += (high * 0.6f + ring.Tick(noise) * 3f) * amplitude * Mathf.Exp(-i / (float)length);
                }
            }
        }

        /// <summary>Rustle: leaves and twigs - band-limited noise with a ragged swell.</summary>
        private static void AddRustle(float[] buffer, int seed, float start, float seconds, float gain, float attack = 0.3f)
        {
            var random = new System.Random(seed);
            int from = Mathf.RoundToInt(start * Rate);
            float low = 0f;
            float band = 0f;
            float swell = 0f;
            for (int i = 0; from + i < buffer.Length && i < seconds * Rate; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                low += 0.35f * (noise - low);
                band = low - band * 0.15f;
                if (i % 900 == 0)
                {
                    swell = (float)random.NextDouble();
                }

                float envelope = Mathf.Clamp01(t / attack) * Mathf.Clamp01((seconds - t) / 0.6f);
                buffer[from + i] += band * gain * envelope * (0.35f + 0.65f * swell);
            }
        }

        /// <summary>Low growl: a jittery sawtooth through a throat formant, with breath.</summary>
        private static void AddGrowl(float[] buffer, int seed, float start, float seconds, float gain)
        {
            var random = new System.Random(seed);
            var throat = new Resonator(380f, 160f);
            var chest = new Resonator(140f, 90f);
            int from = Mathf.RoundToInt(start * Rate);
            float phase = 0f;
            float jitter = 0f;
            float low = 0f;
            for (int i = 0; from + i < buffer.Length && i < seconds * Rate; i++)
            {
                float t = i / (float)Rate;
                float u = t / seconds;
                if (i % 400 == 0)
                {
                    jitter = ((float)random.NextDouble() - 0.5f) * 0.25f;
                }

                float frequency = (52f + 22f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.4f))) * (1f + jitter);
                phase += frequency / Rate;
                float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
                float sub = Mathf.Sin(Mathf.PI * phase);
                float breath = (float)random.NextDouble() * 2f - 1f;
                low += 0.05f * (saw - low);
                float voice = throat.Tick(saw) * 6f + chest.Tick(saw) * 5f + low * 0.6f + sub * 0.35f + breath * 0.08f;
                float envelope = Mathf.Clamp01(t / 0.35f) * Mathf.Clamp01((seconds - t) / 0.9f);
                buffer[from + i] += voice * gain * envelope;
            }
        }

        private static void AddEcho(float[] buffer, float delaySeconds, float feedback, int taps)
        {
            int delay = Mathf.RoundToInt(delaySeconds * Rate);
            for (int tap = 0; tap < taps; tap++)
            {
                for (int i = buffer.Length - 1; i >= delay; i--)
                {
                    buffer[i] += buffer[i - delay] * feedback;
                }
            }
        }

        private static void LowPass(float[] buffer, float amount)
        {
            float y = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                y += amount * (buffer[i] - y);
                buffer[i] = y;
            }
        }

        // ---- the sounds

        private static float[] Presence(float seconds, int seed)
        {
            float[] b = Buffer(seconds);
            var random = new System.Random(seed);
            AddRustle(b, seed, 0f, seconds, 0.25f, 0.01f);
            // Creaks spaced through the loop, away from its ends so the seam is quiet.
            for (float t = 0.6f; t < seconds - 1.6f; t += 1.6f + (float)random.NextDouble() * 1.2f)
            {
                AddCreak(b, seed + (int)(t * 10), t, 0.7f + (float)random.NextDouble() * 0.6f, u => 18f + 40f * u, 0.5f, 0.6f);
            }

            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                b[i] += Mathf.Sin(2f * Mathf.PI * 42f * t) * 0.05f; // the hum - a whole number of cycles over 8 s
            }

            // Ends faded into each other for a clean loop.
            int fade = Rate / 4;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                b[i] = b[i] * k + b[b.Length - fade + i] * (1f - k);
            }

            Array.Resize(ref b, b.Length - fade);
            return b;
        }

        private static float[] Footstep(int seed)
        {
            float[] b = Buffer(0.9f);
            AddBoom(b, seed, 0f, 0.7f, 75f, 38f, 1f);
            AddCracks(b, seed, 0.01f, 0.08f, 4, 0.25f);
            AddRustle(b, seed, 0f, 0.5f, 0.35f, 0.02f);
            AddCreak(b, seed, 0.05f, 0.35f, u => 35f + 20f * u, 0.25f, 0.55f);
            return b;
        }

        private static float[] Roar(int seed)
        {
            float[] b = Buffer(3.6f);
            AddGrowl(b, seed, 0f, 3f, 1f);
            AddCreak(b, seed, 0.1f, 2.8f, u => 22f + 70f * Mathf.Sin(Mathf.PI * u), 0.9f, 0.5f);
            AddBoom(b, seed, 0.05f, 1.2f, 60f, 32f, 0.6f);
            AddRustle(b, seed, 0.4f, 3f, 0.4f, 0.6f);
            AddEcho(b, 0.21f, 0.28f, 2);
            return b;
        }

        private static float[] Strain(int seed)
        {
            float[] b = Buffer(1.8f);
            AddCreak(b, seed, 0f, 1.7f, u => 14f + 95f * u * u, 1f, 0.7f);
            AddRustle(b, seed, 0f, 1.6f, 0.2f, 0.5f);
            return b;
        }

        private static float[] Impact(int seed, float size)
        {
            float[] b = Buffer(1.6f * size + 0.6f);
            AddBoom(b, seed, 0f, 1.4f * size, 70f, 28f, 1f);
            AddCracks(b, seed, 0f, 0.25f * size, Mathf.RoundToInt(10 * size), 0.6f);
            AddRustle(b, seed, 0.02f, 1.2f * size, 0.5f, 0.02f); // earth and leaves settling
            AddEcho(b, 0.17f * size, 0.22f, 2);
            return b;
        }

        private static float[] Groan(int seed)
        {
            float[] b = Buffer(1f);
            AddCreak(b, seed, 0f, 0.9f, u => 30f + 45f * Mathf.Sin(Mathf.PI * u), 1f, 0.75f);
            AddBoom(b, seed, 0f, 0.3f, 120f, 70f, 0.3f);
            return b;
        }

        private static float[] FallingCreak(int seed)
        {
            float[] b = Buffer(3.4f);
            AddCreak(b, seed, 0f, 3.2f, u => 10f + 150f * u * u * u, 1f, 0.55f);
            AddCracks(b, seed, 2.6f, 0.6f, 8, 0.7f);
            AddRustle(b, seed, 1.5f, 1.9f, 0.4f, 0.8f);
            return b;
        }

        private static float[] Whistle(int seed)
        {
            float[] b = Buffer(3f);
            var random = new System.Random(seed);
            float baseHz = 1250f + (float)random.NextDouble() * 200f;
            float phase = 0f;
            // Two notes - "fyu... fyuuu" - the Leshy whistling in the thicket.
            (float start, float length, float glide)[] notes = { (0f, 0.35f, -180f), (0.55f, 0.9f, -320f) };
            foreach ((float start, float length, float glide) in notes)
            {
                int from = Mathf.RoundToInt(start * Rate);
                for (int i = 0; i < length * Rate; i++)
                {
                    float t = i / (float)Rate;
                    float u = t / length;
                    float frequency = baseHz + glide * u + 18f * Mathf.Sin(2f * Mathf.PI * 6f * t);
                    phase += frequency / Rate;
                    float envelope = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((length - t) / 0.15f);
                    float breath = ((float)random.NextDouble() * 2f - 1f) * 0.05f;
                    b[from + i] += (Mathf.Sin(2f * Mathf.PI * phase) + breath) * envelope * 0.6f;
                }
            }

            AddEcho(b, 0.33f, 0.35f, 3); // through the trees
            LowPass(b, 0.6f);
            return b;
        }

        private static float[] DistantCreak(int seed)
        {
            float[] b = Buffer(2.8f);
            AddCreak(b, seed, 0f, 2f, u => 12f + 30f * Mathf.Sin(Mathf.PI * u), 1f, 0.5f);
            AddEcho(b, 0.29f, 0.3f, 3);
            LowPass(b, 0.12f); // far away: the highs are gone
            return b;
        }
    }
}
