using UnityEngine;

namespace NAV.Presentation.Weather
{
    /// <summary>
    /// Procedurally generated stand-in sounds for weather (the project has no audio assets yet
    /// and final audio is "Not Yet Decided"): rain = low-passed white noise loop, thunder = a
    /// sharp crack into a long low rumble. Replace with real clips by swapping what
    /// WeatherEffects assigns to its AudioSources - nothing else depends on this class.
    /// </summary>
    public static class PlaceholderWeatherAudio
    {
        private const int SampleRate = 44100;

        public static AudioClip CreateRainLoop(float seconds = 4f)
        {
            int length = Mathf.RoundToInt(SampleRate * seconds);
            var data = new float[length];
            var rng = new System.Random(1234);
            float low = 0f;

            // Two passes so the low-pass filter state at the loop point matches the start
            // (no click when the clip wraps).
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < length; i++)
                {
                    float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                    low += 0.18f * (white - low);
                    // Occasional brighter "drop" ticks on top of the hiss.
                    float drop = rng.NextDouble() < 0.0015 ? white * 0.8f : 0f;
                    data[i] = (low * 0.9f + drop * 0.25f) * 0.5f;
                }
            }

            AudioClip clip = AudioClip.Create("Rain (placeholder)", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip CreateThunder(int seed, float seconds = 4f)
        {
            int length = Mathf.RoundToInt(SampleRate * seconds);
            var data = new float[length];
            var rng = new System.Random(seed * 7919);
            float brown = 0f;
            float crackLow = 0f;

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);

                // Rumble: brown noise (integrated white), slow attack, long decay with wobble.
                brown = Mathf.Clamp(brown + white * 0.02f, -1f, 1f) * 0.999f;
                float rumbleEnvelope = Mathf.Clamp01(t / 0.25f) * Mathf.Exp(-t * 0.9f) * (0.75f + 0.25f * Mathf.Sin(t * 5.3f + seed));

                // Crack: brighter noise burst in the first ~0.3 s.
                crackLow += 0.5f * (white - crackLow);
                float crackEnvelope = Mathf.Exp(-t * 14f);

                data[i] = brown * rumbleEnvelope * 2.2f + crackLow * crackEnvelope * 0.6f;
            }

            AudioClip clip = AudioClip.Create($"Thunder {seed} (placeholder)", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
