using UnityEngine;

namespace NAV.Presentation.Weather
{
    /// <summary>
    /// Procedurally generated stand-in thunder (the nature sound pack has rain but no thunder): a
    /// sharp crack into a long low rumble. Replace with real clips by swapping what WeatherEffects
    /// assigns to its thunder AudioSource - nothing else depends on this class.
    /// </summary>
    public static class PlaceholderWeatherAudio
    {
        private const int SampleRate = 44100;

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
