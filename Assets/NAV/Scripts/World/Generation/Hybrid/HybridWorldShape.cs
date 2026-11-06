using System.Collections.Generic;
using NAV.World.Biomes;
using UnityEngine;

namespace NAV.World.Generation.Hybrid
{
    /// <summary>The mask zone a point lies in, by its plain distance from the centre.</summary>
    public enum WorldZone
    {
        Continent,
        Archipelago,
        Ocean
    }

    /// <summary>Everything the world is at one spot.</summary>
    public readonly struct WorldPoint
    {
        public WorldPoint(float height, float elevation, float radius01, float latitude, WorldZone zone, bool onIsland, bool polar, BiomeDefinition biome)
        {
            Height = height;
            Elevation = elevation;
            Radius01 = radius01;
            Latitude = latitude;
            Zone = zone;
            OnIsland = onIsland;
            Polar = polar;
            Biome = biome;
        }

        /// <summary>Metres above (positive) or below (negative) sea level, local relief included.</summary>
        public float Height { get; }
        /// <summary>Unitless land value after the mask; above 0 is land.</summary>
        public float Elevation { get; }
        /// <summary>Distance from the centre as a fraction of the world radius.</summary>
        public float Radius01 { get; }
        /// <summary>World Z as a fraction of the radius: +1 far north, -1 far south.</summary>
        public float Latitude { get; }
        public WorldZone Zone { get; }
        /// <summary>Land outside the continents (the archipelago's islands).</summary>
        public bool OnIsland { get; }
        public bool Polar { get; }
        public BiomeDefinition Biome { get; }
        public bool IsLand => Elevation > 0f;
    }

    /// <summary>
    /// The hybrid world as a pure function of (seed, x, z) - no arrays, so any spot of a 30 km world
    /// can be asked for in any order (terrain tiles, chunk content, POIs, the map).
    ///  1. Land: low-frequency continent noise + high-frequency detail noise (ragged coasts, islets).
    ///  2. Mask: inside Continent Zone End land is guaranteed (the coast wobbles with the continent
    ///     noise); in the archipelago zone land needs an ever higher value, so it breaks into ever
    ///     smaller islands; past Archipelago Zone End everything sinks smoothly into deep ocean.
    ///  3. Biomes: height rules first (mountains on the peaks of the continent and the big
    ///     islands, swamps in island lowlands),
    ///     then polar caps by latitude, then radial climate bands - all borders blurred by noise.
    ///  4. Local relief: small bumps scaled by the biome's Height Variation, none at the shoreline.
    /// Coordinates are metres from the world centre. Everything is copied from the settings in the
    /// constructor, so Sample is safe on worker threads (no engine calls).
    /// </summary>
    public sealed class HybridWorldShape
    {
        // Salts keep every noise layer independent of the others for the same seed.
        private const int ContinentSalt = 101;
        private const int DetailSalt = 102;
        private const int MountainSalt = 103;
        private const int ClimateSalt = 104;
        private const int PolarSalt = 105;
        private const int PatchSalt = 106;
        private const int ReliefSalt = 107;
        private const int MixSalt = 108;
        // Samples used to find the noise level that leaves a band's Mix Share above it.
        private const int QuantileSamples = 4096;
        // Metres from the shoreline over which the local relief fades in - keeps coasts clean.
        private const float ReliefShoreFade = 3f;

        private readonly NoiseField _continent;
        private readonly NoiseField _detail;
        private readonly NoiseField _mountain;
        private readonly NoiseField _climateBorder;
        private readonly NoiseField _polarBorder;
        private readonly NoiseField _patch;
        private readonly NoiseField _relief;
        private readonly NoiseField _mix;
        // Per climate band: mix noise at or above this is the band's Mix Biome.
        private readonly float[] _mixThreshold;

