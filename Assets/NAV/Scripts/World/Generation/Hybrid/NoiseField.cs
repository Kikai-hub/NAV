using System;
using UnityEngine;

namespace NAV.World.Generation.Hybrid
{
    /// <summary>
    /// One layer of seeded fractal Perlin noise (fBm): Octaves layers, each Lacunarity times finer
    /// and Gain times weaker, normalised back to [0, 1]. Pure and allocation-free - safe to sample
    /// millions of times, from any thread (Perlin2D). The seed only shifts the noise (WorldHash),
    /// so two layers built with different salts never line up.
    /// </summary>
    [Serializable]
    public struct NoiseLayer
    {
        [Tooltip("Size of the largest features, in metres.")]
        [Min(1f)] public float Scale;
        [Range(1, 8)] public int Octaves;
        [Min(1f)] public float Lacunarity;
        [Range(0f, 1f)] public float Gain;

        public NoiseLayer(float scale, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            Scale = scale;
            Octaves = octaves;
            Lacunarity = lacunarity;
            Gain = gain;
        }
    }

    public readonly struct NoiseField
    {
        // Offsets stay within a few thousand noise units, where floats are still precise.
        private const float OffsetRange = 4000f;

        private readonly NoiseLayer _layer;
        private readonly Vector2 _offset;
        private readonly float _normaliser;

        public NoiseField(NoiseLayer layer, int seed, int salt)
        {
            _layer = layer;
            _offset = new Vector2(
                WorldHash.Value(seed, salt, 1, 0, 0) * OffsetRange,
                WorldHash.Value(seed, salt, 2, 0, 0) * OffsetRange);

            float amplitude = 1f;
            float total = 0f;
            for (int i = 0; i < Mathf.Max(1, layer.Octaves); i++)
            {
                total += amplitude;
                amplitude *= layer.Gain;
            }

            _normaliser = 1f / total;
        }

        /// <summary>Noise in [0, 1] at a world position (metres).</summary>
        public float Sample(float x, float z)
        {
            float frequency = 1f / Mathf.Max(1f, _layer.Scale);
            float amplitude = 1f;
            float sum = 0f;
            for (int i = 0; i < Mathf.Max(1, _layer.Octaves); i++)
            {
                sum += amplitude * Perlin2D.Noise01(x * frequency + _offset.x + i * 31.7f, z * frequency + _offset.y - i * 17.3f);
                frequency *= _layer.Lacunarity;
                amplitude *= _layer.Gain;
            }

            return Mathf.Clamp01(sum * _normaliser);
        }

        /// <summary>Ridged noise in [0, 1]: sharp crests where the plain noise crosses its middle - mountain ridges.</summary>
        public float SampleRidged(float x, float z)
        {
            float ridge = 1f - Mathf.Abs(Sample(x, z) * 2f - 1f);
            return ridge * ridge;
        }
    }
}
