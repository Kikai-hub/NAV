using UnityEngine;

namespace NAV.Presentation.Combat
{
    /// <summary>
    /// Procedurally generated stand-in combat sounds (the project has no audio assets yet and
    /// final audio is "Not Yet Decided" - same approach as PlaceholderWeatherAudio). Each sound
    /// is a short noise burst and/or a decaying tone; the seed gives variations of the same
    /// sound so repeated hits don't sound identical. Replace with real clips by changing what
    /// HitFeedback plays - nothing else depends on this class.
    /// </summary>
    public static class PlaceholderCombatAudio
    {
        private const int SampleRate = 44100;

        /// <summary>Hit on a creature: low, soft thump.</summary>
        public static AudioClip CreateFleshHit(int seed)
        {
            return Create($"Flesh Hit {seed}", 0.25f, seed, (t, white, state) =>
            {
                state.Low += 0.08f * (white - state.Low);
                float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(140f, 60f, t / 0.25f) * t);
                return (state.Low * 2.2f + tone * 0.6f) * Mathf.Exp(-t * 22f);
            });
        }

        /// <summary>Axe into wood: hollow knock plus a short splintery crack.</summary>
        public static AudioClip CreateWoodHit(int seed)
        {
            float pitch = 1f + (seed % 3) * 0.07f;
            return Create($"Wood Hit {seed}", 0.35f, seed, (t, white, state) =>
            {
                state.Low += 0.35f * (white - state.Low);
                float knock = Mathf.Sin(2f * Mathf.PI * 230f * pitch * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 470f * pitch * t);
                return knock * 0.45f * Mathf.Exp(-t * 18f) + state.Low * 0.9f * Mathf.Exp(-t * 45f);
            });
        }

        /// <summary>Pick on stone: bright metallic clink over a gritty burst.</summary>
        public static AudioClip CreateStoneHit(int seed)
        {
            float pitch = 1f + (seed % 3) * 0.06f;
            return Create($"Stone Hit {seed}", 0.4f, seed, (t, white, state) =>
            {
                float clink = Mathf.Sin(2f * Mathf.PI * 1850f * pitch * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 2730f * pitch * t) * 0.4f;
                return clink * 0.35f * Mathf.Exp(-t * 14f) + white * 0.5f * Mathf.Exp(-t * 60f);
            });
        }

        /// <summary>Hit that didn't count (wrong tool) or on something unknown: dull clunk.</summary>
        public static AudioClip CreateDullHit(int seed)
        {
            return Create($"Dull Hit {seed}", 0.2f, seed, (t, white, state) =>
            {
                state.Low += 0.15f * (white - state.Low);
                return (state.Low * 1.6f + Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.3f) * Mathf.Exp(-t * 30f);
            });
        }

        /// <summary>Weapon swing: band-limited noise that swells and fades.</summary>
        public static AudioClip CreateSwing(int seed)
        {
            return Create($"Swing {seed}", 0.3f, seed, (t, white, state) =>
            {
                state.Low += 0.12f * (white - state.Low);
                state.High += 0.5f * (state.Low - state.High);
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.3f));
                return (state.Low - state.High * 0.6f) * envelope * envelope * 1.6f;
            });
        }

        /// <summary>Player takes damage: short grunt-like low burst.</summary>
        public static AudioClip CreateHurt(int seed)
        {
            return Create($"Hurt {seed}", 0.3f, seed, (t, white, state) =>
            {
                state.Low += 0.06f * (white - state.Low);
                float voice = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(190f, 120f, t / 0.3f) * t);
                float envelope = Mathf.Clamp01(t / 0.02f) * Mathf.Exp(-t * 12f);
                return (voice * 0.5f + state.Low * 1.5f) * envelope;
            });
        }

        /// <summary>Blocked hit: wooden thud on the guard.</summary>
        public static AudioClip CreateBlock(int seed)
        {
            return Create($"Block {seed}", 0.3f, seed, (t, white, state) =>
            {
                state.Low += 0.2f * (white - state.Low);
                return (Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.7f + state.Low * 1.2f) * Mathf.Exp(-t * 20f);
            });
        }

        /// <summary>Parry: ringing metal clang.</summary>
        public static AudioClip CreateParry(int seed)
        {
            return Create($"Parry {seed}", 0.7f, seed, (t, white, state) =>
            {
                float ring = Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 1980f * t) * 0.3f + Mathf.Sin(2f * Mathf.PI * 2970f * t) * 0.2f;
                return ring * 0.5f * Mathf.Exp(-t * 6f) + white * 0.4f * Mathf.Exp(-t * 80f);
            });
        }

        /// <summary>Felled tree hitting the ground: a deep thump, a creak and rustling needles.</summary>
        public static AudioClip CreateTreeFall(int seed)
        {
            return Create($"Tree Fall {seed}", 1.6f, seed, (t, white, state) =>
            {
                state.Low += 0.02f * (white - state.Low);
                state.High += 0.4f * (white - state.High);
                float thump = (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(70f, 38f, Mathf.Clamp01(t / 0.4f)) * t) * 0.8f + state.Low * 4f) * Mathf.Exp(-t * 5f);
                float crack = Mathf.Sin(2f * Mathf.PI * (330f + 40f * Mathf.Sin(t * 60f)) * t) * 0.25f * Mathf.Exp(-t * 25f);
                float rustle = state.High * 0.25f * Mathf.Exp(-t * 2.2f) * Mathf.Clamp01(t / 0.05f);
                return thump + crack + rustle;
            });
        }

        private class FilterState
        {
            public float Low;
            public float High;
        }

        private static AudioClip Create(string name, float seconds, int seed, System.Func<float, float, FilterState, float> sample)
        {
            int length = Mathf.RoundToInt(SampleRate * seconds);
            var data = new float[length];
            var rng = new System.Random(seed * 7919 + name.Length);
            var state = new FilterState();

            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                data[i] = Mathf.Clamp(sample(t, white, state), -1f, 1f);
            }

            // Short fade-out so a clip cut by its length never clicks.
            int fade = Mathf.Min(length, SampleRate / 100);
            for (int i = 0; i < fade; i++)
            {
                data[length - 1 - i] *= (float)i / fade;
            }

            AudioClip clip = AudioClip.Create($"{name} (placeholder)", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
