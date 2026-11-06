namespace NAV.World.Generation.Hybrid
{
    /// <summary>
    /// Classic 2D gradient (Perlin) noise in plain C#. Mathf.PerlinNoise is an engine call; this
    /// one is safe on worker threads, where the terrain tiles are computed. The permutation table
    /// is fixed, so a seed shifts the noise (NoiseField) rather than reshuffling it.
    /// </summary>
    public static class Perlin2D
    {
        private static readonly int[] Permutation = BuildPermutation();

        // Gradient noise rarely leaves +-0.7; stretched so its spread matches Mathf.PerlinNoise,
        // which the world's thresholds were tuned on.
        private const float Contrast = 0.75f;

        /// <summary>Noise in [0, 1], 0.5 on average.</summary>
        public static float Noise01(float x, float y)
        {
            float value = 0.5f + Noise(x, y) * Contrast;
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }

        /// <summary>Noise in about [-1, 1].</summary>
        public static float Noise(float x, float y)
        {
            int xi = FastFloor(x);
            int yi = FastFloor(y);
            float xf = x - xi;
            float yf = y - yi;
            int ix = xi & 255;
            int iy = yi & 255;

            int[] p = Permutation;
            int aa = p[p[ix] + iy];
            int ab = p[p[ix] + iy + 1];
            int ba = p[p[ix + 1] + iy];
            int bb = p[p[ix + 1] + iy + 1];

            float u = Fade(xf);
            float v = Fade(yf);
            float bottom = Lerp(Gradient(aa, xf, yf), Gradient(ba, xf - 1f, yf), u);
            float top = Lerp(Gradient(ab, xf, yf - 1f), Gradient(bb, xf - 1f, yf - 1f), u);
            return Lerp(bottom, top, v);
        }

        private static int FastFloor(float value)
        {
            int truncated = (int)value;
            return value < truncated ? truncated - 1 : truncated;
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        // Eight directions; diagonals scaled to unit length so all gradients weigh the same.
        private static float Gradient(int hash, float x, float y)
        {
            const float Diagonal = 0.70710678f;
            switch (hash & 7)
            {
                case 0: return (x + y) * Diagonal;
                case 1: return (-x + y) * Diagonal;
                case 2: return (x - y) * Diagonal;
                case 3: return (-x - y) * Diagonal;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }

        // 0..255 shuffled by a fixed LCG, then doubled so lookups never wrap.
        private static int[] BuildPermutation()
        {
            var values = new int[256];
            for (int i = 0; i < 256; i++)
            {
                values[i] = i;
            }

            uint state = 0x2545F491u;
            for (int i = 255; i > 0; i--)
            {
                state = state * 1664525u + 1013904223u;
                int j = (int)((state >> 8) % (uint)(i + 1));
                (values[i], values[j]) = (values[j], values[i]);
            }

            var table = new int[512];
            for (int i = 0; i < 512; i++)
            {
                table[i] = values[i & 255];
            }

            return table;
        }
    }
}
