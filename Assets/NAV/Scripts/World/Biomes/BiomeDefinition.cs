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

        public GameObject Prefab => _prefab;

        /// <summary>Chance (0-1) that any given candidate spawn point in this biome instantiates
        /// this prefab.</summary>
        public float Density => _density;

        /// <summary>World-unit spacing of the candidate-point grid this entry is rolled against.
        /// Smaller = more candidate points = more of this prefab for the same Density.</summary>
        public float MinSpacing => _minSpacing;
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

        public GameObject Prefab => _prefab;

        /// <summary>How many of this creature WorldGenerator tries to place in this biome per
        /// generated world (fewer if not enough valid NavMesh points are found).</summary>
        public int Count => _count;
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

        [Header("Terrain Shape")]
        [Tooltip("0 = perfectly flat within this biome, 1 = full detail-noise amplitude from WorldGenerationSettings. Keep Plains low (flat fields), Forest higher (rolling hills), Ocean low (smooth seabed).")]
        [SerializeField, Range(0f, 1f)] private float _heightVariation = 0.5f;

        [Header("Resources")]
        [SerializeField] private List<BiomeResourceSpawn> _resourceSpawns = new();

        [Header("Creatures")]
        [SerializeField] private List<BiomeCreatureSpawn> _creatureSpawns = new();

        public string DisplayName => _displayName;
        public TerrainLayer TerrainLayer => _terrainLayer;
        public bool IsWater => _isWater;
        public float HeightVariation => _heightVariation;
        public IReadOnlyList<BiomeResourceSpawn> ResourceSpawns => _resourceSpawns;
        public IReadOnlyList<BiomeCreatureSpawn> CreatureSpawns => _creatureSpawns;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_displayName))
            {
                _displayName = name;
            }
        }
    }
}
