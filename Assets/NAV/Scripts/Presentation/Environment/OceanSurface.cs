using System;
using System.Collections.Generic;
using NAV.Core;
using NAV.Gameplay.Environment;
using NAV.World.Weather;
using UnityEngine;
using UnityEngine.Rendering;

namespace NAV.Presentation.Environment
{
    /// <summary>
    /// The sea with real waves. Put on the sea's water plane (WaterBody + NAV/Water material); in
    /// Play Mode it:
    ///  - configures NAV.Core.WaveField (Gerstner waves, so gameplay and the shader agree) and
    ///    passes the same numbers to the shader as globals every frame;
    ///  - replaces the flat plane by LOD rings around the camera (clipmap): ring 0 is a 128 x 128
    ///    grid of Base Cell Size cells, every next ring the same grid with cells twice as big and
    ///    the middle cut out. The rings jump in steps of the largest cell, so vertices stay on a
    ///    fixed world grid (no swimming); their outer edges are morphed in the shader (no cracks);
    ///  - makes the water body endless and wavy, so swimming, the camera and ripples follow the
    ///    waves, and the boat floats on them;
    ///  - scales the waves with the weather (WeatherDefinition Sea State).
    /// In the editor (not playing) the flat plane stays as it was.
    /// </summary>
    [RequireComponent(typeof(WaterBody))]
    public class OceanSurface : MonoBehaviour
    {
        private const int GridCells = 128;
        private const int HoleStart = GridCells / 4;
        private const int HoleEnd = GridCells * 3 / 4;

        [Serializable]
        private class Wave
        {
            [Tooltip("Direction the wave travels (XZ).")]
            public Vector2 Direction = Vector2.right;
            [Min(0.5f)] public float Wavelength = 20f;
            [Min(0f)] public float Amplitude = 0.2f;
            [Range(0f, 1f)] public float Steepness = 0.5f;
        }

        [Header("Waves (calm sea; the weather's Sea State multiplies the height)")]
        [SerializeField] private List<Wave> _waves = new()
        {
            new Wave { Direction = new Vector2(1f, 0.25f), Wavelength = 42f, Amplitude = 0.32f, Steepness = 0.55f },
            new Wave { Direction = new Vector2(0.7f, 0.75f), Wavelength = 21f, Amplitude = 0.16f, Steepness = 0.6f },
            new Wave { Direction = new Vector2(0.95f, -0.4f), Wavelength = 11f, Amplitude = 0.07f, Steepness = 0.5f },
            new Wave { Direction = new Vector2(0.3f, 1f), Wavelength = 6f, Amplitude = 0.035f, Steepness = 0.4f },
        };

        [Header("Weather")]
        [Tooltip("Optional - found in the scene when empty. Its Sea State scales the waves.")]
        [SerializeField] private WeatherSystem _weather;
        [Tooltip("How fast (per second) the sea follows a change of weather.")]
        [SerializeField, Min(0.01f)] private float _seaStateResponse = 0.05f;

        [Header("Mesh")]
        [Tooltip("Cell size (m) of the innermost ring; each further ring doubles it.")]
        [SerializeField, Min(0.25f)] private float _baseCellSize = 1f;
        [Tooltip("Rings around the camera. 6 rings of 1 m cells reach about 2 km.")]
        [SerializeField, Range(1, 8)] private int _rings = 6;

        private static readonly int WaveDirsId = Shader.PropertyToID("_NAVWaveDirs");
        private static readonly int WaveAmpsId = Shader.PropertyToID("_NAVWaveAmps");
        private static readonly int WaveCountId = Shader.PropertyToID("_NAVWaveCount");
        private static readonly int WaveTimeId = Shader.PropertyToID("_NAVWaveTime");
        private static readonly int WaveCentreId = Shader.PropertyToID("_NAVWaveCentre");
        private static readonly int WaveMaxHeightId = Shader.PropertyToID("_NAVWaveMaxHeight");

        private readonly Vector4[] _shaderDirs = new Vector4[WaveField.MaxWaves];
        private readonly Vector4[] _shaderAmps = new Vector4[WaveField.MaxWaves];
        private readonly List<Mesh> _meshes = new();
        private Transform _root;
        private Renderer _plane;
        private float _seaState = 1f;

        private void Awake()
        {
            var water = GetComponent<WaterBody>();
            water.Endless = true;
            water.HasWaves = true;
            ConfigureWaves();
            if (_weather == null)
            {
                _weather = FindAnyObjectByType<WeatherSystem>();
            }
        }

        private void Start()
        {
            _plane = GetComponent<Renderer>();
            if (_plane == null || _plane.sharedMaterial == null)
            {
                Debug.LogError($"{nameof(OceanSurface)} on '{name}' needs the water plane's Renderer with the NAV/Water material.", this);
                enabled = false;
                return;
            }

            BuildRings(_plane.sharedMaterial);
            // The rings draw the sea now; the plane stays for its WaterBody.
            _plane.enabled = false;
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                ConfigureWaves();
            }
        }