        private readonly float _radius;
        private readonly Vector2 _detailWeight;
        private readonly float _continentEnd;
        private readonly float _archipelagoEnd;
        private readonly float _zoneBlend;
        private readonly float _coastWarp;
        private readonly float _reliefBase;
        private readonly float _continentMinElevation;
        private readonly Vector2 _archipelagoThreshold;
        private readonly float _deepOcean;
        private readonly float _landScale;
        private readonly float _seaScale;
        private readonly float _mountainStart;
        private readonly float _mountainHeight;
        private readonly float _reliefHeight;
        private readonly float _climateBlur;
        private readonly float _polarStart;
        private readonly float _polarBlur;
        private readonly ClimateBand[] _bands;
        private readonly HeightBiomeRule[] _heightRules;
        private readonly BiomeDefinition _northPolar;
        private readonly BiomeDefinition _southPolar;
        private readonly BiomeDefinition _ocean;
        private readonly bool _hasPolar;
        // Per-biome Height Variation, looked up by reference (Unity's Equals is not for worker threads).
        private readonly BiomeDefinition[] _reliefBiomes;
        private readonly float[] _reliefVariation;

        public HybridWorldShape(HybridWorldSettings settings, int seed)
        {
            Settings = settings;
            _continent = new NoiseField(settings.ContinentNoise, seed, ContinentSalt);
            _detail = new NoiseField(settings.DetailNoise, seed, DetailSalt);
            _mountain = new NoiseField(settings.MountainNoise, seed, MountainSalt);
            _climateBorder = new NoiseField(settings.ClimateBorderNoise, seed, ClimateSalt);
            _polarBorder = new NoiseField(settings.ClimateBorderNoise, seed, PolarSalt);
            _patch = new NoiseField(settings.PatchNoise, seed, PatchSalt);
            _relief = new NoiseField(settings.LocalReliefNoise, seed, ReliefSalt);
            _mix = new NoiseField(settings.BandMixNoise, seed, MixSalt);

            _radius = settings.WorldRadius;
            _detailWeight = settings.DetailWeight;
            _continentEnd = settings.ContinentZoneEnd;
            _archipelagoEnd = settings.ArchipelagoZoneEnd;
            _zoneBlend = settings.ZoneBlend;
            _coastWarp = settings.ContinentCoastWarp;
            _reliefBase = settings.ContinentReliefBase;
            _continentMinElevation = settings.ContinentMinElevation;
            _archipelagoThreshold = settings.ArchipelagoThreshold;
            _deepOcean = settings.DeepOceanElevation;
            _landScale = settings.LandHeightScale;
            _seaScale = settings.SeaDepthScale;
            _mountainStart = settings.MountainStart;
            _mountainHeight = settings.MountainHeight;
            _reliefHeight = settings.LocalReliefHeight;
            _climateBlur = settings.ClimateBorderBlur;
            _polarStart = settings.PolarStart;
            _polarBlur = settings.PolarBorderBlur;
            _bands = new List<ClimateBand>(settings.ClimateBands).ToArray();
            _mixThreshold = new float[_bands.Length];
            for (int i = 0; i < _bands.Length; i++)
            {
                _mixThreshold[i] = _bands[i].MixBiome == null || _bands[i].MixShare <= 0f
                    ? float.MaxValue
                    : NoiseLevelAbove(_mix, _bands[i].MixShare, _radius);
            }
            _heightRules = new List<HeightBiomeRule>(settings.HeightRules).ToArray();
            _northPolar = settings.NorthPolarBiome;
            _southPolar = settings.SouthPolarBiome;
            _ocean = settings.OceanBiome;
            _hasPolar = _northPolar != null || _southPolar != null;

            _reliefBiomes = Biomes.ToArray();
            _reliefVariation = new float[_reliefBiomes.Length];
            for (int i = 0; i < _reliefBiomes.Length; i++)
            {
                _reliefVariation[i] = _reliefBiomes[i].HeightVariation;
            }
        }

        public HybridWorldSettings Settings { get; }
        public float Radius => _radius;

        /// <summary>Lowest the ground can be (deep sea floor with relief), metres from sea level.</summary>
        public float MinHeight => _deepOcean * _seaScale - _reliefHeight - 1f;

        /// <summary>Highest the ground can be, metres above sea level.</summary>
        public float MaxHeight => _landScale + _mountainHeight + _reliefHeight + 1f;

