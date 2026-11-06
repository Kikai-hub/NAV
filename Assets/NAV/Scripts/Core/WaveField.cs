using UnityEngine;

namespace NAV.Core
{
    /// <summary>
    /// The sea's waves as a function of position and time - Gerstner waves (crests sharpen, troughs
    /// widen, the water moves in circles). One definition for both sides:
    ///  - OceanSurface feeds the same numbers to the water shader (vertex displacement), and
    ///  - gameplay asks HeightAt for the surface under a point (boats, swimming, the camera, ripples
    ///    - via WaterBody), so what floats rides exactly the waves that are drawn.
    /// Each wave fades out with distance from Fade Centre (the camera), over a range scaled by its
    /// wavelength - far away the mesh is too coarse to draw short waves, so they are not physical
    /// there either. Time is the level clock: waves stop while the game is paused.
    /// </summary>
    public static class WaveField
    {
        public const int MaxWaves = 4;
        private const float Gravity = 9.81f;
        // Fade of a wave from FadeStart x wavelength to FadeEnd x wavelength from the centre.
        public const float FadeStart = 7f;
        public const float FadeEnd = 14f;
        // Fixed-point steps to undo the waves' sideways motion when asked for the height at a point.
        private const int InverseSteps = 3;

        private static readonly Vector2[] Directions = new Vector2[MaxWaves];
        private static readonly float[] WaveNumbers = new float[MaxWaves];
        private static readonly float[] Speeds = new float[MaxWaves];
        private static readonly float[] Amplitudes = new float[MaxWaves];
        private static readonly float[] Steepness = new float[MaxWaves];
        private static readonly float[] Wavelengths = new float[MaxWaves];
        private static int _count;

        /// <summary>Wave height multiplier (calm 0 .. storm 2-3) - the sea state of the weather.</summary>
        public static float Intensity { get; set; } = 1f;

        /// <summary>Where the waves are at full size (the camera, XZ).</summary>
        public static Vector2 FadeCentre { get; set; }

        public static int Count => _count;
        public static float Time => UnityEngine.Time.timeSinceLevelLoad;

        /// <summary>Sets the waves (up to MaxWaves): direction (XZ, normalised here), wavelength
        /// (m), amplitude (m) and steepness (0 = round swell, 1 = sharp crests).</summary>
        public static void Configure(Vector2[] directions, float[] wavelengths, float[] amplitudes, float[] steepness, int count)
        {
            _count = Mathf.Clamp(count, 0, MaxWaves);
            for (int i = 0; i < _count; i++)
            {
                Directions[i] = directions[i].sqrMagnitude > 0f ? directions[i].normalized : Vector2.right;
                Wavelengths[i] = Mathf.Max(0.5f, wavelengths[i]);
                WaveNumbers[i] = 2f * Mathf.PI / Wavelengths[i];
                Speeds[i] = Mathf.Sqrt(Gravity / WaveNumbers[i]);
                Amplitudes[i] = Mathf.Max(0f, amplitudes[i]);
                Steepness[i] = Mathf.Clamp01(steepness[i]);
            }
        }

        public static void Clear() => _count = 0;

        /// <summary>For the shader: per wave (dir.x, dir.z, wave number, speed).</summary>
        public static Vector4 ShaderDirection(int i) => new(Directions[i].x, Directions[i].y, WaveNumbers[i], Speeds[i]);

        /// <summary>For the shader: per wave (amplitude x intensity, steepness, fade start, fade end).</summary>
        public static Vector4 ShaderAmplitude(int i) => new(Amplitudes[i] * Intensity, Steepness[i], Wavelengths[i] * FadeStart, Wavelengths[i] * FadeEnd);

        /// <summary>How a still-water point (x, z) is moved by the waves at this time.</summary>
        public static Vector3 Displacement(float x, float z)
        {
            Vector3 offset = Vector3.zero;
            float t = Time;
            float fromCentreX = x - FadeCentre.x;
            float fromCentreZ = z - FadeCentre.y;
            float distance = Mathf.Sqrt(fromCentreX * fromCentreX + fromCentreZ * fromCentreZ);
            for (int i = 0; i < _count; i++)
            {
                float amplitude = Amplitudes[i] * Intensity
                    * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Wavelengths[i] * FadeStart, Wavelengths[i] * FadeEnd, distance)));
                if (amplitude <= 0f)
                {
                    continue;
                }

                Vector2 d = Directions[i];
                float phase = WaveNumbers[i] * (d.x * x + d.y * z - Speeds[i] * t);
                float sideways = Steepness[i] / (WaveNumbers[i] * _count) * Mathf.Min(1f, amplitude * WaveNumbers[i] * _count) * Mathf.Cos(phase);
                offset.x += d.x * sideways;
                offset.z += d.y * sideways;
                offset.y += amplitude * Mathf.Sin(phase);
            }

            return offset;
        }

        /// <summary>Height of the waves (m, relative to still water) at the world point (x, z):
        /// finds the still-water point the waves carried there, then its height.</summary>
        public static float HeightAt(float x, float z)
        {
            if (_count == 0)
            {
                return 0f;
            }

            float sourceX = x;
            float sourceZ = z;
            for (int step = 0; step < InverseSteps; step++)
            {
                Vector3 moved = Displacement(sourceX, sourceZ);
                sourceX = x - moved.x;
                sourceZ = z - moved.z;
            }

            return Displacement(sourceX, sourceZ).y;
        }

        /// <summary>Largest height the waves can reach right now (for whitecaps, safety margins).</summary>
        public static float MaxHeight
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < _count; i++)
                {
                    sum += Amplitudes[i];
                }

                return sum * Intensity;
            }
        }
    }
}
