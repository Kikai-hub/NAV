using System;
using NAV.World.Biomes;
using UnityEngine;

namespace NAV.World.Generation.Hybrid
{
    /// <summary>Which kind of land a rule applies to.</summary>
    public enum LandmassFilter
    {
        Any,
        Continent,
        Island
    }

    /// <summary>
    /// A ring of the radial climate: from the previous band's edge out to Up To (fraction of the
    /// world radius, after border noise). The list runs from the safe centre to the dangerous rim.
    /// </summary>
    [Serializable]
    public class ClimateBand
    {
        [SerializeField] private BiomeDefinition _biome;
        [Tooltip("Outer edge of this band, as a fraction of the world radius (0 = centre, 1 = rim).")]
        [SerializeField, Range(0f, 1.2f)] private float _upTo = 0.3f;
        [Tooltip("Optional second biome scattered through this band in patches (forest copses in the plains).")]
        [SerializeField] private BiomeDefinition _mixBiome;
        [Tooltip("Share of the band's land the mix biome takes (0.25 = one part in four).")]
        [SerializeField, Range(0f, 1f)] private float _mixShare;

        public ClimateBand(BiomeDefinition biome, float upTo, BiomeDefinition mixBiome = null, float mixShare = 0f)
        {
            _biome = biome;
            _upTo = upTo;
            _mixBiome = mixBiome;
            _mixShare = mixShare;
        }

        public BiomeDefinition Biome => _biome;
        public float UpTo => _upTo;
        public BiomeDefinition MixBiome => _mixBiome;
        public float MixShare => _mixShare;
    }

    /// <summary>
    /// A biome that height decides, overriding the climate: mountains only high on continents,
    /// swamps only in low ground on islands. Rules are checked in list order; the first match wins.
    /// </summary>
    [Serializable]
    public class HeightBiomeRule
    {
        [SerializeField] private BiomeDefinition _biome;
        [Tooltip("Metres above sea level.")]
        [SerializeField] private float _minHeight = -1000f;
        [SerializeField] private float _maxHeight = 10000f;
        [SerializeField] private LandmassFilter _landmass = LandmassFilter.Any;
        [Tooltip("Share of the matching ground this biome takes (patchy noise). 1 = all of it.")]
        [SerializeField, Range(0f, 1f)] private float _coverage = 1f;
        [Tooltip("Not inside the polar caps (a swamp in permafrost makes no sense).")]
        [SerializeField] private bool _excludePolar;

        public HeightBiomeRule(BiomeDefinition biome, float minHeight, float maxHeight, LandmassFilter landmass, float coverage, bool excludePolar)
        {
            _biome = biome;
            _minHeight = minHeight;
            _maxHeight = maxHeight;
            _landmass = landmass;
            _coverage = coverage;
            _excludePolar = excludePolar;
        }

        public BiomeDefinition Biome => _biome;
        public float MinHeight => _minHeight;
        public float MaxHeight => _maxHeight;
        public LandmassFilter Landmass => _landmass;
        public float Coverage => _coverage;
        public bool ExcludePolar => _excludePolar;

        public bool Matches(float height, bool onIsland, bool polar, float patch)
        {
            // "is null", not "== null": Unity's == is an engine call, and this runs on worker threads.
            if (_biome is null || height < _minHeight || height > _maxHeight || (polar && _excludePolar))
            {
                return false;
            }

            if ((_landmass == LandmassFilter.Continent && onIsland) || (_landmass == LandmassFilter.Island && !onIsland))
            {
                return false;
            }

            // Patch noise above (1 - coverage) - Coverage 0.4 keeps the 40% of spots with the highest noise.
            return patch >= 1f - _coverage;
        }
    }
}
