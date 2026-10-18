using System.IO;
using UnityEditor;
using UnityEngine;

namespace NAV.Editor
{
    /// <summary>
    /// Bakes the seamlessly tiling noise texture the NAV/Skybox/Panoramic Blend cloud layer
    /// samples (sampling a texture is far cheaper than per-pixel noise maths, and its exact
    /// tiling lets the wind offsets wrap without a jump). Channels:
    ///   R - Perlin fBm, the large cloud shapes
    ///   G - inverted Worley fBm, the puffy "billows" that break up the edges
    ///   B - Perlin fBm with another seed, scrolled differently so the shapes morph over time
    /// Re-run via the menu if the generator changes; the output is deterministic.
    /// </summary>
    internal static class CloudNoiseGenerator
    {
        private const string OutputPath = "Assets/NAV/Art/Textures/CloudNoise.png";
        private const int Size = 512;

        [MenuItem("NAV/Sky/Generate Cloud Noise Texture")]
        private static void Generate()
        {
            var pixels = new Color[Size * Size];
            var r = new float[pixels.Length];
            var g = new float[pixels.Length];
            var b = new float[pixels.Length];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (float)x / Size;
                    float v = (float)y / Size;
                    int i = y * Size + x;
                    r[i] = PerlinFbm(u, v, basePeriod: 4, octaves: 6, seed: 11);
                    g[i] = WorleyFbm(u, v, basePeriod: 6, octaves: 3, seed: 23);
                    b[i] = PerlinFbm(u, v, basePeriod: 5, octaves: 5, seed: 37);
                }
            }

            Normalize(r);
            Normalize(g);
            Normalize(b);
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(r[i], g[i], b[i], 1f);
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, linear: true);
            texture.SetPixels(pixels);
            texture.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllBytes(OutputPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(OutputPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(OutputPath);
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            // Smooth gradients band visibly under block compression.
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            Debug.Log($"{nameof(CloudNoiseGenerator)}: wrote {OutputPath}");
        }

        private static float PerlinFbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f;
            float amplitude = 0.5f;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += amplitude * Perlin(u * period, v * period, period, seed + o * 101);
                amplitude *= 0.5f;
                period *= 2;
            }

            return sum;
        }

        private static float WorleyFbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            float sum = 0f;
            float amplitude = 0.6f;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += amplitude * (1f - Worley(u * period, v * period, period, seed + o * 101));
                amplitude *= 0.5f;
                period *= 2;
            }

            return sum;
        }

        // Gradient noise whose lattice wraps every `period` cells, so the texture tiles.
        private static float Perlin(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;

            float n00 = Gradient(Hash(x0, y0, period, seed), fx, fy);
            float n10 = Gradient(Hash(x0 + 1, y0, period, seed), fx - 1f, fy);
            float n01 = Gradient(Hash(x0, y0 + 1, period, seed), fx, fy - 1f);
            float n11 = Gradient(Hash(x0 + 1, y0 + 1, period, seed), fx - 1f, fy - 1f);

            float sx = Fade(fx);
            float sy = Fade(fy);
            return Mathf.Lerp(Mathf.Lerp(n00, n10, sx), Mathf.Lerp(n01, n11, sx), sy);
        }

        // Distance to the nearest of one random feature point per cell, wrapping every `period` cells.
        private static float Worley(float x, float y, int period, int seed)
        {
            int cx = Mathf.FloorToInt(x);
            int cy = Mathf.FloorToInt(y);
            float nearest = float.MaxValue;
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    int h = Hash(cx + ox, cy + oy, period, seed);
                    float px = cx + ox + (h & 0xFFFF) / 65535f;
                    float py = cy + oy + ((h >> 16) & 0xFFFF) / 65535f;
                    float dx = px - x;
                    float dy = py - y;
                    nearest = Mathf.Min(nearest, dx * dx + dy * dy);
                }
            }

            return Mathf.Clamp01(Mathf.Sqrt(nearest));
        }

        private static int Hash(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return h ^ (h >> 16);
            }
        }

        private static float Gradient(int hash, float x, float y)
        {
            float angle = (hash & 1023) / 1024f * 2f * Mathf.PI;
            return Mathf.Cos(angle) * x + Mathf.Sin(angle) * y;
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static void Normalize(float[] values)
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            foreach (float value in values)
            {
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
            }

            float range = Mathf.Max(max - min, 1e-6f);
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = (values[i] - min) / range;
            }
        }
    }
}
