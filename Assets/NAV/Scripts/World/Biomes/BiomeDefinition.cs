using System;
using System.Collections.Generic;
using UnityEngine;

namespace NAV.World.Biomes
{
    /// <summary>
    /// One entry in a biome's resource spawn list: which resource-node/vegetation prefab can
    /// appear in this biome, how likely it is per candidate spawn point (Density), and how far
    /// apart candidate points are for it (MinSpacing - also the main knob for "how many of
    /// these", since a smaller spacing means more candidate points to roll Density against).
    /// Prefab is expected to be a ready-to-place instance (e.g. an existing ResourceNode prefab
    /// like UNS_Spruce_WoodNode/SM_Rocks_03/Flint_Ore_Rock_01) - WorldGenerator just instantiates
    /// it, it doesn't configure anything on it.
    /// </summary>
    [Serializable]
    public class BiomeResourceSpawn
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField, Range(0f, 1f)] private float _density = 0.1f;
        [SerializeField] private float _minSpacing = 8f;
        [Tooltip("Random uniform size per instance (min, max) - trees of one kind shouldn't all be the same height. (1, 1) = as the prefab.")]
        [SerializeField] private Vector2 _scale = Vector2.one;
        [Tooltip("Only where the ground is this many metres above the water (min, max) - e.g. (0.2, 2) keeps bog iron on the shore. Leave wide for anywhere.")]
        [SerializeField] private Vector2 _heightAboveWater = new(-1000f, 1000f);

        public GameObject Prefab => _prefab;
        public float MinScale => Mathf.Max(0.05f, _scale.x);
        public float MaxScale => Mathf.Max(MinScale, _scale.y);
        public Vector2 HeightAboveWater => _heightAboveWater;

        /// <summary>Chance (0-1) that any given candidate spawn point in this biome instantiates
        /// this prefab.</summary>
        public float Density => _density;

        /// <summary>World-unit spacing of the candidate-point grid this entry is rolled against.
        /// Smaller = more candidate points = more of this prefab for the same Density.</summary>
        public float MinSpacing => _minSpacing;
    }

    /// <summary>
    /// One entry in a biome's decor list: scenery (bushes, mushrooms, stumps, fallen logs,
    /// pebbles) scattered like resources (spacing grid + Density) but not gatherable and not
    /// tracked by the Save system - every load re-scatters it from the seed. Without Keep
    /// Colliders the copy is walk-through (bushes, mushrooms); with it, it blocks like the
    /// prefab does (stumps, logs) and the NavMesh goes around it.
    /// </summary>
    [Serializable]
    public class BiomeDecorSpawn
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField, Range(0f, 1f)] private float _density = 0.1f;
        [SerializeField] private float _minSpacing = 10f;
        [SerializeField] private Vector2 _scale = new(0.8f, 1.2f);
        [SerializeField] private bool _keepColliders;

        public GameObject Prefab => _prefab;
        public float Density => _density;
        public float MinSpacing => _minSpacing;
        public float MinScale => Mathf.Max(0.05f, _scale.x);
        public float MaxScale => Mathf.Max(MinScale, _scale.y);
        public bool KeepColliders => _keepColliders;
    }

    /// <summary>
    /// One entry in a biome's creature spawn list: which creature prefab (a ready-to-place
    /// NavMeshAgent + Creature instance) appears in this biome and how many of it per world.
    /// Unlike resources, creatures are placed by count rather than by a density grid - a
    /// handful of animals per biome is the target, not hundreds.
    /// </summary>
    [Serializable]
    public class BiomeCreatureSpawn
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField, Min(0)] private int _count = 3;
        [Tooltip("How many live in this biome at night. Below 0 = the same as Count. More = night is more dangerous (wolves); fewer = they sleep (deer).")]
        [SerializeField] private int _nightCount = -1;
        [Tooltip("They appear in groups of this many (a pack), placed together.")]
        [SerializeField] private Vector2Int _groupSize = new(1, 1);
        [Tooltip("At most one group is added back to this entry every this many seconds when below its count (killed, or night began).")]
        [SerializeField, Min(5f)] private float _respawnSeconds = 120f;

        public GameObject Prefab => _prefab;

        /// <summary>How many of this creature WorldGenerator tries to place in this biome per
        /// generated world (fewer if not enough valid NavMesh points are found) - and how many
        /// CreaturePopulation keeps alive there by day.</summary>
        public int Count => _count;

        public int GetCount(bool night)
        {
            return night && _nightCount >= 0 ? _nightCount : _count;
        }

        public int MinGroupSize => Mathf.Max(1, _groupSize.x);
        public int MaxGroupSize => Mathf.Max(MinGroupSize, _groupSize.y);
        public float RespawnSeconds => _respawnSeconds;
    }

    /// <summary>
    /// Static data for one biome: which ground texture paints it onto the Terrain, whether it
    /// counts as water, how much the terrain's detail noise is allowed to move height around
    /// within this biome (HeightVariation - see WorldGenerator, this is what keeps Plains flat
    /// while Forest gets rolling hills instead of one uniform mountain range everywhere), and
    /// which resource-node/vegetation prefabs (ResourceSpawns) and creatures (CreatureSpawns)
    /// can spawn in it. Further biome content (ambient audio - see ARCHITECTURE_v0.1.md's World
    /// Generation pipeline) is later pipeline-stage work.
    /// </summary>
    [CreateAssetMenu(fileName = "BiomeDefinition", menuName = "NAV/World/Biome Definition")]
    public class BiomeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _displayName;
        [SerializeField] private TerrainLayer _terrainLayer;
        [SerializeField] private bool _isWater;
        [Tooltip("Colour of this biome on the world map (and the generator's preview).")]
        [SerializeField] private Color _mapColor = new(0.5f, 0.6f, 0.4f, 1f);

        [Header("Terrain Shape")]
        [Tooltip("0 = perfectly flat within this biome, 1 = full detail-noise amplitude from WorldGenerationSettings. Keep Plains low (flat fields), Forest higher (rolling hills), Ocean low (smooth seabed).")]
        [SerializeField, Range(0f, 1f)] private float _heightVariation = 0.5f;

        [Header("Resources")]
        [SerializeField] private List<BiomeResourceSpawn> _resourceSpawns = new();

        [Header("Decor")]
        [SerializeField] private List<BiomeDecorSpawn> _decorSpawns = new();
        [Tooltip("Chance (0-1) per terrain detail cell (~2 m) of a grass clump.")]
        [SerializeField, Range(0f, 1f)] private float _grassDensity;
        [Tooltip("Chance (0-1) per terrain detail cell of a flower.")]
        [SerializeField, Range(0f, 1f)] private float _flowerDensity;

        [Header("Creatures")]
        [SerializeField] private List<BiomeCreatureSpawn> _creatureSpawns = new();

        [Header("Atmosphere")]
        [Tooltip("Extra fog while the camera is in this biome (exponential-squared density; 0.02 = you see ~100 m). AtmosphereFog adds it to the view-distance edge, weather and morning haze. 0 = none.")]
        [SerializeField, Min(0f)] private float _fogDensity;
        [Tooltip("Multiplies the sky-matched fog colour in this biome (0.5 grey = unchanged, darker/greener = gloomier); alpha = how strongly (0 = not at all). Multiplied, so it still darkens at night.")]
        [SerializeField] private Color _fogColor = new(0.5f, 0.5f, 0.5f, 0f);
        [Tooltip("How much of the daylight this biome swallows (0 = none, 1 = night at noon) - the black forest's eternal dusk. AtmosphereFog dims the sun and ambient light by it and DayNightGrading leans towards the night look.")]
        [SerializeField, Range(0f, 1f)] private float _twilight;

        [Header("Climate")]
        [Tooltip("How cold it is here (PlayerCold): 0 = mild, 1 = cold (mountains - a warm cloak keeps it off), 1.5 = bitter (snowy peaks, permafrost - cloak and hood).")]
        [SerializeField, Range(0f, 2f)] private float _cold;

        [Header("Ambience")]
        [Tooltip("Loop heard here by day (birds, cicadas, the sea) - AmbienceController blends the biomes around the listener.")]
        [SerializeField] private AudioClip _dayAmbience;
        [Tooltip("Loop heard here at night (crickets, owls); the sea keeps its own.")]
        [SerializeField] private AudioClip _nightAmbience;
        [SerializeField, Range(0f, 1f)] private float _ambienceVolume = 0.5f;
        [Tooltip("Wind loop here (open fields: calm wind; forests: wind in the trees) - louder in bad weather.")]
        [SerializeField] private AudioClip _windAmbience;
        [SerializeField, Range(0f, 1f)] private float _windVolume = 0.3f;

        public string DisplayName => NAV.Core.Localization.Loc.T(_displayName);
        public TerrainLayer TerrainLayer => _terrainLayer;
        public bool IsWater => _isWater;
        public Color MapColor => _mapColor;
        public float HeightVariation => _heightVariation;
        public IReadOnlyList<BiomeResourceSpawn> ResourceSpawns => _resourceSpawns;
        public IReadOnlyList<BiomeCreatureSpawn> CreatureSpawns => _creatureSpawns;
        public IReadOnlyList<BiomeDecorSpawn> DecorSpawns => _decorSpawns;
        public float GrassDensity => _grassDensity;
        public float FlowerDensity => _flowerDensity;
        public float FogDensity => _fogDensity;
        public Color FogColor => _fogColor;
        public float Twilight => _twilight;
        public float Cold => _cold;
        public AudioClip DayAmbience => _dayAmbience;
        public AudioClip NightAmbience => _nightAmbience;
        public float AmbienceVolume => _ambienceVolume;
        public AudioClip WindAmbience => _windAmbience;
        public float WindVolume => _windVolume;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_displayName))
            {
                _displayName = name;
            }
        }
    }
}