        /// <summary>Every biome this world can produce, each once, in a fixed order (ocean, climate
        /// bands, height rules, polar caps) - stable between sessions, so per-biome ids stay put.</summary>
        public List<BiomeDefinition> Biomes
        {
            get
            {
                var biomes = new List<BiomeDefinition>();
                AddOnce(biomes, Settings.OceanBiome);
                foreach (ClimateBand band in Settings.ClimateBands)
                {
                    AddOnce(biomes, band.Biome);
                }

                foreach (ClimateBand band in Settings.ClimateBands)
                {
                    AddOnce(biomes, band.MixBiome);
                }

                foreach (HeightBiomeRule rule in Settings.HeightRules)
                {
                    AddOnce(biomes, rule.Biome);
                }

                AddOnce(biomes, Settings.NorthPolarBiome);
                AddOnce(biomes, Settings.SouthPolarBiome);
                return biomes;
            }
        }

        private static void AddOnce(List<BiomeDefinition> biomes, BiomeDefinition biome)
        {
            if (biome != null && !biomes.Contains(biome))
            {
                biomes.Add(biome);
            }
        }

        public WorldPoint Sample(float x, float z)
        {
            float radius01 = Mathf.Sqrt(x * x + z * z) / _radius;
            float continent = _continent.Sample(x, z);
            float detail = _detail.Sample(x, z);

            float continentWeight = ContinentWeight(radius01, continent, detail);
            float elevation = LandElevation(radius01, continent, detail, continentWeight);
            float height = ToMetres(elevation) + MountainRise(x, z, continent, elevation);

            bool onIsland = continentWeight < 0.5f;
            float latitude = z / _radius;
            bool polar = IsPolar(x, z, latitude);
            BiomeDefinition biome = elevation > 0f
                ? LandBiome(x, z, radius01, height, onIsland, polar, latitude)
                : _ocean;

            height += LocalRelief(x, z, height, biome);
            return new WorldPoint(height, elevation, radius01, latitude, ZoneOf(radius01), onIsland, polar, biome);
        }

        /// <summary>Sea-level test only - cheaper than Sample (no biome).</summary>
        public bool IsLand(float x, float z)
        {
            float radius01 = Mathf.Sqrt(x * x + z * z) / _radius;
            float continent = _continent.Sample(x, z);
            float detail = _detail.Sample(x, z);
            return LandElevation(radius01, continent, detail, ContinentWeight(radius01, continent, detail)) > 0f;
        }

        public WorldZone ZoneOf(float radius01)
        {
            if (radius01 < _continentEnd)
            {
                return WorldZone.Continent;
            }

            return radius01 < _archipelagoEnd ? WorldZone.Archipelago : WorldZone.Ocean;
        }