        private void OnDestroy()
        {
            WaveField.Clear();
            Shader.SetGlobalFloat(WaveCountId, 0f);
            foreach (Mesh mesh in _meshes)
            {
                Destroy(mesh);
            }

            if (_root != null)
            {
                Destroy(_root.gameObject);
            }
        }

        private void ConfigureWaves()
        {
            int count = Mathf.Min(_waves.Count, WaveField.MaxWaves);
            var directions = new Vector2[count];
            var wavelengths = new float[count];
            var amplitudes = new float[count];
            var steepness = new float[count];
            for (int i = 0; i < count; i++)
            {
                directions[i] = _waves[i].Direction;
                wavelengths[i] = _waves[i].Wavelength;
                amplitudes[i] = _waves[i].Amplitude;
                steepness[i] = _waves[i].Steepness;
            }

            WaveField.Configure(directions, wavelengths, amplitudes, steepness, count);
        }

        private void Update()
        {
            if (_weather != null && _weather.Current != null)
            {
                float target = _weather.Evaluate(weather => weather.SeaState);
                _seaState = Mathf.MoveTowards(_seaState, target, _seaStateResponse * Time.deltaTime);
            }

            WaveField.Intensity = _seaState;
        }

        private void LateUpdate()
        {
            UnityEngine.Camera view = UnityEngine.Camera.main;
            if (view == null || _root == null)
            {
                return;
            }

            Vector3 eye = view.transform.position;
            WaveField.FadeCentre = new Vector2(eye.x, eye.z);

            // Steps of the largest cell keep every ring on its own world grid.
            float step = _baseCellSize * (1 << (_rings - 1));
            _root.position = new Vector3(Mathf.Round(eye.x / step) * step, transform.position.y, Mathf.Round(eye.z / step) * step);

            for (int i = 0; i < WaveField.Count; i++)
            {
                _shaderDirs[i] = WaveField.ShaderDirection(i);
                _shaderAmps[i] = WaveField.ShaderAmplitude(i);
            }

            Shader.SetGlobalVectorArray(WaveDirsId, _shaderDirs);
            Shader.SetGlobalVectorArray(WaveAmpsId, _shaderAmps);
            Shader.SetGlobalFloat(WaveCountId, WaveField.Count);
            Shader.SetGlobalFloat(WaveTimeId, WaveField.Time);
            Shader.SetGlobalVector(WaveCentreId, new Vector4(eye.x, eye.z, 0f, 0f));
            Shader.SetGlobalFloat(WaveMaxHeightId, WaveField.MaxHeight);
        }

        private void BuildRings(Material material)
        {
            _root = new GameObject("Ocean Surface").transform;
            _root.position = transform.position;
            for (int ring = 0; ring < _rings; ring++)
            {
                float cell = _baseCellSize * (1 << ring);
                Mesh mesh = BuildRingMesh(cell, ring > 0);
                _meshes.Add(mesh);

                var level = new GameObject($"Ocean Ring {ring}");
                level.layer = gameObject.layer;
                level.transform.SetParent(_root, false);
                level.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = level.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
        }

        // A GridCells x GridCells grid centred on the origin, the middle half cut out when hollow.
        // uv0: (1 on odd outer-edge vertices = morph, axis of that edge 0 = X / 1 = Z); uv1.x: cell.
        private static Mesh BuildRingMesh(float cell, bool hollow)
        {
            const int n = GridCells + 1;
            float half = GridCells * cell * 0.5f;
            var vertices = new Vector3[n * n];
            var morph = new Vector2[n * n];
            var cells = new Vector2[n * n];
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = z * n + x;
                    vertices[i] = new Vector3(x * cell - half, 0f, z * cell - half);
                    bool alongX = (z == 0 || z == GridCells) && (x & 1) == 1;
                    bool alongZ = (x == 0 || x == GridCells) && (z & 1) == 1;
                    morph[i] = new Vector2(alongX || alongZ ? 1f : 0f, alongZ ? 1f : 0f);
                    cells[i] = new Vector2(cell, 0f);
                }
            }

            var triangles = new List<int>(GridCells * GridCells * 6);
            for (int z = 0; z < GridCells; z++)
            {
                for (int x = 0; x < GridCells; x++)
                {
                    if (hollow && x >= HoleStart && x < HoleEnd && z >= HoleStart && z < HoleEnd)
                    {
                        continue;
                    }

                    int a = z * n + x;
                    int b = a + 1;
                    int c = a + n;
                    int d = c + 1;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            var mesh = new Mesh { name = $"Ocean Ring {cell} m", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, morph);
            mesh.SetUVs(1, cells);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            // Waves move the vertices in the shader - keep the bounds generous.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(half * 2f, 40f, half * 2f));
            return mesh;
        }
    }
}