        // 1 deep inside the continent zone, 0 outside it. The zone edge is pushed out where the
        // continent noise is high (peninsulas) and in where it is low (bays).
        private float ContinentWeight(float radius01, float continent, float detail)
        {
            float warped = radius01
                + (0.5f - continent) * _coastWarp
                + (0.5f - detail) * _coastWarp * 0.25f;
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_continentEnd - _zoneBlend, _continentEnd + _zoneBlend, warped));
        }

        private float LandElevation(float radius01, float continent, float detail, float continentWeight)
        {
            // Two layers: the detail share grows outwards, so coasts fray into islands.
            float detailShare = Mathf.Lerp(_detailWeight.y, _detailWeight.x, continentWeight);
            float land = continent * (1f - detailShare) + detail * detailShare;

            // Continent zone: guaranteed land, relief from the land value.
            float continentElevation = Mathf.Max(_continentMinElevation, land - _reliefBase);

            // Archipelago zone: the threshold climbs from the inner to the outer edge (and keeps
            // climbing a little past it), leaving fewer and smaller islands.
            float t = Mathf.Clamp((radius01 - _continentEnd) / (_archipelagoEnd - _continentEnd), 0f, 1.5f);
            float threshold = Mathf.LerpUnclamped(_archipelagoThreshold.x, _archipelagoThreshold.y, t);
            float archipelagoElevation = land - threshold;

            float elevation = Mathf.Lerp(archipelagoElevation, continentElevation, continentWeight);

            // Ocean rim: everything slides under water towards the edge, then deep sea beyond it.
            float sink = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_archipelagoEnd, 1f, radius01));
            return Mathf.Lerp(elevation, _deepOcean, sink);
        }

        private float ToMetres(float elevation)
        {
            return elevation > 0f ? elevation * _landScale : elevation * _seaScale;
        }

        // Ridges only where the continent noise is high - the hearts of the big landmasses: the
        // continent and the large islands of the archipelago (small islets come from the detail
        // noise and stay low) - and only on dry land, fading in from the shore.
        private float MountainRise(float x, float z, float continent, float elevation)
        {
            if (elevation <= 0f || continent <= _mountainStart)
            {
                return 0f;
            }

            float mask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_mountainStart, 1f, continent))
                * Mathf.Clamp01(elevation * 20f);
            return _mountain.SampleRidged(x, z) * mask * _mountainHeight;
        }

        private float LocalRelief(float x, float z, float height, BiomeDefinition biome)
        {
            float variation = 0f;
            for (int i = 0; i < _reliefBiomes.Length; i++)
            {
                if (ReferenceEquals(_reliefBiomes[i], biome))
                {
                    variation = _reliefVariation[i];
                    break;
                }
            }

            if (_reliefHeight <= 0f || variation <= 0f)
            {
                return 0f;
            }

            float shore = Mathf.Clamp01(Mathf.Abs(height) / ReliefShoreFade);
            return (_relief.Sample(x, z) - 0.5f) * 2f * _reliefHeight * variation * shore;
        }

        private bool IsPolar(float x, float z, float latitude)
        {
            if (!_hasPolar)
            {
                return false;
            }

            float blurred = Mathf.Abs(latitude) + (_polarBorder.Sample(x, z) - 0.5f) * 2f * _polarBlur;
            return blurred >= _polarStart;
        }

        private BiomeDefinition LandBiome(float x, float z, float radius01, float height, bool onIsland, bool polar, float latitude)
        {
            float patch = _patch.Sample(x, z);
            foreach (HeightBiomeRule rule in _heightRules)
            {
                if (rule.Matches(height, onIsland, polar, patch))
                {
                    return rule.Biome;
                }
            }

            if (polar)
            {
                BiomeDefinition cap = latitude > 0f ? _northPolar : _southPolar;
                if (!(cap is null))
                {
                    return cap;
                }
            }

            return ClimateBiome(x, z, radius01);
        }

        private BiomeDefinition ClimateBiome(float x, float z, float radius01)
        {
            if (_bands.Length == 0)
            {
                return null;
            }

            float climate = radius01 + (_climateBorder.Sample(x, z) - 0.5f) * 2f * _climateBlur;
            for (int i = 0; i < _bands.Length; i++)
            {
                if (climate <= _bands[i].UpTo)
                {
                    return BandBiome(i, x, z);
                }
            }

            return BandBiome(_bands.Length - 1, x, z);
        }

        private BiomeDefinition BandBiome(int band, float x, float z)
        {
            return _mixThreshold[band] < float.MaxValue && _mix.Sample(x, z) >= _mixThreshold[band]
                ? _bands[band].MixBiome
                : _bands[band].Biome;
        }

        // fBm noise bunches around 0.5, so "share" can't be a plain 1 - share cut: the level is read
        // off a sorted sample of the noise over the world instead (deterministic per seed).
        private static float NoiseLevelAbove(NoiseField noise, float share, float radius)
        {
            int side = (int)Mathf.Sqrt(QuantileSamples);
            var values = new float[side * side];
            for (int z = 0; z < side; z++)
            {
                for (int x = 0; x < side; x++)
                {
                    values[z * side + x] = noise.Sample((x + 0.5f) / side * 2f * radius - radius, (z + 0.5f) / side * 2f * radius - radius);
                }
            }

            System.Array.Sort(values);
            int index = Mathf.Clamp(Mathf.RoundToInt((1f - share) * values.Length), 0, values.Length - 1);
            return values[index];
        }
    }
}
